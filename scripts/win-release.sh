#!/usr/bin/env bash
# Build a clean Release of Chummer and its launcher on the Windows host and package them as
# dist/Chummer5a-MCP-<version>.zip. Usage: scripts/win-release.sh [version]   (default: AssemblyVersion from Chummer.csproj)
# Zip layout: docs, Claude Code setup, the Chummer5a.exe launcher and Examples\ (from characters/) at the top, the app itself in Chummer\.
# See docs/releasing.md for publishing the zip as a GitHub release.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
export CONFIG=Release
. "$HERE/win-env.sh"

VERSION="${1:-$(grep -oPm1 '(?<=<AssemblyVersion>)[^<]+' "$REPO/Chummer/Chummer.csproj")}"
VERSION="${VERSION#v}"
NAME="Chummer5a-MCP-$VERSION"
OUT="$WIN_DIR/Chummer/bin/Release"
LAUNCHER="$WIN_DIR/ChummerLauncher/bin/Release/Chummer5a.exe"
DIST="$REPO/dist"
STAGE="$DIST/$NAME"

# Start from an empty output folder so settings, logs or characters from local test runs never end up in the zip
rm -rf "$OUT"
# The version label shows in Chummer's title bar and the MCP server info; saves still record the plain AssemblyVersion
"$HERE/win-build.sh" -t:Rebuild "-p:InformationalVersion=$VERSION"
[ -f "$OUT/Chummer5.exe" ] || { echo "Build output not found at $OUT" >&2; exit 1; }
win_cmd "\"$MSBUILD\" ChummerLauncher\\ChummerLauncher.csproj -restore -t:Rebuild -p:Configuration=Release -nologo -v:minimal"
[ -f "$LAUNCHER" ] || { echo "Launcher not found at $LAUNCHER" >&2; exit 1; }

rm -rf "$STAGE" "$DIST/$NAME.zip"
mkdir -p "$STAGE/Chummer" "$STAGE/.claude/skills" "$STAGE/Examples"
cp -a "$OUT/." "$STAGE/Chummer/"
cp "$LAUNCHER" "$STAGE/"
cp "$REPO/package/README.md" "$REPO/package/CLAUDE-SETUP.md" "$STAGE/"
# CLAUDE.md, .mcp.json and the skill let "claude" started in the top folder work without running the setup script
cp "$REPO/package/CLAUDE.release.md" "$STAGE/CLAUDE.md"
cp "$REPO/.mcp.json" "$STAGE/"
cp -a "$REPO/.claude/skills/sr5-build-advisor" "$STAGE/.claude/skills/"
sed 's/\r$//; s/$/\r/' "$REPO/package/Setup-ClaudeCode.cmd" > "$STAGE/Setup-ClaudeCode.cmd"
cp "$REPO"/characters/*.{chum5,pdf,html,md} "$STAGE/Examples/"

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
