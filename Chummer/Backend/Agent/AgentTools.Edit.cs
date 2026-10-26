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
using System.Xml;
using Chummer.Backend.Attributes;
using Chummer.Backend.Enums;
using Chummer.Backend.Skills;
using Newtonsoft.Json.Linq;

namespace Chummer.Agent
{
    public static partial class AgentTools
    {
        private const string ForcedValueDescription =
            "Pre-answer the choice this item asks for (e.g. which skill or attribute it applies to), so no dialog appears. "
            + "If omitted and the item needs a choice, Chummer shows its selection dialog to the user and this call waits for them.";

        private static IEnumerable<AgentTool> CreateEditTools()
        {
            yield return new AgentTool("set_attribute",
                "Set an attribute's points during creation. `base` is attribute points from the priority table (priority builds only); `karma` is karma-bought "
                + "ranks on top. Limits come from the metatype and are enforced; get_character_summary shows maxBase/maxKarma.",
                AgentTool.Schema(("name!", "string", "Attribute abbreviation: BOD, AGI, REA, STR, CHA, INT, LOG, WIL, EDG, MAG, RES, DEP."),
                                 ("base", "integer", "New number of priority points in this attribute."),
                                 ("karma", "integer", "New number of karma-bought ranks."),
                                 ("character", "string", CharacterArgDescription)),
                SetAttributeAsync);
            yield return new AgentTool("set_skill",
                "Set an active skill's ranks during creation: `base` from skill points, `karma` bought with karma. Rating = free + base + karma and may not exceed the maximum.",
                AgentTool.Schema(("name!", "string", "Skill name, e.g. 'Pistols', 'Sneaking'."),
                                 ("base", "integer", "Ranks paid with skill points."),
                                 ("karma", "integer", "Ranks paid with karma."),
                                 ("character", "string", CharacterArgDescription)),
                SetSkillAsync);
            yield return new AgentTool("set_skill_group",
                "Set a skill group's ranks during creation (`base` from skill group points, `karma` with karma).",
                AgentTool.Schema(("name!", "string", "Skill group name, e.g. 'Firearms', 'Stealth'."),
                                 ("base", "integer", "Ranks paid with skill group points."),
                                 ("karma", "integer", "Ranks paid with karma."),
                                 ("character", "string", CharacterArgDescription)),
                SetSkillGroupAsync);
            yield return new AgentTool("add_knowledge_skill",
                "Add a knowledge or language skill, or set the ranks of one the character already has.",
                AgentTool.Schema(("name!", "string", "Skill name, e.g. 'English', 'Corporate Security', 'Seattle Gangs'."),
                                 ("base", "integer", "Ranks paid with knowledge skill points."),
                                 ("karma", "integer", "Ranks paid with karma."),
                                 ("native_language", "boolean", "Make this a native language (uses one of the character's native language slots)."),
                                 ("character", "string", CharacterArgDescription)),
                AddKnowledgeSkillAsync);
            yield return new AgentTool("add_quality",
                "Add a quality from search_catalog(kind='quality'). Checks requirements and the positive/negative quality karma limits like the Add Quality button.",
                AgentTool.Schema(("id!", "string", "Quality id from search_catalog, or its exact English name."),
                                 ("levels", "integer", "Levels to add for qualities with levels. Default 1."),
                                 ("forced_value", "string", ForcedValueDescription),
                                 ("character", "string", CharacterArgDescription)),
                AddQualityAsync);
            yield return new AgentTool("remove_quality",
                "Remove a quality the character has (by the id from get_character_summary, or its name). Removes one level of a leveled quality.",
                AgentTool.Schema(("id!", "string", "Quality id from get_character_summary, or its displayed name."),
                                 ("character", "string", CharacterArgDescription)),
                RemoveQualityAsync);
            yield return new AgentTool("add_spell",
                "Add a spell, ritual or preparation from search_catalog(kind='spell'). Enforces requirements and the creation limit of MAG x 2 per type.",
                AgentTool.Schema(("id!", "string", "Spell id from search_catalog, or its exact English name."),
                                 ("alchemical", "boolean", "Learn it as an alchemical preparation."),
                                 ("limited", "boolean", "Take it as a Limited spell."),
                                 ("extended", "boolean", "Take it as an Extended spell (where the spell allows)."),
                                 ("forced_value", "string", ForcedValueDescription),
                                 ("character", "string", CharacterArgDescription)),
                AddSpellAsync);
            yield return new AgentTool("add_power",
                "Add an adept power from search_catalog(kind='power'), optionally at a rating.",
                AgentTool.Schema(("id!", "string", "Power id from search_catalog, or its exact English name."),
                                 ("rating", "integer", "Rating for powers with levels. Default 1."),
                                 ("character", "string", CharacterArgDescription)),
                AddPowerAsync);
            yield return new AgentTool("set_power_rating",
                "Change the rating of an adept power the character has.",
                AgentTool.Schema(("id!", "string", "Power id from get_character_summary, or its displayed name."),
                                 ("rating!", "integer", "New rating."),
                                 ("character", "string", CharacterArgDescription)),
                SetPowerRatingAsync);
            yield return new AgentTool("add_contact",
                "Add a contact paid for with contact points (CHA x 3 by default) and then karma.",
                AgentTool.Schema(("name", "string", "Contact's name or description, e.g. 'Fixer - Mama Rosa'."),
                                 ("connection!", "integer", "Connection rating 1-12."),
                                 ("loyalty!", "integer", "Loyalty rating 1-6."),
                                 ("character", "string", CharacterArgDescription)),
                AddContactAsync);
            yield return new AgentTool("remove_item",
                "Remove something the character has, by the id (or name) shown in get_character_summary: a spell, adept power, contact, weapon, armor, "
                + "gear, cyberware/bioware, vehicle or lifestyle. Bought equipment is refunded. Use remove_quality for qualities.",
                AgentTool.Schema(("id!", "string", "Id from get_character_summary."),
                                 ("character", "string", CharacterArgDescription)),
                RemoveItemAsync);
        }

