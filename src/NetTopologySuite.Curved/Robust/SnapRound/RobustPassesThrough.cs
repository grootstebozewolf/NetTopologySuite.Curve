// =============================================================================
// NetTopologySuite.Curve.Robust.SnapRound.RobustPassesThrough
// -----------------------------------------------------------------------------
// C# transliteration of the hot-pixel passes-through predicates from
//   theories-flocq/HotPixel_b64.v
//   theories-flocq/PassesThroughHalfopen_b64.v
// in NetTopologySuite.Proofs.
//
// Two Boolean predicates over (P0, P1, C):
//
//   PassesThroughFilter   <-> b64_passes_through_hot_pixel
//                              (sound vs CLOSED, complete vs HALF-OPEN)
//   PassesThroughHalfOpen <-> b64_passes_through_hot_pixel_halfopen
//                              (sound AND complete vs HALF-OPEN)
//
// Each is a conjunction of a Liang-Barsky touch on the original (P0, P1)
// AND on the half-to-even-snapped segment (snap(P0), snap(P1)) against the
// unit-square pixel centred at C ([cx - 0.5, cx + 0.5] x [cy - 0.5, cy + 0.5]).
// The half-open variant additionally pins the upper-bound midpoint witness
// xmid < xhi && ymid < yhi -- the corpus's characterisation that survives
// either orientation of (c0, c1) without an explicit case split.
//
// The corpus's bracket lemma b64_passes_through_hot_pixel_halfopen_implies_closed
// (NetTopologySuite.Proofs PR #22) is the formally-proved option-layer pin:
//   HalfOpen TRUE  =>  Filter TRUE
//   Filter FALSE   =>  HalfOpen FALSE
// Differential bit-equality with the Coq-extracted oracle is checked by
// the test class RobustPassesThroughRocqRefTests.
// =============================================================================

using System;
using NetTopologySuite.Robust.Simplify;

namespace NetTopologySuite.Robust.SnapRound
{
    /// <summary>
    /// Hot-pixel passes-through predicates on binary64 points.  Mirrors
    /// <c>b64_passes_through_hot_pixel</c> and
    /// <c>b64_passes_through_hot_pixel_halfopen</c> in
    /// <c>theories-flocq/HotPixel_b64.v</c> /
    /// <c>theories-flocq/PassesThroughHalfopen_b64.v</c>.
    /// </summary>
    /// <remarks>
    /// The pixel is the unit square <c>[cx - 0.5, cx + 0.5] x [cy - 0.5, cy + 0.5]</c>.
    /// Callers operating on a coarser/finer grid pre-scale their inputs so the
    /// pixel half-width is exactly 0.5; this matches the corpus's b64 layer,
    /// which is proved over the unit pixel after <c>b64_snap_coord</c> scaling.
    /// </remarks>
    public static class RobustPassesThrough
    {
        /// <summary>
        /// Closed-pixel passes-through filter: sound against the closed pixel,
        /// complete against the half-open pixel.  Transliterates
        /// <c>b64_passes_through_hot_pixel</c>.
        /// </summary>
        public static bool PassesThroughFilter(BPoint p0, BPoint p1, BPoint c)
        {
            return LiangBarskyTouches(p0, p1, c)
                && LiangBarskyTouches(Snap(p0), Snap(p1), c);
        }

        /// <summary>
        /// Half-open-pixel passes-through filter: sound AND complete against
        /// the half-open pixel.  Transliterates
        /// <c>b64_passes_through_hot_pixel_halfopen</c>.
        /// </summary>
        /// <remarks>
        /// On segments grazing the closed-pixel boundary but not the open
        /// pixel, this returns <c>false</c> while
        /// <see cref="PassesThroughFilter"/> returns <c>true</c>.  That
        /// divergence is the load-bearing distinction the Hobby snap-round
        /// pipeline relies on; see
        /// <c>PixelBoundary_ClosedVsHalfOpen_Diverge_Empirical</c> in the
        /// differential test class.
        /// </remarks>
        public static bool PassesThroughHalfOpen(BPoint p0, BPoint p1, BPoint c)
        {
            return LiangBarskyTouchesHalfOpen(p0, p1, c)
                && LiangBarskyTouchesHalfOpen(Snap(p0), Snap(p1), c);
        }

        // ---------------------------------------------------------------------
        // Liang-Barsky filter on the unit-square pixel at C.
        // ---------------------------------------------------------------------

