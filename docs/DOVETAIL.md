# DOVETAIL — bring NetTopologySuite.Curve onto upstream NetTopologySuite/develop

This document tracks the multi-session refactor that decouples
`NetTopologySuite.Curve` from the deleted `enhancement/curved` branch
of upstream `NetTopologySuite/NetTopologySuite` and brings the
submodule pin onto current `develop`.

It is the deliverable of **Session 1**.  Sessions 2–10 each ship their
own PR with the concrete code work, branching off `phase1/robust-intersect`.

## Why

Upstream deleted the `enhancement/curved` branch this fork was built
on.  The curve extensibility layer the fork's
`src/NetTopologySuite.Curved/` depends on — `Curve : Geometry`,
`Surface<T>`, `ILinearizable<T>`, `SortIndexValue`, public
`TokenStream`, and `WKB/WKT*Ex.ReadOther*` / `WriteOther*` override
hooks — lived only on that branch and was never merged to `develop`.

The submodule is therefore frozen on commit `2772c9b3` (June 2021).
A blind bump to `develop` tip produces ~74 build errors across 10
distinct categories.  This refactor vendors the missing infrastructure
fork-locally and re-architects the IO extensibility from "override
hooks" to "composition", so the submodule can finally move forward.

## Inventory of upstream-removed dependencies

### Types removed from upstream develop

| Type | On 2772c9b3 | On develop | Fork usage |
|---|---|---|---|
| `Curve : Geometry` | ✅ public abstract | ❌ removed | base of `CircularString`, `CompoundCurve`, `MultiCurve` references; also used as type-check (`if (!(o is Curve))`) in `CircularString.Equals` |
| `abstract class Surface<T>` | ✅ | ❌ removed | base of `CurvePolygon : Surface<Curve>` |
| `interface ILinearizable<T>` | ✅ | ❌ removed | implemented by `CircularString`, `CompoundCurve`, `MultiCurve`, `CurvePolygon`, `MultiSurface`; type-checked in `CurvePolygon.IsEquivalentClass`, `CurveGeometryOverlay`, `NewLinearizedGeometry<T>` |
| `SortIndexValue` enum | ✅ | ❌ removed | `protected override SortIndexValue SortIndex` on every fork curve type — controls ordering in collection operations |
| `class TokenStream` | ✅ public | ⚠️ `internal` | parameter type on `WKTReaderEx.ReadOtherGeometryText` and on every private parser method |

### Virtual hooks removed from upstream develop

| Hook on upstream | On 2772c9b3 | On develop | Fork override |
|---|---|---|---|
| `WKBReader.ReadOtherGeometry(uint, BinaryReader, WKBReader.CoordinateSystem, int)` | ✅ `protected virtual` | ❌ removed | `WKBReaderEx.ReadOtherGeometry` |
| `WKBWriter.GetOtherGeometryRequiredBufferSize(Geometry, bool)` | ✅ | ❌ removed | `WKBWriterEx.GetOtherGeometryRequiredBufferSize` |
| `WKBWriter.GetGeometryType(Geometry)` | ✅ `protected virtual` | ❌ removed | `WKBWriterEx.GetGeometryType` |
| `WKBWriter.WriteOtherGeometry(Geometry, BinaryWriter, bool)` | ✅ | ❌ removed | `WKBWriterEx.WriteOtherGeometry` |
| `WKTReader.ReadOtherGeometryText(string, TokenStream, GeometryFactory, Ordinates)` | ✅ `protected virtual` | ❌ removed | `WKTReaderEx.ReadOtherGeometryText` |
| `WKTWriter.AppendOtherGeometryTaggedText(Geometry, Ordinates, bool, bool, int, TextWriter, OrdinateFormat)` | ✅ | ❌ removed | `WKTWriterEx.AppendOtherGeometryTaggedText` |

## Strategy

**Parallel-source vendoring with the pin on 2772c9b3 throughout
sessions 2–8.**

