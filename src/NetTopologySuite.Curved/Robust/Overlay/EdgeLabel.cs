// =============================================================================
// NetTopologySuite.Curve.Robust.Overlay.EdgeLabel
// -----------------------------------------------------------------------------
// C# transliteration of the Coq `EdgeLabel` record from
//   theories/OverlayGraph.v in NetTopologySuite.Proofs:
//
//     Record EdgeLabel : Type := { in_left : bool; in_right : bool }.
//
// Each edge in the overlay graph carries one of these labels.  `in_left`
// records whether the edge belongs to (a side / interior region of) the
// left operand A; `in_right` likewise for the right operand B.  The
// boolean overlay result for an edge is then computed by
// <see cref="EdgeInResult.Evaluate"/> from the active <c>BooleanOp</c>
// and this label, matching the Coq definition
//   `edge_in_result : BooleanOp -> EdgeLabel -> bool`
// at OverlayGraph.v:375.
//
// The label merge for shared edges (when one geometry's edge coincides
// with another's) is the pointwise <c>||</c> of the two flags; that
// merge is the responsibility of the labelling pipeline (Coq:
// `merge_labels` at OverlayGraph.v:411) and is not modelled here -- this
// struct only carries the resulting label.
// =============================================================================

namespace NetTopologySuite.Robust.Overlay
{
    /// <summary>
    /// Two-bool label attached to an overlay graph edge.  Mirrors the Coq
    /// <c>EdgeLabel</c> record in <c>theories/OverlayGraph.v</c>.
    /// </summary>
    public readonly struct EdgeLabel
    {
        /// <summary>True iff the edge belongs to the left operand <c>A</c>.</summary>
        public bool InLeft { get; }

        /// <summary>True iff the edge belongs to the right operand <c>B</c>.</summary>
        public bool InRight { get; }

        /// <summary>Construct an <see cref="EdgeLabel"/>.</summary>
        public EdgeLabel(bool inLeft, bool inRight)
        {
            InLeft = inLeft;
            InRight = inRight;
        }
    }
}
