// =============================================================================
// NetTopologySuite.Curve.Robust.Simplify.BPoint
// -----------------------------------------------------------------------------
// Line-for-line C# transliteration of the Coq record `BPoint` from
//   theories-flocq/Validate_binary64.v in NetTopologySuite.Proofs:
//
//     Record BPoint : Type := mkBP { bx : binary64; by_ : binary64 }.
//
// `binary64` in the Coq side is Flocq's `Binary.binary_float prec emax`
// at prec=53 / emax=1024, i.e. IEEE 754 binary64 -- the same bit-level
// type as .NET `double`.  We map directly to `double` here; in particular
// `+`, `-`, `*` on `double` use IEEE 754 binary64 round-to-nearest-even,
// matching Flocq's `mode_NE`.
//
// This file is the executable + structural layer.  The R-bridge soundness
// theorem against `Simplify.simp_star` lives in the companion file
// `Validate_binary64_bridge.v` (still NOT YET CLAIMED, deferred).
// =============================================================================

namespace NetTopologySuite.Robust.Simplify
{
    /// <summary>
    /// A 2D point with binary64 coordinates.  Transliterates Coq:
    /// <code>Record BPoint : Type := mkBP { bx : binary64; by_ : binary64 }.</code>
    /// </summary>
    public readonly struct BPoint
    {
        /// <summary>X coordinate (Coq <c>bx</c>).</summary>
        public double X { get; }

        /// <summary>Y coordinate (Coq <c>by_</c>; underscore in Coq to avoid the
        /// <c>by</c> tactical token).</summary>
        public double Y { get; }

        /// <summary>Construct a <see cref="BPoint"/>.  Transliterates <c>mkBP</c>.</summary>
        public BPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
