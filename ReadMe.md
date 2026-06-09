# NetTopologySuite.Curve

This project adds support for **circular** and other curved geometries to
[NetTopologySuite](https://github.com/NetTopologySuite/NetTopologySuite).

The fork vendors the curve extensibility layer that upstream removed when
`enhancement/curved` was deleted (`Curve`, `Surface<T>`, `ILinearizable<T>`,
curve WKB/WKT hooks) under `src/NetTopologySuite.Curved/Compat/` and uses
composition-based IO (`CurveWKTReader`/`Writer`, `CurveWKBReader`/`Writer`).
See [DOVETAIL.md](docs/DOVETAIL.md) for the full session-by-session refactor
log (sessions 1–10).

## Upstream submodule

The `NetTopologySuite/` git submodule tracks upstream **`develop`**.  The
pinned SHA is recorded in the parent repo; do not run `git submodule update
--remote` by hand unless you intend to take on an API-drift fix.  Instead,
let the weekly CI lane (`.github/workflows/submodule-drift.yml`) bump the
pointer and open a PR when the suite is green against `origin/develop` tip.

After a fresh clone:

```bash
git submodule update --init --recursive
dotnet build -c Release -p:EnableApiCompat=false
dotnet test test/NetTopologySuite.Curved.Test/ -p:EnableApiCompat=false --blame-hang-timeout 60s
```

## CI

| Job | Workflow | Purpose |
|-----|----------|---------|
| `Build (ubuntu / windows / macOS)` | [dotnet.yml](.github/workflows/dotnet.yml) | Release build + full test matrix on the pinned submodule |
| `RocqRef differential tests` | [dotnet.yml](.github/workflows/dotnet.yml) | Oracle-backed robust-predicate fixtures |
| `Build against upstream develop tip` | [submodule-drift.yml](.github/workflows/submodule-drift.yml) | Weekly drift check; auto-opens bump PR when green |

## Standing test skips

Nine `TestSerializeability` overrides in the curved-geometry fixtures
`Assert.Ignore` because upstream `develop` removed `[Serializable]` from
`GeometryFactoryEx`; curve types inherit through `CurveGeometryFactory`.
Re-enable when a non-obsolete serializer is adopted.

RocqRef fixtures (below) `Assert.Ignore` when `ROCQ_REF_BIN` is unset so the
regular build matrix stays green without the Rocq toolchain.

## RocqRef-backed predicates

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

The dedicated `rocqref` CI job in
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

Contributions are welcome — especially additional unit tests, IO coverage, and
code documentation.