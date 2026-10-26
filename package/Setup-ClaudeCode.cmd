@echo off
rem Connects Claude Code (Windows) to Chummer: registers the "chummer" MCP server for your user
rem and installs the sr5-build-advisor skill. Usage: Setup-ClaudeCode.cmd [port]   (default 51723)
setlocal
set "PORT=51723"
if not "%~1"=="" set "PORT=%~1"

where claude >nul 2>nul
if errorlevel 1 (
  echo Claude Code was not found on your PATH.
  echo Install it first ^(see README.md^), open a new terminal, and run this script again.
  goto :end
)

echo Registering the "chummer" MCP server at http://localhost:%PORT%/mcp ...
call claude mcp remove chummer --scope user >nul 2>nul
call claude mcp add --scope user --transport http chummer http://localhost:%PORT%/mcp
if errorlevel 1 (
  echo Failed to register the MCP server.
  goto :end
)

echo Installing the sr5-build-advisor skill to "%USERPROFILE%\.claude\skills" ...
xcopy /e /i /y /q "%~dp0.claude\skills\sr5-build-advisor" "%USERPROFILE%\.claude\skills\sr5-build-advisor" >nul
if errorlevel 1 (
  echo Failed to copy the skill.
  goto :end
)

echo.
echo Done. Start Chummer5a.exe, open a character, then run "claude" in any terminal.
echo Inside Claude Code, "/mcp" should list "chummer" as connected.

:end
endlocal
pause
