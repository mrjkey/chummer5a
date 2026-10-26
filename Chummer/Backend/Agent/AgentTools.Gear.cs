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
using Chummer.Backend.Equipment;
using Chummer.Backend.Uniques;
using Newtonsoft.Json.Linq;

namespace Chummer.Agent
{
    public static partial class AgentTools
    {
        private const string FreeDescription = "Take it for free (only when a quality or the GM grants it). Default false.";

        private const string BlackMarketDescription = "Apply the Black Market Pipeline discount (only if the character has that quality). Default false.";

        private static IEnumerable<AgentTool> CreateGearTools()
        {
            yield return new AgentTool("add_weapon",
                "Buy a weapon from search_catalog(kind='weapon'), as the editor's Add Weapon does. Checks requirements, books and the availability limit; "
                + "the result shows nuyen remaining.",
                AgentTool.Schema(("id!", "string", "Weapon id from search_catalog, or its exact English name."),
                                 ("free", "boolean", FreeDescription),
                                 ("black_market", "boolean", BlackMarketDescription),
                                 ("character", "string", CharacterArgDescription)),
                AddWeaponAsync);
            yield return new AgentTool("add_armor",
                "Buy armor from search_catalog(kind='armor'), as the editor's Add Armor does. Checks requirements, books and the availability limit.",
                AgentTool.Schema(("id!", "string", "Armor id from search_catalog, or its exact English name."),
                                 ("rating", "integer", "Rating, for armor that has one. Default 1."),
                                 ("free", "boolean", FreeDescription),
                                 ("black_market", "boolean", BlackMarketDescription),
                                 ("character", "string", CharacterArgDescription)),
                AddArmorAsync);
            yield return new AgentTool("add_gear",
                "Buy gear from search_catalog(kind='gear') (commlinks, fake SINs and licenses, ammo, tools, drugs...), as the editor's Add Gear does. "
                + "A commlink becomes the active one if the character has none. Identical items stack. Checks requirements, books, rating and availability. "
                + "Ammunition, spare clips and speed loaders ask which weapon category they are for: pass it as forced_value (e.g. 'Heavy Pistols').",
                AgentTool.Schema(("id!", "string", "Gear id from search_catalog, or its exact English name."),
                                 ("rating", "integer", "Rating, for gear that has one (e.g. a Fake SIN 4). Default the item's minimum."),
                                 ("quantity", "number", "How many to buy. Default 1 (or the pack size, for things sold in packs)."),
                                 ("parent", "string", "Id of gear the character has to plug this into (e.g. a program into a commlink, from get_character_summary)."),
                                 ("forced_value", "string", ForcedValueDescription),
                                 ("free", "boolean", FreeDescription),
                                 ("black_market", "boolean", BlackMarketDescription),
                                 ("character", "string", CharacterArgDescription)),
                AddGearAsync);
            yield return new AgentTool("add_cyberware",
                "Install cyberware or bioware from search_catalog(kind='cyberware' or 'bioware'), as the editor's Add Cyberware/Bioware does. "
                + "The grade changes essence cost, price and availability. Checks requirements, books, grade, rating and availability; the result shows essence.",
                AgentTool.Schema(("id!", "string", "Cyberware or bioware id from search_catalog, or its exact English name."),
                                 ("kind", "string", "'cyberware' (default) or 'bioware'."),
                                 ("grade", "string", "Grade name, e.g. 'Standard' (default), 'Alphaware', 'Used', 'Betaware'."),
                                 ("rating", "integer", "Rating, for ware that has one. Default the item's minimum."),
                                 ("parent", "string", "Id of cyberware the character has to install this into (e.g. a cyberarm), from get_character_summary."),
                                 ("free", "boolean", FreeDescription),
                                 ("black_market", "boolean", BlackMarketDescription),
                                 ("character", "string", CharacterArgDescription)),
                AddCyberwareAsync);
            yield return new AgentTool("add_vehicle",
                "Buy a vehicle or drone from search_catalog(kind='vehicle'), as the editor's Add Vehicle does. Checks requirements, books and availability.",
                AgentTool.Schema(("id!", "string", "Vehicle id from search_catalog, or its exact English name."),
                                 ("free", "boolean", FreeDescription),
                                 ("black_market", "boolean", BlackMarketDescription),
                                 ("character", "string", CharacterArgDescription)),
                AddVehicleAsync);
            yield return new AgentTool("add_weapon_accessory",
                "Fit an accessory (search_catalog kind='accessory') to a weapon the character has, as the editor's Add Accessory does. "
                + "The accessory must fit one of the weapon's free mounts; the error lists them.",
                AgentTool.Schema(("weapon!", "string", "Weapon id from get_character_summary, or its name."),
                                 ("id!", "string", "Accessory id from search_catalog, or its exact English name."),
                                 ("mount", "string", "Mount to use, e.g. 'Top', 'Barrel', 'Under'. Default the first that fits."),
                                 ("extra_mount", "string", "Second mount, for accessories that take two. Default the first that fits."),
                                 ("rating", "integer", "Rating, for accessories that have one."),
                                 ("free", "boolean", FreeDescription),
                                 ("black_market", "boolean", BlackMarketDescription),
                                 ("character", "string", CharacterArgDescription)),
                AddWeaponAccessoryAsync);
            yield return new AgentTool("add_armor_mod",
                "Fit a mod (search_catalog kind='armormod') to a piece of armor the character has, as the editor's Add Armor Mod does. "
                + "Only mods for the armor's categories are allowed.",
                AgentTool.Schema(("armor!", "string", "Armor id from get_character_summary, or its name."),
                                 ("id!", "string", "Armor mod id from search_catalog, or its exact English name."),
                                 ("rating", "integer", "Rating, for mods that have one."),
                                 ("free", "boolean", FreeDescription),
                                 ("character", "string", CharacterArgDescription)),
                AddArmorModAsync);
            yield return new AgentTool("add_vehicle_mod",
                "Install a mod (search_catalog kind='vehiclemod') in a vehicle or drone the character has, as the editor's Add Modification does.",
                AgentTool.Schema(("vehicle!", "string", "Vehicle id from get_character_summary, or its name."),
                                 ("id!", "string", "Vehicle mod id from search_catalog, or its exact English name."),
                                 ("rating", "integer", "Rating, for mods that have one."),
                                 ("free", "boolean", FreeDescription),
                                 ("black_market", "boolean", BlackMarketDescription),
                                 ("character", "string", CharacterArgDescription)),
                AddVehicleModAsync);
            yield return new AgentTool("add_lifestyle",
                "Add a lifestyle from search_catalog(kind='lifestyle'), e.g. Squatter, Low, Middle, paid for a number of months at creation.",
                AgentTool.Schema(("id!", "string", "Lifestyle id from search_catalog, or its exact English name."),
                                 ("months", "integer", "Months paid in advance. Default 1."),
                                 ("character", "string", CharacterArgDescription)),
                AddLifestyleAsync);
        }

