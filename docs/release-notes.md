Chummer5a with a built-in MCP server, so [Claude Code](https://code.claude.com/docs) can read and edit your open characters while Chummer enforces the rules.

## What's new

- (fill in)

## Install

1. Download `Chummer5a-MCP-<version>.zip` below. Right-click it, choose **Properties**, tick **Unblock**, then extract it to a folder you own (not `Program Files`).
2. Run `Chummer5.exe` and open a character.
3. Install Claude Code. In PowerShell: `irm https://claude.ai/install.ps1 | iex`.
4. Double-click `Setup-ClaudeCode.cmd` in the Chummer folder.
5. Run `claude` in any terminal and ask it to help with your character. `/mcp` should show `chummer` as connected.

The full guide, including WSL setup and troubleshooting, is `AI-AGENT-SETUP.md` in the zip, or [on GitHub](https://github.com/mrjkey/chummer5a/blob/main/docs/ai-agent-setup.md).

Built from upstream Chummer5a <upstream version>. Don't use Chummer's built-in updater: it installs the official build, which doesn't have the MCP server.
