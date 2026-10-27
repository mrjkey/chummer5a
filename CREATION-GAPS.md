# MCP creation-mode gaps

What a person can do to a character **during creation** in Chummer's editor that the `chummer` MCP tools can't do yet. Career mode is out of scope here.

Audited 2026-10-27 against the 68 tools in `Chummer/Backend/Agent/` (after `add_weapon_mount` was added), by comparing:

- every click, checkbox, spin-box and combo-box handler in `Forms/Character Forms/CharacterCreate.cs`
- the controls' data bindings in that form (`Register*DataBinding*`)
- the editor's user controls: `Controls/Attributes`, `Controls/Skills`, `Controls/Characters` (contacts, pets, spirits), `Controls/Powers` and `Controls/Shared/LimitTabUserControl`
- `ChummerMainForm` menus that create or manage characters

Status 2026-10-27: everything below is done except the unticked items (custom drugs, karma value, reapply improvements, and the main-window items). Tick an item when a tool covers it, and add test coverage to `Chummer.Tests/AgentToolsTests.cs`, as `CLAUDE.md` asks. When a GUI handler is named, its code is where the shared core should be refactored out.

Tags: **[common]** most builds hit it, **[book]** needs a non-core book, **[niche]** rare characters or conveniences.

## 1. Gear and equipment