        #region Helpers

        /// <summary>
        /// A catalog item the character may buy now: in the enabled books, requirements met and within the availability limit
        /// (unless the character ignores rules), as the selection dialogs only list such items.
        /// </summary>
        private static async Task<XmlNode> GetBuyableNodeAsync(Character objCharacter, string strFile, string strPath, string strId,
                                                               int intRating = 1, int intAvailModifier = 0, CancellationToken token = default)
        {
            XmlNode objNode = await GetDataNodeAsync(objCharacter, strFile, strPath, strId, token).ConfigureAwait(false);
            if (await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false))
                return objNode;
            string strName = objNode["name"]?.InnerText;
            string strSource = objNode["source"]?.InnerText;
            CharacterSettings objSettings = await objCharacter.GetSettingsAsync(token).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(strSource) && !await objSettings.BookEnabledAsync(strSource, token).ConfigureAwait(false))
                throw new AgentToolException(strName + " is from " + strSource + ", which isn't enabled in this character's settings.");
            await EnsureRequirementsMetAsync(objNode, objCharacter, token).ConfigureAwait(false);
            if (!await SelectionShared.CheckAvailRestrictionAsync(objNode, objCharacter, intRating, intAvailModifier, token).ConfigureAwait(false))
            {
                // Show the availability itself only when it's a plain value, not a per-rating expression
                string strAvail = objNode["avail"]?.InnerText ?? string.Empty;
                throw new AgentToolException(strName + " (availability" + (strAvail.IndexOfAny(new[] { '(', '{' }) < 0 ? " " + strAvail : string.Empty)
                                             + (intRating > 1 ? " at rating " + intRating.ToString(GlobalSettings.InvariantCultureInfo) : string.Empty)
                                             + (intAvailModifier != 0 ? ", " + intAvailModifier.ToString("+0;-0", GlobalSettings.InvariantCultureInfo) + " from its grade" : string.Empty)
                                             + ") is above the availability limit of "
                                             + (await objSettings.GetMaximumAvailabilityAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.InvariantCultureInfo)
                                             + " at character creation.");
            }