        #region Helpers

        private static int? OptionalInt(JObject objArgs, string strName)
        {
            JToken objToken = objArgs[strName];
            if (objToken == null || objToken.Type == JTokenType.Null)
                return null;
            if (objToken.Type == JTokenType.Integer)
                return objToken.Value<int>();
            if (int.TryParse(objToken.ToString(), out int intValue))
                return intValue;
            throw new AgentToolException("`" + strName + "` must be a whole number.");
        }

        private static string RequiredString(JObject objArgs, string strName)
        {
            string strValue = objArgs.Value<string>(strName)?.Trim();
            if (string.IsNullOrEmpty(strValue))
                throw new AgentToolException("`" + strName + "` is required.");
            return strValue;
        }

        /// <summary>
        /// Mark the character as changed, let the editor refresh, and report the headline numbers so the agent sees the effect of its edit.
        /// </summary>
        private static async Task<object> FinishEditAsync(CharacterCreate frmCreate, string strResult, CancellationToken token)
        {
            await frmCreate.MakeDirtyWithCharacterUpdate(token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            SkillsSection objSkills = await objCharacter.GetSkillsSectionAsync(token).ConfigureAwait(false);
            (decimal decNuyen, decimal _) = await objCharacter.CalculateNuyenCreateModeAsync(token).ConfigureAwait(false);
            int intKarma = await frmCreate.CalculateKarmaRemainingAsync(null, token).ConfigureAwait(false);
            int intSkillPoints = await objSkills.GetSkillPointsAsync(token).ConfigureAwait(false);
            int intSkillGroupPoints = await objSkills.GetSkillGroupPointsAsync(token).ConfigureAwait(false);
            int intKnowledgePoints = await objSkills.GetKnowledgeSkillPointsRemainAsync(token).ConfigureAwait(false);
            decimal decEssence = await objCharacter.EssenceAsync(token: token).ConfigureAwait(false);
            // Chummer lets the editor go over budget (it only complains when finishing creation), so say so loudly
            List<string> lstOverspent = new List<string>();
            if (intKarma < 0)
                lstOverspent.Add("karma by " + (-intKarma).ToString(GlobalSettings.InvariantCultureInfo));
            if (intSkillPoints < 0)
                lstOverspent.Add("skill points by " + (-intSkillPoints).ToString(GlobalSettings.InvariantCultureInfo));
            if (intSkillGroupPoints < 0)
                lstOverspent.Add("skill group points by " + (-intSkillGroupPoints).ToString(GlobalSettings.InvariantCultureInfo));
            if (intKnowledgePoints < 0)
                lstOverspent.Add("knowledge skill points by " + (-intKnowledgePoints).ToString(GlobalSettings.InvariantCultureInfo));
            if (decNuyen < 0)
                lstOverspent.Add("nuyen by " + (-decNuyen).ToString(GlobalSettings.InvariantCultureInfo));
            return new
            {
                result = strResult,
                warning = lstOverspent.Count > 0
                    ? "Over budget: " + string.Join(", ", lstOverspent) + ". The character can't finish creation until this is fixed."
                    : null,
                karmaRemaining = intKarma,
                skillPointsRemaining = intSkillPoints,
                skillGroupPointsRemaining = intSkillGroupPoints,
                knowledgePointsRemaining = intKnowledgePoints,
                nuyenRemaining = decNuyen,
                essence = decEssence
            };
        }

        private static async Task<XmlNode> GetDataNodeAsync(Character objCharacter, string strFile, string strPath, string strId, CancellationToken token)
        {
            XmlDocument objDocument = await objCharacter.LoadDataAsync(strFile, token: token).ConfigureAwait(false);
            return objDocument.TryGetNodeByNameOrId(strPath, strId)
                   ?? throw new AgentToolException("Nothing in " + strFile + " has id or name '" + strId + "'. Use search_catalog to find it.");
        }

        private static async Task EnsureRequirementsMetAsync(XmlNode objNode, Character objCharacter, CancellationToken token)
        {
            if (await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false))
                return;
#pragma warning disable CS0618 // The XmlNode wrapper is what the editor's own add buttons use
            if (!await objNode.RequirementsMetAsync(objCharacter, token: token).ConfigureAwait(false))
#pragma warning restore CS0618
            {
                throw new AgentToolException("The character does not meet the requirements for '" + objNode["name"]?.InnerText
                                             + "' (or it is forbidden by something they already have). Use get_catalog_item to see its required/forbidden conditions.");
            }
        }

