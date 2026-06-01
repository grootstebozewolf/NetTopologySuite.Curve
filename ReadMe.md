# NetTopologySuite.Curve

Fork of [NetTopologySuite/NetTopologySuite.Curve](https://github.com/NetTopologySuite/NetTopologySuite.Curve) — adds support for circular / curved geometries to NTS, plus an in-progress *formally-specified robust geometry layer* under `NetTopologySuite.Robust.*` that pairs each algorithm with a Coq specification and a reference implementation extracted from the proofs.

> Phase 0 has two slices done.  The greedy perpendicular-distance simplifier lives on [`phase0/verified-perp-simplifier`](https://github.com/grootstebozewolf/NetTopologySuite.Curve/tree/phase0/verified-perp-simplifier); the robust orientation predicate (Shewchuk Stage A filter — returns `Uncertain` rather than flip sign near collinear) lives on [`phase0/robust-orientation`](https://github.com/grootstebozewolf/NetTopologySuite.Curve/tree/phase0/robust-orientation).  Both pass 396 tests bit-exact against the Coq-extracted reference.  [Phase 0 status](#phase-0-status) has the details.

---

## Two parallel strands

### 1. Curved geometries (original mission, upstream-tracking)

Adds `CircularString`, `CompoundCurve`, `CurvePolygon`, `MultiCurve`, and the WKT/WKB I/O for them.  Mostly tracks `NetTopologySuite/NetTopologySuite.Curve` upstream.

The project is at an early stage, contributions are highly welcome.
Help is especially needed for:
- [ ] Unit tests
- [ ] I/O WKT and WKB
- [ ] Code documentation

### 2. Formally-specified robust geometry (new in this fork)

Goal: ship `NetTopologySuite.Robust.*` — drop-in alternatives to the floating-point-fragile predicates and algorithms in core NTS — paired with Coq specifications.

The work splits across two repositories:

| Repo | Role |
|---|---|
| [NetTopologySuite.Proofs](https://github.com/grootstebozewolf/NetTopologySuite.Proofs) | Coq specifications, structural lemmas, and the extracted **RocqRefRunner** binary used for differential testing. |
| **NetTopologySuite.Curve** (this repo) | C# implementations under `NetTopologySuite.Robust.*`, unit tests mirroring the Coq lemmas, and the RocqRef differential harness. |

The C# follows the Coq specification and matches it bit-for-bit on every shipped test case.  Structural properties (head preservation, length monotonicity, head membership, NaN safety) are proven Qed-closed in the Coq corpus and mirrored as unit tests on the C# side.  Full semantic soundness against the real-number model is future work, not claimed yet.

---

## Phase 0 status

Two slices have landed.  Both are on branches; both pass bit-exact against the Coq-extracted RocqRefRunner on Apple Silicon (.NET 10 + OCaml 5.4.1).

### Slice 1: greedy perpendicular-distance polyline simplifier

| Item | Where |
|---|---|
| Active branch | [`phase0/verified-perp-simplifier`](https://github.com/grootstebozewolf/NetTopologySuite.Curve/tree/phase0/verified-perp-simplifier) |
| Production code | [`src/NetTopologySuite.Curved/Robust/Simplify/`](https://github.com/grootstebozewolf/NetTopologySuite.Curve/tree/phase0/verified-perp-simplifier/src/NetTopologySuite.Curved/Robust/Simplify) |
| Coq spec | [`theories-flocq/Validate_binary64.v`](https://github.com/grootstebozewolf/NetTopologySuite.Proofs/blob/main/theories-flocq/Validate_binary64.v) |

- `GreedyPerpSimplifier` — idiomatic iterative implementation, single sweep with two indices.  Coq Fixpoint clause documented inline.
- `BPoint` / `B64Ops` — record + binary64 arithmetic helpers.  NaN-safe `Le` matches `b64_le` (false on NaN).
- 14 unit tests mirroring the Qed-closed structural lemmas (`_nil`, `_singleton`, `_two_points`, `_nonempty`, `_length_le`, `_preserves_head`, `_in_head`, plus collinear-drop and NaN-safety).
- 248 RocqRef differential cases across 5 deterministic fixtures + 150 randomised polylines + 93 adversarial (tight clusters, repeated points, near-collinear, NaN, huge magnitudes).  **262 / 262** bit-exact.

The R-bridge soundness theorem (`greedy_simplify_binary64_sound` — threading Flocq's no-overflow preconditions through the Fixpoint) is not yet proven and not stubbed with `Admitted`.

### Slice 2: robust orientation predicate (Shewchuk Stage A)

| Item | Where |
|---|---|
| Active branch | [`phase0/robust-orientation`](https://github.com/grootstebozewolf/NetTopologySuite.Curve/tree/phase0/robust-orientation) |
| Production code | [`src/NetTopologySuite.Curved/Robust/Orientation/`](https://github.com/grootstebozewolf/NetTopologySuite.Curve/tree/phase0/robust-orientation/src/NetTopologySuite.Curved/Robust/Orientation) |
| Coq spec | [`theories-flocq/Orientation_b64.v`](https://github.com/grootstebozewolf/NetTopologySuite.Proofs/blob/main/theories-flocq/Orientation_b64.v) |

- `RobustOrientation.Orient2d` — naive cross-product signed twice-area.
- `RobustOrientation.Sign` — 4-valued naive sign (`OrientSign` enum).
- `RobustOrientation.SignFiltered` — 5-valued Shewchuk Stage A sign (`OrientSignRobust` enum: `Pos`/`Neg`/`Zero`/`Nan`/`Uncertain`).  Returns `Uncertain` when `|det|` is within the forward-error bound `(3 + 16·eps)·eps · (|t1| + |t2|)` of zero, rather than risk a silent sign flip.
- Qed-closed structural lemmas on the Coq side: decidability, totality, distinctness across both 4-valued and 5-valued sign types.
- RocqRef differential against both `ORIENT` and `ORIENT_FILTERED` modes.  **396 / 396** bit-exact (262 simplifier + 134 orientation including filtered-sign agreement).

Shewchuk's deeper stages (B / C / D — expansion arithmetic that resolves `Uncertain` into a definite Pos/Neg/Zero) are deferred.  Callers facing `Uncertain` today either fall back to a higher-precision predicate or treat the triangle as collinear with a documented caveat.

The arithmetic identities that hold over ℝ (antisymmetry, cyclic permutation, translation invariance) are not yet claimed in binary64 — they need the same no-overflow precondition machinery deferred for the simplifier R-bridge.

---

## Running the tests

The unit tests stand alone — no special setup needed:

```bash
git checkout phase0/verified-perp-simplifier
git submodule update --init --depth 1
dotnet test test/NetTopologySuite.Curved.Test/ \
  --filter "FullyQualifiedName~GreedyPerp"
# 14/14 pass; 248 RocqRef cases marked Skipped.
```

To activate the full RocqRef differential suite you need the RocqRefRunner (the
"oracle") and `ROCQ_REF_BIN` pointed at it.  You don't have to build the proof
corpus locally — the [Proofs CI](https://github.com/grootstebozewolf/NetTopologySuite.Proofs/actions)
publishes a prebuilt `oracle-bin-linux` artifact on every successful run, and
[`scripts/fetch-oracle.sh`](scripts/fetch-oracle.sh) downloads the latest one:

```bash
# Needs a GitHub token with actions:read on the Proofs repo (artifacts are not
# anonymously downloadable, even for public repos). A fine-grained PAT scoped
# to NetTopologySuite.Proofs (Actions + Contents: read-only) is enough.
export GH_TOKEN=<your-token>

export ROCQ_REF_BIN="$(scripts/fetch-oracle.sh)"

dotnet test test/NetTopologySuite.Curved.Test/ \
  --filter "FullyQualifiedName~GreedyPerp"
# 262/262 pass, all bit-equal with the Coq spec.
```

`fetch-oracle.sh` grabs the newest non-expired `oracle-bin-linux` by default;
set `ARTIFACT_RUN=<run-id>` to pin a specific Proofs CI run.  The harness
self-gates on `ROCQ_REF_BIN`: leave it unset and the differential cases report
as Skipped while the structural unit tests still run.

Prefer to build the oracle from source instead?  Clone the Proofs repo, build
the corpus + extract OCaml (`make -f Makefile.gen`), compile the runner
(`make -C oracle`), and point `ROCQ_REF_BIN` at `oracle/oracle_bin`.

---

## What's next

- **Shewchuk Stages B / C / D** — expansion-arithmetic refinement that resolves `OrientSignRobust.Uncertain` into a definite Pos/Neg/Zero.  Same Coq-spec + RocqRef pattern.
- **Robust segment-segment intersection** — Phase 1 of the chokepoint roadmap.
- **CI integration** — the [`RocqRef differential`](.github/workflows/rocqref-differential.yml) workflow downloads the prebuilt `oracle-bin-linux` artifact from the Proofs CI (via [`scripts/fetch-oracle.sh`](scripts/fetch-oracle.sh)) and runs the differential suite as a PR gate.  Set the `PROOFS_ARTIFACT_TOKEN` repo secret (a PAT with `actions:read` on the Proofs repo) to arm the differential cases; without it the gate runs the structural unit tests only.  Building the runner in-container from the proof corpus remains a possible future addition.
