// =============================================================================
// NetTopologySuite.Curve.Robust.Intersect.IntersectSign
// -----------------------------------------------------------------------------
// C# transliteration of the Coq `intersect_sign` Inductive from
//   theories-flocq/Intersect_b64.v in NetTopologySuite.Proofs:
//
//     Inductive intersect_sign : Type :=
//     | IntersectNone
//     | IntersectPoint
//     | IntersectCollinear
//     | IntersectNan
//     | IntersectUncertain.
//
// Five-valued: NaN and Uncertain are explicit rather than collapsed into a
// "false" return.  Conflating Uncertain with None or Collinear silently
// drops the soundness guarantee the Coq proof provides for the integer
// regime (b64_intersect_sign_filtered_none_sound_small_int).
// =============================================================================

namespace NetTopologySuite.Robust.Intersect
{
    /// <summary>
    /// Five-valued segment-pair intersection result, with Shewchuk Stage A
    /// filtering on each of the four underlying orientation tests.  Mirrors
    /// the Coq <c>intersect_sign</c> Inductive in
    /// <c>theories-flocq/Intersect_b64.v</c>.
    /// </summary>
    public enum IntersectSign
    {
        /// <summary>No intersection: at least one of the two pairs of endpoints
        /// is strictly on the same side of the other segment's supporting
        /// line.  Sound: in the integer regime the Coq proof shows the segments
        /// genuinely do not share a point.</summary>
        None = 0,

        /// <summary>All four orientation tests are nonzero and split into
        /// opposite pairs.  The segments cross at exactly one interior point.</summary>
        Point = 1,

        /// <summary>At least one orientation test returned Zero -- an
        /// endpoint lies on the other segment's supporting line, or the
        /// segments are collinear.  Could be a shared endpoint, T-junction,
        /// or full collinear overlap; higher-level geometric refinement is
        /// required to distinguish.</summary>
        Collinear = 2,

        /// <summary>NaN coordinate or intermediate NaN propagated through
        /// one of the four orientation tests.</summary>
        Nan = 3,

        /// <summary>At least one of the four Stage A orientation filters
        /// returned <see cref="NetTopologySuite.Robust.Orientation.OrientSignRobust.Uncertain"/>.
        /// Higher-precision refinement (Shewchuk Stages B/C/D) would resolve
        /// it.  Do not silently coerce to <see cref="None"/> or
        /// <see cref="Collinear"/>.</summary>
        Uncertain = 4,
    }
}
