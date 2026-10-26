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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.XPath;
using Chummer.Backend.Enums;
using Newtonsoft.Json.Linq;

namespace Chummer.Agent
{
    public static partial class AgentTools
    {
        private const string SettingsArgDescription =
            "Character settings (house rules, books, build method) by key or name from list_build_settings. Defaults to Chummer's default settings.";

        private const string MetatypeArgsDescription =
            "Priority builds also take `priorities` and `talent`; karma builds only need the metatype. "
            + "Chummer checks everything the new-character dialog would (letters used once or summing correctly, metatype allowed by the Heritage priority, "
            + "talent allowed by the Talent priority and metatype, the talent's free skill choices) and says what the options are when something is off.";

        private static IEnumerable<AgentTool> CreateCreationTools()
        {
            yield return new AgentTool("list_build_settings",
                "The character settings (build rules) Chummer has: build method (Priority, SumtoTen, Karma, LifeModule), build karma, priority table, "
                + "maximum availability and enabled books. Pick one for create_character.",
                AgentTool.Schema(), (a, t) => ListBuildSettingsAsync(t));
            yield return new AgentTool("create_character",
                "Create a new character and open it in Chummer's editor, as File > New Character does: choose the settings, then metatype, priorities "
                + "and talent. " + MetatypeArgsDescription,
                AgentTool.Schema(("settings", "string", SettingsArgDescription),
                                 ("metatype!", "string", "Metatype name or id, e.g. 'Human', 'Elf', 'Ork' (see get_priority_table or list_metatypes)."),
                                 ("metavariant", "string", "Metavariant name, if any."),
                                 ("priorities", "object",
                                  "Priority builds: {\"heritage\": \"D\", \"attributes\": \"B\", \"talent\": \"E\", \"skills\": \"A\", \"resources\": \"C\"}."),
                                 ("talent", "string",
                                  "Priority builds: the talent value from get_priority_table, e.g. 'Mundane', 'Adept', 'Magician', 'Aspected Magician', 'Technomancer'. "
                                  + "May be left out when the Talent priority offers only one."),
                                 ("talent_skills", "array", "Free skills (or skill groups) the talent grants, by name, e.g. [\"Sorcery\", \"Conjuring\"]."),
                                 ("name", "string", "The character's name (optional)."),
                                 ("alias", "string", "Street name (optional)."),
                                 ("force", "integer", "Force or level, only for metatypes that have one (spirits, sprites)."),
                                 ("ignore_rules", "boolean", "Turn off Chummer's rule enforcement for this character. Default false.")),
                CreateCharacterAsync);
            yield return new AgentTool("set_priorities",
                "Change an open creation-mode character's metatype, priorities, talent or the talent's free skills, as the editor's Change Priorities "
                + "does. Anything left out keeps its current value. " + MetatypeArgsDescription,
                AgentTool.Schema(("metatype", "string", "New metatype name or id."),
                                 ("metavariant", "string", "New metavariant name, or 'None'."),
                                 ("priorities", "object", "Priorities to change, e.g. {\"resources\": \"B\", \"skills\": \"C\"}; the rest stay as they are."),
                                 ("talent", "string", "New talent value."),
                                 ("talent_skills", "array", "New free skill choices for the talent."),
                                 ("character", "string", CharacterArgDescription)),
                SetPrioritiesAsync);
            yield return new AgentTool("save_character",
                "Save an open character to its file. A new character without a file is saved to Chummer's saves folder under its name unless `path` is given.",
                AgentTool.Schema(("path", "string", "File to save to (optional). Relative paths go in Chummer's saves folder; .chum5 is added if missing."),
                                 ("overwrite", "boolean", "Replace an existing file at `path` that isn't this character's. Default false."),
                                 ("character", "string", CharacterArgDescription)),
                SaveCharacterAsync);
        }

        #region Settings

        /// <summary>
        /// Settings by key or name, or null for none asked for.
        /// </summary>
        private static async Task<CharacterSettings> FindSettingsAsync(string strWanted, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(strWanted))
                return null;
            return (await FindSettingsEntryAsync(strWanted, token).ConfigureAwait(false)).Settings;
        }

