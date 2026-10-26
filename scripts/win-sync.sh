#!/usr/bin/env bash
# Mirror the repo onto the Windows filesystem so MSBuild's cmd-based build events work (they fail on \\wsl$ UNC paths).
set -euo pipefail
. "$(cd "$(dirname "$0")" && pwd)/win-env.sh"
REPO="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$WIN_DIR"
rsync -a --delete \
  --exclude '.git/' --exclude 'bin/' --exclude 'obj/' --exclude '.vs/' \
  --exclude 'Chummer.Tests/TestFiles/TestRun-*/' \
  "$REPO/" "$WIN_DIR/"
