// =============================================================================
// NetTopologySuite.Curve.Robust.Overlay.EdgeInResult
// -----------------------------------------------------------------------------
// C# transliteration of the Coq `edge_in_result` Definition from
//   theories/OverlayGraph.v:375 in NetTopologySuite.Proofs:
//
//     Definition edge_in_result (op : BooleanOp) (l : EdgeLabel) : bool :=
//       match op with
//       | Union        => orb  (in_left l) (in_right l)
//       | Intersection => andb (in_left l) (in_right l)
//       | Difference   => andb (in_left l) (negb (in_right l))
//       | SymDiff      => xorb (in_left l) (in_right l)
//       end.
//
// Pure boolean function -- no floating point, no precondition, total over
// the 16 cell domain (BooleanOp x in_left x in_right).  The non-short-
// circuiting bitwise operators `|`, `&`, `^` are used (rather than `||`,
// `&&`) to mirror Coq's `orb` / `andb` / `xorb`, which are strict.
//
// The oracle binary exposes this via the EDGE_IN_RESULT mode in
// oracle/driver.ml; the full 16-row truth table is asserted bit-for-bit
// by EdgeInResultRocqRefTests in the .Test project.
// =============================================================================

using System;

namespace NetTopologySuite.Robust.Overlay
{
    /// <summary>
    /// Predicate: does an overlay graph edge with label <paramref name="label"/>
    /// belong to the result of boolean operation <paramref name="op"/>?
    /// Mirrors Coq <c>edge_in_result</c> at <c>theories/OverlayGraph.v:375</c>.
    /// </summary>
    public static class EdgeInResult
    {
        /// <summary>
        /// Evaluate the predicate for the given operation and edge label.
        /// Total over the 4 x 2 x 2 = 16 cell input domain.
        /// </summary>
        public static bool Evaluate(BooleanOp op, EdgeLabel label)
        {
            bool l = label.InLeft;
            bool r = label.InRight;
            switch (op)
            {
                case BooleanOp.Union:        return l | r;
                case BooleanOp.Intersection: return l & r;
                case BooleanOp.Difference:   return l & !r;
                case BooleanOp.SymDiff:      return l ^ r;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(op), op, "unknown BooleanOp");
            }
        }
    }
}
