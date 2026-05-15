// =============================================================================
// NetTopologySuite.Curve.Robust.Orientation.OrientSign
// -----------------------------------------------------------------------------
// C# transliteration of the Coq `orient_sign` Inductive from
//   theories-flocq/Orientation_b64.v in NetTopologySuite.Proofs:
//
//     Inductive orient_sign : Type :=
//     | OrientPos
//     | OrientNeg
//     | OrientZero
//     | OrientNan.
//
// `Nan` is explicit -- callers MUST handle it rather than treat it as a
// default sign.  Conflating it with `Zero` would let a NaN in the input
// silently flip a downstream "is this point on the boundary?" decision.
// =============================================================================

namespace NetTopologySuite.Robust.Orientation
{
    /// <summary>
    /// Four-valued orientation result on binary64 points.
    /// </summary>
    public enum OrientSign
    {
        /// <summary>Counter-clockwise: signed twice-area is strictly positive.</summary>
        Pos = 1,

        /// <summary>Clockwise: signed twice-area is strictly negative.</summary>
        Neg = -1,

        /// <summary>Collinear (or degenerate triangle): signed twice-area is exactly zero.</summary>
        Zero = 0,

        /// <summary>NaN was produced by the binary64 computation; the orientation
        /// is undefined.  The Coq spec's <c>OrientNan</c> branch is reached when
        /// <c>b64_compare</c> sees a NaN operand.</summary>
        Nan = 2,
    }
}
