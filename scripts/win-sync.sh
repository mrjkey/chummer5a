#!/usr/bin/env bash
# Mirror the repo onto the Windows filesystem so MSBuild's cmd-based build events work (they fail on \\wsl$ UNC paths).
# Does nothing when the repo already lives on a Windows drive.
set -euo pipefail
. "$(cd "$(dirname "$0")" && pwd)/win-env.sh"
win_sync
