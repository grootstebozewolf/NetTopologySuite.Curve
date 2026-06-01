# Session handoff — 2026-06-01

A complete record of this Claude Code session so it can be resumed on a fork.
Two distinct tasks were worked: (1) artifact-download integration (**done, pushed**)
and (2) JTS #1106 orientation contribution (**in progress, paused before posting**).

- **Repo:** `grootstebozewolf/NetTopologySuite.Curve`
- **Branch:** `claude/artifact-download-integration-euPMX`
- **Companion repo (public):** `grootstebozewolf/NetTopologySuite.Proofs` (Coq/Rocq proofs + extracted oracle)

---

## Environment notes (carry-over)

- Remote container; `CLAUDE_CODE_REMOTE=true`. SessionStart hook installs .NET 10 + inits the NTS submodule.
- **`GH_TOKEN` in the live container is a placeholder** (fails real GitHub API with 401). Git push works via a local proxy scoped to this repo only.
- **GitHub MCP tools and the git proxy are hard-scoped to `nettopologysuite.curve`.** `locationtech/jts` and `NetTopologySuite.Proofs` are out of scope; `add_repo`/`list_repos` were not available this session.
- **Network policy is restrictive.** Blocked: apt third-party PPAs (deadsnakes/ondrej → 403), `dot.net` installer. Reachable (200): `api.github.com`, Maven Central (`repo1.maven.org`), Ubuntu main archive (`archive.ubuntu.com`, `azure.archive.ubuntu.com`).
- **Java is already installed:** OpenJDK 21 (`/usr/bin/java`, `/usr/bin/javac`). No install needed.
- A user PAT was added to the session app settings under the name `GH_TOKEN` — it will be live on the **next** container start (session settings don't hot-reload mid-session). The PAT pasted earlier in chat should be **rotated**.

---

## Task 1 — Artifact-download integration ✅ DONE & PUSHED

**Goal:** "Integrate Artifact download URL" → the artifact at
`https://github.com/grootstebozewolf/NetTopologySuite.Proofs/actions/runs/26741343927/artifacts/7325770564`
is the prebuilt Linux **RocqRefRunner ("oracle")** binary, artifact name **`oracle-bin-linux`** (~851 KB).

**Decisions made:** CI workflow **+** local script; target the **latest successful** build (not the pinned run).

**Delivered (commit `de96672`, pushed):**

1. **`scripts/fetch-oracle.sh`** — downloads the newest non-expired `oracle-bin-linux` from the Proofs repo via the GitHub REST API, unpacks the binary, prints its path for `ROCQ_REF_BIN`. Env overrides: `PROOFS_REPO`, `ARTIFACT_NAME`, `ARTIFACT_RUN` (pin a run), `GH_TOKEN`/`GITHUB_TOKEN`. Two-step download (resolve redirect, then fetch signed blob URL bare). Validated end-to-end against the live repo with a real token — it correctly selected an even newer build (run `26743235123`) than the URL's run, and produced a working 2.4 MB x86-64 ELF binary.
2. **`.github/workflows/rocqref-differential.yml`** — PR gate on `develop` (+ `workflow_dispatch` with an `artifact_run` input). Downloads the oracle when the `PROOFS_ARTIFACT_TOKEN` secret is set, exports `ROCQ_REF_BIN`, then restore/build/test. The C# harness self-gates on `ROCQ_REF_BIN`, so without the secret it still runs structural unit tests (differential cases Skipped). Uses `actions/setup-dotnet@v4` `10.0.x` (test project targets `net10.0`).
3. **`ReadMe.md`** — RocqRef setup now leads with `fetch-oracle.sh` (download prebuilt) instead of building the corpus from source; build-from-source kept as fallback; "What's next → CI integration" updated.

**To arm CI:** add a **GitHub repo secret** `PROOFS_ARTIFACT_TOKEN` (PAT with `actions:read` on the Proofs repo) in repo Settings → Secrets and variables → Actions. The Claude session `GH_TOKEN` does **not** reach GitHub Actions — these are separate secret stores. Could not be set from this session (needs repo-admin write).

**Not done:** PR not opened (per instructions, only on explicit request). The stale legacy `.github/workflows/dotnet.yml` (pins .NET 5; project is now `net10.0`) was left untouched to keep the change focused.

---

## Task 2 — JTS #1106 orientation contribution ⏸ PAUSED (do not post yet)

**Request:** Help on **JTS #1106** "Summary: Point-Line Orientation robustness issues"
(`locationtech/jts`, open, opened by **dr-jts = Martin Davis, JTS lead**; meta-issue over near-collinear
orientation robustness, references #750 / #1093). Action requested: comment with link to
`verified-claims.md` Phase 0 + oracle test output; "Add Java to the toolchain if needed."

### Verified facts (grounding)

- **`verified-claims.md` exists** in the Proofs repo at **`docs/verified-claims.md`** (not root).
- **Proven, but tightly scoped (Qed):**
  - `b64_orient_sign_filtered_sound_small_int` — filtered Pos/Neg/Zero **provably agree with the true sign, only for integer coordinates with `|coord| ≤ 2²⁵`**.
  - `b64_passes_through_sound` / `b64_passes_through_complete` — hot-pixel pass predicate, `[exact]`.
- **Deferred (NOT proven):** general bounded-magnitude soundness, i.e. Shewchuk **Stages B–D** (which would resolve `Uncertain` for arbitrary doubles). `b64_orient2d_exact_sound` is prose-sketched only. So this is **not a drop-in fix for arbitrary-double inputs.**
- Structural lemmas (decidability/totality/distinctness) for both 4- and 5-valued sign types are Qed-closed.

### Oracle protocol (RocqRefRunner / `oracle_bin`)

- Persistent loop: reads a **mode keyword line**, then that mode's operands, emits a result, repeats. Each case needs its **own** mode keyword.
- `ORIENT` / `ORIENT_FILTERED`: 4 lines — `ORIENT` (or `ORIENT_FILTERED`), then three points `x y` each on its own line.
- Output: `SIGN hex_float`, SIGN ∈ `POS|NEG|ZERO|NAN` (and `UNCERTAIN` for the filtered mode).
- Other modes seen in `driver.ml`: `INTERSECT_FILTERED`, `INTERSECT_POINT_FILTERED`, `INTERSECT_POINT_XY`, `PASSES_THROUGH_FILTER`, `PASSES_THROUGH_HALFOPEN`, `EDGE_IN_RESULT`, `INCIRCLE_SIGN`, `ARC_CHORD_CROSSES_CIRCLE`, `ARC_PASSES_THROUGH_PIXEL`, `SIMPLIFY` (exits).

### Real oracle output already captured

Family `orient2d((0,0),(1,1),(0.5, 0.5+δ))`, true det = δ; forward-error bound `(3+16ε)·ε·(|t₁|+|t₂|) ≈ 6.66e-16`:

| δ requested | qy stored (hex) | naive `ORIENT` | filtered `ORIENT_FILTERED` |
|---|---|---|---|
| 0 … 5e-17 | `0x1.0000000000000p-1` | ZERO | ZERO |
| 1e-16 | `0x1.0000000000001p-1` | POS | **UNCERTAIN** |
| 2e-16 | `0x1.0000000000002p-1` | POS | **UNCERTAIN** |
| 5e-16 | `0x1.0000000000005p-1` | POS | **UNCERTAIN** |
| 1e-15 | `0x1.0000000000009p-1` | POS | POS |
| 1e-14 | `0x1.000000000005ap-1` | POS | POS |

Interpretation: the naive predicate commits to POS across the whole band where the sign is not certifiable in binary64; the filtered predicate abstains (`UNCERTAIN`) exactly there.

The oracle binary was downloaded to `/tmp/oracle/oracle_bin` during the session (ephemeral — re-fetch with `scripts/fetch-oracle.sh` in a new session once `GH_TOKEN` is live).

### Decisions made for Task 2

1. **Hold — do not post yet.** Strengthen evidence first: use Java/JTS to reproduce JTS's *own* `Orientation.index` behavior on these vectors and include a head-to-head contrast before anything goes to the issue.
2. **Frame the comment around the oracle + reproducible test vectors** (a robustness test-aid), mentioning the proofs only briefly — rather than leading with soundness claims.

> ⚠️ Honesty note for the head-to-head: modern JTS `Orientation.index` routes through `CGAlgorithmsDD` (double-double) and is expected to be **correct** on classic near-collinear / Kettner-et-al. examples. So the realistic contrast is likely: *naive double flips → JTS-DD is correct → our `ORIENT` (naive) reproduces the flip faithfully → our `ORIENT_FILTERED` returns `UNCERTAIN`.* That positions the contribution accurately (a faithful differential oracle + a conservative filter), without implying JTS-DD is broken. Verify empirically before drafting final numbers.

### Draft comment (tightened to oracle + vectors; for review, NOT posted)

> **A differential oracle + reproducible near-collinear test vectors for the orientation predicate**
> (from the NetTopologySuite.Curve robust-geometry fork — predicates paired with a Rocq/Coq spec and a
> reference binary extracted from the proofs.)
>
> Offering, in case it helps the robustness test surface here:
>
> **1. Reproducible near-collinear vectors + a reference oracle.** A standalone binary emits deterministic
> signs for `ORIENT` (naive) and `ORIENT_FILTERED` (Shewchuk Stage-A filter → returns `Uncertain` when
> `|det|` is within `(3+16·ε)·ε·(|t₁|+|t₂|)` of zero). Example on `orient2d((0,0),(1,1),(0.5,0.5+δ))`,
> true det = δ:
> | δ | naive | filtered | JTS `Orientation.index` |
> |---|---|---|---|
> | ≤5e-17 | ZERO | ZERO | _(fill in)_ |
> | 1e-16…5e-16 | POS | **UNCERTAIN** | _(fill in)_ |
> | ≥1e-15 | POS | POS | _(fill in)_ |
>
> **2. A conservative filtered alternative** that abstains rather than risk a sign flip in the
> non-certifiable band — usable as a guard / differential check.
>
> **3. (Briefly) formal backing.** `b64_orient_sign_filtered_sound_small_int` is Qed-proven for integer
> coords `|coord| ≤ 2²⁵`; general bounded-magnitude soundness (Stages B–D) is deferred — so this is a
> testing/guard aid, not a drop-in fix for arbitrary doubles. Ledger: `docs/verified-claims.md` → Phase 0.
>
> Happy to contribute the vectors / oracle as test fixtures.

### Blockers for posting (unchanged)

- Cannot reach `locationtech/jts` with current tooling (MCP scope-restricted; `add_repo` unavailable).
- High-profile outward-facing target → exact wording needs explicit owner sign-off before any post.

---

## Resume checklist (next session, on the fork)

1. Confirm `GH_TOKEN` is now live (`scripts/fetch-oracle.sh` should work with no args). Re-fetch the oracle binary.
2. Java is already present (OpenJDK 21). Pull `jts-core` from Maven Central:
   `https://repo1.maven.org/maven2/org/locationtech/jts/jts-core/<ver>/jts-core-<ver>.jar` (it has no runtime deps).
3. Write a tiny Java program that, for each test vector, prints:
   - JTS `org.locationtech.jts.algorithm.Orientation.index(p1, p2, q)` (DD-robust default), and
   - a naive `double` cross-product sign for contrast.
   Compile with `javac -cp jts-core.jar`, run with the jar on the classpath.
4. Run the same vectors through `oracle_bin` (`ORIENT` and `ORIENT_FILTERED`). Include classic
   Kettner-et-al. failing examples in addition to the δ family.
5. Fill the head-to-head table in the draft; keep the oracle-+-vectors framing and the explicit
   integer-coord ≤2²⁵ / Stages-B–D-deferred caveat.
6. **Do not post** until the owner approves the exact wording. Posting must be done by the owner (or
   after `locationtech/jts` is explicitly added to scope), per the decisions above.
7. Decide whether to persist Java in the toolchain — only needed if JTS cross-checking becomes ongoing;
   OpenJDK 21 is already in the base image, so a SessionStart change may be unnecessary.

## Open follow-ups from Task 1

- Set the `PROOFS_ARTIFACT_TOKEN` GitHub repo secret to arm the differential CI gate.
- Rotate the PAT that was pasted in chat.
- Optionally fix the stale `.github/workflows/dotnet.yml` (.NET 5 → 10).
- Open a PR for `claude/artifact-download-integration-euPMX` if/when desired.
