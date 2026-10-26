# Plan: AI agent co-pilot for Chummer5a

## Goals

1. **AI agent interaction (priority).** Build a Shadowrun 5e character by talking to a Claude Code agent ("where should my points go, and why?"). Chummer enforces the rules, so the result is always legal. The user can still open the editor and tweak by hand.
2. **Cross-platform / web UI (later).** Move away from Windows-only WinForms, e.g. a local server (Go or similar) that serves an HTML/JS/CSS editor in the browser.

## Decisions

- **Live in the running app.** Chummer hosts a local MCP server. The agent reads and edits the character open in the editor, and the user sees changes immediately. If a choice can't be passed in, Chummer's normal dialog appears for the user.
- **This is a fork, not tracking upstream.** Chummer internals can be changed freely. The server lives directly in `Chummer.csproj` (no MEF plugin), and logic stuck in forms gets extracted into reusable methods.

## Background (facts found while planning)

- Chummer5a is .NET Framework 4.8 WinForms. `Chummer/Backend` is about 363k lines; the rules content is in `Chummer/data/*.xml` (7.5 MB).
- There is no headless mode. 64 Backend files reference WinForms, and the improvement system opens selection dialogs straight from Backend code.
- Several key pieces live in `Forms/Character Forms/CharacterCreate.cs`, not the backend:
  - karma accounting (`CalculateBPandRefreshBPDisplays`)
  - finish-creation validation (`CheckCharacterValidity`)
  - the add-item flows (`cmdAdd*_Click`)
- Priority/metatype setup is in `SelectMetatypePriority.MetatypeSelected` (~1000 lines).
- Backend pieces the agent tools reuse:
  - requirement checks: `SelectionShared.RequirementsMetAsync`, `CheckAvailRestrictionAsync`
  - data loading: `Character.LoadDataXPathAsync`
  - the computed model: `Character.GenerateExportXml`
- Pre-filled choices: `ImprovementManager.SetForcedValue` / `strForceValue`.

---

## Status

Steps 0–5 are done: built, and 79/79 tests pass (76 existing plus 3 new). The server was verified live from WSL and from Claude Code with Chummer running.

### Step 0: Build/run loop (done)

- The Windows host has VS 2022 Community, the net48 targeting pack and .NET SDK 8.0.427. `global.json` pins SDK 8.0.401 with `rollForward: latestFeature`.
- **`dotnet build` does not work** because of binary `.resx` resources (MSB3823). Use **Visual Studio's MSBuild**:
  `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`
- This VS install has no .NET SDK resolver. Before running MSBuild, set:
  - `MSBuildSDKsPath=C:\Program Files\dotnet\sdk\8.0.427\Sdks`
  - `MSBuildEnableWorkloadResolver=false`
- Build command: `MSBuild Chummer\Chummer.csproj -restore -p:Configuration=Debug -p:SolutionDir=<repo>\ -m`. Pass `SolutionDir` so the post-build `copy LICENSE` step finds the file.
- Tests: build `Chummer.Tests\Chummer.Tests.csproj` the same way, then run `dotnet test Chummer.Tests\Chummer.Tests.csproj -c Debug --no-build`. The full suite takes about 5 minutes.
- When the repo lived in WSL, the scripts `scripts/win-{env,sync,build,test,run}.sh` mirrored it to `C:\dev\chummer5a` (cmd build events break on `\\wsl$` paths) and ran the build there through a generated `.cmd` file.
  - Now that the repo lives on `C:\`, build in place. The sync step is unnecessary.
  - `win-run.sh` must launch with `powershell Start-Process`. `cmd /c start` never returns to WSL.

### Step 1: MCP server inside Chummer (done)

- `Chummer/Backend/Agent/McpServer.cs`:
  - Hand-rolled MCP Streamable HTTP transport (JSON responses only, no SSE) on `HttpListener`, at `http://localhost:51723/mcp`. Override the port with `/mcpport:N`.
  - Handles `initialize`, `ping`, `tools/list` and `tools/call`. Notifications get a 202. Requests whose Origin isn't loopback are rejected.
  - Tool errors come back as `isError: true` results.
- `Chummer/Backend/Agent/AgentTool.cs` holds the tool definition, the schema helper and `AgentToolException`.
- `Program.cs` wraps `Application.Run(MainForm)` in `using (Agent.McpServer.TryStart(...))`.
- Target character: the optional `character` argument (name, alias or file name). Otherwise the active MDI editor tab, taken from `Program.MainForm.OpenCharacterEditorForms`.
- WSL2 uses `networkingMode=mirrored`, so `localhost` from WSL reaches Windows.

### Step 2: Read-only tools (done, `AgentTools.cs`, `AgentTools.Catalog.cs`)