- [x] **[common] Cyberware and bioware suites**: add a suite (Special > Add Cyberware/Bioware Suite, `mnuSpecialAddCyberwareSuite`/`mnuSpecialAddBiowareSuite`). Also save the current ware as a suite (`mnuSpecialCreateCyberwareSuite`/`mnuSpecialCreateBiowareSuite`). Today each piece has to be added one at a time with `add_cyberware`. **Done:** `add_cyberware_suite`, `create_cyberware_suite`. Suites saved in-program now load from `data/` (an `XmlManager` fix; upstream wrote them where they were never read).
- [x] **[common] PACKS kits**: add a kit (`mnuSpecialAddPACKSKit`) and create one (`mnuSpecialCreatePACKSKit`). There is no catalog kind for packs. **Done:** `add_packs_kit`, `create_packs_kit`.
- [x] **[common] Drugs**: buy a premade drug on the Drugs tab (`btnAddDrug` → `AddPremadeDrugAsync`). Also build or delete a custom drug (`btnDeleteCustomDrug`, the custom drug designer). `list_items` shows drugs and `set_item_values` changes their quantity, but nothing adds them. There is no `drug` catalog kind. **Done:** `add_drug`. Custom drug designer: still not covered.
- [x] **[book] Drugs in cyberware**, e.g. a drug reservoir (`tsCyberwareAddDrug`, `tsVehicleCyberwareAddDrug` → `AddDrugToCyberwareAsync`). **Done:** `add_drug(parent)`, creation only.
- [x] **[common] Armor rating after purchase** (`nudArmorRating`): `set_item_values` handles gear, cyberware, armor mods, weapons, accessories and vehicle mods, but not armor itself. Workaround: remove it and buy it again with `add_armor(rating)`. **Done:** Not a gap: the editor hides the rating box for armor itself (gear and mods in armor already work).
- [x] **[niche] Natural weapons** (`tsCreateNaturalWeapon` → `CreateNaturalWeapon` dialog), e.g. for changelings and critter-like characters. **Done:** `add_natural_weapon`.
- [x] **[book] Weapon mount add-ons** such as the Pop-Out or Blow-Away Panel add-on (the `CreateWeaponMount` dialog's Add Mod button, `cmdAddMod_Click`; data under `/chummer/weaponmountmods`). Its slots and cost depend on the mount. **Done:** `add_weapon_mount(mods)`, `edit_weapon_mount(add_mods/remove_mods)`.
- [x] **[niche] Editing an existing weapon mount** (`tsEditWeaponMount`: change size, visibility, flexibility, control or add-ons). Today you remove the mount and build it again. **Done:** `edit_weapon_mount`, creation only.
- [x] **[niche] Black market discount on armor mods**: `add_armor_mod` has no `black_market` argument. It may be settable afterwards with `set_item_state(black_market_discount)`; check. **Done:** Covered: `set_item_state(black_market_discount)` works on armor mods, and the GUI's dialog has no such box either.
- [x] **[niche] Copy and paste items** (`mnuEditCopy`/`mnuEditPaste`): duplicate a weapon, armor, gear item, vehicle and so on with its children. Workaround: buy it again. **Done:** `copy_item`, `paste_item(into)`.

## 2. Magic and resonance

- [x] **[common] Mystic adept Magic split** (`nudMysticAdeptMAGMagician`): how many points of Magic go to the adept side, which gives power points. Mystic adepts can't be built properly without it. **Done:** `set_mystic_adept_power_points`.
- [x] **[book] Custom tradition**: its name (`txtTraditionName`), drain attribute (`cboDrain`) and the spirit for each element (`cboSpiritCombat` and the other spirit boxes, see `BindSpiritVisibility`). `set_tradition` refuses custom traditions on purpose (`AgentTools.Magic.cs`). **Done:** `set_tradition(id='Custom', name, drain, spirits)`.
- [x] **[book] Adept Way and Geas power discounts**: the powers grid's `DiscountedAdeptWay` and `DiscountedGeas` columns (`PowersTabUserControl`). `set_power_rating` only changes the rating. **Done:** `set_power_rating(adept_way)`. Geas: the GUI's column is commented out, so nothing to match.
- [x] **[book] Enhancements**, power enhancements for initiated adepts (`tsMetamagicAddEnhancement` → `SelectArt` in `Mode.Enhancement`). `list_items` lists them but nothing adds them. **Done:** `add_art(kind='enhancement')`.
- [x] **[book] Enchantments** (`tsMetamagicAddEnchantment` → `SelectArt` in `Mode.Enchantment`). **Done:** `add_art(kind='enchantment')`.
- [x] **[niche] Custom spells** (`tsCreateSpell` → the `CreateSpell` dialog). **Done:** `create_spell`.
- [x] **[niche] Stacked foci**: build one from foci the character has (`cmdCreateStackedFocus`). `bond_focus` can bond an existing stack but can't create one. **Done:** `create_stacked_focus`.
- [x] **[niche] Convert a spirit or sprite to a free one** (`mnuSpecialConvertToFreeSprite`). **Done:** `convert_to_free_sprite` (for sprite characters). The data has no Denial power, so only the category changes; the shared core no longer crashes the GUI on that.
- [x] **[niche] Linked character file for a spirit**, i.e. attaching, opening or removing a file (`SpiritControl.tsAttachCharacter`/`tsContactOpen`/`tsRemoveCharacter`), and creating a critter character from a spirit (`tsCreateCharacter`). **Done:** `set_spirit_details(linked_file)`, `open_linked_character`, `create_spirit_character` (now also links the file, which upstream's comment said it did).

## 3. Critters, AIs and special characters

- [x] **[book] Critter powers**: add and delete them (`cmdAddCritterPower`/`cmdDeleteCritterPower`), and set whether one counts toward the free power limit (`chkCritterPowerCount` → `CritterPower.CountTowardsLimit`). Covers free spirits, Infected, shapeshifters and drakes. A `critterpower` catalog kind exists, but no tool adds powers. **Done:** `add_critter_power`, `set_critter_power`, `remove_item`. Creation only (career charges karma through dialogs).
- [x] **[book] AI programs**: add and delete them (`cmdAddAIProgram`/`cmdDeleteAIProgram`) for A.I. characters (Data Trails). `list_items` shows them. **Done:** `add_ai_program`, `remove_item`. Tested with the tab switched on by hand: new A.I.s ask a Codeslinger question the hidden test window can't answer.
- [x] **[book] Cyberzombie** conversion (`mnuSpecialCyberzombie`). **Done:** `convert_to_cyberzombie(wil_hits)`.
- [x] **[niche] New critter characters** (main form `mnuNewCritter`). `create_character` only makes metahumans, though `set_priorities` already reads `critters.xml` for critters. **Done:** `create_critter`, `list_metatypes(critters)`. Critters open in career mode, as in the GUI.

## 4. Build methods

- [x] **[book] Life Module builds**: adding life modules (`cmdLifeModule` → the life module selection dialog). The settings can be picked with `create_character`, but the build can't be finished. **Done:** `add_life_module(version)`. Only the refusal outside Life Module builds is tested.
- [x] **[book] Backstory generator** (`btnCreateBackstory` → `StoryBuilder`), which writes a backstory from the life modules. **Done:** `create_backstory`. Untested.

## 5. Skills, attributes and limits

- [x] **[niche] A skill's displayed attribute** (`SkillControl.btnAttribute`, reset with `SkillsTabUserControl.btnResetCustomDisplayAttribute`). This only changes which attribute the dice pool shows. **Done:** Not a gap: it is a display choice in the skill row, not saved with the character.
- [x] **[niche] Custom limit modifiers**: add, edit, delete and give notes to them on the Limits tab (`LimitTabUserControl.cmdAddLimitModifier`/`tssLimitModifierEdit`/`cmdDeleteLimitModifier`/`tssLimitModifierNotes`). `export_character(section='limitmodifiers')` can read them. **Done:** `manage_limit_modifier`.

## 6. Contacts

- [x] **[niche] Import contacts from a file** (`tsAddFromFile` → `AddContactsFromFile`). **Done:** `import_contacts`.
- [x] **[niche] Linked character file for a contact or pet**: attach, open or remove one (`ContactControl`/`PetControl` `tsAttachCharacter`/`tsContactOpen`/`tsRemoveCharacter`). **Done:** `set_contact_details(linked_file)`, `open_linked_character`.

## 7. Personal details

- [x] **[niche] Group name and group notes** (`txtGroupName`, `txtGroupNotes`). `set_character_details` doesn't take them. **Done:** `set_character_details(group_name, group_notes)`.

## 8. Special menu and maintenance

- [ ] **[niche] Karma value** report (`mnuSpecialKarmaValue` → `Character.CalculateKarmaValue`): the character's total karma worth, read only.
- [ ] **[niche] Reapply improvements** (`mnuSpecialReapplyImprovements`, and the per-item Reapply Improvements menu items): a repair tool for characters whose bonuses got out of sync.

## 9. Characters and files (main window)

- [ ] **[common] List saved characters**, as the Character Roster or File > Open would show them. `open_character` needs an exact path.
- [ ] **[common] Close an editor tab** (`mnuFileClose`). Characters opened or created by the agent stay open.
- [ ] **[niche] Import a Hero Lab portfolio** (`mnuHeroLabImporter`).
- [ ] **[niche] Print several characters at once** (`mnuFilePrintMultiple`). Workaround: `export_character_sheet` for each.
- [ ] **[niche] File > Export** to the other export formats (`mnuFileExport` → the export dialog's XSLT/JSON exporters). `export_character` returns the XML only.

## Catalog kinds `search_catalog` lacks

Adding these goes with the items above:

- [x] `drug` (drugs.xml)
- [x] weapon mount sizes and options (`vehicles.xml` `/chummer/weaponmounts/weaponmount`) and weapon mount add-ons (`/chummer/weaponmountmods/mod`). For now, `add_weapon_mount` lists the choices in its errors.
- [x] cyberware and bioware suites (`cyberware.xml`/`bioware.xml` `suites`)
- [x] PACKS kits (`packs.xml`)
- [x] power enhancements and enchantments
- [x] life modules (`lifemodules.xml`)

## Already covered (don't re-audit)

GUI → tool, for the creation editor:

| GUI | Tool |
|---|---|
| Add Weapon, Armor, Gear, Cyberware, Bioware, Vehicle (also into a location) | `add_weapon`, `add_armor`, `add_gear`, `add_cyberware`, `add_vehicle` (+ `set_item_details(location)`) |
| Add Accessory, Underbarrel, Armor Mod, Vehicle Mod, Weapon Mount; vehicle weapons, gear, sensors, cyberware | `add_weapon_accessory`, `add_weapon(parent)`, `add_armor_mod`, `add_vehicle_mod`, `add_weapon_mount`, `add_gear(parent)`, `add_cyberware(parent)` |
| Gear/armor/cyberware/accessory "Add as Plugin" and "Add Gear" | `add_gear(parent)` |
| Rating, quantity, grade, lifestyle months spin boxes | `set_item_values` (not armor rating, see above) |
| Equipped, wireless, active commlink, home node, stolen, black market, prototype transhuman, included in weapon, firing mode | `set_item_state` |
| Matrix attribute boxes (gear, cyberware, vehicles) | `swap_matrix_attributes` |
| Change Mount (cyberware, vehicle cyberware) | `move_modular_cyberware` |
| Notes, Name/Rename items | `set_item_details` |
| Locations: add/rename/delete for gear, armor (incl. Add Armor Bundle), weapons, vehicles | `manage_location` |
| Armor Equip/Unequip All | `set_item_state` per item |
| Delete buttons | `remove_item`, `remove_quality` |
| Qualities, quality levels | `add_quality(levels)`, `remove_quality` |
| Spells, rituals, preparations; powers; complex forms | `add_spell`, `add_power`, `set_power_rating`, `add_complex_form` |
| Tradition / stream (not custom) | `set_tradition` |
| Mentor spirit | `add_mentor_spirit` |
| Spirits, sprites and their details | `add_spirit`, `set_spirit_details` |
| Foci bonding | `bond_focus` |
| Initiation/submersion (group, ordeal, schooling), arts, metamagics, echoes | `add_initiation`, `add_art`, `add_metamagic` |
| Martial arts, techniques | `add_martial_art`, `add_technique` |
| Attributes, skills, groups, specializations, knowledge/language skills, exotic skills, skill notes | `set_attribute`, `set_skill`, `set_skill_group`, `set_specialization`, `add_knowledge_skill`, `add_exotic_skill`, `set_skill_details` |
| Contacts, enemies, pets and their details | `add_contact(kind)`, `set_contact_details` |
| Lifestyles (basic and advanced) | `add_lifestyle`, `edit_lifestyle` |
| Karma ↔ nuyen box | `set_karma_for_nuyen` |
| Description tab, primary arm | `set_character_details` |
| Mugshots | `manage_portrait` |
| Change Priorities, Change Metatype (karma builds) | `set_priorities` |
| Change Options (settings/books) | `set_build_settings` |
| Confirm Validity | `validate_character` |
| Save, Save As, Save as Created, Print, Open, New | `save_character`, `finish_creation`, `export_character_sheet`, `open_character`, `create_character` |

Not tools, on purpose (UI only): a skill's displayed attribute, sort and display filters, the contacts layout toggle and expand buttons (`cmdSwapContactOrder`, `cmdContactsExpansionToggle`), clicking a source to open its PDF, the dice roller, translator, XML editor, data exporter, global settings, updater, bug report and Discord/wiki links.
