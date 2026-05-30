// =============================================================================
// NetTopologySuite.Curve.Robust.Arc.ArcChordCrossesCircle
// -----------------------------------------------------------------------------
// C# transliteration of `run_arc_chord_crosses_circle` in
//   NetTopologySuite.Proofs/oracle/driver.ml
// of the Coq predicate
//   `chord_crosses_arc_circle` (theories/ArcIntersect.v:129)
// with IVT-witnessed soundness theorem
//   `chord_crosses_arc_circle_implies_circle_intersection`
//   (theories/ArcIntersectIVT.v).
//
// SUFFICIENT condition: sp * sq < 0, where sp = inCircle_R(A, B, C, P) and
// sq = inCircle_R(A, B, C, Q) with (A, B, C) = (arc_start, arc_mid,
// arc_end) -- the three arc-defining points -- and (P, Q) the chord
// endpoints.  When TRUE the chord strictly straddles the arc's
// circumscribed circle: one endpoint inside, the other outside, hence (by
// IVT applied to the continuous distance-squared-to-centre map) the chord
// crosses the circle.
//
// FALSE does NOT imply non-crossing.  Both chord endpoints on the same
// side of the circle (sp and sq same sign) still admit two crossings; the
// sufficient-condition framing is the one the Coq IVT theorem proves.
// Floating-point edge cases that yield FALSE under this predicate but
// geometrically cross include:
//   * either endpoint exactly ON the circle (sp=0 or sq=0 -> sp*sq=0, fails)
//   * overflows at huge scales where sp and sq both become +inf with the
//     same sign and the product is +inf * +inf = +inf
//   * underflows at subnormal scales where sp and sq both collapse to 0
//   * any NaN propagation (NaN < 0.0 is false)
//
// The C# port is byte-for-byte the OCaml shape: two InCircle.Determinant
// calls followed by `sp * sq < 0.0`.  The two determinants are the
// same translations / squared-norm formula in the same parenthesization;
// running on IEEE 754 binary64 round-to-nearest-even, the outputs must
// match the oracle exactly.
//
// Bit-equality contract is checked by ArcChordCrossesCircleRocqRefTests.
// =============================================================================

using NetTopologySuite.Robust.Simplify;

namespace NetTopologySuite.Robust.Arc
{
    /// <summary>
    /// Sufficient-condition predicate: does the chord <c>(P, Q)</c> cross the
    /// circumscribed circle of the arc defined by (arc_start, arc_mid,
    /// arc_end)?  Mirrors Coq <c>chord_crosses_arc_circle</c>
    /// (<c>theories/ArcIntersect.v:129</c>).
    /// </summary>
    public static class ArcChordCrossesCircle
    {
        /// <summary>
        /// Test the sufficient condition <c>sp * sq &lt; 0</c>, where
        /// <c>sp = InCircle.Determinant(arcStart, arcMid, arcEnd, chordP)</c>
        /// and <c>sq = InCircle.Determinant(arcStart, arcMid, arcEnd,
        /// chordQ)</c>.
        /// </summary>
        /// <remarks>
        /// TRUE implies the chord crosses the circle (formally proved by
        /// <c>chord_crosses_arc_circle_implies_circle_intersection</c> in
        /// <c>theories/ArcIntersectIVT.v</c>).  FALSE is non-conclusive --
        /// either no crossing, or a crossing the sufficient condition does
        /// not capture (both endpoints same side, an endpoint exactly on
        /// the circle, an overflow / underflow / NaN floating-point edge).
        /// </remarks>
        public static bool Evaluate(
            BPoint arcStart, BPoint arcMid, BPoint arcEnd,
            BPoint chordP, BPoint chordQ)
        {
            double sp = InCircle.Determinant(arcStart, arcMid, arcEnd, chordP);
            double sq = InCircle.Determinant(arcStart, arcMid, arcEnd, chordQ);
            return sp * sq < 0.0;
        }
    }
}
