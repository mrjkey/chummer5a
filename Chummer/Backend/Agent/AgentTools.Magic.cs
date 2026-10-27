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
using Chummer.Backend.Enums;
using Chummer.Backend.Equipment;
using Chummer.Backend.Uniques;
using Newtonsoft.Json.Linq;

namespace Chummer.Agent
{
    public static partial class AgentTools
    {
        private static IEnumerable<AgentTool> CreateMagicTools()
        {
            yield return new AgentTool("set_tradition",
                "Choose the magician's tradition (search_catalog kind='tradition') or the technomancer's stream (kind='stream'), as the editor's "
                + "tradition list does. It sets the drain attributes and the spirit types the character can summon. The 'Custom' tradition "
                + "(Street Grimoire) takes its own name, drain attributes and a spirit for each of the five types; a wrong choice lists the options.",
                AgentTool.Schema(("id!", "string", "Tradition or stream id from search_catalog, or its exact English name."),
                                 ("name", "string", "Custom tradition only: its name."),
                                 ("drain", "string", "Custom tradition only: drain attributes, e.g. '{WIL} + {LOG}'."),
                                 ("spirits", "object", "Custom tradition only: {combat, detection, health, illusion, manipulation}, each a spirit name, "
                                                       + "e.g. 'Spirit of Fire'."),
                                 ("character", "string", CharacterArgDescription)),
                SetTraditionAsync);
            yield return new AgentTool("set_mystic_adept_power_points",
                "Split a mystic adept's Magic between spellcasting and adept powers, as the magic tab's power point box does: each point bought gives "
                + "one power point and costs karma. Only for mystic adepts whose settings don't use a second Magic attribute.",
                AgentTool.Schema(("points!", "integer", "Points of Magic that go to the adept side (power points), from 0 to the character's Magic."),
                                 ("character", "string", CharacterArgDescription)),
                SetMysticAdeptPowerPointsAsync);
            yield return new AgentTool("add_mentor_spirit",
                "Take the Mentor Spirit quality with a mentor (search_catalog kind='mentor') and its bonus choices, with no dialog. "
                + "Most mentors offer a Magician and an Adept choice; only the ones that fit the character are allowed, and the error lists them.",
                AgentTool.Schema(("mentor!", "string", "Mentor id or exact English name, e.g. 'Dog', 'Wise Warrior'."),
                                 ("choice", "string", "The mentor's first bonus choice, by name (required when there is more than one)."),
                                 ("choice2", "string", "The mentor's second bonus choice, for mystic adepts and mentors that offer two."),
                                 ("character", "string", CharacterArgDescription)),
                AddMentorSpiritAsync);
            yield return new AgentTool("add_complex_form",
                "Learn a complex form (search_catalog kind='complexform') for a technomancer. Enforces requirements and the RES x 2 limit.",
                AgentTool.Schema(("id!", "string", "Complex form id from search_catalog, or its exact English name."),
                                 ("character", "string", CharacterArgDescription)),
                AddComplexFormAsync);
            yield return new AgentTool("add_spirit",
                "Bind a spirit (magicians) or register a sprite (technomancers), as the editor's Add Spirit/Add Sprite does, then set its type, force "
                + "and services. The types allowed come from the tradition or stream; the error lists them.",
                AgentTool.Schema(("kind", "string", "'spirit' (default) or 'sprite'."),
                                 ("type!", "string", "Spirit or sprite type, e.g. 'Spirit of Air', 'Courier Sprite'."),
                                 ("force", "integer", "Force (or level). Default the highest allowed."),
                                 ("services", "integer", "Services (or tasks) owed. Default 1."),
                                 ("character", "string", CharacterArgDescription)),
                AddSpiritAsync);
            yield return new AgentTool("set_spirit_details",
                "Change a bound spirit or registered sprite the character has, as its panel on the Spirits/Sprites tab does: its type, force or level, "
                + "services or tasks owed, bound/registered and fettered boxes, its own name, and notes. Only the fields passed change. Works in creation "
                + "and career mode.",
                AgentTool.Schema(("id!", "string", "Spirit or sprite id from get_character_summary."),
                                 ("type", "string", "Spirit or sprite type the character can have, e.g. 'Spirit of Air'."),
                                 ("force", "integer", "Force (spirits) or level (sprites), up to the character's maximum."),
                                 ("services", "integer", "Services (spirits) or tasks (sprites) owed."),
                                 ("bound", "boolean", "Bound (spirits) or registered (sprites)."),
                                 ("fettered", "boolean", "Fettered (spirits only, where allowed)."),
                                 ("name", "string", "The spirit's own name."),
                                 ("notes", "string", "Notes. Empty clears them."),
                                 ("character", "string", CharacterArgDescription)),
                SetSpiritDetailsAsync);
            yield return new AgentTool("bond_focus",
                "Bond or unbond a focus the character has, as ticking it in the Magic tab's foci list does. Bonding is refused past the character's "
                + "limits (number of foci up to Magic, total force up to Magic x 5). Bonded foci give their bonuses. Bonding karma counts toward the build.",
                AgentTool.Schema(("id!", "string", "Focus gear (or stacked focus) id from list_items."),
                                 ("bonded", "boolean", "True to bond (default), false to unbond."),
                                 ("forced_value", "string", "Answer for the choice bonding asks: most foci are attuned to a tradition (default: the character's "
                                                            + "own); summoning, banishing and binding foci to a spirit type."),
                                 ("character", "string", CharacterArgDescription)),
                BondFocusAsync);
            yield return new AgentTool("add_initiation",
                "Raise the initiate grade (or the submersion grade for technomancers) by one, as the editor's Add Initiation/Submersion does. "
                + "Each grade costs karma and allows one metamagic or echo.",
                AgentTool.Schema(("group", "boolean", "Initiation through a magical group (cheaper)."),
                                 ("ordeal", "boolean", "An ordeal was taken (cheaper)."),
                                 ("schooling", "boolean", "Schooling was taken (cheaper)."),
                                 ("character", "string", CharacterArgDescription)),
                AddInitiationAsync);
            yield return new AgentTool("add_art",
                "Learn a magical art (search_catalog kind='art', e.g. 'Centering', 'Quickening') at an initiation grade, as the editor does. "
                + "Most metamagics need their art first. With `kind` it learns an enchantment (kind='enchantment', Street Grimoire) or an adept's "
                + "power enhancement (kind='enhancement', Street Grimoire) instead, as the initiation tree's Add Enchantment/Add Enhancement do; an "
                + "enhancement goes on its power when the character has it.",
                AgentTool.Schema(("id!", "string", "Art, enchantment or enhancement id from search_catalog, or its exact English name."),
                                 ("kind", "string", "'art' (default), 'enchantment' or 'enhancement'."),
                                 ("grade", "integer", "Initiation grade to learn it at. Default the highest grade."),
                                 ("character", "string", CharacterArgDescription)),
                AddArtAsync);
            yield return new AgentTool("add_metamagic",
                "Learn a metamagic (search_catalog kind='metamagic') or echo (kind='echo') at an initiation or submersion grade, as the editor does. "
                + "Most metamagics need their art first (add_art).",
                AgentTool.Schema(("id!", "string", "Metamagic or echo id from search_catalog, or its exact English name."),
                                 ("grade", "integer", "Grade to learn it at. Default the highest grade."),
                                 ("forced_value", "string", ForcedValueDescription),
                                 ("character", "string", CharacterArgDescription)),
                AddMetamagicAsync);
        }

