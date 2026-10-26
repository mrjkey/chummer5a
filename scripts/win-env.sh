# Shared settings for the win-*.sh scripts (sourced, not executed).
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Build in place when the repo already lives on a Windows drive (/mnt/c/...). A repo on the WSL filesystem is mirrored
# to C:\dev\chummer5a first, because MSBuild's cmd-based build events fail on \\wsl$ UNC paths.
case "$REPO" in
  /mnt/[a-z]/*) WIN_DIR="${CHUMMER_WIN_DIR:-$REPO}" ;;
  *) WIN_DIR="${CHUMMER_WIN_DIR:-/mnt/c/dev/chummer5a}" ;;
esac
WIN_PATH="$(wslpath -w "$WIN_DIR")"
CONFIG="${CONFIG:-Debug}"

# Visual Studio's MSBuild is required: dotnet build cannot compile the binary .resx resources (MSB3823).
if [ -z "${MSBUILD:-}" ]; then
  VSWHERE="/mnt/c/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe"
  VS_PATH=""
  [ -x "$VSWHERE" ] && VS_PATH="$("$VSWHERE" -latest -products '*' -property installationPath 2>/dev/null | head -n1 | tr -d '\r')"
  MSBUILD="${VS_PATH:-C:\\Program Files\\Microsoft Visual Studio\\2022\\Community}\\MSBuild\\Current\\Bin\\MSBuild.exe"
fi

# VS installs without the .NET SDK resolver need MSBuild pointed straight at an installed SDK's Sdks folder.
# Default to the newest 8.0 SDK, which is what global.json asks for.
if [ -z "${MSBUILD_SDKS_PATH:-}" ]; then
  SDK_VER="$(ls "/mnt/c/Program Files/dotnet/sdk" 2>/dev/null | grep -E '^8\.0\.' | sort -V | tail -n1)"
  MSBUILD_SDKS_PATH="C:\\Program Files\\dotnet\\sdk\\${SDK_VER:-8.0.427}\\Sdks"
fi
SDKS_PATH="$MSBUILD_SDKS_PATH"

# Mirror a WSL-filesystem repo onto $WIN_DIR. No-op when building in place.
win_sync() {
  [ "$REPO" = "$WIN_DIR" ] && return 0
  mkdir -p "$WIN_DIR"
  rsync -a --delete \
    --exclude '.git/' --exclude 'bin/' --exclude 'obj/' --exclude '.vs/' --exclude 'dist/' \
    --exclude 'Chummer.Tests/TestFiles/TestRun-*/' \
    "$REPO/" "$WIN_DIR/"
}

# Run a batch script on the Windows host from inside $WIN_DIR. Writing a .cmd file sidesteps WSL->cmd.exe quoting problems.
win_cmd() {
  local bat="$WIN_DIR/.wsl-run.cmd"
  printf '@echo off\r\nset "MSBuildSDKsPath=%s"\r\nset "MSBuildEnableWorkloadResolver=false"\r\ncd /d "%s"\r\n%s\r\n' "$SDKS_PATH" "$WIN_PATH" "$1" > "$bat"
  (cd /mnt/c && cmd.exe /c "$(wslpath -w "$bat")")
}