1. Vendor each missing upstream type into a fork-local namespace
   (proposed: `NetTopologySuite.Curved.Compat`) so the fork no longer
   inherits from or references the upstream-removed types.  Sessions
   2–4.
2. Re-architect the IO extensibility from "override the upstream
   virtual" to "compose with a fork-local serialiser that dispatches
   to upstream for known types and to the fork-local types
   otherwise".  Sessions 5–7.
3. After session 7 the fork's `src/NetTopologySuite.Curved/` no longer
   references any of the upstream-removed types.  Build is green on
   the 2772c9b3 pin.  Bump can proceed.  Session 9.
4. Session 10: integration verification, re-enable [Ignore]'d tests,
   add a CI lane that builds against `origin/develop` tip to catch
   future upstream drift early.

Each session ends with: build green, full test suite green
(modulo the standing `Assert.Ignore`s from PR #3, which session 8
may revisit), PR merged.

## Sessions

### Session 1 — Inventory + design doc (this PR)

Deliverable: this file (`docs/DOVETAIL.md`).  No code change.

Acceptance:
- Inventory tables above match what the failed bump experiment
  surfaced.
- Per-session task list below is concrete enough that any developer
  (human or Claude) can pick up the next session from cold.

### Session 2 — Vendor `ILinearizable<T>` + `SortIndexValue`

Foundational types with no dependencies on the other vendored items.
Lowest-risk starting point.

Tasks:
- Copy `NetTopologySuite/src/NetTopologySuite/Geometries/ILinearizable.cs`
  from 2772c9b3 into
  `src/NetTopologySuite.Curved/Compat/ILinearizable.cs`, namespaced
  `NetTopologySuite.Curved.Compat`.
- Locate `SortIndexValue` on 2772c9b3 (it's an enum inside
  `Geometry.cs` or its own file — check both), copy into
  `src/NetTopologySuite.Curved/Compat/SortIndexValue.cs`.
- Update every fork file that references `ILinearizable<T>` to
  `using NetTopologySuite.Curved.Compat;` and qualify references.
- For `protected override SortIndexValue SortIndex` — the upstream
  type still exists on 2772c9b3, so for this session the override
  still works *against the upstream type*.  In session 8 we'll decide
  whether to keep the override semantically (with our own
  `SortIndexValue`) or drop it.  Add a `TODO(dovetail-8)` comment.

Acceptance: build green, all non-`Assert.Ignore` tests pass.

### Session 3 — Vendor `Curve : Geometry`

Tasks:
- Copy `NetTopologySuite/src/NetTopologySuite/Geometries/Curve.cs` from
  2772c9b3 into `src/NetTopologySuite.Curved/Compat/Curve.cs`,
  namespaced `NetTopologySuite.Curved.Compat`.
- Resolve any references inside the vendored file to other
  upstream-removed types (likely `SortIndexValue` from session 2).
- Change `CircularString`, `CompoundCurve`, `MultiCurve` to derive from
  `NetTopologySuite.Curved.Compat.Curve` instead of upstream `Curve`.
- Update type-check sites (`if (!(o is Curve))`) to reference the
  fork-local type.

Acceptance: build green; all tests still pass.

### Session 4 — Vendor `Surface<T>`

Tasks:
- Copy `Surface.cs` from 2772c9b3 into
  `src/NetTopologySuite.Curved/Compat/Surface.cs`.
- Resolve internal references.
- Change `CurvePolygon : Surface<Curve>` and `MultiSurface :
  Surface<...>` to reference the fork-local versions.

Acceptance: build green; all tests still pass.

### Session 5 — Re-architect `WKTReaderEx`

Upstream develop made `TokenStream` `internal`; the override hook
`ReadOtherGeometryText` was removed.

Tasks:
- Copy `TokenStream.cs` (and any helper it uses) from 2772c9b3 into
  `src/NetTopologySuite.Curved/Compat/IO/TokenStream.cs`, kept public.
