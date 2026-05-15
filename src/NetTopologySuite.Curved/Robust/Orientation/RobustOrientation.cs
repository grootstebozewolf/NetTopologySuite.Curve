// =============================================================================
// NetTopologySuite.Curve.Robust.Orientation.RobustOrientation
// -----------------------------------------------------------------------------
// C# transliteration of the Coq orientation predicate from
//   theories-flocq/Orientation_b64.v in NetTopologySuite.Proofs:
//
//     Definition b64_orient2d (P0 P1 Q : BPoint) : binary64 :=
//       b64_minus
//         (b64_mult (b64_minus (bx P1) (bx P0)) (b64_minus (by_ Q)  (by_ P0)))
//         (b64_mult (b64_minus (bx Q)  (bx P0)) (b64_minus (by_ P1) (by_ P0))).
//
//     Definition b64_orient_sign (P0 P1 Q : BPoint) : orient_sign := ...
//
// This is the NAIVE binary64 layer.  The Shewchuk-adaptive filter +
// expansion-arithmetic fallback that closes the robustness story is the
// next slice in the Coq corpus and will be ported here once it lands.
// At near-collinear inputs, rounding can flip the sign returned by
// `Sign` -- callers needing robustness against that today should treat
// `Zero` results as "uncertain, fall back to higher precision".
// =============================================================================

using NetTopologySuite.Robust.Simplify;

namespace NetTopologySuite.Robust.Orientation
{
    /// <summary>
    /// 2D orientation predicate on binary64 points, mirroring the Coq spec
    /// in <c>theories-flocq/Orientation_b64.v</c>.  Signed twice-area via the
    /// cross product; sign decoded as <see cref="OrientSign"/>.
    /// </summary>
    public static class RobustOrientation
    {
        /// <summary>
        /// Signed twice-area of the triangle (<paramref name="p0"/>,
        /// <paramref name="p1"/>, <paramref name="q"/>).  Transliterates the
        /// Coq <c>b64_orient2d</c>.  Positive = CCW, negative = CW, zero =
        /// collinear, NaN = at least one input was NaN or some intermediate
        /// product overflowed to infinity-minus-infinity.
        /// </summary>
        public static double Orient2d(BPoint p0, BPoint p1, BPoint q)
        {
            return (p1.X - p0.X) * (q.Y - p0.Y)
                 - (q.X  - p0.X) * (p1.Y - p0.Y);
        }

        /// <summary>
        /// Sign of <see cref="Orient2d"/> decoded as the four-valued
        /// <see cref="OrientSign"/>.  Transliterates Coq:
        /// <code>
        ///   Definition b64_orient_sign P0 P1 Q :=
        ///     let v := b64_orient2d P0 P1 Q in
        ///     match b64_compare v 0 with
        ///     | Some Lt => OrientNeg
        ///     | Some Gt => OrientPos
        ///     | Some Eq => OrientZero
        ///     | None    => OrientNan
        ///     end.
        /// </code>
        /// </summary>
        public static OrientSign Sign(BPoint p0, BPoint p1, BPoint q)
        {
            double v = Orient2d(p0, p1, q);
            if (double.IsNaN(v))
            {
                return OrientSign.Nan;
            }
            if (v > 0.0) return OrientSign.Pos;
            if (v < 0.0) return OrientSign.Neg;
            return OrientSign.Zero;
        }
    }
}
