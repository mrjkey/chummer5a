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
using Chummer.Backend.Equipment;
using Chummer.Backend.Uniques;
using Newtonsoft.Json.Linq;

namespace Chummer.Agent
{
    public static partial class AgentTools
    {
        private const string ItemIdDescription = "Id of something the character has, from list_items or get_character_summary.";

        private static IEnumerable<AgentTool> CreateItemTools()
        {
            yield return new AgentTool("list_items",
                "Everything the character has, as a tree with ids: qualities, magic, contacts, lifestyles, locations and all equipment including what is "
                + "plugged into or fitted to other equipment (gear in gear, accessories, mods, vehicle weapons, cyberware plugins). Shows each item's "
                + "rating, quantity, equipped/wireless state, location, custom name and notes. Use the ids with set_item_details, set_item_state, "
                + "set_item_values and remove_item.",
                AgentTool.Schema(("kind", "string", "Only this kind, e.g. 'gear', 'weapon', 'armor', 'cyberware', 'vehicle', 'lifestyle', 'quality', 'location'."),
                                 ("character", "string", CharacterArgDescription)),
                ListItemsAsync);
            yield return new AgentTool("set_item_details",
                "Describe an item the character has, as its Notes and Name menu items do: notes, a custom name (weapons, armor, gear, vehicles, "
                + "lifestyles, spirits), the free-text name of custom gear (`extra`), and which location it is filed under. Only the fields passed change. "
                + "Works in creation and career mode.",
                AgentTool.Schema(("id!", "string", ItemIdDescription),
                                 ("notes", "string", "Notes. Empty clears them."),
                                 ("custom_name", "string", "Custom name shown instead of the item's own name. Empty clears it."),
                                 ("extra", "string", "Free text that names a custom item (gear marked as renamable, during creation)."),
                                 ("location", "string", "Name or id of a location from manage_location to file the item under; empty takes it out of its location."),
                                 ("character", "string", CharacterArgDescription)),
                SetItemDetailsAsync);
            yield return new AgentTool("set_item_state",
                "Toggle an item's checkboxes, as the editor's item panels do: equipped, wireless, active commlink, home node, stolen, and the black market "
                + "discount (creation only, needs the Black Market Pipeline quality). Only the fields passed change.",
                AgentTool.Schema(("id!", "string", ItemIdDescription),
                                 ("equipped", "boolean", "Equipped (gear, armor, armor mods, weapons, weapon accessories)."),
                                 ("wireless", "boolean", "Wireless on, which turns its wireless bonus on or off."),
                                 ("active_commlink", "boolean", "Make this the character's active commlink (commlinks, cyberdecks and other matrix devices)."),
                                 ("home_node", "boolean", "Make this the character's home node (AIs only)."),
                                 ("stolen", "boolean", "Mark it stolen (only with an improvement that grants stolen gear)."),
                                 ("black_market_discount", "boolean", "Apply the Black Market Pipeline discount."),
                                 ("prototype_transhuman", "boolean", "Bioware paid for by the Prototype Transhuman quality's free essence (creation only)."),
                                 ("included_in_weapon", "boolean", "A weapon accessory counts as part of the base weapon (where the settings allow editing that)."),
                                 ("fire_mode", "string", "A vehicle weapon's firing mode: Skill, GunneryCommandDevice, RemoteOperated, DogBrain or ManualOperation."),
                                 ("character", "string", CharacterArgDescription)),
                SetItemStateAsync);
            yield return new AgentTool("move_modular_cyberware",
                "Move modular cyberware (e.g. a cyberlimb or a modular plugin) to another mount, or unmount it, as its Change Mount button does. "
                + "Without `mount` the result lists the mounts it can go to.",
                AgentTool.Schema(("id!", "string", "Modular cyberware id from list_items."),
                                 ("mount", "string", "Id of the mount (cyberware or vehicle mod) to move it to, or 'None' to unmount it."),
                                 ("character", "string", CharacterArgDescription)),
                MoveModularCyberwareAsync);
            yield return new AgentTool("set_item_values",
                "Change an item the character bought during creation, as its spin boxes do: rating (gear, armor mods, cyberware, weapons, accessories, "
                + "vehicle mods), quantity (gear, drugs), cyberware grade, or months paid for a lifestyle. Costs and essence update; the result shows the budget.",
                AgentTool.Schema(("id!", "string", ItemIdDescription),
                                 ("rating", "integer", "New rating, within the item's limits."),
                                 ("quantity", "number", "New quantity."),
                                 ("grade", "string", "New cyberware/bioware grade, e.g. 'Alphaware'."),
                                 ("months", "integer", "Months paid for a lifestyle."),
                                 ("character", "string", CharacterArgDescription)),
                SetItemValuesAsync);
            yield return new AgentTool("swap_matrix_attributes",
                "Swap two matrix attributes of a device whose attributes can be rearranged (cyberdecks, some commlinks and RCCs), as picking a value in "
                + "its attribute boxes does.",
                AgentTool.Schema(("id!", "string", "Device id from list_items."),
                                 ("first!", "string", "'Attack', 'Sleaze', 'Data Processing' or 'Firewall'."),
                                 ("second!", "string", "'Attack', 'Sleaze', 'Data Processing' or 'Firewall'."),
                                 ("character", "string", CharacterArgDescription)),
                SwapMatrixAttributesAsync);
            yield return new AgentTool("copy_item",
                "Copy armor, an armor mod, cyberware, gear, a lifestyle, a vehicle, a weapon or a weapon accessory with everything in it, as Edit > Copy "
                + "does. paste_item then adds the copy, to this character or another one in creation.",
                AgentTool.Schema(("id!", "string", ItemIdDescription),
                                 ("character", "string", CharacterArgDescription)),
                CopyItemAsync);
            yield return new AgentTool("paste_item",
                "Add a copy of what copy_item copied, as Edit > Paste does. Armor, cyberware, gear, lifestyles, vehicles and weapons go on the character; "
                + "with `into`, armor mods go into armor, plugins into cyberware, gear into what takes gear, accessories into weapons, and weapons into "
                + "weapon mounts or other weapons. A copy costs what the original did.",
                AgentTool.Schema(("into", "string", "Id of the item to paste into, from list_items (optional)."),
                                 ("character", "string", CharacterArgDescription)),
                PasteItemAsync);
            yield return new AgentTool("manage_location",
                "Add, rename or remove a location (a folder for organizing gear, armor, weapons or vehicles), as the editor's Add Location and "
                + "rename/delete menu items do. Removing one leaves its items unfiled. File items under it with set_item_details(location).",
                AgentTool.Schema(("action!", "string", "'add', 'rename' or 'remove'."),
                                 ("tab", "string", "For 'add': 'gear' (default), 'armor', 'weapons' or 'vehicles'."),
                                 ("name!", "string", "Location name (or id, for rename/remove)."),
                                 ("new_name", "string", "For 'rename': the new name."),
                                 ("character", "string", CharacterArgDescription)),
                ManageLocationAsync);
        }

        #region Owned items

