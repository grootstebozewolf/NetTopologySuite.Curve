# NetTopologySuite.Curve
This project aims to add support for __circular__ geometries to the [NetTopologySuite](/NetTopologySuite/NetTopologySuite) project.

The project is at an early stage, contributions are highly welcome.
Help is especially needed for:
- [] Unit tests
- [] I/O WKT and WKB
- [] Code documentation

Stay tuned.

### RocqRef-backed Perpendicular Simplifier

The greedy perpendicular-distance simplifier under `NetTopologySuite.Robust.Simplify`
follows the specification and structural properties proved in the companion
[NetTopologySuite.Proofs](https://github.com/grootstebozewolf/NetTopologySuite.Proofs)
corpus (`theories-flocq/Validate_binary64.v`).  Differential testing against the
extracted reference binary (**RocqRefRunner**, pointed to by the `ROCQ_REF_BIN`
environment variable) ensures bit-exact agreement on every test case.

Full semantic soundness (binary64 ↔ ℝ-model) is future work; what is claimed
today is structural correctness (head preservation, length-monotonicity,
non-emptiness, head membership) plus bit-exact agreement with the Coq spec on
all current fixtures, randomised cases, and adversarial families.

