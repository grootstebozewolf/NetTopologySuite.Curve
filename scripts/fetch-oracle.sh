#!/usr/bin/env bash
#
# fetch-oracle.sh — download the prebuilt RocqRefRunner ("oracle") binary
# from the NetTopologySuite.Proofs CI and make it ready for the RocqRef
# differential test suite.
#
# By default it grabs the *latest successful* build of the `oracle-bin-linux`
# artifact, so the oracle stays in lock-step with the proofs as they evolve.
# The artifact is the OCaml binary extracted from the Coq corpus; the C#
# differential harness talks to it over stdin (SIMPLIFY / ORIENT /
# ORIENT_FILTERED / ... modes) and compares results bit-for-bit.
#
# Usage:
#   scripts/fetch-oracle.sh [--dest DIR]
#
#   # Wire it straight into the test run:
#   export ROCQ_REF_BIN="$(scripts/fetch-oracle.sh)"
#   dotnet test test/NetTopologySuite.Curved.Test/
#
# The resolved binary path is printed to stdout (and nothing else goes to
# stdout), so it is safe to capture in a command substitution. All progress
# and diagnostics go to stderr.
#
# Auth: GitHub Actions artifacts are not anonymously downloadable, even for
# public repositories. Provide a token with `actions:read` on the Proofs repo
# via GH_TOKEN or GITHUB_TOKEN. A fine-grained PAT scoped to
# NetTopologySuite.Proofs (Actions: read-only, Contents: read-only) is enough.
#
# Environment overrides:
#   PROOFS_REPO    owner/name of the proofs repo   (default: grootstebozewolf/NetTopologySuite.Proofs)
#   ARTIFACT_NAME  artifact to download             (default: oracle-bin-linux)
#   ARTIFACT_RUN   pin to a specific run id         (default: unset -> latest successful)
#   GH_TOKEN /     GitHub token with actions:read on PROOFS_REPO
#   GITHUB_TOKEN
#   GITHUB_API     API base url                     (default: https://api.github.com)

set -euo pipefail

PROOFS_REPO="${PROOFS_REPO:-grootstebozewolf/NetTopologySuite.Proofs}"
ARTIFACT_NAME="${ARTIFACT_NAME:-oracle-bin-linux}"
ARTIFACT_RUN="${ARTIFACT_RUN:-}"
GITHUB_API="${GITHUB_API:-https://api.github.com}"
TOKEN="${GH_TOKEN:-${GITHUB_TOKEN:-}}"

DEST=""
while [ $# -gt 0 ]; do
  case "$1" in
    --dest) DEST="${2:?--dest needs a directory}"; shift 2 ;;
    --dest=*) DEST="${1#--dest=}"; shift ;;
    -h|--help) sed -n '2,40p' "$0" >&2; exit 0 ;;
    *) echo "fetch-oracle: unknown argument: $1" >&2; exit 2 ;;
  esac
done
DEST="${DEST:-${PWD}/.oracle}"

log() { echo "fetch-oracle: $*" >&2; }
die() { echo "fetch-oracle: error: $*" >&2; exit 1; }

for tool in curl jq unzip; do
  command -v "$tool" >/dev/null 2>&1 || die "'$tool' is required but not installed"
done
[ -n "$TOKEN" ] || die "no GitHub token found. Set GH_TOKEN or GITHUB_TOKEN to a PAT with actions:read on ${PROOFS_REPO}."

api() {
  # api <path> -> JSON on stdout, fails on non-2xx
  local path="$1" code body tmp
  tmp="$(mktemp)"
  code="$(curl -sS -w '%{http_code}' -o "$tmp" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "${GITHUB_API}/repos/${PROOFS_REPO}${path}")"
  body="$(cat "$tmp")"; rm -f "$tmp"
  case "$code" in
    2*) printf '%s' "$body" ;;
    401|403) die "GitHub API ${code} for ${path} — token rejected or lacks actions:read on ${PROOFS_REPO}." ;;
    404) die "GitHub API 404 for ${path} — repo, run, or artifact not found (is the token allowed to see ${PROOFS_REPO}?)." ;;
    *) die "GitHub API ${code} for ${path}: $(printf '%s' "$body" | jq -r '.message? // empty' 2>/dev/null)" ;;
  esac
}

# 1. Resolve the artifact id (newest non-expired match for ARTIFACT_NAME).
if [ -n "$ARTIFACT_RUN" ]; then
  log "looking up '${ARTIFACT_NAME}' in run ${ARTIFACT_RUN} of ${PROOFS_REPO}"
  list="$(api "/actions/runs/${ARTIFACT_RUN}/artifacts?per_page=100")"
else
  log "looking up latest '${ARTIFACT_NAME}' artifact in ${PROOFS_REPO}"
  # The list endpoint accepts a server-side name filter and returns newest first.
  list="$(api "/actions/artifacts?name=$(jq -rn --arg n "$ARTIFACT_NAME" '$n|@uri')&per_page=100")"
fi

artifact="$(printf '%s' "$list" | jq \
  --arg name "$ARTIFACT_NAME" '
    [.artifacts[] | select(.name == $name and (.expired | not))]
    | sort_by(.created_at) | reverse | .[0] // empty')"

[ -n "$artifact" ] && [ "$artifact" != "null" ] \
  || die "no non-expired '${ARTIFACT_NAME}' artifact found in ${PROOFS_REPO}${ARTIFACT_RUN:+ run ${ARTIFACT_RUN}} (artifacts expire after 90 days — re-run the Proofs CI)."

art_id="$(printf '%s' "$artifact" | jq -r '.id')"
art_run="$(printf '%s' "$artifact" | jq -r '.workflow_run.id // "?"')"
art_created="$(printf '%s' "$artifact" | jq -r '.created_at')"
log "selected artifact id=${art_id} (run ${art_run}, created ${art_created})"

# 2. Download the zip. The /zip endpoint 302-redirects to a signed blob URL
#    that must be fetched *without* the Authorization header, so resolve the
#    redirect first, then download the location bare.
workdir="$(mktemp -d)"
trap 'rm -rf "$workdir"' EXIT
zip="${workdir}/artifact.zip"

signed="$(curl -sS -o /dev/null -w '%{redirect_url}' \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Accept: application/vnd.github+json" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  "${GITHUB_API}/repos/${PROOFS_REPO}/actions/artifacts/${art_id}/zip")"
[ -n "$signed" ] || die "did not receive a download redirect for artifact ${art_id}."

curl -sSL -o "$zip" "$signed" || die "download failed for artifact ${art_id}."
unzip -q -o "$zip" -d "$workdir/unpacked" || die "could not unzip artifact ${art_id}."

# 3. Locate the binary inside the archive (prefer well-known names, else the
#    first regular file), then install it into DEST.
bin_src=""
for cand in oracle_bin oracle driver RocqRefRunner; do
  found="$(find "$workdir/unpacked" -type f -name "$cand" | head -n1)"
  [ -n "$found" ] && { bin_src="$found"; break; }
done
[ -n "$bin_src" ] || bin_src="$(find "$workdir/unpacked" -type f | head -n1)"
[ -n "$bin_src" ] || die "artifact ${art_id} contained no files."

mkdir -p "$DEST"
bin_dst="${DEST}/oracle_bin"
install -m 0755 "$bin_src" "$bin_dst"

log "oracle ready at ${bin_dst}"
printf '%s\n' "$bin_dst"
