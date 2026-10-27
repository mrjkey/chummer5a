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
using System.Xml.XPath;
using Chummer.Backend.Uniques;
using Newtonsoft.Json.Linq;

namespace Chummer.Agent
{
    public static partial class AgentTools
    {
        private static IEnumerable<AgentTool> CreateCritterTools()
        {
            yield return new AgentTool("add_critter_power",
                "Give the character a critter power (search_catalog kind='critterpower'), as the Critter Powers tab's Add Critter Power does: free "
                + "spirits, the Infected, shapeshifters, drakes and other characters with critter powers. Only the powers that dialog lists are allowed: "
                + "the categories the character may use and, for metatypes with an optional power list, only those. Remove one with remove_item.",
                AgentTool.Schema(("id!", "string", "Critter power id from search_catalog, or its exact English name."),
                                 ("rating", "integer", "Rating, for powers that have one."),
                                 ("forced_value", "string", ForcedValueDescription),
                                 ("count_towards_limit", "boolean", "Whether it counts toward the free power limit (the tab's checkbox). Default true."),
                                 ("character", "string", CharacterArgDescription)),
                AddCritterPowerAsync);
            yield return new AgentTool("set_critter_power",
                "Change whether a critter power counts toward the character's free power limit, as the Critter Powers tab's checkbox does.",
                AgentTool.Schema(("id!", "string", "Critter power id from list_items, or its name."),
                                 ("count_towards_limit!", "boolean", "Whether it counts toward the limit."),
                                 ("character", "string", CharacterArgDescription)),
                SetCritterPowerAsync);
            yield return new AgentTool("add_ai_program",
                "Give an A.I. character (Data Trails) a program (search_catalog kind='program'), as the A.I. Programs tab's Add Program does. A program "
                + "that builds on another needs that one first. Remove one with remove_item.",
                AgentTool.Schema(("id!", "string", "Program id from search_catalog, or its exact English name."),
                                 ("forced_value", "string", "Text for programs that ask for it."),
                                 ("character", "string", CharacterArgDescription)),
                AddAIProgramAsync);
            yield return new AgentTool("convert_to_cyberzombie",
                "Turn the character into a cyberzombie (Chrome Flesh), as Special > Cyberzombie does: its Essence must be below 0 and something must "
                + "allow the conversion. Pass the hits the player rolled on the Willpower test; missing the threshold adds negative qualities.",
                AgentTool.Schema(("wil_hits!", "integer", "Hits on the Willpower test the conversion asks for."),
                                 ("character", "string", CharacterArgDescription)),
                ConvertToCyberzombieAsync);
            yield return new AgentTool("create_critter",
                "Make a new critter character (a spirit, sprite, paracritter, drone-like critter...) from critters.xml and open it, as File > New Critter "
                + "does. Critters skip creation: they open in career mode with rule enforcement off. Use list_metatypes with critter categories to see them.",
                AgentTool.Schema(("metatype!", "string", "Critter name or id, e.g. 'Spirit of Air', 'Hellhound'."),
                                 ("metavariant", "string", "Metavariant, if any."),
                                 ("force", "integer", "Force or level, for critters that have one."),
                                 ("settings", "string", SettingsArgDescription),
                                 ("name", "string", "The critter's name (optional).")),
                CreateCritterAsync);
            yield return new AgentTool("create_spirit_character",
                "Save a character file for one of the character's spirits or sprites, at its force, link it to the spirit and open it, as the spirit's "
                + "Create Critter menu item does.",
                AgentTool.Schema(("id!", "string", "Spirit or sprite id from get_character_summary."),
                                 ("path!", "string", "Where to save the critter's .chum5 file."),
                                 ("name", "string", "The critter's own name (optional)."),
                                 ("character", "string", CharacterArgDescription)),
                CreateSpiritCharacterAsync);
        }

        private static async Task<object> AddCritterPowerAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            // The Critter Powers tab only shows for characters that have critter powers
            if (!await objCharacter.GetCritterEnabledAsync(token).ConfigureAwait(false))
                throw new AgentToolException("This character has no Critter Powers tab (it isn't a critter, free spirit, Infected or similar).");
            XmlNode xmlPower = await GetDataNodeAsync(objCharacter, "critterpowers.xml", "/chummer/powers/power", RequiredString(objArgs, "id"), token)
                .ConfigureAwait(false);
            string strName = xmlPower["name"]?.InnerText;
            int intRating = OptionalInt(objArgs, "rating") ?? 0;
            decimal decPowerPoints = 0;
            // The powers and power point cost the Select Critter Power dialog offers
            SelectCritterPower frmSelect = await Program.MainForm.DoThreadSafeFuncAsync(() => new SelectCritterPower(objCharacter), token).ConfigureAwait(false);
            try
            {
                if (!await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false))
                {
                    await frmSelect.FillCategoriesAsync(token).ConfigureAwait(false);
                    List<XPathNavigator> lstAllowed = await frmSelect.GetAllowedPowersAsync("Show All", string.Empty, token).ConfigureAwait(false);
                    if (!lstAllowed.Exists(x => x.SelectSingleNode("name")?.Value == strName))
                        throw new AgentToolException(strName + " isn't a critter power this character can take. Options: "
                                                     + string.Join(", ", lstAllowed.Select(x => x.SelectSingleNode("name")?.Value).Take(60)) + ".");
                }