            return objNode;
        }

        /// <summary>
        /// The rating to buy an item at: the asked-for rating, or the item's minimum, checked against the item's limits.
        /// </summary>
        private static int GetRating(XmlNode objNode, int? intWanted, bool blnIgnoreRules)
        {
            // Ratings that are expressions (e.g. tied to an attribute) are left for Chummer to work out
            bool blnFixedMaximum = int.TryParse(objNode["rating"]?.InnerText, out int intMaximum);
            if (!int.TryParse(objNode["minrating"]?.InnerText, out int intMinimum))
                intMinimum = blnFixedMaximum && intMaximum > 0 ? 1 : 0;
            int intRating = intWanted ?? intMinimum;
            if (blnIgnoreRules || !blnFixedMaximum)
                return intRating;
            if (intMaximum == 0 && intRating > 0)
                throw new AgentToolException(objNode["name"]?.InnerText + " has no rating.");
            if (intRating < intMinimum || intRating > intMaximum)
                throw new AgentToolException(objNode["name"]?.InnerText + " takes a rating from " + intMinimum.ToString(GlobalSettings.InvariantCultureInfo)
                                             + " to " + intMaximum.ToString(GlobalSettings.InvariantCultureInfo) + ".");
            return intRating;
        }

        private static async Task<Gear> FindGearAsync(IEnumerable<Gear> lstGear, string strId, CancellationToken token)
        {
            foreach (Gear objGear in lstGear)
            {
                if (string.Equals(objGear.InternalId, strId, StringComparison.OrdinalIgnoreCase))
                    return objGear;
                Gear objChild = await FindGearAsync(await objGear.Children.ToListAsync(token).ConfigureAwait(false), strId, token).ConfigureAwait(false);
                if (objChild != null)
                    return objChild;
            }

            return null;
        }

        private static async Task<Cyberware> FindCyberwareAsync(IEnumerable<Cyberware> lstCyberware, string strId, CancellationToken token)
        {
            foreach (Cyberware objCyberware in lstCyberware)
            {
                if (string.Equals(objCyberware.InternalId, strId, StringComparison.OrdinalIgnoreCase))
                    return objCyberware;
                Cyberware objChild = await FindCyberwareAsync(await (await objCyberware.GetChildrenAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false),
                                                              strId, token).ConfigureAwait(false);
                if (objChild != null)
                    return objChild;
            }

            return null;
        }

        #endregion

        private static async Task<object> AddWeaponAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            XmlNode objXmlWeapon = await GetBuyableNodeAsync(frmCreate.CharacterObject, "weapons.xml", "/chummer/weapons/weapon", RequiredString(objArgs, "id"),
                                                             token: token).ConfigureAwait(false);
            Weapon objWeapon = await frmCreate.AddWeaponFromNodeAsync(objXmlWeapon, objArgs.Value<bool?>("free") == true,
                                                                      objArgs.Value<bool?>("black_market") == true, token: token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate, "Added " + await objWeapon.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " (id " + objWeapon.InternalId + ").", token).ConfigureAwait(false);
        }

        private static async Task<object> AddArmorAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            XmlNode objXmlArmor = await GetDataNodeAsync(objCharacter, "armor.xml", "/chummer/armors/armor", RequiredString(objArgs, "id"), token).ConfigureAwait(false);
            int intRating = GetRating(objXmlArmor, OptionalInt(objArgs, "rating"), await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false));
            objXmlArmor = await GetBuyableNodeAsync(objCharacter, "armor.xml", "/chummer/armors/armor", RequiredString(objArgs, "id"), Math.Max(1, intRating),
                                                    token: token).ConfigureAwait(false);
            Armor objArmor = await frmCreate.AddArmorFromNodeAsync(objXmlArmor, intRating, objArgs.Value<bool?>("free") == true,
                                                                   objArgs.Value<bool?>("black_market") == true, token: token).ConfigureAwait(false)
                             ?? throw new AgentToolException("The armor was not added (a choice it needed was cancelled).");
            return await FinishEditAsync(frmCreate, "Added " + await objArmor.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " (id " + objArmor.InternalId + ").", token).ConfigureAwait(false);
        }

        private static async Task<object> AddGearAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strId = RequiredString(objArgs, "id");
            XmlNode objXmlGear = await GetDataNodeAsync(objCharacter, "gear.xml", "/chummer/gears/gear", strId, token).ConfigureAwait(false);
            int intRating = GetRating(objXmlGear, OptionalInt(objArgs, "rating"), await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false));
            objXmlGear = await GetBuyableNodeAsync(objCharacter, "gear.xml", "/chummer/gears/gear", strId, Math.Max(1, intRating), token: token).ConfigureAwait(false);
            decimal decQuantity = objArgs.Value<decimal?>("quantity")
                                  ?? (decimal.TryParse(objXmlGear["costfor"]?.InnerText, System.Globalization.NumberStyles.Any,
                                                       GlobalSettings.InvariantCultureInfo, out decimal decCostFor) && decCostFor > 0
                                      ? decCostFor
                                      : 1);
            if (decQuantity <= 0)
                throw new AgentToolException("`quantity` must be more than 0.");
            Gear objParent = null;
            string strParent = objArgs.Value<string>("parent")?.Trim();
            if (!string.IsNullOrEmpty(strParent))
            {
                objParent = await FindGearAsync(await objCharacter.Gear.ToListAsync(token).ConfigureAwait(false), strParent, token).ConfigureAwait(false)
                            ?? await FindOwnedAsync(await objCharacter.Gear.ToListAsync(token).ConfigureAwait(false), strParent, token).ConfigureAwait(false)
                            ?? throw new AgentToolException("The character has no gear '" + strParent + "' to plug this into.");
            }

            Gear objGear = await frmCreate.AddGearFromNodeAsync(objXmlGear, intRating, decQuantity, objParent, null,
                                                                objArgs.Value<bool?>("free") == true, objArgs.Value<bool?>("black_market") == true,
                                                                false, true, objArgs.Value<string>("forced_value") ?? string.Empty, token)
                                    .ConfigureAwait(false)
                           ?? throw new AgentToolException("The gear was not added (its choice dialog was cancelled, or it could not be created).");
            return await FinishEditAsync(frmCreate, "Added " + await objGear.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " (id " + objGear.InternalId + ", quantity "
                                                    + objGear.Quantity.ToString(GlobalSettings.InvariantCultureInfo) + ").", token).ConfigureAwait(false);
        }

        private static async Task<object> AddCyberwareAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            bool blnIgnoreRules = await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false);
            string strId = RequiredString(objArgs, "id");
            bool blnBioware;
            switch (objArgs.Value<string>("kind")?.Trim().ToLowerInvariant())
            {
                case null:
                case "":
                case "cyberware":
                    blnBioware = false;
                    break;
                case "bioware":
                    blnBioware = true;
                    break;
                default:
                    throw new AgentToolException("`kind` must be 'cyberware' or 'bioware'.");
            }

            Improvement.ImprovementSource eSource = blnBioware ? Improvement.ImprovementSource.Bioware : Improvement.ImprovementSource.Cyberware;
            string strFile = blnBioware ? "bioware.xml" : "cyberware.xml";
            string strPath = blnBioware ? "/chummer/biowares/bioware" : "/chummer/cyberwares/cyberware";

            // Grades the character may use (banned grades are left out, as in the Select Cyberware dialog)
            string strGrade = objArgs.Value<string>("grade")?.Trim();
            if (string.IsNullOrEmpty(strGrade))
                strGrade = "Standard";
            List<Grade> lstGrades = await objCharacter.GetGradesListAsync(eSource, blnIgnoreRules, token).ConfigureAwait(false);
            Grade objGrade = null;
            foreach (Grade objLoopGrade in lstGrades)
            {
                if (string.Equals(objLoopGrade.Name, strGrade, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(await objLoopGrade.GetCurrentDisplayNameAsync(token).ConfigureAwait(false), strGrade, StringComparison.OrdinalIgnoreCase))
                {
                    objGrade = objLoopGrade;
                    break;
                }
            }

            if (objGrade == null)
                throw new AgentToolException("'" + strGrade + "' is not an available grade. Options: " + string.Join(", ", lstGrades.Select(x => x.Name)) + ".");

            XmlNode objXmlCyberware = await GetDataNodeAsync(objCharacter, strFile, strPath, strId, token).ConfigureAwait(false);
            int intRating = GetRating(objXmlCyberware, OptionalInt(objArgs, "rating"), blnIgnoreRules);
            objXmlCyberware = await GetBuyableNodeAsync(objCharacter, strFile, strPath, strId, Math.Max(1, intRating), objGrade.Avail, token)
                .ConfigureAwait(false);

            Cyberware objParent = null;
            string strParent = objArgs.Value<string>("parent")?.Trim();
            if (!string.IsNullOrEmpty(strParent))
            {
                List<Cyberware> lstOwned = await (await objCharacter.GetCyberwareAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false);
                objParent = await FindCyberwareAsync(lstOwned, strParent, token).ConfigureAwait(false)
                            ?? await FindOwnedAsync(lstOwned, strParent, token).ConfigureAwait(false)
                            ?? throw new AgentToolException("The character has no cyberware '" + strParent + "' to install this into.");
            }

            decimal decOldEssence = await objCharacter.EssenceAsync(token: token).ConfigureAwait(false);
            Cyberware objCyberware = await frmCreate.AddCyberwareFromNodeAsync(objXmlCyberware, eSource, objGrade, intRating, objParent,
                                                                               blnFree: objArgs.Value<bool?>("free") == true,
                                                                               blnBlackMarketDiscount: objArgs.Value<bool?>("black_market") == true,
                                                                               blnPromptForBaseWeapon: false, token: token).ConfigureAwait(false)
                                     ?? throw new AgentToolException("Chummer did not install it (it may not fit, or a choice it needed was cancelled).");
            decimal decEssence = await objCharacter.EssenceAsync(token: token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate, "Installed " + await objCyberware.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " (id " + objCyberware.InternalId + "), essence "
                                                    + decOldEssence.ToString(GlobalSettings.InvariantCultureInfo) + " -> "
                                                    + decEssence.ToString(GlobalSettings.InvariantCultureInfo) + ".", token).ConfigureAwait(false);
        }

        private static async Task<object> AddVehicleAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            XmlNode objXmlVehicle = await GetBuyableNodeAsync(frmCreate.CharacterObject, "vehicles.xml", "/chummer/vehicles/vehicle", RequiredString(objArgs, "id"),
                                                              token: token).ConfigureAwait(false);
            Vehicle objVehicle = await frmCreate.AddVehicleFromNodeAsync(objXmlVehicle, objArgs.Value<bool?>("free") == true,
                                                                         objArgs.Value<bool?>("black_market") == true, token: token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate, "Added " + await objVehicle.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " (id " + objVehicle.InternalId + ").", token).ConfigureAwait(false);
        }

        private static async Task<object> AddLifestyleAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            XmlNode objXmlLifestyle = await GetDataNodeAsync(objCharacter, "lifestyles.xml", "/chummer/lifestyles/lifestyle", RequiredString(objArgs, "id"), token)
                .ConfigureAwait(false);
            string strSource = objXmlLifestyle["source"]?.InnerText;
            if (!string.IsNullOrEmpty(strSource) && !await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false)
                                                 && !await (await objCharacter.GetSettingsAsync(token).ConfigureAwait(false)).BookEnabledAsync(strSource, token).ConfigureAwait(false))
                throw new AgentToolException(objXmlLifestyle["name"]?.InnerText + " is from " + strSource + ", which isn't enabled in this character's settings.");
            int intMonths = Math.Max(1, OptionalInt(objArgs, "months") ?? 1);
            // The same steps Chummer uses when it adds a lifestyle on its own (e.g. the free Street lifestyle)
            Lifestyle objLifestyle = new Lifestyle(objCharacter);
            try
            {
                await objLifestyle.CreateAsync(objXmlLifestyle, token).ConfigureAwait(false);
                await objLifestyle.SetIncrementsAsync(intMonths, token).ConfigureAwait(false);
                await objCharacter.Lifestyles.AddAsync(objLifestyle, token).ConfigureAwait(false);
            }
            catch
            {
                await objLifestyle.RemoveAsync(false, CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            return await FinishEditAsync(frmCreate, "Added " + await objLifestyle.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " lifestyle for " + intMonths.ToString(GlobalSettings.InvariantCultureInfo) + " month(s) (id "
                                                    + objLifestyle.InternalId + ").", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Availability modifiers the character has for one item, as the selection dialogs apply them.
        /// </summary>
        private static async Task<int> GetAvailabilityModifierAsync(Character objCharacter, XmlNode objNode, CancellationToken token)
        {
            return (await ImprovementManager.ValueOfAsync(objCharacter, Improvement.ImprovementType.Availability,
                                                          strImprovedName: objNode["id"]?.InnerText, blnIncludeNonImproved: true, token: token)
                                            .ConfigureAwait(false)).StandardRound();
        }

        private static async Task<object> AddWeaponAccessoryAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strWeapon = RequiredString(objArgs, "weapon");
            Weapon objWeapon = await FindOwnedAsync(await (await objCharacter.GetWeaponsAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false),
                                                    strWeapon, token).ConfigureAwait(false)
                               ?? throw new AgentToolException("The character has no weapon '" + strWeapon + "'.");
            if (objWeapon.Cyberware)
                throw new AgentToolException("Accessories can't be added to cyberweapons.");
            if (!objWeapon.AllowAccessory)
                throw new AgentToolException(objWeapon.Name + " can't take accessories.");
            bool blnIgnoreRules = await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false);
            string strId = RequiredString(objArgs, "id");
            XmlNode xmlAccessory = await GetDataNodeAsync(objCharacter, "weapons.xml", "/chummer/accessories/accessory", strId, token).ConfigureAwait(false);
            int intRating = GetRating(xmlAccessory, OptionalInt(objArgs, "rating"), blnIgnoreRules);
            xmlAccessory = await GetBuyableNodeAsync(objCharacter, "weapons.xml", "/chummer/accessories/accessory", strId, Math.Max(1, intRating),
                                                     await GetAvailabilityModifierAsync(objCharacter, xmlAccessory, token).ConfigureAwait(false), token)
                .ConfigureAwait(false);
            string strName = xmlAccessory["name"]?.InnerText;
            if (!blnIgnoreRules && !await objWeapon.CheckAccessoryRequirementsAsync(xmlAccessory.CreateNavigator(), token).ConfigureAwait(false))
                throw new AgentToolException(strName + " doesn't fit " + objWeapon.Name + " (its mount or weapon requirements aren't met).");

            // The mount choices the Select Weapon Accessory dialog offers: the accessory's mounts that are free on the weapon
            List<string> lstAllowedMounts = await objWeapon.GetAccessoryMountsAsync(token: token).ConfigureAwait(false);
            List<string> GetMountChoices(string strField)
            {
                List<string> lstMounts = (xmlAccessory[strField]?.InnerText ?? string.Empty)
                                         .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (!lstMounts.Contains("None"))
                    lstMounts.Add("None");
                return lstMounts.Where(x => lstAllowedMounts.Contains(x)).ToList();
            }

            List<string> lstMountChoices = GetMountChoices("mount");
            if (lstMountChoices.Count == 0)
                throw new AgentToolException(objWeapon.Name + " has no free mount for " + strName + ".");
            string strMount = objArgs.Value<string>("mount")?.Trim();
            if (string.IsNullOrEmpty(strMount))
                strMount = lstMountChoices[0];
            else
                strMount = lstMountChoices.Find(x => string.Equals(x, strMount, StringComparison.OrdinalIgnoreCase))
                           ?? throw new AgentToolException("'" + strMount + "' is not a free mount for " + strName + ". Options: " + string.Join(", ", lstMountChoices) + ".");
            List<string> lstExtraChoices = GetMountChoices("extramount");
            string strExtraMount = objArgs.Value<string>("extra_mount")?.Trim();
            if (string.IsNullOrEmpty(strExtraMount))
            {
                strExtraMount = lstExtraChoices.Count > 0 ? lstExtraChoices[0] : "None";
                if (strExtraMount != "None" && strExtraMount == strMount)
                    strExtraMount = lstExtraChoices.Count > 1 ? lstExtraChoices[1] : "None";
            }
            else
                strExtraMount = lstExtraChoices.Find(x => string.Equals(x, strExtraMount, StringComparison.OrdinalIgnoreCase))
                                ?? throw new AgentToolException("'" + strExtraMount + "' is not a free second mount for " + strName + ". Options: "
                                                                + string.Join(", ", lstExtraChoices) + ".");

            WeaponAccessory objAccessory = await frmCreate.AddWeaponAccessoryFromNodeAsync(
                                               objWeapon, xmlAccessory, new ValueTuple<string, string>(strMount, strExtraMount), intRating,
                                               objArgs.Value<bool?>("free") == true, objArgs.Value<bool?>("black_market") == true, 0, false, string.Empty,
                                               NuyenExpenseType.AddWeaponAccessory, true, false, token).ConfigureAwait(false)
                                           ?? throw new AgentToolException("The accessory was not added.");
            return await FinishEditAsync(frmCreate, "Fitted " + await objAccessory.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " to " + objWeapon.Name + " (" + strMount + ", id " + objAccessory.InternalId + ").", token).ConfigureAwait(false);
        }

        private static async Task<object> AddArmorModAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strArmor = RequiredString(objArgs, "armor");
            Armor objArmor = await FindOwnedAsync(await (await objCharacter.GetArmorAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false),
                                                  strArmor, token).ConfigureAwait(false)
                             ?? throw new AgentToolException("The character has no armor '" + strArmor + "'.");
            bool blnIgnoreRules = await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false);
            string strId = RequiredString(objArgs, "id");
            XmlNode xmlMod = await GetDataNodeAsync(objCharacter, "armor.xml", "/chummer/mods/mod", strId, token).ConfigureAwait(false);
            string strName = xmlMod["name"]?.InnerText;
            int.TryParse(xmlMod["maxrating"]?.InnerText, out int intMaxRating);
            int intRating = OptionalInt(objArgs, "rating") ?? (intMaxRating > 0 ? 1 : 0);
            if (!blnIgnoreRules)
            {
                if (intMaxRating <= 1 && intRating > 1)
                    throw new AgentToolException(strName + " has no rating.");
                if (intMaxRating > 1 && (intRating < 1 || intRating > intMaxRating))
                    throw new AgentToolException(strName + " takes a rating from 1 to " + intMaxRating.ToString(GlobalSettings.InvariantCultureInfo) + ".");
                // The categories the Select Armor Mod dialog lists for this armor
                (string strAllowed, bool blnExcludeGeneral) = CharacterCreate.GetArmorModCategories(objArmor, await objArmor.GetNodeAsync(token).ConfigureAwait(false), token);
                string strCategory = xmlMod["category"]?.InnerText ?? string.Empty;
                List<string> lstAllowed = strAllowed.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (!blnExcludeGeneral)
                    lstAllowed.Add("General");
                if (!lstAllowed.Contains(strCategory))
                    throw new AgentToolException(strName + " (" + strCategory + ") doesn't go on " + objArmor.Name + ". Allowed categories: "
                                                 + string.Join(", ", lstAllowed) + ".");
            }

            xmlMod = await GetBuyableNodeAsync(objCharacter, "armor.xml", "/chummer/mods/mod", strId, Math.Max(1, intRating),
                                               await GetAvailabilityModifierAsync(objCharacter, xmlMod, token).ConfigureAwait(false), token).ConfigureAwait(false);
            ArmorMod objMod = await frmCreate.AddArmorModFromNodeAsync(objArmor, xmlMod, intRating, objArgs.Value<bool?>("free") == true, token).ConfigureAwait(false)
                              ?? throw new AgentToolException("The mod was not added (a choice it needed was cancelled).");
            return await FinishEditAsync(frmCreate, "Fitted " + await objMod.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " to " + objArmor.Name + " (id " + objMod.InternalId + ").", token).ConfigureAwait(false);
        }

        private static async Task<object> AddVehicleModAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strVehicle = RequiredString(objArgs, "vehicle");
            Vehicle objVehicle = await FindOwnedAsync(await (await objCharacter.GetVehiclesAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false),
                                                      strVehicle, token).ConfigureAwait(false)
                                 ?? throw new AgentToolException("The character has no vehicle '" + strVehicle + "'.");
            bool blnIgnoreRules = await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false);
            string strId = RequiredString(objArgs, "id");
            XmlNode xmlMod = await GetDataNodeAsync(objCharacter, "vehicles.xml", "/chummer/mods/mod", strId, token).ConfigureAwait(false);
            string strName = xmlMod["name"]?.InnerText;
            // Ratings can be tied to the vehicle's Body or seats, as in the Select Vehicle Mod dialog
            string strRating = xmlMod["rating"]?.InnerText ?? "0";
            int intMaxRating;
            switch (strRating.ToLowerInvariant())
            {
                case "body":
                    intMaxRating = await objVehicle.GetTotalBodyAsync(token).ConfigureAwait(false);
                    break;
                case "seats":
                    intMaxRating = await objVehicle.GetTotalSeatsAsync(token).ConfigureAwait(false);
                    break;
                default:
                    int.TryParse(strRating, out intMaxRating);
                    break;
            }

            int intRating = OptionalInt(objArgs, "rating") ?? (intMaxRating > 0 ? 1 : 0);
            if (!blnIgnoreRules)
            {
                if (intMaxRating == 0 && intRating > 0 && OptionalInt(objArgs, "rating") != null)
                    throw new AgentToolException(strName + " has no rating.");
                if (intMaxRating > 0 && (intRating < 1 || intRating > intMaxRating))
                    throw new AgentToolException(strName + " takes a rating from 1 to " + intMaxRating.ToString(GlobalSettings.InvariantCultureInfo) + " on this vehicle.");
                if (!await objVehicle.CheckModRequirementsAsync(xmlMod.CreateNavigator(), token).ConfigureAwait(false))
                    throw new AgentToolException(strName + " can't be installed in " + objVehicle.Name + ".");
            }

            xmlMod = await GetBuyableNodeAsync(objCharacter, "vehicles.xml", "/chummer/mods/mod", strId, Math.Max(1, intRating),
                                               await GetAvailabilityModifierAsync(objCharacter, xmlMod, token).ConfigureAwait(false), token).ConfigureAwait(false);
            // Character creation doesn't enforce mod slots when adding (validation reports overfull vehicles)
            VehicleMod objMod = await frmCreate.AddVehicleModFromNodeAsync(objVehicle, xmlMod, intRating, objArgs.Value<bool?>("free") == true,
                                                                           objArgs.Value<bool?>("black_market") == true, 0, false, string.Empty,
                                                                           NuyenExpenseType.AddVehicleMod, true, false, false, token).ConfigureAwait(false)
                                ?? throw new AgentToolException("The mod was not installed.");
            return await FinishEditAsync(frmCreate, "Installed " + await objMod.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " in " + objVehicle.Name + " (id " + objMod.InternalId + ").", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Find a piece of equipment the character owns by id or name, for removal.
        /// </summary>
        private static async Task<ICanRemove> FindOwnedEquipmentAsync(Character objCharacter, string strId, CancellationToken token)
        {
            List<Weapon> lstWeapons = await (await objCharacter.GetWeaponsAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false);
            Weapon objWeapon = await FindOwnedAsync(lstWeapons, strId, token).ConfigureAwait(false);
            if (objWeapon != null)
                return objWeapon;
            Armor objArmor = await FindOwnedAsync(await (await objCharacter.GetArmorAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false), strId, token)
                .ConfigureAwait(false);
            if (objArmor != null)
                return objArmor;
            List<Gear> lstGear = await objCharacter.Gear.ToListAsync(token).ConfigureAwait(false);
            Gear objGear = await FindGearAsync(lstGear, strId, token).ConfigureAwait(false) ?? await FindOwnedAsync(lstGear, strId, token).ConfigureAwait(false);
            if (objGear != null)
                return objGear;
            List<Cyberware> lstCyberware = await (await objCharacter.GetCyberwareAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false);
            Cyberware objCyberware = await FindCyberwareAsync(lstCyberware, strId, token).ConfigureAwait(false)
                                     ?? await FindOwnedAsync(lstCyberware, strId, token).ConfigureAwait(false);
            if (objCyberware != null)
                return objCyberware;
            Vehicle objVehicle = await FindOwnedAsync(await (await objCharacter.GetVehiclesAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false), strId, token)
                .ConfigureAwait(false);
            if (objVehicle != null)
                return objVehicle;
            List<Lifestyle> lstLifestyles = await (await objCharacter.GetLifestylesAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false);
            foreach (Lifestyle objLifestyle in lstLifestyles)
            {
                if (string.Equals(objLifestyle.InternalId, strId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(await objLifestyle.GetCurrentDisplayNameAsync(token).ConfigureAwait(false), strId, StringComparison.OrdinalIgnoreCase))
                    return objLifestyle;
            }

            // Things fitted to equipment: weapon accessories, armor mods, vehicle mods
            foreach (Weapon objLoop in lstWeapons)
            {
                WeaponAccessory objAccessory = await FindOwnedAsync(await objLoop.WeaponAccessories.ToListAsync(token).ConfigureAwait(false), strId, token).ConfigureAwait(false);
                if (objAccessory != null)
                    return objAccessory;
            }

            foreach (Armor objLoop in await (await objCharacter.GetArmorAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false))
            {
                ArmorMod objMod = await FindOwnedAsync(await objLoop.ArmorMods.ToListAsync(token).ConfigureAwait(false), strId, token).ConfigureAwait(false);
                if (objMod != null)
                    return objMod;
            }

            foreach (Vehicle objLoop in await (await objCharacter.GetVehiclesAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false))
            {
                VehicleMod objMod = await FindOwnedAsync(await objLoop.Mods.ToListAsync(token).ConfigureAwait(false), strId, token).ConfigureAwait(false);
                if (objMod != null)
                    return objMod;
            }

            // Magic, resonance and martial arts
            ComplexForm objForm = await FindOwnedAsync(await objCharacter.ComplexForms.ToListAsync(token).ConfigureAwait(false), strId, token).ConfigureAwait(false);
            if (objForm != null)
                return objForm;
            Metamagic objMetamagic = await FindOwnedAsync(await objCharacter.Metamagics.ToListAsync(token).ConfigureAwait(false), strId, token).ConfigureAwait(false);
            if (objMetamagic != null)
                return objMetamagic;
            Art objArt2 = await FindOwnedAsync(await objCharacter.Arts.ToListAsync(token).ConfigureAwait(false), strId, token).ConfigureAwait(false);
            if (objArt2 != null)
                return objArt2;
            List<MartialArt> lstArts = await objCharacter.MartialArts.ToListAsync(token).ConfigureAwait(false);
            MartialArt objArt = await FindOwnedAsync(lstArts, strId, token).ConfigureAwait(false);
            if (objArt != null)
                return objArt;
            foreach (MartialArt objLoop in lstArts)
            {
                MartialArtTechnique objTechnique = await FindOwnedAsync(await objLoop.Techniques.ToListAsync(token).ConfigureAwait(false), strId, token).ConfigureAwait(false);
                if (objTechnique != null)
                    return objTechnique;
            }

            foreach (InitiationGrade objGrade in await objCharacter.InitiationGrades.ToListAsync(token).ConfigureAwait(false))
            {
                if (string.Equals(objGrade.InternalId, strId, StringComparison.OrdinalIgnoreCase))
                    return objGrade;
            }

            return null;
        }
    }
}
