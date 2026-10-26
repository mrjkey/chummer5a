/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Chummer.Backend.Attributes;
using Chummer.Backend.Enums;
using Chummer.Backend.Equipment;
using Chummer.Backend.Skills;
using Chummer.Backend.Uniques;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Chummer.Agent
{
    /// <summary>
    /// The tools Chummer exposes to AI agents. Reads go straight to the backend objects; anything that the character
    /// editor computes (karma totals, validation) is asked of the open <see cref="CharacterCreate"/> form so the numbers
    /// match what the user sees.
    /// </summary>
    public static partial class AgentTools
    {
        public const string ServerInstructions =
            "Chummer is a Shadowrun 5th Edition character builder. These tools read and edit the characters the user has open in Chummer's editor; "
            + "the user sees every change live and can also edit by hand, so re-read state (get_character_summary) instead of assuming it. "
            + "Chummer enforces the rules: use search_catalog with legal_only=true to find options the character can actually take, "
            + "and validate_character before calling a character finished. To start a character, pick settings (list_build_settings), plan with "
            + "get_priority_table, then create_character; set_priorities changes them later. Spend points with set_attribute/set_skill/add_quality "
            + "and friends; magic and resonance with set_tradition, add_spell, add_power, add_complex_form, add_mentor_spirit, add_spirit, add_initiation, "
            + "add_art and add_metamagic; buy gear with add_weapon/add_armor/add_gear/add_cyberware/add_vehicle/add_lifestyle and fit it out with "
            + "add_weapon_accessory/add_armor_mod/add_vehicle_mod; remove_item undoes any of these. set_character_details fills in looks, concept and background. "
            + "Call save_character when the user wants it saved and "
            + "export_character_sheet for a sheet to play from; open_character reopens saved files. Unless asked otherwise, stick to the core rulebook (the Standard settings). "
            + "Edits only work while the character is in creation mode, except set_character_details.";

        private const string CharacterArgDescription =
            "Which open character to use (matches name, alias or file name). Defaults to the character in the active editor tab.";

        public static IEnumerable<AgentTool> CreateAll()
        {
            yield return new AgentTool("list_open_characters",
                "List the characters currently open in Chummer's editor, and which one is active.",
                AgentTool.Schema(), (a, t) => ListOpenCharactersAsync(t));
            yield return new AgentTool("get_character_summary",
                "Everything needed to plan a build: build method, priorities, points/karma remaining per category, attributes (base/karma/limits), "
                + "skills, qualities, magic/resonance, gear and contacts.",
                AgentTool.Schema(("character", "string", CharacterArgDescription)), GetCharacterSummaryAsync);
            yield return new AgentTool("validate_character",
                "Run Chummer's finish-creation checks without any dialogs. Errors block finishing creation; warnings are things like unspent points.",
                AgentTool.Schema(("character", "string", CharacterArgDescription)), ValidateCharacterAsync);
            yield return new AgentTool("export_character",
                "Chummer's full computed character export (the data behind the printed character sheets: dice pools, limits, initiative, "
                + "armor, weapons with damage, etc). Without `section` this lists the available sections and their sizes.",
                AgentTool.Schema(("character", "string", CharacterArgDescription),
                                 ("section", "string", "Top-level export element to return, e.g. 'attributes', 'skills', 'qualities', 'weapons', 'limitmodifiers'. Use '*' for everything.")),
                ExportCharacterAsync);
            foreach (AgentTool objTool in CreateCatalogTools())
                yield return objTool;
            foreach (AgentTool objTool in CreateCreationTools())
                yield return objTool;
            foreach (AgentTool objTool in CreateEditTools())
                yield return objTool;
            foreach (AgentTool objTool in CreateGearTools())
                yield return objTool;
            foreach (AgentTool objTool in CreateMagicTools())
                yield return objTool;
        }

        #region Character resolution

        /// <summary>
        /// Find the editor form for the character the agent means.
        /// </summary>
        internal static async Task<CharacterShared> GetCharacterFormAsync(JObject objArgs, CancellationToken token)
        {
            ChummerMainForm frmMain = Program.MainForm
                                      ?? throw new AgentToolException("Chummer's main window is not open.");
            List<CharacterShared> lstForms = await frmMain.OpenCharacterEditorForms.ToListAsync(token).ConfigureAwait(false);
            if (lstForms.Count == 0)
                throw new AgentToolException(
                    "No character is open in Chummer. Ask the user to create one (File > New Character) or open a .chum5 file.");
            string strWanted = objArgs?.Value<string>("character");
            if (!string.IsNullOrWhiteSpace(strWanted))
            {
                strWanted = strWanted.Trim();
                List<CharacterShared> lstMatches = new List<CharacterShared>();
                foreach (CharacterShared frmLoop in lstForms)
                {
                    Character objLoop = frmLoop.CharacterObject;
                    if (objLoop == null)
                        continue;
                    string[] astrNames =
                    {
                        await objLoop.GetCharacterNameAsync(token).ConfigureAwait(false),
                        await objLoop.GetNameAsync(token).ConfigureAwait(false),
                        await objLoop.GetAliasAsync(token).ConfigureAwait(false),
                        System.IO.Path.GetFileNameWithoutExtension(await objLoop.GetFileNameAsync(token).ConfigureAwait(false) ?? string.Empty)
                    };
                    if (astrNames.Any(x => string.Equals(x, strWanted, StringComparison.OrdinalIgnoreCase)))
                        return frmLoop;
                    if (astrNames.Any(x => !string.IsNullOrEmpty(x) && x.IndexOf(strWanted, StringComparison.OrdinalIgnoreCase) >= 0))
                        lstMatches.Add(frmLoop);
                }

                if (lstMatches.Count == 1)
                    return lstMatches[0];
                throw new AgentToolException((lstMatches.Count == 0 ? "No open character matches '" : "More than one open character matches '")
                                             + strWanted + "'. Use list_open_characters.");
            }

            Form frmActive = await frmMain.DoThreadSafeFuncAsync(x => x.ActiveMdiChild, token).ConfigureAwait(false);
            if (frmActive is CharacterShared frmActiveCharacter && lstForms.Contains(frmActiveCharacter))
                return frmActiveCharacter;
            if (lstForms.Count == 1)
                return lstForms[0];
            throw new AgentToolException("Several characters are open and none is active; pass `character`. Use list_open_characters.");
        }

        internal static async Task<CharacterCreate> GetCreateFormAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            return frmCharacter as CharacterCreate
                   ?? throw new AgentToolException("This character has finished creation and is in career mode; these tools only work during character creation.");
        }

        #endregion

        #region Read tools

        private static async Task<object> ListOpenCharactersAsync(CancellationToken token)
        {
            ChummerMainForm frmMain = Program.MainForm
                                      ?? throw new AgentToolException("Chummer's main window is not open.");
            List<CharacterShared> lstForms = await frmMain.OpenCharacterEditorForms.ToListAsync(token).ConfigureAwait(false);
            Form frmActive = await frmMain.DoThreadSafeFuncAsync(x => x.ActiveMdiChild, token).ConfigureAwait(false);
            List<object> lstReturn = new List<object>(lstForms.Count);
            foreach (CharacterShared frmLoop in lstForms)
            {
                Character objLoop = frmLoop.CharacterObject;
                if (objLoop == null)
                    continue;
                lstReturn.Add(new
                {
                    name = await objLoop.GetCharacterNameAsync(token).ConfigureAwait(false),
                    file = await objLoop.GetFileNameAsync(token).ConfigureAwait(false),
                    metatype = await objLoop.GetMetatypeAsync(token).ConfigureAwait(false),
                    mode = frmLoop is CharacterCreate ? "creation" : "career",
                    active = ReferenceEquals(frmLoop, frmActive)
                });
            }

            return lstReturn;
        }

        private static async Task<object> GetCharacterSummaryAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            CharacterSettings objSettings = await objCharacter.GetSettingsAsync(token).ConfigureAwait(false);
            CharacterBuildMethod eBuildMethod = await objCharacter.GetEffectiveBuildMethodAsync(token).ConfigureAwait(false);
            bool blnUsesPriorities = eBuildMethod.UsesPriorityTables();
            Dictionary<string, object> dicReturn = new Dictionary<string, object>
            {
                ["name"] = await objCharacter.GetCharacterNameAsync(token).ConfigureAwait(false),
                ["file"] = await objCharacter.GetFileNameAsync(token).ConfigureAwait(false),
                ["mode"] = frmCharacter is CharacterCreate ? "creation" : "career",
                ["settings"] = await objSettings.GetNameAsync(token).ConfigureAwait(false),
                ["buildMethod"] = eBuildMethod.ToString(),
                ["ignoreRules"] = await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false),
                ["metatype"] = await objCharacter.GetMetatypeAsync(token).ConfigureAwait(false),
                ["metavariant"] = await objCharacter.GetMetavariantAsync(token).ConfigureAwait(false),
                ["metatypeCategory"] = await objCharacter.GetMetatypeCategoryAsync(token).ConfigureAwait(false)
            };
            if (blnUsesPriorities)
            {
                dicReturn["priorityTable"] = await objSettings.GetPriorityTableAsync(token).ConfigureAwait(false);
                dicReturn["priorities"] = new
                {
                    metatype = await objCharacter.GetMetatypePriorityAsync(token).ConfigureAwait(false),
                    attributes = await objCharacter.GetAttributesPriorityAsync(token).ConfigureAwait(false),
                    talent = await objCharacter.GetSpecialPriorityAsync(token).ConfigureAwait(false),
                    skills = await objCharacter.GetSkillsPriorityAsync(token).ConfigureAwait(false),
                    resources = await objCharacter.GetResourcesPriorityAsync(token).ConfigureAwait(false),
                    talentName = await objCharacter.GetTalentPriorityAsync(token).ConfigureAwait(false)
                };
            }

            if (frmCharacter is CharacterCreate frmCreate)
            {
                Dictionary<string, int> dicKarmaUsed = new Dictionary<string, int>();
                int intKarmaRemaining = await frmCreate.CalculateKarmaRemainingAsync(dicKarmaUsed, token).ConfigureAwait(false);
                dicReturn["karma"] = new
                {
                    buildKarma = await objSettings.GetBuildKarmaAsync(token).ConfigureAwait(false),
                    remaining = intKarmaRemaining,
                    spentByCategory = dicKarmaUsed.Where(x => x.Value != 0).ToDictionary(x => x.Key, x => x.Value)
                };
            }
            else
                dicReturn["karma"] = new { available = await objCharacter.GetKarmaAsync(token).ConfigureAwait(false) };

            AttributeSection objAttributeSection = await objCharacter.GetAttributeSectionAsync(token).ConfigureAwait(false);
            List<CharacterAttrib> lstAttributes = await (await objAttributeSection.GetAttributeListAsync(token).ConfigureAwait(false))
                .ToListAsync(token).ConfigureAwait(false);
            List<CharacterAttrib> lstSpecialAttributes = await (await objAttributeSection.GetSpecialAttributeListAsync(token).ConfigureAwait(false))
                .ToListAsync(token).ConfigureAwait(false);
            SkillsSection objSkillsSection = await objCharacter.GetSkillsSectionAsync(token).ConfigureAwait(false);
            (decimal decNuyenRemaining, decimal _) = await objCharacter.CalculateNuyenCreateModeAsync(token).ConfigureAwait(false);
            (int intContactPointsUsed, int _) = await objCharacter.GetContactPointsUsedAsync(token).ConfigureAwait(false);
            Dictionary<string, object> dicPoints = new Dictionary<string, object>();
            if (blnUsesPriorities)
            {
                int intAttributePointsSpent = 0;
                foreach (CharacterAttrib objAttribute in lstAttributes)
                    intAttributePointsSpent += await objAttribute.GetSpentPriorityPointsAsync(token).ConfigureAwait(false);
                int intSpecialPointsSpent = 0;
                foreach (CharacterAttrib objAttribute in lstSpecialAttributes)
                    intSpecialPointsSpent += await objAttribute.GetSpentPriorityPointsAsync(token).ConfigureAwait(false);
                int intAttributePoints = await objCharacter.GetTotalAttributesAsync(token).ConfigureAwait(false);
                int intSpecialPoints = await objCharacter.GetTotalSpecialAsync(token).ConfigureAwait(false);
                dicPoints["attributePoints"] = new { total = intAttributePoints, remaining = intAttributePoints - intAttributePointsSpent };
                dicPoints["specialAttributePoints"] = new { total = intSpecialPoints, remaining = intSpecialPoints - intSpecialPointsSpent };
                dicPoints["skillPoints"] = new { total = objSkillsSection.SkillPointsMaximum, remaining = await objSkillsSection.GetSkillPointsAsync(token).ConfigureAwait(false) };
                dicPoints["skillGroupPoints"] = new { total = objSkillsSection.SkillGroupPointsMaximum, remaining = await objSkillsSection.GetSkillGroupPointsAsync(token).ConfigureAwait(false) };
            }

            dicPoints["knowledgeSkillPoints"] = new
            {
                total = await objSkillsSection.GetKnowledgeSkillPointsAsync(token).ConfigureAwait(false),
                remaining = await objSkillsSection.GetKnowledgeSkillPointsRemainAsync(token).ConfigureAwait(false)
            };
            int intContactPoints = await objCharacter.GetContactPointsAsync(token).ConfigureAwait(false);
            dicPoints["contactPoints"] = new { total = intContactPoints, remaining = intContactPoints - intContactPointsUsed };
            dicPoints["nuyen"] = new
            {
                starting = await objCharacter.GetStartingNuyenAsync(token).ConfigureAwait(false),
                remaining = decNuyenRemaining
            };
            dicReturn["points"] = dicPoints;

            dicReturn["attributes"] = await DescribeAttributesAsync(lstAttributes, token).ConfigureAwait(false);
            dicReturn["specialAttributes"] = await DescribeAttributesAsync(lstSpecialAttributes, token).ConfigureAwait(false);
            dicReturn["essence"] = await objCharacter.EssenceAsync(token: token).ConfigureAwait(false);

            bool blnMagician = await objCharacter.GetMagicianEnabledAsync(token).ConfigureAwait(false);
            bool blnAdept = await objCharacter.GetAdeptEnabledAsync(token).ConfigureAwait(false);
            bool blnTechnomancer = await objCharacter.GetTechnomancerEnabledAsync(token).ConfigureAwait(false);
            if (blnMagician || blnAdept || blnTechnomancer)
            {
                Tradition objTradition = await objCharacter.GetMagicTraditionAsync(token).ConfigureAwait(false);
                dicReturn["magic"] = new
                {
                    magician = blnMagician,
                    adept = blnAdept,
                    technomancer = blnTechnomancer,
                    tradition = objTradition != null ? await objTradition.GetCurrentDisplayNameAsync(token).ConfigureAwait(false) : null,
                    freeSpells = await objCharacter.GetFreeSpellsAsync(token).ConfigureAwait(false),
                    freeComplexForms = await objCharacter.GetCFPLimitAsync(token).ConfigureAwait(false),
                    powerPoints = blnAdept ? new
                    {
                        total = await objCharacter.GetPowerPointsTotalAsync(token).ConfigureAwait(false),
                        used = await objCharacter.GetPowerPointsUsedAsync(token).ConfigureAwait(false)
                    } : null,
                    mentorSpirits = await DescribeNamedAsync(objCharacter.MentorSpirits, token).ConfigureAwait(false),
                    initiateGrade = await objCharacter.GetInitiateGradeAsync(token).ConfigureAwait(false),
                    submersionGrade = await objCharacter.GetSubmersionGradeAsync(token).ConfigureAwait(false),
                    metamagics = await DescribeNamedAsync(objCharacter.Metamagics, token).ConfigureAwait(false),
                    arts = await DescribeNamedAsync(objCharacter.Arts, token).ConfigureAwait(false)
                };
                List<object> lstSpirits = new List<object>();
                foreach (Spirit objSpirit in await objCharacter.Spirits.ToListAsync(token).ConfigureAwait(false))
                {
                    lstSpirits.Add(new
                    {
                        id = objSpirit.InternalId,
                        kind = (await objSpirit.GetEntityTypeAsync(token).ConfigureAwait(false)).ToString(),
                        type = await objSpirit.GetNameAsync(token).ConfigureAwait(false),
                        force = await objSpirit.GetForceAsync(token).ConfigureAwait(false),
                        services = await objSpirit.GetServicesOwedAsync(token).ConfigureAwait(false),
                        bound = await objSpirit.GetBoundAsync(token).ConfigureAwait(false)
                    });
                }

                dicReturn["spirits"] = lstSpirits;
            }

            List<object> lstSkillGroups = new List<object>();
            foreach (SkillGroup objGroup in await objSkillsSection.SkillGroups.ToListAsync(token).ConfigureAwait(false))
            {
                int intRating = await objGroup.GetRatingAsync(token).ConfigureAwait(false);
                if (intRating <= 0)
                    continue;
                lstSkillGroups.Add(new
                {
                    name = await objGroup.GetCurrentDisplayNameAsync(token).ConfigureAwait(false),
                    @base = await objGroup.GetBaseAsync(token).ConfigureAwait(false),
                    karma = await objGroup.GetKarmaAsync(token).ConfigureAwait(false),
                    rating = intRating
                });
            }

            dicReturn["skillGroups"] = lstSkillGroups;
            dicReturn["skills"] = await DescribeSkillsAsync(await objSkillsSection.Skills.ToListAsync(token).ConfigureAwait(false), token).ConfigureAwait(false);
            dicReturn["knowledgeSkills"] = await DescribeSkillsAsync(
                (await objSkillsSection.KnowledgeSkills.ToListAsync(token).ConfigureAwait(false)).Cast<Skill>().ToList(), token, true).ConfigureAwait(false);

            List<object> lstQualities = new List<object>();
            foreach (Quality objQuality in await (await objCharacter.GetQualitiesAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false))
            {
                lstQualities.Add(new
                {
                    id = objQuality.InternalId,
                    name = await objQuality.GetCurrentDisplayNameAsync(token).ConfigureAwait(false),
                    type = (await objQuality.GetTypeAsync(token).ConfigureAwait(false)).ToString(),
                    karma = await objQuality.GetBPAsync(token).ConfigureAwait(false) * await objQuality.GetLevelsAsync(token).ConfigureAwait(false),
                    source = (await objQuality.GetOriginSourceAsync(token).ConfigureAwait(false)).ToString()
                });
            }

            dicReturn["qualities"] = lstQualities;
            dicReturn["spells"] = await DescribeNamedAsync(await objCharacter.GetSpellsAsync(token).ConfigureAwait(false), token).ConfigureAwait(false);
            dicReturn["complexForms"] = await DescribeNamedAsync(await objCharacter.GetComplexFormsAsync(token).ConfigureAwait(false), token).ConfigureAwait(false);
            List<object> lstMartialArts = new List<object>();
            foreach (MartialArt objArt in await (await objCharacter.GetMartialArtsAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false))
            {
                lstMartialArts.Add(new
                {
                    id = objArt.InternalId,
                    name = await objArt.GetCurrentDisplayNameAsync(token).ConfigureAwait(false),
                    techniques = await DescribeNamedAsync(objArt.Techniques, token).ConfigureAwait(false)
                });
            }

            dicReturn["martialArts"] = lstMartialArts;
            List<object> lstPowers = new List<object>();
            foreach (Power objPower in await (await objCharacter.GetPowersAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false))
            {
                lstPowers.Add(new
                {
                    id = objPower.InternalId,
                    name = await objPower.GetCurrentDisplayNameAsync(token).ConfigureAwait(false),
                    rating = await objPower.GetRatingAsync(token).ConfigureAwait(false)
                });
            }

            dicReturn["powers"] = lstPowers;
            List<object> lstCyberware = new List<object>();
            foreach (Cyberware objCyberware in await (await objCharacter.GetCyberwareAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false))
            {
                Grade objGrade = await objCyberware.GetGradeAsync(token).ConfigureAwait(false);
                lstCyberware.Add(new
                {
                    id = objCyberware.InternalId,
                    name = await objCyberware.GetCurrentDisplayNameAsync(token).ConfigureAwait(false),
                    grade = objGrade != null ? await objGrade.GetCurrentDisplayNameAsync(token).ConfigureAwait(false) : null,
                    rating = await objCyberware.GetRatingAsync(token).ConfigureAwait(false),
                    essence = await objCyberware.GetCalculatedESSAsync(token).ConfigureAwait(false)
                });
            }

            dicReturn["cyberware"] = lstCyberware;
            List<object> lstGear = new List<object>();
            foreach (Gear objGear in await (await objCharacter.GetGearAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false))
            {
                lstGear.Add(new
                {
                    id = objGear.InternalId,
                    name = await objGear.GetCurrentDisplayNameAsync(token).ConfigureAwait(false),
                    rating = await objGear.GetRatingAsync(token).ConfigureAwait(false),
                    quantity = objGear.Quantity
                });
            }

            dicReturn["gear"] = lstGear;
            List<object> lstWeapons = new List<object>();
            foreach (Weapon objWeapon in await (await objCharacter.GetWeaponsAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false))
            {
                lstWeapons.Add(new
                {
                    id = objWeapon.InternalId,
                    name = await objWeapon.GetCurrentDisplayNameAsync(token).ConfigureAwait(false),
                    accessories = await DescribeNamedAsync(objWeapon.WeaponAccessories, token).ConfigureAwait(false)
                });
            }

            dicReturn["weapons"] = lstWeapons;
            List<object> lstArmor = new List<object>();
            foreach (Armor objArmor in await (await objCharacter.GetArmorAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false))
            {
                lstArmor.Add(new
                {
                    id = objArmor.InternalId,
                    name = await objArmor.GetCurrentDisplayNameAsync(token).ConfigureAwait(false),
                    mods = await DescribeNamedAsync(objArmor.ArmorMods, token).ConfigureAwait(false)
                });
            }

            dicReturn["armor"] = lstArmor;
            List<object> lstVehicles = new List<object>();
            foreach (Vehicle objVehicle in await (await objCharacter.GetVehiclesAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false))
            {
                lstVehicles.Add(new
                {
                    id = objVehicle.InternalId,
                    name = await objVehicle.GetCurrentDisplayNameAsync(token).ConfigureAwait(false),
                    mods = await DescribeNamedAsync(objVehicle.Mods, token).ConfigureAwait(false)
                });
            }

            dicReturn["vehicles"] = lstVehicles;
            List<object> lstLifestyles = new List<object>();
            foreach (Lifestyle objLifestyle in await (await objCharacter.GetLifestylesAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false))
            {
                lstLifestyles.Add(new
                {
                    id = objLifestyle.InternalId,
                    name = await objLifestyle.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                });
            }

            dicReturn["lifestyles"] = lstLifestyles;
            List<object> lstContacts = new List<object>();
            foreach (Contact objContact in await (await objCharacter.GetContactsAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false))
            {
                lstContacts.Add(new
                {
                    id = objContact.InternalId,
                    name = await objContact.GetNameAsync(token).ConfigureAwait(false),
                    type = (await objContact.GetEntityTypeAsync(token).ConfigureAwait(false)).ToString(),
                    connection = await objContact.GetConnectionAsync(token).ConfigureAwait(false),
                    loyalty = await objContact.GetLoyaltyAsync(token).ConfigureAwait(false),
                    free = await objContact.GetFreeAsync(token).ConfigureAwait(false)
                });
            }

            dicReturn["contacts"] = lstContacts;
            return dicReturn;
        }

        private static async Task<List<object>> DescribeAttributesAsync(IEnumerable<CharacterAttrib> lstAttributes, CancellationToken token)
        {
            List<object> lstReturn = new List<object>();
            foreach (CharacterAttrib objAttribute in lstAttributes)
            {
                lstReturn.Add(new
                {
                    name = objAttribute.Abbrev,
                    @base = await objAttribute.GetBaseAsync(token).ConfigureAwait(false),
                    karma = await objAttribute.GetKarmaAsync(token).ConfigureAwait(false),
                    value = await objAttribute.GetValueAsync(token).ConfigureAwait(false),
                    augmented = await objAttribute.GetTotalValueAsync(token).ConfigureAwait(false),
                    minimum = await objAttribute.GetTotalMinimumAsync(token).ConfigureAwait(false),
                    maximum = await objAttribute.GetTotalMaximumAsync(token).ConfigureAwait(false),
                    augmentedMaximum = await objAttribute.GetTotalAugmentedMaximumAsync(token).ConfigureAwait(false),
                    maxBase = await objAttribute.GetPriorityMaximumAsync(token).ConfigureAwait(false),
                    maxKarma = await objAttribute.GetKarmaMaximumAsync(token).ConfigureAwait(false)
                });
            }

            return lstReturn;
        }

        private static async Task<List<object>> DescribeSkillsAsync(IEnumerable<Skill> lstSkills, CancellationToken token, bool blnIncludeZero = false)
        {
            List<object> lstReturn = new List<object>();
            foreach (Skill objSkill in lstSkills)
            {
                int intRating = await objSkill.GetRatingAsync(token).ConfigureAwait(false);
                if (intRating <= 0 && !blnIncludeZero)
                    continue;
                List<string> lstSpecs = new List<string>();
                foreach (SkillSpecialization objSpec in await (await objSkill.GetSpecializationsAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false))
                    lstSpecs.Add(await objSpec.GetNameAsync(token).ConfigureAwait(false));
                lstReturn.Add(new
                {
                    name = await objSkill.GetCurrentDisplayNameAsync(token).ConfigureAwait(false),
                    attribute = await objSkill.GetAttributeAsync(token).ConfigureAwait(false),
                    @base = await objSkill.GetBaseAsync(token).ConfigureAwait(false),
                    karma = await objSkill.GetKarmaAsync(token).ConfigureAwait(false),
                    rating = intRating,
                    maximum = await objSkill.GetRatingMaximumAsync(token).ConfigureAwait(false),
                    pool = await objSkill.GetPoolAsync(token).ConfigureAwait(false),
                    specializations = lstSpecs,
                    nativeLanguage = blnIncludeZero && await objSkill.GetIsNativeLanguageAsync(token).ConfigureAwait(false)
                });
            }

            return lstReturn;
        }

        private static async Task<List<object>> DescribeNamedAsync<T>(IAsyncEnumerable<T> lstItems, CancellationToken token) where T : IHasName, IHasInternalId
        {
            List<object> lstReturn = new List<object>();
            foreach (T objItem in await lstItems.ToListAsync(token).ConfigureAwait(false))
            {
                lstReturn.Add(new
                {
                    id = objItem.InternalId,
                    name = await objItem.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                });
            }

            return lstReturn;
        }

        private static async Task<object> ValidateCharacterAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            List<string> lstErrors = new List<string>();
            List<string> lstWarnings = new List<string>();
            bool blnValid = await frmCreate.CollectValidityIssuesAsync(lstErrors, lstWarnings, token).ConfigureAwait(false);
            return new { valid = blnValid, errors = lstErrors, warnings = lstWarnings };
        }

        private static async Task<object> ExportCharacterAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            XmlDocument objExport = await frmCharacter.CharacterObject
                                                      .GenerateExportXml(GlobalSettings.InvariantCultureInfo, GlobalSettings.DefaultLanguage, token)
                                                      .ConfigureAwait(false);
            XmlNode objRoot = objExport.SelectSingleNode("/characters/character") ?? objExport.DocumentElement;
            if (objRoot == null)
                throw new AgentToolException("Chummer produced an empty export.");
            string strSection = objArgs.Value<string>("section");
            if (string.IsNullOrWhiteSpace(strSection))
            {
                return new
                {
                    hint = "Pass `section` with one of these names (sizes are characters of JSON, roughly).",
                    sections = objRoot.ChildNodes.OfType<XmlElement>()
                                      .GroupBy(x => x.Name)
                                      .ToDictionary(x => x.Key, x => x.Sum(y => y.OuterXml.Length))
                };
            }

            if (strSection == "*")
                return JsonConvert.SerializeXmlNode(objRoot, Newtonsoft.Json.Formatting.Indented, true);
            List<XmlElement> lstMatches = objRoot.ChildNodes.OfType<XmlElement>()
                                                 .Where(x => string.Equals(x.Name, strSection, StringComparison.OrdinalIgnoreCase))
                                                 .ToList();
            if (lstMatches.Count == 0)
                throw new AgentToolException("No export section named '" + strSection + "'. Call export_character without `section` to list them.");
            return lstMatches.Count == 1
                ? JsonConvert.SerializeXmlNode(lstMatches[0], Newtonsoft.Json.Formatting.Indented)
                : "[" + string.Join(",", lstMatches.Select(x => JsonConvert.SerializeXmlNode(x, Newtonsoft.Json.Formatting.Indented))) + "]";
        }

        #endregion
    }
}
