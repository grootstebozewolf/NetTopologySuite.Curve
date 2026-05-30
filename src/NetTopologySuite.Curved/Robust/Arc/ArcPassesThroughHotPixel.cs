// =============================================================================
// NetTopologySuite.Curve.Robust.Arc.ArcPassesThroughHotPixel
// -----------------------------------------------------------------------------
// C# transliteration of `run_arc_passes_through_pixel` in
//   NetTopologySuite.Proofs/oracle/driver.ml
// of the Coq predicate
//   `arc_passes_through_hot_pixel` (theories/ArcHotPixel.v:95)
//
// Six-way disjunction: four pixel-edge chord-crossings PLUS two endpoint-
// in-pixel tests.  The pixel is the half-open box
//   [cx - r, cx + r) x [cy - r, cy + r)
// with r = scale / 2 (bottom + left CLOSED, top + right OPEN -- matching
// the in_hot_pixel half-open convention used in Phase 2's PassesThroughHalfOpen).
//
// The four edges of the pixel are:
//   bottom-left  = (cx - r, cy - r)
//   bottom-right = (cx + r, cy - r)
//   top-right    = (cx + r, cy + r)
//   top-left     = (cx - r, cy + r)
// and the four chord-crossings are (bl, br), (br, tr), (tr, tl), (tl, bl)
// -- each tested by the InCircle sign-product (same form as
// <see cref="ArcChordCrossesCircle"/>).  The two endpoint tests are
// in_hot_pixel_halfopen(arc_start, ...) and in_hot_pixel_halfopen(arc_end, ...).
//
// SUFFICIENT condition.  TRUE => the arc passes through the pixel.
// FALSE does NOT imply non-crossing -- the sign-product is sound but not
// complete on each individual edge, and the disjunction inherits that.
// FALSE POSITIVE is possible too: the predicate tests whether the arc's
// CIRCLE crosses a pixel edge, not whether the arc itself does.  The arc
// is only a subarc of the circle, so a circle-crossing on a portion of
// the circle outside the arc's angular extent still counts as TRUE.  The
// C# port mirrors this exactly -- the contract is bit-equality with the
// oracle, not geometric "necessary AND sufficient" semantics.
//
// Half-open pixel convention: at scale=0 the box is empty (every `< hi`
// excludes the lo); at scale<0 the box is empty as well (lo > hi).
// Negative scales can still produce TRUE through the four edge-crossings,
// since the four corner points are still well-defined geometric points
// regardless of pixel-box degeneracy.  The C# port preserves both
// behaviours.
//
// Bit-equality contract is checked by ArcPassesThroughHotPixelRocqRefTests.
// =============================================================================

using NetTopologySuite.Robust.Simplify;

namespace NetTopologySuite.Robust.Arc
{
    /// <summary>
    /// Sufficient-condition predicate: does an arc defined by three points
    /// pass through the half-open hot pixel centred at <paramref name="center"/>
    /// with side length <paramref name="scale"/>?  Mirrors Coq
    /// <c>arc_passes_through_hot_pixel</c> (<c>theories/ArcHotPixel.v:95</c>).
    /// </summary>
    public static class ArcPassesThroughHotPixel
    {
        /// <summary>
        /// Six-way disjunction over the four pixel-edge chord-crossings and
        /// the two endpoint-in-pixel tests.  TRUE implies the arc passes
        /// through the pixel.  See class doc for the soundness-not-
        /// completeness caveats and the "circle vs arc" false-positive case.
        /// </summary>
        public static bool Evaluate(
            BPoint arcStart, BPoint arcMid, BPoint arcEnd,
            BPoint center, double scale)
        {
            double r = scale * 0.5;

            // Four pixel corners.  Same layout as oracle/driver.ml.
            var bl = new BPoint(center.X - r, center.Y - r);
            var br = new BPoint(center.X + r, center.Y - r);
            var tr = new BPoint(center.X + r, center.Y + r);
            var tl = new BPoint(center.X - r, center.Y + r);

            // Four edge-crossings (sign-product on InCircle determinants).
            // OCaml uses `||` which is short-circuit; C# `||` is the same.
            return Crosses(arcStart, arcMid, arcEnd, bl, br)
                || Crosses(arcStart, arcMid, arcEnd, br, tr)
                || Crosses(arcStart, arcMid, arcEnd, tr, tl)
                || Crosses(arcStart, arcMid, arcEnd, tl, bl)
                || InHotPixelHalfOpen(arcStart, center, scale)
                || InHotPixelHalfOpen(arcEnd,   center, scale);
        }

        // Inline replica of ArcChordCrossesCircle.Evaluate.  We keep it
        // inline (rather than calling through) to preserve the OCaml
        // structure verbatim, and because allocating a delegate or making
        // an extra public call here would obscure the line-by-line
        // correspondence with `run_arc_passes_through_pixel` -- which is
        // the auditable contract.
        private static bool Crosses(
            BPoint arcStart, BPoint arcMid, BPoint arcEnd,
            BPoint p, BPoint q)
        {
            double sp = InCircle.Determinant(arcStart, arcMid, arcEnd, p);
            double sq = InCircle.Determinant(arcStart, arcMid, arcEnd, q);
            return sp * sq < 0.0;
        }

        // Per-axis half-open membership.  Mirrors `in_hot_pixel_halfopen`
        // in oracle/driver.ml byte-for-byte:
        //   p.bx >= c.bx - r && p.bx < c.bx + r &&
        //   p.by >= c.by - r && p.by < c.by + r
        private static bool InHotPixelHalfOpen(BPoint p, BPoint c, double scale)
        {
            double r = scale * 0.5;
            return p.X >= c.X - r && p.X < c.X + r
                && p.Y >= c.Y - r && p.Y < c.Y + r;
        }
    }
}
