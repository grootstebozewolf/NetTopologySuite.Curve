# NetTopologySuite.Curve
This project aims to add support for __circular__ geometries to the [NetTopologySuite](/NetTopologySuite/NetTopologySuite) project.

The project is at an early stage, contributions are highly welcome.
Help is especially needed for:
- [] Unit tests
- [] I/O WKT and WKB
- [] Code documentation

Stay tuned.

### RocqRef-backed predicates

Three robust-predicate components under `NetTopologySuite.Robust` follow the
specification proved in the companion
[NetTopologySuite.Proofs](https://github.com/grootstebozewolf/NetTopologySuite.Proofs)
corpus, and are differentially tested against the Rocq-extracted reference
binary (**RocqRefRunner**, pointed to by the `ROCQ_REF_BIN` environment
variable):

| Component | Proof source | Oracle mode(s) | Test fixture |
|-----------|--------------|----------------|--------------|
| `Robust.Simplify.GreedyPerpSimplifier` | `theories-flocq/Validate_binary64.v` | `SIMPLIFY` | `GreedyPerpSimplifierRocqRefTests` |
| `Robust.Orientation.RobustOrientation` | `theories-flocq/Orientation_b64.v` | `ORIENT` + `ORIENT_FILTERED` | `RobustOrientationRocqRefTests` |
| `Robust.Intersect.RobustLineIntersector` | `theories-flocq/Intersect_b64.v` + `Intersect_b64_exact.v` | `INTERSECT_FILTERED` + `INTERSECT_POINT_FILTERED` | `RobustLineIntersectorRocqRefTests` |

The fixtures `Assert.Ignore` when `ROCQ_REF_BIN` is unset, so the regular
build matrix (macOS / Windows / Linux without an oracle build) stays green
without the Rocq toolchain installed.  The dedicated `rocqref` CI job in
[`dotnet.yml`](.github/workflows/dotnet.yml) builds the oracle binary
inside the pinned Rocq + Flocq Docker image used by the Proofs corpus and
runs the categorised tests for real.  To exercise the same path locally:

```bash
# 1.  Build the oracle binary from a sibling NetTopologySuite.Proofs checkout.
cd ../NetTopologySuite.Proofs
rocq makefile -f _CoqProject.full -o Makefile.gen
make -f Makefile.gen
make -C oracle

# 2.  Point the env var at it and run only the RocqRef-categorised fixtures.
export ROCQ_REF_BIN=$PWD/oracle/oracle_bin
cd ../NetTopologySuite.Curve
dotnet test -c Release --filter "FullyQualifiedName~RocqRef"
```

Full semantic soundness (binary64 ↔ ℝ-model) is future work; what is claimed
today is structural correctness (head preservation, length-monotonicity,
non-emptiness, head membership for the simplifier; four-valued sign agreement
for the orientation predicate; five-valued sign + intersection-point
agreement for the line intersector) plus bit-exact agreement with the
Coq-extracted reference on every current fixture.

