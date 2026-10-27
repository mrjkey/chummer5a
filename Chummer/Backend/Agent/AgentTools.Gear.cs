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
        private const string FreeDescription = "Take it for free (only when a quality or the GM grants it). Default false.";

        private const string BlackMarketDescription = "Apply the Black Market Pipeline discount (only if the character has that quality). Default false.";

        private static IEnumerable<AgentTool> CreateGearTools()
        {
            yield return new AgentTool("add_weapon",
                "Buy a weapon from search_catalog(kind='weapon'), as the editor's Add Weapon does. Checks requirements, books and the availability limit; "
                + "the result shows nuyen remaining. With `parent` it goes under a weapon (underbarrel weapons), into a vehicle's weapon mount, or into "
                + "a drone or mechanical arm vehicle mod.",
                AgentTool.Schema(("id!", "string", "Weapon id from search_catalog, or its exact English name."),
                                 ("parent", "string", "Id of a weapon, weapon mount or arm vehicle mod to put it in, from list_items."),
                                 ("location", "string", "Weapon location to file it under, from manage_location(tab='weapons')."),
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
                + "With `parent` it goes into something the character has, as that item's Add Gear / Add as Plugin menu item does: programs into a "
                + "commlink, gear into armor or its mods, cyberware, a weapon accessory, a vehicle or a vehicle's sensor; only what the parent takes is allowed. "
                + "Capacity is not checked here; validate_character reports overfull items. "
                + "Ammunition, spare clips and speed loaders ask which weapon category they are for: pass it as forced_value (e.g. 'Heavy Pistols').",
                AgentTool.Schema(("id!", "string", "Gear id from search_catalog, or its exact English name."),
                                 ("rating", "integer", "Rating, for gear that has one (e.g. a Fake SIN 4). Default the item's minimum."),
                                 ("quantity", "number", "How many to buy. Default 1 (or the pack size, for things sold in packs)."),
                                 ("parent", "string", "Id of the item to put this into (gear, armor, armor mod, cyberware, weapon accessory or vehicle), from list_items."),
                                 ("location", "string", "Gear location (or a vehicle's location, with a vehicle parent) to file it under, from manage_location."),
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
                                 ("parent", "string", "Id of cyberware to install this into as a plugin (e.g. a cyberarm; it takes the parent's grade), or of a vehicle mod "
                                                      + "that holds cyberware (e.g. a drone arm), from list_items."),
                                 ("forced_value", "string", "Answer for the choice it asks: 'Left' or 'Right' for ware that goes on one side, or its other choice."),
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
                AgentTool.Schema(("weapon!", "string", "Weapon id from list_items (vehicle and underbarrel weapons too), or its name."),
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
            yield return new AgentTool("add_weapon_mount",
                "Build a weapon mount on a vehicle or drone the character has, as the vehicle tab's Add Weapon Mount dialog does: a size plus its "
                + "visibility, flexibility and control. Only the choices the dialog offers for that vehicle and size are allowed; a wrong one lists them. "
                + "Then put weapons in it with add_weapon(parent=<mount id>). In career mode it checks the vehicle's capacity and the character's nuyen.",
                AgentTool.Schema(("vehicle!", "string", "Vehicle id from get_character_summary, or its name."),
                                 ("size!", "string", "Mount size, by name or id, e.g. 'Light', 'Standard', 'Heavy' (Rigger 5.0), 'Standard [SR5]' (core book), "
                                                     + "or a drone size such as 'Small (Drone)'."),
                                 ("visibility", "string", "e.g. 'External', 'Internal', 'Concealed'. Default the first the size allows."),
                                 ("flexibility", "string", "e.g. 'Fixed', 'Flexible', 'Turret'. Default the first the size allows."),
                                 ("control", "string", "e.g. 'Remote', 'Manual', 'Armored Manual'. Default the first the size allows."),
                                 ("mods", "array", "Weapon mount add-ons (search_catalog kind='weaponmountmod', Rigger 5.0), by id or name, e.g. 'Pop-Out Weapon Mount "
                                                   + "Add-on'. Only add-ons the vehicle takes are allowed."),
                                 ("free", "boolean", FreeDescription),
                                 ("black_market", "boolean", BlackMarketDescription),
                                 ("character", "string", CharacterArgDescription)),
                AddWeaponMountAsync);
            yield return new AgentTool("edit_weapon_mount",
                "Change a weapon mount the character built, as the vehicle tab's Edit Weapon Mount does: its size, visibility, flexibility and "
                + "control, and its add-ons. Only the fields passed change; options the new size doesn't allow fall back to the first it does. "
                + "A mount that comes with the vehicle only changes its add-ons.",
                AgentTool.Schema(("id!", "string", "Weapon mount id from list_items."),
                                 ("size", "string", "New size, by name or id."),
                                 ("visibility", "string", "New visibility."),
                                 ("flexibility", "string", "New flexibility."),
                                 ("control", "string", "New control."),
                                 ("add_mods", "array", "Add-ons to fit, by id or name (search_catalog kind='weaponmountmod')."),
                                 ("remove_mods", "array", "Add-ons to take off, by id (from list_items) or name."),
                                 ("character", "string", CharacterArgDescription)),
                EditWeaponMountAsync);
            yield return new AgentTool("add_drug",
                "Buy a premade drug (search_catalog kind='drug'), as the Drugs tab's Add Drug does, or put a dose into ware that holds drugs (e.g. a "
                + "drug reservoir), as the cyberware's Add Drug menu item does. Checks books, requirements and availability.",
                AgentTool.Schema(("id!", "string", "Drug id from search_catalog, or its exact English name."),
                                 ("grade", "string", "Drug grade, e.g. 'Standard' (default), 'Street Cooked', 'Pharmaceutical', 'Designer'."),
                                 ("quantity", "integer", "Doses. Default 1."),
                                 ("parent", "string", "Id of cyberware or bioware that holds drugs, from list_items, to put the dose into."),
                                 ("free", "boolean", FreeDescription),
                                 ("black_market", "boolean", BlackMarketDescription),
                                 ("character", "string", CharacterArgDescription)),
                AddDrugAsync);
            yield return new AgentTool("add_cyberware_suite",
                "Install a whole cyberware or bioware suite (search_catalog kind='cyberwaresuite' or 'biowaresuite') at its grade, as Special > Add "
                + "Cyberware/Bioware Suite does. Suites come from custom data, such as ones saved with create_cyberware_suite.",
                AgentTool.Schema(("id!", "string", "Suite id from search_catalog, or its exact name."),
                                 ("kind", "string", "'cyberware' (default) or 'bioware'."),
                                 ("character", "string", CharacterArgDescription)),
                AddCyberwareSuiteAsync);
            yield return new AgentTool("create_cyberware_suite",
                "Save all of the character's cyberware (or bioware) as a named suite in a custom data file, as Special > Create Cyberware/Bioware "
                + "Suite does. All of it must share one grade. Chummer may need a restart before the new suite shows in search_catalog.",
                AgentTool.Schema(("name!", "string", "The suite's name."),
                                 ("kind", "string", "'cyberware' (default) or 'bioware'."),
                                 ("file_name", "string", "Data file to add it to, e.g. 'custom_cyberware.xml' (the default). It must start with 'custom_' and end "
                                                         + "with '_cyberware.xml' or '_bioware.xml'."),
                                 ("character", "string", CharacterArgDescription)),
                CreateCyberwareSuiteAsync);
            yield return new AgentTool("add_packs_kit",
                "Add a PACKS kit (search_catalog kind='packs'): the qualities, martial arts, spells, complex forms, programs, starting nuyen, armor, "
                + "weapons, gear, cyberware, lifestyles and vehicles it holds, as Special > Add PACKS Kit does.",
                AgentTool.Schema(("id!", "string", "Kit name from search_catalog."),
                                 ("category", "string", "The kit's category, when kits in different categories share a name."),
                                 ("martial_art", "string", "For kits that let the character pick a martial art: its id or name."),
                                 ("character", "string", CharacterArgDescription)),
                AddPACKSKitAsync);
            yield return new AgentTool("create_packs_kit",
                "Save parts of the character as a custom PACKS kit, as Special > Create PACKS Kit does. The kit goes in the PACKS folder under the "
                + "'Custom' category, where add_packs_kit can find it.",
                AgentTool.Schema(("name!", "string", "The kit's name."),
                                 ("parts", "array", "Which parts to save. Default all: " + string.Join(", ", CreatePACKSKit.KitParts) + "."),
                                 ("file_name", "string", "Kit file, default 'custom_packs.xml'."),
                                 ("character", "string", CharacterArgDescription)),
                CreatePACKSKitAsync);
            yield return new AgentTool("add_natural_weapon",
                "Make a natural weapon (claws, a bite, horns...), as the weapons tab's Create Natural Weapon dialog does. It costs nothing; the quality "
                + "or power that grants it is added separately.",
                AgentTool.Schema(("name!", "string", "The weapon's name, e.g. 'Claws'."),
                                 ("skill", "string", "Combat skill it uses. Default 'Unarmed Combat'."),
                                 ("damage", "string", "Base damage: 'STR/2' (default), 'STR', or a number from 1 to 20."),
                                 ("damage_bonus", "integer", "Added to the base damage. Default 0."),
                                 ("damage_type", "string", "'P' (physical, default) or 'S' (stun)."),
                                 ("ap", "integer", "Armor penetration. Default 0."),
                                 ("reach", "integer", "Reach. Default 0."),
                                 ("character", "string", CharacterArgDescription)),
                AddNaturalWeaponAsync);
            yield return new AgentTool("add_lifestyle",
                "Add a lifestyle, as the Lifestyles tab's Add Lifestyle (or Add Advanced Lifestyle) dialog does: a base lifestyle from "
                + "search_catalog(kind='lifestyle'), paid for some months, with optional roommates, cost percentage, trust fund and lifestyle qualities. "
                + "Advanced lifestyles (Run Faster) also take Neighborhood/Comforts/Security points and bonus LP.",
                AgentTool.Schema(("id!", "string", "Base lifestyle id from search_catalog, or its exact English name, e.g. 'Low', 'Medium', 'High'."),
                                 ("months", "integer", "Months paid in advance. Default 1."),
                                 ("name", "string", "The lifestyle's name, e.g. 'Penthouse'. Default: the base lifestyle's name."),
                                 ("advanced", "boolean", "An advanced lifestyle with points to spend (Run Faster). Default false."),
                                 ("percentage", "number", "Percentage of the cost the character pays. Default 100."),
                                 ("roommates", "integer", "Number of roommates. Default 0."),
                                 ("split_cost", "boolean", "Roommates split the cost."),
                                 ("trust_fund", "boolean", "Paid by the character's Trust Fund quality."),
                                 ("area", "integer", "Advanced: extra Neighborhood points."),
                                 ("comforts", "integer", "Advanced: extra Comforts points."),
                                 ("security", "integer", "Advanced: extra Security points."),
                                 ("bonus_lp", "integer", "Advanced: bonus lifestyle points."),
                                 ("qualities", "array", "Lifestyle qualities to add, by name or id (search_catalog kind='lifestyle' lists them under the "
                                                        + "lifestyle data); prefix a name with 'free:' to take it for free."),
                                 ("character", "string", CharacterArgDescription)),
                AddLifestyleAsync);
            yield return new AgentTool("edit_lifestyle",
                "Change a lifestyle the character has, as double-clicking it on the Lifestyles tab does: its base lifestyle, name, percentage, "
                + "roommates, trust fund, advanced points, and lifestyle qualities. Only the fields passed change. Use set_item_values for months.",
                AgentTool.Schema(("id!", "string", "Lifestyle id from list_items."),
                                 ("base", "string", "New base lifestyle."),
                                 ("name", "string", "New name."),
                                 ("percentage", "number", "Percentage of the cost the character pays."),
                                 ("roommates", "integer", "Number of roommates."),
                                 ("split_cost", "boolean", "Roommates split the cost."),
                                 ("trust_fund", "boolean", "Paid by the character's Trust Fund quality."),
                                 ("area", "integer", "Advanced: extra Neighborhood points."),
                                 ("comforts", "integer", "Advanced: extra Comforts points."),
                                 ("security", "integer", "Advanced: extra Security points."),
                                 ("bonus_lp", "integer", "Advanced: bonus lifestyle points."),
                                 ("add_qualities", "array", "Lifestyle qualities to add, by name or id; prefix 'free:' to take one for free."),
                                 ("remove_qualities", "array", "Lifestyle qualities to remove, by name or id."),
                                 ("character", "string", CharacterArgDescription)),
                EditLifestyleAsync);
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
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            CharacterCareer frmCareer = frmCharacter as CharacterCareer;
            Character objCharacter = frmCharacter.CharacterObject;
            XmlNode objXmlWeapon = await GetBuyableNodeAsync(objCharacter, "weapons.xml", "/chummer/weapons/weapon", RequiredString(objArgs, "id"),
                                                             token: token).ConfigureAwait(false);
            string strName = objXmlWeapon["name"]?.InnerText;
            bool blnIgnoreRules = await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false);
            bool blnFree = objArgs.Value<bool?>("free") == true;
            bool blnBlackMarket = objArgs.Value<bool?>("black_market") == true;
            decimal decMarkup = GetMarkup(objArgs, frmCharacter);
            OwnedItem objParentItem = null;
            string strParent = objArgs.Value<string>("parent")?.Trim();
            if (!string.IsNullOrEmpty(strParent))
                objParentItem = await FindItemAsync(objCharacter, strParent, token).ConfigureAwait(false);

            Weapon objWeapon;
            bool blnTooExpensive = false;
            switch (objParentItem?.Item)
            {
                case null:
                {
                    Location objLocation = null;
                    string strLocation = objArgs.Value<string>("location")?.Trim();
                    if (!string.IsNullOrEmpty(strLocation))
                        objLocation = await FindLocationAsync(objCharacter.WeaponLocations, strLocation, token).ConfigureAwait(false)
                                      ?? throw new AgentToolException("There is no weapon location '" + strLocation + "'. Add one with manage_location(tab='weapons').");
                    // The Add Weapon dialog's list: no underbarrel-only or cyberware weapons
                    if (!blnIgnoreRules && !await SelectWeapon.WeaponFitsAsync(objXmlWeapon, null, null, objCharacter, new HashSet<string>(), token).ConfigureAwait(false))
                        throw new AgentToolException(strName + " can't be bought on its own (it goes under another weapon, or is part of cyberware).");
                    if (frmCareer != null)
                        (objWeapon, blnTooExpensive) = await frmCareer.AddWeaponFromNodeAsync(objXmlWeapon, blnFree, blnBlackMarket, decMarkup, objLocation, token)
                                                                      .ConfigureAwait(false);
                    else
                        objWeapon = await ((CharacterCreate)frmCharacter).AddWeaponFromNodeAsync(objXmlWeapon, blnFree, blnBlackMarket, objLocation, token: token)
                                                                         .ConfigureAwait(false);
                    break;
                }
                case Weapon objParentWeapon:
                {
                    // Add Underbarrel Weapon: only underbarrel weapons that fit the parent's free mounts
                    if (objParentWeapon.Cyberware)
                        throw new AgentToolException("Underbarrel weapons can't be added to cyberweapons.");
                    if (!blnIgnoreRules)
                    {
                        if (objXmlWeapon["category"]?.InnerText != "Underbarrel Weapons")
                            throw new AgentToolException(strName + " isn't an underbarrel weapon.");
                        XmlNode xmlParentNode = (await objCharacter.LoadDataAsync("weapons.xml", token: token).ConfigureAwait(false))
                            .TryGetNodeById("/chummer/weapons/weapon", objParentWeapon.SourceID);
                        HashSet<string> setMounts = new HashSet<string>(await objParentWeapon.GetAccessoryMountsAsync(token: token).ConfigureAwait(false));
                        if (!await SelectWeapon.WeaponFitsAsync(objXmlWeapon, xmlParentNode, objParentWeapon, objCharacter, setMounts, token).ConfigureAwait(false))
                            throw new AgentToolException(strName + " doesn't fit under " + objParentWeapon.Name + " (its mount is taken, or the weapon doesn't qualify). Free mounts: "
                                                         + string.Join(", ", setMounts) + ".");
                    }

                    bool blnInVehicle = false;
                    for (OwnedItem objLoop = objParentItem.Parent; objLoop != null; objLoop = objLoop.Parent)
                        blnInVehicle |= objLoop.Item is Vehicle;
                    (objWeapon, blnTooExpensive) = await frmCharacter.AddUnderbarrelWeaponFromNodeAsync(
                                                                         objParentWeapon, objXmlWeapon, blnFree, blnBlackMarket, decMarkup, frmCareer != null,
                                                                         frmCareer != null
                                                                             ? await GetExpenseStringAsync(blnInVehicle ? "String_ExpensePurchaseVehicleWeapon" : "String_ExpensePurchaseWeapon",
                                                                                                           token).ConfigureAwait(false)
                                                                             : string.Empty,
                                                                         NuyenExpenseType.AddVehicleWeapon, frmCareer == null, token)
                                                                     .ConfigureAwait(false);
                    break;
                }
                case WeaponMount objMount:
                    if (!blnIgnoreRules)
                    {
                        if (objMount.IsWeaponsFull)
                            throw new AgentToolException(objMount.Name + " already holds as many weapons as it can.");
                        await EnsureWeaponFitsMountAsync(objCharacter, objXmlWeapon, objMount.AllowedWeaponCategories, objMount.WeaponFilter, objMount.Name, token)
                            .ConfigureAwait(false);
                    }

                    (objWeapon, blnTooExpensive) = await frmCharacter.AddWeaponToWeaponMountFromNodeAsync(objMount, null, objXmlWeapon, blnFree, decMarkup,
                                                                                                         frmCareer != null, token).ConfigureAwait(false);
                    break;
                case VehicleMod objMod:
                {
                    // Drone and mechanical arms hold a weapon themselves
                    if (!objMod.Name.StartsWith("Mechanical Arm", StringComparison.Ordinal) && !objMod.Name.Contains("Drone Arm"))
                        throw new AgentToolException(objMod.Name + " can't hold a weapon; pick a weapon mount, or a drone or mechanical arm.");
                    (_, _, WeaponMount objArmMount) = await objCharacter.Vehicles.FindVehicleModAsync(x => x.InternalId == objMod.InternalId, token).ConfigureAwait(false);
                    if (!blnIgnoreRules)
                        await EnsureWeaponFitsMountAsync(objCharacter, objXmlWeapon, objMod.WeaponMountCategories, string.Empty, objMod.Name, token).ConfigureAwait(false);
                    (objWeapon, blnTooExpensive) = await frmCharacter.AddWeaponToWeaponMountFromNodeAsync(objArmMount, objMod, objXmlWeapon, blnFree, decMarkup,
                                                                                                         frmCareer != null, token).ConfigureAwait(false);
                    break;
                }
                default:
                    throw new AgentToolException("Weapons can go under another weapon, into a vehicle's weapon mount, or into a drone or mechanical arm; not into "
                                                 + await GetItemNameAsync(objParentItem.Item, token).ConfigureAwait(false) + ".");
            }

            if (objWeapon == null)
                throw new AgentToolException(blnTooExpensive ? "The character can't afford " + strName + "." : "The weapon was not added.");
            return await FinishAnyEditAsync(frmCharacter, "Added " + await objWeapon.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                          + (objParentItem != null ? " to " + await GetItemNameAsync(objParentItem.Item, token).ConfigureAwait(false) : string.Empty)
                                                          + " (id " + objWeapon.InternalId + ").", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Refuse weapons a mount's Select Weapon dialog would not list: outside its categories or filter, or failing the usual checks.
        /// </summary>
        private static async Task EnsureWeaponFitsMountAsync(Character objCharacter, XmlNode objXmlWeapon, string strCategories, string strFilter, string strMountName,
                                                             CancellationToken token)
        {
            string strName = objXmlWeapon["name"]?.InnerText;
            HashSet<string> setCategories = new HashSet<string>((strCategories ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                                                                .Select(x => x.Trim()));
            string strCategory = objXmlWeapon["category"]?.InnerText ?? string.Empty;
            if (setCategories.Count > 0 && !setCategories.Contains(strCategory))
                throw new AgentToolException(strName + " (" + strCategory + ") doesn't fit " + strMountName + ". It takes: " + string.Join(", ", setCategories) + ".");
            if (!string.IsNullOrEmpty(strFilter) && objXmlWeapon.CreateNavigator().SelectSingleNode("self::node()[" + strFilter + "]") == null)
                throw new AgentToolException(strName + " doesn't fit " + strMountName + ".");
            if (!await SelectWeapon.WeaponFitsAsync(objXmlWeapon, null, null, objCharacter, new HashSet<string>(), token).ConfigureAwait(false))
                throw new AgentToolException(strName + " can't go into " + strMountName + ".");
        }

        private static async Task<object> AddArmorAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            XmlNode objXmlArmor = await GetDataNodeAsync(objCharacter, "armor.xml", "/chummer/armors/armor", RequiredString(objArgs, "id"), token).ConfigureAwait(false);
            int intRating = GetRating(objXmlArmor, OptionalInt(objArgs, "rating"), await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false));
            objXmlArmor = await GetBuyableNodeAsync(objCharacter, "armor.xml", "/chummer/armors/armor", RequiredString(objArgs, "id"), Math.Max(1, intRating),
                                                    token: token).ConfigureAwait(false);
            bool blnFree = objArgs.Value<bool?>("free") == true;
            bool blnBlackMarket = objArgs.Value<bool?>("black_market") == true;
            decimal decMarkup = GetMarkup(objArgs, frmCharacter);
            Armor objArmor;
            bool blnTooExpensive = false;
            if (frmCharacter is CharacterCareer frmCareer)
                (objArmor, blnTooExpensive) = await frmCareer.AddArmorFromNodeAsync(objXmlArmor, intRating, blnFree, blnBlackMarket, decMarkup, token: token)
                                                             .ConfigureAwait(false);
            else
                objArmor = await ((CharacterCreate)frmCharacter).AddArmorFromNodeAsync(objXmlArmor, intRating, blnFree, blnBlackMarket, token: token)
                                                                .ConfigureAwait(false);
            if (objArmor == null)
                throw new AgentToolException(blnTooExpensive
                                                 ? "The character can't afford " + objXmlArmor["name"]?.InnerText + "."
                                                 : "The armor was not added (a choice it needed was cancelled).");
            return await FinishAnyEditAsync(frmCharacter, "Added " + await objArmor.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                          + " (id " + objArmor.InternalId + ").", token).ConfigureAwait(false);
        }

        private static async Task<object> AddGearAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            string strId = RequiredString(objArgs, "id");
            XmlNode objXmlGear = await GetDataNodeAsync(objCharacter, "gear.xml", "/chummer/gears/gear", strId, token).ConfigureAwait(false);
            bool blnIgnoreRules = await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false);
            int intRating = GetRating(objXmlGear, OptionalInt(objArgs, "rating"), blnIgnoreRules);
            objXmlGear = await GetBuyableNodeAsync(objCharacter, "gear.xml", "/chummer/gears/gear", strId, Math.Max(1, intRating), token: token).ConfigureAwait(false);
            decimal decQuantity = objArgs.Value<decimal?>("quantity")
                                  ?? (decimal.TryParse(objXmlGear["costfor"]?.InnerText, System.Globalization.NumberStyles.Any,
                                                       GlobalSettings.InvariantCultureInfo, out decimal decCostFor) && decCostFor > 0
                                      ? decCostFor
                                      : 1);
            if (decQuantity <= 0)
                throw new AgentToolException("`quantity` must be more than 0.");
            bool blnFree = objArgs.Value<bool?>("free") == true;
            bool blnBlackMarket = objArgs.Value<bool?>("black_market") == true;
            string strForced = objArgs.Value<string>("forced_value") ?? string.Empty;
            string strGearName = objXmlGear["name"]?.InnerText;

            OwnedItem objParentItem = null;
            string strParent = objArgs.Value<string>("parent")?.Trim();
            if (!string.IsNullOrEmpty(strParent))
                objParentItem = await FindItemAsync(objCharacter, strParent, token).ConfigureAwait(false);
            object objParent = objParentItem?.Item;
            // The closest thing up the tree that isn't gear (armor, cyberware, an accessory, a vehicle...), and the vehicle it is in, if any
            OwnedItem objHolder = objParentItem;
            while (objHolder?.Item is Gear)
                objHolder = objHolder.Parent;
            OwnedItem objVehicleItem = objParentItem;
            while (objVehicleItem != null && !(objVehicleItem.Item is Vehicle))
                objVehicleItem = objVehicleItem.Parent;
            Vehicle objInVehicle = objVehicleItem?.Item as Vehicle;

            Location objLocation = null;
            string strLocation = objArgs.Value<string>("location")?.Trim();
            if (!string.IsNullOrEmpty(strLocation))
            {
                ThreadSafeObservableCollection<Location> lstLocations = objParent == null
                    ? objCharacter.GearLocations
                    : objParent is Vehicle objLocationVehicle
                        ? objLocationVehicle.Locations
                        : throw new AgentToolException("`location` only applies to gear bought for the character or for a vehicle.");
                objLocation = await FindLocationAsync(lstLocations, strLocation, token).ConfigureAwait(false)
                              ?? throw new AgentToolException("There is no location '" + strLocation + "'. Add one with manage_location.");
            }

            decimal decMarkup = GetMarkup(objArgs, frmCharacter);
            if (!blnIgnoreRules)
                await EnsureGearFitsParentAsync(objCharacter, objXmlGear, objParent, objInVehicle != null, token).ConfigureAwait(false);

            // Each parent's Add Gear / Add as Plugin menu item
            Gear objGear;
            bool blnTooExpensive = false;
            string strRefusal = string.Empty;
            if (frmCharacter is CharacterCareer frmCareer)
            {
                (objGear, strRefusal) = await BuyCareerGearAsync(frmCareer, objXmlGear, intRating, decQuantity, objParent, objHolder?.Item, objInVehicle,
                                                                 objLocation, blnFree, blnBlackMarket, decMarkup, strForced, token).ConfigureAwait(false);
            }
            else
            {
                CharacterCreate frmCreate = (CharacterCreate)frmCharacter;
                switch (objParent)
                {
                    case null:
                        objGear = await frmCreate.AddGearFromNodeAsync(objXmlGear, intRating, decQuantity, null, objLocation, blnFree, blnBlackMarket,
                                                                       false, true, strForced, token).ConfigureAwait(false);
                        break;
                    case Gear objParentGear:
                        switch (objHolder?.Item)
                        {
                            case null:
                                objGear = await frmCreate.AddGearFromNodeAsync(objXmlGear, intRating, decQuantity, objParentGear, null, blnFree, blnBlackMarket,
                                                                               false, true, strForced, token).ConfigureAwait(false);
                                break;
                            case Armor objArmor:
                                objGear = await frmCreate.AddArmorGearFromNodeAsync(objArmor, null, objParentGear, objXmlGear, intRating, decQuantity, blnFree,
                                                                                    blnBlackMarket, false, strForced, token).ConfigureAwait(false);
                                break;
                            case ArmorMod objMod:
                                objGear = await frmCreate.AddArmorGearFromNodeAsync(objMod.Parent, objMod, objParentGear, objXmlGear, intRating, decQuantity, blnFree,
                                                                                    blnBlackMarket, false, strForced, token).ConfigureAwait(false);
                                break;
                            case Cyberware objCyberware:
                                objGear = await frmCreate.AddChildGearFromNodeAsync(objXmlGear, intRating, decQuantity, objParentGear, objParentGear.Children,
                                                                                    objInVehicle == null
                                                                                    && ((objParentGear.Parent as Gear)?.Equipped
                                                                                        ?? await objCyberware.GetIsModularCurrentlyEquippedAsync(token).ConfigureAwait(false)),
                                                                                    blnFree, blnBlackMarket, false, null, strForced, token).ConfigureAwait(false);
                                break;
                            case WeaponAccessory objAccessory:
                                objGear = objInVehicle == null
                                    ? await frmCreate.AddChildGearFromNodeAsync(objXmlGear, intRating, decQuantity, objParentGear, objParentGear.Children,
                                                                                (objParentGear.Parent as Gear)?.Equipped ?? objAccessory.Equipped,
                                                                                blnFree, blnBlackMarket, false, null, strForced, token).ConfigureAwait(false)
                                    : await frmCreate.AddChildGearFromNodeAsync(objXmlGear, intRating, decQuantity, objParentGear, objParentGear.Children, false,
                                                                                blnFree, blnBlackMarket, false, (x, t) => objInVehicle.Weapons.AddAsync(x, t),
                                                                                strForced, token).ConfigureAwait(false);
                                break;
                            case Vehicle objVehicle:
                                // A vehicle sensor's plugins
                                objGear = await frmCreate.AddChildGearFromNodeAsync(objXmlGear, intRating, decQuantity, objParentGear, objParentGear.Children, false,
                                                                                    blnFree, null, false, (x, t) => objVehicle.Weapons.AddAsync(x, t),
                                                                                    strForced, token).ConfigureAwait(false);
                                break;
                            default:
                                throw new AgentToolException("Gear can't be plugged into that.");
                        }

                        break;
                    case Armor objArmor:
                        objGear = await frmCreate.AddArmorGearFromNodeAsync(objArmor, null, null, objXmlGear, intRating, decQuantity, blnFree, blnBlackMarket,
                                                                            false, strForced, token).ConfigureAwait(false);
                        break;
                    case ArmorMod objArmorMod:
                        objGear = await frmCreate.AddArmorGearFromNodeAsync(objArmorMod.Parent, objArmorMod, null, objXmlGear, intRating, decQuantity, blnFree,
                                                                            blnBlackMarket, false, strForced, token).ConfigureAwait(false);
                        break;
                    case Cyberware objCyberware:
                        (objGear, blnTooExpensive) = await frmCreate.AddGearToCyberwareFromNodeAsync(objCyberware, objXmlGear, intRating, decQuantity, blnFree,
                                                                                                    blnBlackMarket, false, 0, false, true, true,
                                                                                                    objInVehicle == null, string.Empty,
                                                                                                    NuyenExpenseType.AddCyberwareGear, strForced, token)
                                                                     .ConfigureAwait(false);
                        break;
                    case WeaponAccessory objAccessory:
                        objGear = objInVehicle == null
                            ? await frmCreate.AddChildGearFromNodeAsync(objXmlGear, intRating, decQuantity, objAccessory, objAccessory.GearChildren,
                                                                        objAccessory.Equipped, blnFree, blnBlackMarket, false, null, strForced, token)
                                             .ConfigureAwait(false)
                            : await frmCreate.AddChildGearFromNodeAsync(objXmlGear, intRating, decQuantity, objAccessory, objAccessory.GearChildren, false,
                                                                        blnFree, blnBlackMarket, false, (x, t) => objAccessory.Parent.Children.AddAsync(x, t),
                                                                        strForced, token).ConfigureAwait(false);
                        break;
                    case Vehicle objVehicle:
                        (objGear, blnTooExpensive) = await frmCreate.AddVehicleGearFromNodeAsync(objVehicle, objLocation, objXmlGear, intRating, decQuantity,
                                                                                                blnFree, blnBlackMarket, false, true, strForced, token)
                                                                     .ConfigureAwait(false);
                        break;
                    default:
                        throw new AgentToolException("Gear can't be put into " + await GetItemNameAsync(objParent, token).ConfigureAwait(false) + ".");
                }
            }

            if (objGear == null)
                throw new AgentToolException(blnTooExpensive
                                                 ? "The character can't afford " + strGearName + "."
                                                 : await GetRefusalMessageAsync(strRefusal, "The gear was not added (its choice dialog was cancelled, or it could not be created).",
                                                                                token).ConfigureAwait(false));
            return await FinishAnyEditAsync(frmCharacter, "Added " + await objGear.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + (objParent != null ? " to " + await GetItemNameAsync(objParent, token).ConfigureAwait(false) : string.Empty)
                                                    + " (id " + objGear.InternalId + ", quantity "
                                                    + objGear.Quantity.ToString(GlobalSettings.InvariantCultureInfo) + ").", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Buy gear in career mode, paid from the character's nuyen, as each parent's Add Gear / Add as Plugin menu item does in the career editor.
        /// </summary>
        /// <returns>The gear that holds the purchase (null if nothing was bought), and the language key of why not.</returns>
        private static async Task<(Gear Gear, string Refusal)> BuyCareerGearAsync(CharacterCareer frmCareer, XmlNode objXmlGear, int intRating, decimal decQuantity,
                                                                                  object objParent, object objHolder, Vehicle objInVehicle, Location objLocation,
                                                                                  bool blnFree, bool blnBlackMarket, decimal decMarkup, string strForced,
                                                                                  CancellationToken token)
        {
            Gear objGear;
            bool blnTooExpensive;
            switch (objParent)
            {
                case null:
                    return await frmCareer.AddGearFromNodeAsync(objXmlGear, intRating, decQuantity, blnFree, blnBlackMarket, false, true, decMarkup,
                                                                objLocation: objLocation, strForceItemValue: strForced, token: token).ConfigureAwait(false);
                case Gear objParentGear:
                    switch (objHolder)
                    {
                        case null:
                            return await frmCareer.AddGearFromNodeAsync(objXmlGear, intRating, decQuantity, blnFree, blnBlackMarket, false, true, decMarkup,
                                                                        objParentGear, strForceItemValue: strForced, token: token).ConfigureAwait(false);
                        case Armor objArmor:
                            return await frmCareer.AddArmorGearFromNodeAsync(objArmor, null, objParentGear, objXmlGear, intRating, decQuantity, blnFree,
                                                                             blnBlackMarket, false, decMarkup, strForced, token).ConfigureAwait(false);
                        case ArmorMod objMod:
                            return await frmCareer.AddArmorGearFromNodeAsync(objMod.Parent, objMod, objParentGear, objXmlGear, intRating, decQuantity, blnFree,
                                                                             blnBlackMarket, false, decMarkup, strForced, token).ConfigureAwait(false);
                        case Cyberware objCyberware:
                            (objGear, blnTooExpensive) = await frmCareer.BuyChildGearFromNodeAsync(
                                objXmlGear, intRating, decQuantity, objParentGear, objParentGear.Children,
                                objInVehicle == null
                                && ((objParentGear.Parent as Gear)?.Equipped ?? await objCyberware.GetIsModularCurrentlyEquippedAsync(token).ConfigureAwait(false)),
                                blnFree, false, decMarkup, "String_ExpensePurchaseCyberwareGear", NuyenExpenseType.AddCyberwareGear, true, null, strForced,
                                token).ConfigureAwait(false);
                            break;
                        case WeaponAccessory objAccessory:
                            (objGear, blnTooExpensive) = await frmCareer.BuyChildGearFromNodeAsync(
                                objXmlGear, intRating, decQuantity, objParentGear, objParentGear.Children,
                                objInVehicle == null && ((objParentGear.Parent as Gear)?.Equipped ?? objAccessory.Equipped),
                                blnFree, false, decMarkup, "String_ExpensePurchaseWeaponGear", NuyenExpenseType.AddWeaponGear, true,
                                objInVehicle == null
                                    ? (Func<Weapon, CancellationToken, Task>)null
                                    : async (x, t) =>
                                    {
                                        await x.SetParentVehicleAsync(objInVehicle, t).ConfigureAwait(false);
                                        await objInVehicle.Weapons.AddAsync(x, t).ConfigureAwait(false);
                                    }, strForced, token).ConfigureAwait(false);
                            break;
                        case Vehicle objVehicle:
                            // A vehicle sensor's plugins
                            (objGear, blnTooExpensive) = await frmCareer.BuyChildGearFromNodeAsync(
                                objXmlGear, intRating, decQuantity, objParentGear, objParentGear.Children, false, blnFree, false, decMarkup,
                                "String_ExpensePurchaseVehicleGear", NuyenExpenseType.AddVehicleGear, true,
                                (x, t) => objVehicle.Weapons.AddAsync(x, t), strForced, token).ConfigureAwait(false);
                            break;
                        default:
                            throw new AgentToolException("Gear can't be plugged into that.");
                    }

                    break;
                case Armor objArmor:
                    return await frmCareer.AddArmorGearFromNodeAsync(objArmor, null, null, objXmlGear, intRating, decQuantity, blnFree, blnBlackMarket,
                                                                     false, decMarkup, strForced, token).ConfigureAwait(false);
                case ArmorMod objArmorMod:
                    return await frmCareer.AddArmorGearFromNodeAsync(objArmorMod.Parent, objArmorMod, null, objXmlGear, intRating, decQuantity, blnFree,
                                                                     blnBlackMarket, false, decMarkup, strForced, token).ConfigureAwait(false);
                case Cyberware objCyberware:
                    (objGear, blnTooExpensive) = await frmCareer.AddGearToCyberwareFromNodeAsync(
                        objCyberware, objXmlGear, intRating, decQuantity, blnFree, false, false, decMarkup, true, false, false, objInVehicle == null,
                        await GetExpenseStringAsync("String_ExpensePurchaseCyberwareGear", token).ConfigureAwait(false), NuyenExpenseType.AddCyberwareGear,
                        strForced, token).ConfigureAwait(false);
                    break;
                case WeaponAccessory objAccessory:
                    (objGear, blnTooExpensive) = await frmCareer.BuyChildGearFromNodeAsync(
                        objXmlGear, intRating, decQuantity, objAccessory, objAccessory.GearChildren, objInVehicle == null && objAccessory.Equipped,
                        blnFree, false, decMarkup, "String_ExpensePurchaseWeaponGear", NuyenExpenseType.AddWeaponGear, false,
                        objInVehicle == null ? (Func<Weapon, CancellationToken, Task>)null : (x, t) => objAccessory.Parent.Children.AddAsync(x, t),
                        strForced, token).ConfigureAwait(false);
                    break;
                case Vehicle objVehicle:
                    (objGear, blnTooExpensive) = await frmCareer.AddVehicleGearFromNodeAsync(objVehicle, objLocation, objXmlGear, intRating, decQuantity,
                                                                                            blnFree, blnBlackMarket, false, true, strForced, token)
                                                                 .ConfigureAwait(false);
                    break;
                default:
                    throw new AgentToolException("Gear can't be put into " + await GetItemNameAsync(objParent, token).ConfigureAwait(false) + ".");
            }

            return (objGear, objGear == null && blnTooExpensive ? "Message_NotEnoughNuyen" : string.Empty);
        }

        /// <summary>
        /// Refuse gear the parent's Select Gear dialog would not list: outside the categories or names the parent takes, needing a parent it
        /// doesn't have, or failing the parent's own requirements.
        /// </summary>
        private static async Task EnsureGearFitsParentAsync(Character objCharacter, XmlNode objXmlGear, object objParent, bool blnInVehicle, CancellationToken token)
        {
            string strName = objXmlGear["name"]?.InnerText;
            string strCategory = objXmlGear["category"]?.InnerText ?? string.Empty;
            if (objParent == null)
            {
                if (objXmlGear["requireparent"] != null)
                    throw new AgentToolException(strName + " has to go into another item; pass `parent`.");
                return;
            }

            // The categories and names each Add Gear menu item lets its dialog show
            HashSet<string> setCategories = new HashSet<string>();
            HashSet<string> setNames = new HashSet<string>();
            string strParentCapacity = null;
            switch (objParent)
            {
                case Cyberware objCyberware:
                    if (objCyberware.AllowGear == null)
                        throw new AgentToolException(objCyberware.Name + " can't hold gear.");
                    // Cyberware in a vehicle takes any of its gear categories; the character's own also honors gear names
                    foreach (XmlNode objNode in blnInVehicle
                                 ? objCyberware.AllowGear.ChildNodes.Cast<XmlNode>()
                                 : objCyberware.AllowGear.SelectNodes("gearcategory")?.Cast<XmlNode>() ?? Enumerable.Empty<XmlNode>())
                        setCategories.Add(objNode.InnerText);
                    if (!blnInVehicle)
                    {
                        foreach (XmlNode objNode in objCyberware.AllowGear.SelectNodes("gearname")?.Cast<XmlNode>() ?? Enumerable.Empty<XmlNode>())
                            setNames.Add(objNode.InnerText);
                    }

                    strParentCapacity = objCyberware.Capacity == "0" ? null : objCyberware.Capacity;
                    break;
                case WeaponAccessory objAccessory:
                    if (objAccessory.AllowGear == null)
                        throw new AgentToolException(objAccessory.Name + " can't hold gear.");
                    foreach (XmlNode objNode in objAccessory.AllowGear.ChildNodes)
                        setCategories.Add(objNode.InnerText);
                    break;
                case Vehicle _:
                    break;
                case IHasXmlDataNode objWithNode:
                    XPathNavigator objParentNode = await objWithNode.GetNodeXPathAsync(token).ConfigureAwait(false);
                    if (objParentNode != null)
                    {
                        foreach (XPathNavigator objCategory in objParentNode.Select("addoncategory"))
                            setCategories.Add(objCategory.Value);
                    }

                    if (objParent is Gear objGearParent)
                        strParentCapacity = objGearParent.Capacity;
                    break;
            }

            if (setCategories.Count > 0 && !setCategories.Contains(strCategory))
                throw new AgentToolException(strName + " (" + strCategory + ") doesn't go into " + await GetItemNameAsync(objParent, token).ConfigureAwait(false)
                                             + ". It takes: " + string.Join(", ", setCategories.OrderBy(x => x)) + ".");
            if (setNames.Count > 0 && !setNames.Contains(strName))
                throw new AgentToolException(strName + " doesn't go into " + await GetItemNameAsync(objParent, token).ConfigureAwait(false) + ". It takes: "
                                             + string.Join(", ", setNames.OrderBy(x => x)) + ".");
            // A parent that provides capacity only takes items that use capacity
            if (setCategories.Count > 0 && !string.IsNullOrEmpty(strParentCapacity)
                                        && (!strParentCapacity.Contains('[') || strParentCapacity.Contains("/["))
                                        && strCategory != "Custom" && objXmlGear["capacity"]?.InnerText.Contains('[') != true)
                throw new AgentToolException(strName + " doesn't use capacity, so it can't be plugged into " + await GetItemNameAsync(objParent, token).ConfigureAwait(false) + ".");
            if (objParent is Armor && strCategory != "Custom" && objXmlGear["armorcapacity"]?.InnerText.Contains('[') != true)
                throw new AgentToolException(strName + " doesn't fit into armor (it has no armor capacity cost).");
            if (!await SelectGear.GearFitsParentAsync(objXmlGear.CreateNavigator(), (objParent as IHasXmlDataNode) != null
                                                          ? await ((IHasXmlDataNode)objParent).GetNodeXPathAsync(token).ConfigureAwait(false)
                                                          : null, objCharacter, objParent, token).ConfigureAwait(false))
                throw new AgentToolException(strName + " can't go into " + await GetItemNameAsync(objParent, token).ConfigureAwait(false)
                                             + " (it or its parent doesn't meet the other's requirements).");
        }

        private static async Task<object> AddCyberwareAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            CharacterCareer frmCareer = frmCharacter as CharacterCareer;
            Character objCharacter = frmCharacter.CharacterObject;
            decimal decMarkup = GetMarkup(objArgs, frmCharacter);
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

            // What it goes into: the character's own cyberware (as a plugin) or, on a vehicle, a vehicle mod or cyberware in one
            Cyberware objParent = null;
            VehicleMod objVehicleMod = null;
            Vehicle objVehicle = null;
            string strParent = objArgs.Value<string>("parent")?.Trim();
            if (!string.IsNullOrEmpty(strParent))
            {
                OwnedItem objParentItem = await FindItemAsync(objCharacter, strParent, token).ConfigureAwait(false);
                for (OwnedItem objLoop = objParentItem; objLoop != null; objLoop = objLoop.Parent)
                {
                    if (objVehicleMod == null && objLoop.Item is VehicleMod objLoopMod)
                        objVehicleMod = objLoopMod;
                    else if (objLoop.Item is Vehicle objLoopVehicle)
                        objVehicle = objLoopVehicle;
                }

                switch (objParentItem.Item)
                {
                    case Cyberware objParentCyberware:
                        objParent = objParentCyberware;
                        // Add as Plugin: only the subsystem categories the parent takes
                        if (string.IsNullOrWhiteSpace(objParent.AllowedSubsystems))
                            throw new AgentToolException(objParent.Name + " doesn't take plugins.");
                        break;
                    case VehicleMod objMod:
                        if (!objMod.AllowCyberware)
                            throw new AgentToolException(objMod.Name + " can't hold cyberware.");
                        break;
                    default:
                        throw new AgentToolException("Cyberware can be installed into other cyberware or a vehicle mod that holds cyberware, not into "
                                                     + await GetItemNameAsync(objParentItem.Item, token).ConfigureAwait(false) + ".");
                }

                if (objVehicle != null && blnBioware)
                    throw new AgentToolException("Vehicles can't have bioware.");
            }

            // Grades the character may use (banned grades are left out, as in the Select Cyberware dialog). Plugins take their parent's grade.
            string strGrade = objParent != null
                ? (await objParent.GetGradeAsync(token).ConfigureAwait(false))?.Name
                : objArgs.Value<string>("grade")?.Trim();
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

            if (!blnIgnoreRules)
            {
                string strSubsystems = objParent != null ? objParent.AllowedSubsystems : objVehicleMod?.Subsystems;
                if (!string.IsNullOrWhiteSpace(strSubsystems))
                {
                    HashSet<string> setSubsystems = new HashSet<string>(strSubsystems.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
                    string strCategory = objXmlCyberware["category"]?.InnerText ?? string.Empty;
                    if (!setSubsystems.Contains(strCategory))
                        throw new AgentToolException(objXmlCyberware["name"]?.InnerText + " (" + strCategory + ") doesn't go into "
                                                     + (objParent?.Name ?? objVehicleMod?.Name) + ". It takes: " + string.Join(", ", setSubsystems) + ".");
                }
            }

            bool blnFree = objArgs.Value<bool?>("free") == true;
            bool blnBlackMarket = objArgs.Value<bool?>("black_market") == true;
            string strForced = objArgs.Value<string>("forced_value") ?? string.Empty;
            string strNotInstalled = frmCareer != null
                ? "Chummer did not install it (the character can't afford it, it doesn't fit, or a choice it needed was cancelled)."
                : "Chummer did not install it (it may not fit, or a choice it needed was cancelled).";
            if (objVehicle != null)
            {
                // The Vehicles tab's Add Cyberware
                Cyberware objVehicleCyberware = await frmCharacter.AddVehicleCyberwareFromNodeAsync(objVehicle, objVehicleMod, objParent, objXmlCyberware, objGrade,
                                                                                                    intRating, decMarkup, blnFree, blnBlackMarket, token, strForced,
                                                                                                    false).ConfigureAwait(false)
                                                ?? throw new AgentToolException(strNotInstalled);
                return await FinishAnyEditAsync(frmCharacter, "Installed " + await objVehicleCyberware.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                              + " in " + objVehicle.Name + " (id " + objVehicleCyberware.InternalId + ").", token).ConfigureAwait(false);
            }

            decimal decOldEssence = await objCharacter.EssenceAsync(token: token).ConfigureAwait(false);
            Cyberware objCyberware = (frmCareer != null
                                         ? await frmCareer.AddCyberwareFromNodeAsync(objXmlCyberware, eSource, objGrade, intRating, objParent, decMarkup, blnFree,
                                                                                     blnBlackMarket, blnPromptForBaseWeapon: false, token: token,
                                                                                     strForceValue: strForced, blnShowNotEnoughNuyenMessage: false)
                                                                 .ConfigureAwait(false)
                                         : await ((CharacterCreate)frmCharacter).AddCyberwareFromNodeAsync(objXmlCyberware, eSource, objGrade, intRating, objParent,
                                                                                                           blnFree: blnFree, blnBlackMarketDiscount: blnBlackMarket,
                                                                                                           blnPromptForBaseWeapon: false, token: token,
                                                                                                           strForceValue: strForced).ConfigureAwait(false))
                                     ?? throw new AgentToolException(strNotInstalled);
            decimal decEssence = await objCharacter.EssenceAsync(token: token).ConfigureAwait(false);
            return await FinishAnyEditAsync(frmCharacter, "Installed " + await objCyberware.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                          + " (id " + objCyberware.InternalId + "), essence "
                                                          + decOldEssence.ToString(GlobalSettings.InvariantCultureInfo) + " -> "
                                                          + decEssence.ToString(GlobalSettings.InvariantCultureInfo) + ".", token).ConfigureAwait(false);
        }

        private static async Task<object> AddVehicleAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            XmlNode objXmlVehicle = await GetBuyableNodeAsync(frmCharacter.CharacterObject, "vehicles.xml", "/chummer/vehicles/vehicle", RequiredString(objArgs, "id"),
                                                              token: token).ConfigureAwait(false);
            bool blnFree = objArgs.Value<bool?>("free") == true;
            bool blnBlackMarket = objArgs.Value<bool?>("black_market") == true;
            decimal decMarkup = GetMarkup(objArgs, frmCharacter);
            Vehicle objVehicle;
            bool blnTooExpensive = false;
            if (frmCharacter is CharacterCareer frmCareer)
            {
                // The career Select Vehicle dialog can also buy it used
                bool blnUsed = objArgs.Value<bool?>("used") == true;
                decimal decUsedPercent = objArgs.Value<decimal?>("used_discount") ?? 0;
                if (!blnUsed && decUsedPercent != 0)
                    throw new AgentToolException("`used_discount` needs `used`.");
                if (decUsedPercent < 0 || decUsedPercent > 100)
                    throw new AgentToolException("`used_discount` must be from 0 to 100.");
                string strUsedAvail = string.Empty;
                decimal decUsedCost = 0;
                if (blnUsed)
                    (strUsedAvail, decUsedCost) = await SelectVehicle.GetUsedVehicleAvailAndCostAsync(objXmlVehicle.CreateNavigator(), frmCareer.CharacterObject,
                                                                                                      decUsedPercent, token).ConfigureAwait(false);
                (objVehicle, blnTooExpensive) = await frmCareer.AddVehicleFromNodeAsync(objXmlVehicle, blnFree, blnBlackMarket, decMarkup, blnUsed,
                                                                                        strUsedAvail, decUsedCost, token: token).ConfigureAwait(false);
            }
            else
            {
                if (objArgs.Value<bool?>("used") == true)
                    throw new AgentToolException("Used vehicles can only be bought in career mode.");
                objVehicle = await ((CharacterCreate)frmCharacter).AddVehicleFromNodeAsync(objXmlVehicle, blnFree, blnBlackMarket, token: token).ConfigureAwait(false);
            }

            if (objVehicle == null)
                throw new AgentToolException(blnTooExpensive ? "The character can't afford " + objXmlVehicle["name"]?.InnerText + "." : "The vehicle was not added.");
            return await FinishAnyEditAsync(frmCharacter, "Added " + await objVehicle.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                          + " (id " + objVehicle.InternalId + ").", token).ConfigureAwait(false);
        }

        private static async Task<object> AddLifestyleAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            bool blnAdvanced = objArgs.Value<bool?>("advanced") == true;
            XmlNode objXmlLifestyle = await GetBaseLifestyleNodeAsync(objCharacter, RequiredString(objArgs, "id"), blnAdvanced, token).ConfigureAwait(false);
            int intMonths = Math.Max(1, OptionalInt(objArgs, "months") ?? 1);
            // Add Lifestyle (or Add Advanced Lifestyle): a new lifestyle filled in through the Select Lifestyle dialog
            Lifestyle objLifestyle = new Lifestyle(objCharacter);
            try
            {
                await objLifestyle.SetStyleTypeAsync(blnAdvanced ? LifestyleType.Advanced : LifestyleType.Standard, token).ConfigureAwait(false);
                await objLifestyle.SetBaseLifestyleAsync(objXmlLifestyle["name"]?.InnerText, token).ConfigureAwait(false);
                await objLifestyle.SetNameAsync(objArgs.Value<string>("name")?.Trim() is string strName && strName.Length > 0
                                                    ? strName
                                                    : objXmlLifestyle["name"]?.InnerText, token).ConfigureAwait(false);
                await AddLifestyleQualitiesAsync(objCharacter, objLifestyle, objArgs["qualities"] as JArray, token).ConfigureAwait(false);
                await ApplyLifestyleArgsAsync(objCharacter, objLifestyle, objXmlLifestyle, objArgs, token).ConfigureAwait(false);
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

        private static async Task<object> EditLifestyleAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            OwnedItem objOwned = await FindItemAsync(objCharacter, RequiredString(objArgs, "id"), token).ConfigureAwait(false);
            if (!(objOwned.Item is Lifestyle objLifestyle))
                throw new AgentToolException("That isn't a lifestyle.");
            bool blnAdvanced = await objLifestyle.GetStyleTypeAsync(token).ConfigureAwait(false) != LifestyleType.Standard;
            string strBase = objArgs.Value<string>("base")?.Trim();
            XmlNode objXmlLifestyle = await GetBaseLifestyleNodeAsync(objCharacter, string.IsNullOrEmpty(strBase)
                                                                          ? await objLifestyle.GetBaseLifestyleAsync(token).ConfigureAwait(false)
                                                                          : strBase, blnAdvanced, token).ConfigureAwait(false);
            // The Select Lifestyle dialog edits the lifestyle in place
            if (!string.IsNullOrEmpty(strBase))
                await objLifestyle.SetBaseLifestyleAsync(objXmlLifestyle["name"]?.InnerText, token).ConfigureAwait(false);
            string strName = objArgs.Value<string>("name")?.Trim();
            if (!string.IsNullOrEmpty(strName))
                await objLifestyle.SetNameAsync(strName, token).ConfigureAwait(false);
            if (objArgs["remove_qualities"] is JArray objRemove)
            {
                List<LifestyleQuality> lstQualities = await objLifestyle.LifestyleQualities.ToListAsync(token).ConfigureAwait(false);
                foreach (JToken objToken in objRemove)
                {
                    string strWanted = objToken.ToString().Trim();
                    LifestyleQuality objQuality = lstQualities.Find(x => string.Equals(x.InternalId, strWanted, StringComparison.OrdinalIgnoreCase))
                                                  ?? lstQualities.Find(x => string.Equals(x.Name, strWanted, StringComparison.OrdinalIgnoreCase))
                                                  ?? throw new AgentToolException("The lifestyle has no quality '" + strWanted + "'.");
                    // The dialog's Delete Quality button: built-in qualities stay
                    if (await objQuality.GetOriginSourceAsync(token).ConfigureAwait(false) == QualitySource.BuiltIn)
                        throw new AgentToolException(objQuality.Name + " comes with the base lifestyle and can't be removed.");
                    await objQuality.RemoveAsync(false, token).ConfigureAwait(false);
                }
            }

            await AddLifestyleQualitiesAsync(objCharacter, objLifestyle, objArgs["add_qualities"] as JArray, token).ConfigureAwait(false);
            await ApplyLifestyleArgsAsync(objCharacter, objLifestyle, objXmlLifestyle, objArgs, token).ConfigureAwait(false);
            return await FinishItemEditAsync(frmCreate, objLifestyle, "Updated " + await objLifestyle.GetCurrentDisplayNameAsync(token).ConfigureAwait(false) + ".", token)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// A base lifestyle the Select Lifestyle dialog lists: in the enabled books, and for standard lifestyles from the core rulebook or a contract.
        /// </summary>
        private static async Task<XmlNode> GetBaseLifestyleNodeAsync(Character objCharacter, string strId, bool blnAdvanced, CancellationToken token)
        {
            XmlNode objXmlLifestyle = await GetDataNodeAsync(objCharacter, "lifestyles.xml", "/chummer/lifestyles/lifestyle", strId, token).ConfigureAwait(false);
            if (!await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false))
            {
                await EnsureBookEnabledAsync(objXmlLifestyle, objCharacter, token).ConfigureAwait(false);
                if (!blnAdvanced && objXmlLifestyle["source"]?.InnerText != "SR5" && objXmlLifestyle["category"]?.InnerText != "Contracts")
                    throw new AgentToolException(objXmlLifestyle["name"]?.InnerText + " is only available as an advanced lifestyle (advanced=true).");
            }

            return objXmlLifestyle;
        }

        /// <summary>
        /// Add lifestyle qualities by name or id, as the Select Lifestyle dialog's Add Quality does; "free:" before a name takes it for free.
        /// </summary>
        private static async Task AddLifestyleQualitiesAsync(Character objCharacter, Lifestyle objLifestyle, JArray objQualities, CancellationToken token)
        {
            if (objQualities == null)
                return;
            bool blnIgnoreRules = await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false);
            bool blnStandard = await objLifestyle.GetStyleTypeAsync(token).ConfigureAwait(false) == LifestyleType.Standard;
            foreach (JToken objToken in objQualities)
            {
                string strWanted = objToken.ToString().Trim();
                bool blnFree = strWanted.StartsWith("free:", StringComparison.OrdinalIgnoreCase);
                if (blnFree)
                    strWanted = strWanted.Substring(5).Trim();
                XmlNode objXmlQuality = await GetDataNodeAsync(objCharacter, "lifestyles.xml", "/chummer/qualities/quality", strWanted, token).ConfigureAwait(false);
                if (!blnIgnoreRules)
                {
                    // The Select Lifestyle Quality list: enabled books, core-book or contract qualities for standard lifestyles, and requirements
                    await EnsureBookEnabledAsync(objXmlQuality, objCharacter, token).ConfigureAwait(false);
                    if (blnStandard && objXmlQuality["source"]?.InnerText != "SR5" && objXmlQuality["category"]?.InnerText != "Contracts")
                        throw new AgentToolException(objXmlQuality["name"]?.InnerText + " needs an advanced lifestyle.");
                    if (!await objXmlQuality.CreateNavigator().RequirementsMetAsync(objCharacter, objLifestyle, token: token).ConfigureAwait(false))
                        throw new AgentToolException("The lifestyle doesn't meet the requirements for " + objXmlQuality["name"]?.InnerText + ".");
                }

                if (await SelectLifestyle.AddLifestyleQualityFromNodeAsync(objCharacter, objLifestyle, objXmlQuality, blnFree, token).ConfigureAwait(false) == null)
                    throw new AgentToolException(objXmlQuality["name"]?.InnerText + " was not added (a choice it needed was cancelled).");
            }
        }

        /// <summary>
        /// Apply the dialog values passed (keeping the lifestyle's current ones otherwise) and finish the lifestyle as the dialog's OK does.
        /// </summary>
        private static async Task ApplyLifestyleArgsAsync(Character objCharacter, Lifestyle objLifestyle, XmlNode objXmlLifestyle, JObject objArgs,
                                                          CancellationToken token)
        {
            bool blnAdvanced = await objLifestyle.GetStyleTypeAsync(token).ConfigureAwait(false) != LifestyleType.Standard;
            async Task<int> GetPointsAsync(string strField, int intCurrent, Func<CancellationToken, Task<decimal>> funcMaximum)
            {
                int? intValue = OptionalInt(objArgs, strField);
                if (!intValue.HasValue)
                    return intCurrent;
                if (!blnAdvanced)
                    throw new AgentToolException("`" + strField + "` only applies to advanced lifestyles.");
                decimal decMaximum = await funcMaximum(token).ConfigureAwait(false);
                if (intValue.Value < 0 || intValue.Value > decMaximum)
                    throw new AgentToolException("`" + strField + "` must be 0-" + decMaximum.ToString("0.##", GlobalSettings.InvariantCultureInfo) + " for this lifestyle.");
                return intValue.Value;
            }

            int intArea = await GetPointsAsync("area", await objLifestyle.GetAreaAsync(token).ConfigureAwait(false), objLifestyle.GetAreaDeltaAsync).ConfigureAwait(false);
            int intComforts = await GetPointsAsync("comforts", await objLifestyle.GetComfortsAsync(token).ConfigureAwait(false), objLifestyle.GetComfortsDeltaAsync)
                .ConfigureAwait(false);
            int intSecurity = await GetPointsAsync("security", await objLifestyle.GetSecurityAsync(token).ConfigureAwait(false), objLifestyle.GetSecurityDeltaAsync)
                .ConfigureAwait(false);
            int intBonusLP = OptionalInt(objArgs, "bonus_lp") ?? await objLifestyle.GetBonusLPAsync(token).ConfigureAwait(false);
            if (!blnAdvanced && objArgs["bonus_lp"] != null)
                throw new AgentToolException("`bonus_lp` only applies to advanced lifestyles.");
            bool blnTrustFund = objArgs.Value<bool?>("trust_fund") ?? await objLifestyle.GetTrustFundAsync(token).ConfigureAwait(false);
            if (blnTrustFund && !await objLifestyle.GetIsTrustFundEligibleAsync(token).ConfigureAwait(false))
                throw new AgentToolException("The character has no Trust Fund that covers this lifestyle.");
            int intRoommates = OptionalInt(objArgs, "roommates") ?? await objLifestyle.GetRoommatesAsync(token).ConfigureAwait(false);
            if (intRoommates < 0)
                throw new AgentToolException("`roommates` can't be negative.");
            bool blnSplit = objArgs.Value<bool?>("split_cost") ?? await objLifestyle.GetSplitCostWithRoommatesAsync(token).ConfigureAwait(false);
            decimal decPercentage = objArgs.Value<decimal?>("percentage") ?? await objLifestyle.GetPercentageAsync(token).ConfigureAwait(false);
            string strRefusal = await SelectLifestyle.ApplyLifestyleAsync(objLifestyle, objXmlLifestyle, await objLifestyle.GetNameAsync(token).ConfigureAwait(false),
                                                                          decPercentage, intArea, intComforts, intSecurity, blnTrustFund, intRoommates,
                                                                          intRoommates > 0 && blnSplit, intBonusLP, token).ConfigureAwait(false);
            if (strRefusal != null)
                throw new AgentToolException(string.IsNullOrEmpty(strRefusal)
                                                 ? "That base lifestyle doesn't exist."
                                                 : await LanguageManager.GetStringAsync(strRefusal, GlobalSettings.DefaultLanguage, token: token).ConfigureAwait(false));
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
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            bool blnCareer = frmCharacter is CharacterCareer;
            Character objCharacter = frmCharacter.CharacterObject;
            decimal decMarkup = GetMarkup(objArgs, frmCharacter);
            string strWeapon = RequiredString(objArgs, "weapon");
            // Any weapon the character has, including vehicle weapons and underbarrel weapons
            OwnedItem objOwnedWeapon = await FindItemAsync(objCharacter, strWeapon, token).ConfigureAwait(false);
            if (!(objOwnedWeapon.Item is Weapon objWeapon))
                throw new AgentToolException("'" + strWeapon + "' isn't a weapon.");
            bool blnVehicleWeapon = false;
            for (OwnedItem objLoop = objOwnedWeapon.Parent; objLoop != null; objLoop = objLoop.Parent)
                blnVehicleWeapon |= objLoop.Item is Vehicle;
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

            WeaponAccessory objAccessory = await frmCharacter.AddWeaponAccessoryFromNodeAsync(
                                               objWeapon, xmlAccessory, new ValueTuple<string, string>(strMount, strExtraMount), intRating,
                                               objArgs.Value<bool?>("free") == true, objArgs.Value<bool?>("black_market") == true, decMarkup, blnCareer,
                                               blnCareer
                                                   ? await GetExpenseStringAsync(blnVehicleWeapon
                                                                                     ? "String_ExpensePurchaseVehicleWeaponAccessory"
                                                                                     : "String_ExpensePurchaseWeaponAccessory", token).ConfigureAwait(false)
                                                   : string.Empty,
                                               blnVehicleWeapon ? NuyenExpenseType.AddVehicleWeaponAccessory : NuyenExpenseType.AddWeaponAccessory,
                                               !blnCareer, false, token).ConfigureAwait(false)
                                           ?? throw new AgentToolException(blnCareer ? "The character can't afford " + strName + "." : "The accessory was not added.");
            return await FinishAnyEditAsync(frmCharacter, "Fitted " + await objAccessory.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " to " + objWeapon.Name + " (" + strMount + ", id " + objAccessory.InternalId + ").", token).ConfigureAwait(false);
        }

        private static async Task<object> AddArmorModAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            bool blnCareer = frmCharacter is CharacterCareer;
            Character objCharacter = frmCharacter.CharacterObject;
            decimal decMarkup = GetMarkup(objArgs, frmCharacter);
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
            ArmorMod objMod;
            string strRefusal = string.Empty;
            if (frmCharacter is CharacterCareer frmCareer)
                (objMod, strRefusal) = await frmCareer.AddArmorModFromNodeAsync(objArmor, xmlMod, intRating, objArgs.Value<bool?>("free") == true, decMarkup, token)
                                                      .ConfigureAwait(false);
            else
                objMod = await ((CharacterCreate)frmCharacter).AddArmorModFromNodeAsync(objArmor, xmlMod, intRating, objArgs.Value<bool?>("free") == true, token)
                                                              .ConfigureAwait(false);
            if (objMod == null)
                throw new AgentToolException(await GetRefusalMessageAsync(strRefusal, "The mod was not added (a choice it needed was cancelled).", token)
                                                 .ConfigureAwait(false));
            return await FinishAnyEditAsync(frmCharacter, "Fitted " + await objMod.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " to " + objArmor.Name + " (id " + objMod.InternalId + ").", token).ConfigureAwait(false);
        }

        private static async Task<object> AddVehicleModAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            bool blnCareer = frmCharacter is CharacterCareer;
            Character objCharacter = frmCharacter.CharacterObject;
            decimal decMarkup = GetMarkup(objArgs, frmCharacter);
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
            // Character creation doesn't enforce mod slots when adding (validation reports overfull vehicles);
            // career mode checks them as the mod is bought, as the career editor does
            VehicleMod objMod = await frmCharacter.AddVehicleModFromNodeAsync(objVehicle, xmlMod, intRating, objArgs.Value<bool?>("free") == true,
                                                                              objArgs.Value<bool?>("black_market") == true, decMarkup, blnCareer,
                                                                              blnCareer
                                                                                  ? await GetExpenseStringAsync("String_ExpensePurchaseVehicleMod", token).ConfigureAwait(false)
                                                                                  : string.Empty,
                                                                              NuyenExpenseType.AddVehicleMod, !blnCareer, blnCareer, false, token).ConfigureAwait(false)
                                ?? throw new AgentToolException(blnCareer
                                                                    ? "The mod was not installed (the vehicle has no room for it, or the character can't afford it)."
                                                                    : "The mod was not installed.");
            return await FinishAnyEditAsync(frmCharacter, "Installed " + await objMod.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " in " + objVehicle.Name + " (id " + objMod.InternalId + ").", token).ConfigureAwait(false);
        }

        private static async Task<object> AddWeaponMountAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            decimal decMarkup = GetMarkup(objArgs, frmCharacter);
            string strVehicle = RequiredString(objArgs, "vehicle");
            Vehicle objVehicle = await FindOwnedAsync(await (await objCharacter.GetVehiclesAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false),
                                                      strVehicle, token).ConfigureAwait(false)
                                 ?? throw new AgentToolException("The character has no vehicle '" + strVehicle + "'.");
            bool blnFree = objArgs.Value<bool?>("free") == true;
            bool blnBlackMarket = objArgs.Value<bool?>("black_market") == true;

            // The choices the Create Weapon Mount dialog offers for this vehicle, and then for the chosen size
            XmlDocument xmlDoc = await objCharacter.LoadDataAsync("vehicles.xml", token: token).ConfigureAwait(false);
            XmlNode xmlSize = PickWeaponMountChoice(xmlDoc, await CreateWeaponMount.GetAllowedSizesAsync(objCharacter, objVehicle, token).ConfigureAwait(false),
                                                    RequiredString(objArgs, "size"), "size", objVehicle.Name);
            (List<ListItem> lstVisibility, List<ListItem> lstFlexibility, List<ListItem> lstControl)
                = await CreateWeaponMount.GetAllowedOptionsAsync(objCharacter, objVehicle, xmlSize["id"]?.InnerText, token).ConfigureAwait(false);
            string strSizeName = xmlSize["name"]?.InnerText;
            XmlNode xmlVisibility = PickWeaponMountChoice(xmlDoc, lstVisibility, objArgs.Value<string>("visibility"), "visibility", strSizeName);
            XmlNode xmlFlexibility = PickWeaponMountChoice(xmlDoc, lstFlexibility, objArgs.Value<string>("flexibility"), "flexibility", strSizeName);
            XmlNode xmlControl = PickWeaponMountChoice(xmlDoc, lstControl, objArgs.Value<string>("control"), "control", strSizeName);
            if (blnBlackMarket)
            {
                // The dialog only enables the discount for mount categories the character's Black Market Pipeline covers
                List<string> lstBlackMarket = await objCharacter.GenerateBlackMarketMappingsAsync(
                    (await objCharacter.LoadDataXPathAsync("vehicles.xml", token: token).ConfigureAwait(false))
                    .SelectSingleNodeAndCacheExpression("/chummer/weaponmountcategories", token), token).ConfigureAwait(false);
                if (!lstBlackMarket.Contains(xmlSize["category"]?.InnerText ?? string.Empty))
                    throw new AgentToolException("The Black Market Pipeline discount doesn't apply to this weapon mount for this character.");
            }

            WeaponMount objMount = new WeaponMount(objCharacter, objVehicle);
            bool blnAdded = false;
            try
            {
                await objMount.CreateAsync(xmlSize, token).ConfigureAwait(false);
                objMount.DiscountCost = blnBlackMarket;
                await CreateWeaponMount.SetWeaponMountOptionsAsync(objCharacter, objMount, xmlControl, xmlFlexibility, xmlVisibility, token).ConfigureAwait(false);
                if (objArgs["mods"] is JArray objMods)
                {
                    foreach (JToken objModId in objMods)
                    {
                        XmlNode xmlMod = await GetWeaponMountModNodeAsync(objCharacter, objVehicle, objModId.ToString(), token).ConfigureAwait(false);
                        await objMount.Mods.AddAsync(await CreateWeaponMount.CreateMountModAsync(objCharacter, objVehicle, xmlMod, 0, false, blnFree, token)
                                                                            .ConfigureAwait(false), token).ConfigureAwait(false);
                    }
                }

                if (frmCharacter is CharacterCareer frmCareer)
                {
                    string strRefusal = await CreateWeaponMount.CheckCareerPurchaseAsync(objCharacter, objVehicle, objMount,
                                                                                         await objVehicle.GetTotalCostAsync(token).ConfigureAwait(false),
                                                                                         decMarkup, blnFree, token).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(strRefusal))
                        throw new AgentToolException(await GetRefusalMessageAsync(strRefusal, string.Empty, token).ConfigureAwait(false));
                    objMount.FreeCost = blnFree;
                    await frmCareer.AddWeaponMountAsync(objVehicle, objMount, decMarkup, token).ConfigureAwait(false);
                }
                else
                {
                    // Character creation doesn't check the vehicle's capacity here; validation reports overfull vehicles
                    objMount.FreeCost = blnFree;
                    await objVehicle.WeaponMounts.AddAsync(objMount, token).ConfigureAwait(false);
                }

                blnAdded = true;
            }
            finally
            {
                if (!blnAdded)
                    await objMount.DeleteWeaponMountAsync(token: CancellationToken.None).ConfigureAwait(false);
            }

            return await FinishAnyEditAsync(frmCharacter, "Built " + await objMount.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                    + " on " + objVehicle.Name + " (id " + objMount.InternalId + ").", token).ConfigureAwait(false);
        }

        /// <summary>
        /// A weapon mount add-on the Select Vehicle Mod dialog lists for this vehicle's mounts: in the enabled books, fitting the vehicle, within availability.
        /// </summary>
        private static async Task<XmlNode> GetWeaponMountModNodeAsync(Character objCharacter, Vehicle objVehicle, string strId, CancellationToken token)
        {
            XmlNode xmlMod = await GetDataNodeAsync(objCharacter, "vehicles.xml", "/chummer/weaponmountmods/mod", strId.Trim(), token).ConfigureAwait(false);
            if (await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false))
                return xmlMod;
            string strName = xmlMod["name"]?.InnerText;
            await EnsureBookEnabledAsync(xmlMod, objCharacter, token).ConfigureAwait(false);
            if (xmlMod["optionaldrone"] != null
                && !await (await objCharacter.GetSettingsAsync(token).ConfigureAwait(false)).GetDroneModsAsync(token).ConfigureAwait(false))
                throw new AgentToolException(strName + " needs the optional drone modification rules, which this character's settings don't use.");
            if (!await objVehicle.CheckModRequirementsAsync(xmlMod.CreateNavigator(), token).ConfigureAwait(false))
                throw new AgentToolException(strName + " doesn't fit " + objVehicle.Name + ".");
            if (!await SelectionShared.CheckAvailRestrictionAsync(xmlMod, objCharacter, 1, 0, token).ConfigureAwait(false))
                throw new AgentToolException(strName + " is above the availability limit at character creation.");
            return xmlMod;
        }

        private static async Task<object> EditWeaponMountAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            OwnedItem objOwned = await FindItemAsync(objCharacter, RequiredString(objArgs, "id"), token).ConfigureAwait(false);
            if (!(objOwned.Item is WeaponMount objMount))
                throw new AgentToolException(await GetItemNameAsync(objOwned.Item, token).ConfigureAwait(false) + " isn't a weapon mount.");
            Vehicle objVehicle = objMount.Parent;
            List<string> lstChanged = new List<string>();
            string strSize = objArgs.Value<string>("size")?.Trim();
            string[] astrOptions = { objArgs.Value<string>("visibility"), objArgs.Value<string>("flexibility"), objArgs.Value<string>("control") };
            if (!string.IsNullOrEmpty(strSize) || astrOptions.Any(x => !string.IsNullOrWhiteSpace(x)))
            {
                // As in the dialog, a mount that came with the vehicle keeps its size and options
                if (objMount.IncludedInVehicle)
                    throw new AgentToolException(objMount.Name + " comes with " + objVehicle.Name + "; only its add-ons can change.");
                XmlDocument xmlDoc = await objCharacter.LoadDataAsync("vehicles.xml", token: token).ConfigureAwait(false);
                XmlNode xmlSize = PickWeaponMountChoice(xmlDoc, await CreateWeaponMount.GetAllowedSizesAsync(objCharacter, objVehicle, token).ConfigureAwait(false),
                                                        string.IsNullOrEmpty(strSize) ? objMount.SourceIDString : strSize, "size", objVehicle.Name);
                (List<ListItem> lstVisibility, List<ListItem> lstFlexibility, List<ListItem> lstControl)
                    = await CreateWeaponMount.GetAllowedOptionsAsync(objCharacter, objVehicle, xmlSize["id"]?.InnerText, token).ConfigureAwait(false);
                string strSizeName = xmlSize["name"]?.InnerText;
                XmlNode[] axmlOptions = new XmlNode[3];
                string[] astrCategories = { "Visibility", "Flexibility", "Control" };
                List<ListItem>[] alstChoices = { lstVisibility, lstFlexibility, lstControl };
                for (int i = 0; i < 3; ++i)
                {
                    string strWanted = astrOptions[i];
                    if (string.IsNullOrWhiteSpace(strWanted))
                    {
                        // Keep the current choice when the size still allows it
                        string strCurrent = objMount.WeaponMountOptions.FirstOrDefault(x => x.Category == astrCategories[i])?.SourceIDString;
                        if (!string.IsNullOrEmpty(strCurrent) && alstChoices[i].Exists(x => string.Equals(x.Value.ToString(), strCurrent, StringComparison.OrdinalIgnoreCase)))
                            strWanted = strCurrent;
                    }
                    else
                        lstChanged.Add(astrCategories[i].ToLowerInvariant());

                    axmlOptions[i] = PickWeaponMountChoice(xmlDoc, alstChoices[i], strWanted, astrCategories[i].ToLowerInvariant(), strSizeName);
                }

                if (!string.Equals(xmlSize["id"]?.InnerText, objMount.SourceIDString, StringComparison.OrdinalIgnoreCase))
                {
                    await objMount.CreateAsync(xmlSize, token).ConfigureAwait(false);
                    lstChanged.Add("size");
                }

                await CreateWeaponMount.SetWeaponMountOptionsAsync(objCharacter, objMount, axmlOptions[2], axmlOptions[1], axmlOptions[0], token).ConfigureAwait(false);
            }

            if (objArgs["remove_mods"] is JArray objRemove)
            {
                foreach (JToken objModId in objRemove)
                {
                    string strModId = objModId.ToString().Trim();
                    VehicleMod objMod = await FindOwnedAsync(await objMount.Mods.ToListAsync(token).ConfigureAwait(false), strModId, token).ConfigureAwait(false)
                                        ?? throw new AgentToolException(objMount.Name + " has no add-on '" + strModId + "'.");
                    if (objMod.IncludedInVehicle)
                        throw new AgentToolException(objMod.Name + " comes with the mount and can't be taken off.");
                    await objMod.RemoveAsync(false, token).ConfigureAwait(false);
                    await objMount.Mods.RemoveAsync(objMod, token).ConfigureAwait(false);
                    lstChanged.Add("removed " + objMod.Name);
                }
            }

            if (objArgs["add_mods"] is JArray objAdd)
            {
                foreach (JToken objModId in objAdd)
                {
                    XmlNode xmlMod = await GetWeaponMountModNodeAsync(objCharacter, objVehicle, objModId.ToString(), token).ConfigureAwait(false);
                    VehicleMod objMod = await CreateWeaponMount.CreateMountModAsync(objCharacter, objVehicle, xmlMod, 0, false, false, token).ConfigureAwait(false);
                    await objMount.Mods.AddAsync(objMod, token).ConfigureAwait(false);
                    lstChanged.Add("added " + objMod.Name + " (id " + objMod.InternalId + ")");
                }
            }

            if (lstChanged.Count == 0)
                throw new AgentToolException("Nothing to change; pass size, an option, add_mods or remove_mods.");
            return await FinishEditAsync(frmCreate, await objMount.GetCurrentDisplayNameAsync(token).ConfigureAwait(false) + ": " + string.Join(", ", lstChanged) + ".",
                                         token).ConfigureAwait(false);
        }

        /// <summary>
        /// One of the weapon mount choices the Create Weapon Mount dialog lists, by id or English name; the first, as the dialog preselects, when none is asked for.
        /// </summary>
        private static XmlNode PickWeaponMountChoice(XmlDocument xmlDoc, IEnumerable<ListItem> lstChoices, string strWanted, string strWhat, string strFor)
        {
            List<XmlNode> lstNodes = lstChoices.Select(x => xmlDoc.TryGetNodeByNameOrId("/chummer/weaponmounts/weaponmount", x.Value.ToString()))
                                               .Where(x => x != null).ToList();
            if (lstNodes.Count == 0)
                throw new AgentToolException("No weapon mount " + strWhat + " is available for " + strFor + ".");
            if (string.IsNullOrWhiteSpace(strWanted))
                return lstNodes[0];
            strWanted = strWanted.Trim();
            return lstNodes.Find(x => string.Equals(x["id"]?.InnerText, strWanted, StringComparison.OrdinalIgnoreCase))
                   ?? lstNodes.Find(x => string.Equals(x["name"]?.InnerText, strWanted, StringComparison.OrdinalIgnoreCase))
                   ?? throw new AgentToolException("'" + strWanted + "' is not a weapon mount " + strWhat + " available for " + strFor + ". Options: "
                                                   + string.Join(", ", lstNodes.Select(x => x["name"]?.InnerText)) + ".");
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

        private static async Task<object> AddDrugAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            bool blnIgnoreRules = await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false);
            bool blnFree = objArgs.Value<bool?>("free") == true;
            decimal decMarkup = GetMarkup(objArgs, frmCharacter);
            int intQuantity = OptionalInt(objArgs, "quantity") ?? 1;
            if (intQuantity < 1)
                throw new AgentToolException("`quantity` must be at least 1.");

            // Ware that holds drugs only takes the drug categories it names
            Cyberware objParent = null;
            string strParent = objArgs.Value<string>("parent")?.Trim();
            if (!string.IsNullOrEmpty(strParent))
            {
                OwnedItem objParentItem = await FindItemAsync(objCharacter, strParent, token).ConfigureAwait(false);
                objParent = objParentItem.Item as Cyberware;
                if (objParent?.AllowDrug == null)
                    throw new AgentToolException(await GetItemNameAsync(objParentItem.Item, token).ConfigureAwait(false) + " can't hold drugs.");
                if (frmCharacter is CharacterCareer)
                    throw new AgentToolException("In career mode, drugs go into ware through the editor (its cyberware's Add Drug menu item).");
            }

            // The grades the Select Drug dialog offers (they come from Chemical Fortitude; without it drugs have no grade)
            string strGrade = objArgs.Value<string>("grade")?.Trim();
            List<Grade> lstGrades = await objCharacter.GetGradesListAsync(Improvement.ImprovementSource.Drug, blnIgnoreRules, token).ConfigureAwait(false);
            Grade objGrade = null;
            if (!string.IsNullOrEmpty(strGrade) || lstGrades.Count > 0)
            {
                if (string.IsNullOrEmpty(strGrade))
                    strGrade = lstGrades.Exists(x => x.Name == "Standard") ? "Standard" : lstGrades[0].Name;
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
                    throw new AgentToolException("'" + strGrade + "' is not an available drug grade."
                                                 + (lstGrades.Count > 0 ? " Options: " + string.Join(", ", lstGrades.Select(x => x.Name)) + "." : string.Empty));
            }

            XmlNode objXmlDrug = await GetBuyableNodeAsync(objCharacter, "drugs.xml", "/chummer/drugs/drug", RequiredString(objArgs, "id"), 1, objGrade?.Avail ?? 0, token)
                .ConfigureAwait(false);
            string strName = objXmlDrug["name"]?.InnerText;
            if (objParent != null && !blnIgnoreRules)
            {
                string strCategories = CharacterShared.GetAllowedDrugCategories(objParent, token);
                if (!string.IsNullOrEmpty(strCategories)
                    && !strCategories.Split(',').Contains(objXmlDrug["category"]?.InnerText ?? string.Empty))
                    throw new AgentToolException(objParent.Name + " only holds " + strCategories.Replace(",", ", ") + ".");
            }

            Drug objDrug = await Drug.CreateFromCatalogAsync(objCharacter, objXmlDrug["id"]?.InnerText ?? strName, objGrade, token: token).ConfigureAwait(false)
                           ?? throw new AgentToolException(strName + " could not be created.");
            objDrug.DiscountCost = objArgs.Value<bool?>("black_market") == true;
            objDrug.Quantity = intQuantity;
            if (objParent != null)
            {
                try
                {
                    await CharacterShared.AttachDrugToCyberwareAsync(objParent, objDrug, token).ConfigureAwait(false);
                }
                catch
                {
                    await objDrug.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
            else
            {
                // Check the price here so the career editor's "not enough nuyen" message box never opens
                if (frmCharacter is CharacterCareer && !blnFree
                    && await frmCharacter.GetCareerDrugPurchaseCostAsync(objDrug, decMarkup, token).ConfigureAwait(false)
                    > await objCharacter.GetNuyenAsync(token).ConfigureAwait(false))
                {
                    await objDrug.DisposeAsync().ConfigureAwait(false);
                    throw new AgentToolException("The character can't afford " + strName + ".");
                }

                if (!await frmCharacter.AddDrugToCharacterAsync(objDrug, decMarkup, blnFree, token).ConfigureAwait(false))
                    throw new AgentToolException("The character can't afford " + strName + ".");
            }

            return await FinishAnyEditAsync(frmCharacter, "Added " + intQuantity.ToString(GlobalSettings.InvariantCultureInfo) + " dose(s) of "
                                                          + await objDrug.GetCurrentDisplayNameAsync(token).ConfigureAwait(false)
                                                          + (objParent != null ? " to " + objParent.Name : string.Empty) + " (id " + objDrug.InternalId + ").",
                                            token).ConfigureAwait(false);
        }

        private static Improvement.ImprovementSource GetWareSource(JObject objArgs)
        {
            switch (objArgs.Value<string>("kind")?.Trim().ToLowerInvariant())
            {
                case null:
                case "":
                case "cyberware":
                    return Improvement.ImprovementSource.Cyberware;
                case "bioware":
                    return Improvement.ImprovementSource.Bioware;
                default:
                    throw new AgentToolException("`kind` must be 'cyberware' or 'bioware'.");
            }
        }

        private static async Task<object> AddCyberwareSuiteAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            Improvement.ImprovementSource eSource = GetWareSource(objArgs);
            string strType = eSource == Improvement.ImprovementSource.Cyberware ? "cyberware" : "bioware";
            // Reload the file, as the Add Suite menu item does, so suites saved since Chummer started are found
            XmlDocument objXmlDocument = await objCharacter.LoadDataAsync(strType + ".xml", string.Empty, true, token).ConfigureAwait(false);
            string strId = RequiredString(objArgs, "id");
            XmlNode xmlSuite = objXmlDocument.TryGetNodeByNameOrId("/chummer/suites/suite", strId)
                               ?? throw new AgentToolException("There is no " + strType + " suite '" + strId + "'. Use search_catalog(kind='" + strType + "suite').");
            if (!await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false)
                && !await SelectCyberwareSuite.IsSuiteAllowedAsync(objCharacter, xmlSuite, eSource, token).ConfigureAwait(false))
                throw new AgentToolException("The character can't take " + xmlSuite["name"]?.InnerText + " (its grade, " + xmlSuite["grade"]?.InnerText
                                             + ", isn't available to them).");
            List<Cyberware> lstAdded = await frmCreate.AddCyberwareSuiteFromNodeAsync(xmlSuite, eSource, token).ConfigureAwait(false);
            List<string> lstNames = new List<string>(lstAdded.Count);
            foreach (Cyberware objCyberware in lstAdded)
                lstNames.Add(await objCyberware.GetCurrentDisplayNameAsync(token).ConfigureAwait(false) + " (id " + objCyberware.InternalId + ")");
            return await FinishEditAsync(frmCreate, "Installed the " + xmlSuite["name"]?.InnerText + " suite: " + string.Join(", ", lstNames) + ".", token)
                .ConfigureAwait(false);
        }

        private static async Task<object> CreateCyberwareSuiteAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            Improvement.ImprovementSource eSource = GetWareSource(objArgs);
            string strName = RequiredString(objArgs, "name");
            string strFileName = objArgs.Value<string>("file_name")?.Trim();
            if (string.IsNullOrEmpty(strFileName))
                strFileName = "custom_" + (eSource == Improvement.ImprovementSource.Cyberware ? "cyberware" : "bioware") + ".xml";
            (string strError, string _) = await CreateCyberwareSuite.CheckCanCreateSuiteAsync(objCharacter, eSource, token).ConfigureAwait(false);
            if (string.IsNullOrEmpty(strError))
                (strError, _) = await CreateCyberwareSuite.WriteSuiteAsync(objCharacter, eSource, strName, strFileName, token).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(strError))
                throw new AgentToolException(strError);
            return new { result = "Saved the suite '" + strName + "' to " + System.IO.Path.Combine(Utils.GetDataFolderPath, strFileName.CleanForFileName()) + "." };
        }

        private static async Task<object> AddPACKSKitAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            string strId = RequiredString(objArgs, "id");
            string strCategory = objArgs.Value<string>("category")?.Trim();
            XmlDocument objXmlDocument = await objCharacter.LoadDataAsync("packs.xml", token: token).ConfigureAwait(false);
            // The Select PACKS Kit dialog lists every kit that isn't hidden; kit names only repeat across categories
            string strFilter = "not(hide)" + (string.IsNullOrEmpty(strCategory) ? string.Empty : " and category = " + strCategory.CleanXPath());
            List<XmlNode> lstKits = new List<XmlNode>();
            using (XmlNodeList xmlKits = objXmlDocument.SelectNodes("/chummer/packs/pack[" + strFilter + " and (id = " + strId.CleanXPath() + " or name = "
                                                                   + strId.CleanXPath() + ")]"))
            {
                if (xmlKits != null)
                    lstKits.AddRange(xmlKits.Cast<XmlNode>());
            }

            if (lstKits.Count == 0)
                throw new AgentToolException("There is no PACKS kit '" + strId + "'" + (string.IsNullOrEmpty(strCategory) ? string.Empty : " in " + strCategory)
                                             + ". Use search_catalog(kind='packs').");
            if (lstKits.Count > 1)
                throw new AgentToolException("Kits in several categories are named '" + strId + "'; pass `category`. Options: "
                                             + string.Join(", ", lstKits.Select(x => x["category"]?.InnerText)) + ".");
            XmlNode xmlKit = lstKits[0];

            // A kit that lets the character pick a martial art needs the pick before anything is added
            string strMartialArt = objArgs.Value<string>("martial_art")?.Trim();
            XmlElement xmlSelectMartialArt = xmlKit["selectmartialart"];
            if (xmlSelectMartialArt != null && string.IsNullOrEmpty(strMartialArt))
            {
                strMartialArt = xmlSelectMartialArt.Attributes["select"]?.InnerText;
                if (string.IsNullOrEmpty(strMartialArt))
                    throw new AgentToolException(xmlKit["name"]?.InnerText + " lets the character pick a martial art; pass `martial_art` (search_catalog kind='martialart').");
            }

            if (!string.IsNullOrEmpty(strMartialArt))
            {
                XmlNode xmlMartialArt = await GetDataNodeAsync(objCharacter, "martialarts.xml", "/chummer/martialarts/martialart", strMartialArt, token).ConfigureAwait(false);
                await EnsureBookEnabledAsync(xmlMartialArt, objCharacter, token).ConfigureAwait(false);
                await EnsureRequirementsMetAsync(xmlMartialArt, objCharacter, token).ConfigureAwait(false);
                strMartialArt = xmlMartialArt["id"]?.InnerText ?? strMartialArt;
            }

            await frmCreate.AddPACKSKitFromNodeAsync(xmlKit, (x, t) => Task.FromResult(strMartialArt ?? string.Empty), token).ConfigureAwait(false);
            return await FinishEditAsync(frmCreate, "Added the " + xmlKit["name"]?.InnerText + " kit. Use list_items to see what it added.", token).ConfigureAwait(false);
        }

        private static async Task<object> CreatePACKSKitAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            string strName = RequiredString(objArgs, "name");
            string strFileName = objArgs.Value<string>("file_name")?.Trim();
            if (string.IsNullOrEmpty(strFileName))
                strFileName = "custom_packs.xml";
            HashSet<string> setParts = new HashSet<string>(CreatePACKSKit.KitParts);
            if (objArgs["parts"] is JArray objParts)
            {
                setParts.Clear();
                foreach (JToken objPart in objParts)
                {
                    string strPart = objPart.ToString().Trim().ToLowerInvariant();
                    if (!CreatePACKSKit.KitParts.Contains(strPart))
                        throw new AgentToolException("Unknown part '" + strPart + "'. Options: " + string.Join(", ", CreatePACKSKit.KitParts) + ".");
                    setParts.Add(strPart);
                }
            }

            (string strError, string _) = await CreatePACKSKit.WriteKitAsync(frmCharacter.CharacterObject, strName, strFileName, setParts, token).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(strError))
                throw new AgentToolException(strError);
            return new { result = "Saved the PACKS kit '" + strName + "' (category Custom) with: " + string.Join(", ", CreatePACKSKit.KitParts.Where(setParts.Contains)) + "." };
        }

        private static async Task<object> AddNaturalWeaponAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            string strName = RequiredString(objArgs, "name");
            // The skills the dialog lists: the combat active skills
            string strSkill = objArgs.Value<string>("skill")?.Trim();
            if (string.IsNullOrEmpty(strSkill))
                strSkill = "Unarmed Combat";
            List<string> lstSkills = (await objCharacter.LoadDataXPathAsync("skills.xml", token: token).ConfigureAwait(false))
                                     .Select("/chummer/skills/skill[category = \"Combat Active\"]/name").Cast<XPathNavigator>().Select(x => x.Value).ToList();
            string strFoundSkill = lstSkills.Find(x => string.Equals(x, strSkill, StringComparison.OrdinalIgnoreCase))
                                   ?? throw new AgentToolException("'" + strSkill + "' isn't a combat skill. Options: " + string.Join(", ", lstSkills) + ".");
            string strDamage = objArgs.Value<string>("damage")?.Trim().ToUpperInvariant().Replace(" ", string.Empty);
            string strDVBase;
            switch (strDamage)
            {
                case null:
                case "":
                case "STR/2":
                case "(STR/2)":
                    strDVBase = "({STR}/2)";
                    break;
                case "STR":
                case "(STR)":
                    strDVBase = "({STR})";
                    break;
                default:
                    if (!int.TryParse(strDamage, out int intBase) || intBase < 1 || intBase > 20)
                        throw new AgentToolException("`damage` must be 'STR/2', 'STR' or a number from 1 to 20.");
                    strDVBase = intBase.ToString(GlobalSettings.InvariantCultureInfo);
                    break;
            }

            string strDVType = objArgs.Value<string>("damage_type")?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(strDVType))
                strDVType = "P";
            else if (strDVType != "P" && strDVType != "S")
                throw new AgentToolException("`damage_type` must be 'P' or 'S'.");
            Weapon objWeapon = await CreateNaturalWeapon.CreateWeaponAsync(objCharacter, strName, strFoundSkill, strDVBase, OptionalInt(objArgs, "damage_bonus") ?? 0,
                                                                           strDVType, OptionalInt(objArgs, "ap") ?? 0, OptionalInt(objArgs, "reach") ?? 0, token)
                                                         .ConfigureAwait(false)
                               ?? throw new AgentToolException("The data has no Natural Weapon power to base the weapon on.");
            await objCharacter.Weapons.AddAsync(objWeapon, token).ConfigureAwait(false);
            return await FinishAnyEditAsync(frmCharacter, "Made the natural weapon " + objWeapon.Name + ", " + objWeapon.Damage + " (id " + objWeapon.InternalId + ").", token)
                .ConfigureAwait(false);
        }
    }
}
