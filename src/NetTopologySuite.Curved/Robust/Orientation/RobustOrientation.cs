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

        // ---------------------------------------------------------------
        // Shewchuk Stage A filter.
        // ---------------------------------------------------------------
        // The naive `Sign` above can flip at near-collinear inputs because
        // of rounding.  Shewchuk's adaptive orient2d (1997) closes this
        // with a four-stage refinement; this implementation ships Stage A
        // only (deeper stages are deferred -- they would refine
        // `Uncertain` results into a definite Pos/Neg/Zero via expansion
        // arithmetic).  Transliterates the Coq `b64_orient_sign_filtered`
        // in `theories-flocq/Orientation_b64.v`.

        // (3 + 16 * eps) * eps, where eps = 2^-52 (spacing at 1.0 in
        // binary64).  Computed once and stored.  ~6.66e-16 -- the
        // forward-error coefficient Shewchuk derives in his paper.
        private static readonly double ErrBoundACoeff =
            (3.0 + 16.0 * Eps) * Eps;

        private const double Eps = 2.220446049250313e-16; // 2^-52

        /// <summary>
        /// Robust orientation sign with Shewchuk Stage A filter.  Returns
        /// <see cref="OrientSignRobust.Uncertain"/> when the naive cross-
        /// product is too close to zero relative to the operand
        /// magnitudes for its sign to be trusted under IEEE 754 binary64
        /// rounding.  In every other case agrees with <see cref="Sign"/>.
        /// </summary>
        public static OrientSignRobust SignFiltered(BPoint p0, BPoint p1, BPoint q)
        {
            double t1 = (p1.X - p0.X) * (q.Y  - p0.Y);
            double t2 = (q.X  - p0.X) * (p1.Y - p0.Y);
            double det = t1 - t2;

            if (double.IsNaN(det))
            {
                return OrientSignRobust.Nan;
            }
            if (det == 0.0)
            {
                return OrientSignRobust.Zero;
            }

            double detsum = System.Math.Abs(t1) + System.Math.Abs(t2);
            double errbnd = ErrBoundACoeff * detsum;
            double absDet = System.Math.Abs(det);

            if (double.IsNaN(errbnd) || double.IsNaN(absDet))
            {
                return OrientSignRobust.Nan;
            }

            if (errbnd < absDet)
            {
                // Filter passes; naive sign is reliable.
                return det > 0.0 ? OrientSignRobust.Pos : OrientSignRobust.Neg;
            }

            // Filter cannot decide.  Higher-precision refinement (Shewchuk
            // Stages B / C / D) would resolve it; not implemented yet.
            return OrientSignRobust.Uncertain;
        }
    }
}
