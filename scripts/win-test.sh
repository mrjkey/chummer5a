#!/usr/bin/env bash
# Build and run Chummer.Tests on the Windows host. Extra args are passed to dotnet test (e.g. --filter Name~Agent).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
. "$HERE/win-env.sh"
win_sync
win_cmd "\"$MSBUILD\" Chummer.Tests\\Chummer.Tests.csproj -restore \"-p:Configuration=$CONFIG\" -p:SolutionDir=$WIN_PATH\\ -nologo -v:minimal -m || exit /b 1
dotnet test Chummer.Tests\\Chummer.Tests.csproj -c \"$CONFIG\" --no-build -nologo $*"