- Replace `WKTReaderEx : WKTReader` (override) with a new design:
  `CurveWKTReader` that *contains* (composes) a standard
  `WKTReader` and parses curve-specific WKT entry points itself,
  routing non-curve types to the contained reader.
- Update any test / factory hookup that previously relied on the
  upstream override dispatch.

Acceptance: build green; existing WKT-reading tests pass (including
the curve WKT cases used by `CircularStringImplTest`,
`CompoundCurveImplTest`, etc.).

### Session 6 — Re-architect `WKBReaderEx` / `WKBWriterEx`

Same composition shift, applied to the WKB layer.  `ReadOtherGeometry`,
`GetOtherGeometryRequiredBufferSize`, `GetGeometryType`, and
`WriteOtherGeometry` override hooks all go away on develop.

Tasks:
- New `CurveWKBReader` composes a standard `WKBReader`, intercepts
  the curve geometry-type codes, and dispatches to internal curve
  readers for those; everything else flows through the standard reader.
- New `CurveWKBWriter` similarly.
- Match the existing fork-side IO surface so callers don't notice.

Acceptance: build green; WKB round-trip tests pass.

### Session 7 — Re-architect `WKTWriterEx`

Same composition shift on the WKT-writing side
(`AppendOtherGeometryTaggedText` override hook is gone on develop).

Acceptance: build green; WKT round-trip tests pass.

### Session 8 — Pre-bump verification + (optional) source bug fixes

At this point `src/NetTopologySuite.Curved/` no longer references any
upstream-removed type or override hook.  Build is still green on the
2772c9b3 pin.

Tasks:
- Run the full test suite (`dotnet test --blame-hang-timeout 60s`).
- Revisit the six `Assert.Ignore`d tests in PR #3
  (`CurvePolygonImplTest.TestApplyCoordinateSequenceFilter` and
  friends, `MultiCurve/MultiSurface.TestSerializeability`).  The
  underlying bugs in `CurvePolygon.Apply` and `MultiCurve.Apply`
  (`GeometryChanged()` -> `Linearize()` recursion) may be fixable
  now that we own all the relevant types — try.
- Decide on the future of the `SortIndex` override (cosmetic vs
  semantic — sessions 2–4 left this open).

Acceptance: still green.  Number of `Assert.Ignore` calls decreased
(ideally).

### Session 9 — Bump submodule to `origin/develop` tip

Tasks:
- Update submodule pointer to `origin/develop` tip.
- Build — expect API drift from 386 upstream commits over 4.5 years
  (parameter renames, removed obsolete overloads, etc.) but **no
  more curve-infra errors**, since sessions 2–7 vendored / replaced
  all of those.
- Fix each remaining error.
- Run full test suite.

Acceptance: matrix `Build (ubuntu / windows / macOS)` + `rocqref`
all green against the develop-tip submodule.

### Session 10 — Integration verification + CI

Tasks:
- Add a CI lane (or modify existing) that periodically bumps the
  submodule to `origin/develop` and runs the suite, so future
  upstream drift is caught early instead of accumulating into
  another 4.5-year debt.
- Re-evaluate any remaining `Assert.Ignore`s.
- Update the project README and `.gitmodules` comment block.

Acceptance: PR merged; the comment block in `.gitmodules` warning
against `--remote` (from PR #4) is replaced with an "auto-bumped
weekly by CI" note.

## Out of scope

- Resurrecting or contributing the curve extensibility layer back
  upstream.  That would be a separate community-coordination effort.
- Rewriting the curve geometry types' algorithms.  We're keeping
  them as-is; only their type-hierarchy parents and IO plumbing
  change.

## Rollback

Every session is its own squash-merge commit on
`phase1/robust-intersect`.  Each can be reverted independently with
`git revert <session-commit>` if it causes problems downstream.  Until
session 9, no submodule-pointer change is made, so the production
build is unaffected by sessions 1–8 except for the new fork-local
types in `src/NetTopologySuite.Curved/Compat/`.
