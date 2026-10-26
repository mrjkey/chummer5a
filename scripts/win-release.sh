#!/usr/bin/env bash
# Build a clean Release of Chummer on the Windows host and package it, with the Claude Code setup files, as
# dist/Chummer5a-MCP-<version>.zip. Usage: scripts/win-release.sh [version]   (default: AssemblyVersion from Chummer.csproj)
# See docs/releasing.md for publishing the zip as a GitHub release.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
export CONFIG=Release
. "$HERE/win-env.sh"

VERSION="${1:-$(grep -oPm1 '(?<=<AssemblyVersion>)[^<]+' "$REPO/Chummer/Chummer.csproj")}"
VERSION="${VERSION#v}"
NAME="Chummer5a-MCP-$VERSION"
OUT="$WIN_DIR/Chummer/bin/Release"
DIST="$REPO/dist"
STAGE="$DIST/$NAME"

# Start from an empty output folder so settings, logs or characters from local test runs never end up in the zip
rm -rf "$OUT"
"$HERE/win-build.sh" -t:Rebuild
[ -f "$OUT/Chummer5.exe" ] || { echo "Build output not found at $OUT" >&2; exit 1; }

rm -rf "$STAGE" "$DIST/$NAME.zip"
mkdir -p "$STAGE/.claude/skills"
cp -a "$OUT/." "$STAGE/"
# Lets "claude" started inside the Chummer folder pick up the server and skill without running the setup script
cp "$REPO/.mcp.json" "$STAGE/"
cp -a "$REPO/.claude/skills/sr5-build-advisor" "$STAGE/.claude/skills/"
cp "$REPO/docs/ai-agent-setup.md" "$STAGE/AI-AGENT-SETUP.md"
sed 's/\r$//; s/$/\r/' "$HERE/claude-setup/Setup-ClaudeCode.cmd" > "$STAGE/Setup-ClaudeCode.cmd"

python3 -I - "$STAGE" "$DIST/$NAME.zip" <<'EOF'
import os, sys, zipfile
src, dst = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED) as z:
    for root, dirs, files in os.walk(src):
        dirs.sort()
        # Keep empty folders (settings, packs, livecustomdata) that the post-build step creates
        for d in dirs:
            z.write(os.path.join(root, d), os.path.relpath(os.path.join(root, d), src))
        for f in sorted(files):
            p = os.path.join(root, f)
            z.write(p, os.path.relpath(p, src))
EOF
rm -rf "$STAGE"
echo "Created $DIST/$NAME.zip ($(du -h "$DIST/$NAME.zip" | cut -f1))"
