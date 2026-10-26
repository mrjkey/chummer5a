# Using Chummer with Claude Code

This build of Chummer5a has an MCP server built in. While Chummer is running, [Claude Code](https://code.claude.com/docs) can connect to it, read the characters you have open, and edit them live. Chummer still enforces the rules, and karma and point totals match what the Chummer window shows.

It also includes the `sr5-build-advisor` skill, which walks you through building a Shadowrun 5e character.

You need:

- Windows 10 or 11. Chummer is a Windows app.
- A Claude account that can use Claude Code: a Pro, Max, Team or Enterprise plan, or an Anthropic Console account.

## 1. Install Chummer

1. Download `Chummer5a-MCP-<version>.zip` from the [Releases page](https://github.com/mrjkey/chummer5a/releases).
2. Right-click the zip, choose **Properties**, tick **Unblock** if it is there, and click **OK**. If you skip this, Windows may block some of Chummer's files.
3. Right-click the zip and choose **Extract All...**. Pick a folder you own, such as `C:\Games\Chummer5a-MCP`. Don't use `Program Files`, because Chummer saves its settings next to `Chummer5.exe`.
4. Run `Chummer5.exe`. The build isn't code-signed, so if Windows shows "Windows protected your PC", click **More info**, then **Run anyway**.

> Don't use Chummer's built-in updater with this build. It downloads the official Chummer release, which doesn't have the MCP server. Get updates from the Releases page above instead.

## 2. Install Claude Code on Windows

Open **PowerShell** and run:

```powershell
irm https://claude.ai/install.ps1 | iex
```

Close PowerShell, open a new one, and run `claude` once to sign in. If something goes wrong, see the [official setup guide](https://code.claude.com/docs/en/setup). It also lists other ways to install, such as WinGet and npm, and what else is required (for example Git for Windows).

## 3. Connect Claude Code to Chummer

Pick **one** of these options.

### Option A: run the setup script (recommended)

Double-click `Setup-ClaudeCode.cmd` in the Chummer folder. It does two things:

- It registers the `chummer` MCP server for your Windows user, so it works from any folder.
- It copies the `sr5-build-advisor` skill to `%USERPROFILE%\.claude\skills`.

### Option B: set it up by hand

In PowerShell or Command Prompt:

```powershell
claude mcp add --scope user --transport http chummer http://localhost:51723/mcp
```

Then copy the folder `.claude\skills\sr5-build-advisor` from the Chummer folder into `%USERPROFILE%\.claude\skills\`. Create that folder if it doesn't exist.

### Option C: no setup, but only from the Chummer folder

The Chummer folder already has a `.mcp.json` and the skill in `.claude\skills`. Open a terminal in the Chummer folder (in File Explorer, type `cmd` in the address bar and press Enter) and run `claude`. When Claude Code asks whether to use the `chummer` server from `.mcp.json`, approve it. This only works when you start `claude` from that folder.

## 4. Use it

1. Start `Chummer5.exe` and create or open a character. The tools only work on characters that are open in an editor tab.
2. Open a terminal and run `claude`.
3. Type `/mcp`. `chummer` should show as **connected**.
4. Ask for what you want, for example:
   - "Help me build a street samurai for a priority game."
   - "What's my character's Pistols dice pool?"
   - "Is this character legal? What's still unspent?"

   You can also start the guided build directly with `/sr5-build-advisor`.

If you started Claude Code before Chummer, run `/mcp` and reconnect `chummer`, or restart `claude`.

## Using Claude Code inside WSL

Chummer still runs on Windows. Only Claude Code runs inside WSL (Linux).

1. **Turn on mirrored networking.** Chummer only accepts connections from `localhost` on Windows. WSL's default networking mode can't reach Windows' `localhost`, so you need mirrored mode, which requires Windows 11 22H2 or newer. Create or edit `%USERPROFILE%\.wslconfig` on Windows:

   ```ini
   [wsl2]
   networkingMode=mirrored
   ```

   Then run `wsl --shutdown` in PowerShell and open WSL again.

2. **Install Claude Code in WSL**, if you haven't already:

   ```bash
   curl -fsSL https://claude.ai/install.sh | bash
   ```

3. **Register the server and install the skill.** Adjust the path to wherever you extracted Chummer:

   ```bash
   claude mcp add --scope user --transport http chummer http://localhost:51723/mcp
   mkdir -p ~/.claude/skills
   cp -r /mnt/c/Games/Chummer5a-MCP/.claude/skills/sr5-build-advisor ~/.claude/skills/
   ```

4. **Check the connection.** Start Chummer on Windows, then run this in WSL:

   ```bash
   curl -s -X POST http://localhost:51723/mcp -H 'Content-Type: application/json' -d '{"jsonrpc":"2.0","id":1,"method":"ping"}'
   ```

   You should get back a JSON reply containing `"result"`. If the connection is refused, mirrored networking isn't active. If you can't use mirrored mode, run Claude Code on Windows instead (sections 2 and 3).

## Troubleshooting

**`/mcp` shows `chummer` as failed.**

Chummer isn't running, or it was started after Claude Code. Start Chummer, then reconnect from `/mcp`. To check the server directly from PowerShell:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:51723/mcp -ContentType application/json -Body '{"jsonrpc":"2.0","id":1,"method":"ping"}'
```

**Port 51723 is already in use.**

Start Chummer on a different port: make a shortcut to `Chummer5.exe` and add `/mcpport:51724` to the end of its **Target**. Then point Claude Code at the new port by running `Setup-ClaudeCode.cmd 51724` from a terminal, or by re-running the `claude mcp add` command with the new port. Run `claude mcp remove chummer --scope user` first.

**Claude says no character is open.**

Open or create a character in Chummer. Closing a character's tab makes it unavailable to Claude Code.

**A dialog popped up in Chummer.**

Some items ask a question when they're added. Answer the dialog in the Chummer window and Claude Code will carry on.

**Security.**

The MCP server only listens on `localhost` and rejects requests from web pages. It has no password, though, so any program running on your PC can read and edit your open characters while Chummer is running. Save your characters regularly.