        /// <summary>
        /// Settings and their key by key or name; Chummer's default settings when nothing is asked for.
        /// </summary>
        private static async Task<(string Key, CharacterSettings Settings)> FindSettingsEntryAsync(string strWanted, CancellationToken token)
        {
            IReadOnlyDictionary<string, CharacterSettings> dicSettings = await SettingsManager.GetLoadedCharacterSettingsAsync(token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(strWanted))
                strWanted = GlobalSettings.DefaultCharacterSetting;
            strWanted = strWanted.Trim();
            if (dicSettings.TryGetValue(strWanted, out CharacterSettings objSettings))
                return (strWanted, objSettings);
            List<string> lstNames = new List<string>(dicSettings.Count);
            foreach (KeyValuePair<string, CharacterSettings> kvpSettings in dicSettings)
            {
                string strName = await kvpSettings.Value.GetNameAsync(token).ConfigureAwait(false);
                string strDisplayName = await kvpSettings.Value.GetCurrentDisplayNameAsync(token).ConfigureAwait(false);
                if (string.Equals(kvpSettings.Key, strWanted, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(strName, strWanted, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(strDisplayName, strWanted, StringComparison.OrdinalIgnoreCase))
                    return (kvpSettings.Key, kvpSettings.Value);
                lstNames.Add(strDisplayName);
            }

            throw new AgentToolException("No character settings named '" + strWanted + "'. Use one of: " + string.Join(", ", lstNames) + ".");
        }

        private static async Task<object> ListBuildSettingsAsync(CancellationToken token)
        {
            IReadOnlyDictionary<string, CharacterSettings> dicSettings = await SettingsManager.GetLoadedCharacterSettingsAsync(token).ConfigureAwait(false);
            List<object> lstReturn = new List<object>(dicSettings.Count);
            foreach (KeyValuePair<string, CharacterSettings> kvpSettings in dicSettings)
            {
                CharacterSettings objSettings = kvpSettings.Value;
                CharacterBuildMethod eBuildMethod = await objSettings.GetBuildMethodAsync(token).ConfigureAwait(false);
                lstReturn.Add(new
                {
                    key = kvpSettings.Key,
                    name = await objSettings.GetCurrentDisplayNameAsync(token).ConfigureAwait(false),
                    isDefault = kvpSettings.Key == GlobalSettings.DefaultCharacterSetting,
                    buildMethod = eBuildMethod.ToString(),
                    buildKarma = await objSettings.GetBuildKarmaAsync(token).ConfigureAwait(false),
                    priorityTable = eBuildMethod.UsesPriorityTables() ? await objSettings.GetPriorityTableAsync(token).ConfigureAwait(false) : null,
                    sumToTen = eBuildMethod == CharacterBuildMethod.SumtoTen ? await objSettings.GetSumtoTenAsync(token).ConfigureAwait(false) : (int?)null,
                    maximumAvailability = await objSettings.GetMaximumAvailabilityAsync(token).ConfigureAwait(false),
                    books = objSettings.Books.OrderBy(x => x, StringComparer.Ordinal).ToList()
                });
            }

            return lstReturn;
        }

        #endregion

        #region Metatype and priorities

        private static List<string> ReadStringList(JObject objArgs, string strName)
        {
            JToken objToken = objArgs[strName];
            if (objToken == null || objToken.Type == JTokenType.Null)
                return null;
            if (objToken is JArray objArray)
                return objArray.Select(x => x.ToString().Trim()).Where(x => x.Length > 0).ToList();
            return objToken.ToString().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        }

        private static JObject ReadPriorities(JObject objArgs)
        {
            JToken objToken = objArgs["priorities"];
            if (objToken == null || objToken.Type == JTokenType.Null)
                return null;
            if (objToken is JObject objPriorities)
                return objPriorities;
            try
            {
                return JObject.Parse(objToken.ToString());
            }
            catch (Newtonsoft.Json.JsonException)
            {
                throw new AgentToolException("`priorities` must be an object like {\"heritage\": \"D\", \"attributes\": \"B\", \"talent\": \"E\", \"skills\": \"A\", \"resources\": \"C\"}.");
            }
        }

        /// <summary>
        /// Apply metatype (and for priority builds, priorities and talent) through the same code as the new-character dialogs' OK buttons.
        /// Anything not in <paramref name="objArgs"/> keeps the character's current value.
        /// </summary>
        private static async Task<string> ApplyMetatypeAndPrioritiesAsync(Character objCharacter, JObject objArgs, CancellationToken token)
        {
            ChummerMainForm frmMain = Program.MainForm ?? throw new AgentToolException("Chummer's main window is not open.");
            string strMetatype = objArgs.Value<string>("metatype")?.Trim();
            if (string.IsNullOrEmpty(strMetatype))
                strMetatype = await objCharacter.GetMetatypeAsync(token).ConfigureAwait(false);
            if (string.IsNullOrEmpty(strMetatype))
                throw new AgentToolException("`metatype` is required.");
            JToken objMetavariantArg = objArgs["metavariant"];
            string strMetavariant = objMetavariantArg != null && objMetavariantArg.Type != JTokenType.Null
                ? objMetavariantArg.ToString().Trim()
                : string.IsNullOrEmpty(objArgs.Value<string>("metatype"))
                    ? await objCharacter.GetMetavariantAsync(token).ConfigureAwait(false)
                    : string.Empty;
            int intForce = OptionalInt(objArgs, "force") ?? 0;

            if (!await objCharacter.GetEffectiveBuildMethodUsesPriorityTablesAsync(token).ConfigureAwait(false))
            {
                if (ReadPriorities(objArgs) != null || !string.IsNullOrEmpty(objArgs.Value<string>("talent")))
                    throw new AgentToolException("These settings use a karma build, which has no priorities or talents: buy magic or resonance with qualities instead.");
                XPathNavigator xmlMetatypes = (await objCharacter.LoadDataXPathAsync(objCharacter.IsCritter ? "critters.xml" : "metatypes.xml", token: token)
                                                                 .ConfigureAwait(false)).SelectSingleNode("/chummer/metatypes");
                string strBookXPath = await (await objCharacter.GetSettingsAsync(token).ConfigureAwait(false)).BookXPathAsync(token: token).ConfigureAwait(false);
                if (xmlMetatypes?.TryGetNodeByNameOrId("metatype", strMetatype, strBookXPath) == null)
                    throw new AgentToolException("There is no metatype '" + strMetatype + "' in the enabled books. Use list_metatypes.");
                SelectMetatypeKarma frmKarma = await frmMain.DoThreadSafeFuncAsync(() => new SelectMetatypeKarma(objCharacter), token).ConfigureAwait(false);
                try
                {
                    string strRefusal = await frmKarma.ApplyMetatypeSelectionAsync(strMetatype, strMetavariant, string.Empty, intForce, string.Empty, token)
                                                      .ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(strRefusal))
                        throw new AgentToolException(strRefusal);
                }
                finally
                {
                    await frmKarma.DoThreadSafeAsync(x => x.Dispose(), CancellationToken.None).ConfigureAwait(false);
                }

                return strMetatype + (string.IsNullOrEmpty(strMetavariant) || strMetavariant.IsEmptyGuid() ? string.Empty : " (" + strMetavariant + ")");
            }

            JObject objPriorities = ReadPriorities(objArgs) ?? new JObject();
            string GetPriority(string strKey, string strCurrent)
            {
                string strValue = objPriorities.Value<string>(strKey);
                return string.IsNullOrWhiteSpace(strValue) ? strCurrent : strValue;
            }

            List<string> lstSkills = ReadStringList(objArgs, "talent_skills");
            // A different talent comes with different free skill choices, so only keep the current ones for the same talent
            if (lstSkills == null && (objArgs["talent"] != null || objPriorities["talent"] != null))
                lstSkills = new List<string>();
            if (lstSkills == null)
            {
                lstSkills = new List<string>();
                await objCharacter.PriorityBonusSkillList.ForEachAsync(x => lstSkills.Add(x), token).ConfigureAwait(false);
            }

            if (lstSkills.Count > 3)
                throw new AgentToolException("A talent grants at most three free skills.");
            string strTalentValue = objArgs.Value<string>("talent");
            if (string.IsNullOrWhiteSpace(strTalentValue) && objArgs["talent"] == null)
                strTalentValue = await objCharacter.GetTalentPriorityAsync(token).ConfigureAwait(false);
            SelectMetatypePriority.PrioritySelection objSelection = new SelectMetatypePriority.PrioritySelection
            {
                Heritage = GetPriority("heritage", await objCharacter.GetMetatypePriorityAsync(token).ConfigureAwait(false)),
                Attributes = GetPriority("attributes", await objCharacter.GetAttributesPriorityAsync(token).ConfigureAwait(false)),
                Talent = GetPriority("talent", await objCharacter.GetSpecialPriorityAsync(token).ConfigureAwait(false)),
                Skills = GetPriority("skills", await objCharacter.GetSkillsPriorityAsync(token).ConfigureAwait(false)),
                Resources = GetPriority("resources", await objCharacter.GetResourcesPriorityAsync(token).ConfigureAwait(false)),
                TalentValue = strTalentValue,
                Skill1 = lstSkills.Count > 0 ? lstSkills[0] : string.Empty,
                Skill2 = lstSkills.Count > 1 ? lstSkills[1] : string.Empty,
                Skill3 = lstSkills.Count > 2 ? lstSkills[2] : string.Empty,
                Metatype = strMetatype,
                Metavariant = strMetavariant,
                Force = intForce
            };
            SelectMetatypePriority frmPriority = await frmMain.DoThreadSafeFuncAsync(() => new SelectMetatypePriority(objCharacter), token).ConfigureAwait(false);
            try
            {
                string strRefusal = await frmPriority.ValidatePrioritySelectionAsync(objSelection, token).ConfigureAwait(false);
                if (string.IsNullOrEmpty(strRefusal))
                    strRefusal = await frmPriority.ApplyPrioritySelectionAsync(objSelection, token).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(strRefusal))
                    throw new AgentToolException(strRefusal);
            }
            finally
            {
                await frmPriority.DoThreadSafeAsync(x => x.Dispose(), CancellationToken.None).ConfigureAwait(false);
            }

            string strSkills = string.Join(", ", new[] { objSelection.Skill1, objSelection.Skill2, objSelection.Skill3 }.Where(x => !string.IsNullOrEmpty(x)));
            return objSelection.Metatype + (objSelection.Metavariant.IsEmptyGuid() ? string.Empty : " (" + objSelection.Metavariant + ")")
                                         + "; priorities heritage " + objSelection.Heritage + ", attributes " + objSelection.Attributes
                                         + ", talent " + objSelection.Talent + " (" + objSelection.TalentValue
                                         + (strSkills.Length > 0 ? ": " + strSkills : string.Empty) + "), skills " + objSelection.Skills
                                         + ", resources " + objSelection.Resources;
        }

