// =============================================================================
// NetTopologySuite.Curve.Robust.Overlay.BooleanOp
// -----------------------------------------------------------------------------
// C# transliteration of the Coq `BooleanOp` Inductive from
//   theories/Overlay.v:212 in NetTopologySuite.Proofs:
//
//     Inductive BooleanOp : Type :=
//     | Union
//     | Intersection
//     | Difference
//     | SymDiff.
//
// Identical layout in the OCaml extraction (oracle/driver.ml parses the
// uppercase token names `UNION | INTERSECTION | DIFFERENCE | SYMDIFF` and
// dispatches to the matching `BooleanOp` constructor).
// =============================================================================

namespace NetTopologySuite.Robust.Overlay
{
    /// <summary>
    /// Four-valued boolean overlay operation, mirroring the Coq
    /// <c>BooleanOp</c> Inductive in <c>theories/Overlay.v</c>.
    /// </summary>
    public enum BooleanOp
    {
        /// <summary>Set union: <c>A ∪ B</c>.</summary>
        Union = 0,

        /// <summary>Set intersection: <c>A ∩ B</c>.</summary>
        Intersection = 1,

        /// <summary>Set difference: <c>A \ B</c>.</summary>
        Difference = 2,

        /// <summary>Symmetric difference: <c>(A ∪ B) \ (A ∩ B)</c>.</summary>
        SymDiff = 3,
    }
}