        /// <summary>
        /// Something the character has, with what it is fitted to or plugged into.
        /// </summary>
        private sealed class OwnedItem
        {
            public OwnedItem(object objItem, string strKind, OwnedItem objParent)
            {
                Item = objItem;
                Kind = strKind;
                Parent = objParent;
            }

            public object Item { get; }
            public string Kind { get; }
            public OwnedItem Parent { get; }
            public List<OwnedItem> Children { get; } = new List<OwnedItem>();
        }

        /// <summary>
        /// Every item the character has, as the editor's trees show them.
        /// </summary>
        private static async Task<List<OwnedItem>> GetOwnedItemsAsync(Character objCharacter, CancellationToken token)
        {
            List<OwnedItem> lstRoots = new List<OwnedItem>();

            async Task AddAllAsync<T>(IEnumerable<T> lstItems, string strKind, OwnedItem objParent, Func<T, OwnedItem, Task> funcChildren = null)
            {
                foreach (T objItem in lstItems)
                {
                    OwnedItem objOwned = new OwnedItem(objItem, strKind, objParent);
                    if (objParent == null)
                        lstRoots.Add(objOwned);
                    else
                        objParent.Children.Add(objOwned);
                    if (funcChildren != null)
                        await funcChildren(objItem, objOwned).ConfigureAwait(false);
                }
            }

            async Task GearChildrenAsync(Gear objGear, OwnedItem objOwned)
            {
                await AddAllAsync(await objGear.Children.ToListAsync(token).ConfigureAwait(false), "gear", objOwned, GearChildrenAsync).ConfigureAwait(false);
            }

            async Task WeaponChildrenAsync(Weapon objWeapon, OwnedItem objOwned)
            {
                await AddAllAsync(await objWeapon.WeaponAccessories.ToListAsync(token).ConfigureAwait(false), "weapon accessory", objOwned,
                                  async (x, y) => await AddAllAsync(await x.GearChildren.ToListAsync(token).ConfigureAwait(false), "gear", y, GearChildrenAsync)
                                                      .ConfigureAwait(false)).ConfigureAwait(false);
                await AddAllAsync(await objWeapon.UnderbarrelWeapons.ToListAsync(token).ConfigureAwait(false), "weapon", objOwned, WeaponChildrenAsync)
                    .ConfigureAwait(false);
            }

            async Task CyberwareChildrenAsync(Cyberware objCyberware, OwnedItem objOwned)
            {
                await AddAllAsync(await objCyberware.Children.ToListAsync(token).ConfigureAwait(false), "cyberware", objOwned, CyberwareChildrenAsync)
                    .ConfigureAwait(false);
                await AddAllAsync(await objCyberware.GearChildren.ToListAsync(token).ConfigureAwait(false), "gear", objOwned, GearChildrenAsync).ConfigureAwait(false);
                await AddAllAsync(await objCyberware.DrugChildren.ToListAsync(token).ConfigureAwait(false), "drug", objOwned).ConfigureAwait(false);
            }

            await AddAllAsync(await objCharacter.Qualities.ToListAsync(token).ConfigureAwait(false), "quality", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.Spells.ToListAsync(token).ConfigureAwait(false), "spell", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.Powers.ToListAsync(token).ConfigureAwait(false), "power", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.ComplexForms.ToListAsync(token).ConfigureAwait(false), "complex form", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.AIPrograms.ToListAsync(token).ConfigureAwait(false), "ai program", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.MartialArts.ToListAsync(token).ConfigureAwait(false), "martial art", null,
                              async (x, y) => await AddAllAsync(await x.Techniques.ToListAsync(token).ConfigureAwait(false), "technique", y).ConfigureAwait(false))
                .ConfigureAwait(false);
            await AddAllAsync(await objCharacter.MentorSpirits.ToListAsync(token).ConfigureAwait(false), "mentor spirit", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.InitiationGrades.ToListAsync(token).ConfigureAwait(false), "initiation grade", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.Metamagics.ToListAsync(token).ConfigureAwait(false), "metamagic", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.Arts.ToListAsync(token).ConfigureAwait(false), "art", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.Enhancements.ToListAsync(token).ConfigureAwait(false), "enhancement", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.CritterPowers.ToListAsync(token).ConfigureAwait(false), "critter power", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.Spirits.ToListAsync(token).ConfigureAwait(false), "spirit", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.Contacts.ToListAsync(token).ConfigureAwait(false), "contact", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.Lifestyles.ToListAsync(token).ConfigureAwait(false), "lifestyle", null,
                              async (x, y) => await AddAllAsync(await x.LifestyleQualities.ToListAsync(token).ConfigureAwait(false), "lifestyle quality", y)
                                                  .ConfigureAwait(false)).ConfigureAwait(false);
            foreach ((ThreadSafeObservableCollection<Location> lstLocations, string strTab) in new[]
                     {
                         (objCharacter.GearLocations, "gear"), (objCharacter.ArmorLocations, "armor"),
                         (objCharacter.WeaponLocations, "weapons"), (objCharacter.VehicleLocations, "vehicles")
                     })
                await AddAllAsync(await lstLocations.ToListAsync(token).ConfigureAwait(false), "location (" + strTab + ")", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.Weapons.ToListAsync(token).ConfigureAwait(false), "weapon", null, WeaponChildrenAsync).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.Armor.ToListAsync(token).ConfigureAwait(false), "armor", null,
                              async (x, y) =>
                              {
                                  await AddAllAsync(await x.ArmorMods.ToListAsync(token).ConfigureAwait(false), "armor mod", y,
                                                    async (z, w) => await AddAllAsync(await z.GearChildren.ToListAsync(token).ConfigureAwait(false), "gear", w,
                                                                                      GearChildrenAsync).ConfigureAwait(false)).ConfigureAwait(false);
                                  await AddAllAsync(await x.GearChildren.ToListAsync(token).ConfigureAwait(false), "gear", y, GearChildrenAsync).ConfigureAwait(false);
                              }).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.Gear.ToListAsync(token).ConfigureAwait(false), "gear", null, GearChildrenAsync).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.Cyberware.ToListAsync(token).ConfigureAwait(false), "cyberware", null, CyberwareChildrenAsync).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.Drugs.ToListAsync(token).ConfigureAwait(false), "drug", null).ConfigureAwait(false);
            await AddAllAsync(await objCharacter.Vehicles.ToListAsync(token).ConfigureAwait(false), "vehicle", null,
                              async (x, y) =>
                              {
                                  await AddAllAsync(await x.Mods.ToListAsync(token).ConfigureAwait(false), "vehicle mod", y,
                                                    async (z, w) =>
                                                    {
                                                        await AddAllAsync(await z.Weapons.ToListAsync(token).ConfigureAwait(false), "weapon", w, WeaponChildrenAsync)
                                                            .ConfigureAwait(false);
                                                        await AddAllAsync(await z.Cyberware.ToListAsync(token).ConfigureAwait(false), "cyberware", w, CyberwareChildrenAsync)
                                                            .ConfigureAwait(false);
                                                    }).ConfigureAwait(false);
                                  await AddAllAsync(await x.WeaponMounts.ToListAsync(token).ConfigureAwait(false), "weapon mount", y,
                                                    async (z, w) =>
                                                    {
                                                        await AddAllAsync(await z.Weapons.ToListAsync(token).ConfigureAwait(false), "weapon", w, WeaponChildrenAsync)
                                                            .ConfigureAwait(false);
                                                        await AddAllAsync(await z.Mods.ToListAsync(token).ConfigureAwait(false), "vehicle mod", w).ConfigureAwait(false);
                                                    }).ConfigureAwait(false);
                                  await AddAllAsync(await x.Weapons.ToListAsync(token).ConfigureAwait(false), "weapon", y, WeaponChildrenAsync).ConfigureAwait(false);
                                  await AddAllAsync(await x.GearChildren.ToListAsync(token).ConfigureAwait(false), "gear", y, GearChildrenAsync).ConfigureAwait(false);
                              }).ConfigureAwait(false);
            return lstRoots;
        }

        private static IEnumerable<OwnedItem> Flatten(IEnumerable<OwnedItem> lstItems)
        {
            foreach (OwnedItem objItem in lstItems)
            {
                yield return objItem;
                foreach (OwnedItem objChild in Flatten(objItem.Children))
                    yield return objChild;
            }
        }

        private static async Task<string> GetItemNameAsync(object objItem, CancellationToken token)
        {
            switch (objItem)
            {
                case IHasName objNamed:
                    return await objNamed.GetCurrentDisplayNameAsync(token).ConfigureAwait(false);
                case IHasCustomName objCustomNamed:
                    return await objCustomNamed.GetCurrentDisplayNameAsync(token).ConfigureAwait(false);
                case InitiationGrade objGrade:
                    return (objGrade.Technomancer ? "Submersion grade " : "Initiation grade ") + objGrade.Grade.ToString(GlobalSettings.InvariantCultureInfo);
                default:
                    return objItem?.ToString();
            }
        }

        /// <summary>
        /// Find an item the character has by id, falling back to a unique name.
        /// </summary>
        private static async Task<OwnedItem> FindItemAsync(Character objCharacter, string strId, CancellationToken token)
        {
            List<OwnedItem> lstAll = Flatten(await GetOwnedItemsAsync(objCharacter, token).ConfigureAwait(false)).ToList();
            OwnedItem objMatch = lstAll.Find(x => x.Item is IHasInternalId objId && string.Equals(objId.InternalId, strId, StringComparison.OrdinalIgnoreCase));
            if (objMatch != null)
                return objMatch;
            List<OwnedItem> lstNamed = new List<OwnedItem>();
            foreach (OwnedItem objLoop in lstAll)
            {
                if (string.Equals(await GetItemNameAsync(objLoop.Item, token).ConfigureAwait(false), strId, StringComparison.OrdinalIgnoreCase)
                    || objLoop.Item is IHasName objNamed && string.Equals(objNamed.Name, strId, StringComparison.OrdinalIgnoreCase))
                    lstNamed.Add(objLoop);
            }

            if (lstNamed.Count == 1)
                return lstNamed[0];
            throw new AgentToolException(lstNamed.Count == 0
                                             ? "The character has nothing matching '" + strId + "'. Use list_items for ids."
                                             : "Several items are called '" + strId + "'; pass the id from list_items.");
        }

        private static async Task<Dictionary<string, object>> DescribeItemAsync(OwnedItem objOwned, Character objCharacter, CancellationToken token)
        {
            object objItem = objOwned.Item;
            Dictionary<string, object> dicReturn = new Dictionary<string, object>
            {
                ["id"] = (objItem as IHasInternalId)?.InternalId,
                ["kind"] = objOwned.Kind,
                ["name"] = await GetItemNameAsync(objItem, token).ConfigureAwait(false)
            };
            switch (objItem)
            {
                case Gear objGear:
                    dicReturn["quantity"] = objGear.Quantity;
                    if (!string.IsNullOrEmpty(objGear.GearName))
                        dicReturn["customName"] = objGear.GearName;
                    if (objGear.IncludedInParent)
                        dicReturn["includedInParent"] = true;
                    if (objGear.Bonded)
                        dicReturn["bonded"] = true;
                    break;
                case Drug objDrug:
                    dicReturn["quantity"] = objDrug.Quantity;
                    break;
                case Cyberware objCyberware:
                    Grade objGrade = await objCyberware.GetGradeAsync(token).ConfigureAwait(false);
                    if (objGrade != null)
                        dicReturn["grade"] = objGrade.Name;
                    dicReturn["essence"] = await objCyberware.GetCalculatedESSAsync(token).ConfigureAwait(false);
                    break;
                case Lifestyle objLifestyle:
                    dicReturn["months"] = await objLifestyle.GetIncrementsAsync(token).ConfigureAwait(false);
                    break;
                case Spirit objSpirit:
                    string strCritterName = await objSpirit.GetCritterNameAsync(token).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(strCritterName))
                        dicReturn["customName"] = strCritterName;
                    break;
            }

            if (objItem is IHasCustomName objCustom && !string.IsNullOrEmpty(objCustom.CustomName))
                dicReturn["customName"] = objCustom.CustomName;
            if (objItem is IHasRating objRating)
            {
                int intRating = await objRating.GetRatingAsync(token).ConfigureAwait(false);
                if (intRating > 0)
                    dicReturn["rating"] = intRating;
            }

            if (objItem is ICanEquip objEquip)
                dicReturn["equipped"] = objEquip.Equipped;
            if (objItem is IHasWirelessBonus objWireless && objWireless.WirelessBonus != null)
                dicReturn["wireless"] = objWireless.WirelessOn;
            if (objItem is IHasMatrixAttributes objMatrix && await objMatrix.IsActiveCommlinkAsync(objCharacter, token).ConfigureAwait(false))
                dicReturn["activeCommlink"] = true;
            if (objItem is IHasLocation objHasLocation && objHasLocation.Location != null)
                dicReturn["location"] = objHasLocation.Location.Name;
            if (objItem is IHasNotes objNotes)
            {
                string strNotes = await objNotes.GetNotesAsync(token).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(strNotes))
                    dicReturn["notes"] = strNotes;
            }

            if (objOwned.Children.Count > 0)
            {
                List<object> lstChildren = new List<object>(objOwned.Children.Count);
                foreach (OwnedItem objChild in objOwned.Children)
                    lstChildren.Add(await DescribeItemAsync(objChild, objCharacter, token).ConfigureAwait(false));
                dicReturn["contains"] = lstChildren;
            }

            return dicReturn;
        }

        private static async Task<object> ListItemsAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            string strKind = objArgs.Value<string>("kind")?.Trim().ToLowerInvariant();
            List<object> lstReturn = new List<object>();
            foreach (OwnedItem objRoot in await GetOwnedItemsAsync(objCharacter, token).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(strKind) && !objRoot.Kind.StartsWith(strKind, StringComparison.Ordinal))
                    continue;
                lstReturn.Add(await DescribeItemAsync(objRoot, objCharacter, token).ConfigureAwait(false));
            }

            return lstReturn;
        }

        /// <summary>
        /// Let the editor show an item's changed name, notes and values, and mark the character as changed.
        /// </summary>
        private static async Task<object> FinishItemEditAsync(CharacterShared frmCharacter, object objItem, string strResult, CancellationToken token)
        {
            await frmCharacter.RefreshItemTreeNodesAsync(objItem, token).ConfigureAwait(false);
            if (frmCharacter is CharacterCreate frmCreate)
                return await FinishEditAsync(frmCreate, strResult, token).ConfigureAwait(false);
            await frmCharacter.MakeDirtyWithCharacterUpdate(token).ConfigureAwait(false);
            return new { result = strResult };
        }

        #endregion

        private static async Task<object> SetItemDetailsAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            OwnedItem objOwned = await FindItemAsync(objCharacter, RequiredString(objArgs, "id"), token).ConfigureAwait(false);
            object objItem = objOwned.Item;
            string strName = await GetItemNameAsync(objItem, token).ConfigureAwait(false);
            List<string> lstChanged = new List<string>();

            string GetText(string strField)
            {
                JToken objValue = objArgs[strField];
                return objValue == null || objValue.Type == JTokenType.Null
                    ? null
                    : objValue.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            }

            string strNotes = GetText("notes");
            if (strNotes != null)
            {
                if (!(objItem is IHasNotes objNotes))
                    throw new AgentToolException(strName + " has no notes.");
                await objNotes.SetNotesAsync(strNotes, token).ConfigureAwait(false);
                lstChanged.Add("notes");
            }

            string strCustomName = GetText("custom_name");
            if (strCustomName != null)
            {
                // The editor's Name menu items for each tab
                switch (objItem)
                {
                    case Gear objGear:
                        objGear.GearName = strCustomName;
                        break;
                    case Spirit objSpirit:
                        await objSpirit.SetCritterNameAsync(strCustomName, token).ConfigureAwait(false);
                        break;
                    case IHasCustomName objCustom:
                        objCustom.CustomName = strCustomName;
                        break;
                    default:
                        throw new AgentToolException(strName + " can't be given a custom name.");
                }

                lstChanged.Add("custom name");
            }

            string strExtra = GetText("extra");
            if (strExtra != null)
            {
                if (!(objItem is Gear objGear) || !objGear.AllowRename)
                    throw new AgentToolException(strName + " isn't a custom item that can be renamed (use custom_name for a display name).");
                objGear.Extra = strExtra;
                lstChanged.Add("extra");
            }

            string strLocation = GetText("location");
            if (strLocation != null)
            {
                if (!(objItem is IHasLocation objHasLocation) || objOwned.Parent != null)
                    throw new AgentToolException(strName + " can't be filed under a location (only top-level gear, armor, weapons and vehicles can).");
                ThreadSafeObservableCollection<Location> lstLocations = GetLocationsFor(objCharacter, objItem)
                                                                        ?? throw new AgentToolException(strName + " can't be filed under a location.");
                Location objNewLocation = null;
                if (!string.IsNullOrWhiteSpace(strLocation))
                {
                    objNewLocation = await FindLocationAsync(lstLocations, strLocation.Trim(), token).ConfigureAwait(false)
                                     ?? throw new AgentToolException("There is no location '" + strLocation + "' on that tab. Add one with manage_location.");
                }

                // What dragging an item onto a location node (or out of one) does
                Location objOldLocation = objHasLocation.Location;
                if (!ReferenceEquals(objOldLocation, objNewLocation))
                {
                    if (objOldLocation != null)
                        await objOldLocation.Children.RemoveAsync(objHasLocation, token).ConfigureAwait(false);
                    objHasLocation.Location = objNewLocation;
                    if (objNewLocation != null)
                        await objNewLocation.Children.AddAsync(objHasLocation, token).ConfigureAwait(false);
                }

                lstChanged.Add("location");
            }

            if (lstChanged.Count == 0)
                throw new AgentToolException("Pass at least one field to set: notes, custom_name, extra or location.");
            return await FinishItemEditAsync(frmCharacter, objItem, "Set " + string.Join(", ", lstChanged) + " for " + strName + ".", token).ConfigureAwait(false);
        }

        private static async Task<object> SetItemStateAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            OwnedItem objOwned = await FindItemAsync(objCharacter, RequiredString(objArgs, "id"), token).ConfigureAwait(false);
            object objItem = objOwned.Item;
            string strName = await GetItemNameAsync(objItem, token).ConfigureAwait(false);
            List<string> lstChanged = new List<string>();

            bool? blnEquipped = objArgs.Value<bool?>("equipped");
            if (blnEquipped.HasValue)
            {
                bool blnValue = blnEquipped.Value;
                // The Equipped checkboxes of the Gear, Armor and Weapons tabs
                switch (objItem)
                {
                    case Gear objGear:
                        await objGear.SetEquippedAsync(blnValue, token).ConfigureAwait(false);
                        (Gear _, Armor objParentArmor, ArmorMod objParentMod) = await objCharacter.Armor.FindArmorGearAsync(objGear.InternalId, token).ConfigureAwait(false);
                        if (objParentArmor != null)
                        {
                            // Gear in armor only gives its bonuses while the armor (and its mod) is worn
                            if (!blnValue)
                                await objGear.ChangeEquippedStatusAsync(false, token: token).ConfigureAwait(false);
                            else if (objParentArmor.Equipped && objParentMod?.Equipped != false)
                                await objGear.ChangeEquippedStatusAsync(true, token: token).ConfigureAwait(false);
                        }
                        else
                            await objGear.ChangeEquippedStatusAsync(blnValue, token: token).ConfigureAwait(false);
                        break;
                    case Armor objArmor:
                        await objArmor.SetEquippedAsync(blnValue, token).ConfigureAwait(false);
                        break;
                    case ArmorMod objMod:
                        await objMod.SetEquippedAsync(blnValue, token).ConfigureAwait(false);
                        break;
                    case Weapon objWeapon:
                        await objWeapon.SetEquippedAsync(blnValue, token).ConfigureAwait(false);
                        break;
                    case WeaponAccessory objAccessory:
                        await objAccessory.SetEquippedAsync(blnValue, token: token).ConfigureAwait(false);
                        break;
                    default:
                        throw new AgentToolException(strName + " can't be equipped or unequipped.");
                }

                lstChanged.Add(blnValue ? "equipped" : "unequipped");
            }

            bool? blnWireless = objArgs.Value<bool?>("wireless");
            if (blnWireless.HasValue)
            {
                if (!(objItem is IHasWirelessBonus objWireless))
                    throw new AgentToolException(strName + " has no wireless mode.");
                objWireless.WirelessOn = blnWireless.Value;
                lstChanged.Add("wireless " + (blnWireless.Value ? "on" : "off"));
            }

            bool? blnActive = objArgs.Value<bool?>("active_commlink");
            if (blnActive.HasValue)
            {
                if (!(objItem is IHasMatrixAttributes objMatrix) || !await objMatrix.GetIsCommlinkAsync(token).ConfigureAwait(false))
                    throw new AgentToolException(strName + " can't be the active commlink.");
                await objMatrix.SetActiveCommlinkAsync(objCharacter, blnActive.Value, token).ConfigureAwait(false);
                lstChanged.Add(blnActive.Value ? "active commlink" : "not the active commlink");
            }

            bool? blnHomeNode = objArgs.Value<bool?>("home_node");
            if (blnHomeNode.HasValue)
            {
                if (!(objItem is IHasMatrixAttributes objMatrix))
                    throw new AgentToolException(strName + " can't be a home node.");
                if (!await objCharacter.GetIsAIAsync(token).ConfigureAwait(false))
                    throw new AgentToolException("Only AIs have a home node.");
                // The Home Node checkbox is enabled for a commlink whose program limit fits the AI (2 if its Depth is above the device rating)
                if (blnHomeNode.Value
                    && (!await objMatrix.GetIsCommlinkAsync(token).ConfigureAwait(false)
                        || await objMatrix.GetTotalMatrixAttributeAsync("Program Limit", token).ConfigureAwait(false)
                        < (await (await objCharacter.GetAttributeAsync("DEP", token: token).ConfigureAwait(false)).GetTotalValueAsync(token).ConfigureAwait(false)
                           > await objMatrix.GetTotalMatrixAttributeAsync("Device Rating", token).ConfigureAwait(false)
                            ? 2
                            : 1)))
                    throw new AgentToolException(strName + " can't be this AI's home node (it must be a commlink with a big enough program limit).");
                await objMatrix.SetHomeNodeAsync(objCharacter, blnHomeNode.Value, token).ConfigureAwait(false);
                lstChanged.Add(blnHomeNode.Value ? "home node" : "not the home node");
            }

            bool? blnStolen = objArgs.Value<bool?>("stolen");
            if (blnStolen.HasValue)
            {
                if (!(objItem is IHasStolenProperty objLoot))
                    throw new AgentToolException(strName + " can't be marked stolen.");
                if ((await ImprovementManager.GetCachedImprovementListForValueOfAsync(objCharacter, Improvement.ImprovementType.Nuyen, "Stolen", token: token)
                                             .ConfigureAwait(false)).Count == 0)
                    throw new AgentToolException("The character has nothing that grants stolen gear, so items can't be marked stolen.");
                objLoot.Stolen = blnStolen.Value;
                lstChanged.Add(blnStolen.Value ? "stolen" : "not stolen");
            }

            bool? blnDiscount = objArgs.Value<bool?>("black_market_discount");
            if (blnDiscount.HasValue)
            {
                if (!(frmCharacter is CharacterCreate))
                    throw new AgentToolException("The black market discount can only be changed during character creation.");
                if (!(objItem is ICanBlackMarketDiscount objDiscountable))
                    throw new AgentToolException(strName + " can't get the black market discount.");
                if (!await objCharacter.GetBlackMarketDiscountAsync(token).ConfigureAwait(false))
                    throw new AgentToolException("The character doesn't have the Black Market Pipeline quality.");
                if (objItem is Gear objGear && objGear.IncludedInParent)
                    throw new AgentToolException(strName + " comes with its parent item; set the discount on that instead.");
                if (!await IsBlackMarketCategoryAsync(objCharacter, objItem, token).ConfigureAwait(false))
                    throw new AgentToolException("The character's Black Market Pipeline doesn't cover " + strName + "'s category.");
                objDiscountable.DiscountCost = blnDiscount.Value;
                lstChanged.Add(blnDiscount.Value ? "black market discount" : "no black market discount");
            }

            bool? blnPrototype = objArgs.Value<bool?>("prototype_transhuman");
            if (blnPrototype.HasValue)
            {
                // The Prototype Transhuman checkbox: top-level bioware of a Prototype Transhuman character, during creation
                if (!(frmCharacter is CharacterCreate) || !(objItem is Cyberware objBioware)
                                                       || await objBioware.GetSourceTypeAsync(token).ConfigureAwait(false) != Improvement.ImprovementSource.Bioware
                                                       || objOwned.Parent != null
                                                       || !await objCharacter.GetIsPrototypeTranshumanAsync(token).ConfigureAwait(false))
                    throw new AgentToolException("Only top-level bioware of a Prototype Transhuman character can use Prototype Transhuman essence, during creation.");
                await objBioware.SetPrototypeTranshumanAsync(blnPrototype.Value, token).ConfigureAwait(false);
                lstChanged.Add(blnPrototype.Value ? "prototype transhuman" : "not prototype transhuman");
            }

            bool? blnIncluded = objArgs.Value<bool?>("included_in_weapon");
            if (blnIncluded.HasValue)
            {
                if (!(objItem is WeaponAccessory objIncludedAccessory) || objIncludedAccessory.Parent == null
                                                                       || !(await objCharacter.GetSettingsAsync(token).ConfigureAwait(false)).AllowEditPartOfBaseWeapon)
                    throw new AgentToolException("Only weapon accessories can be part of the base weapon, and only where the settings allow editing that.");
                objIncludedAccessory.IncludedInWeapon = blnIncluded.Value;
                lstChanged.Add(blnIncluded.Value ? "part of the base weapon" : "not part of the base weapon");
            }

            string strFireMode = objArgs.Value<string>("fire_mode")?.Trim();
            if (!string.IsNullOrEmpty(strFireMode))
            {
                bool blnVehicleWeapon = false;
                for (OwnedItem objLoop = objOwned.Parent; objLoop != null; objLoop = objLoop.Parent)
                    blnVehicleWeapon |= objLoop.Item is Vehicle;
                if (!(objItem is Weapon objWeapon) || !blnVehicleWeapon)
                    throw new AgentToolException("Only vehicle weapons have a firing mode.");
                if (!Enum.TryParse(strFireMode, true, out Backend.Enums.FiringMode eMode) || eMode == Backend.Enums.FiringMode.NumFiringModes)
                    throw new AgentToolException("`fire_mode` must be Skill, GunneryCommandDevice, RemoteOperated, DogBrain or ManualOperation.");
                objWeapon.FireMode = eMode;
                lstChanged.Add("firing mode " + eMode);
            }

            if (lstChanged.Count == 0)
                throw new AgentToolException("Pass at least one field to set.");
            return await FinishItemEditAsync(frmCharacter, objItem, strName + ": " + string.Join(", ", lstChanged) + ".", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Whether the character's Black Market Pipeline covers an item, as the editor decides whether to enable its discount checkbox.
        /// </summary>
        private static async Task<bool> IsBlackMarketCategoryAsync(Character objCharacter, object objItem, CancellationToken token)
        {
            string strFile;
            string strCategory;
            switch (objItem)
            {
                case Gear objGear:
                    strFile = "gear.xml";
                    strCategory = objGear.Category;
                    break;
                case Weapon objWeapon:
                    strFile = "weapons.xml";
                    strCategory = objWeapon.Category;
                    break;
                case Armor objArmor:
                    strFile = "armor.xml";
                    strCategory = objArmor.Category;
                    break;
                case Cyberware objCyberware:
                    strFile = await objCyberware.GetSourceTypeAsync(token).ConfigureAwait(false) == Improvement.ImprovementSource.Bioware
                        ? "bioware.xml"
                        : "cyberware.xml";
                    strCategory = objCyberware.Category;
                    break;
                case Vehicle objVehicle:
                    strFile = "vehicles.xml";
                    strCategory = objVehicle.Category;
                    break;
                default:
                    // Accessories, mods and the like follow their parent's discount in the editor
                    return true;
            }

            return (await objCharacter.GenerateBlackMarketMappingsAsync(
                       (await objCharacter.LoadDataXPathAsync(strFile, token: token).ConfigureAwait(false)).SelectSingleNodeAndCacheExpression("/chummer", token: token),
                       token).ConfigureAwait(false)).Contains(strCategory);
        }

        private static async Task<object> SetItemValuesAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            OwnedItem objOwned = await FindItemAsync(objCharacter, RequiredString(objArgs, "id"), token).ConfigureAwait(false);
            object objItem = objOwned.Item;
            string strName = await GetItemNameAsync(objItem, token).ConfigureAwait(false);
            bool blnIgnoreRules = await objCharacter.GetIgnoreRulesAsync(token).ConfigureAwait(false);
            List<string> lstChanged = new List<string>();

            int? intRating = OptionalInt(objArgs, "rating");
            if (intRating.HasValue)
            {
                int intNew = intRating.Value;
                void CheckRange(int intMinimum, int intMaximum, bool blnEditable)
                {
                    if (!blnEditable || intMaximum <= intMinimum)
                        throw new AgentToolException(strName + "'s rating can't be changed" + (blnEditable ? " (it has a fixed rating)." : " (it comes with its parent item)."));
                    if (!blnIgnoreRules && (intNew < intMinimum || intNew > intMaximum))
                        throw new AgentToolException(strName + " takes a rating from " + intMinimum.ToString(GlobalSettings.InvariantCultureInfo) + " to "
                                                     + intMaximum.ToString(GlobalSettings.InvariantCultureInfo) + ".");
                }

                // The ranges the editor gives each tab's rating box
                switch (objItem)
                {
                    case Gear objGear:
                    {
                        int intMaximum = await objGear.GetMaxRatingValueAsync(token).ConfigureAwait(false);
                        if (intMaximum == int.MaxValue)
                            intMaximum = 0;
                        CheckRange(await objGear.GetMinRatingValueAsync(token).ConfigureAwait(false), intMaximum, !objGear.IncludedInParent);
                        if (!await frmCreate.SetGearRatingAsync(objGear, intNew, token).ConfigureAwait(false))
                            throw new AgentToolException(strName + " couldn't be set to that rating (bonded foci are limited by Magic and the focus limit).");
                        break;
                    }
                    case Cyberware objCyberware:
                        CheckRange(await objCyberware.GetMinRatingAsync(token).ConfigureAwait(false), await objCyberware.GetMaxRatingAsync(token).ConfigureAwait(false),
                                   string.IsNullOrEmpty(objCyberware.ParentID));
                        await frmCreate.SetCyberwareRatingAsync(objCyberware, intNew, token).ConfigureAwait(false);
                        break;
                    case ArmorMod objArmorMod:
                        CheckRange(1, await objArmorMod.GetMaxRatingValueAsync(token).ConfigureAwait(false), !objArmorMod.IncludedInArmor);
                        await frmCreate.SetArmorModRatingAsync(objArmorMod, intNew, token).ConfigureAwait(false);
                        break;
                    case Weapon objWeapon:
                    {
                        int intMaximum = await objWeapon.GetMaxRatingValueAsync(token).ConfigureAwait(false);
                        CheckRange(Math.Min(await objWeapon.GetMinRatingValueAsync(token).ConfigureAwait(false), intMaximum), intMaximum,
                                   string.IsNullOrEmpty(objWeapon.ParentID) && await objWeapon.GetRatingAsync(token).ConfigureAwait(false) > 0);
                        await objWeapon.SetRatingAsync(intNew, token).ConfigureAwait(false);
                        break;
                    }
                    case WeaponAccessory objAccessory:
                    {
                        int intMaximum = await objAccessory.GetMaxRatingValueAsync(token).ConfigureAwait(false);
                        CheckRange(Math.Min(1, intMaximum), intMaximum, !objAccessory.IncludedInWeapon);
                        await objAccessory.SetRatingAsync(intNew, token).ConfigureAwait(false);
                        break;
                    }
                    case VehicleMod objVehicleMod:
                    {
                        int intMaximum = await objVehicleMod.GetMaxRatingAsync(token).ConfigureAwait(false);
                        CheckRange(Math.Min(1, intMaximum), intMaximum, !objVehicleMod.IncludedInVehicle);
                        await objVehicleMod.SetRatingAsync(intNew, token).ConfigureAwait(false);
                        break;
                    }
                    default:
                        throw new AgentToolException(strName + "'s rating can't be changed after it is bought; remove it and buy it again at the new rating.");
                }

                lstChanged.Add("rating " + intNew.ToString(GlobalSettings.InvariantCultureInfo));
            }

            decimal? decQuantity = objArgs.Value<decimal?>("quantity");
            if (decQuantity.HasValue)
            {
                decimal decNew = decQuantity.Value;
                switch (objItem)
                {
                    case Gear objGear:
                    {
                        if (objGear.IncludedInParent)
                            throw new AgentToolException(strName + " comes with its parent item; its quantity can't be changed.");
                        // The quantity box's minimum and decimals: nuyen and currency can be fractional
                        decimal decMinimum = 1;
                        bool blnWhole = true;
                        if (objGear.Name.StartsWith("Nuyen", StringComparison.Ordinal))
                        {
                            int intDecimals = await (await objCharacter.GetSettingsAsync(token).ConfigureAwait(false)).GetMaxNuyenDecimalsAsync(token).ConfigureAwait(false);
                            for (int i = 0; i < intDecimals; ++i)
                                decMinimum /= 10.0m;
                            blnWhole = intDecimals <= 0;
                        }
                        else if (objGear.Category == "Currency")
                        {
                            decMinimum = 0.01m;
                            blnWhole = false;
                        }

                        if (decNew < decMinimum || blnWhole && decNew != decimal.Truncate(decNew))
                            throw new AgentToolException(strName + "'s quantity must be " + (blnWhole ? "a whole number of at least " : "at least ")
                                                         + decMinimum.ToString(GlobalSettings.InvariantCultureInfo) + ". Use remove_item to get rid of it.");
                        await objGear.SetQuantityAsync(decNew, token).ConfigureAwait(false);
                        break;
                    }
                    case Drug objDrug:
                        if (decNew < 1 || decNew != decimal.Truncate(decNew))
                            throw new AgentToolException(strName + "'s quantity must be a whole number of at least 1.");
                        objDrug.Quantity = decNew;
                        break;
                    default:
                        throw new AgentToolException(strName + " has no quantity.");
                }

                lstChanged.Add("quantity " + decNew.ToString(GlobalSettings.InvariantCultureInfo));
            }

            string strGrade = objArgs.Value<string>("grade")?.Trim();
            if (!string.IsNullOrEmpty(strGrade))
            {
                if (!(objItem is Cyberware objCyberware))
                    throw new AgentToolException(strName + " has no grade.");
                if (!string.IsNullOrEmpty(objCyberware.ParentID))
                    throw new AgentToolException(strName + " comes with its parent item; change the parent's grade instead.");
                // The grades the Cyberware tab's grade box offers
                Improvement.ImprovementSource eSource = await objCyberware.GetSourceTypeAsync(token).ConfigureAwait(false);
                List<Grade> lstGrades = await objCharacter.GetGradesListAsync(eSource, blnIgnoreRules, token).ConfigureAwait(false);
                Grade objNewGrade = null;
                foreach (Grade objLoop in lstGrades)
                {
                    if (string.Equals(objLoop.Name, strGrade, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(await objLoop.GetCurrentDisplayNameAsync(token).ConfigureAwait(false), strGrade, StringComparison.OrdinalIgnoreCase))
                    {
                        objNewGrade = objLoop;
                        break;
                    }
                }

                if (objNewGrade == null)
                    throw new AgentToolException("'" + strGrade + "' is not an available grade. Options: " + string.Join(", ", lstGrades.Select(x => x.Name)) + ".");
                await objCyberware.SetGradeAsync(objNewGrade, token).ConfigureAwait(false);
                lstChanged.Add("grade " + objNewGrade.Name);
            }

            int? intMonths = OptionalInt(objArgs, "months");
            if (intMonths.HasValue)
            {
                if (!(objItem is Lifestyle objLifestyle))
                    throw new AgentToolException(strName + " isn't a lifestyle.");
                if (intMonths.Value < 1)
                    throw new AgentToolException("A lifestyle is paid for at least 1 month.");
                await objLifestyle.SetIncrementsAsync(intMonths.Value, token).ConfigureAwait(false);
                lstChanged.Add(intMonths.Value.ToString(GlobalSettings.InvariantCultureInfo) + " month(s)");
            }

            if (lstChanged.Count == 0)
                throw new AgentToolException("Pass at least one field to set: rating, quantity, grade or months.");
            return await FinishItemEditAsync(frmCreate, objItem, strName + ": " + string.Join(", ", lstChanged) + ".", token).ConfigureAwait(false);
        }

        private static async Task<object> SwapMatrixAttributesAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            OwnedItem objOwned = await FindItemAsync(objCharacter, RequiredString(objArgs, "id"), token).ConfigureAwait(false);
            string strName = await GetItemNameAsync(objOwned.Item, token).ConfigureAwait(false);
            if (!(objOwned.Item is IHasMatrixAttributes objMatrix) || !objMatrix.CanSwapAttributes)
                throw new AgentToolException(strName + "'s matrix attributes can't be rearranged.");
            string[] astrAttributes = { "Attack", "Sleaze", "Data Processing", "Firewall" };
            string GetAttribute(string strField)
            {
                string strValue = RequiredString(objArgs, strField);
                return astrAttributes.FirstOrDefault(x => string.Equals(x, strValue, StringComparison.OrdinalIgnoreCase)
                                                          || string.Equals(x.Replace(" ", string.Empty), strValue.Replace(" ", string.Empty).Replace("_", string.Empty),
                                                                           StringComparison.OrdinalIgnoreCase))
                       ?? throw new AgentToolException("`" + strField + "` must be one of: " + string.Join(", ", astrAttributes) + ".");
            }

            string strFirst = GetAttribute("first");
            string strSecond = GetAttribute("second");
            if (strFirst == strSecond)
                throw new AgentToolException("Pick two different attributes.");
            await objMatrix.SwapMatrixAttributesAsync(objCharacter, strFirst, strSecond, token).ConfigureAwait(false);
            await objMatrix.RefreshMatrixAttributeArrayAsync(objCharacter, token).ConfigureAwait(false);
            return await FinishItemEditAsync(frmCharacter, objOwned.Item, "Swapped " + strFirst + " and " + strSecond + " on " + strName + ": now "
                                                                           + string.Join(", ", await Task.WhenAll(astrAttributes.Select(async x =>
                                                                               x + " " + (await objMatrix.GetTotalMatrixAttributeAsync(x, token).ConfigureAwait(false))
                                                                               .ToString(GlobalSettings.InvariantCultureInfo))).ConfigureAwait(false)) + ".",
                                             token).ConfigureAwait(false);
        }

        private static async Task<object> MoveModularCyberwareAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            OwnedItem objOwned = await FindItemAsync(objCharacter, RequiredString(objArgs, "id"), token).ConfigureAwait(false);
            if (!(objOwned.Item is Cyberware objModular) || string.IsNullOrEmpty(await objModular.GetPlugsIntoModularMountAsync(token).ConfigureAwait(false)))
                throw new AgentToolException("That isn't modular cyberware.");
            // The mounts its Change Mount dialog offers
            List<ListItem> lstMounts = await objCharacter.ConstructModularCyberlimbListAsync(objModular, true, token).ConfigureAwait(false);
            List<(string Id, string Name)> lstOptions;
            try
            {
                lstOptions = lstMounts.Select(x => (x.Value?.ToString(), x.Name)).ToList();
            }
            finally
            {
                Utils.ListItemListPool.Return(ref lstMounts);
            }

            string strMount = objArgs.Value<string>("mount")?.Trim();
            string strOptions = string.Join("; ", lstOptions.Select(x => x.Name + " (" + x.Id + ")"));
            if (string.IsNullOrEmpty(strMount))
                return new { result = "Mounts " + objModular.Name + " can go to: " + strOptions + "." };
            (string strId, string strName) = lstOptions.FirstOrDefault(x => string.Equals(x.Id, strMount, StringComparison.OrdinalIgnoreCase)
                                                                           || string.Equals(x.Name, strMount, StringComparison.OrdinalIgnoreCase));
            if (strId == null)
                throw new AgentToolException("'" + strMount + "' isn't a mount " + objModular.Name + " can go to. Options: " + strOptions + ".");
            bool blnInVehicle = false;
            for (OwnedItem objLoop = objOwned.Parent; objLoop != null; objLoop = objLoop.Parent)
                blnInVehicle |= objLoop.Item is Vehicle;
            if (blnInVehicle)
                await frmCharacter.ApplyVehicleModularMountLocationAsync(objModular, strId, token).ConfigureAwait(false);
            else
                await frmCharacter.ApplyModularMountLocationAsync(objModular, strId, token).ConfigureAwait(false);
            return await FinishItemEditAsync(frmCharacter, objModular, "Moved " + objModular.Name + " to " + strName + ".", token).ConfigureAwait(false);
        }

        #region Locations

        private static ThreadSafeObservableCollection<Location> GetLocationsFor(Character objCharacter, object objItem)
        {
            switch (objItem)
            {
                case Gear _:
                    return objCharacter.GearLocations;
                case Armor _:
                    return objCharacter.ArmorLocations;
                case Weapon _:
                    return objCharacter.WeaponLocations;
                case Vehicle _:
                    return objCharacter.VehicleLocations;
                default:
                    return null;
            }
        }

        private static async Task<Location> FindLocationAsync(ThreadSafeObservableCollection<Location> lstLocations, string strName, CancellationToken token)
        {
            List<Location> lstAll = await lstLocations.ToListAsync(token).ConfigureAwait(false);
            return lstAll.Find(x => string.Equals(x.InternalId, strName, StringComparison.OrdinalIgnoreCase))
                   ?? lstAll.Find(x => string.Equals(x.Name, strName, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task<object> ManageLocationAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCharacter.CharacterObject;
            string strAction = RequiredString(objArgs, "action").ToLowerInvariant();
            string strName = RequiredString(objArgs, "name");
            ThreadSafeObservableCollection<Location>[] alstAll =
            {
                objCharacter.GearLocations, objCharacter.ArmorLocations, objCharacter.WeaponLocations, objCharacter.VehicleLocations
            };
            switch (strAction)
            {
                case "add":
                {
                    ThreadSafeObservableCollection<Location> lstLocations;
                    switch (objArgs.Value<string>("tab")?.Trim().ToLowerInvariant())
                    {
                        case null:
                        case "":
                        case "gear":
                            lstLocations = objCharacter.GearLocations;
                            break;
                        case "armor":
                            lstLocations = objCharacter.ArmorLocations;
                            break;
                        case "weapon":
                        case "weapons":
                            lstLocations = objCharacter.WeaponLocations;
                            break;
                        case "vehicle":
                        case "vehicles":
                            lstLocations = objCharacter.VehicleLocations;
                            break;
                        default:
                            throw new AgentToolException("`tab` must be 'gear', 'armor', 'weapons' or 'vehicles'.");
                    }

                    // The tabs' Add Location buttons
                    Location objLocation = new Location(objCharacter, lstLocations, strName);
                    await lstLocations.AddAsync(objLocation, token).ConfigureAwait(false);
                    await frmCharacter.MakeDirtyWithCharacterUpdate(token).ConfigureAwait(false);
                    return new { result = "Added location " + strName + " (id " + objLocation.InternalId + ")." };
                }
                case "rename":
                case "remove":
                {
                    Location objLocation = null;
                    foreach (ThreadSafeObservableCollection<Location> lstLoop in alstAll)
                    {
                        objLocation = await FindLocationAsync(lstLoop, strName, token).ConfigureAwait(false);
                        if (objLocation != null)
                            break;
                    }

                    if (objLocation == null)
                        throw new AgentToolException("There is no location '" + strName + "'. Use list_items(kind='location').");
                    if (strAction == "rename")
                    {
                        string strNewName = RequiredString(objArgs, "new_name");
                        // The tabs' Rename Location menu items
                        objLocation.Name = strNewName;
                        await frmCharacter.RefreshItemTreeNodesAsync(objLocation, token).ConfigureAwait(false);
                        await frmCharacter.MakeDirtyWithCharacterUpdate(token).ConfigureAwait(false);
                        return new { result = "Renamed location " + strName + " to " + strNewName + "." };
                    }

                    // Deleting a location node: its items go back to the top of the tab
                    await objLocation.RemoveAsync(false, token).ConfigureAwait(false);
                    await frmCharacter.MakeDirtyWithCharacterUpdate(token).ConfigureAwait(false);
                    return new { result = "Removed location " + strName + "; its items are no longer filed under a location." };
                }
                default:
                    throw new AgentToolException("`action` must be 'add', 'rename' or 'remove'.");
            }
        }

        #endregion

        private static async Task<object> CopyItemAsync(JObject objArgs, CancellationToken token)
        {
            CharacterShared frmCharacter = await GetCharacterFormAsync(objArgs, token).ConfigureAwait(false);
            OwnedItem objOwned = await FindItemAsync(frmCharacter.CharacterObject, RequiredString(objArgs, "id"), token).ConfigureAwait(false);
            object objItem = objOwned.Item;
            string strName = await GetItemNameAsync(objItem, token).ConfigureAwait(false);
            // What Edit > Copy handles
            if (!(objItem is Armor || objItem is ArmorMod || objItem is Cyberware || objItem is Gear || objItem is Lifestyle || objItem is Vehicle
                  || objItem is Weapon || objItem is WeaponAccessory))
                throw new AgentToolException(strName + " can't be copied; only armor, armor mods, cyberware, gear, lifestyles, vehicles, weapons and weapon accessories can.");
            await frmCharacter.CopyObject(objItem, token).ConfigureAwait(false);
            return new { result = "Copied " + strName + ". Use paste_item to add the copy." };
        }

        private static async Task<object> PasteItemAsync(JObject objArgs, CancellationToken token)
        {
            CharacterCreate frmCreate = await GetCreateFormAsync(objArgs, token).ConfigureAwait(false);
            Character objCharacter = frmCreate.CharacterObject;
            object objInto = null;
            string strInto = objArgs.Value<string>("into")?.Trim();
            if (!string.IsNullOrEmpty(strInto))
            {
                objInto = (await FindItemAsync(objCharacter, strInto, token).ConfigureAwait(false)).Item;
                // Edit > Paste uses the item selected in the copied item's own tab, so only these can be targets
                bool blnFits;
                switch (await GlobalSettings.GetClipboardContentTypeAsync(token).ConfigureAwait(false))
                {
                    case ClipboardContentType.ArmorMod:
                        blnFits = objInto is Armor;
                        break;
                    case ClipboardContentType.Cyberware:
                        blnFits = objInto is Cyberware;
                        break;
                    case ClipboardContentType.Gear:
                        blnFits = objInto is IHasGear;
                        break;
                    case ClipboardContentType.Weapon:
                        blnFits = objInto is Weapon || objInto is WeaponMount || objInto is VehicleMod;
                        break;
                    case ClipboardContentType.WeaponAccessory:
                        blnFits = objInto is Weapon;
                        break;
                    default:
                        blnFits = false;
                        break;
                }

                if (!blnFits)
                    throw new AgentToolException("What was copied doesn't go into " + await GetItemNameAsync(objInto, token).ConfigureAwait(false) + ".");
            }

            if (!await frmCreate.PasteClipboardAsync(objInto, token).ConfigureAwait(false))
                throw new AgentToolException(objInto == null
                                                 ? "Nothing that goes on the character was copied; copy something with copy_item, or pass `into` for parts that go into another item."
                                                 : "What was copied doesn't go into " + await GetItemNameAsync(objInto, token).ConfigureAwait(false) + ".");
            return await FinishEditAsync(frmCreate, "Pasted the copy" + (objInto != null ? " into " + await GetItemNameAsync(objInto, token).ConfigureAwait(false) : string.Empty)
                                                    + ". Use list_items to see it.", token).ConfigureAwait(false);
        }
    }
}
