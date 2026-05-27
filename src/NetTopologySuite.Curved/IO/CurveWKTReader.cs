using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NetTopologySuite.Curved.Compat.IO;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using RTools_NTS.Util;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Reads curve geometries from Well-Known Text.
    /// </summary>
    /// <remarks>
    /// This reader <i>composes</i> a standard <see cref="WKTReader"/> rather than deriving from it.
    /// Curve-specific tagged text (<c>CIRCULARSTRING</c>, <c>COMPOUNDCURVE</c>, <c>CURVEPOLYGON</c>,
    /// <c>MULTICURVE</c>, <c>MULTISURFACE</c>) is parsed here; every other geometry is delegated to the
    /// contained standard reader. The previous design overrode <c>WKTReader.ReadOtherGeometryText</c>,
    /// a hook that no longer exists on upstream <c>develop</c> (and which required the now-internal
    /// <c>TokenStream</c>), so composition is the only forward-compatible shape.
    /// </remarks>
    public class CurveWKTReader
    {
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        private readonly NtsCurveGeometryServices _services;
        private readonly WKTReader _other;

        // Mirrors the WKTReader defaults this reader was previously seeded with.
        private const bool AllowOldNtsCoordinateSyntax = true;

        /// <summary>
        /// Creates an instance of this class using the provided geometry services object.
        /// </summary>
        /// <param name="geometryServices">A curve geometry services object.</param>
        public CurveWKTReader(NtsCurveGeometryServices geometryServices)
        {
            _services = geometryServices ?? throw new ArgumentNullException(nameof(geometryServices));
            _other = new WKTReader(geometryServices);
        }

        /// <summary>
        /// Converts a Well-known Text representation to a <c>Geometry</c>.
        /// </summary>
        public Geometry Read(string wellKnownText)
        {
            if (!StartsWithCurveType(wellKnownText))
                return _other.Read(wellKnownText);

            using (var reader = new StringReader(wellKnownText))
                return Read(reader);
        }

        /// <summary>
        /// Converts a Well-known Text representation to a <c>Geometry</c>.
        /// </summary>
        public Geometry Read(Stream stream)
        {
            using (var reader = new StreamReader(stream))
                return Read(reader);
        }

        /// <summary>
        /// Converts a Well-known Text representation to a <c>Geometry</c>.
        /// </summary>
        public Geometry Read(TextReader reader)
        {
            string text = reader.ReadToEnd();
            if (!StartsWithCurveType(text))
                return _other.Read(text);

            try
            {
                using (var sr = new StringReader(text))
                {
                    var tokens = Tokenizer(sr);
                    return ReadCurveGeometryTaggedText(tokens);
                }
            }
            catch (IOException e)
            {
                throw new ParseException(e.ToString());
            }
        }

        private static bool StartsWithCurveType(string wkt)
        {
            if (string.IsNullOrEmpty(wkt))
                return false;

            int i = 0;
            while (i < wkt.Length && char.IsWhiteSpace(wkt[i])) i++;

            // Skip an optional "SRID=<n>;" prefix before the geometry type keyword.
            if (i + 4 <= wkt.Length && string.Compare(wkt, i, "SRID", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
            {
                int semi = wkt.IndexOf(';', i);
                if (semi >= 0)
                {
                    i = semi + 1;
                    while (i < wkt.Length && char.IsWhiteSpace(wkt[i])) i++;
                }
            }

            int start = i;
            while (i < wkt.Length && char.IsLetter(wkt[i])) i++;
            string type = wkt.Substring(start, i - start);

            return type.StartsWith("CIRCULARSTRING", StringComparison.OrdinalIgnoreCase)
                || type.StartsWith("COMPOUNDCURVE", StringComparison.OrdinalIgnoreCase)
                || type.StartsWith("CURVEPOLYGON", StringComparison.OrdinalIgnoreCase)
                || type.StartsWith("MULTICURVE", StringComparison.OrdinalIgnoreCase)
                || type.StartsWith("MULTISURFACE", StringComparison.OrdinalIgnoreCase);
        }

        private static TokenStream Tokenizer(TextReader reader)
        {
            var tokenizer = new StreamTokenizer(reader);
            tokenizer.Settings.ResetCharTypeTable();
            tokenizer.Settings.WordChars('a', 'z');
            tokenizer.Settings.WordChars('A', 'Z');
            tokenizer.Settings.WordChars('0', '9');
            tokenizer.Settings.WordChars('-', '-');
            tokenizer.Settings.WordChars('+', '+');
            tokenizer.Settings.WordChars('.', '.');
            tokenizer.Settings.WhitespaceChars(0, ' ');
            tokenizer.Settings.CommentChar('#');
            return new TokenStream(tokenizer.GetEnumerator());
        }

        private Geometry ReadCurveGeometryTaggedText(TokenStream tokens)
        {
            int srid;
            string type = GetNextWord(tokens);
            if (type.Equals("SRID", StringComparison.OrdinalIgnoreCase))
            {
                var tok = tokens.NextToken(true);
                if (!(tok is CharToken eq && (char)eq.Object == '='))
                    throw new ParseException("Expected '=' after SRID");

                srid = Convert.ToInt32(GetNextNumber(tokens));
                tok = tokens.NextToken(true);
                if (!(tok is CharToken semi && (char)semi.Object == ';'))
                    throw new ParseException("Expected ';' after SRID value");

                type = GetNextWord(tokens);
            }
            else
            {
                srid = _services.DefaultSRID;
            }

            var ordinateFlags = Ordinates.XY;
            if (type.EndsWith(WKTConstants.ZM, StringComparison.OrdinalIgnoreCase))
                ordinateFlags = Ordinates.XYZM;
            else if (type.EndsWith(WKTConstants.Z, StringComparison.OrdinalIgnoreCase))
                ordinateFlags = Ordinates.XYZ;
            else if (type.EndsWith(WKTConstants.M, StringComparison.OrdinalIgnoreCase))
                ordinateFlags = Ordinates.XYM;

            if (ordinateFlags == Ordinates.XY)
                ordinateFlags = GetNextOrdinateFlags(tokens);

            var csFactory = (_services.DefaultCoordinateSequenceFactory.Ordinates & ordinateFlags) == ordinateFlags
                ? _services.DefaultCoordinateSequenceFactory
                : CoordinateArraySequenceFactory.Instance;
            var factory = _services.CreateGeometryFactory(_services.DefaultPrecisionModel, srid, csFactory);

            if (!(factory is CurveGeometryFactory curveFactory))
                throw new ArgumentException("Not a CurveGeometryFactory");

            try
            {
                if (type.StartsWith("CIRCULARSTRING", StringComparison.OrdinalIgnoreCase))
                    return ReadCircularStringText(tokens, curveFactory, ordinateFlags);
                if (type.StartsWith("COMPOUNDCURVE", StringComparison.OrdinalIgnoreCase))
                    return ReadCompoundCurveText(tokens, curveFactory, ordinateFlags);
                if (type.StartsWith("CURVEPOLYGON", StringComparison.OrdinalIgnoreCase))
                    return ReadCurvePolygonText(tokens, curveFactory, ordinateFlags);
                if (type.StartsWith("MULTICURVE", StringComparison.OrdinalIgnoreCase))
                    return ReadMultiCurveText(tokens, curveFactory, ordinateFlags);
                if (type.StartsWith("MULTISURFACE", StringComparison.OrdinalIgnoreCase))
                    return ReadMultiSurfaceText(tokens, curveFactory, ordinateFlags);
            }
            catch (Exception e)
            {
                throw new ParseException(e);
            }

            throw new ParseException("Unknown type: " + type);
        }

        private CircularString ReadCircularStringText(TokenStream tokens, CurveGeometryFactory factory, Ordinates ordinateFlags)
        {
            var sequence = GetCoordinateSequence(factory, tokens, ordinateFlags);
            return factory.CreateCircularString(sequence);
        }

        private CompoundCurve ReadCompoundCurveText(TokenStream tokens, CurveGeometryFactory factory, Ordinates ordinateFlags)
        {
            string nextToken = GetNextEmptyOrOpener(tokens);
            if (nextToken.Equals(WKTConstants.EMPTY))
                return factory.CreateCompoundCurve();

            var curves = new List<Curve>();
            do
            {
                var curve = ReadCurveText(tokens, factory, ordinateFlags, false);
                curves.Add(curve);
                nextToken = GetNextCloserOrComma(tokens);
            }
            while (nextToken.Equals(","));

            return factory.CreateCompoundCurve(curves.ToArray());
        }

        private Curve ReadCurveText(TokenStream tokens, CurveGeometryFactory factory, Ordinates ordinateFlags, bool allowCompoundCurve)
        {
            string current = LookAheadWord(tokens);

            if (current == "EMPTY" || current == "(")
            {
                var sequence = GetCoordinateSequence(factory, tokens, ordinateFlags);
                return factory.CreateLineString(sequence);
            }

            if (current.StartsWith("CIRCULARSTRING"))
            {
                GetNextWord(tokens);
                return ReadCircularStringText(tokens, factory, ordinateFlags);
            }

            if (current.StartsWith("COMPOUNDCURVE"))
            {
                if (!allowCompoundCurve)
                    throw new ParseException("CompoundCurve not allowed at this position");
                GetNextWord(tokens);
                return ReadCompoundCurveText(tokens, factory, ordinateFlags);
            }

            throw new ParseException($"Unexpected token: {current}");
        }

        private CurvePolygon ReadCurvePolygonText(TokenStream tokens, CurveGeometryFactory factory, Ordinates ordinateFlags)
        {
            string nextToken = GetNextEmptyOrOpener(tokens);
            if (nextToken.Equals(WKTConstants.EMPTY))
                return factory.CreateCurvePolygon();

            var holes = new List<Curve>();
            var shell = ReadCurveText(tokens, factory, ordinateFlags, true);
            nextToken = GetNextCloserOrComma(tokens);
            while (nextToken.Equals(","))
            {
                var hole = ReadCurveText(tokens, factory, ordinateFlags, true);
                holes.Add(hole);
                nextToken = GetNextCloserOrComma(tokens);
            }
            return factory.CreateCurvePolygon(shell, holes.ToArray());
        }

        private MultiCurve ReadMultiCurveText(TokenStream tokens, CurveGeometryFactory factory, Ordinates ordinateFlags)
        {
            string nextToken = GetNextEmptyOrOpener(tokens);
            if (nextToken.Equals(WKTConstants.EMPTY))
                return factory.CreateMultiCurve();

            var curves = new List<Geometry>();
            do
            {
                var curve = ReadCurveText(tokens, factory, ordinateFlags, true);
                curves.Add(curve);
                nextToken = GetNextCloserOrComma(tokens);
            }
            while (nextToken.Equals(","));

            return factory.CreateMultiCurve(curves.ToArray());
        }

        private MultiSurface ReadMultiSurfaceText(TokenStream tokens, CurveGeometryFactory factory, Ordinates ordinateFlags)
        {
            string nextToken = GetNextEmptyOrOpener(tokens);
            if (nextToken.Equals(WKTConstants.EMPTY))
                return factory.CreateMultiSurface();

            var surfaces = new List<Geometry>();
            do
            {
                string current = LookAheadWord(tokens);
                if (current == "EMPTY" || current == "(")
                    surfaces.Add(ReadPolygonText(tokens, factory, ordinateFlags));
                else if (current.StartsWith("CURVEPOLYGON"))
                {
                    GetNextWord(tokens);
                    surfaces.Add(ReadCurvePolygonText(tokens, factory, ordinateFlags));
                }
                else
                    throw new ParseException($"Unexpected token: {current}");

                nextToken = GetNextCloserOrComma(tokens);
            }
            while (nextToken.Equals(","));

            return factory.CreateMultiSurface(surfaces.ToArray());
        }

        private Polygon ReadPolygonText(TokenStream tokens, GeometryFactory factory, Ordinates ordinateFlags)
        {
            string nextToken = GetNextEmptyOrOpener(tokens);
            if (nextToken.Equals(WKTConstants.EMPTY))
                return factory.CreatePolygon();

            var holes = new List<LinearRing>();
            var shell = ReadLinearRingText(tokens, factory, ordinateFlags);
            nextToken = GetNextCloserOrComma(tokens);
            while (nextToken.Equals(","))
            {
                var hole = ReadLinearRingText(tokens, factory, ordinateFlags);
                holes.Add(hole);
                nextToken = GetNextCloserOrComma(tokens);
            }
            return factory.CreatePolygon(shell, holes.ToArray());
        }

        private LinearRing ReadLinearRingText(TokenStream tokens, GeometryFactory factory, Ordinates ordinateFlags)
        {
            var sequence = GetCoordinateSequence(factory, tokens, ordinateFlags);
            return factory.CreateLinearRing(sequence);
        }

        private CoordinateSequence GetCoordinateSequence(GeometryFactory factory, TokenStream tokens, Ordinates ordinateFlags)
        {
            if (GetNextEmptyOrOpener(tokens).Equals(WKTConstants.EMPTY))
                return factory.CoordinateSequenceFactory.Create(0, ToDimension(ordinateFlags), ordinateFlags.HasFlag(Ordinates.M) ? 1 : 0);

            var coordinates = new List<CoordinateSequence>();
            do
            {
                coordinates.Add(GetCoordinate(factory, tokens, ordinateFlags, false));
            }
            while (GetNextCloserOrComma(tokens).Equals(","));

            return MergeSequences(factory, coordinates, ordinateFlags);
        }

        private CoordinateSequence GetCoordinate(GeometryFactory factory, TokenStream tokens, Ordinates ordinateFlags, bool tryParen)
        {
            bool opened = false;
            if (tryParen && IsOpenerNext(tokens))
            {
                tokens.NextToken(true);
                opened = true;
            }

            int offsetM = ordinateFlags.HasFlag(Ordinates.Z) ? 1 : 0;
            var sequence = factory.CoordinateSequenceFactory.Create(1, ToDimension(ordinateFlags), ordinateFlags.HasFlag(Ordinates.M) ? 1 : 0);
            sequence.SetOrdinate(0, 0, factory.PrecisionModel.MakePrecise(GetNextNumber(tokens)));
            sequence.SetOrdinate(0, 1, factory.PrecisionModel.MakePrecise(GetNextNumber(tokens)));

            if (ordinateFlags.HasFlag(Ordinates.Z))
                sequence.SetOrdinate(0, 2, GetNextNumber(tokens));

            if (ordinateFlags.HasFlag(Ordinates.M))
                sequence.SetOrdinate(0, 2 + offsetM, GetNextNumber(tokens));

            if (ordinateFlags == Ordinates.XY && AllowOldNtsCoordinateSyntax && IsNumberNext(tokens))
                sequence.SetOrdinate(0, 2, GetNextNumber(tokens));

            if (opened)
                GetNextCloser(tokens);

            return sequence;
        }

        private static int ToDimension(Ordinates ordinateFlags)
        {
            int dimension = 2;
            if (ordinateFlags.HasFlag(Ordinates.Z))
                dimension++;
            if (ordinateFlags.HasFlag(Ordinates.M))
                dimension++;

            if (dimension == 2 && AllowOldNtsCoordinateSyntax)
                dimension++;

            return dimension;
        }

        private static CoordinateSequence MergeSequences(GeometryFactory factory, List<CoordinateSequence> sequences, Ordinates ordinateFlags)
        {
            if (sequences == null || sequences.Count == 0)
                return factory.CoordinateSequenceFactory.Create(0, ToDimension(ordinateFlags), ordinateFlags.HasFlag(Ordinates.M) ? 1 : 0);

            if (sequences.Count == 1)
                return sequences[0];

            Ordinates mergeOrdinates;
            if (AllowOldNtsCoordinateSyntax && ordinateFlags == Ordinates.XY)
            {
                mergeOrdinates = ordinateFlags;
                foreach (var seq in sequences)
                {
                    if (seq.HasZ)
                    {
                        mergeOrdinates |= Ordinates.Z;
                        break;
                    }
                }
            }
            else
            {
                mergeOrdinates = ordinateFlags;
            }

            var sequence = factory.CoordinateSequenceFactory.Create(sequences.Count, ToDimension(mergeOrdinates), mergeOrdinates.HasFlag(Ordinates.M) ? 1 : 0);

            int offsetM = 2 + (mergeOrdinates.HasFlag(Ordinates.Z) ? 1 : 0);
            for (int i = 0; i < sequences.Count; i++)
            {
                var item = sequences[i];
                sequence.SetOrdinate(i, 0, item.GetOrdinate(0, 0));
                sequence.SetOrdinate(i, 1, item.GetOrdinate(0, 1));
                if (mergeOrdinates.HasFlag(Ordinates.Z))
                    sequence.SetOrdinate(i, 2, item.GetOrdinate(0, 2));
                if (mergeOrdinates.HasFlag(Ordinates.M))
                    sequence.SetOrdinate(i, offsetM, item.GetOrdinate(0, offsetM));
            }

            return sequence;
        }

        private static bool IsNumberNext(TokenStream tokens)
        {
            return tokens.NextToken(false) is WordToken;
        }

        private static bool IsOpenerNext(TokenStream tokens)
        {
            return tokens.NextToken(false) is CharToken charToken &&
                   charToken.Object is char c &&
                   c == '(';
        }

        private static double GetNextNumber(TokenStream tokens)
        {
            var token = tokens.NextToken(true);
            switch (token)
            {
                case WordToken wordToken:
                    if (wordToken.StringValue.Equals("NaN", StringComparison.OrdinalIgnoreCase))
                        return double.NaN;
                    if (wordToken.StringValue.Equals("Inf", StringComparison.OrdinalIgnoreCase))
                        return double.PositiveInfinity;
                    if (wordToken.StringValue.Equals("-Inf", StringComparison.OrdinalIgnoreCase))
                        return double.NegativeInfinity;
                    if (double.TryParse(wordToken.StringValue, NumberStyles.Float | NumberStyles.AllowThousands, InvariantCulture, out double val))
                        return val;

                    throw new ParseException($"Invalid number: {wordToken.StringValue}");

                default:
                    throw new ParseException($"Expected number but found {token?.ToDebugString() ?? "the end of input"}");
            }
        }

        private static string GetNextEmptyOrOpener(TokenStream tokens)
        {
            string nextWord = GetNextWord(tokens);
            if (nextWord.Equals(WKTConstants.Z, StringComparison.OrdinalIgnoreCase))
                nextWord = GetNextWord(tokens);
            else if (nextWord.Equals(WKTConstants.M, StringComparison.OrdinalIgnoreCase))
                nextWord = GetNextWord(tokens);
            else if (nextWord.Equals(WKTConstants.ZM, StringComparison.OrdinalIgnoreCase))
                nextWord = GetNextWord(tokens);

            if (nextWord.Equals(WKTConstants.EMPTY) || nextWord.Equals("("))
                return nextWord;
            throw new ParseException($"Expected '{WKTConstants.EMPTY}' or '(' but encountered '" + nextWord + "'");
        }

        private static Ordinates GetNextOrdinateFlags(TokenStream tokens)
        {
            string nextWord = LookAheadWord(tokens);
            if (nextWord.Equals(WKTConstants.Z, StringComparison.OrdinalIgnoreCase))
            {
                tokens.NextToken(true);
                return Ordinates.XYZ;
            }
            if (nextWord.Equals(WKTConstants.M, StringComparison.OrdinalIgnoreCase))
            {
                tokens.NextToken(true);
                return Ordinates.XYM;
            }
            if (nextWord.Equals(WKTConstants.ZM, StringComparison.OrdinalIgnoreCase))
            {
                tokens.NextToken(true);
                return Ordinates.XYZM;
            }
            return Ordinates.XY;
        }

        private static string LookAheadWord(TokenStream tokens)
        {
            return GetNextWord(tokens, false);
        }

        private static string GetNextCloserOrComma(TokenStream tokens)
        {
            string nextWord = GetNextWord(tokens);
            if (nextWord.Equals(",") || nextWord.Equals(")"))
                return nextWord;

            throw new ParseException("Expected ')' or ',' but encountered '" + nextWord + "'");
        }

        private static string GetNextCloser(TokenStream tokens)
        {
            string nextWord = GetNextWord(tokens);
            if (nextWord.Equals(")"))
                return nextWord;
            throw new ParseException("Expected ')' but encountered '" + nextWord + "'");
        }

        private static string GetNextWord(TokenStream tokens, bool advance = true)
        {
            var token = tokens.NextToken(advance);
            switch (token)
            {
                case WordToken wordToken:
                    if (wordToken.StringValue.Equals(WKTConstants.EMPTY, StringComparison.OrdinalIgnoreCase))
                        return WKTConstants.EMPTY;
                    return wordToken.StringValue;

                case CharToken charToken when charToken.Object is char c && (c == '(' || c == ')' || c == ','):
                    return charToken.StringValue;

                default:
                    throw new ParseException($"Expected a word but encountered {token?.ToDebugString() ?? "the end of input"}");
            }
        }
    }
}
