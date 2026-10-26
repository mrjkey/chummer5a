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
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Backend.Attributes;
using Chummer.Backend.Skills;
using Newtonsoft.Json.Linq;

namespace Chummer.Agent
{
    public static partial class AgentTools
    {
        private static IEnumerable<AgentTool> CreateCareerTools()
        {
            yield return new AgentTool("finish_creation",
                "Finish character creation, as File > Save as Created does: the character must pass validate_character with no errors; it is saved "
                + "(to `path`, or its own file) and reopened in career mode, where karma and nuyen are tracked in an expense log. This can't be undone "
                + "from here.",
                AgentTool.Schema(("path", "string", "File to save to, if the character has none yet. Relative paths go in Chummer's saves folder."),
                                 ("character", "string", CharacterArgDescription)),
                FinishCreationAsync);
            yield return new AgentTool("log_karma",
                "Career mode: record karma gained (positive amount, e.g. a run's reward) or spent outside the editor (negative), as Karma Gained / "
                + "Karma Spent do. It goes in the expense log; spending can't take karma below 0.",
                AgentTool.Schema(("amount!", "integer", "Karma gained (positive) or spent (negative)."),
                                 ("reason!", "string", "What it was for, e.g. 'Run: the Renraku job'."),
                                 ("date", "string", "When, as yyyy-mm-dd. Default today."),
                                 ("refund", "boolean", "Mark it as a refund."),
                                 ("convert", "boolean", "Pay for it with (or turn it into) nuyen at the settings' karma/nuyen rate."),
                                 ("character", "string", CharacterArgDescription)),
                LogKarmaAsync);
            yield return new AgentTool("log_nuyen",
                "Career mode: record nuyen gained (positive amount, e.g. a run's pay) or spent outside the editor (negative, e.g. bribes, rent paid "
                + "elsewhere), as Nuyen Gained / Nuyen Spent do. It goes in the expense log; spending can't take nuyen below 0.",
                AgentTool.Schema(("amount!", "number", "Nuyen gained (positive) or spent (negative)."),
                                 ("reason!", "string", "What it was for."),
                                 ("date", "string", "When, as yyyy-mm-dd. Default today."),
                                 ("refund", "boolean", "Mark it as a refund."),
                                 ("convert", "boolean", "Pay for it with (or turn it into) karma at the settings' karma/nuyen rate."),
                                 ("character", "string", CharacterArgDescription)),
                LogNuyenAsync);
            yield return new AgentTool("improve_attribute",
                "Career mode: raise an attribute with karma, as its + button does (new rating x 5 karma by default). Refused past the maximum or "
                + "without enough karma.",
                AgentTool.Schema(("name!", "string", "Attribute abbreviation: BOD, AGI, REA, STR, CHA, INT, LOG, WIL, EDG, MAG, RES, DEP."),
                                 ("times", "integer", "How many points to raise it by. Default 1."),
                                 ("character", "string", CharacterArgDescription)),
                ImproveAttributeAsync);
            yield return new AgentTool("improve_skill",
                "Career mode: raise an active skill, knowledge skill or skill group with karma, as its + button does. A new knowledge skill starts at "
                + "0: add it with add_knowledge_skill, then improve it. Refused past the maximum or without enough karma.",
                AgentTool.Schema(("name!", "string", "Skill or skill group name, e.g. 'Pistols', 'Seattle Gangs', 'Firearms'."),
                                 ("group", "boolean", "The name is a skill group. Default false."),
                                 ("times", "integer", "How many ranks to raise it by. Default 1."),
                                 ("character", "string", CharacterArgDescription)),
                ImproveSkillAsync);
            yield return new AgentTool("burn_edge",
                "Career mode: permanently burn a point of Edge (to survive certain death), as the Edge attribute's Burn button does.",
                AgentTool.Schema(("character", "string", CharacterArgDescription)),
                BurnEdgeAsync);
        }

        #region Helpers

        internal static async Task<CharacterCareer> GetCareerFormAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            return frmCharacter as CharacterCareer
                   ?? throw new AgentToolException("This character is still in creation; this tool is for career mode. Use finish_creation when the build is done.");
        }

