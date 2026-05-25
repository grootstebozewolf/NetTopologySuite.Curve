#!/bin/bash
set -euo pipefail

# Only run in Claude Code on the web's remote container.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

cd "$CLAUDE_PROJECT_DIR"

# 1. Install .NET 10 SDK if not already present.
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  sudo apt-get update -qq
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends dotnet-sdk-10.0
fi

# 2. Initialize the NetTopologySuite submodule (shallow clone).
if [ ! -f NetTopologySuite/src/NetTopologySuite/NetTopologySuite.csproj ]; then
  git submodule update --init --depth 1
fi

# 3. Restore NuGet packages. The submodule's NuGet.config only lists the
#    dotnet-eng Azure DevOps feed (which 403s for Microsoft.DotNet.ApiCompat
#    from outside Microsoft's network); add nuget.org and disable the
#    legacy ApiCompat package since the modern SDK ships ApiCompat built-in.
dotnet restore NetTopologySuite.Curve.sln \
  -p:EnableApiCompat=false \
  --source https://api.nuget.org/v3/index.json

# Persist build properties so subsequent `dotnet build/test` calls
# don't need to repeat them.
{
  echo 'export EnableApiCompat=false'
} >> "$CLAUDE_ENV_FILE"
