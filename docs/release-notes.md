Chummer5a with a built-in MCP server, so [Claude Code](https://code.claude.com/docs) can build and edit your characters while Chummer enforces the rules.

## What's new

First release.

- **Claude Code can build characters with you.** Start Chummer, start `claude`, and ask for a character. It can:
  - create characters (priority, sum-to-ten or karma builds) and open saved ones
  - set priorities, metatype and magic or resonance
  - set attributes, skills, specializations, knowledge skills and languages
  - add qualities, contacts, spells, adept powers, complex forms, tradition and mentor spirit, spirits and sprites, initiation and metamagic, and martial arts
  - buy weapons and accessories, armor and mods, gear and ammo, cyberware and bioware, vehicles and mods, and lifestyles
  - check the character for mistakes and unspent points, save it, and export a PDF character sheet
- **The `sr5-build-advisor` skill** walks you through a build step by step and explains the trade-offs. Start it with `/sr5-build-advisor`.
- **Core rulebook by default.** New characters use the Shadowrun 5 core rules unless you ask for other books.
- **Example characters** in `Examples\`: Darrow (a face) and Sif (an adept gunslinger), with PDF sheets and build notes.
- **One-click setup:** `Setup-ClaudeCode.cmd` connects Claude Code to Chummer from any folder. Or just run `claude` in the extracted folder.

Known limits:

- Claude Code can only change characters during creation. Career-mode characters (after "Character Created" is ticked) are read-only to it.
- A few items ask a question when added. If the AI doesn't supply the answer, the question shows in the Chummer window: answer it there and Claude Code carries on.

## Install

1. Download `Chummer5a-MCP-<version>.zip` below. Right-click it, choose **Properties**, tick **Unblock**, then extract it to a folder you own (not `Program Files`).
2. Run `Chummer5a.exe`. Windows may warn that the app isn't signed: click **More info**, then **Run anyway**.
3. Install Claude Code. In PowerShell: `irm https://claude.ai/install.ps1 | iex`.
4. Double-click `Setup-ClaudeCode.cmd` in the extracted folder.
5. Run `claude` in any terminal and ask it to help with your character. `/mcp` should show `chummer` as connected.

Start with `README.md` in the zip. The full Claude Code guide, including WSL setup and troubleshooting, is `CLAUDE-SETUP.md`, also [on GitHub](https://github.com/mrjkey/chummer5a/blob/main/package/CLAUDE-SETUP.md). You can also open a terminal in the extracted folder, run `claude`, and ask it to help you get set up.

Upgrading from an earlier release: copy `Chummer\settings` and `Chummer\saves` into the new folder.

Built from upstream Chummer5a 5.226.0. Chummer's own update check is turned off in this version, because it would install the official build, which doesn't have the MCP server. Report problems with this version [here](https://github.com/mrjkey/chummer5a/issues), not to the official Chummer project.
