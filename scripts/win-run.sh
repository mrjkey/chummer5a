#!/usr/bin/env bash
# Launch the built Chummer5.exe on the Windows host (detached). Extra args go to Chummer (e.g. /mcpport:51724 or a .chum5 path).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
. "$HERE/win-env.sh"
EXE="$(find "$WIN_DIR/Chummer/bin" -maxdepth 3 -name Chummer5.exe -path "*$CONFIG*" | head -n1)"
[ -n "$EXE" ] || { echo "Chummer5.exe not found under $WIN_DIR/Chummer/bin; run scripts/win-build.sh first" >&2; exit 1; }
cd "$(dirname "$EXE")"
cmd.exe /c start "" "$(wslpath -w "$EXE")" "$@"
