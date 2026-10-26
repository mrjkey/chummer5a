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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Chummer.Agent
{
    public static partial class AgentTools
    {
        /// <summary>
        /// A kind of thing in Chummer's data files that the agent can search.
        /// </summary>
        private sealed class CatalogKind
        {
            public string File { get; }
            public string ItemPath { get; }
            public string[] SummaryFields { get; }
            public bool HasAvailability { get; }

            public CatalogKind(string strFile, string strItemPath, bool blnHasAvailability, params string[] astrSummaryFields)
            {
                File = strFile;
                ItemPath = strItemPath;
                HasAvailability = blnHasAvailability;
                SummaryFields = astrSummaryFields;
            }
        }

        private static readonly IReadOnlyDictionary<string, CatalogKind> s_DicCatalogKinds = new Dictionary<string, CatalogKind>
        {
            ["quality"] = new CatalogKind("qualities.xml", "qualities/quality", false, "karma", "limit"),
            ["spell"] = new CatalogKind("spells.xml", "spells/spell", false, "type", "range", "damage", "duration", "dv"),
            ["power"] = new CatalogKind("powers.xml", "powers/power", false, "points", "levels", "limit"),
            ["complexform"] = new CatalogKind("complexforms.xml", "complexforms/complexform", false, "target", "duration", "fv"),
            ["cyberware"] = new CatalogKind("cyberware.xml", "cyberwares/cyberware", true, "ess", "capacity", "rating", "avail", "cost"),
            ["bioware"] = new CatalogKind("bioware.xml", "biowares/bioware", true, "ess", "rating", "avail", "cost"),
            ["gear"] = new CatalogKind("gear.xml", "gears/gear", true, "rating", "avail", "cost"),
            ["weapon"] = new CatalogKind("weapons.xml", "weapons/weapon", true, "damage", "ap", "mode", "rc", "ammo", "accuracy", "avail", "cost"),
            ["armor"] = new CatalogKind("armor.xml", "armors/armor", true, "armor", "armorcapacity", "avail", "cost"),
            ["vehicle"] = new CatalogKind("vehicles.xml", "vehicles/vehicle", true, "handling", "speed", "accel", "body", "armor", "pilot", "sensor", "seats", "avail", "cost"),
            ["martialart"] = new CatalogKind("martialarts.xml", "martialarts/martialart", false, "cost"),
            ["metamagic"] = new CatalogKind("metamagic.xml", "metamagics/metamagic", false, "adept", "magician"),
            ["echo"] = new CatalogKind("echoes.xml", "echoes/echo", false),
            ["mentor"] = new CatalogKind("mentors.xml", "mentors/mentor", false, "advantage", "disadvantage"),
            ["tradition"] = new CatalogKind("traditions.xml", "traditions/tradition", false, "drain"),
            ["lifestyle"] = new CatalogKind("lifestyles.xml", "lifestyles/lifestyle", false, "cost", "dice", "multiplier"),
            ["critterpower"] = new CatalogKind("critterpowers.xml", "powers/power", false, "karma", "type", "action", "range", "duration"),
            ["program"] = new CatalogKind("programs.xml", "programs/program", true, "avail", "cost"),
            ["skill"] = new CatalogKind("skills.xml", "skills/skill", false, "attribute", "skillgroup", "default", "exotic"),
            ["knowledgeskill"] = new CatalogKind("skills.xml", "knowledgeskills/skill", false, "attribute")
        };

        private static IEnumerable<AgentTool> CreateCatalogTools()
        {
            string strKinds = string.Join(", ", s_DicCatalogKinds.Keys);
            yield return new AgentTool("search_catalog",
                "Search Chummer's rules data (only books enabled for the character). With legal_only (the default) results are filtered to what the "
                + "character can take right now: requirements/forbidden lists, metatype restrictions, chargen-only items and availability limits. "
                + "Results include id (pass it to get_catalog_item or the add_* tools), cost fields, source and page.",
                AgentTool.Schema(("kind!", "string", "One of: " + strKinds),
                                 ("query", "string", "Text to match in the name (optional)."),
                                 ("category", "string", "Exact category to restrict to (optional; the response lists valid categories)."),
                                 ("legal_only", "boolean", "Only return items the open character qualifies for. Default true."),
                                 ("limit", "integer", "Maximum results, default 40, max 200."),
                                 ("character", "string", CharacterArgDescription)),
                SearchCatalogAsync);
            yield return new AgentTool("get_catalog_item",
                "Full data for one catalog entry: bonuses it grants, required/forbidden conditions, costs, source and page, and whether the character currently qualifies.",
                AgentTool.Schema(("kind!", "string", "One of: " + strKinds),
                                 ("id!", "string", "Item id (GUID) from search_catalog, or its exact English name."),
                                 ("character", "string", CharacterArgDescription)),
                GetCatalogItemAsync);
            yield return new AgentTool("get_priority_table",
                "The priority table (A-E) for the character's settings, limited to the enabled books: what each priority gives for Heritage "
                + "(metatypes with special attribute points and karma cost), Talent (magic/resonance options and the free skills they grant), "
                + "Attributes, Skills and Resources. Works before a character exists, using the default settings (or `settings`).",
                AgentTool.Schema(("category", "string", "Only this category: Heritage, Talent, Attributes, Skills or Resources (optional)."),
                                 ("metatype", "string", "Only this metatype in the Heritage rows (optional)."),
                                 ("settings", "string", SettingsArgDescription),
                                 ("character", "string", CharacterArgDescription)),
                GetPriorityTableAsync);
            yield return new AgentTool("list_metatypes",
                "Metatypes with their attribute ranges (min/max/augmented max), karma cost and metavariants.",
                AgentTool.Schema(("category", "string", "Only this category, e.g. 'Metahuman', 'Metasapient', 'Shapeshifter' (optional)."),
                                 ("character", "string", CharacterArgDescription)),
                ListMetatypesAsync);
        }

        private static CatalogKind GetCatalogKind(JObject objArgs)
        {
            string strKind = objArgs.Value<string>("kind")?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(strKind) || !s_DicCatalogKinds.TryGetValue(strKind, out CatalogKind objKind))
                throw new AgentToolException("Unknown kind '" + strKind + "'. Use one of: " + string.Join(", ", s_DicCatalogKinds.Keys));
            return objKind;
        }

        /// <summary>
        /// The character the agent refers to, or null if no character is open and none was asked for.
        /// </summary>
        private static async Task<Character> TryGetCharacterAsync(JObject objArgs, CancellationToken token)
        {
            ChummerMainForm frmMain = Program.MainForm;
            if (frmMain == null)
                return null;
            if (string.IsNullOrWhiteSpace(objArgs.Value<string>("character"))
                && await frmMain.OpenCharacterEditorForms.GetCountAsync(token).ConfigureAwait(false) == 0)
                return null;
            return (await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false)).CharacterObject;
        }

        /// <summary>
        /// Root of a data file as the character sees it (their custom data), or as a new character with the default settings would.
        /// </summary>
        private static async Task<(XPathNavigator Root, CharacterSettings Settings)> LoadDataAsync(Character objCharacter, string strFile, CancellationToken token,
                                                                                                    CharacterSettings objSettings = null)
        {
            if (objCharacter != null)
            {
                XPathNavigator objRoot = await objCharacter.LoadDataXPathAsync(strFile, token: token).ConfigureAwait(false);
                return (objRoot.SelectSingleNode("/chummer"), await objCharacter.GetSettingsAsync(token).ConfigureAwait(false));
            }

            if (objSettings == null)
            {
                IReadOnlyDictionary<string, CharacterSettings> dicSettings = await SettingsManager.GetLoadedCharacterSettingsAsync(token).ConfigureAwait(false);
                if (!dicSettings.TryGetValue(GlobalSettings.DefaultCharacterSetting, out objSettings))
                    objSettings = dicSettings.Values.FirstOrDefault();
            }

            IReadOnlyList<string> lstCustomData = objSettings != null
                ? await objSettings.GetEnabledCustomDataDirectoryPathsAsync(token).ConfigureAwait(false)
                : null;
            XPathNavigator objGlobalRoot = await XmlManager.LoadXPathAsync(strFile, lstCustomData, token: token).ConfigureAwait(false);
            return (objGlobalRoot.SelectSingleNode("/chummer"), objSettings);
        }

        private static async Task<object> SearchCatalogAsync(JObject objArgs, CancellationToken token)
        {
            CatalogKind objKind = GetCatalogKind(objArgs);
            Character objCharacter = await TryGetCharacterAsync(objArgs, token).ConfigureAwait(false);
            bool blnLegalOnly = objArgs.Value<bool?>("legal_only") ?? true;
            int intLimit = Math.Max(1, Math.Min(200, objArgs.Value<int?>("limit") ?? 40));
            string strQuery = objArgs.Value<string>("query");
            string strCategory = objArgs.Value<string>("category");
            (XPathNavigator objRoot, CharacterSettings objSettings) = await LoadDataAsync(objCharacter, objKind.File, token).ConfigureAwait(false);

            List<string> lstFilters = new List<string>();
            if (objSettings != null)
            {
                string strBookFilter = await objSettings.BookXPathAsync(token: token).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(strBookFilter))
                    lstFilters.Add(strBookFilter);
            }

            if (!string.IsNullOrWhiteSpace(strCategory))
                lstFilters.Add("category = " + strCategory.CleanXPath());
            if (!string.IsNullOrWhiteSpace(strQuery))
                lstFilters.Add(CommonFunctions.GenerateSearchXPath(strQuery));
            string strXPath = objKind.ItemPath + (lstFilters.Count > 0 ? "[" + string.Join(" and ", lstFilters) + "]" : string.Empty);

            // Metatypes can restrict which qualities are available to them at all
            XPathNavigator objQualityRestriction = null;
            if (objCharacter != null && blnLegalOnly && objKind.File == "qualities.xml")
            {
                objQualityRestriction = (await objCharacter.GetNodeXPathAsync(false, token: token).ConfigureAwait(false))
                    ?.SelectSingleNode("qualityrestriction");
            }

            List<JObject> lstResults = new List<JObject>();
            int intMatches = 0;
            int intExcluded = 0;
            foreach (XPathNavigator objItem in objRoot.Select(strXPath))
            {
                token.ThrowIfCancellationRequested();
                string strName = objItem.SelectSingleNode("name")?.Value;
                if (string.IsNullOrEmpty(strName))
                    continue;
                if (objCharacter != null && blnLegalOnly)
                {
                    if (objQualityRestriction != null)
                    {
                        string strItemCategory = objItem.SelectSingleNode("category")?.Value?.ToLowerInvariant() ?? "*";
                        if (objQualityRestriction.SelectSingleNode(strItemCategory + "/quality[. = " + strName.CleanXPath() + "]") == null)
                        {
                            ++intExcluded;
                            continue;
                        }
                    }

                    if (!await objItem.RequirementsMetAsync(objCharacter, token: token).ConfigureAwait(false)
                        || (objKind.HasAvailability
                            && !await objItem.CheckAvailRestrictionAsync(objCharacter, token: token).ConfigureAwait(false)))
                    {
                        ++intExcluded;
                        continue;
                    }
                }

                ++intMatches;
                if (lstResults.Count < intLimit)
                    lstResults.Add(SummarizeCatalogItem(objItem, objKind));
            }

            return new
            {
                count = intMatches,
                returned = lstResults.Count,
                excludedAsIllegal = intExcluded,
                bookFilterApplied = objSettings != null,
                note = objCharacter == null && blnLegalOnly ? "No character is open, so results are not filtered by legality." : null,
                categories = objRoot.Select("categories/category").Cast<XPathNavigator>().Select(x => x.Value).Distinct().ToList(),
                items = lstResults.OrderBy(x => x.Value<string>("name"), StringComparer.OrdinalIgnoreCase).ToList()
            };
        }

        private static JObject SummarizeCatalogItem(XPathNavigator objItem, CatalogKind objKind)
        {
            JObject objReturn = new JObject
            {
                ["id"] = objItem.SelectSingleNode("id")?.Value,
                ["name"] = objItem.SelectSingleNode("name")?.Value
            };
            string strCategory = objItem.SelectSingleNode("category")?.Value;
            if (!string.IsNullOrEmpty(strCategory))
                objReturn["category"] = strCategory;
            foreach (string strField in objKind.SummaryFields)
            {
                XPathNavigator objField = objItem.SelectSingleNode(strField);
                if (objField != null)
                    objReturn[strField] = objField.HasChildren && objField.SelectChildren(XPathNodeType.Element).Count > 0
                        ? (JToken)NavigatorToJson(objField)
                        : objField.Value;
            }

            objReturn["source"] = objItem.SelectSingleNode("source")?.Value + " p." + objItem.SelectSingleNode("page")?.Value;
            return objReturn;
        }

        private static async Task<object> GetCatalogItemAsync(JObject objArgs, CancellationToken token)
        {
            CatalogKind objKind = GetCatalogKind(objArgs);
            string strId = objArgs.Value<string>("id");
            if (string.IsNullOrWhiteSpace(strId))
                throw new AgentToolException("`id` is required.");
            Character objCharacter = await TryGetCharacterAsync(objArgs, token).ConfigureAwait(false);
            (XPathNavigator objRoot, CharacterSettings _) = await LoadDataAsync(objCharacter, objKind.File, token).ConfigureAwait(false);
            XPathNavigator objItem = objRoot.TryGetNodeByNameOrId(objKind.ItemPath, strId.Trim())
                                     ?? throw new AgentToolException("No " + objArgs.Value<string>("kind") + " with id or name '" + strId + "'.");
            JObject objReturn = new JObject { ["data"] = NavigatorToJson(objItem) };
            if (objCharacter != null)
            {
                objReturn["characterQualifies"] = await objItem.RequirementsMetAsync(objCharacter, token: token).ConfigureAwait(false);
                if (objKind.HasAvailability)
                    objReturn["availabilityAllowed"] = await objItem.CheckAvailRestrictionAsync(objCharacter, token: token).ConfigureAwait(false);
            }

            return objReturn;
        }

        private static async Task<object> GetPriorityTableAsync(JObject objArgs, CancellationToken token)
        {
            Character objCharacter = await TryGetCharacterAsync(objArgs, token).ConfigureAwait(false);
            CharacterSettings objWantedSettings = objCharacter == null ? await FindSettingsAsync(objArgs.Value<string>("settings"), token).ConfigureAwait(false) : null;
            (XPathNavigator objRoot, CharacterSettings objSettings) = await LoadDataAsync(objCharacter, "priorities.xml", token, objWantedSettings).ConfigureAwait(false);
            (XPathNavigator objMetatypesRoot, CharacterSettings _) = await LoadDataAsync(objCharacter, "metatypes.xml", token, objWantedSettings).ConfigureAwait(false);
            string strPriorityTable = objSettings != null ? await objSettings.GetPriorityTableAsync(token).ConfigureAwait(false) : "Standard";
            string strBookXPath = objSettings != null ? await objSettings.BookXPathAsync(token: token).ConfigureAwait(false) : string.Empty;
            string strFilter = "(not(prioritytable) or prioritytable = " + strPriorityTable.CleanXPath() + ")";
            string strCategory = objArgs.Value<string>("category");
            if (!string.IsNullOrWhiteSpace(strCategory))
                strFilter += " and category = " + strCategory.Trim().CleanXPath();
            string strMetatype = objArgs.Value<string>("metatype")?.Trim();
            Dictionary<string, List<JToken>> dicByCategory = new Dictionary<string, List<JToken>>();
            foreach (XPathNavigator objPriority in objRoot.Select("priorities/priority[" + strFilter + "]"))
            {
                token.ThrowIfCancellationRequested();
                string strLoopCategory = objPriority.SelectSingleNode("category")?.Value ?? "Other";
                if (!dicByCategory.TryGetValue(strLoopCategory, out List<JToken> lstEntries))
                    dicByCategory[strLoopCategory] = lstEntries = new List<JToken>();
                JObject objEntry = new JObject { ["priority"] = objPriority.SelectSingleNode("value")?.Value };
                switch (strLoopCategory)
                {
                    case "Heritage":
                        objEntry["metatypes"] = DescribeHeritageRow(objPriority, objMetatypesRoot, strBookXPath, strMetatype);
                        break;
                    case "Talent":
                        objEntry["talents"] = DescribeTalentRow(objPriority);
                        break;
                    default:
                        foreach (XPathNavigator objField in objPriority.SelectChildren(XPathNodeType.Element))
                        {
                            switch (objField.Name)
                            {
                                case "id":
                                case "name":
                                case "value":
                                case "category":
                                case "prioritytable":
                                case "gameplayoption":
                                    continue;
                            }

                            objEntry[objField.Name] = objField.HasChildren && objField.SelectChildren(XPathNodeType.Element).Count > 0
                                ? (JToken)NavigatorToJson(objField)
                                : objField.Value;
                        }

                        break;
                }

                lstEntries.Add(objEntry);
            }

            return new
            {
                priorityTable = strPriorityTable,
                settings = objSettings != null ? await objSettings.GetNameAsync(token).ConfigureAwait(false) : null,
                categories = dicByCategory
            };
        }

        /// <summary>
        /// The metatypes a Heritage priority allows (only those in the enabled books), with special attribute points and karma cost.
        /// </summary>
        private static JArray DescribeHeritageRow(XPathNavigator objPriority, XPathNavigator objMetatypesRoot, string strBookXPath, string strOnlyMetatype)
        {
            JArray objReturn = new JArray();
            foreach (XPathNavigator objMetatype in objPriority.Select("metatypes/metatype"))
            {
                string strName = objMetatype.SelectSingleNode("name")?.Value;
                if (string.IsNullOrEmpty(strName)
                    || (!string.IsNullOrEmpty(strOnlyMetatype) && !string.Equals(strName, strOnlyMetatype, StringComparison.OrdinalIgnoreCase)))
                    continue;
                XPathNavigator objData = objMetatypesRoot.TryGetNodeByNameOrId("metatypes/metatype", strName, strBookXPath);
                if (objData == null)
                    continue;
                JObject objEntry = new JObject
                {
                    ["name"] = strName,
                    ["category"] = objData.SelectSingleNode("category")?.Value,
                    ["specialPoints"] = objMetatype.SelectSingleNode("value")?.ValueAsInt ?? 0,
                    ["karma"] = objMetatype.SelectSingleNode("karma")?.ValueAsInt ?? 0
                };
                JObject objMetavariants = new JObject();
                foreach (XPathNavigator objMetavariant in objMetatype.Select("metavariants/metavariant"))
                {
                    string strMetavariant = objMetavariant.SelectSingleNode("name")?.Value;
                    if (string.IsNullOrEmpty(strMetavariant)
                        || objData.TryGetNodeByNameOrId("metavariants/metavariant", strMetavariant, strBookXPath) == null)
                        continue;
                    objMetavariants[strMetavariant] = new JObject
                    {
                        ["specialPoints"] = objMetavariant.SelectSingleNode("value")?.ValueAsInt ?? 0,
                        ["karma"] = objMetavariant.SelectSingleNode("karma")?.ValueAsInt ?? 0
                    };
                }

                if (objMetavariants.Count > 0)
                    objEntry["metavariants"] = objMetavariants;
                objReturn.Add(objEntry);
            }

            return objReturn;
        }

        /// <summary>
        /// The talents a Talent priority offers, with what each grants and which metatypes may (not) take it.
        /// </summary>
        private static JArray DescribeTalentRow(XPathNavigator objPriority)
        {
            JArray objReturn = new JArray();
            foreach (XPathNavigator objTalent in objPriority.Select("talents/talent"))
            {
                JObject objEntry = new JObject
                {
                    ["value"] = objTalent.SelectSingleNode("value")?.Value,
                    ["name"] = objTalent.SelectSingleNode("name")?.Value
                };
                foreach (string strField in new[] { "magic", "resonance", "depth", "spells", "cfp", "specialattribpoints" })
                {
                    XPathNavigator objField = objTalent.SelectSingleNode(strField);
                    if (objField != null)
                        objEntry[strField] = objField.Value;
                }

                List<string> lstQualities = objTalent.Select("qualities/quality").Cast<XPathNavigator>().Select(x => x.Value).ToList();
                if (lstQualities.Count > 0)
                    objEntry["qualities"] = new JArray(lstQualities);
                XPathNavigator objCount = objTalent.SelectSingleNode("skillqty") ?? objTalent.SelectSingleNode("skillgroupqty");
                if (objCount != null)
                {
                    bool blnGroups = objTalent.SelectSingleNode("skillgroupqty") != null;
                    JObject objFreeSkills = new JObject
                    {
                        ["count"] = objCount.Value,
                        ["rating"] = (objTalent.SelectSingleNode("skillval") ?? objTalent.SelectSingleNode("skillgroupval"))?.Value,
                        ["type"] = (objTalent.SelectSingleNode("skilltype") ?? objTalent.SelectSingleNode("skillgrouptype"))?.Value ?? "active",
                        ["skillGroups"] = blnGroups
                    };
                    List<string> lstChoices = objTalent.Select("skillchoices/skill | skillgroupchoices/skillgroup").Cast<XPathNavigator>()
                                                       .Select(x => x.Value).ToList();
                    if (lstChoices.Count > 0)
                        objFreeSkills["choices"] = new JArray(lstChoices);
                    objEntry["freeSkills"] = objFreeSkills;
                }

                foreach (string strCondition in new[] { "required", "forbidden" })
                {
                    List<string> lstConditions = objTalent.Select(strCondition + "/oneof/*").Cast<XPathNavigator>()
                                                          .Select(x => x.Name + ": " + x.Value).ToList();
                    if (lstConditions.Count > 0)
                        objEntry[strCondition + "OneOf"] = new JArray(lstConditions);
                }

                objReturn.Add(objEntry);
            }

            return objReturn;
        }

        private static async Task<object> ListMetatypesAsync(JObject objArgs, CancellationToken token)
        {
            Character objCharacter = await TryGetCharacterAsync(objArgs, token).ConfigureAwait(false);
            (XPathNavigator objRoot, CharacterSettings objSettings) = await LoadDataAsync(objCharacter, "metatypes.xml", token).ConfigureAwait(false);
            List<string> lstFilters = new List<string>();
            if (objSettings != null)
                lstFilters.Add(await objSettings.BookXPathAsync(token: token).ConfigureAwait(false));
            string strCategory = objArgs.Value<string>("category");
            if (!string.IsNullOrWhiteSpace(strCategory))
                lstFilters.Add("category = " + strCategory.Trim().CleanXPath());
            lstFilters.RemoveAll(string.IsNullOrEmpty);
            string strXPath = "metatypes/metatype" + (lstFilters.Count > 0 ? "[" + string.Join(" and ", lstFilters) + "]" : string.Empty);
            string[] astrAttributes = { "bod", "agi", "rea", "str", "cha", "int", "log", "wil", "ini", "edg", "mag", "res", "dep", "ess" };
            List<object> lstReturn = new List<object>();
            foreach (XPathNavigator objMetatype in objRoot.Select(strXPath))
            {
                token.ThrowIfCancellationRequested();
                Dictionary<string, string> dicAttributes = new Dictionary<string, string>();
                foreach (string strAttribute in astrAttributes)
                {
                    string strMin = objMetatype.SelectSingleNode(strAttribute + "min")?.Value;
                    string strMax = objMetatype.SelectSingleNode(strAttribute + "max")?.Value;
                    string strAug = objMetatype.SelectSingleNode(strAttribute + "aug")?.Value;
                    if (string.IsNullOrEmpty(strMax) || strMax == "0")
                        continue;
                    dicAttributes[strAttribute.ToUpperInvariant()] = strMin + "-" + strMax + " (aug " + strAug + ")";
                }

                lstReturn.Add(new
                {
                    id = objMetatype.SelectSingleNode("id")?.Value,
                    name = objMetatype.SelectSingleNode("name")?.Value,
                    category = objMetatype.SelectSingleNode("category")?.Value,
                    karma = objMetatype.SelectSingleNode("karma")?.Value,
                    attributes = dicAttributes,
                    metavariants = objMetatype.Select("metavariants/metavariant/name").Cast<XPathNavigator>().Select(x => x.Value).ToList(),
                    source = objMetatype.SelectSingleNode("source")?.Value + " p." + objMetatype.SelectSingleNode("page")?.Value
                });
            }

            return lstReturn;
        }

        /// <summary>
        /// Convert a data node to JSON (children only, attributes kept with an @ prefix).
        /// </summary>
        internal static JObject NavigatorToJson(XPathNavigator objNode)
        {
            XmlDocument objDocument = new XmlDocument { XmlResolver = null };
            objDocument.LoadXml(objNode.OuterXml);
            string strJson = JsonConvert.SerializeXmlNode(objDocument.DocumentElement, Newtonsoft.Json.Formatting.None, true);
            JToken objToken = JToken.Parse(strJson);
            return objToken as JObject ?? new JObject { ["value"] = objToken };
        }
    }
}
