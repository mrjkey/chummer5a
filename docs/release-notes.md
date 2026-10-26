Chummer5a with a built-in MCP server, so [Claude Code](https://code.claude.com/docs) can read and edit your open characters while Chummer enforces the rules.

## What's new

- (fill in)

## Install

1. Download `Chummer5a-MCP-<version>.zip` below. Right-click it, choose **Properties**, tick **Unblock**, then extract it to a folder you own (not `Program Files`).
2. Run `Chummer5a.exe` and open a character.
3. Install Claude Code. In PowerShell: `irm https://claude.ai/install.ps1 | iex`.
4. Double-click `Setup-ClaudeCode.cmd` in the extracted folder.
5. Run `claude` in any terminal and ask it to help with your character. `/mcp` should show `chummer` as connected.

Start with `README.md` in the zip. The full Claude Code guide, including WSL setup and troubleshooting, is `CLAUDE-SETUP.md`, also [on GitHub](https://github.com/mrjkey/chummer5a/blob/main/package/CLAUDE-SETUP.md). You can also open a terminal in the extracted folder, run `claude`, and ask it to help you get set up.

Upgrading from an earlier release: copy `Chummer\settings` and `Chummer\saves` into the new folder.

Built from upstream Chummer5a <upstream version>. Don't use Chummer's built-in updater: it installs the official build, which doesn't have the MCP server.
