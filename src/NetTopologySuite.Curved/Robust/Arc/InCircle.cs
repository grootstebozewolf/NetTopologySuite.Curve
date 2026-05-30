// =============================================================================
// NetTopologySuite.Curve.Robust.Arc.InCircle
// -----------------------------------------------------------------------------
// C# transliteration of the hand-rolled OCaml mirror `incircle_r_native` in
//   NetTopologySuite.Proofs/oracle/driver.ml
// of the Coq predicate
//   `inCircle_R` (theories/ArcOrient.v:88) in NetTopologySuite.Proofs.
//
// Cofactor expansion along the third (squared-norm) column of the lifted
// 3 x 3 determinant
//
//     | ax  ay  na |
//     | bx  by  nb |
//     | cx  cy  nc |
//
// where (ax, ay) = A - P, (bx, by) = B - P, (cx, cy) = C - P, and na, nb,
// nc are the squared norms of the rows.
//
//     det = ax * (by * nc - cy * nb)
//         - ay * (bx * nc - cx * nb)
//         + na * (bx * cy - cx * by)
//
// HAND-ROLLED port (not extraction): the Coq side is on R (no b64
// bridge yet -- "Phase 4 R-side; no b64 bridge yet" per the OCaml
// comment), so we transliterate the OCaml driver byte-for-byte, including
// the exact parenthesization of the three rows, so that the IEEE 754
// round-to-nearest-even reductions match.
//
// Sign convention from `inCircle_R`:
//   POS  iff (A, B, C) is CCW AND P is strictly inside the circumscribed
//        circle.
//   NEG  iff (A, B, C) is CCW AND P is strictly outside (or, dually, CW
//        and P is strictly inside).
//   ZERO iff P lies on the circumscribed circle, or the inputs underflow
//        (subnormal-scale collapse to representable zero).
//   NAN  iff any coordinate is NaN, or an intermediate cancels to NaN
//        (e.g. infinity - infinity at huge scales).
//
// Differential bit-equality vs the oracle's INCIRCLE_SIGN mode is checked
// by InCircleRocqRefTests in the .Test project.
// =============================================================================

using NetTopologySuite.Robust.Simplify;

namespace NetTopologySuite.Robust.Arc
{
    /// <summary>
    /// Lifted in-circle determinant on four binary64 points.  Mirrors the
    /// Coq <c>inCircle_R</c> predicate (theories/ArcOrient.v:88) via the
    /// OCaml hand-rolled mirror <c>incircle_r_native</c> in
    /// <c>NetTopologySuite.Proofs/oracle/driver.ml</c>.
    /// </summary>
    public static class InCircle
    {
        /// <summary>
        /// The signed determinant value.  Sign convention: positive iff
        /// (A, B, C) is CCW AND P is strictly inside the circumscribed
        /// circle; CW flips the sign; zero on the circle (or under
        /// floating-point underflow / cancellation collapse); NaN if any
        /// input is NaN or an intermediate is NaN.
        /// </summary>
        public static double Determinant(BPoint a, BPoint b, BPoint c, BPoint p)
        {
            // Translation so P is the origin; mirrors the OCaml driver's
            // let-bindings exactly.
            double ax = a.X - p.X;
            double ay = a.Y - p.Y;
            double bx = b.X - p.X;
            double by = b.Y - p.Y;
            double cx = c.X - p.X;
            double cy = c.Y - p.Y;

            double na = ax * ax + ay * ay;
            double nb = bx * bx + by * by;
            double nc = cx * cx + cy * cy;

            // Cofactor expansion along the third column.  Parenthesization
            // preserved bit-for-bit from oracle/driver.ml.
            return ax * (by * nc - cy * nb)
                 - ay * (bx * nc - cx * nb)
                 + na * (bx * cy - cx * by);
        }

        /// <summary>
        /// Classification of <see cref="Determinant"/> as
        /// <see cref="IncircleSign"/>.  Mirrors the OCaml driver's
        /// <c>incircle_sign_string</c> classifier byte-for-byte:
        /// NaN takes precedence, then strict sign, then zero.
        /// </summary>
        public static IncircleSign Sign(BPoint a, BPoint b, BPoint c, BPoint p)
        {
            double v = Determinant(a, b, c, p);
            // OCaml: `if v <> v` -- the NaN check.  C# `double.IsNaN`
            // realises the same predicate.
            if (double.IsNaN(v)) return IncircleSign.Nan;
            if (v > 0.0) return IncircleSign.Pos;
            if (v < 0.0) return IncircleSign.Neg;
            return IncircleSign.Zero;
        }
    }
}