        private static bool LiangBarskyTouches(BPoint p0, BPoint p1, BPoint c)
        {
            double x0 = p0.X, y0 = p0.Y;
            double x1 = p1.X, y1 = p1.Y;
            double cx = c.X,  cy = c.Y;
            double xlo = cx - 0.5, xhi = cx + 0.5;
            double ylo = cy - 0.5, yhi = cy + 0.5;

            return InSlabClosed(x0, x1, xlo, xhi)
                && InSlabClosed(y0, y1, ylo, yhi)
                && Math.Max(0.0, Math.Max(Tlo(x0, x1, xlo, xhi),
                                          Tlo(y0, y1, ylo, yhi)))
                   <= Math.Min(1.0, Math.Min(Thi(x0, x1, xlo, xhi),
                                             Thi(y0, y1, ylo, yhi)));
        }

        private static bool LiangBarskyTouchesHalfOpen(BPoint p0, BPoint p1, BPoint c)
        {
            double x0 = p0.X, y0 = p0.Y;
            double x1 = p1.X, y1 = p1.Y;
            double cx = c.X,  cy = c.Y;
            double xlo = cx - 0.5, xhi = cx + 0.5;
            double ylo = cy - 0.5, yhi = cy + 0.5;

            double tmin = Math.Max(0.0, Math.Max(Tlo(x0, x1, xlo, xhi),
                                                 Tlo(y0, y1, ylo, yhi)));
            double tmax = Math.Min(1.0, Math.Min(Thi(x0, x1, xlo, xhi),
                                                 Thi(y0, y1, ylo, yhi)));
            double tmid = (tmin + tmax) / 2.0;
            double xmid = (1.0 - tmid) * x0 + tmid * x1;
            double ymid = (1.0 - tmid) * y0 + tmid * y1;

            return InSlabHalfOpen(x0, x1, xlo, xhi)
                && InSlabHalfOpen(y0, y1, ylo, yhi)
                && tmin <= tmax
                && xmid < xhi
                && ymid < yhi;
        }

        // Per-axis slab membership for degenerate (axis-parallel) segments.
        // Non-degenerate axes return true unconditionally; the t-bounds
        // carry the constraint.
        private static bool InSlabClosed(double c0, double c1, double lo, double hi)
        {
            if (c1 == c0) return lo <= c0 && c0 <= hi;
            return true;
        }

        // Strict upper boundary -- mirrors `in_hot_pixel`'s `< xhi`.
        private static bool InSlabHalfOpen(double c0, double c1, double lo, double hi)
        {
            if (c1 == c0) return lo <= c0 && c0 < hi;
            return true;
        }

        // Per-axis t-bounds.  For non-degenerate axes the two t-values
        // (lo - c0) / (c1 - c0) and (hi - c0) / (c1 - c0) sit at the slab
        // boundaries; Tlo picks the smaller and Thi the larger regardless of
        // orientation.  Degenerate axes return [0, 1] so the t-overlap check
        // reduces to "non-empty segment parameter range".
        private static double Tlo(double c0, double c1, double lo, double hi)
        {
            if (c1 == c0) return 0.0;
            return Math.Min((lo - c0) / (c1 - c0), (hi - c0) / (c1 - c0));
        }

        private static double Thi(double c0, double c1, double lo, double hi)
        {
            if (c1 == c0) return 1.0;
            return Math.Max((lo - c0) / (c1 - c0), (hi - c0) / (c1 - c0));
        }

        // ---------------------------------------------------------------------
        // Half-to-even rounding -- mirrors b64_snap_coord (`Bnearbyint mode_NE`).
        // Ported line-by-line from `round_half_to_even` in
        // NetTopologySuite.Proofs/oracle/driver.ml so the two implementations
        // share the same arithmetic shape, not just the same mathematical
        // function.  Math.Round(x, MidpointRounding.ToEven) is the same in
        // outcome but uses a different decomposition internally; staying
        // faithful to the OCaml is the lower-risk port.
        // ---------------------------------------------------------------------
        internal static double SnapCoord(double x)
        {
            if (double.IsNaN(x)) return x;
            if (double.IsInfinity(x)) return x;
            double f = Math.Floor(x);
            double d = x - f;
            if (d < 0.5) return f;
            if (d > 0.5) return f + 1.0;
            return (f % 2.0 == 0.0) ? f : f + 1.0;
        }

        private static BPoint Snap(BPoint p) =>
            new BPoint(SnapCoord(p.X), SnapCoord(p.Y));
    }
}
