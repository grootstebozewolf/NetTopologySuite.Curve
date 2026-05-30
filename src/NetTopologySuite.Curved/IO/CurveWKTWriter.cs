using System;
using System.IO;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Writes curve geometries to Well-Known Text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Structural sibling of <see cref="CurveWKTReader"/>: composes a standard
    /// <see cref="WKTWriter"/> rather than deriving from it.  Curve-specific tagged
    /// text (<c>CIRCULARSTRING</c>, <c>COMPOUNDCURVE</c>, <c>CURVEPOLYGON</c>,
    /// <c>MULTICURVE</c>, <c>MULTISURFACE</c>) is written here; every other
    /// geometry is delegated to the contained standard writer.  The previous
    /// design (<c>WKTWriterEx</c>) overrode
    /// <c>WKTWriter.AppendOtherGeometryTaggedText</c>, a hook that is removed on
    /// upstream <c>develop</c>, so composition is the only forward-compatible
    /// shape (mirrors Session 5's <c>CurveWKTReader</c> shift).
    /// </para>
    /// <para>
    /// <c>CIRCULARSTRING</c>, <c>COMPOUNDCURVE</c>, <c>CURVEPOLYGON</c>,
    /// <c>MULTICURVE</c>, and <c>MULTISURFACE</c> are SQL/MM Spatial
    /// (ISO/IEC 13249-3) types, not OGC SFA ISO 19125.
    /// </para>
    /// </remarks>
    public class CurveWKTWriter
    {
        private readonly WKTWriter _inner;
        private readonly bool _mssql;

        /// <summary>
        /// Returns a new writer configured for SQL Server-style output (4 dimensions,
        /// mssql flag set).
        /// </summary>
        public static CurveWKTWriter ForSqlServer()
        {
            return new CurveWKTWriter(4, true);
        }

        /// <summary>
        /// Creates an instance of this class which is writing at most 2 dimensions.
        /// </summary>
        public CurveWKTWriter()
            : this(2, false)
        {
        }

        /// <summary>
        /// Creates an instance of this class which is writing at most
        /// <paramref name="outputDimension"/> dimensions.
        /// </summary>
        /// <param name="outputDimension">Number of dimensions written.</param>
        public CurveWKTWriter(int outputDimension)
            : this(outputDimension, false)
        {
        }

        /// <summary>
        /// Creates an instance of this class which is writing at most
        /// <paramref name="outputDimension"/> dimensions, optionally in SQL Server
        /// format.
        /// </summary>
        /// <param name="outputDimension">Number of dimensions written.</param>
        /// <param name="mssql">A flag indicating if SQLServer WKT should be written.</param>
        public CurveWKTWriter(int outputDimension, bool mssql)
        {
            // WKTWriter's (int, bool) ctor is `protected`, so the contained inner writer can
            // only be constructed via the public (int) ctor.  The `mssql` flag here governs
            // this writer's own NaN-replacement (" NULL" vs " NaN") for curve types; non-curve
            // types passed to the inner writer get the inner's default behaviour.
            _inner = new WKTWriter(outputDimension);
            _mssql = mssql;
        }

        /// <summary>
        /// Gets or sets the ordinates the writer is allowed to emit.
        /// Mirrors <see cref="WKTWriter.OutputOrdinates"/> and forwards the value
        /// to the inner writer for non-curve geometries.
        /// </summary>
        public Ordinates OutputOrdinates
        {
            get => _inner.OutputOrdinates;
            set => _inner.OutputOrdinates = value;
        }

        /// <summary>
        /// Converts a <see cref="Geometry"/> to its Well-known Text representation.
        /// </summary>
        public string Write(Geometry geometry)
        {
            using (var sw = new StringWriter())
            {
                Write(geometry, sw);
                return sw.ToString();
            }
        }

        /// <summary>
        /// Writes the Well-known Text representation of <paramref name="geometry"/>
        /// to <paramref name="writer"/>.
        /// </summary>
        public void Write(Geometry geometry, TextWriter writer)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            if (IsCurveType(geometry))
            {
                var outputOrdinates = ComputeOutputOrdinates(geometry);
                var ordinateFormat = CreateOrdinateFormat(geometry.Factory.PrecisionModel);
                WriteGeometryTaggedText(geometry, outputOrdinates, true, writer, ordinateFormat);
            }
            else
            {
                _inner.Write(geometry, writer);
            }
        }

        // ---------------------------------------------------------------------
        // Curve type dispatch.
        // ---------------------------------------------------------------------

        private static bool IsCurveType(Geometry g) =>
            g is CircularString
            || g is CompoundCurve
            || g is CurvePolygon
            || g is MultiCurve
            || g is MultiSurface;

        private void WriteGeometryTaggedText(Geometry g, Ordinates outputOrdinates, bool topLevel,
            TextWriter writer, OrdinateFormat ordinateFormat)
        {
            switch (g)
            {
                case CircularString cs:
                    WriteCircularStringTaggedText(cs, outputOrdinates, topLevel, writer, ordinateFormat);
                    break;
                case CompoundCurve cc:
                    WriteCompoundCurveTaggedText(cc, outputOrdinates, topLevel, writer, ordinateFormat);
                    break;
                case CurvePolygon cp:
                    WriteCurvePolygonTaggedText(cp, outputOrdinates, topLevel, writer, ordinateFormat);
                    break;
                case MultiCurve mc:
                    WriteMultiCurveTaggedText(mc, outputOrdinates, topLevel, writer, ordinateFormat);
                    break;
                case MultiSurface ms:
                    WriteMultiSurfaceTaggedText(ms, outputOrdinates, topLevel, writer, ordinateFormat);
                    break;
                default:
                    // Non-curve child of a curve container (e.g. LineString in COMPOUNDCURVE).
                    // Delegate the curve-list-rendering helper.
                    WriteCurveText(g, outputOrdinates, writer, ordinateFormat);
                    break;
            }
        }

        private void WriteCircularStringTaggedText(CircularString cs, Ordinates outputOrdinates, bool topLevel,
            TextWriter writer, OrdinateFormat ordinateFormat)
        {
            writer.Write("CIRCULARSTRING ");
            if (topLevel) WriteOrdinateTag(outputOrdinates, writer);
            WriteSequenceText(cs.ControlPoints, outputOrdinates, writer, ordinateFormat);
        }

        private void WriteCompoundCurveTaggedText(CompoundCurve cc, Ordinates outputOrdinates, bool topLevel,
            TextWriter writer, OrdinateFormat ordinateFormat)
        {
            writer.Write("COMPOUNDCURVE ");
            if (topLevel) WriteOrdinateTag(outputOrdinates, writer);

            if (cc.IsEmpty)
            {
                writer.Write("EMPTY");
                return;
            }

            writer.Write("(");
            for (int i = 0; i < cc.Curves.Count; i++)
            {
                if (i > 0) writer.Write(", ");
                WriteCurveText(cc.Curves[i], outputOrdinates, writer, ordinateFormat);
            }
            writer.Write(")");
        }

        private void WriteCurvePolygonTaggedText(CurvePolygon cp, Ordinates outputOrdinates, bool topLevel,
            TextWriter writer, OrdinateFormat ordinateFormat)
        {
            writer.Write("CURVEPOLYGON ");
            if (topLevel) WriteOrdinateTag(outputOrdinates, writer);

            if (cp.IsEmpty)
            {
                writer.Write("EMPTY");
                return;
            }

            writer.Write("(");
            WriteCurveText(cp.ExteriorRing, outputOrdinates, writer, ordinateFormat);

            for (int i = 0; i < cp.NumInteriorRings; i++)
            {
                writer.Write(", ");
                WriteCurveText(cp.GetInteriorRingN(i), outputOrdinates, writer, ordinateFormat);
            }
            writer.Write(")");
        }

        private void WriteMultiCurveTaggedText(MultiCurve mc, Ordinates outputOrdinates, bool topLevel,
            TextWriter writer, OrdinateFormat ordinateFormat)
        {
            writer.Write("MULTICURVE ");
            if (topLevel) WriteOrdinateTag(outputOrdinates, writer);

            if (mc.IsEmpty)
            {
                writer.Write("EMPTY");
                return;
            }

            writer.Write("(");
            for (int i = 0; i < mc.NumGeometries; i++)
            {
                if (i > 0) writer.Write(", ");
                WriteCurveText(mc.GetGeometryN(i), outputOrdinates, writer, ordinateFormat);
            }
            writer.Write(")");
        }

        private void WriteMultiSurfaceTaggedText(MultiSurface ms, Ordinates outputOrdinates, bool topLevel,
            TextWriter writer, OrdinateFormat ordinateFormat)
        {
            writer.Write("MULTISURFACE ");
            if (topLevel) WriteOrdinateTag(outputOrdinates, writer);

            if (ms.IsEmpty)
            {
                writer.Write("EMPTY");
                return;
            }

            writer.Write("(");
            for (int i = 0; i < ms.NumGeometries; i++)
            {
                if (i > 0) writer.Write(", ");
                var member = ms.GetGeometryN(i);
                if (member is Polygon p)
                    WritePolygonText(p, outputOrdinates, writer, ordinateFormat);
                else if (member is CurvePolygon cp)
                    WriteCurvePolygonTaggedText(cp, outputOrdinates, false, writer, ordinateFormat);
                else
                    throw new InvalidOperationException(
                        "Invalid geometry type for MultiSurface member: " + member.GeometryType);
            }
            writer.Write(")");
        }

        // Dispatches a curve-list member.  Plain LineString members are emitted as a
        // raw sequence "(x y, x y, ...)" without the LINESTRING tag, matching how
        // COMPOUNDCURVE and CURVEPOLYGON express LineString rings.
        private void WriteCurveText(Geometry curve, Ordinates outputOrdinates,
            TextWriter writer, OrdinateFormat ordinateFormat)
        {
            switch (curve)
            {
                case LineString ls:
                    WriteSequenceText(ls.CoordinateSequence, outputOrdinates, writer, ordinateFormat);
                    break;
                case CircularString cs:
                    WriteCircularStringTaggedText(cs, outputOrdinates, false, writer, ordinateFormat);
                    break;
                case CompoundCurve cc:
                    WriteCompoundCurveTaggedText(cc, outputOrdinates, false, writer, ordinateFormat);
                    break;
                default:
                    throw new InvalidOperationException(
                        "Invalid curve-list member type: " + curve.GeometryType);
            }
        }

        // Non-tagged Polygon body, mirrors upstream WKTWriter.AppendPolygonText for the
        // nested case inside MULTISURFACE (no "POLYGON" prefix; just the parenthesised
        // ring list).
        private void WritePolygonText(Polygon polygon, Ordinates outputOrdinates,
            TextWriter writer, OrdinateFormat ordinateFormat)
        {
            if (polygon.IsEmpty)
            {
                writer.Write("EMPTY");
                return;
            }

            writer.Write("(");
            WriteSequenceText(polygon.ExteriorRing.CoordinateSequence, outputOrdinates, writer, ordinateFormat);
            for (int i = 0; i < polygon.NumInteriorRings; i++)
            {
                writer.Write(", ");
                WriteSequenceText(polygon.GetInteriorRingN(i).CoordinateSequence, outputOrdinates, writer, ordinateFormat);
            }
            writer.Write(")");
        }

        // ---------------------------------------------------------------------
        // Inlined formatting primitives.  These mirror the protected helpers
        // on upstream WKTWriter on the pinned submodule (2772c9b3) but use
        // only its public surface, so they survive when the bump removes the
        // protected hooks.  Byte-for-byte output match is verified by
        // WKTReadWriteTest's exact-string assertions.
        // ---------------------------------------------------------------------

        // Writes "Z", "M", or "ZM" (no trailing space, no leading space) depending on
        // outputOrdinates.  Called immediately after the type tag (e.g. "CIRCULARSTRING ").
        private static void WriteOrdinateTag(Ordinates outputOrdinates, TextWriter writer)
        {
            if (outputOrdinates.HasFlag(Ordinates.Z)) writer.Write("Z");
            if (outputOrdinates.HasFlag(Ordinates.M)) writer.Write("M");
        }

        // Writes either "EMPTY" or "(x y[ z][ m], x y[ z][ m], ...)".
        private void WriteSequenceText(CoordinateSequence seq, Ordinates outputOrdinates,
            TextWriter writer, OrdinateFormat ordinateFormat)
        {
            if (seq == null || seq.Count == 0)
            {
                writer.Write("EMPTY");
                return;
            }

            writer.Write("(");
            for (int i = 0; i < seq.Count; i++)
            {
                if (i > 0) writer.Write(", ");
                WriteCoordinate(seq, outputOrdinates, i, writer, ordinateFormat);
            }
            writer.Write(")");
        }

        // Writes "x y" plus optional " z" / " m" ordinates.  When an ordinate flag is
        // set but the value is NaN, " NaN" is emitted (upstream convention for the
        // non-mssql writer); in mssql mode " NULL" replaces " NaN".
        private void WriteCoordinate(CoordinateSequence seq, Ordinates outputOrdinates, int i,
            TextWriter writer, OrdinateFormat ordinateFormat)
        {
            writer.Write(ordinateFormat.Format(seq.GetX(i)));
            writer.Write(" ");
            writer.Write(ordinateFormat.Format(seq.GetY(i)));

            if (outputOrdinates.HasFlag(Ordinates.Z))
            {
                double z = seq.GetZ(i);
                if (!double.IsNaN(z))
                {
                    writer.Write(" ");
                    writer.Write(ordinateFormat.Format(z));
                }
                else
                {
                    writer.Write(_mssql ? " NULL" : " NaN");
                }
            }

            if (outputOrdinates.HasFlag(Ordinates.M))
            {
                double m = seq.GetM(i);
                if (!double.IsNaN(m))
                {
                    writer.Write(" ");
                    writer.Write(ordinateFormat.Format(m));
                }
                else
                {
                    writer.Write(_mssql ? " NULL" : " NaN");
                }
            }
        }

        // ---------------------------------------------------------------------
        // OrdinateFormat + per-geometry OutputOrdinates derivation.
        // ---------------------------------------------------------------------

        // Mirrors upstream WKTWriter.CreateOrdinateFormat (internal helper) using the
        // public OrdinateFormat(int) constructor.
        private static OrdinateFormat CreateOrdinateFormat(PrecisionModel pm)
        {
            int digits = pm == null ? 16 : pm.MaximumSignificantDigits;
            int decimalPlaces = digits < 0 ? 0 : digits;
            return new OrdinateFormat(decimalPlaces);
        }

        // Walks the geometry's coordinate sequences and promotes Z/M into the
        // result only when a non-NaN value actually appears.  Mirrors upstream
        // WKTWriter.CheckOrdinatesFilter without depending on that internal class.
        // Starts from XY and OR-merges Z / M discoveries up to the configured limit.
        private Ordinates ComputeOutputOrdinates(Geometry g)
        {
            var configured = _inner.OutputOrdinates;
            bool considerZ = configured.HasFlag(Ordinates.Z);
            bool considerM = configured.HasFlag(Ordinates.M);
            if (!considerZ && !considerM)
                return Ordinates.XY;

            bool sawZ = false;
            bool sawM = false;
            ScanGeometry(g, considerZ, considerM, ref sawZ, ref sawM);

            var result = Ordinates.XY;
            if (considerZ && sawZ) result |= Ordinates.Z;
            if (considerM && sawM) result |= Ordinates.M;
            return result;
        }

        private static void ScanGeometry(Geometry g, bool considerZ, bool considerM,
            ref bool sawZ, ref bool sawM)
        {
            if (g == null || g.IsEmpty) return;
            if ((!considerZ || sawZ) && (!considerM || sawM)) return;

            switch (g)
            {
                case CircularString cs:
                    ScanSequence(cs.ControlPoints, considerZ, considerM, ref sawZ, ref sawM);
                    break;
                case CompoundCurve cc:
                    if (cc.Curves != null)
                        for (int i = 0; i < cc.Curves.Count; i++)
                            ScanGeometry(cc.Curves[i], considerZ, considerM, ref sawZ, ref sawM);
                    break;
                case CurvePolygon cp:
                    ScanGeometry(cp.ExteriorRing, considerZ, considerM, ref sawZ, ref sawM);
                    for (int i = 0; i < cp.NumInteriorRings; i++)
                        ScanGeometry(cp.GetInteriorRingN(i), considerZ, considerM, ref sawZ, ref sawM);
                    break;
                case MultiCurve mc:
                    for (int i = 0; i < mc.NumGeometries; i++)
                        ScanGeometry(mc.GetGeometryN(i), considerZ, considerM, ref sawZ, ref sawM);
                    break;
                case MultiSurface ms:
                    for (int i = 0; i < ms.NumGeometries; i++)
                        ScanGeometry(ms.GetGeometryN(i), considerZ, considerM, ref sawZ, ref sawM);
                    break;
                case LineString ls:
                    ScanSequence(ls.CoordinateSequence, considerZ, considerM, ref sawZ, ref sawM);
                    break;
                case Polygon p:
                    ScanSequence(p.ExteriorRing.CoordinateSequence, considerZ, considerM, ref sawZ, ref sawM);
                    for (int i = 0; i < p.NumInteriorRings; i++)
                        ScanSequence(p.GetInteriorRingN(i).CoordinateSequence, considerZ, considerM, ref sawZ, ref sawM);
                    break;
            }
        }

        private static void ScanSequence(CoordinateSequence seq, bool considerZ, bool considerM,
            ref bool sawZ, ref bool sawM)
        {
            if (seq == null || seq.Count == 0) return;
            bool hasZdim = seq.Ordinates.HasFlag(Ordinates.Z);
            bool hasMdim = seq.Ordinates.HasFlag(Ordinates.M);
            if (!hasZdim && !hasMdim) return;

            for (int i = 0; i < seq.Count; i++)
            {
                if (considerZ && !sawZ && hasZdim && !double.IsNaN(seq.GetZ(i))) sawZ = true;
                if (considerM && !sawM && hasMdim && !double.IsNaN(seq.GetM(i))) sawM = true;
                if ((!considerZ || sawZ) && (!considerM || sawM)) return;
            }
        }
    }
}