        /// <summary>
        /// Find an item the character has by internal id, falling back to its display name.
        /// </summary>
        private static async Task<T> FindOwnedAsync<T>(IEnumerable<T> lstItems, string strId, CancellationToken token) where T : IHasName, IHasInternalId
        {
            List<T> lstList = lstItems.ToList();
            T objMatch = lstList.FirstOrDefault(x => string.Equals(x.InternalId, strId, StringComparison.OrdinalIgnoreCase));
            if (objMatch != null)
                return objMatch;
            foreach (T objLoop in lstList)
            {
                if (string.Equals(await objLoop.GetCurrentDisplayNameAsync(token).ConfigureAwait(false), strId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(objLoop.Name, strId, StringComparison.OrdinalIgnoreCase))
                    return objLoop;
            }

            return default;
        }

        #endregion

        #region Attributes and skills

        private static async Task<object> SetAttributeAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strName = RequiredString(objArgs, "name").ToUpperInvariant();
            int? intBase = OptionalInt(objArgs, "base");
            int? intKarma = OptionalInt(objArgs, "karma");
            if (intBase == null && intKarma == null)
                throw new AgentToolException("Pass `base` and/or `karma`.");
            CharacterAttrib objAttribute = await objCharacter.GetAttributeAsync(strName, token: token).ConfigureAwait(false)
                                           ?? throw new AgentToolException("Unknown attribute '" + strName + "'.");
            bool blnIgnoreRules = await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false);
            int intOldBase = await objAttribute.GetBaseAsync(token).ConfigureAwait(false);
            if (intBase != null)
            {
                int intMaxBase = await objAttribute.GetPriorityMaximumAsync(token).ConfigureAwait(false);
                if (intBase < 0 || (!blnIgnoreRules && intBase > intMaxBase))
                    throw new AgentToolException(strName + " base must be between 0 and " + intMaxBase.ToString(GlobalSettings.InvariantCultureInfo)
                                                 + (await objCharacter.GetEffectiveBuildMethodUsesPriorityTablesAsync(token).ConfigureAwait(false)
                                                     ? "."
                                                     : " (this is not a priority build, so attributes are bought with karma)."));
                await objAttribute.SetBaseAsync(intBase.Value, token).ConfigureAwait(false);
            }

            if (intKarma != null)
            {
                int intMaxKarma = await objAttribute.GetKarmaMaximumAsync(token).ConfigureAwait(false);
                if (intKarma < 0 || (!blnIgnoreRules && intKarma > intMaxKarma))
                {
                    if (intBase != null)
                        await objAttribute.SetBaseAsync(intOldBase, token).ConfigureAwait(false);
                    throw new AgentToolException(strName + " karma must be between 0 and " + intMaxKarma.ToString(GlobalSettings.InvariantCultureInfo)
                                                 + " with that base (the metatype maximum is "
                                                 + (await objAttribute.GetTotalMaximumAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo) + ").");
                }

                await objAttribute.SetKarmaAsync(intKarma.Value, token).ConfigureAwait(false);
            }

