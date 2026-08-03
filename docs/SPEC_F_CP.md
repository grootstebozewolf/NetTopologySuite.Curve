# SPEC_F_CP — Structural CurvePolygon (C# side)

**Phase 1 / TAG F-CP** of the SFA / ISO 19125-2 Curve Awareness epic
(see [locationtech/jts#1195](https://github.com/locationtech/jts/issues/1195)).

## The TAG in one sentence

A `CurvePolygon` whose **shell** and **holes** can be curved (`CircularString` /
`CompoundCurve`) instead of flat (`LinearRing`), and that exposes those rings
as `Curve` instances to curve-aware code paths.

## Why this is the gating phase

Every Phase 2 TAG that asks a question *about* a curved boundary needs to be
able to *get hold of* one. Without F-CP, `B-CP` (curved boundary),
`M-AREA-CP` (Green's-theorem area with circular-segment correction), and
`V-CP` (curved-polygon validity) all have nothing to operate on.

## Status: green by architecture (no Java-style port required)

This document is the C# companion to JTS' `modules/curved/SPEC_F_CP.md` from
the `feature/sfa-curve-F-CP-spike-optionA` branch. The Java spec lays out
three options (A — legacy fallback, B — widened return type, C — fail-fast)
because JTS' `Polygon.getExteriorRing(): LinearRing` *forces* a dovetail
decision (epic §7 risk #1, FCP-DOVE).

**That dovetail does not exist in NetTopologySuite.Curve.** The repo already
uses a different architectural choice — its `CurvePolygon` extends
`Surface<Curve>` (the generic abstract base introduced on the
`enhancement/curved` branch by FObermaier), not the concrete `Polygon` class:

```csharp
public class CurvePolygon : Surface<Curve>, ILinearizable<Polygon>
{
    public override Curve ExteriorRing { get; }
    public override Curve GetInteriorRingN(int index) { ... }
}
```

The shell and holes are typed as `Curve`, the abstract base of `LinearRing`,
`LineString`, `CircularString`, and `CompoundCurve`. Curve-aware callers go
through the typed accessor and pattern-match for the subtype they need.
Linearisation-aware callers go through `Linearize()` for the legacy `Polygon`
projection.

This is, in effect, the **JTS Option-B endpoint reached before the legacy API
ever existed** — there is no `getExteriorRing(): LinearRing` to widen, so there
is no widening blast radius to count.

## What F-CP means for this repo

The TAG, sub-TAG by sub-TAG, mapped to the C# implementation:

| Sub-TAG    | Java side (Option A)                                     | C# side (today)                                                                              | Status              |
|------------|----------------------------------------------------------|----------------------------------------------------------------------------------------------|---------------------|
| **FCP-S**  | New `structuralShell : LineString` field + `getExteriorCurve()` accessor.            | `ExteriorRing : Curve` is the structural accessor by design.                                  | green by architecture |
| **FCP-MEM**| Reader must preserve `CIRCULARSTRING`/`COMPOUNDCURVE` member subtypes on parse.       | Depends on `WKTReaderEx` / `WKBReaderEx` behavior. **Verified by spec test.**                 | covered by spec test |
| **FCP-H**  | Holes typed as `Curve` (not `LinearRing`).                                            | `GetInteriorRingN(int) : Curve` returns whatever subtype was supplied.                        | green by architecture |
| **FCP-CP** | `copyInternal()` preserves shell + holes as Curves.                                   | `CopyInternal()` deep-copies each `Curve` via its own `Copy()`; subtype preserved.            | green by architecture |
| **FCP-TL** | `toLinear(tolerance)` walks shell + each hole at the given tolerance.                 | `Linearize(arcSegmentLength)` walks shell + each hole, asking each its own linearisation.     | green by architecture |
| **FCP-WKT**| Round-trip via `CurvedWKTWriter` + `CurvedWKTReader` preserves the structural tag.    | Depends on `WKTWriterEx` / `WKTReaderEx`. **Verified by spec test.**                          | covered by spec test |
| **FCP-DOVE** | Pick A / B / C for the legacy `getExteriorRing()` API contract.                     | **N/A.** No legacy `LinearRing`-typed accessor exists; nothing to dovetail.                   | not applicable      |

The four "green by architecture" rows are not unverified claims — they are
pinned by `test/NetTopologySuite.Curve.Test/CurveAwareness/CurvePolygonStructuralSpec.cs`,
which asserts each promise against a real
`CURVEPOLYGON (COMPOUNDCURVE (CIRCULARSTRING (...)...), ...)` constructed
geometry. If a future refactor accidentally collapses the shell to a flat
`LinearRing` during copy or read, that test goes red and the regression has
a name.

### Empirical status as of 2026-05-15

Spec class run locally against `enhancement/curved` HEAD (2772c9b3) + the
.NET 10 wake-up + rename PRs (#7 and #8):

```
Passed FCP_S_compound_shell_exposed_as_CompoundCurve
Passed FCP_S_arc_shell_exposed_as_CircularString
Passed FCP_MEM_compound_shell_members_retain_subtypes
Passed FCP_H_arc_hole_exposed_as_CircularString
Passed FCP_CP_copy_preserves_shell_subtype
Passed FCP_CP_copy_preserves_arc_hole_subtype
Passed FCP_TL_linearisation_walks_shell_and_holes
Passed FCP_WKT_roundtrip_preserves_arc_shell
Passed FCP_WKT_roundtrip_preserves_compound_shell
Passed FCP_DOVE_not_applicable_in_csharp

Test Run Successful. Total tests: 10
```

**All ten green** — including the two TAGs (FCP-MEM and FCP-WKT) that were
flagged as reader/writer-dependent. `WKTReaderEx` already preserves
`COMPOUNDCURVE` member subtypes on parse; `WKTWriterEx` already emits the
`COMPOUNDCURVE` / `CIRCULARSTRING` tags in the body when given a structural
`CurvePolygon`. So in C#, **F-CP needs no implementation work** — only the
regression net this PR adds.

## Cross-reference to the Coq proofs

When the F-CP spec test asserts that `CurvePolygon.Linearize()` produces a
geometrically faithful flat `Polygon`, the algebraic backing is in:

- [`Vec.v`](https://github.com/grootstebozewolf/NetTopologySuite.Proofs/blob/develop/theories/Vec.v) —
  `vmag_sq_nonneg` and the bilinear-dot-product laws underwrite the chord-length
  bound `Linearize` reports it stays within.
- [`Bbox.v`](https://github.com/grootstebozewolf/NetTopologySuite.Proofs/blob/develop/theories/Bbox.v) —
  `disjoint_bboxes_imply_no_shared_point` is the formal justification for
  the envelope short-circuit `CurvePolygon.ComputeEnvelopeInternal` uses.

These are not load-bearing for F-CP itself (F-CP is pure type-discipline plumbing),
but the Phase-2 metric and validity TAGs that follow will lean on the proofs
explicitly. The pattern: when a TAG's behavior is justified by a proof, the
C# code comment cites it as `(* see NetTopologySuite.Proofs/theories/Foo.v#bar *)`.

## What's NOT in this PR

- **Implementation changes to `CurvePolygon.cs` itself.** The architectural
  choice that makes F-CP green-by-default is FObermaier's, on
  `enhancement/curved` (NTS PR #526). This PR documents and *pins* that
  choice; it does not modify it.
- **WKT / WKB reader-side member preservation.** The spec test asserts the
  expected behavior; if it goes red, the reader needs surgery and that is
  its own PR (likely titled `feat: F-CP-MEM CurvedWKTReader preserves
  CIRCULARSTRING member subtype on CurvePolygon shell read`).
- **F-MC / F-MS / F-RD.** The other Phase 1 sub-TAGs. F-MC and F-MS are
  reportedly half-already-free on the JTS Phase-1 base (per
  [JTS #1195 comment 4436748851](https://github.com/locationtech/jts/issues/1195#issuecomment-4436748851));
  a follow-up PR will run the same SPEC + test pattern for both.

## Smallest concrete next step

Land this PR (or close it as already-true and replace with whatever spec
form the maintainers prefer). Then run the spec class — three outcomes:

1. **All green.** F-CP is done; the spec stays in place as a regression net.
2. **FCP-MEM red.** Reader doesn't preserve compound-curve members. Next PR
   is the F-CP-MEM reader fix.
3. **FCP-WKT red.** Writer emits flat polygon body. Next PR is the F-CP-WKT
   writer fix.

The spec class itself stays in the codebase indefinitely — converting a red
to a green is editing the assertion message (or, if the maintainers prefer
the JTS "delete on green" convention, deleting the method). In NTS' more
mature codebase the assert-style "regression net" is the more natural fit.

## AI assistance disclosure

This document is AI-drafted and human-reviewed. AI-generated portions are
dedicated to CC0-1.0; human curation falls under the NTS BSD-3-Clause grant.

```
SPDX-License-Identifier: BSD-3-Clause AND CC0-1.0
Assisted-by: Claude (Opus-4.7)
```
