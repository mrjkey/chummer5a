# Chummer5a (fork with an AI-agent MCP server)

Chummer is a .NET Framework 4.8 WinForms app (Windows only). This repo lives in WSL; it's built and run on the Windows host through the scripts in `scripts/`.

## Build / run / test from WSL

- `scripts/win-build.sh`: rsyncs the repo to `C:\dev\chummer5a` (cmd-based build events break on `\\wsl$` paths) and builds `Chummer.csproj` with Visual Studio's MSBuild. `dotnet build` does **not** work (binary .resx resources, MSB3823).
- `scripts/win-test.sh [dotnet test args]`: builds and runs `Chummer.Tests` (MSTest), e.g. `scripts/win-test.sh --filter "FullyQualifiedName~AgentToolsTests"`.
- `scripts/win-run.sh [chummer args]`: launches the built `Chummer5.exe`. Close any running Chummer before rebuilding.
- `scripts/win-env.sh` holds the paths. Override them with `CHUMMER_WIN_DIR`, `CONFIG`, `MSBUILD` or `MSBUILD_SDKS_PATH`. The VS install has no .NET SDK resolver, so the scripts point MSBuild at the installed .NET SDK and disable the workload resolver.

## AI agent integration

- `Chummer/Backend/Agent/` holds the MCP server:
  - `McpServer.cs`: Streamable HTTP, JSON responses, localhost only. Port 51723; override with `/mcpport:N`.
  - `AgentTools*.cs`: the tools.
- `Program.cs` starts the server around `Application.Run(MainForm)`.
- Tools work on the characters open in the editor (`Program.MainForm.OpenCharacterEditorForms`).
- Karma totals and validation come from the open `CharacterCreate` form:
  - `CalculateKarmaRemainingAsync`
  - `CollectValidityIssuesAsync`
  - `AddQualityFromNodeAsync`
  - `RemoveQuality`

  They are refactored out of the button/label code, so the numbers always match the UI.
- `.mcp.json` registers the server with Claude Code. `.claude/skills/sr5-build-advisor` is the conversational build workflow.
- Chummer must be running (and built with the agent code) for the `chummer` MCP tools to connect. WSL uses mirrored networking, so `localhost` reaches Windows.
- When adding a tool, put the rules logic in the same code path the GUI uses rather than re-implementing it, and add coverage to `Chummer.Tests/AgentToolsTests.cs`. That test opens a real `CharacterCreate` form on `TestFiles/Skink.chum5`.

## Code style

Match the surrounding code: Hungarian-ish prefixes (`str`, `int`, `bln`, `obj`, `lst`, `dic`), async methods with `CancellationToken token` and `.ConfigureAwait(false)` everywhere, and the GPL header on new files.
