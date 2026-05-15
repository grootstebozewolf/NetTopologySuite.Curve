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
    /// Four-valued orientation result on binary64 points (naive layer).
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

    /// <summary>
    /// Five-valued orientation result with Shewchuk Stage A filtering.  C#
    /// transliteration of the Coq <c>orient_sign_robust</c> Inductive from
    /// <c>theories-flocq/Orientation_b64.v</c>.  Extends <see cref="OrientSign"/>
    /// with <see cref="Uncertain"/>: the filter cannot decide the sign from
    /// the naive cross product alone, so the caller must either fall back
    /// to a higher-precision predicate or treat the triangle as collinear
    /// with a documented caveat.
    /// </summary>
    public enum OrientSignRobust
    {
        /// <summary>Counter-clockwise; filter confident.</summary>
        Pos = 1,

        /// <summary>Clockwise; filter confident.</summary>
        Neg = -1,

        /// <summary>Exact zero from the naive computation.</summary>
        Zero = 0,

        /// <summary>NaN coordinate or intermediate NaN propagated through.</summary>
        Nan = 2,

        /// <summary>Stage A filter cannot decide: |det| is within the
        /// Shewchuk error bound of zero.  A confident sign would require
        /// Stage B / C / D expansion-arithmetic refinement (not implemented
        /// yet in this layer).</summary>
        Uncertain = 3,
    }
}
