// =============================================================================
// NetTopologySuite.Curve.Robust.Arc.IncircleSign
// -----------------------------------------------------------------------------
// Four-valued classification of the lifted in-circle determinant
//   det | A-P  ||A-P||^2 |
//       | B-P  ||B-P||^2 |
//       | C-P  ||C-P||^2 |
// computed by `InCircle.Determinant` (cofactor expansion along the third
// column).  Mirrors the OCaml driver's `incircle_sign_string` classifier
// in NetTopologySuite.Proofs/oracle/driver.ml:
//
//   if v <> v          -> "NAN"
//   else if v > 0.0    -> "POS"
//   else if v < 0.0    -> "NEG"
//   else               -> "ZERO"
//
// The Coq predicate `inCircle_R` (theories/ArcOrient.v:88) lives on R; the
// C# / OCaml side computes on IEEE 754 binary64 with the trust pattern
// "round-to-nearest-even realises Flocq's R semantics for the ops used"
// (matching the PASSES_THROUGH_* and SIMPLIFY modes -- see
// RobustPassesThrough.cs).
// =============================================================================

namespace NetTopologySuite.Robust.Arc
{
    /// <summary>
    /// Four-valued sign classification of the in-circle determinant.  POS
    /// iff (A, B, C) is CCW AND P is strictly inside the circumscribed
    /// circle.  CW (A, B, C) flips the sign.  See <see cref="InCircle"/>.
    /// </summary>
    public enum IncircleSign
    {
        /// <summary>Determinant strictly negative.  For CCW (A,B,C): P is
        /// strictly outside the circumscribed circle.</summary>
        Neg = 0,

        /// <summary>Determinant exactly zero.  Either P lies on the
        /// circumscribed circle, or the inputs underflow / cancel to a
        /// representable zero.</summary>
        Zero = 1,

        /// <summary>Determinant strictly positive.  For CCW (A,B,C): P is
        /// strictly inside the circumscribed circle.</summary>
        Pos = 2,

        /// <summary>At least one coordinate was NaN, or an intermediate
        /// product was NaN (e.g. <c>infinity - infinity</c>).</summary>
        Nan = 3,
    }
}
