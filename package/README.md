# Chummer5a with Claude Code

This is Chummer5a, the Shadowrun 5th Edition character builder, with an MCP server built in. While Chummer is running, [Claude Code](https://code.claude.com/docs) can read the characters you have open and edit them live. Chummer still enforces the rules, so karma and point totals always match what the Chummer window shows.

## What's in this folder

| File or folder | What it is |
| --- | --- |
| `Chummer5a.exe` | Starts Chummer. Use this one. |
| `README.md` | This file. |
| `CLAUDE-SETUP.md` | Full guide to installing Claude Code and connecting it to Chummer, including WSL and troubleshooting. |
| `Setup-ClaudeCode.cmd` | One-click setup: connects Claude Code to Chummer from any folder and installs the character-building skill. |
| `CLAUDE.md`, `.mcp.json`, `.claude\` | Let Claude Code work with Chummer when you start it in this folder. |
| `Chummer\` | The Chummer program itself, its data, and your settings and saves. You don't need to open it. |

## Quick start (Windows)

1. **Extract.** If you haven't yet, right-click the downloaded zip, choose **Properties**, tick **Unblock**, and click **OK**. Then extract it to a folder you own, such as `C:\Games\Chummer5a-MCP`. Don't use `Program Files`, because Chummer saves your settings inside its folder.
2. **Start Chummer.** Double-click `Chummer5a.exe`, then create or open a character. The build isn't code-signed, so if Windows shows "Windows protected your PC", click **More info**, then **Run anyway**.
3. **Install Claude Code.** Open PowerShell and run the line below. Then close it, open a new PowerShell window, and run `claude` once to sign in.

   ```powershell
   irm https://claude.ai/install.ps1 | iex
   ```

4. **Connect.** Double-click `Setup-ClaudeCode.cmd`.
5. **Build.** Open a terminal and run `claude`. Ask something like "Help me build a street samurai", or type `/sr5-build-advisor` for a guided build.

You can also skip step 4. Open a terminal in this folder (in File Explorer, type `cmd` in the address bar and press Enter), run `claude`, and approve the `chummer` server when asked. If anything doesn't work, ask Claude in this folder to help you set it up, or see `CLAUDE-SETUP.md`.

Using Claude Code inside WSL? See the WSL section of `CLAUDE-SETUP.md`.

## Upgrading

Your settings and saved characters live in `Chummer\settings` and `Chummer\saves`. To upgrade:

1. Extract the new release to a new folder.
2. Copy those two folders into the new `Chummer\` folder.

If you saved characters somewhere else, there's nothing to copy for them.

Don't use Chummer's built-in updater. It installs the official Chummer build, which doesn't have the MCP server. Get new versions from the [Releases page](https://github.com/mrjkey/chummer5a/releases).

## License and source

Chummer5a is free software under the GNU GPL v3. See `Chummer\LICENSE`. The source code for this build is at <https://github.com/mrjkey/chummer5a>, and the original project is at <https://github.com/chummer5a/chummer5a>.