| Tool | What it returns |
|---|---|
| `list_open_characters` | The open characters and which one is active. |
| `get_character_summary` | Build method, priorities, karma remaining with a per-category breakdown, and points remaining (attribute, special, skill, skill group, knowledge, contact, nuyen). Also attributes (base/karma/value/min/max/maxBase/maxKarma), skills, groups, qualities, spells, powers, cyberware, gear, weapons, armor, vehicles, lifestyles and contacts. |
| `validate_character` | Errors and warnings from Chummer's finish-creation check, with no dialogs. |
| `export_character` | The `GenerateExportXml` export as JSON. Without `section` it lists the available sections. |
| `search_catalog(kind, query, category, legal_only=true, limit)` | Catalog matches. `kind` is one of: quality, spell, power, complexform, cyberware, bioware, gear, weapon, armor, vehicle, martialart, metamagic, echo, mentor, tradition, lifestyle, critterpower, program, skill, knowledgeskill. Filtered by the character's book setting, metatype quality restrictions, `RequirementsMetAsync` and availability. Works without a character, using the default settings. |
| `get_catalog_item(kind, id)` | The full data node, plus whether the character qualifies. |
| `get_priority_table(category?)` | The priority table rows. |
| `list_metatypes(category?)` | Metatypes and their attributes. |

### Step 3: Extract accounting/validation out of `CharacterCreate` (done)

- `CalculateBPandRefreshBPDisplays` gained an `IDictionary<string,int>` per-category breakdown and an internal wrapper, `CalculateKarmaRemainingAsync(dic, token)`.
- `CheckCharacterValidity` now has a private core that takes `lstErrors`/`lstWarnings`, plus an internal `CollectValidityIssuesAsync`.
  - Yes/No prompts go through `ShowValidityPromptAsync`, which records a warning and continues when issues are being collected.
  - Known quirk: the leftover-points warnings only appear once there are no errors, because the original code gates them on `blnValid`.
- The body of `cmdAddQuality_Click` was extracted into `internal AddQualityFromNodeAsync(node, id, ratingToAdd, free, forcedValue, showMessages, token)`. It returns `(Added, Refusal)`. The button calls it.
- `RemoveQuality` was made internal.

### Step 4: Edit tools (done, `AgentTools.Edit.cs`; creation mode only)

- `set_attribute`: limits come from `PriorityMaximum`/`KarmaMaximum`.
- `set_skill`, `set_skill_group`: check the unlocked state, free ranks and rating maximum.
- `add_knowledge_skill`, including native language.
- `add_quality`: requirements plus the quality karma limits, via the extracted method. Takes `levels` and `forced_value`. `remove_quality` refuses metatype- or improvement-granted qualities.
- `add_spell`: requirements plus the creation limit of MAG×2 per type.
- `add_power` and `set_power_rating`: capped at the maximum level.
- `add_contact`, and `remove_item` for spells, powers and contacts.
- Every edit calls `MakeDirtyWithCharacterUpdate` and returns karma and points remaining. It adds a `warning` when anything is over budget; the editor allows overspending but validation then fails.

### Step 5: Claude Code side (done)

- `.mcp.json`: `{"mcpServers":{"chummer":{"type":"http","url":"http://localhost:51723/mcp"}}}`
- `.claude/skills/sr5-build-advisor/SKILL.md` covers the conversation flow:
  1. concept
  2. priorities and metatype (the user applies them in the GUI)
  3. attributes
  4. magic
  5. skills
  6. qualities
  7. contacts
  8. gear (the user adds it by hand for now)
  9. `validate_character`

  Rules for the agent: always confirm legality with tools, cite source and page, propose before applying, and re-read state.
- Root `CLAUDE.md` documents the build/run/test commands and the agent architecture.

### Tests (`Chummer.Tests/AgentToolsTests.cs`)

- `Protocol_InitializeListAndErrors`: initialize, tools/list, notifications, unknown method and unknown tool.
- `CommandLinePort`: the `/mcpport:N` override.
- `ToolsAgainstOpenCharacter` opens a hidden main form and a `CharacterCreate` editor on `TestFiles/Skink.chum5` and checks:
  - the summary, validation, legal-vs-all quality search, priority table, metatypes and export
  - that a karma-bought skill rank costs karma and an over-maximum skill or attribute is refused
  - contact and knowledge-skill add/remove
  - that adding a quality that overspends karma produces a warning and makes validation fail
- Only test files listed in `Chummer.Tests.csproj` are copied to the output (`Gangerbean.chum5` is not).

---

## Next steps (goal 1)

1. Fix the scripts and `CLAUDE.md` now that the repo lives on `C:\`: build in place, with no rsync.
2. More edit tools, each reusing the GUI code path:
   - `add_cyberware(id, grade, rating)`: refactor `PickCyberware`.
   - `add_gear`, `add_weapon`, `add_armor`, `add_vehicle`, `add_lifestyle`: extract from their `cmdAdd*` handlers.
   - Tradition and mentor spirit selection.
   - Skill specializations.
   - Complex forms, martial arts, spirits.
3. A `set_priorities` / `new_character` tool: extract `SelectMetatypePriority.MetatypeSelected` (and its karma counterpart in `SelectMetatypeKarma`) into a backend method.
4. Report leftover-point warnings even while there are errors (remove the `blnValid` gating in collect mode).
5. An optional on/off toggle or token for the MCP server. It is currently always on and localhost-only, with no auth.

## Later (goal 2): cross-platform / web UI

- The MCP/HTTP layer is the contract. The same local server could serve a browser UI, but the engine still runs on Windows.
- Running fully OS-independent means moving the engine to .NET 8 without WinForms: removing the dialogs from Backend code, the TreeNode/menu helpers, and the data-binding/threading glue (64+ files).
- A Go rewrite isn't realistic, because much of the rules logic is C# code, not just the XML data.
- Revisit once the agent layer has the remaining edit tools.
