# NetTopologySuite.Curve

Fork of [NetTopologySuite/NetTopologySuite.Curve](https://github.com/NetTopologySuite/NetTopologySuite.Curve) — adds support for circular / curved geometries to NTS, plus an in-progress *formally-specified robust geometry layer* under `NetTopologySuite.Robust.*` that pairs each algorithm with a Coq specification and a reference implementation extracted from the proofs.

> Phase 0 — a greedy perpendicular-distance polyline simplifier with a Coq specification — is essentially done.  It lives on [`phase0/verified-perp-simplifier`](https://github.com/grootstebozewolf/NetTopologySuite.Curve/tree/phase0/verified-perp-simplifier), with 262 tests passing bit-exact against the Coq-extracted reference.  [Phase 0 status](#phase-0-status) below has the details.

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

The first algorithm slice — the greedy perpendicular-distance polyline simplifier — is feature-complete and shipped on a branch.

| Item | Where |
|---|---|
| Active branch | [`phase0/verified-perp-simplifier`](https://github.com/grootstebozewolf/NetTopologySuite.Curve/tree/phase0/verified-perp-simplifier) |
| Production code | [`src/NetTopologySuite.Curved/Robust/Simplify/`](https://github.com/grootstebozewolf/NetTopologySuite.Curve/tree/phase0/verified-perp-simplifier/src/NetTopologySuite.Curved/Robust/Simplify) |
| Tests | [`test/NetTopologySuite.Curved.Test/Robust/Simplify/`](https://github.com/grootstebozewolf/NetTopologySuite.Curve/tree/phase0/verified-perp-simplifier/test/NetTopologySuite.Curved.Test/Robust/Simplify) |
| Coq spec | [`theories-flocq/Validate_binary64.v`](https://github.com/grootstebozewolf/NetTopologySuite.Proofs/blob/main/theories-flocq/Validate_binary64.v) |
| RocqRefRunner build | [`oracle/`](https://github.com/grootstebozewolf/NetTopologySuite.Proofs/tree/main/oracle) in the proofs repo |

### What landed

- **`GreedyPerpSimplifier`** — idiomatic iterative implementation, single sweep with two indices.  The Coq Fixpoint clause it transliterates is documented inline.
- **`BPoint` / `B64Ops`** — record + binary64 arithmetic helpers.  NaN-safe `Le` matches the Coq `b64_le` semantics (`false` on either operand NaN).
- **14 unit tests** — one per Qed-closed structural lemma in the Coq corpus (`_nil`, `_singleton`, `_two_points`, `_nonempty`, `_length_le`, `_preserves_head`, `_in_head`, plus collinear-drop and NaN-safety expectations).
- **248 RocqRef differential cases** across five families:
  - 5 deterministic fixtures.
  - 150 randomised polylines (uniform in `[-5, 5]^2`).
  - 24 tight-cluster cases (subnormal scales).
  - 12 repeated-point cases.
  - 45 near-collinear cases at machine-epsilon `dy`.
  - 3 NaN-position cases.
  - 9 huge-magnitude cases (up to `1e300`).

All 262 tests pass bit-exact on the dev box (Apple Silicon, OCaml 5.4.1, .NET 10).

### Soundness bridge (not yet)

The R-bridge soundness theorem (`greedy_simplify_binary64_sound` — threading Flocq's no-overflow preconditions through the Fixpoint) is not yet proven.  The `PROOF STATUS` block at the top of `Validate_binary64.v` says so explicitly.  It is also not stubbed with `Admitted`; the corpus holds the "no Admitted, no Axiom, no Parameter" invariant uniformly.

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

To activate the full RocqRef differential suite, build the RocqRefRunner from the proofs repo and point `ROCQ_REF_BIN` at it:

```bash
git clone https://github.com/grootstebozewolf/NetTopologySuite.Proofs.git
# Build the proof corpus + extract OCaml (container or local rocq + coq-flocq):
#   make -f Makefile.gen
# Compile the RocqRefRunner:
make -C NetTopologySuite.Proofs/oracle
export ROCQ_REF_BIN=$(pwd)/NetTopologySuite.Proofs/oracle/oracle_bin

dotnet test test/NetTopologySuite.Curved.Test/ \
  --filter "FullyQualifiedName~GreedyPerp"
# 262/262 pass, all bit-equal with the Coq spec.
```

---

## What's next

Phase 1 is where the real questions are: robust 2D orientation predicates (Shewchuk-style adaptive precision) and snap rounding.  Past that:

- Robust segment-segment intersection, same Coq-spec + RocqRef pattern.
- CI integration — a workflow that builds the RocqRefRunner in a container and runs the differential suite as a PR gate.
