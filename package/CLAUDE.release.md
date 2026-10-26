# Chummer5a release folder

This folder is a packaged release of Chummer5a, the Shadowrun 5th Edition character builder, with an MCP server built in. It isn't a source repository, so don't edit files under `Chummer\`. The user is most likely here to build or check a character, or to get the Chummer connection working.

## Layout

- `Chummer5a.exe` is the launcher the user starts. It runs `Chummer\Chummer5.exe`.
- `Chummer\` holds the app, its data, and the user's settings (`Chummer\settings`) and saves (`Chummer\saves`).
- `Examples\` has two finished starting characters (`Darrow`, a face, and `Sif`, an adept gunslinger) with PDF sheets and build notes in `.md`. They're useful as examples for new players; `open_character` loads them.
- `.mcp.json` registers the `chummer` MCP server, `http://localhost:51723/mcp`, for Claude Code sessions started in this folder.
- `.claude\skills\sr5-build-advisor` is the guided character-building skill.
- `README.md` is the user's quick start, and `CLAUDE-SETUP.md` is the full setup and troubleshooting guide. Read `CLAUDE-SETUP.md` before answering setup questions.

## Working with Chummer

- The `chummer` tools only exist while Chummer is running. They can:
  - create a new character (`create_character`, which opens it in Chummer) or open a saved one (`open_character`)
  - edit characters that are open in an editor tab during creation: priorities, attributes, skills, qualities, magic, contacts, gear, cyberware, vehicles, lifestyles. Career-mode characters are read-only.
  - check them (`validate_character`), save them (`save_character`; new characters go to `Chummer\saves`) and export a PDF or HTML sheet (`export_character_sheet`)
- Build with the core rulebook (the `Standard` settings) unless the user asks for other books.
- If the tools are missing or fail to connect:
  1. Ask the user to start `Chummer5a.exe`, or offer to start it yourself:
     - from a Windows shell: `start "" Chummer5a.exe`
     - from WSL: `cmd.exe /c start "" Chummer5a.exe`
  2. Once it's running, have the user reconnect with `/mcp`.
- If `chummer` isn't listed in `/mcp` at all, the user probably declined the project server prompt. They can run `claude mcp reset-project-choices` here and restart `claude`.
- If you're running inside WSL (`uname -r` mentions `microsoft`) and the server can't be reached while Chummer is running, WSL needs mirrored networking. See the WSL section of `CLAUDE-SETUP.md`.
- For character building or advice, use the `sr5-build-advisor` skill. Chummer is the source of truth for rules and numbers.

## Using it from any folder

The setup in this folder only applies when `claude` is started here. If the user wants Chummer available from any folder, suggest one of these:

- **Setup script:** double-click `Setup-ClaudeCode.cmd`. It registers the server at user scope and copies the skill to `%USERPROFILE%\.claude\skills`.
- **By hand:**
  1. Run `claude mcp add --scope user --transport http chummer http://localhost:51723/mcp`.
  2. Copy `.claude\skills\sr5-build-advisor` to the user's skills folder.
