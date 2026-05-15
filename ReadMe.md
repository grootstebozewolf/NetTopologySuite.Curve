# NetTopologySuite.Curve

Fork of [NetTopologySuite/NetTopologySuite.Curve](https://github.com/NetTopologySuite/NetTopologySuite.Curve) — adds support for circular / curved geometries to NTS, **plus** an in-progress *formally-specified robust geometry layer* under `NetTopologySuite.Robust.*` that pairs each algorithm with a Coq specification and a differential testing oracle.

> **Reviewer's quick read.** Phase 0 (the greedy perpendicular-distance polyline simplifier) is finished and lives on the branch [`phase0/verified-perp-simplifier`](https://github.com/grootstebozewolf/NetTopologySuite.Curve/tree/phase0/verified-perp-simplifier). 262/262 tests pass bit-exact against the Coq-extracted reference binary. Jump to [Phase 0 status](#phase-0-status) for the details.

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
| [NetTopologySuite.Proofs](https://github.com/grootstebozewolf/NetTopologySuite.Proofs) | Coq specifications, structural lemmas, and the extracted **RocqRefRunner** binary used as a differential testing oracle. |
| **NetTopologySuite.Curve** (this repo) | Production C# implementations under `NetTopologySuite.Robust.*`, unit tests mirroring the Coq lemmas, and the RocqRef differential harness. |

**Honest framing.** The C# implementations are *Coq-specified* and *structurally verified* (head preservation, length monotonicity, head membership, NaN safety, etc.) and *bit-exact* against the Coq-extracted reference on every test case we ship.  Full semantic soundness against the real-number model is future work and is **not** claimed.

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

### What is deliberately deferred

The headline soundness theorem `greedy_simplify_binary64_sound` (R-bridge with no-overflow precondition threading) is not yet claimed — see the `PROOF STATUS` block at the top of `Validate_binary64.v` in the proofs repo.  It is explicitly *not* stubbed with `Admitted`; the file holds the corpus-wide "no Admitted, no Axiom, no Parameter" invariant.

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

To activate the full RocqRef differential suite, build the oracle binary from the proofs repo and point `ROCQ_REF_BIN` at it:

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

## Roadmap (not yet started)

- **Robust 2D orientation predicate** — Shewchuk-style adaptive precision, Coq-spec + RocqRef pattern.
- **Robust segment-segment intersection** — same pattern, with the perpendicular-distance regime already proven structurally as a stepping stone.
- **CI integration** — workflow that builds the RocqRefRunner in a container and runs the differential suite as a PR gate.
