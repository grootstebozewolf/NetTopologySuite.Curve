// =============================================================================
// NetTopologySuite.Curve.Robust.Relate.DE9IM
// -----------------------------------------------------------------------------
// C# transliteration of the DE-9IM intersection-matrix algebra from
//   theories/DE9IM.v in NetTopologySuite.Proofs.
//
// Formalises the dimensionally-extended 3×3 intersection matrix used by JTS
// RelateNG / OGC spatial predicates.  Pure matrix algebra — no geometry
// carrier, no RelateNG algorithm.  Mirrors Coq types:
//   DimValue, PatternChar, IMPattern, IntersectionMatrix, RelatePredicate.
// =============================================================================

using System;

namespace NetTopologySuite.Robust.Relate
{
    /// <summary>Dimension entry: <c>null</c> = empty (F); non-null = dimension.</summary>
    public readonly struct DimValue : IEquatable<DimValue>
    {
        public int? Value { get; }

        public DimValue(int? value)
        {
            if (value.HasValue && (value.Value < 0 || value.Value > 2))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "dimension must be 0, 1, or 2");
            }
            Value = value;
        }

        public static DimValue Empty => new DimValue(null);
        public static DimValue Of(int d) => new DimValue(d);

        public bool IsEmpty => !Value.HasValue;
        public bool IsNonempty => Value.HasValue;

        public bool DimValueOk =>
            !Value.HasValue || (Value.Value >= 0 && Value.Value <= 2);

        public bool Equals(DimValue other) => Value == other.Value;
        public override bool Equals(object obj) => obj is DimValue d && Equals(d);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
    }

    /// <summary>Pattern character: Wild (*), False (F), True (T), or exact dimension.</summary>
    public enum PatternCharKind { Wild, False, True, Dim }

    public readonly struct PatternChar : IEquatable<PatternChar>
    {
        public PatternCharKind Kind { get; }
        public int Dim { get; }

        private PatternChar(PatternCharKind kind, int dim = 0)
        {
            Kind = kind;
            Dim = dim;
        }

        public static PatternChar Wild  => new PatternChar(PatternCharKind.Wild);
        public static PatternChar False => new PatternChar(PatternCharKind.False);
        public static PatternChar True  => new PatternChar(PatternCharKind.True);
        public static PatternChar DimN(int n) => new PatternChar(PatternCharKind.Dim, n);

        public bool Matches(DimValue d)
        {
            switch (Kind)
            {
                case PatternCharKind.Wild:  return true;
                case PatternCharKind.False: return d.IsEmpty;
                case PatternCharKind.True:  return d.IsNonempty;
                case PatternCharKind.Dim:   return d.Value.HasValue && d.Value.Value == Dim;
                default: throw new InvalidOperationException();
            }
        }

        public bool Equals(PatternChar other) =>
            Kind == other.Kind && (Kind != PatternCharKind.Dim || Dim == other.Dim);
        public override bool Equals(object obj) => obj is PatternChar p && Equals(p);
        public override int GetHashCode() => Kind.GetHashCode() ^ Dim.GetHashCode();
    }

    /// <summary>Nine-cell DE-9IM pattern (row-major II..EE).</summary>
    public readonly struct IMPattern : IEquatable<IMPattern>
    {
        public PatternChar Ii { get; }
        public PatternChar Ib { get; }
        public PatternChar Ie { get; }
        public PatternChar Bi { get; }
        public PatternChar Bb { get; }
        public PatternChar Be { get; }
        public PatternChar Ei { get; }
        public PatternChar Eb { get; }
        public PatternChar Ee { get; }

        public IMPattern(
            PatternChar ii, PatternChar ib, PatternChar ie,
            PatternChar bi, PatternChar bb, PatternChar be,
            PatternChar ei, PatternChar eb, PatternChar ee)
        {
            Ii = ii; Ib = ib; Ie = ie;
            Bi = bi; Bb = bb; Be = be;
            Ei = ei; Eb = eb; Ee = ee;
        }

        public IMPattern Transpose() => new IMPattern(
            Ii, Bi, Ei,
            Ib, Bb, Eb,
            Ie, Be, Ee);

        public bool Matches(IntersectionMatrix m) =>
            Ii.Matches(m.Ii) && Ib.Matches(m.Ib) && Ie.Matches(m.Ie) &&
            Bi.Matches(m.Bi) && Bb.Matches(m.Bb) && Be.Matches(m.Be) &&
            Ei.Matches(m.Ei) && Eb.Matches(m.Eb) && Ee.Matches(m.Ee);

        public bool Equals(IMPattern other) =>
            Ii.Equals(other.Ii) && Ib.Equals(other.Ib) && Ie.Equals(other.Ie) &&
            Bi.Equals(other.Bi) && Bb.Equals(other.Bb) && Be.Equals(other.Be) &&
            Ei.Equals(other.Ei) && Eb.Equals(other.Eb) && Ee.Equals(other.Ee);
        public override bool Equals(object obj) => obj is IMPattern p && Equals(p);
        public override int GetHashCode() => 0; // not used in tests
    }

    /// <summary>DE-9IM intersection matrix (nine dimension cells).</summary>
    public readonly struct IntersectionMatrix : IEquatable<IntersectionMatrix>
    {
        public DimValue Ii { get; }
        public DimValue Ib { get; }
        public DimValue Ie { get; }
        public DimValue Bi { get; }
        public DimValue Bb { get; }
        public DimValue Be { get; }
        public DimValue Ei { get; }
        public DimValue Eb { get; }
        public DimValue Ee { get; }

        public IntersectionMatrix(
            DimValue ii, DimValue ib, DimValue ie,
            DimValue bi, DimValue bb, DimValue be,
            DimValue ei, DimValue eb, DimValue ee)
        {
            Ii = ii; Ib = ib; Ie = ie;
            Bi = bi; Bb = bb; Be = be;
            Ei = ei; Eb = eb; Ee = ee;
        }

        public bool MatrixOk =>
            Ii.DimValueOk && Ib.DimValueOk && Ie.DimValueOk &&
            Bi.DimValueOk && Bb.DimValueOk && Be.DimValueOk &&
            Ei.DimValueOk && Eb.DimValueOk && Ee.DimValueOk;

        /// <summary>
        /// Parse a 9-char NTS/JTS matrix string (row-major II..EE; F = empty).
        /// </summary>
        public static IntersectionMatrix FromNtsString(string matrix)
        {
            if (matrix == null || matrix.Length != 9)
            {
                throw new ArgumentException(
                    "matrix must be exactly 9 characters", nameof(matrix));
            }

            DimValue Cell(char c)
            {
                if (c == 'F') return DimValue.Empty;
                if (c >= '0' && c <= '2') return DimValue.Of(c - '0');
                throw new ArgumentException(
                    "invalid matrix character: " + c, nameof(matrix));
            }

            return new IntersectionMatrix(
                Cell(matrix[0]), Cell(matrix[1]), Cell(matrix[2]),
                Cell(matrix[3]), Cell(matrix[4]), Cell(matrix[5]),
                Cell(matrix[6]), Cell(matrix[7]), Cell(matrix[8]));
        }

        public IntersectionMatrix Transpose() => new IntersectionMatrix(
            Ii, Bi, Ei,
            Ib, Bb, Eb,
            Ie, Be, Ee);

        public bool Equals(IntersectionMatrix other) =>
            Ii.Equals(other.Ii) && Ib.Equals(other.Ib) && Ie.Equals(other.Ie) &&
            Bi.Equals(other.Bi) && Bb.Equals(other.Bb) && Be.Equals(other.Be) &&
            Ei.Equals(other.Ei) && Eb.Equals(other.Eb) && Ee.Equals(other.Ee);
        public override bool Equals(object obj) => obj is IntersectionMatrix m && Equals(m);
        public override int GetHashCode() => 0;
    }

    /// <summary>JTS / OGC relate predicate names.</summary>
    public enum RelatePredicate
    {
        Disjoint, Intersects, Contains, Within,
        Covers, CoveredBy, EqualsTopo, Touches,
        Crosses, Overlaps
    }

    /// <summary>
    /// Standard JTS / OGC pattern tables and predicate evaluation.
    /// Mirrors <c>theories/DE9IM.v</c>.
    /// </summary>
    public static class DE9IM
    {
        public static readonly IMPattern PatDisjoint = new IMPattern(
            PatternChar.False, PatternChar.False, PatternChar.Wild,
            PatternChar.False, PatternChar.False, PatternChar.Wild,
            PatternChar.False, PatternChar.False, PatternChar.Wild);

        public static readonly IMPattern PatIntersects0 = new IMPattern(
            PatternChar.True, PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild);

        public static readonly IMPattern PatIntersects1 = new IMPattern(
            PatternChar.Wild, PatternChar.True, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild);

        public static readonly IMPattern PatIntersects3 = new IMPattern(
            PatternChar.Wild, PatternChar.Wild, PatternChar.True,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild);

        public static readonly IMPattern PatIntersects4 = new IMPattern(
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.True, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild);

        public static readonly IMPattern PatContains = new IMPattern(
            PatternChar.True, PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild,
            PatternChar.False, PatternChar.False, PatternChar.Wild);

        public static readonly IMPattern PatWithin = PatContains.Transpose();

        public static readonly IMPattern PatCovers0 = PatContains;

        public static readonly IMPattern PatCovers1 = new IMPattern(
            PatternChar.Wild, PatternChar.True, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild,
            PatternChar.False, PatternChar.False, PatternChar.Wild);

        public static readonly IMPattern PatCovers3 = new IMPattern(
            PatternChar.Wild, PatternChar.Wild, PatternChar.True,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild,
            PatternChar.False, PatternChar.False, PatternChar.Wild);

        public static readonly IMPattern PatCovers4 = new IMPattern(
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.True, PatternChar.Wild,
            PatternChar.False, PatternChar.False, PatternChar.Wild);

        public static readonly IMPattern PatCoveredBy0 = PatCovers0.Transpose();
        public static readonly IMPattern PatCoveredBy1 = PatCovers1.Transpose();
        public static readonly IMPattern PatCoveredBy3 = PatCovers3.Transpose();
        public static readonly IMPattern PatCoveredBy4 = PatCovers4.Transpose();

        public static readonly IMPattern PatEqualsTopo = new IMPattern(
            PatternChar.True, PatternChar.Wild, PatternChar.False,
            PatternChar.Wild, PatternChar.Wild, PatternChar.False,
            PatternChar.False, PatternChar.False, PatternChar.False);

        public static readonly IMPattern PatTouches0 = new IMPattern(
            PatternChar.False, PatternChar.True, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild);

        public static readonly IMPattern PatTouches1 = new IMPattern(
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild,
            PatternChar.False, PatternChar.True, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild);

        public static readonly IMPattern PatTouches3 = new IMPattern(
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild,
            PatternChar.False, PatternChar.True, PatternChar.Wild);

        public static readonly IMPattern PatCrossesPlPaLa = new IMPattern(
            PatternChar.True, PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.False, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild);

        public static readonly IMPattern PatCrossesLpApAl = new IMPattern(
            PatternChar.True, PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.True,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild);

        public static readonly IMPattern PatCrossesLl = new IMPattern(
            PatternChar.DimN(0), PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.Wild);

        public static readonly IMPattern PatOverlapsPpAa = new IMPattern(
            PatternChar.True, PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.True, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.True);

        public static readonly IMPattern PatOverlapsLl = new IMPattern(
            PatternChar.DimN(1), PatternChar.Wild, PatternChar.Wild,
            PatternChar.Wild, PatternChar.True, PatternChar.Wild,
            PatternChar.Wild, PatternChar.Wild, PatternChar.True);

        /// <summary>Coq witness: disjoint yet matches intersects_3 (interior-exterior touch).</summary>
        public static readonly IntersectionMatrix DisjointIntersects3Example =
            new IntersectionMatrix(
                DimValue.Empty, DimValue.Empty, DimValue.Of(0),
                DimValue.Empty, DimValue.Empty, DimValue.Empty,
                DimValue.Empty, DimValue.Empty, DimValue.Empty);

        /// <summary>Coq witness: neither intersects nor disjoint (EI nonempty, EE empty).</summary>
        public static readonly IntersectionMatrix NotIntersectsGapExample =
            new IntersectionMatrix(
                DimValue.Empty, DimValue.Empty, DimValue.Empty,
                DimValue.Empty, DimValue.Empty, DimValue.Empty,
                DimValue.Of(0), DimValue.Empty, DimValue.Empty);

        public static bool ImDisjoint(IntersectionMatrix m) =>
            PatDisjoint.Matches(m);

        public static bool ImIntersects(IntersectionMatrix m) =>
            PatIntersects0.Matches(m) ||
            PatIntersects1.Matches(m) ||
            PatIntersects3.Matches(m) ||
            PatIntersects4.Matches(m);

        public static bool ImContains(IntersectionMatrix m) =>
            PatContains.Matches(m);

        public static bool ImWithin(IntersectionMatrix m) =>
            PatWithin.Matches(m);

        public static bool ImCovers(IntersectionMatrix m) =>
            PatCovers0.Matches(m) || PatCovers1.Matches(m) ||
            PatCovers3.Matches(m) || PatCovers4.Matches(m);

        public static bool ImCoveredBy(IntersectionMatrix m) =>
            PatCoveredBy0.Matches(m) || PatCoveredBy1.Matches(m) ||
            PatCoveredBy3.Matches(m) || PatCoveredBy4.Matches(m);

        public static bool ImEqualsTopo(IntersectionMatrix m) =>
            PatEqualsTopo.Matches(m);

        public static bool ImTouches(IntersectionMatrix m) =>
            PatTouches0.Matches(m) ||
            PatTouches1.Matches(m) ||
            PatTouches3.Matches(m);

        public static bool ImCrosses(IntersectionMatrix m) =>
            PatCrossesPlPaLa.Matches(m) ||
            PatCrossesLpApAl.Matches(m) ||
            PatCrossesLl.Matches(m);

        public static bool ImOverlaps(IntersectionMatrix m) =>
            PatOverlapsPpAa.Matches(m) ||
            PatOverlapsLl.Matches(m);

        public static bool PredicateHolds(RelatePredicate r, IntersectionMatrix m)
        {
            switch (r)
            {
                case RelatePredicate.Disjoint:    return ImDisjoint(m);
                case RelatePredicate.Intersects:  return ImIntersects(m);
                case RelatePredicate.Contains:    return ImContains(m);
                case RelatePredicate.Within:      return ImWithin(m);
                case RelatePredicate.Covers:      return ImCovers(m);
                case RelatePredicate.CoveredBy:   return ImCoveredBy(m);
                case RelatePredicate.EqualsTopo:  return ImEqualsTopo(m);
                case RelatePredicate.Touches:     return ImTouches(m);
                case RelatePredicate.Crosses:     return ImCrosses(m);
                case RelatePredicate.Overlaps:    return ImOverlaps(m);
                default:
                    throw new ArgumentOutOfRangeException(nameof(r), r,
                        "unknown RelatePredicate");
            }
        }

        /// <summary>
        /// Mirrors <c>im_disjoint_not_intersects_partial</c>: disjoint ⇒
        /// ¬intersects₀ ∧ ¬intersects₁ ∧ ¬intersects₄.
        /// </summary>
        public static bool DisjointImpliesNotIntersectsPartial(IntersectionMatrix m)
        {
            if (!ImDisjoint(m)) return true;
            return !PatIntersects0.Matches(m) &&
                   !PatIntersects1.Matches(m) &&
                   !PatIntersects4.Matches(m);
        }

        /// <summary>
        /// Mirrors <c>im_intersects_not_disjoint_partial</c>: any of
        /// intersects₀/₁/₄ ⇒ ¬disjoint.
        /// </summary>
        public static bool IntersectsPartialImpliesNotDisjoint(IntersectionMatrix m)
        {
            bool partial = PatIntersects0.Matches(m) ||
                           PatIntersects1.Matches(m) ||
                           PatIntersects4.Matches(m);
            return !partial || !ImDisjoint(m);
        }
    }
}