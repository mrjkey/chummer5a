#!/usr/bin/env bash
# Build Chummer on the Windows host from WSL. Extra args are passed to MSBuild. CONFIG=Release for a release build.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
. "$HERE/win-env.sh"
win_sync
win_cmd "\"$MSBUILD\" Chummer\\Chummer.csproj -restore \"-p:Configuration=$CONFIG\" -p:SolutionDir=$WIN_PATH\\ -nologo -v:minimal -m $*"
