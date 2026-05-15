// =============================================================================
// NetTopologySuite.Curve.Robust.Simplify.B64Ops
// -----------------------------------------------------------------------------
// Line-for-line C# transliteration of the Flocq binary64 helpers from
//   theories-flocq/Validate_binary64.v in NetTopologySuite.Proofs:
//
//     b64_plus, b64_minus, b64_mult, b64_compare, b64_le
//     b64_cross, b64_dist_sq
//
// IEEE 754 mapping: Flocq's `Bplus`/`Bminus`/`Bmult` at prec=53/emax=1024
// with `mode_NE` correspond bit-for-bit to .NET `double` `+`/`-`/`*` with
// the default round-to-nearest-even rounding mode.  `Bcompare` returns
// `None` on NaN inputs; .NET's `double.CompareTo` puts NaN as smallest,
// so we re-implement the predicate as in the Coq:
//
//     Definition b64_le (x y : binary64) : bool :=
//       match b64_compare x y with
//       | Some Lt | Some Eq => true
//       | _                 => false
//       end.
//
// i.e. NaN on either side -> false (the safe default for the simplifier:
// "if uncertain, do not drop").
// =============================================================================

namespace NetTopologySuite.Robust.Simplify
{
    /// <summary>
    /// Binary64 arithmetic + geometry primitives mirroring the Flocq versions
    /// proved in <c>theories-flocq/Validate_binary64.v</c>.  Operations are
    /// pure functions on <see cref="double"/>; in IEEE 754 binary64 with
    /// round-to-nearest-even they match the Coq operators bit-for-bit
    /// (modulo NaN-payload preservation, which is unobservable to the
    /// downstream <c>Le</c> predicate).
    /// </summary>
    public static class B64Ops
    {
        /// <summary>
        /// 2D cross-product of the segments <c>(P0, P1)</c> and <c>(P0, Q)</c>.
        /// Transliterates Coq:
        /// <code>
        /// Definition b64_cross (p0 p1 q : BPoint) : binary64 :=
        ///   b64_minus
        ///     (b64_mult (b64_minus (bx p1) (bx p0)) (b64_minus (by_ q)  (by_ p0)))
        ///     (b64_mult (b64_minus (bx q)  (bx p0)) (b64_minus (by_ p1) (by_ p0))).
        /// </code>
        /// </summary>
        public static double Cross(BPoint p0, BPoint p1, BPoint q)
        {
            return (p1.X - p0.X) * (q.Y - p0.Y)
                 - (q.X  - p0.X) * (p1.Y - p0.Y);
        }

        /// <summary>
        /// Squared Euclidean distance between <paramref name="p"/> and
        /// <paramref name="q"/>.  Transliterates Coq:
        /// <code>
        /// Definition b64_dist_sq (p q : BPoint) : binary64 :=
        ///   b64_plus
        ///     (b64_mult (b64_minus (bx p) (bx q)) (b64_minus (bx p) (bx q)))
        ///     (b64_mult (b64_minus (by_ p) (by_ q)) (b64_minus (by_ p) (by_ q))).
        /// </code>
        /// </summary>
        public static double DistSq(BPoint p, BPoint q)
        {
            double dx = p.X - q.X;
            double dy = p.Y - q.Y;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Boolean &lt;= test with NaN-safety: returns <c>false</c> whenever
        /// either input is NaN.  Transliterates Coq:
        /// <code>
        /// Definition b64_le (x y : binary64) : bool :=
        ///   match b64_compare x y with
        ///   | Some Lt | Some Eq =&gt; true
        ///   | _                 =&gt; false
        ///   end.
        /// </code>
        /// </summary>
        public static bool Le(double x, double y)
        {
            if (double.IsNaN(x) || double.IsNaN(y))
            {
                return false;
            }
            return x <= y;
        }
    }
}