        /// <summary>
        /// Let the career editor refresh, and report the karma and nuyen left so the agent sees the cost of its edit.
        /// </summary>
        private static async Task<object> FinishCareerEditAsync(CharacterShared frmCharacter, string strResult, CancellationToken token)
        {
            await frmCharacter.MakeDirtyWithCharacterUpdate(token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            return new
            {
                result = strResult,
                karma = await objCharacter.GetKarmaAsync(token).ConfigureAwait(false),
                nuyen = await objCharacter.GetNuyenAsync(token).ConfigureAwait(false)
            };
        }

        /// <summary>
        /// Report an edit as the character's editor needs: points left during creation, karma and nuyen left in career mode.
        /// </summary>
        private static Task<object> FinishAnyEditAsync(CharacterShared frmCharacter, string strResult, CancellationToken token)
        {
            return frmCharacter is CharacterCreate frmCreate
                ? FinishEditAsync(frmCreate, strResult, token)
                : FinishCareerEditAsync(frmCharacter, strResult, token);
        }

        /// <summary>
        /// The purchase dialogs' Markup box, which only matters in career mode, where purchases are paid from the character's nuyen.
        /// </summary>
        private static decimal GetMarkup(JObject objArgs, CharacterShared frmCharacter)
        {
            decimal decMarkup = objArgs.Value<decimal?>("markup") ?? 0;
            if (decMarkup != 0 && !(frmCharacter is CharacterCareer))
                throw new AgentToolException("`markup` only applies in career mode.");
            if (decMarkup <= -100)
                throw new AgentToolException("`markup` must be more than -100.");
            return decMarkup;
        }

        /// <summary>
        /// The message for a purchase core's refusal: its language string, or the fallback when it gave no reason.
        /// </summary>
        private static async Task<string> GetRefusalMessageAsync(string strRefusal, string strFallback, CancellationToken token)
        {
            return string.IsNullOrEmpty(strRefusal)
                ? strFallback
                : await LanguageManager.GetStringAsync(strRefusal, GlobalSettings.DefaultLanguage, token: token).ConfigureAwait(false);
        }

        /// <summary>
        /// The localized expense log prefix for a career purchase, as the career editor writes it.
        /// </summary>
        private static Task<string> GetExpenseStringAsync(string strKey, CancellationToken token)
        {
            return LanguageManager.GetStringAsync(strKey, token: token);
        }

        private static DateTime GetDate(JObject objArgs)
        {
            string strDate = objArgs.Value<string>("date")?.Trim();
            if (string.IsNullOrEmpty(strDate))
                return DateTime.Now;
            if (!DateTime.TryParse(strDate, GlobalSettings.InvariantCultureInfo, DateTimeStyles.AssumeLocal, out DateTime datReturn))
                throw new AgentToolException("`date` must look like 2080-01-31.");
            return datReturn;
        }

        #endregion

        private static async Task<object> FinishCreationAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            ChummerMainForm frmMain = Program.MainForm ?? throw new AgentToolException("Chummer's main window is not open.");
            // The checks Save as Created runs before it asks for confirmation
            List<string> lstErrors = new List<string>();
            List<string> lstWarnings = new List<string>();
            if (!await frmCreate.CollectValidityIssuesAsync(lstErrors, lstWarnings, token).ConfigureAwait(false))
                throw new AgentToolException("The character can't finish creation yet: " + string.Join(" ", lstErrors));
            string strPath = objArgs.Value<string>("path")?.Trim();
            if (!string.IsNullOrEmpty(strPath))
                await objCharacter.SetFileNameAsync(ResolveOutputPath(strPath, ".chum5", ".chum5lz"), token).ConfigureAwait(false);
            if (string.IsNullOrEmpty(await objCharacter.GetFileNameAsync(token).ConfigureAwait(false)))
                throw new AgentToolException("The character has no file yet; pass `path` (or call save_character first).");
            string strFile = await objCharacter.GetFileNameAsync(token).ConfigureAwait(false);
            if (!await frmCreate.SaveCharacter(false, true, token).ConfigureAwait(false) || !frmCreate.IsReopenQueued)
                throw new AgentToolException("Chummer didn't save the character as created.");
            // Closing the creation window makes Chummer reopen the character in the career window
            await frmCreate.DoThreadSafeAsync(x => x.Close(), token).ConfigureAwait(false);
            for (int i = 0; i < 200; ++i)
            {
                CharacterShared frmCareer = await frmMain.OpenCharacterEditorForms
                                                         .FirstOrDefaultAsync(async (x, t) => x is CharacterCareer
                                                                                          && string.Equals(await x.CharacterObject.GetFileNameAsync(t).ConfigureAwait(false),
                                                                                                           strFile, StringComparison.OrdinalIgnoreCase), token)
                                                         .ConfigureAwait(false);
                if (frmCareer?.IsFinishedInitializing == true)
                    return new { result = "Saved " + strFile + " as created; " + await frmCareer.CharacterObject.GetCharacterNameAsync(token).ConfigureAwait(false) + " is open in career mode.", warnings = lstWarnings };
                await Task.Delay(50, token).ConfigureAwait(false);
            }

            // Chummer doesn't reopen it on its own (e.g. under tests), so open the saved file as File > Open does
            JObject objOpened = JObject.FromObject(await OpenCharacterAsync(new JObject { ["path"] = strFile }, token).ConfigureAwait(false));
            return new { result = "Saved " + strFile + " as created. " + objOpened.Value<string>("result"), warnings = lstWarnings };
        }