            return await FinishEditAsync(frmCreate,
                                         strName + " is now " + (await objAttribute.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo)
                                         + " (base " + (await objAttribute.GetBaseAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo)
                                         + ", karma " + (await objAttribute.GetKarmaAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo) + ").",
                                         token).ConfigureAwait(false);
        }

        private static async Task<Skill> FindSkillAsync(IEnumerable<Skill> lstSkills, string strName, CancellationToken token)
        {
            foreach (Skill objSkill in lstSkills)
            {
                if (string.Equals(await objSkill.GetNameAsync(token).ConfigureAwait(false), strName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(await objSkill.GetCurrentDisplayNameAsync(token).ConfigureAwait(false), strName, StringComparison.OrdinalIgnoreCase))
                    return objSkill;
            }

            return null;
        }

        /// <summary>
        /// Apply base/karma ranks to a skill with the same limits the skill editor enforces.
        /// </summary>
        private static async Task SetSkillRanksAsync(Skill objSkill, int? intBase, int? intKarma, bool blnIgnoreRules, CancellationToken token)
        {
            string strName = await objSkill.GetCurrentDisplayNameAsync(token).ConfigureAwait(false);
            if (!blnIgnoreRules)
            {
                if (intBase != null && !await objSkill.GetBaseUnlockedAsync(token).ConfigureAwait(false)
                                    && intBase != await objSkill.GetBaseAsync(token).ConfigureAwait(false))
                    throw new AgentToolException(strName + " can't take skill points right now (no skill points in this build, or it is locked by its skill group).");
                if (intKarma != null && !await objSkill.GetKarmaUnlockedAsync(token).ConfigureAwait(false)
                                     && intKarma != await objSkill.GetKarmaAsync(token).ConfigureAwait(false))
                    throw new AgentToolException(strName + " can't take karma ranks right now (it is probably locked by its skill group).");
                int intFreeBase = await objSkill.GetFreeBaseAsync(token).ConfigureAwait(false);
                int intFreeKarma = await objSkill.GetFreeKarmaAsync(token).ConfigureAwait(false);
                int intNewBase = intBase ?? await objSkill.GetBaseAsync(token).ConfigureAwait(false);
                int intNewKarma = intKarma ?? await objSkill.GetKarmaAsync(token).ConfigureAwait(false);
                int intMaximum = await objSkill.GetRatingMaximumAsync(token).ConfigureAwait(false);
                if (intNewBase < intFreeBase || intNewKarma < intFreeKarma)
                    throw new AgentToolException(strName + " has " + intFreeBase.ToString(GlobalSettings.InvariantCultureInfo) + " free base and "
                                                 + intFreeKarma.ToString(GlobalSettings.InvariantCultureInfo) + " free karma ranks; values can't go below those.");
                if (intNewBase + intNewKarma > intMaximum)
                    throw new AgentToolException(strName + " can't go above rating " + intMaximum.ToString(GlobalSettings.InvariantCultureInfo)
                                                 + " during creation (base " + intNewBase.ToString(GlobalSettings.InvariantCultureInfo)
                                                 + " + karma " + intNewKarma.ToString(GlobalSettings.InvariantCultureInfo) + ").");
            }

            if (intBase != null)
                await objSkill.SetBaseAsync(intBase.Value, token).ConfigureAwait(false);
            if (intKarma != null)
                await objSkill.SetKarmaAsync(intKarma.Value, token).ConfigureAwait(false);
        }

        private static async Task<object> SetSkillAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strName = RequiredString(objArgs, "name");
            int? intBase = OptionalInt(objArgs, "base");
            int? intKarma = OptionalInt(objArgs, "karma");
            if (intBase == null && intKarma == null)
                throw new AgentToolException("Pass `base` and/or `karma`.");
            SkillsSection objSkillsSection = await objCharacter.GetSkillsSectionAsync(token).ConfigureAwait(false);
            Skill objSkill = await objSkillsSection.GetActiveSkillAsync(strName, token).ConfigureAwait(false)
                             ?? await FindSkillAsync(await objSkillsSection.Skills.ToListAsync(token).ConfigureAwait(false), strName, token).ConfigureAwait(false)
                             ?? throw new AgentToolException("The character has no active skill named '" + strName + "'. Exotic skills must be added in Chummer first.");
            await SetSkillRanksAsync(objSkill, intBase, intKarma, await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false), token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate,
                                         await objSkill.GetCurrentDisplayNameAsync(token).ConfigureAwait(false) + " is now rating "
                                         + (await objSkill.GetRatingAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo)
                                         + " (dice pool " + (await objSkill.GetPoolAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo) + ").",
                                         token).ConfigureAwait(false);
        }

        private static async Task<object> SetSkillGroupAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strName = RequiredString(objArgs, "name");
            int? intBase = OptionalInt(objArgs, "base");
            int? intKarma = OptionalInt(objArgs, "karma");
            if (intBase == null && intKarma == null)
                throw new AgentToolException("Pass `base` and/or `karma`.");
            SkillGroup objGroup = null;
            foreach (SkillGroup objLoop in await (await objCharacter.GetSkillsSectionAsync(token).ConfigureAwait(false)).SkillGroups.ToListAsync(token).ConfigureAwait(false))
            {
                if (string.Equals(await objLoop.GetNameAsync(token).ConfigureAwait(false), strName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(await objLoop.GetCurrentDisplayNameAsync(token).ConfigureAwait(false), strName, StringComparison.OrdinalIgnoreCase))
                {
                    objGroup = objLoop;
                    break;
                }
            }

            if (objGroup == null)
                throw new AgentToolException("No skill group named '" + strName + "'.");
            if (!await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false))
            {
                if (await objGroup.GetIsDisabledAsync(token).ConfigureAwait(false))
                    throw new AgentToolException("The " + strName + " group is broken (one of its skills has its own ranks or a specialization), so it can't be raised as a group.");
                int intNewBase = intBase ?? await objGroup.GetBaseAsync(token).ConfigureAwait(false);
                int intNewKarma = intKarma ?? await objGroup.GetKarmaAsync(token).ConfigureAwait(false);
                int intFreeBase = await objGroup.GetFreeBaseAsync(token).ConfigureAwait(false);
                int intMaximum = await objGroup.GetRatingMaximumAsync(token).ConfigureAwait(false);
                if (intNewBase < intFreeBase || intNewKarma < 0)
                    throw new AgentToolException("Ranks can't go below the free ranks (" + intFreeBase.ToString(GlobalSettings.InvariantCultureInfo) + ") or zero.");
                if (intNewBase + intNewKarma > intMaximum)
                    throw new AgentToolException(strName + " can't go above rating " + intMaximum.ToString(GlobalSettings.InvariantCultureInfo) + " during creation.");
            }

            if (intBase != null)
                await objGroup.SetBaseAsync(intBase.Value, token).ConfigureAwait(false);
            if (intKarma != null)
                await objGroup.SetKarmaAsync(intKarma.Value, token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate,
                                         await objGroup.GetCurrentDisplayNameAsync(token).ConfigureAwait(false) + " group is now rating "
                                         + (await objGroup.GetRatingAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo) + ".",
                                         token).ConfigureAwait(false);
        }

        private static async Task<object> AddKnowledgeSkillAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strName = RequiredString(objArgs, "name");
            SkillsSection objSkillsSection = await objCharacter.GetSkillsSectionAsync(token).ConfigureAwait(false);
            List<Skill> lstExisting = (await objSkillsSection.KnowledgeSkills.ToListAsync(token).ConfigureAwait(false)).Cast<Skill>().ToList();
            KnowledgeSkill objSkill = await FindSkillAsync(lstExisting, strName, token).ConfigureAwait(false) as KnowledgeSkill;
            bool blnNew = objSkill == null;
            if (blnNew)
            {
                objSkill = new KnowledgeSkill(objCharacter, false);
                try
                {
                    await objSkill.SetDefaultAttributeAsync("LOG", token).ConfigureAwait(false);
                    await objSkill.SetWritableNameAsync(strName, token).ConfigureAwait(false);
                    if (objArgs.Value<bool?>("native_language") == true)
                    {
                        if (!await objSkillsSection.GetHasAvailableNativeLanguageSlotsAsync(token).ConfigureAwait(false))
                            throw new AgentToolException("The character has no native language slots left.");
                        if (!await objSkill.GetIsLanguageAsync(token).ConfigureAwait(false))
                            await objSkill.SetTypeAsync("Language", token).ConfigureAwait(false);
                        await objSkill.SetIsNativeLanguageAsync(true, token).ConfigureAwait(false);
                    }

                    await objSkillsSection.KnowledgeSkills.AddAsync(objSkill, token).ConfigureAwait(false);
                }
                catch
                {
                    await objSkill.RemoveAsync(CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
            }

            int? intBase = OptionalInt(objArgs, "base");
            int? intKarma = OptionalInt(objArgs, "karma");
            if (intBase != null || intKarma != null)
                await SetSkillRanksAsync(objSkill, intBase, intKarma, await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false), token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate,
                                         (blnNew ? "Added " : "Updated ") + await objSkill.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                         + (await objSkill.GetIsNativeLanguageAsync(token).ConfigureAwait(false) ? " (native language)" : " at rating "
                                             + (await objSkill.GetRatingAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo)) + ".",
                                         token).ConfigureAwait(false);
        }

        #endregion

        #region Qualities, magic, contacts

        private static async Task<object> AddQualityAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strId = RequiredString(objArgs, "id");
            XmlNode objXmlQuality = await GetDataNodeAsync(objCharacter, "qualities.xml", "/chummer/qualities/quality", strId, token).ConfigureAwait(false);
            await EnsureRequirementsMetAsync(objXmlQuality, objCharacter, token).ConfigureAwait(false);
            string strSourceId = objXmlQuality["id"]?.InnerText ?? strId;
            int intLevels = Math.Max(1, OptionalInt(objArgs, "levels") ?? 1);
            // The editor's dialog asks for the total level wanted and subtracts what the character already has; ask for existing + new.
            int intDummy = 0;
            if (objXmlQuality["nolevels"] == null && objXmlQuality.TryGetInt32FieldQuickly("limit", ref intDummy))
            {
                intLevels += await (await objCharacter.GetQualitiesAsync(token).ConfigureAwait(false))
                                   .CountAsync(async (x, t) =>
                                                   (await x.GetSourceIDStringAsync(t).ConfigureAwait(false))
                                                   .Equals(strSourceId, StringComparison.OrdinalIgnoreCase)
                                                   && string.IsNullOrEmpty(await x.GetSourceNameAsync(t).ConfigureAwait(false)), token)
                                   .ConfigureAwait(false);
            }

            (int intAdded, string strRefusal) = await frmCreate.AddQualityFromNodeAsync(objXmlQuality, strSourceId, intLevels, false,
                                                                                        objArgs.Value<string>("forced_value") ?? string.Empty,
                                                                                        false, token).ConfigureAwait(false);
            if (intAdded == 0)
                throw new AgentToolException(!string.IsNullOrEmpty(strRefusal)
                                                 ? strRefusal
                                                 : "The quality was not added (its choice dialog was cancelled, or it could not be created for this character).");
            return await FinishEditAsync(frmCreate,
                                         "Added " + objXmlQuality["name"]?.InnerText + (intAdded > 1 ? " x" + intAdded.ToString(GlobalSettings.InvariantCultureInfo) : string.Empty)
                                         + (string.IsNullOrEmpty(strRefusal) ? "." : ". Stopped early: " + strRefusal),
                                         token).ConfigureAwait(false);
        }

        private static async Task<object> RemoveQualityAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strId = RequiredString(objArgs, "id");
            Quality objQuality = await FindOwnedAsync(await (await objCharacter.GetQualitiesAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false),
                                                      strId, token).ConfigureAwait(false)
                                 ?? throw new AgentToolException("The character has no quality '" + strId + "'.");
            switch (await objQuality.GetOriginSourceAsync(token).ConfigureAwait(false))
            {
                case QualitySource.Metatype:
                case QualitySource.Heritage:
                    throw new AgentToolException("That quality comes from the metatype and can't be removed.");
                case QualitySource.Improvement:
                case QualitySource.QualityLevelImprovement:
                    throw new AgentToolException("That quality is granted by something else the character has; remove that instead.");
            }

            string strName = await objQuality.GetCurrentDisplayNameAsync(token).ConfigureAwait(false);
            if (!await frmCreate.RemoveQuality(objQuality, false, false, token).ConfigureAwait(false))
                throw new AgentToolException("Chummer did not remove " + strName + ".");
            return await FinishEditAsync(frmCreate, "Removed " + strName + ".", token).ConfigureAwait(false);
        }

        private static async Task<object> AddSpellAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            XmlNode objXmlSpell = await GetDataNodeAsync(objCharacter, "spells.xml", "/chummer/spells/spell", RequiredString(objArgs, "id"), token).ConfigureAwait(false);
            bool blnAlchemical = objArgs.Value<bool?>("alchemical") == true;
            if (!await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false))
            {
                // Same limit as the spell selection dialog: at creation, MAG x 2 spells, rituals and preparations each
                string strCategory = objXmlSpell["category"]?.InnerText;
                int intLimit = await (await objCharacter.GetAttributeAsync("MAG", token: token).ConfigureAwait(false)).GetTotalValueAsync(token).ConfigureAwait(false) * 2;
                int intCount = await (await objCharacter.GetSpellsAsync(token).ConfigureAwait(false))
                                     .CountAsync(x => blnAlchemical
                                                     ? x.Alchemical
                                                     : !x.Alchemical && (strCategory == "Rituals") == (x.Category == "Rituals"), token)
                                     .ConfigureAwait(false);
                if (intCount >= intLimit)
                    throw new AgentToolException("The character already knows the maximum of " + intLimit.ToString(GlobalSettings.InvariantCultureInfo)
                                                 + (blnAlchemical ? " preparations" : strCategory == "Rituals" ? " rituals" : " spells") + " (MAG x 2) allowed at creation.");
                await EnsureRequirementsMetAsync(objXmlSpell, objCharacter, token).ConfigureAwait(false);
            }

            Spell objSpell = new Spell(objCharacter);
            try
            {
                await objSpell.CreateAsync(objXmlSpell, objArgs.Value<string>("forced_value") ?? string.Empty,
                                           objArgs.Value<bool?>("limited") == true, objArgs.Value<bool?>("extended") == true,
                                           blnAlchemical, token: token).ConfigureAwait(false);
                if (objSpell.InternalId.IsEmptyGuid())
                {
                    await objSpell.RemoveAsync(false, CancellationToken.None).ConfigureAwait(false);
                    throw new AgentToolException("The spell was not added (its choice dialog was cancelled).");
                }

                await objCharacter.Spells.AddAsync(objSpell, token).ConfigureAwait(false);
            }
            catch
            {
                await objSpell.RemoveAsync(false, CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            return await FinishEditAsync(frmCreate, "Added " + await objSpell.GetCurrentDisplayNameAsync(token).ConfigureAwait(false) + ".", token).ConfigureAwait(false);
        }

        private static async Task<object> AddPowerAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            XmlNode objXmlPower = await GetDataNodeAsync(objCharacter, "powers.xml", "/chummer/powers/power", RequiredString(objArgs, "id"), token).ConfigureAwait(false);
            await EnsureRequirementsMetAsync(objXmlPower, objCharacter, token).ConfigureAwait(false);
            int intRating = Math.Max(1, OptionalInt(objArgs, "rating") ?? 1);
            Power objPower = new Power(objCharacter);
            try
            {
                if (!await objPower.CreateAsync(objXmlPower, token: token).ConfigureAwait(false))
                {
                    await objPower.DeletePowerAsync(token).ConfigureAwait(false);
                    throw new AgentToolException("The power was not added (its choice dialog was cancelled, or it could not be created for this character).");
                }

                await objCharacter.Powers.AddAsync(objPower, token).ConfigureAwait(false);
            }
            catch (AgentToolException)
            {
                throw;
            }
            catch
            {
                await objPower.DeletePowerAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            string strNote = string.Empty;
            if (intRating > 1)
                strNote = await ApplyPowerRatingAsync(objPower, intRating, token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate, "Added " + await objPower.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " at rating " + (await objPower.GetRatingAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo)
                                                    + "." + strNote, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Set a power's rating within its level limits. Returns a note if the rating had to be capped.
        /// </summary>
        private static async Task<string> ApplyPowerRatingAsync(Power objPower, int intRating, CancellationToken token)
        {
            if (!await objPower.GetLevelsEnabledAsync(token).ConfigureAwait(false))
                return intRating > 1 ? " This power has no levels." : string.Empty;
            int intMaximum = await objPower.GetTotalMaximumLevelsAsync(token).ConfigureAwait(false);
            string strNote = string.Empty;
            if (intRating > intMaximum)
            {
                strNote = " Rating capped at the maximum of " + intMaximum.ToString(GlobalSettings.InvariantCultureInfo) + ".";
                intRating = intMaximum;
            }

            await objPower.SetRatingAsync(Math.Max(1, intRating), token).ConfigureAwait(false);
            return strNote;
        }

        private static async Task<object> SetPowerRatingAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strId = RequiredString(objArgs, "id");
            Power objPower = await FindOwnedAsync(await (await objCharacter.GetPowersAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false), strId, token).ConfigureAwait(false)
                             ?? throw new AgentToolException("The character has no power '" + strId + "'.");
            int intRating = OptionalInt(objArgs, "rating") ?? throw new AgentToolException("`rating` is required.");
            string strNote = await ApplyPowerRatingAsync(objPower, intRating, token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate, await objPower.GetCurrentDisplayNameAsync(token).ConfigureAwait(false) + " is now rating "
                                                    + (await objPower.GetRatingAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo) + "." + strNote,
                                         token).ConfigureAwait(false);
        }

        private static async Task<object> AddContactAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            int intConnection = OptionalInt(objArgs, "connection") ?? throw new AgentToolException("`connection` is required.");
            int intLoyalty = OptionalInt(objArgs, "loyalty") ?? throw new AgentToolException("`loyalty` is required.");
            if (intConnection < 1 || intConnection > 12 || intLoyalty < 1 || intLoyalty > 6)
                throw new AgentToolException("Connection must be 1-12 and loyalty 1-6.");
            Contact objContact = new Contact(objCharacter);
            try
            {
                await objContact.SetEntityTypeAsync(ContactType.Contact, token).ConfigureAwait(false);
                string strName = objArgs.Value<string>("name");
                if (!string.IsNullOrWhiteSpace(strName))
                    await objContact.SetNameAsync(strName.Trim(), token).ConfigureAwait(false);
                await objContact.SetConnectionAsync(intConnection, token).ConfigureAwait(false);
                await objContact.SetLoyaltyAsync(intLoyalty, token).ConfigureAwait(false);
                await objCharacter.Contacts.AddAsync(objContact, token: token).ConfigureAwait(false);
            }
            catch
            {
                await objCharacter.Contacts.RemoveAsync(objContact, CancellationToken.None).ConfigureAwait(false);
                await objContact.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            return await FinishEditAsync(frmCreate,
                                         "Added contact " + (await objContact.GetNameAsync(token).ConfigureAwait(false)) + " (connection "
                                         + (await objContact.GetConnectionAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo) + ", loyalty "
                                         + (await objContact.GetLoyaltyAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo) + ").",
                                         token).ConfigureAwait(false);
        }

        private static async Task<object> RemoveItemAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strId = RequiredString(objArgs, "id");
            Spell objSpell = await FindOwnedAsync(await (await objCharacter.GetSpellsAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false), strId, token).ConfigureAwait(false);
            if (objSpell != null)
            {
                if (objSpell.FreeBonus || objSpell.Grade != 0)
                    throw new AgentToolException("That spell was granted by something else (or by initiation) and can't be removed here.");
                string strName = await objSpell.GetCurrentDisplayNameAsync(token).ConfigureAwait(false);
                await objSpell.RemoveAsync(false, token).ConfigureAwait(false);
                return await FinishEditAsync(frmCreate, "Removed spell " + strName + ".", token).ConfigureAwait(false);
            }

            Power objPower = await FindOwnedAsync(await (await objCharacter.GetPowersAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false), strId, token).ConfigureAwait(false);
            if (objPower != null)
            {
                string strName = await objPower.GetCurrentDisplayNameAsync(token).ConfigureAwait(false);
                await objPower.DeletePowerAsync(token).ConfigureAwait(false);
                return await FinishEditAsync(frmCreate, "Removed power " + strName + ".", token).ConfigureAwait(false);
            }

            Contact objContact = await FindOwnedAsync(await (await objCharacter.GetContactsAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false), strId, token).ConfigureAwait(false);
            if (objContact != null)
            {
                if (await objContact.GetReadOnlyAsync(token).ConfigureAwait(false))
                    throw new AgentToolException("That contact was granted by something else and can't be removed here.");
                string strName = await objContact.GetNameAsync(token).ConfigureAwait(false);
                await objCharacter.Contacts.RemoveAsync(objContact, token).ConfigureAwait(false);
                return await FinishEditAsync(frmCreate, "Removed contact " + strName + ".", token).ConfigureAwait(false);
            }

            ICanRemove objEquipment = await FindOwnedEquipmentAsync(objCharacter, strId, token).ConfigureAwait(false);
            if (objEquipment != null)
            {
                string strName = objEquipment is IHasName objNamed
                    ? await objNamed.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                    : strId;
                if (!await objEquipment.RemoveAsync(false, token).ConfigureAwait(false))
                    throw new AgentToolException("Chummer did not remove " + strName + " (it may come with something else the character has; remove that instead).");
                return await FinishEditAsync(frmCreate, "Removed " + strName + ".", token).ConfigureAwait(false);
            }

            throw new AgentToolException("Nothing the character has matches '" + strId + "'. Use get_character_summary for ids, and remove_quality for qualities.");
        }

        #endregion
    }
}