                if (xmlPower["rating"] == null)
                    intRating = 0;
                decPowerPoints = await frmSelect.GetPowerPointCostAsync(xmlPower.CreateNavigator(), token).ConfigureAwait(false);
            }
            finally
            {
                await frmSelect.DoThreadSafeAsync(x => x.Dispose(), CancellationToken.None).ConfigureAwait(false);
            }

            // What Add Critter Power does with the dialog's choice
            CritterPower objPower = new CritterPower(objCharacter);
            await objPower.CreateAsync(xmlPower, intRating, objArgs.Value<string>("forced_value") ?? string.Empty, token).ConfigureAwait(false);
            objPower.PowerPoints = decPowerPoints;
            if (objPower.InternalId.IsEmptyGuid())
                throw new AgentToolException("Chummer did not add " + strName + " (a choice it needed was cancelled; pass forced_value).");
            bool? blnCount = objArgs.Value<bool?>("count_towards_limit");
            if (blnCount.HasValue)
                objPower.CountTowardsLimit = blnCount.Value;
            await objCharacter.CritterPowers.AddAsync(objPower, token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate, "Added the critter power " + await objPower.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " (id " + objPower.InternalId + ").", token).ConfigureAwait(false);
        }

        private static async Task<object> SetCritterPowerAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strId = RequiredString(objArgs, "id");
            CritterPower objPower = await FindOwnedAsync(await objCharacter.CritterPowers.ToListAsync(token).ConfigureAwait(false), strId, token).ConfigureAwait(false)
                                    ?? throw new AgentToolException("The character has no critter power '" + strId + "'.");
            bool blnCount = objArgs.Value<bool?>("count_towards_limit") ?? throw new AgentToolException("`count_towards_limit` is required.");
            objPower.CountTowardsLimit = blnCount;
            return await FinishEditAsync(frmCreate, objPower.Name + (blnCount ? " counts" : " doesn't count") + " toward the power limit.", token).ConfigureAwait(false);
        }

        private static async Task<object> AddAIProgramAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            // The A.I. Programs tab only shows when something enables advanced programs
            if (!await objCharacter.GetAdvancedProgramsEnabledAsync(token).ConfigureAwait(false))
                throw new AgentToolException("This character has no A.I. Programs tab (it isn't an A.I.); give other characters programs with add_gear(parent=<commlink or deck>).");
            XmlNode xmlProgram = await GetDataNodeAsync(objCharacter, "programs.xml", "/chummer/programs/program", RequiredString(objArgs, "id"), token)
                .ConfigureAwait(false);
            string strName = xmlProgram["name"]?.InnerText;
            await EnsureBookEnabledAsync(xmlProgram, objCharacter, token).ConfigureAwait(false);
            // The Select A.I. Program dialog's list, with Limit List ticked
            if (!await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false)
                && !await SelectAIProgram.IsProgramAllowedAsync(objCharacter, xmlProgram.CreateNavigator(), true, token).ConfigureAwait(false))
                throw new AgentToolException(strName + " isn't available: it builds on " + (xmlProgram["require"]?.InnerText ?? "another program")
                                             + " the character doesn't have, or the character's metatype doesn't allow it.");
            string strExtra = string.Empty;
            if (xmlProgram.SelectSingleNode("bonus/selecttext") != null)
                strExtra = objArgs.Value<string>("forced_value")?.Trim()
                           ?? throw new AgentToolException(strName + " asks for text; pass it as forced_value.");
            AIProgram objProgram = new AIProgram(objCharacter);
            await objProgram.CreateAsync(xmlProgram, strExtra, token: token).ConfigureAwait(false);
            if (objProgram.InternalId.IsEmptyGuid())
                throw new AgentToolException("Chummer did not add " + strName + ".");
            await objCharacter.AIPrograms.AddAsync(objProgram, token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate, "Added the program " + await objProgram.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " (id " + objProgram.InternalId + ").", token).ConfigureAwait(false);
        }

        private static async Task<object> ConvertToCyberzombieAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            int intHits = OptionalInt(objArgs, "wil_hits") ?? throw new AgentToolException("`wil_hits` is required.");
            if (intHits < 0)
                throw new AgentToolException("`wil_hits` can't be negative.");
            string strProblem = await objCharacter.GetCyberzombieRequirementsProblemAsync(token).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(strProblem))
                throw new AgentToolException(strProblem.Replace(Environment.NewLine, " ").Replace('\t', ' '));
            await objCharacter.ApplyCyberzombieAsync(intHits, token).ConfigureAwait(false);
            return await FinishAnyEditAsync(frmCharacter, "The character is now a cyberzombie.", token).ConfigureAwait(false);
        }

        private static async Task<object> CreateCritterAsync(JObject objArgs, CancellationToken token)
        {
            ChummerMainForm frmMain = Program.MainForm ?? throw new AgentToolException("Chummer's main window is not open.");
            string strMetatype = RequiredString(objArgs, "metatype");
            (string strSettingsKey, CharacterSettings _) = await FindSettingsEntryAsync(objArgs.Value<string>("settings"), token).ConfigureAwait(false);
            Character objCharacter = new Character();
            bool blnOpened = false;
            try
            {
                // What New Critter does: the settings, its overrides, then the metatype dialog
                await objCharacter.SetSettingsKeyAsync(strSettingsKey, token).ConfigureAwait(false);
                await ChummerMainForm.PrepareNewCritterAsync(objCharacter, token).ConfigureAwait(false);
                string strMetavariant = objArgs.Value<string>("metavariant")?.Trim() ?? string.Empty;
                string strDescription = await ApplyKarmaMetatypeAsync(objCharacter, strMetatype, strMetavariant, OptionalInt(objArgs, "force") ?? 0, token)
                    .ConfigureAwait(false);
                string strName = objArgs.Value<string>("name");
                if (!string.IsNullOrWhiteSpace(strName))
                    await objCharacter.SetNameAsync(strName.Trim(), token).ConfigureAwait(false);
                await frmMain.OpenNewCritterEditorAsync(objCharacter, token).ConfigureAwait(false);
                blnOpened = true;
                await WaitForEditorAsync(frmMain, objCharacter, token).ConfigureAwait(false);
                return new
                {
                    result = "Created the critter " + strDescription + " (" + await objCharacter.GetCharacterNameAsync(token).ConfigureAwait(false)
                             + "). It is open in Chummer's career editor."
                };
            }
            finally
            {
                // Once open, the editor owns the character
                if (!blnOpened)
                    await objCharacter.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Wait until a character just handed to the main window has an editor that has finished loading.
        /// </summary>
        private static async Task WaitForEditorAsync(ChummerMainForm frmMain, Character objCharacter, CancellationToken token)
        {
            for (int i = 0; i < 600; ++i)
            {
                CharacterShared frmEditor = await frmMain.OpenCharacterEditorForms
                                                         .FirstOrDefaultAsync(x => ReferenceEquals(x.CharacterObject, objCharacter), token).ConfigureAwait(false);
                if (frmEditor?.IsFinishedInitializing == true)
                    return;
                await Task.Delay(50, token).ConfigureAwait(false);
            }
        }

        private static async Task<object> CreateSpiritCharacterAsync(JObject objArgs, CancellationToken token)
        {
            ChummerMainForm frmMain = Program.MainForm ?? throw new AgentToolException("Chummer's main window is not open.");
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            string strId = RequiredString(objArgs, "id");
            Spirit objSpirit = await objCharacter.Spirits.FirstOrDefaultAsync(x => string.Equals(x.InternalId, strId, StringComparison.OrdinalIgnoreCase), token)
                                                 .ConfigureAwait(false)
                               ?? throw new AgentToolException("The character has no spirit or sprite '" + strId + "'.");
            string strType = await objSpirit.GetNameAsync(token).ConfigureAwait(false);
            if (string.IsNullOrEmpty(strType))
                throw new AgentToolException(await LanguageManager.GetStringAsync("Message_SelectCritterType", GlobalSettings.DefaultLanguage, token: token).ConfigureAwait(false));
            XmlNode xmlMetatype = (await objCharacter.LoadDataAsync("critters.xml", token: token).ConfigureAwait(false))
                                  .TryGetNodeByNameOrId("/chummer/metatypes/metatype", strType)
                                  ?? throw new AgentToolException("critters.xml has no critter '" + strType + "'.");
            string strPath = ResolveOutputPath(RequiredString(objArgs, "path"), ".chum5", ".chum5lz");
            Character objCritter = new Character();
            bool blnOpened = false;
            try
            {
                IAsyncDisposable objLocker = await objCritter.LockObject.EnterWriteLockAsync(token).ConfigureAwait(false);
                try
                {
                    if (!await SpiritControl.CreateCritterCharacterAsync(objSpirit, objCritter, xmlMetatype, strType, await objSpirit.GetForceAsync(token).ConfigureAwait(false),
                                                                         objArgs.Value<string>("name")?.Trim() ?? string.Empty, strPath, token).ConfigureAwait(false))
                        throw new AgentToolException("Chummer could not save " + strPath + ".");
                }
                finally
                {
                    await objLocker.DisposeAsync().ConfigureAwait(false);
                }

                await frmMain.OpenCharacter(objCritter, token: token).ConfigureAwait(false);
                blnOpened = true;
                await WaitForEditorAsync(frmMain, objCritter, token).ConfigureAwait(false);
                return new { result = "Saved " + strType + " to " + await objCritter.GetFileNameAsync(token).ConfigureAwait(false) + ", linked it to the spirit and opened it." };
            }
            finally
            {
                if (!blnOpened)
                    await objCritter.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
