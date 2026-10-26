# Shared settings for the win-*.sh scripts (sourced, not executed).
WIN_DIR="${CHUMMER_WIN_DIR:-/mnt/c/dev/chummer5a}"
WIN_PATH="$(wslpath -w "$WIN_DIR")"
CONFIG="${CONFIG:-Debug}"
# Visual Studio's MSBuild is required: dotnet build cannot compile the binary .resx resources (MSB3823).
MSBUILD="${MSBUILD:-C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\MSBuild\\Current\\Bin\\MSBuild.exe}"

# This VS install lacks the .NET SDK resolver, so point MSBuild straight at the installed SDK's Sdks folder.
SDKS_PATH="${MSBUILD_SDKS_PATH:-C:\\Program Files\\dotnet\\sdk\\8.0.427\\Sdks}"

# Run a batch script on the Windows host from inside $WIN_DIR. Writing a .cmd file sidesteps WSL->cmd.exe quoting problems.
win_cmd() {
  local bat="$WIN_DIR/.wsl-run.cmd"
  printf '@echo off\r\nset "MSBuildSDKsPath=%s"\r\nset "MSBuildEnableWorkloadResolver=false"\r\ncd /d "%s"\r\n%s\r\n' "$SDKS_PATH" "$WIN_PATH" "$1" > "$bat"
  (cd /mnt/c && cmd.exe /c "$(wslpath -w "$bat")")
}