        private static async Task<object> SetTraditionAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            bool blnStream = await objCharacter.GetRESEnabledAsync(token).ConfigureAwait(false);
            // Chummer wants every awakened or emerged character to have one, adepts included
            if (!await objCharacter.GetShouldHaveTraditionAsync(token).ConfigureAwait(false))
                throw new AgentToolException("Only awakened and emerged characters have a tradition or stream.");
            string strFile = blnStream ? "streams.xml" : "traditions.xml";
            XPathNavigator xmlTradition = (await objCharacter.LoadDataXPathAsync(strFile, token: token).ConfigureAwait(false))
                                          .TryGetNodeByNameOrId("/chummer/traditions/tradition", RequiredString(objArgs, "id"),
                                                                await (await objCharacter.GetSettingsAsync(token).ConfigureAwait(false))
                                                                      .BookXPathAsync(token: token).ConfigureAwait(false))
                                          ?? throw new AgentToolException("No " + (blnStream ? "stream" : "tradition") + " '" + objArgs.Value<string>("id")
                                                                          + "' in the enabled books. Use search_catalog kind='" + (blnStream ? "stream" : "tradition") + "'.");
            string strId = xmlTradition.SelectSingleNodeAndCacheExpression("id", token)?.Value;
            bool blnCustom = string.Equals(strId, Tradition.CustomMagicalTraditionGuidString, StringComparison.OrdinalIgnoreCase);
            string strCustomName = objArgs.Value<string>("name")?.Trim();
            string strDrain = objArgs.Value<string>("drain")?.Trim();
            JObject objSpirits = objArgs["spirits"] as JObject;
            string[] astrSpiritKinds = { "combat", "detection", "health", "illusion", "manipulation" };
            string[] astrSpirits = new string[5];
            if (!blnCustom)
            {
                if (!string.IsNullOrEmpty(strCustomName) || !string.IsNullOrEmpty(strDrain) || objSpirits != null)
                    throw new AgentToolException("`name`, `drain` and `spirits` are only for the Custom tradition.");
            }
            else
            {
                // The choices the tradition panel's drain and spirit boxes offer for a custom tradition
                XPathNavigator xmlTraditionsRoot = (await objCharacter.LoadDataXPathAsync("traditions.xml", token: token).ConfigureAwait(false))
                    .SelectSingleNodeAndCacheExpression("/chummer", token);
                List<string> lstDrains = CharacterCreate.GetDrainAttributeChoices(xmlTraditionsRoot, token).Select(x => x.Value?.ToString())
                                                        .Where(x => !string.IsNullOrEmpty(x)).ToList();
                List<string> lstSpirits = (await frmCreate.GetTraditionSpiritChoicesAsync(xmlTraditionsRoot, token).ConfigureAwait(false))
                                          .Select(x => x.Value?.ToString()).Where(x => !string.IsNullOrEmpty(x)).ToList();
                if (string.IsNullOrEmpty(strCustomName))
                    throw new AgentToolException("A custom tradition needs a `name`.");
                strDrain = lstDrains.Find(x => string.Equals(x.Replace(" ", string.Empty), (strDrain ?? string.Empty).Replace(" ", string.Empty),
                                                             StringComparison.OrdinalIgnoreCase))
                           ?? throw new AgentToolException("A custom tradition needs `drain`, one of: " + string.Join(", ", lstDrains) + ".");
                for (int i = 0; i < 5; ++i)
                {
                    string strWanted = objSpirits?.Value<string>(astrSpiritKinds[i])?.Trim();
                    astrSpirits[i] = lstSpirits.Find(x => string.Equals(x, strWanted, StringComparison.OrdinalIgnoreCase))
                                     ?? throw new AgentToolException("A custom tradition needs `spirits." + astrSpiritKinds[i] + "`, one of: "
                                                                     + string.Join(", ", lstSpirits) + ".");
                }
            }