        private static async Task<object> LogKarmaAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCareer frmCareer = await GetCareerFormAsync(objArgs, token).ConfigureAwait(false);
            int intAmount = OptionalInt(objArgs, "amount") ?? throw new AgentToolException("`amount` is required.");
            if (intAmount == 0)
                throw new AgentToolException("`amount` can't be 0.");
            string strReason = RequiredString(objArgs, "reason");
            DateTime datDate = GetDate(objArgs);
            bool blnRefund = objArgs.Value<bool?>("refund") == true;
            bool blnConvert = objArgs.Value<bool?>("convert") == true;
            if (intAmount > 0)
                await frmCareer.AddKarmaGainedAsync(intAmount, strReason, datDate, blnRefund, blnConvert, false, token).ConfigureAwait(false);
            else if (!await frmCareer.AddKarmaSpentAsync(-intAmount, strReason, datDate, blnRefund, blnConvert, false, token).ConfigureAwait(false))
                throw new AgentToolException("The character only has " + (await frmCareer.CharacterObject.GetKarmaAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo)
                                             + " karma.");
            return await FinishCareerEditAsync(frmCareer, (intAmount > 0 ? "Gained " : "Spent ") + Math.Abs(intAmount).ToString(GlobalSettings.InvariantCultureInfo)
                                                          + " karma: " + strReason + ".", token).ConfigureAwait(false);
        }

        private static async Task<object> LogNuyenAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCareer frmCareer = await GetCareerFormAsync(objArgs, token).ConfigureAwait(false);
            decimal decAmount = objArgs.Value<decimal?>("amount") ?? throw new AgentToolException("`amount` is required.");
            if (decAmount == 0)
                throw new AgentToolException("`amount` can't be 0.");
            string strReason = RequiredString(objArgs, "reason");
            DateTime datDate = GetDate(objArgs);
            bool blnRefund = objArgs.Value<bool?>("refund") == true;
            bool blnConvert = objArgs.Value<bool?>("convert") == true;
            if (decAmount > 0)
                await frmCareer.AddNuyenGainedAsync(decAmount, strReason, datDate, blnRefund, blnConvert, false, token).ConfigureAwait(false);
            else if (!await frmCareer.AddNuyenSpentAsync(-decAmount, strReason, datDate, blnRefund, blnConvert, false, token).ConfigureAwait(false))
                throw new AgentToolException("The character only has " + (await frmCareer.CharacterObject.GetNuyenAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo)
                                             + " nuyen.");
            return await FinishCareerEditAsync(frmCareer, (decAmount > 0 ? "Gained " : "Spent ") + Math.Abs(decAmount).ToString("#,0.##", GlobalSettings.InvariantCultureInfo)
                                                          + " nuyen: " + strReason + ".", token).ConfigureAwait(false);
        }

        private static async Task<object> ImproveAttributeAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCareer frmCareer = await GetCareerFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCareer.CharacterObject;
            string strName = RequiredString(objArgs, "name").ToUpperInvariant();
            CharacterAttrib objAttribute = await objCharacter.GetAttributeAsync(strName, token: token).ConfigureAwait(false)
                                           ?? throw new AgentToolException("There is no attribute '" + strName + "'.");
            int intTimes = Math.Max(1, OptionalInt(objArgs, "times") ?? 1);
            int intSpent = 0;
            for (int i = 0; i < intTimes; ++i)
            {
                // The attribute's + button: refused past the maximum or without the karma
                int intCost = await objAttribute.GetUpgradeKarmaCostAsync(token).ConfigureAwait(false);
                if (intCost < 0 || !await objAttribute.GetCanUpgradeCareerAsync(token).ConfigureAwait(false))
                {
                    if (i == 0)
                    {
                        int intKarma = await objCharacter.GetKarmaAsync(token).ConfigureAwait(false);
                        throw new AgentToolException(strName + " can't be raised: " + (intCost < 0 || intCost <= intKarma
                                                         ? "it is at its maximum."
                                                         : "it costs " + intCost.ToString(GlobalSettings.InvariantCultureInfo) + " karma and the character has "
                                                           + intKarma.ToString(GlobalSettings.InvariantCultureInfo) + "."));
                    }

                    break;
                }

                await objAttribute.Upgrade(token: token).ConfigureAwait(false);
                intSpent += intCost;
            }

            return await FinishCareerEditAsync(frmCareer, strName + " is now " + (await objAttribute.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo)
                                                          + " (" + intSpent.ToString(GlobalSettings.InvariantCultureInfo) + " karma).", token).ConfigureAwait(false);
        }

        private static async Task<object> ImproveSkillAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCareer frmCareer = await GetCareerFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCareer.CharacterObject;
            SkillsSection objSkillsSection = await objCharacter.GetSkillsSectionAsync(token).ConfigureAwait(false);
            string strName = RequiredString(objArgs, "name");
            int intTimes = Math.Max(1, OptionalInt(objArgs, "times") ?? 1);
            int intKarmaBefore = await objCharacter.GetKarmaAsync(token).ConfigureAwait(false);
            string strResult;
            if (objArgs.Value<bool?>("group") == true)
            {
                SkillGroup objGroup = null;
                foreach (SkillGroup objLoop in await objSkillsSection.SkillGroups.ToListAsync(token).ConfigureAwait(false))
                {
                    if (string.Equals(objLoop.Name, strName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(await objLoop.GetCurrentDisplayNameAsync(token).ConfigureAwait(false), strName, StringComparison.OrdinalIgnoreCase))
                    {
                        objGroup = objLoop;
                        break;
                    }
                }

                if (objGroup == null)
                    throw new AgentToolException("There is no skill group '" + strName + "'.");
                for (int i = 0; i < intTimes; ++i)
                {
                    // The group's + button
                    if (!await objGroup.GetCareerCanIncreaseAsync(token).ConfigureAwait(false))
                    {
                        if (i == 0)
                            throw new AgentToolException(objGroup.Name + " can't be raised (it is broken, at its maximum, or costs "
                                                         + (await objGroup.GetUpgradeKarmaCostAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo)
                                                         + " karma).");
                        break;
                    }

                    await objGroup.Upgrade(token).ConfigureAwait(false);
                }

                strResult = objGroup.Name + " is now " + (await objGroup.GetRatingAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo);
            }
            else
            {
                Skill objSkill = await FindSkillAsync(await objSkillsSection.Skills.ToListAsync(token).ConfigureAwait(false), strName, token).ConfigureAwait(false)
                                 ?? await FindSkillAsync((await objSkillsSection.KnowledgeSkills.ToListAsync(token).ConfigureAwait(false)).Cast<Skill>().ToList(), strName, token)
                                     .ConfigureAwait(false)
                                 ?? throw new AgentToolException("The character has no skill '" + strName + "'. Add a knowledge skill with add_knowledge_skill first.");
                for (int i = 0; i < intTimes; ++i)
                {
                    // The skill's + button
                    if (!await objSkill.GetCanUpgradeCareerAsync(token).ConfigureAwait(false))
                    {
                        if (i == 0)
                            throw new AgentToolException(objSkill.Name + " can't be raised (it is at its maximum, part of an unbroken group, or costs "
                                                         + (await objSkill.GetUpgradeKarmaCostAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo)
                                                         + " karma).");
                        break;
                    }

                    await objSkill.Upgrade(token).ConfigureAwait(false);
                }

                strResult = await objSkill.GetCurrentDisplayNameAsync(token).ConfigureAwait(false) + " is now "
                            + (await objSkill.GetRatingAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo);
            }

            return await FinishCareerEditAsync(frmCareer, strResult + " (" + (intKarmaBefore - await objCharacter.GetKarmaAsync(token).ConfigureAwait(false))
                                                          .ToString(GlobalSettings.InvariantCultureInfo) + " karma).", token).ConfigureAwait(false);
        }

        private static async Task<object> BurnEdgeAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCareer frmCareer = await GetCareerFormAsync(objArgs, token).ConfigureAwait(false);
            CharacterAttrib objEdge = await frmCareer.CharacterObject.GetAttributeAsync("EDG", token: token).ConfigureAwait(false);
            // The Burn Edge button: Edge can't go below 0
            if (await objEdge.GetValueAsync(token).ConfigureAwait(false) <= 0)
                throw new AgentToolException("The character has no Edge left to burn.");
            await objEdge.Degrade(token: token).ConfigureAwait(false);
            return await FinishCareerEditAsync(frmCareer, "Burned a point of Edge; Edge is now "
                                                          + (await objEdge.GetValueAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo) + ".",
                                               token).ConfigureAwait(false);
        }
    }
}
