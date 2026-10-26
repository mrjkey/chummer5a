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

Steps 0–8 are done: built, and 82/82 tests pass (76 existing plus 6 agent tests). The server was verified live from WSL and from Claude Code with Chummer running. Step 6 was also checked live: an elf face was created, edited, given gear and saved over MCP, and showed up correctly in the Chummer window.

### Step 0: Build/run loop (done)

- The Windows host has VS 2022 Community, the net48 targeting pack and .NET SDK 8.0.427. `global.json` pins SDK 8.0.401 with `rollForward: latestFeature`.
- **`dotnet build` does not work** because of binary `.resx` resources (MSB3823). Use **Visual Studio's MSBuild**:
  `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`
- This VS install has no .NET SDK resolver. Before running MSBuild, set:
  - `MSBuildSDKsPath=C:\Program Files\dotnet\sdk\8.0.427\Sdks`
  - `MSBuildEnableWorkloadResolver=false`
- Build command: `MSBuild Chummer\Chummer.csproj -restore -p:Configuration=Debug -p:SolutionDir=<repo>\ -m`. Pass `SolutionDir` so the post-build `copy LICENSE` step finds the file.
- Tests: build `Chummer.Tests\Chummer.Tests.csproj` the same way, then run `dotnet test Chummer.Tests\Chummer.Tests.csproj -c Debug --no-build`. The full suite takes about 5 minutes.
- The scripts `scripts/win-{env,sync,build,test,run,release}.sh` run the build on Windows through a generated `.cmd` file.
  - The repo lives on `C:\` (`/mnt/c/dev/chummer5a`), so they build in place. A WSL-filesystem checkout is still mirrored to `C:\dev\chummer5a` first, because cmd build events break on `\\wsl$` paths.
  - `win-run.sh` must launch with `powershell Start-Process`. `cmd /c start` never returns to WSL.
  - `win-release.sh` packages a release (see `docs/releasing.md`).

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
| `get_priority_table(category?, metatype?, settings?)` | The priority table rows, compact and limited to the enabled books. Heritage rows give each metatype's special attribute points and karma; Talent rows give the talents, what they grant and their free skills. |
| `list_build_settings` | The settings profiles: build method, build karma, priority table, maximum availability and books. |
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
  2. settings, priorities and metatype (`create_character` / `set_priorities`)
  3. attributes
  4. magic
  5. skills
  6. qualities
  7. contacts
  8. gear (`add_*`)
  9. `validate_character` and `save_character`

  Rules for the agent: always confirm legality with tools, cite source and page, propose before applying, and re-read state.
- Root `CLAUDE.md` documents the build/run/test commands and the agent architecture.

### Step 6: Character creation, gear and saving (done, `AgentTools.Create.cs`, `AgentTools.Gear.cs`)

- `create_character(settings, metatype, metavariant, priorities{heritage,attributes,talent,skills,resources}, talent, talent_skills, name, alias, force, ignore_rules)`. It goes through the same steps as File > New Character:
  1. Set the settings key and ignore-rules flag, which is all `SelectBuildMethod` does.
  2. Construct `SelectMetatypePriority` (or `SelectMetatypeKarma`) on the UI thread without showing it, then validate and apply. `MetatypeSelected` was split into a UI part and `ApplyPrioritySelectionAsync(PrioritySelection)` / `ApplyMetatypeSelectionAsync(...)`.
  3. Open the editor with `ChummerMainForm.OpenNewCharacterEditorAsync`, which was extracted from `ShowNewForm`.
- `ValidatePrioritySelectionAsync` covers what the dialog only ever offers:
  - letters used once each (or the Sum-to-Ten total)
  - the metatype and metavariant in the Heritage row and the enabled books
  - the talent in the Talent row and allowed for the metatype (`IsTalentAvailableAsync`, extracted from `PopulateTalents`)
  - the talent's free skill choices (`GetTalentSkillChoicesAsync`, extracted from `ProcessTalentsIndexChanged`)

  Refusals list the valid options.
- `set_priorities`: the same core on an open creation-mode character. Anything not passed keeps its current value; a new talent starts with no free skills.
- Gear tools. Each checks books, requirements, rating and `CheckAvailRestrictionAsync`, then calls a core extracted from the post-dialog half of its editor method:

  | Tool | Core | Notes |
  |---|---|---|
  | `add_weapon` | `AddWeaponFromNodeAsync` | |
  | `add_armor` | `AddArmorFromNodeAsync` | |
  | `add_gear` | `AddGearFromNodeAsync` | `parent`, `quantity` and `forced_value`. Identical items merge without the Yes/No prompt. |
  | `add_cyberware` | `AddCyberwareFromNodeAsync` | `kind` = cyberware or bioware, `grade`, `parent`. The essence multipliers that `PickCyberware` sets on the dialog are display-only; `Purchase` computes essence itself. |
  | `add_vehicle` | `AddVehicleFromNodeAsync` | |
  | `add_lifestyle` | — | `Lifestyle.CreateAsync` + `SetIncrementsAsync`, as Chummer adds lifestyles itself. |
- `remove_item` also removes weapons, armor, gear, cyberware, vehicles and lifestyles via `ICanRemove.RemoveAsync(false)`. That is what the editor's delete calls after its confirm prompt.
- `save_character(path?, overwrite?)`:
  - Saves to the existing file, otherwise to `<startup>\saves\<name>.chum5`.
  - Translates `/mnt/<drive>/` paths from WSL agents.
  - Uses `CharacterShared.SaveCharacterToFileAsync`, which was extracted from Save As.
  - Refuses if the "Character Created" box is ticked, because saving would finish creation.
- `FinishEditAsync` results now include essence.
- Validation (PLAN step 4): in collect mode, leftover-point warnings are reported even when there are errors. The GUI still only asks about them once everything else is fine.

### Step 7: Magic, skills, martial arts and mods (done, `AgentTools.Magic.cs`, `.Edit.cs`, `.Gear.cs`)

Each tool calls a core extracted from the GUI. The GUI now calls the same core:

| Tool | GUI code it shares |
|---|---|
| `set_tradition` (tradition or stream) | `CharacterCreate.SetTraditionAsync`, the body of the tradition combo's handler. It syncs the combo under `IsRefreshing`. |
| `add_mentor_spirit` | The Mentor Spirit quality with forced value `"Mentor|Choice 1|Choice 2"`. The async `selectmentorspirit`/`selectparagon` improvements skip their dialog only for that `|` format. |
| `add_complex_form` | `AddComplexFormFromNodeAsync`, `IsAtComplexFormLimitAsync` |
| `add_spirit` (spirit or sprite) | `AddSpirit`/`AddSprite` now return the entity. Allowed types come from `Spirit.FillSpiritTypeChoicesAsync`, moved out of `SpiritControl.RebuildSpiritList`. |
| `add_initiation` | `AddInitiationGradeAsync` |
| `add_art` | `AddArtFromNodeAsync` |
| `add_metamagic` | `AddMetamagicFromNodeAsync` |
| `set_specialization` | `Skill.SetTopMostDisplaySpecializationAsync`, as the spec box does |
| `add_exotic_skill` | `SkillsSection.AddExoticSkillAsync`, then `Upgrade` as the button does, or explicit ranks |
| `add_martial_art` | `MartialArt.PurchaseFromNodeAsync` |
| `add_technique` | `AddMartialArtTechniqueFromNodeAsync`; allowed list from `SelectMartialArtTechnique.FillAllowedTechniques` |
| `add_weapon_accessory` | `AddWeaponAccessoryFromNodeAsync`; mounts as the dialog picks them from `Weapon.GetAccessoryMountsAsync` |
| `add_armor_mod` | `AddArmorModFromNodeAsync`; categories from `GetArmorModCategories` |
| `add_vehicle_mod` | `AddVehicleModFromNodeAsync` |

Other changes:
- `remove_item` also removes spirits (unfettered first, as the editor does), complex forms, martial arts and techniques, arts, metamagics, initiation grades, accessories and mods.
- `get_character_summary` now lists mentors, spirits, initiation/submersion grade, arts, metamagics, techniques, accessories and mods.
- `search_catalog` gained the stream, art, technique, accessory, armormod and vehiclemod kinds.
- `add_quality`, `add_spell`, `add_power` and all the new tools now refuse items from books the settings don't enable, as the dialogs do.
- Every SR5 magician metamagic needs an art, and all the arts are in Street Grimoire. With the Standard (core-only) settings, mages therefore can't take metamagics.

### Step 8: Finishing touches found while building real characters (done)

These came up while building two complete core-rules characters, Darrow (a face) and Sif (an adept gunslinger). Both are in `characters/`, with `.chum5`, PDF/HTML sheets and a write-up each.

- `export_character_sheet(path?, format=pdf|html, sheet?)`: the Print Character viewer's path. `CharacterSheetViewer.TransformToHtmlAsync` and `ConvertHtmlToPdfAsync` were extracted from the viewer, which now uses them too; PDFs come from the bundled wkhtmltopdf.
- `open_character(path)`: File > Open's path (`Program.LoadCharacterAsync` + `ChummerMainForm.OpenCharacter`). Load problems come back as the tool's error instead of a message box.
- `set_karma_for_nuyen(karma)`: the editor's nuyen box (`Character.NuyenBP`).
- `add_power` takes `forced_value`, passed as the power's `Extra`. For example, Improved Ability (Pistols).
- `add_knowledge_skill` takes `type` (Academic, Interest, Language, Professional or Street), which sets the linked attribute.
- `set_specialization` takes `with_karma` (the skill's Karma checkbox). By default it pays with karma once skill points run out. Chummer only keeps that flag on a skill that already has a specialization, so the tool sets it after the specialization.
- `set_tradition` follows `Character.GetShouldHaveTraditionAsync`: adepts must have a tradition too, or validation fails.
- Ammunition, spare clips and speed loaders ask for a weapon category. `forced_value` (e.g. "Heavy Pistols") answers it. The dialog still flashes open and closes, which is fine live but impossible in the hidden test window.

### Tests (`Chummer.Tests/AgentToolsTests.cs`)

- `Protocol_InitializeListAndErrors`: initialize, tools/list, notifications, unknown method and unknown tool.
- `CommandLinePort`: the `/mcpport:N` override.
- `ToolsAgainstOpenCharacter` opens a hidden main form and a `CharacterCreate` editor on `TestFiles/Skink.chum5` and checks:
  - the summary, validation, legal-vs-all quality search, priority table, metatypes and export
  - that a karma-bought skill rank costs karma and an over-maximum skill or attribute is refused
  - contact and knowledge-skill add/remove
  - that adding a quality that overspends karma produces a warning and makes validation fail
- `CreateCharacterAndGear` covers:
  - the compact priority table (Heritage under 20 KB)
  - refusals for a duplicate letter, a metatype not allowed by its Heritage priority, and a Magician without free skills
  - creating a Human face (D/B/E/A/C, 140,000¥), then `set_priorities` raising resources to B (275,000¥)
  - adding a commlink, a Fake SIN (with `forced_value`), a weapon, armor, alphaware cyberware, bioware, a vehicle and a lifestyle, and refusing over-availability items
  - removing everything back to the starting nuyen and essence 6
  - unspent-point warnings while there are errors
  - saving to a temp file
- `MagicSkillsAndMods` covers:
  - a hermetic mage: tradition, a spirit (and a refused type), the Dog mentor, a Spellcasting specialization, initiation, and a metamagic refused for lack of its art
  - a technomancer: stream, complex form, sprite
  - a mundane: exotic skill, laser sight (and a refused mount), armor mod, vehicle mod, and martial arts refused under Standard
  - a Full House mage: art, metamagic, Krav Maga with a technique
- `AdeptExtrasOpenAndExport` covers:
  - an adept's power with `forced_value`, and its tradition
  - knowledge skill type
  - a specialization paid with karma
  - karma for nuyen
  - HTML and PDF sheets
  - opening a saved file
- `RunWithMainForm` is the shared hidden-main-window helper. The hidden window can't show dialogs, so items that ask a question need `forced_value` in tests.
- Only test files listed in `Chummer.Tests.csproj` are copied to the output (`Gangerbean.chum5` is not).

---

## Next steps (goal 1)

1. Career mode: the tools only edit characters in creation.
   - Rules focus is the core rulebook. Tools work with other books when the settings enable them, but defaults, docs and tests lean on Standard (core-only) settings.
2. Smaller gaps, each reusing the GUI code path when needed:
   - enchanting, enhancements and rituals
   - AI programs and critter powers
   - vehicle weapon mounts and weapons
   - gear inside armor or vehicles
   - locations

The MCP server stays always on and unauthenticated by design: it only listens on localhost, and anyone running this build wants it.

## Later (goal 2): cross-platform / web UI

- The MCP/HTTP layer is the contract. The same local server could serve a browser UI, but the engine still runs on Windows.
- Running fully OS-independent means moving the engine to .NET 8 without WinForms: removing the dialogs from Backend code, the TreeNode/menu helpers, and the data-binding/threading glue (64+ files).
- A Go rewrite isn't realistic, because much of the rules logic is C# code, not just the XML data.
- Revisit once the agent layer has the remaining edit tools.
