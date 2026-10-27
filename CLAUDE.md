# Chummer5a (fork with an AI-agent MCP server)

Chummer is a .NET Framework 4.8 WinForms app (Windows only). The repo lives on the Windows drive (`/mnt/c/dev/chummer5a`) and is built and run on the Windows host from WSL through the scripts in `scripts/`.

## Build / run / test from WSL

- `scripts/win-build.sh`: builds `Chummer.csproj` with Visual Studio's MSBuild (found via vswhere). `dotnet build` does **not** work (binary .resx resources, MSB3823). `CONFIG=Release scripts/win-build.sh` for a release build.
- `scripts/win-test.sh [dotnet test args]`: builds and runs `Chummer.Tests` (MSTest), e.g. `scripts/win-test.sh --filter "FullyQualifiedName~AgentToolsTests"`.
- `scripts/win-run.sh [chummer args]`: launches the built `Chummer5.exe`. Close any running Chummer before rebuilding.
- `scripts/win-release.sh [version]`: clean Release build packaged as `dist/Chummer5a-MCP-<version>.zip`. The top of the zip has the `ChummerLauncher` exe (`Chummer5a.exe`), the docs and the Claude Code setup files from `package/`. The app goes in `Chummer\`. See `docs/releasing.md`.
- This build differs from upstream outside the agent code too: the updater, the bug-report/issues menu items and Application Insights telemetry are off (they all point at the official project; see `ChummerMainForm`'s and `EditGlobalSettings`' constructors and `Program.Main`), and `Utils.CurrentChummerVersionLabel` shows the release name that `win-release.sh` stamps into `InformationalVersion`.
- `scripts/win-env.sh` holds the paths. If the repo is on the WSL filesystem instead, the scripts rsync it to `C:\dev\chummer5a` first (cmd-based build events break on `\\wsl$` paths). Override with `CHUMMER_WIN_DIR`, `CONFIG`, `MSBUILD` or `MSBUILD_SDKS_PATH`. The VS install has no .NET SDK resolver, so the scripts point MSBuild at the newest installed .NET 8 SDK and disable the workload resolver.

## AI agent integration

- `Chummer/Backend/Agent/` holds the MCP server:
  - `McpServer.cs`: Streamable HTTP, JSON responses, localhost only. Port 51723; override with `/mcpport:N`.
  - `AgentTools*.cs`: the tools. `AgentTools.cs` reads, `.Catalog.cs` searches the rules data, `.Create.cs` creates/opens/saves/exports characters and sets priorities, `.Edit.cs` edits attributes/skills/qualities/spells/powers/contacts/martial arts and personal details, `.Magic.cs` handles tradition, mentor, spirits, complex forms and initiation, `.Gear.cs` buys equipment and its accessories/mods (also drugs, suites, PACKS kits, natural weapons and weapon mounts), `.Items.cs` edits owned items (and copy/paste), and `.Critters.cs` handles critter powers, A.I. programs, cyberzombies and new critters.
- `Program.cs` starts the server around `Application.Run(MainForm)`.
- Tools work on the characters open in the editor (`Program.MainForm.OpenCharacterEditorForms`).
- Every edit goes through code the GUI also uses. These methods were refactored out of the forms' button/dialog code, so the results always match the UI:

  | Form | Methods |
  |---|---|
  | `CharacterCreate` | `CalculateKarmaRemainingAsync`, `CollectValidityIssuesAsync`, `AddQualityFromNodeAsync`, `RemoveQuality`, `SetTraditionAsync`, `AddInitiationGradeAsync`, and `Add{Weapon,Armor,ArmorMod,Gear,Cyberware,Vehicle,ComplexForm,Art,Metamagic,MartialArtTechnique}FromNodeAsync` |
  | `SelectMetatypePriority` | `ValidatePrioritySelectionAsync` (agent input only) and `ApplyPrioritySelectionAsync` |
  | `SelectMetatypeKarma` | `ApplyMetatypeSelectionAsync` |
  | `ChummerMainForm` | `OpenNewCharacterEditorAsync` |
  | `CharacterSheetViewer` | `TransformToHtmlAsync`, `ConvertHtmlToPdfAsync` |
  | `CreateWeaponMount` | `GetAllowedSizesAsync`, `GetAllowedOptionsAsync`, `SetWeaponMountOptionsAsync`, `CheckCareerPurchaseAsync`, `CreateMountModAsync` (static) |
  | `CharacterCreate` (more) | `AddCyberwareSuiteFromNodeAsync`, `AddPACKSKitFromNodeAsync`, `PasteClipboardAsync`, `AddEnchantmentFromNodeAsync`, `AddEnhancementFromNodeAsync`, `GetDrainAttributeChoices`, `GetTraditionSpiritChoicesAsync`, `CanCreateSpellAsync`, `CreateStackedFocusAsync`, `GetNextLifeModuleStageAsync`, `AddLifeModuleFromNodeAsync`, `CreateBackstoryAsync`, `DoReapplyImprovements` |
  | Dialog statics | `CreateCyberwareSuite.CheckCanCreateSuiteAsync`/`WriteSuiteAsync`, `CreatePACKSKit.WriteKitAsync`, `SelectCyberwareSuite.IsSuiteAllowedAsync`, `CreateNaturalWeapon.CreateWeaponAsync`, `CreateSpell.GetCategoryModifiers`/`CalculateDrainAsync`/`ValidateDesignAsync`/`CreateCustomSpellAsync`, `SelectAIProgram.IsProgramAllowedAsync`, `SpiritControl.CreateCritterCharacterAsync`; `SelectCritterPower` is constructed unshown for `FillCategoriesAsync`/`GetAllowedPowersAsync`/`GetPowerPointCostAsync` |
  | `ChummerMainForm` (more) | `PrepareNewCritterAsync`, `OpenNewCritterEditorAsync` |
  | `CharacterCareer` | `AddWeaponMountAsync` |
  | `CharacterShared` | `SaveCharacterToFileAsync`, `AddSpirit`/`AddSprite`, `Add{WeaponAccessory,VehicleMod}FromNodeAsync`, `AddDrugToCharacterAsync`, `AttachDrugToCyberwareAsync`, `CopyObject`, `ConvertToFreeSpriteAsync`, `ImportContactsAsync` |
  | Backend | `Character.GetCyberzombieRequirementsProblemAsync`/`ApplyCyberzombieAsync`, `MartialArt.PurchaseFromNodeAsync`, `Spirit.FillSpiritTypeChoicesAsync`, `SelectMartialArtTechnique.FillAllowedTechniques` |

  The agent constructs the metatype dialogs without showing them, and calls these cores on them.
- `CREATION-GAPS.md` is the checklist of creation-mode editor features the tools don't cover yet; tick items off there as tools are added.
- `.mcp.json` registers the server with Claude Code. `.claude/skills/sr5-build-advisor` is the conversational build workflow.
- Chummer must be running (and built with the agent code) for the `chummer` MCP tools to connect. WSL uses mirrored networking, so `localhost` reaches Windows.
- When adding a tool, put the rules logic in the same code path the GUI uses rather than re-implementing it, and add coverage to `Chummer.Tests/AgentToolsTests.cs`. `ToolsAgainstOpenCharacter` opens a real `CharacterCreate` form on `TestFiles/Skink.chum5`; `CreateCharacterAndGear` and `MagicSkillsAndMods` create characters through the tools (use the `Full House` settings for anything outside the core book). Items that ask a question when added (e.g. a Fake SIN's name) need `forced_value` in tests, because the hidden test window can't show dialogs.

## Code style

Match the surrounding code: Hungarian-ish prefixes (`str`, `int`, `bln`, `obj`, `lst`, `dic`), async methods with `CancellationToken token` and `.ConfigureAwait(false)` everywhere, and the GPL header on new files.