            await frmCreate.SetTraditionAsync(strId, token).ConfigureAwait(false);
            Tradition objTradition = await objCharacter.GetMagicTraditionAsync(token).ConfigureAwait(false);
            if (!string.Equals(await objTradition.GetSourceIDStringAsync(token).ConfigureAwait(false), strId, StringComparison.OrdinalIgnoreCase))
                throw new AgentToolException("Chummer did not take that " + (blnStream ? "stream" : "tradition") + ".");
            if (blnCustom)
            {
                // What the custom tradition's name, drain and spirit boxes set
                await objTradition.SetNameAsync(strCustomName, token).ConfigureAwait(false);
                await objTradition.SetDrainExpressionAsync(strDrain, token).ConfigureAwait(false);
                await objTradition.SetSpiritCombatAsync(astrSpirits[0], token).ConfigureAwait(false);
                await objTradition.SetSpiritDetectionAsync(astrSpirits[1], token).ConfigureAwait(false);
                await objTradition.SetSpiritHealthAsync(astrSpirits[2], token).ConfigureAwait(false);
                await objTradition.SetSpiritIllusionAsync(astrSpirits[3], token).ConfigureAwait(false);
                await objTradition.SetSpiritManipulationAsync(astrSpirits[4], token).ConfigureAwait(false);
            }
            using (new FetchSafelyFromSafeObjectPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstTypes))
            {
                await Spirit.FillSpiritTypeChoicesAsync(objCharacter, objTradition, blnStream ? SpiritType.Sprite : SpiritType.Spirit, lstTypes, token)
                            .ConfigureAwait(false);
                return await FinishEditAsync(frmCreate,
                                             (blnStream ? "Stream" : "Tradition") + " is now " + await objTradition.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                             + " (drain " + await objTradition.GetDrainExpressionAsync(token).ConfigureAwait(false) + "). "
                                             + (blnStream ? "Sprite" : "Spirit") + " types: " + string.Join(", ", lstTypes.Select(x => x.Value)) + ".",
                                             token).ConfigureAwait(false);
            }
        }

        private static async Task<object> AddMentorSpiritAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strBookXPath = await (await objCharacter.GetSettingsAsync(token).ConfigureAwait(false)).BookXPathAsync(token: token).ConfigureAwait(false);
            XPathNavigator xmlMentor = (await objCharacter.LoadDataXPathAsync("mentors.xml", token: token).ConfigureAwait(false))
                                       .TryGetNodeByNameOrId("/chummer/mentors/mentor", RequiredString(objArgs, "mentor"), strBookXPath)
                                       ?? throw new AgentToolException("No mentor spirit '" + objArgs.Value<string>("mentor")
                                                                       + "' in the enabled books. Use search_catalog kind='mentor'.");
            string strMentor = xmlMentor.SelectSingleNodeAndCacheExpression("name", token)?.Value;

            // The same choices the Select Mentor Spirit dialog offers this character
            bool blnAdept = await objCharacter.GetAdeptEnabledAsync(token).ConfigureAwait(false);
            bool blnMagician = await objCharacter.GetMagicianEnabledAsync(token).ConfigureAwait(false);
            List<string> lstChoice1 = new List<string>();
            List<string> lstChoice2 = new List<string>();
            foreach (XPathNavigator xmlChoice in xmlMentor.Select("choices/choice"))
            {
                string strName = xmlChoice.SelectSingleNodeAndCacheExpression("name", token)?.Value ?? string.Empty;
                if ((blnAdept || !strName.StartsWith("Adept:", StringComparison.Ordinal))
                    && (blnMagician || !strName.StartsWith("Magician:", StringComparison.Ordinal)))
                {
                    if (xmlChoice.SelectSingleNodeAndCacheExpression("@set", token)?.Value == "2")
                        lstChoice2.Add(strName);
                    else
                        lstChoice1.Add(strName);
                }
            }

            string PickChoice(List<string> lstOptions, string strArg)
            {
                string strWanted = objArgs.Value<string>(strArg)?.Trim();
                if (lstOptions.Count == 0)
                    return string.Empty;
                if (string.IsNullOrEmpty(strWanted))
                {
                    if (lstOptions.Count == 1)
                        return lstOptions[0];
                    throw new AgentToolException(strMentor + " needs `" + strArg + "`. Options: " + string.Join("; ", lstOptions) + ".");
                }

                return lstOptions.Find(x => string.Equals(x, strWanted, StringComparison.OrdinalIgnoreCase))
                       ?? lstOptions.Find(x => x.IndexOf(strWanted, StringComparison.OrdinalIgnoreCase) >= 0)
                       ?? throw new AgentToolException("'" + strWanted + "' is not one of " + strMentor + "'s choices for this character. Options: "
                                                       + string.Join("; ", lstOptions) + ".");
            }

            string strChoice1 = PickChoice(lstChoice1, "choice");
            string strChoice2 = PickChoice(lstChoice2, "choice2");
            XmlNode xmlQuality = await GetDataNodeAsync(objCharacter, "qualities.xml", "/chummer/qualities/quality", "Mentor Spirit", token).ConfigureAwait(false);
            await EnsureRequirementsMetAsync(xmlQuality, objCharacter, token).ConfigureAwait(false);
            // "Mentor|Choice 1|Choice 2" tells the mentor selection to skip its dialog
            (int intAdded, string strRefusal) = await frmCreate.AddQualityFromNodeAsync(xmlQuality, xmlQuality["id"]?.InnerText, 1, false,
                                                                                        strMentor + "|" + strChoice1 + "|" + strChoice2, false, token)
                                                               .ConfigureAwait(false);
            if (intAdded == 0)
                throw new AgentToolException(!string.IsNullOrEmpty(strRefusal) ? strRefusal : "The Mentor Spirit quality was not added.");
            return await FinishEditAsync(frmCreate,
                                         "Took " + strMentor + " as mentor spirit"
                                         + (string.IsNullOrEmpty(strChoice1) ? string.Empty : " (" + strChoice1
                                             + (string.IsNullOrEmpty(strChoice2) ? string.Empty : "; " + strChoice2) + ")") + ".",
                                         token).ConfigureAwait(false);
        }

        private static async Task<object> AddComplexFormAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            if (!await objCharacter.GetRESEnabledAsync(token).ConfigureAwait(false))
                throw new AgentToolException("Only technomancers learn complex forms.");
            XmlNode xmlForm = await GetDataNodeAsync(objCharacter, "complexforms.xml", "/chummer/complexforms/complexform", RequiredString(objArgs, "id"), token)
                .ConfigureAwait(false);
            await EnsureBookEnabledAsync(xmlForm, objCharacter, token).ConfigureAwait(false);
            if (await frmCreate.IsAtComplexFormLimitAsync(token).ConfigureAwait(false))
                throw new AgentToolException("The character already knows as many complex forms as they may (RES x 2).");
            await EnsureRequirementsMetAsync(xmlForm, objCharacter, token).ConfigureAwait(false);
            ComplexForm objForm = await frmCreate.AddComplexFormFromNodeAsync(xmlForm, token).ConfigureAwait(false)
                                  ?? throw new AgentToolException("The complex form was not added (a choice it needed was cancelled).");
            return await FinishEditAsync(frmCreate, "Learned " + await objForm.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " (id " + objForm.InternalId + ").", token).ConfigureAwait(false);
        }

        private static async Task<object> AddSpiritAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            bool blnSprite;
            switch (objArgs.Value<string>("kind")?.Trim().ToLowerInvariant())
            {
                case null:
                case "":
                case "spirit":
                    blnSprite = false;
                    if (!await objCharacter.GetMagicianEnabledAsync(token).ConfigureAwait(false))
                        throw new AgentToolException("Only magicians and mystic adepts bind spirits.");
                    break;
                case "sprite":
                    blnSprite = true;
                    if (!await objCharacter.GetRESEnabledAsync(token).ConfigureAwait(false))
                        throw new AgentToolException("Only technomancers register sprites.");
                    break;
                default:
                    throw new AgentToolException("`kind` must be 'spirit' or 'sprite'.");
            }

            string strType = RequiredString(objArgs, "type");
            string strMatch;
            using (new FetchSafelyFromSafeObjectPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstTypes))
            {
                await Spirit.FillSpiritTypeChoicesAsync(objCharacter, await objCharacter.GetMagicTraditionAsync(token).ConfigureAwait(false),
                                                        blnSprite ? SpiritType.Sprite : SpiritType.Spirit, lstTypes, token).ConfigureAwait(false);
                strMatch = lstTypes.Select(x => x.Value.ToString())
                                   .FirstOrDefault(x => string.Equals(x, strType, StringComparison.OrdinalIgnoreCase))
                           ?? throw new AgentToolException("'" + strType + "' is not a " + (blnSprite ? "sprite" : "spirit") + " type this character can have. Options: "
                                                           + (lstTypes.Count > 0
                                                               ? string.Join(", ", lstTypes.Select(x => x.Value))
                                                               : "none yet; choose a " + (blnSprite ? "stream" : "tradition") + " with set_tradition first")
                                                           + ".");
            }

            int intMaximum = blnSprite
                ? await objCharacter.GetMaxSpriteLevelAsync(token).ConfigureAwait(false)
                : await objCharacter.GetMaxSpiritForceAsync(token).ConfigureAwait(false);
            int intForce = OptionalInt(objArgs, "force") ?? intMaximum;
            if (intForce < 1 || (intForce > intMaximum && !await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false)))
                throw new AgentToolException((blnSprite ? "Level" : "Force") + " must be between 1 and " + intMaximum.ToString(GlobalSettings.InvariantCultureInfo) + ".");
            int intServices = Math.Max(1, OptionalInt(objArgs, "services") ?? 1);

            Spirit objSpirit = blnSprite
                ? await frmCreate.AddSprite(token, false).ConfigureAwait(false)
                  ?? throw new AgentToolException("The character already has as many registered sprites as they may.")
                : await frmCreate.AddSpirit(token).ConfigureAwait(false);
            try
            {
                await objSpirit.SetNameAsync(strMatch, token).ConfigureAwait(false);
                await objSpirit.SetForceAsync(intForce, token).ConfigureAwait(false);
                await objSpirit.SetServicesOwedAsync(intServices, token).ConfigureAwait(false);
            }
            catch
            {
                await objCharacter.Spirits.RemoveAsync(objSpirit, CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            bool blnBound = await objSpirit.GetBoundAsync(token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate,
                                         "Added " + strMatch + " (" + (blnSprite ? "level " : "force ") + intForce.ToString(GlobalSettings.InvariantCultureInfo)
                                         + ", " + intServices.ToString(GlobalSettings.InvariantCultureInfo) + (blnSprite ? " tasks" : " services") + ", id "
                                         + objSpirit.InternalId + ")" + (blnBound
                                             ? "."
                                             : ". It is unbound because the character is at their bound spirit limit."),
                                         token).ConfigureAwait(false);
        }

        private static async Task<object> SetSpiritDetailsAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            string strId = RequiredString(objArgs, "id");
            Spirit objSpirit = null;
            foreach (Spirit objLoop in await objCharacter.Spirits.ToListAsync(token).ConfigureAwait(false))
            {
                if (string.Equals(objLoop.InternalId, strId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(await objLoop.GetNameAsync(token).ConfigureAwait(false), strId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(await objLoop.GetCritterNameAsync(token).ConfigureAwait(false), strId, StringComparison.OrdinalIgnoreCase))
                {
                    objSpirit = objLoop;
                    break;
                }
            }

            if (objSpirit == null)
                throw new AgentToolException("The character has no spirit or sprite '" + strId + "'. Use get_character_summary for ids.");
            bool blnSprite = await objSpirit.GetEntityTypeAsync(token).ConfigureAwait(false) == SpiritType.Sprite;
            bool blnIgnoreRules = await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false);
            List<string> lstChanged = new List<string>();

            // The panel's boxes, in the order the editor binds them
            string strType = objArgs.Value<string>("type")?.Trim();
            if (!string.IsNullOrEmpty(strType))
            {
                using (new FetchSafelyFromSafeObjectPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstTypes))
                {
                    await Spirit.FillSpiritTypeChoicesAsync(objCharacter, await objCharacter.GetMagicTraditionAsync(token).ConfigureAwait(false),
                                                            blnSprite ? SpiritType.Sprite : SpiritType.Spirit, lstTypes, token).ConfigureAwait(false);
                    string strMatch = lstTypes.Select(x => x.Value.ToString()).FirstOrDefault(x => string.Equals(x, strType, StringComparison.OrdinalIgnoreCase))
                                      ?? throw new AgentToolException("'" + strType + "' is not a type this character can have. Options: "
                                                                      + string.Join(", ", lstTypes.Select(x => x.Value)) + ".");
                    await objSpirit.SetNameAsync(strMatch, token).ConfigureAwait(false);
                }

                lstChanged.Add("type");
            }

            int? intForce = OptionalInt(objArgs, "force");
            if (intForce.HasValue)
            {
                int intMaximum = blnSprite
                    ? await objCharacter.GetMaxSpriteLevelAsync(token).ConfigureAwait(false)
                    : await objCharacter.GetMaxSpiritForceAsync(token).ConfigureAwait(false);
                if (intForce.Value < 1 || intForce.Value > intMaximum && !blnIgnoreRules)
                    throw new AgentToolException((blnSprite ? "Level" : "Force") + " must be between 1 and " + intMaximum.ToString(GlobalSettings.InvariantCultureInfo) + ".");
                await objSpirit.SetForceAsync(intForce.Value, token).ConfigureAwait(false);
                lstChanged.Add(blnSprite ? "level" : "force");
            }

            int? intServices = OptionalInt(objArgs, "services");
            if (intServices.HasValue)
            {
                if (intServices.Value < 0)
                    throw new AgentToolException("Services can't be negative.");
                await objSpirit.SetServicesOwedAsync(intServices.Value, token).ConfigureAwait(false);
                lstChanged.Add(blnSprite ? "tasks" : "services");
            }

            bool? blnBound = objArgs.Value<bool?>("bound");
            if (blnBound.HasValue)
            {
                await objSpirit.SetBoundAsync(blnBound.Value, token).ConfigureAwait(false);
                lstChanged.Add(blnBound.Value ? (blnSprite ? "registered" : "bound") : (blnSprite ? "unregistered" : "unbound"));
            }

            bool? blnFettered = objArgs.Value<bool?>("fettered");
            if (blnFettered.HasValue)
            {
                if (blnSprite || blnFettered.Value && !await objSpirit.GetAllowFetteringAsync(token).ConfigureAwait(false))
                    throw new AgentToolException("This " + (blnSprite ? "sprite" : "spirit") + " can't be fettered.");
                await objSpirit.SetFetteredAsync(blnFettered.Value, token).ConfigureAwait(false);
                lstChanged.Add(blnFettered.Value ? "fettered" : "not fettered");
            }

            string strName = objArgs.Value<string>("name");
            if (strName != null)
            {
                await objSpirit.SetCritterNameAsync(strName.Trim(), token).ConfigureAwait(false);
                lstChanged.Add("name");
            }

            string strNotes = objArgs.Value<string>("notes");
            if (strNotes != null)
            {
                await objSpirit.SetNotesAsync(strNotes.Replace("\r\n", "\n").Replace("\n", Environment.NewLine), token).ConfigureAwait(false);
                lstChanged.Add("notes");
            }

            if (lstChanged.Count == 0)
                throw new AgentToolException("Pass at least one field to set.");
            string strResult = "Set " + string.Join(", ", lstChanged) + " for " + await objSpirit.GetNameAsync(token).ConfigureAwait(false) + ".";
            if (frmCharacter is CharacterCreate frmCreate)
                return await FinishEditAsync(frmCreate, strResult, token).ConfigureAwait(false);
            await frmCharacter.MakeDirtyWithCharacterUpdate(token).ConfigureAwait(false);
            return new { result = strResult };
        }

        private static async Task<object> BondFocusAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strId = RequiredString(objArgs, "id");
            // A focus gear anywhere in the character's gear, or a stacked focus
            Gear objFocusGear = await objCharacter.Gear.DeepFindByIdAsync(strId, token).ConfigureAwait(false);
            StackedFocus objStack = objFocusGear == null
                ? await objCharacter.StackedFoci.FindAsync(x => x.InternalId == strId, token).ConfigureAwait(false)
                : null;
            if (objFocusGear == null && objStack == null)
            {
                OwnedItem objOwned = await FindItemAsync(objCharacter, strId, token).ConfigureAwait(false);
                objFocusGear = objOwned.Item as Gear;
            }

            if (objFocusGear != null && objFocusGear.Category != "Foci" && objFocusGear.Category != "Metamagic Foci")
                throw new AgentToolException(objFocusGear.Name + " isn't a focus.");
            if (objFocusGear == null && objStack == null)
                throw new AgentToolException("The character has no focus '" + strId + "'.");
            string strFocusId = objFocusGear?.InternalId ?? objStack.InternalId;
            bool blnBond = objArgs.Value<bool?>("bonded") != false;
            bool blnIsBonded = objFocusGear?.Bonded ?? objStack.Bonded;
            if (blnBond == blnIsBonded)
                throw new AgentToolException("That focus is already " + (blnBond ? "bonded." : "unbonded."));
            if (blnBond)
            {
                // The editor pre-answers the focus's choice from its Extra text, so answer it there
                if (objFocusGear != null && string.IsNullOrEmpty(objFocusGear.Extra))
                {
                    string strForced = objArgs.Value<string>("forced_value")?.Trim();
                    if (string.IsNullOrEmpty(strForced) && objFocusGear.Bonus?["selecttradition"] != null)
                    {
                        Tradition objTradition = await objCharacter.GetMagicTraditionAsync(token).ConfigureAwait(false);
                        if (objTradition != null && !string.IsNullOrEmpty(objTradition.Name))
                            strForced = await objTradition.GetCurrentDisplayNameAsync(token).ConfigureAwait(false);
                    }

                    if (!string.IsNullOrEmpty(strForced))
                        objFocusGear.Extra = strForced;
                }

                List<string> lstBonded = new List<string>();
                foreach (Focus objFocus in await objCharacter.Foci.ToListAsync(token).ConfigureAwait(false))
                {
                    if (objFocus.GearObject != null)
                        lstBonded.Add(objFocus.GearObject.InternalId);
                }

                foreach (StackedFocus objLoop in await objCharacter.StackedFoci.ToListAsync(token).ConfigureAwait(false))
                {
                    if (objLoop.Bonded)
                        lstBonded.Add(objLoop.InternalId);
                }

                string strRefusal = await frmCreate.BondFocusAsync(strFocusId, lstBonded, token).ConfigureAwait(false);
                if (strRefusal != null)
                    throw new AgentToolException(string.IsNullOrEmpty(strRefusal)
                                                     ? "The focus was not bonded (a choice it needed was cancelled)."
                                                     : await LanguageManager.GetStringAsync(strRefusal, GlobalSettings.DefaultLanguage, token: token).ConfigureAwait(false));
            }
            else
                await frmCreate.UnbondFocusAsync(strFocusId, token).ConfigureAwait(false);

            await frmCreate.RefreshFociListAsync(token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate, (blnBond ? "Bonded " : "Unbonded ")
                                                    + (objFocusGear != null
                                                        ? await objFocusGear.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                        : await objStack.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)) + ".",
                                         token).ConfigureAwait(false);
        }

        private static async Task<object> AddInitiationAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            bool blnMagic = await objCharacter.GetMAGEnabledAsync(token).ConfigureAwait(false);
            if (!blnMagic && !await objCharacter.GetRESEnabledAsync(token).ConfigureAwait(false))
                throw new AgentToolException("Only awakened and emerged characters can initiate or submerge.");
            InitiationGrade objGrade = await frmCreate.AddInitiationGradeAsync(objArgs.Value<bool?>("group") == true, objArgs.Value<bool?>("ordeal") == true,
                                                                               objArgs.Value<bool?>("schooling") == true, false, token).ConfigureAwait(false)
                                       ?? throw new AgentToolException("The " + (blnMagic ? "initiate" : "submersion") + " grade can't go above the character's "
                                                                       + (blnMagic ? "Magic" : "Resonance") + ".");
            return await FinishEditAsync(frmCreate, (blnMagic ? "Initiate" : "Submersion") + " grade is now "
                                                    + objGrade.Grade.ToString(GlobalSettings.InvariantCultureInfo) + ".", token).ConfigureAwait(false);
        }

        /// <summary>
        /// One of the character's initiation (or submersion) grades: the asked-for one, or the highest.
        /// </summary>
        private static async Task<InitiationGrade> GetInitiationGradeAsync(Character objCharacter, JObject objArgs, bool blnSubmersion, CancellationToken token)
        {
            List<InitiationGrade> lstGrades = await objCharacter.InitiationGrades.ToListAsync(token).ConfigureAwait(false);
            lstGrades.RemoveAll(x => x.Technomancer != blnSubmersion);
            if (lstGrades.Count == 0)
                throw new AgentToolException("The character needs " + (blnSubmersion ? "a submersion" : "an initiation") + " grade first (add_initiation).");
            int intGrade = OptionalInt(objArgs, "grade") ?? lstGrades.Max(x => x.Grade);
            return lstGrades.Find(x => x.Grade == intGrade)
                   ?? throw new AgentToolException("The character has no grade " + intGrade.ToString(GlobalSettings.InvariantCultureInfo) + ".");
        }

        private static async Task<object> AddArtAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            if (!await objCharacter.GetMAGEnabledAsync(token).ConfigureAwait(false))
                throw new AgentToolException("Only awakened characters learn magical arts.");
            InitiationGrade objGrade = await GetInitiationGradeAsync(objCharacter, objArgs, false, token).ConfigureAwait(false);
            switch (objArgs.Value<string>("kind")?.Trim().ToLowerInvariant())
            {
                case null:
                case "":
                case "art":
                    break;
                case "enchantment":
                {
                    XmlNode xmlEnchantment = await GetDataNodeAsync(objCharacter, "spells.xml", "/chummer/spells/spell", RequiredString(objArgs, "id"), token)
                        .ConfigureAwait(false);
                    if (xmlEnchantment["category"]?.InnerText != "Enchantments")
                        throw new AgentToolException(xmlEnchantment["name"]?.InnerText + " isn't an enchantment. search_catalog(kind='enchantment') lists them.");
                    await EnsureBookEnabledAsync(xmlEnchantment, objCharacter, token).ConfigureAwait(false);
                    await EnsureRequirementsMetAsync(xmlEnchantment, objCharacter, token).ConfigureAwait(false);
                    Spell objSpell = await frmCreate.AddEnchantmentFromNodeAsync(objGrade, xmlEnchantment, token).ConfigureAwait(false)
                                     ?? throw new AgentToolException("The enchantment was not added.");
                    return await FinishEditAsync(frmCreate, "Learned the enchantment " + await objSpell.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                            + " at grade " + objGrade.Grade.ToString(GlobalSettings.InvariantCultureInfo) + " (id " + objSpell.InternalId + ").",
                                                 token).ConfigureAwait(false);
                }
                case "enhancement":
                {
                    if (!await objCharacter.GetAdeptEnabledAsync(token).ConfigureAwait(false))
                        throw new AgentToolException("Only adepts learn power enhancements.");
                    XmlNode xmlEnhancement = await GetDataNodeAsync(objCharacter, "powers.xml", "/chummer/enhancements/enhancement", RequiredString(objArgs, "id"), token)
                        .ConfigureAwait(false);
                    await EnsureBookEnabledAsync(xmlEnhancement, objCharacter, token).ConfigureAwait(false);
                    await EnsureRequirementsMetAsync(xmlEnhancement, objCharacter, token).ConfigureAwait(false);
                    Enhancement objEnhancement = await frmCreate.AddEnhancementFromNodeAsync(objGrade, xmlEnhancement, token).ConfigureAwait(false)
                                                 ?? throw new AgentToolException("The enhancement was not added.");
                    return await FinishEditAsync(frmCreate, "Learned the enhancement " + await objEnhancement.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                            + " at grade " + objGrade.Grade.ToString(GlobalSettings.InvariantCultureInfo) + " (id " + objEnhancement.InternalId + ").",
                                                 token).ConfigureAwait(false);
                }
                default:
                    throw new AgentToolException("`kind` must be 'art', 'enchantment' or 'enhancement'.");
            }

            XmlNode xmlArt = await GetDataNodeAsync(objCharacter, "metamagic.xml", "/chummer/arts/art", RequiredString(objArgs, "id"), token).ConfigureAwait(false);
            string strArt = xmlArt["name"]?.InnerText;
            if (await objCharacter.Arts.AnyAsync(x => x.Name == strArt, token).ConfigureAwait(false))
                throw new AgentToolException("The character already knows " + strArt + ".");
            await EnsureBookEnabledAsync(xmlArt, objCharacter, token).ConfigureAwait(false);
            await EnsureRequirementsMetAsync(xmlArt, objCharacter, token).ConfigureAwait(false);
            Art objArt = await frmCreate.AddArtFromNodeAsync(objGrade, xmlArt, token).ConfigureAwait(false)
                         ?? throw new AgentToolException("The art was not added.");
            return await FinishEditAsync(frmCreate, "Learned the art of " + await objArt.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " at grade " + objGrade.Grade.ToString(GlobalSettings.InvariantCultureInfo) + " (id " + objArt.InternalId + ").",
                                         token).ConfigureAwait(false);
        }

        private static async Task<object> AddMetamagicAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            bool blnEcho = !await objCharacter.GetMAGEnabledAsync(token).ConfigureAwait(false) && await objCharacter.GetRESEnabledAsync(token).ConfigureAwait(false);
            InitiationGrade objGrade = await GetInitiationGradeAsync(objCharacter, objArgs, blnEcho, token).ConfigureAwait(false);
            int intGrade = objGrade.Grade;
            XmlNode xmlMetamagic = blnEcho
                ? await GetDataNodeAsync(objCharacter, "echoes.xml", "/chummer/echoes/echo", RequiredString(objArgs, "id"), token).ConfigureAwait(false)
                : await GetDataNodeAsync(objCharacter, "metamagic.xml", "/chummer/metamagics/metamagic", RequiredString(objArgs, "id"), token).ConfigureAwait(false);
            await EnsureBookEnabledAsync(xmlMetamagic, objCharacter, token).ConfigureAwait(false);
            if (!await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false))
            {
                // The Select Metamagic dialog only offers metamagics for what the character is: magician, adept, or both
                bool blnMagician = await objCharacter.GetMagicianEnabledAsync(token).ConfigureAwait(false);
                if (!blnEcho && blnMagician != await objCharacter.GetAdeptEnabledAsync(token).ConfigureAwait(false)
                             && xmlMetamagic[blnMagician ? "magician" : "adept"]?.InnerText != bool.TrueString)
                    throw new AgentToolException(xmlMetamagic["name"]?.InnerText + " is not available to " + (blnMagician ? "magicians." : "adepts."));
                await EnsureRequirementsMetAsync(xmlMetamagic, objCharacter, token).ConfigureAwait(false);
            }

            Metamagic objMetamagic = await frmCreate.AddMetamagicFromNodeAsync(objGrade, xmlMetamagic,
                                                                               blnEcho ? Improvement.ImprovementSource.Echo : Improvement.ImprovementSource.Metamagic,
                                                                               objArgs.Value<string>("forced_value") ?? string.Empty, token).ConfigureAwait(false)
                                     ?? throw new AgentToolException("Chummer did not add it (a choice it needed was cancelled).");
            return await FinishEditAsync(frmCreate, "Learned " + await objMetamagic.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " at grade " + intGrade.ToString(GlobalSettings.InvariantCultureInfo) + " (id " + objMetamagic.InternalId + ").",
                                         token).ConfigureAwait(false);
        }

        private static async Task<object> SetMysticAdeptPowerPointsAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            // The box is only shown when this is true
            if (!await objCharacter.GetUseMysticAdeptPPsAsync(token).ConfigureAwait(false))
                throw new AgentToolException(await objCharacter.GetIsMysticAdeptAsync(token).ConfigureAwait(false)
                                                 ? "This character's settings give mystic adepts a second Magic attribute instead; raise it with set_attribute."
                                                 : "Only mystic adepts split their Magic between spells and powers.");
            int intPoints = OptionalInt(objArgs, "points") ?? throw new AgentToolException("`points` is required.");
            int intMagic = await (await objCharacter.GetAttributeAsync("MAG", token: token).ConfigureAwait(false)).GetTotalValueAsync(token).ConfigureAwait(false);
            if (intPoints < 0 || intPoints > intMagic)
                throw new AgentToolException("`points` must be from 0 to the character's Magic, " + intMagic.ToString(GlobalSettings.InvariantCultureInfo) + ".");
            await objCharacter.SetMysticAdeptPowerPointsAsync(intPoints, token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate, intPoints.ToString(GlobalSettings.InvariantCultureInfo) + " of " + intMagic.ToString(GlobalSettings.InvariantCultureInfo)
                                                    + " Magic now goes to adept powers ("
                                                    + (await objCharacter.GetPowerPointsTotalAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo)
                                                    + " power points).", token).ConfigureAwait(false);
        }
    }
}