        private static async Task<object> CreateCharacterAsync(JObject objArgs, CancellationToken token)
        {
            ChummerMainForm frmMain = Program.MainForm ?? throw new AgentToolException("Chummer's main window is not open.");
            RequiredString(objArgs, "metatype");
            (string strSettingsKey, CharacterSettings objSettings) = await FindSettingsEntryAsync(objArgs.Value<string>("settings"), token).ConfigureAwait(false);
            Character objCharacter = new Character();
            bool blnOpened = false;
            try
            {
                // What the build method dialog's OK does
                await objCharacter.SetSettingsKeyAsync(strSettingsKey, token).ConfigureAwait(false);
                await objCharacter.SetIgnoreRulesAsync(objArgs.Value<bool?>("ignore_rules") == true, token).ConfigureAwait(false);
                string strDescription = await ApplyMetatypeAndPrioritiesAsync(objCharacter, objArgs, token).ConfigureAwait(false);
                string strName = objArgs.Value<string>("name");
                if (!string.IsNullOrWhiteSpace(strName))
                    await objCharacter.SetNameAsync(strName.Trim(), token).ConfigureAwait(false);
                string strAlias = objArgs.Value<string>("alias");
                if (!string.IsNullOrWhiteSpace(strAlias))
                    await objCharacter.SetAliasAsync(strAlias.Trim(), token).ConfigureAwait(false);

                CharacterCreate frmCreate = await frmMain.OpenNewCharacterEditorAsync(objCharacter, token).ConfigureAwait(false);
                blnOpened = true;
                for (int i = 0; i < 600 && !frmCreate.IsFinishedInitializing; ++i)
                    await Task.Delay(50, token).ConfigureAwait(false);
                return await FinishEditAsync(frmCreate,
                                             "Created " + await objCharacter.GetCharacterNameAsync(token).ConfigureAwait(false) + " with "
                                             + await objSettings.GetCurrentDisplayNameAsync(token).ConfigureAwait(false) + ": " + strDescription
                                             + ". It is open in Chummer's editor; use get_character_summary to see the points to spend.",
                                             token).ConfigureAwait(false);
            }
            finally
            {
                // Once open, the editor owns the character
                if (!blnOpened)
                    await objCharacter.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static async Task<object> SetPrioritiesAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            string strDescription = await ApplyMetatypeAndPrioritiesAsync(frmCreate.CharacterObject, objArgs, token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate, "Now " + strDescription + ".", token).ConfigureAwait(false);
        }

        #endregion

        #region Saving

        private static async Task<object> SaveCharacterAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            if (frmCharacter is CharacterCreate frmCreate && await frmCreate.GetSaveAsCreatedAsync(token).ConfigureAwait(false))
                throw new AgentToolException("The 'Character Created' box is ticked in Chummer, so saving would finish character creation. "
                                             + "Ask the user to untick it or to save in Chummer themselves.");
            string strCurrentFile = await objCharacter.GetFileNameAsync(token).ConfigureAwait(false);
            string strPath = objArgs.Value<string>("path")?.Trim();
            if (string.IsNullOrEmpty(strPath))
            {
                if (!string.IsNullOrEmpty(strCurrentFile))
                {
                    if (!await frmCharacter.SaveCharacter(false, false, token).ConfigureAwait(false))
                        throw new AgentToolException("Chummer could not save to " + strCurrentFile + ".");
                    return new { result = "Saved to " + strCurrentFile + ".", file = strCurrentFile };
                }

                strPath = (await objCharacter.GetCharacterNameAsync(token).ConfigureAwait(false)).CleanForFileName();
            }

            // Agents running in WSL see Windows drives as /mnt/<drive>/
            if (strPath.Length > 6 && strPath.StartsWith("/mnt/", StringComparison.Ordinal) && char.IsLetter(strPath[5]) && strPath[6] == '/')
                strPath = char.ToUpperInvariant(strPath[5]) + ":" + strPath.Substring(6).Replace('/', Path.DirectorySeparatorChar);
            if (!Path.IsPathRooted(strPath))
                strPath = Path.Combine(Utils.GetStartupPath, "saves", strPath);
            if (!strPath.EndsWith(".chum5", StringComparison.OrdinalIgnoreCase) && !strPath.EndsWith(".chum5lz", StringComparison.OrdinalIgnoreCase))
                strPath += ".chum5";
            strPath = Path.GetFullPath(strPath);
            if (File.Exists(strPath) && objArgs.Value<bool?>("overwrite") != true
                                     && !string.Equals(strPath, strCurrentFile, StringComparison.OrdinalIgnoreCase))
                throw new AgentToolException(strPath + " already exists. Pass overwrite: true to replace it, or pick another path.");
            string strDirectory = Path.GetDirectoryName(strPath);
            if (!string.IsNullOrEmpty(strDirectory))
                Directory.CreateDirectory(strDirectory);
            if (!await frmCharacter.SaveCharacterToFileAsync(strPath, false, token).ConfigureAwait(false))
                throw new AgentToolException("Chummer could not save to " + strPath + ".");
            return new { result = "Saved to " + strPath + ".", file = strPath };
        }

        #endregion
    }
}
