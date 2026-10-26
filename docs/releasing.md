# Releasing the MCP build

A release is one zip on a GitHub release of `mrjkey/chummer5a`. Everything a user reads or runs is at the top of the zip, and the app itself is in a subfolder:

```
Chummer5a.exe          launcher (ChummerLauncher project) that starts Chummer\Chummer5.exe
README.md              quick start                       <- package/README.md
CLAUDE-SETUP.md        Claude Code setup, WSL, troubleshooting  <- package/CLAUDE-SETUP.md
Setup-ClaudeCode.cmd   registers the MCP server (user scope) and installs the skill  <- package/
CLAUDE.md              tells Claude what the folder is   <- package/CLAUDE.release.md
.mcp.json              <- repo .mcp.json
.claude/skills/sr5-build-advisor/  <- repo skill
Examples/              example characters (.chum5, .pdf, .html, .md)  <- characters/
Chummer/               the Release build of Chummer (Chummer/bin/Release)
```

Chummer finds its data, settings and saves next to its own exe, so `Chummer5.exe` can't move to the top. The launcher stands in for it. The release `CLAUDE.md` is named `CLAUDE.release.md` in the repo, so that Claude Code doesn't load it while you work on the source.

## Prerequisites (one time)

- Visual Studio 2022 with the **.NET desktop development** workload. Community is fine.
- The .NET 8 SDK on Windows.
- WSL with `python3` and `rsync`, to run the scripts.
- Optional: the GitHub CLI in WSL, to publish from the terminal. Install it with `sudo apt install gh`, then run `gh auth login`.

## 1. Check the build

Close any running Chummer, then run:

```bash
scripts/win-test.sh --filter "FullyQualifiedName~AgentToolsTests"
```

Commit everything you want in the release and push it to `main`.

## 2. Pick a version

Use a tag that shows both the upstream Chummer version and the fork's own build number: `v<upstream>-mcp.<n>`, for example `v5.226.0-mcp.1`. The upstream version is `<AssemblyVersion>` in `Chummer/Chummer.csproj`. Increase `<n>` for each release made on the same upstream version.

## 3. Build the zip

```bash
scripts/win-release.sh 5.226.0-mcp.1
```

The script does the following:

- It deletes `Chummer/bin/Release` and rebuilds it from scratch, so nothing from local test runs (settings, characters, logs) ends up in the zip.
- It stamps the version into the build, so Chummer's title bar and the MCP server info show `5.226.0-mcp.1`. Saves keep the plain `AssemblyVersion`.
- It builds the launcher.
- It lays out the zip as shown above.
- It writes `dist/Chummer5a-MCP-5.226.0-mcp.1.zip`. The `dist/` folder is gitignored.

Smoke test before publishing:

1. Extract the zip to a new folder and run `Chummer5a.exe`.
2. Open a character.
3. Run `claude` in that folder and check `/mcp`.

## 4. Publish on GitHub

### With the GitHub CLI

```bash
git tag v5.226.0-mcp.1
git push origin v5.226.0-mcp.1
gh release create v5.226.0-mcp.1 dist/Chummer5a-MCP-5.226.0-mcp.1.zip \
  --repo mrjkey/chummer5a --title "Chummer5a MCP 5.226.0-mcp.1" --notes-file docs/release-notes.md
```

`docs/release-notes.md` is the template in the next section. Instead of `--notes-file`, you can use `--notes "..."`, or `--generate-notes` for an automatic list of commits. Add `--prerelease` for test builds.

### With the website

1. Go to `https://github.com/mrjkey/chummer5a/releases` and click **Draft a new release**.
2. Under **Choose a tag**, type the new tag (for example `v5.226.0-mcp.1`) and pick **Create new tag on publish**. Set the target to `main`.
3. Fill in a title and paste the release notes (template below).
4. Drag `dist\Chummer5a-MCP-<version>.zip` into the attachments box. From Windows, the file is at `C:\dev\chummer5a\dist\`.
5. Tick **Set as a pre-release** for test builds, or **Set as the latest release** otherwise. Then click **Publish release**.

The link users need is `https://github.com/mrjkey/chummer5a/releases/latest`.

## Release notes template

Saved as `docs/release-notes.md`. Edit the "What's new" section for each release.

## Notes

- `.github/workflows/nightly-build.yml` comes from upstream. It publishes `Nightly-v*` pre-releases using `dotnet build`, which fails on this project's binary resources. GitHub turns scheduled workflows off in forks, but **Run workflow** can still start it by hand. Disable it under **Actions** if you don't want it run by accident. `main.yml` (a Discord notification on release) needs a secret that the fork doesn't have, so it fails without doing any harm. Disable it too if the failures bother you.
- This build hides Chummer's updater (Tools → Check for Updates, and the automatic update options) and the Help menu's bug-report and issues links, all of which point at the official project, and it never sends Application Insights telemetry. See the constructors of `ChummerMainForm` and `EditGlobalSettings`, and `Program.Main`.
- Issues are switched off by default on GitHub forks. The fork's README and the release docs send bug reports to `mrjkey/chummer5a/issues`, so turn them on under **Settings → General → Features → Issues**.
