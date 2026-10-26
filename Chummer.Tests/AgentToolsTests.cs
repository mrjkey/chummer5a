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
using System.Windows.Forms;
using Chummer.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Chummer.Tests
{
    /// <summary>
    /// Tests for the MCP server that lets AI agents drive the character editor.
    /// </summary>
    [TestClass]
    public class AgentToolsTests
    {
        public TestContext TestContext { get; set; }

        private static readonly McpServer s_ObjServer = new McpServer(McpServer.DefaultPort, AgentTools.CreateAll());

        private static async Task<JObject> RpcAsync(string strMethod, JObject objParams, CancellationToken token, int intId = 1)
        {
            JObject objRequest = new JObject { ["jsonrpc"] = "2.0", ["id"] = intId, ["method"] = strMethod };
            if (objParams != null)
                objRequest["params"] = objParams;
            return await s_ObjServer.HandleMessageAsync(objRequest, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Call a tool and return (isError, text).
        /// </summary>
        private static async Task<(bool IsError, string Text)> CallToolAsync(string strName, JObject objArguments, CancellationToken token)
        {
            JObject objResponse = await RpcAsync("tools/call", new JObject { ["name"] = strName, ["arguments"] = objArguments ?? new JObject() }, token)
                .ConfigureAwait(false);
            JObject objResult = (JObject)objResponse["result"];
            Assert.IsNotNull(objResult, "No result for " + strName + ": " + objResponse);
            return (objResult.Value<bool>("isError"), objResult["content"][0].Value<string>("text"));
        }

        private static async Task<JToken> CallToolOkAsync(string strName, JObject objArguments, CancellationToken token)
        {
            (bool blnIsError, string strText) = await CallToolAsync(strName, objArguments, token).ConfigureAwait(false);
            Assert.IsFalse(blnIsError, strName + " failed: " + strText);
            return JToken.Parse(strText);
        }

        [TestMethod]
        public async Task Protocol_InitializeListAndErrors()
        {
            CancellationToken token = TestContext.CancellationToken;
            JObject objInit = await RpcAsync("initialize", new JObject { ["protocolVersion"] = "2025-03-26" }, token).ConfigureAwait(false);
            Assert.AreEqual("2025-03-26", objInit["result"].Value<string>("protocolVersion"));
            Assert.AreEqual("chummer", objInit["result"]["serverInfo"].Value<string>("name"));
            Assert.IsNotNull(objInit["result"]["capabilities"]["tools"]);

            JObject objList = await RpcAsync("tools/list", null, token).ConfigureAwait(false);
            JArray objTools = (JArray)objList["result"]["tools"];
            string[] astrNames = objTools.Select(x => x.Value<string>("name")).ToArray();
            foreach (string strExpected in new[] { "get_character_summary", "validate_character", "search_catalog", "set_attribute", "add_quality" })
                CollectionAssert.Contains(astrNames, strExpected);
            foreach (JToken objTool in objTools)
                Assert.AreEqual("object", objTool["inputSchema"].Value<string>("type"), objTool.Value<string>("name"));

            JObject objNotification = new JObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" };
            Assert.IsNull(await s_ObjServer.HandleMessageAsync(objNotification, token).ConfigureAwait(false));

            JObject objUnknown = await RpcAsync("does/not/exist", null, token).ConfigureAwait(false);
            Assert.AreEqual(-32601, objUnknown["error"].Value<int>("code"));

            (bool blnIsError, string _) = await CallToolAsync("no_such_tool", null, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError);
            (blnIsError, _) = await CallToolAsync("search_catalog", new JObject { ["kind"] = "nonsense" }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError);
        }

        [TestMethod]
        public void CommandLinePort()
        {
            Assert.AreEqual(McpServer.DefaultPort, McpServer.GetPortFromCommandLine(new[] { "Chummer5.exe" }));
            Assert.AreEqual(51800, McpServer.GetPortFromCommandLine(new[] { "Chummer5.exe", "/mcpport:51800" }));
            Assert.AreEqual(McpServer.DefaultPort, McpServer.GetPortFromCommandLine(new[] { "/mcpport:notaport" }));
        }

        /// <summary>
        /// Show a hidden main window as <see cref="Program.MainForm"/> while a scenario runs, then close it and every editor it opened.
        /// </summary>
        private void RunWithMainForm(Func<ChummerMainForm, CancellationToken, Task> funcScenario)
        {
            CancellationToken token = TestContext.CancellationToken;
            ChummerMainForm frmOldMainForm = Program.MainForm;
            ChummerMainForm frmTestForm = null;
            try
            {
                Utils.IsUnitTestForUI = true;
                frmTestForm = Utils.RunOnMainThread(() => new ChummerMainForm(true, true) { ShowInTaskbar = false }, token: token);
                Program.MainForm = frmTestForm;
                frmTestForm.DoThreadSafe(x => x.Show(), token);
                while (!frmTestForm.IsFinishedLoading)
                    Utils.SafeSleep(token);
                ChummerMainForm frmScenarioForm = frmTestForm;
                Utils.SafelyRunSynchronously(() => funcScenario(frmScenarioForm, token), token);
            }
            finally
            {
                try
                {
                    if (frmTestForm != null)
                    {
                        foreach (Form frmChild in frmTestForm.DoThreadSafeFunc(x => x.MdiChildren, token))
                            frmChild.DoThreadSafe(x => x.Close(), token);
                        frmTestForm.DoThreadSafe(x => x.Close(), token);
                    }
                }
                catch (Exception e) when (e is InvalidOperationException || e is ApplicationException)
                {
                    Console.WriteLine("Non-fatal exception while closing test forms: " + e.Message);
                }

                Program.MainForm = frmOldMainForm;
                Utils.IsUnitTestForUI = false;
            }
        }

        [TestMethod]
        public void ToolsAgainstOpenCharacter()
        {
            CancellationToken token = TestContext.CancellationToken;
            FileInfo objFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? string.Empty,
                                                         "TestFiles", "Skink.chum5"));
            Assert.IsTrue(objFile.Exists, objFile.FullName);
            Character objCharacter = new Character { FileName = objFile.FullName };
            Assert.IsTrue(objCharacter.Load(token: token));
            RunWithMainForm(async (frmMain, t) =>
            {
                CharacterShared frmCharacterForm = await frmMain.DoThreadSafeFuncAsync(() => new CharacterCreate(objCharacter), t).ConfigureAwait(false);
                await frmCharacterForm.DoThreadSafeAsync(x =>
                {
                    x.MdiParent = frmMain;
                    x.ShowInTaskbar = false;
                    x.Show();
                }, t).ConfigureAwait(false);
                while (!frmCharacterForm.IsFinishedInitializing)
                    await Task.Delay(50, t).ConfigureAwait(false);
                await RunToolScenarioAsync(t).ConfigureAwait(false);
            });
        }

        [TestMethod]
        public void CreateCharacterAndGear()
        {
            string strSaveFile = Path.Combine(Path.GetTempPath(), "AgentToolsTest_" + Guid.NewGuid().ToString("N") + ".chum5");
            try
            {
                RunWithMainForm((frmMain, t) => RunCreationScenarioAsync(frmMain, strSaveFile, t));
            }
            finally
            {
                if (File.Exists(strSaveFile))
                    File.Delete(strSaveFile);
            }
        }

        [TestMethod]
        public void MagicSkillsAndMods()
        {
            RunWithMainForm((frmMain, t) => RunMagicSkillsAndModsScenarioAsync(t));
        }

        /// <summary>
        /// Call a tool on one named character and expect success.
        /// </summary>
        private static Task<JToken> CallOnAsync(string strCharacter, string strName, JObject objArguments, CancellationToken token)
        {
            objArguments["character"] = strCharacter;
            return CallToolOkAsync(strName, objArguments, token);
        }

        private static Task<(bool IsError, string Text)> TryCallOnAsync(string strCharacter, string strName, JObject objArguments, CancellationToken token)
        {
            objArguments["character"] = strCharacter;
            return CallToolAsync(strName, objArguments, token);
        }

        /// <summary>
        /// The first option a refusal lists after "Options: ".
        /// </summary>
        private static string FirstOption(string strRefusal)
        {
            int intIndex = strRefusal.IndexOf("Options: ", StringComparison.Ordinal);
            Assert.IsTrue(intIndex >= 0, strRefusal);
            return strRefusal.Substring(intIndex + 9).Split(new[] { ", ", "; " }, StringSplitOptions.None)[0].TrimEnd('.');
        }

        private static async Task RunMagicSkillsAndModsScenarioAsync(CancellationToken token)
        {
            // A hermetic mage: tradition, spirits, mentor, specialization, initiation and metamagic
            const string strMage = "Agent Test Mage";
            await CallToolOkAsync("create_character", new JObject
            {
                ["metatype"] = "Human",
                ["priorities"] = new JObject { ["heritage"] = "D", ["attributes"] = "C", ["talent"] = "A", ["skills"] = "B", ["resources"] = "E" },
                ["talent"] = "Magician",
                ["talent_skills"] = new JArray("Spellcasting", "Summoning"),
                ["name"] = strMage
            }, token).ConfigureAwait(false);
            JObject objResult = (JObject)await CallOnAsync(strMage, "set_tradition", new JObject { ["id"] = "Hermetic" }, token).ConfigureAwait(false);
            StringAssert.Contains(objResult.Value<string>("result"), "Spirit of Fire");
            (bool blnIsError, string strText) = await TryCallOnAsync(strMage, "add_spirit", new JObject { ["type"] = "Spirit of Beasts" }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            StringAssert.Contains(strText, "Spirit of Air");
            await CallOnAsync(strMage, "add_spirit", new JObject { ["type"] = "Spirit of Air", ["force"] = 2, ["services"] = 2 }, token).ConfigureAwait(false);
            // Spirit details change in place
            JToken objNewSpirit = ((JArray)((JObject)await CallOnAsync(strMage, "get_character_summary", new JObject(), token).ConfigureAwait(false))["spirits"]).Single();
            await CallOnAsync(strMage, "set_spirit_details", new JObject
            {
                ["id"] = objNewSpirit.Value<string>("id"), ["services"] = 3, ["name"] = "Zephyr", ["notes"] = "Owes me one."
            }, token).ConfigureAwait(false);
            (bool blnSpiritError, string strSpiritText) = await TryCallOnAsync(strMage, "set_spirit_details", new JObject
            {
                ["id"] = objNewSpirit.Value<string>("id"), ["force"] = 99
            }, token).ConfigureAwait(false);
            Assert.IsTrue(blnSpiritError, strSpiritText);
            await CallOnAsync(strMage, "set_spirit_details", new JObject { ["id"] = "Zephyr", ["services"] = 2 }, token).ConfigureAwait(false);
            await CallOnAsync(strMage, "add_mentor_spirit", new JObject { ["mentor"] = "Dog" }, token).ConfigureAwait(false);
            await CallOnAsync(strMage, "set_specialization", new JObject { ["skill"] = "Spellcasting", ["specialization"] = "Combat Spells" }, token).ConfigureAwait(false);
            objResult = (JObject)await CallOnAsync(strMage, "add_initiation", new JObject(), token).ConfigureAwait(false);
            StringAssert.Contains(objResult.Value<string>("result"), "grade is now 1");
            // SR5 metamagics need their art, and the arts are in Street Grimoire
            (blnIsError, strText) = await TryCallOnAsync(strMage, "add_metamagic", new JObject { ["id"] = "Centering" }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            StringAssert.Contains(strText, "requirements");
            (blnIsError, strText) = await TryCallOnAsync(strMage, "add_art", new JObject { ["id"] = "Centering" }, token).ConfigureAwait(false);
            bool blnHasArts = !blnIsError;
            if (blnHasArts)
                await CallOnAsync(strMage, "add_metamagic", new JObject { ["id"] = "Centering" }, token).ConfigureAwait(false);
            else
                StringAssert.Contains(strText, "isn't enabled");
            // A bonded focus counts toward the build; too many or too strong foci are refused
            objResult = (JObject)await CallOnAsync(strMage, "add_gear", new JObject { ["id"] = "Power Focus", ["rating"] = 1 }, token).ConfigureAwait(false);
            string strFocus = objResult.Value<string>("result").Split(new[] { "(id " }, StringSplitOptions.None)[1].Split(',')[0];
            int intKarmaBeforeBonding = objResult.Value<int>("karmaRemaining");
            objResult = (JObject)await CallOnAsync(strMage, "bond_focus", new JObject { ["id"] = strFocus }, token).ConfigureAwait(false);
            Assert.IsTrue(objResult.Value<int>("karmaRemaining") < intKarmaBeforeBonding, objResult.ToString());
            (blnIsError, strText) = await TryCallOnAsync(strMage, "bond_focus", new JObject { ["id"] = strFocus }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, "Bonding twice must be refused: " + strText);
            JArray objMageItems = (JArray)await CallOnAsync(strMage, "list_items", new JObject { ["kind"] = "gear" }, token).ConfigureAwait(false);
            Assert.IsTrue(objMageItems.Any(x => x.Value<string>("id") == strFocus && x.Value<bool?>("bonded") == true), objMageItems.ToString());
            objResult = (JObject)await CallOnAsync(strMage, "bond_focus", new JObject { ["id"] = strFocus, ["bonded"] = false }, token).ConfigureAwait(false);
            Assert.AreEqual(intKarmaBeforeBonding, objResult.Value<int>("karmaRemaining"), objResult.ToString());
            await CallOnAsync(strMage, "remove_item", new JObject { ["id"] = strFocus }, token).ConfigureAwait(false);

            JObject objSummary = (JObject)await CallOnAsync(strMage, "get_character_summary", new JObject(), token).ConfigureAwait(false);
            JToken objSpirit = ((JArray)objSummary["spirits"]).Single();
            Assert.AreEqual("Spirit of Air", objSpirit.Value<string>("type"));
            Assert.AreEqual(2, objSpirit.Value<int>("force"));
            Assert.AreEqual(2, objSpirit.Value<int>("services"));
            Assert.AreEqual(1, ((JArray)objSummary["magic"]["mentorSpirits"]).Count, objSummary["magic"].ToString());
            Assert.AreEqual(1, objSummary["magic"].Value<int>("initiateGrade"));
            Assert.AreEqual(blnHasArts ? 1 : 0, ((JArray)objSummary["magic"]["metamagics"]).Count, objSummary["magic"].ToString());
            Assert.IsTrue(((JArray)objSummary["skills"]).Any(x => x.Value<string>("name") == "Spellcasting"
                                                                   && x["specializations"].Any(y => y.ToString() == "Combat Spells")),
                          objSummary["skills"].ToString());
            await CallOnAsync(strMage, "remove_item", new JObject { ["id"] = objSpirit.Value<string>("id") }, token).ConfigureAwait(false);
            if (blnHasArts)
                await CallOnAsync(strMage, "remove_item", new JObject { ["id"] = ((JArray)objSummary["magic"]["metamagics"]).Single().Value<string>("id") }, token)
                    .ConfigureAwait(false);
            objSummary = (JObject)await CallOnAsync(strMage, "get_character_summary", new JObject(), token).ConfigureAwait(false);
            Assert.AreEqual(0, ((JArray)objSummary["spirits"]).Count);
            Assert.AreEqual(0, ((JArray)objSummary["magic"]["metamagics"]).Count);

            // A technomancer: stream, complex form, sprite
            const string strTechno = "Agent Test Techno";
            await CallToolOkAsync("create_character", new JObject
            {
                ["metatype"] = "Human",
                ["priorities"] = new JObject { ["heritage"] = "D", ["attributes"] = "C", ["talent"] = "A", ["skills"] = "B", ["resources"] = "E" },
                ["talent"] = "Technomancer",
                ["talent_skills"] = new JArray("Compiling", "Registering", "Decompiling"),
                ["name"] = strTechno
            }, token).ConfigureAwait(false);
            await CallOnAsync(strTechno, "set_tradition", new JObject { ["id"] = "Default" }, token).ConfigureAwait(false);
            await CallOnAsync(strTechno, "add_complex_form", new JObject { ["id"] = "Cleaner" }, token).ConfigureAwait(false);
            await CallOnAsync(strTechno, "add_spirit", new JObject { ["kind"] = "sprite", ["type"] = "Crack Sprite", ["force"] = 2 }, token).ConfigureAwait(false);
            objSummary = (JObject)await CallOnAsync(strTechno, "get_character_summary", new JObject(), token).ConfigureAwait(false);
            Assert.AreEqual(1, ((JArray)objSummary["complexForms"]).Count);
            Assert.AreEqual("Sprite", ((JArray)objSummary["spirits"]).Single().Value<string>("kind"));

            // A mundane: exotic skill, martial arts, weapon accessory, armor mod, vehicle mod
            const string strSam = "Agent Test Sam";
            await CallToolOkAsync("create_character", new JObject
            {
                ["metatype"] = "Human",
                ["priorities"] = new JObject { ["heritage"] = "D", ["attributes"] = "B", ["talent"] = "E", ["skills"] = "A", ["resources"] = "C" },
                ["talent"] = "Mundane",
                ["name"] = strSam
            }, token).ConfigureAwait(false);
            objResult = (JObject)await CallOnAsync(strSam, "add_exotic_skill", new JObject { ["skill"] = "Exotic Ranged Weapon", ["specific"] = "Blowgun" }, token)
                .ConfigureAwait(false);
            StringAssert.Contains(objResult.Value<string>("result"), "rating 1");
            (blnIsError, strText) = await TryCallOnAsync(strSam, "add_exotic_skill", new JObject { ["skill"] = "Exotic Ranged Weapon", ["specific"] = "Blowgun" }, token)
                .ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);

            await CallOnAsync(strSam, "add_weapon", new JObject { ["id"] = "Ares Predator V" }, token).ConfigureAwait(false);
            await CallOnAsync(strSam, "add_armor", new JObject { ["id"] = "Armor Jacket" }, token).ConfigureAwait(false);
            JObject objVehicles = (JObject)await CallToolOkAsync("search_catalog", new JObject { ["kind"] = "vehicle", ["limit"] = 1, ["character"] = strSam }, token)
                .ConfigureAwait(false);
            await CallOnAsync(strSam, "add_vehicle", new JObject { ["id"] = objVehicles["items"][0].Value<string>("id") }, token).ConfigureAwait(false);
            objSummary = (JObject)await CallOnAsync(strSam, "get_character_summary", new JObject(), token).ConfigureAwait(false);
            string strWeapon = ((JArray)objSummary["weapons"]).First(x => x.Value<string>("name").Contains("Predator")).Value<string>("id");
            string strArmor = ((JArray)objSummary["armor"]).Single().Value<string>("id");
            string strVehicle = ((JArray)objSummary["vehicles"]).Single().Value<string>("id");
            (blnIsError, strText) = await TryCallOnAsync(strSam, "add_weapon_accessory", new JObject { ["weapon"] = strWeapon, ["id"] = "Laser Sight", ["mount"] = "Stock" }, token)
                .ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            await CallOnAsync(strSam, "add_weapon_accessory", new JObject { ["weapon"] = strWeapon, ["id"] = "Laser Sight" }, token).ConfigureAwait(false);
            await CallOnAsync(strSam, "add_armor_mod", new JObject { ["armor"] = strArmor, ["id"] = "Fire Resistance", ["rating"] = 2 }, token).ConfigureAwait(false);
            await CallOnAsync(strSam, "add_vehicle_mod", new JObject { ["vehicle"] = strVehicle, ["id"] = "Rigger Interface" }, token).ConfigureAwait(false);
            objSummary = (JObject)await CallOnAsync(strSam, "get_character_summary", new JObject(), token).ConfigureAwait(false);
            JToken objAccessory = ((JArray)objSummary["weapons"]).First(x => x.Value<string>("id") == strWeapon)["accessories"]
                .First(x => x.Value<string>("name").Contains("Laser"));
            Assert.AreEqual(1, ((JArray)((JArray)objSummary["armor"]).Single()["mods"]).Count, objSummary["armor"].ToString());
            Assert.IsTrue(((JArray)((JArray)objSummary["vehicles"]).Single()["mods"]).Any(x => x.Value<string>("name").Contains("Rigger")),
                          objSummary["vehicles"].ToString());
            await CallOnAsync(strSam, "remove_item", new JObject { ["id"] = objAccessory.Value<string>("id") }, token).ConfigureAwait(false);
            objSummary = (JObject)await CallOnAsync(strSam, "get_character_summary", new JObject(), token).ConfigureAwait(false);
            Assert.IsFalse(((JArray)objSummary["weapons"]).First(x => x.Value<string>("id") == strWeapon)["accessories"].Any(x => x.Value<string>("name").Contains("Laser")));

            // Martial arts come from Run & Gun, which the Standard settings don't enable
            (blnIsError, strText) = await TryCallOnAsync(strSam, "add_martial_art", new JObject { ["id"] = "Krav Maga" }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            StringAssert.Contains(strText, "isn't enabled");

            // With every book enabled: arts unlock metamagics, and martial arts take techniques
            const string strFullHouse = "Agent Test Full House";
            await CallToolOkAsync("create_character", new JObject
            {
                ["settings"] = "Full House",
                ["metatype"] = "Human",
                ["priorities"] = new JObject { ["heritage"] = "D", ["attributes"] = "C", ["talent"] = "A", ["skills"] = "B", ["resources"] = "E" },
                ["talent"] = "Magician",
                ["talent_skills"] = new JArray("Spellcasting", "Summoning"),
                ["name"] = strFullHouse
            }, token).ConfigureAwait(false);
            await CallOnAsync(strFullHouse, "add_initiation", new JObject(), token).ConfigureAwait(false);
            await CallOnAsync(strFullHouse, "add_art", new JObject { ["id"] = "Centering" }, token).ConfigureAwait(false);
            await CallOnAsync(strFullHouse, "add_metamagic", new JObject { ["id"] = "Centering" }, token).ConfigureAwait(false);
            await CallOnAsync(strFullHouse, "add_martial_art", new JObject { ["id"] = "Krav Maga" }, token).ConfigureAwait(false);
            (blnIsError, strText) = await TryCallOnAsync(strFullHouse, "add_technique", new JObject { ["martial_art"] = "Krav Maga", ["id"] = "Not A Technique" }, token)
                .ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            (blnIsError, strText) = await TryCallOnAsync(strFullHouse, "add_technique", new JObject { ["martial_art"] = "Krav Maga", ["id"] = "Kick Attack" }, token)
                .ConfigureAwait(false);
            if (blnIsError)
                await CallOnAsync(strFullHouse, "add_technique", new JObject { ["martial_art"] = "Krav Maga", ["id"] = FirstOption(strText) }, token).ConfigureAwait(false);
            objSummary = (JObject)await CallOnAsync(strFullHouse, "get_character_summary", new JObject(), token).ConfigureAwait(false);
            Assert.AreEqual(1, ((JArray)objSummary["magic"]["arts"]).Count, objSummary["magic"].ToString());
            Assert.AreEqual(1, ((JArray)objSummary["magic"]["metamagics"]).Count, objSummary["magic"].ToString());
            Assert.AreEqual(1, ((JArray)((JArray)objSummary["martialArts"]).Single()["techniques"]).Count, objSummary["martialArts"].ToString());
        }

        [TestMethod]
        public void ItemDetailsStateAndValues()
        {
            RunWithMainForm((frmMain, t) => RunItemDetailsStateAndValuesScenarioAsync(t));
        }

        /// <summary>
        /// Every item a character has, from list_items, flattened.
        /// </summary>
        private static async Task<List<JToken>> ListAllItemsAsync(string strCharacter, CancellationToken token)
        {
            JArray objItems = (JArray)await CallOnAsync(strCharacter, "list_items", new JObject(), token).ConfigureAwait(false);
            List<JToken> lstReturn = new List<JToken>();
            Stack<JToken> stkItems = new Stack<JToken>(objItems);
            while (stkItems.Count > 0)
            {
                JToken objItem = stkItems.Pop();
                lstReturn.Add(objItem);
                if (objItem["contains"] is JArray objChildren)
                    foreach (JToken objChild in objChildren)
                        stkItems.Push(objChild);
            }

            return lstReturn;
        }

        private static async Task RunItemDetailsStateAndValuesScenarioAsync(CancellationToken token)
        {
            const string strRunner = "Agent Test Runner";
            await CallToolOkAsync("create_character", new JObject
            {
                ["metatype"] = "Human",
                ["priorities"] = new JObject { ["heritage"] = "D", ["attributes"] = "B", ["talent"] = "E", ["skills"] = "C", ["resources"] = "A" },
                ["talent"] = "Mundane",
                ["name"] = strRunner
            }, token).ConfigureAwait(false);
            await CallOnAsync(strRunner, "add_gear", new JObject { ["id"] = "Hermes Ikon" }, token).ConfigureAwait(false);
            await CallOnAsync(strRunner, "add_gear", new JObject { ["id"] = "Erika MCD-1" }, token).ConfigureAwait(false);
            await CallOnAsync(strRunner, "add_gear", new JObject { ["id"] = "Medkit", ["rating"] = 2 }, token).ConfigureAwait(false);
            await CallOnAsync(strRunner, "add_weapon", new JObject { ["id"] = "Ares Predator V" }, token).ConfigureAwait(false);
            await CallOnAsync(strRunner, "add_armor", new JObject { ["id"] = "Armor Jacket" }, token).ConfigureAwait(false);
            await CallOnAsync(strRunner, "add_cyberware", new JObject { ["id"] = "Datajack" }, token).ConfigureAwait(false);
            await CallOnAsync(strRunner, "add_lifestyle", new JObject { ["id"] = "Low" }, token).ConfigureAwait(false);
            List<JToken> lstItems = await ListAllItemsAsync(strRunner, token).ConfigureAwait(false);
            string IdOf(string strName) => lstItems.First(x => x.Value<string>("name").StartsWith(strName, StringComparison.Ordinal)).Value<string>("id");
            string strPredator = IdOf("Ares Predator V");
            string strMedkit = IdOf("Medkit");
            string strJacket = IdOf("Armor Jacket");
            string strDatajack = IdOf("Datajack");
            string strLifestyle = lstItems.First(x => x.Value<string>("kind") == "lifestyle").Value<string>("id");
            string strDeck = IdOf("Erika MCD-1");
            string strIkon = IdOf("Hermes Ikon");
            // The pistol's built-in smartgun shows up as something fitted to it
            Assert.IsTrue(lstItems.First(x => x.Value<string>("id") == strPredator)["contains"] is JArray, "list_items must show what is fitted to a weapon");

            // Details: notes, custom names, locations
            await CallOnAsync(strRunner, "set_item_details", new JObject { ["id"] = strPredator, ["custom_name"] = "Old Faithful", ["notes"] = "A gift.\nKeep it clean." }, token)
                .ConfigureAwait(false);
            await CallOnAsync(strRunner, "manage_location", new JObject { ["action"] = "add", ["name"] = "Go Bag" }, token).ConfigureAwait(false);
            await CallOnAsync(strRunner, "set_item_details", new JObject { ["id"] = strMedkit, ["location"] = "Go Bag" }, token).ConfigureAwait(false);
            (bool blnIsError, string strText) = await TryCallOnAsync(strRunner, "set_item_details", new JObject { ["id"] = strMedkit, ["location"] = "Nowhere" }, token)
                .ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "set_item_details", new JObject { ["id"] = strDatajack, ["custom_name"] = "Jack" }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, "Cyberware has no custom name: " + strText);
            lstItems = await ListAllItemsAsync(strRunner, token).ConfigureAwait(false);
            JToken objPredator = lstItems.First(x => x.Value<string>("id") == strPredator);
            Assert.AreEqual("Old Faithful", objPredator.Value<string>("customName"), objPredator.ToString());
            StringAssert.Contains(objPredator.Value<string>("notes"), "Keep it clean.");
            Assert.AreEqual("Go Bag", lstItems.First(x => x.Value<string>("id") == strMedkit).Value<string>("location"));
            await CallOnAsync(strRunner, "manage_location", new JObject { ["action"] = "rename", ["name"] = "Go Bag", ["new_name"] = "Bug-Out Bag" }, token).ConfigureAwait(false);
            lstItems = await ListAllItemsAsync(strRunner, token).ConfigureAwait(false);
            Assert.AreEqual("Bug-Out Bag", lstItems.First(x => x.Value<string>("id") == strMedkit).Value<string>("location"));
            await CallOnAsync(strRunner, "manage_location", new JObject { ["action"] = "remove", ["name"] = "Bug-Out Bag" }, token).ConfigureAwait(false);
            lstItems = await ListAllItemsAsync(strRunner, token).ConfigureAwait(false);
            Assert.IsNull(lstItems.First(x => x.Value<string>("id") == strMedkit).Value<string>("location"));

            // State: equipping armor changes the armor value; the active commlink can move
            int intArmor = int.Parse(((JObject)await CallOnAsync(strRunner, "export_character", new JObject { ["section"] = "armor" }, token).ConfigureAwait(false)).Value<string>("armor"));
            await CallOnAsync(strRunner, "set_item_state", new JObject { ["id"] = strJacket, ["equipped"] = false }, token).ConfigureAwait(false);
            int intUnarmored = int.Parse(((JObject)await CallOnAsync(strRunner, "export_character", new JObject { ["section"] = "armor" }, token).ConfigureAwait(false)).Value<string>("armor"));
            Assert.IsTrue(intUnarmored < intArmor, intArmor + " -> " + intUnarmored);
            await CallOnAsync(strRunner, "set_item_state", new JObject { ["id"] = strJacket, ["equipped"] = true }, token).ConfigureAwait(false);
            await CallOnAsync(strRunner, "set_item_state", new JObject { ["id"] = strIkon, ["active_commlink"] = true }, token).ConfigureAwait(false);
            lstItems = await ListAllItemsAsync(strRunner, token).ConfigureAwait(false);
            Assert.IsTrue(lstItems.First(x => x.Value<string>("id") == strIkon).Value<bool?>("activeCommlink") == true);
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "set_item_state", new JObject { ["id"] = strMedkit, ["active_commlink"] = true }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "set_item_state", new JObject { ["id"] = strMedkit, ["black_market_discount"] = true }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);

            // Values: ratings within limits, quantities, grades and months all change the budget
            JObject objResult = (JObject)await CallOnAsync(strRunner, "set_item_values", new JObject { ["id"] = strMedkit, ["rating"] = 4 }, token).ConfigureAwait(false);
            decimal decNuyen = objResult.Value<decimal>("nuyenRemaining");
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "set_item_values", new JObject { ["id"] = strMedkit, ["rating"] = 40 }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            objResult = (JObject)await CallOnAsync(strRunner, "set_item_values", new JObject { ["id"] = strMedkit, ["quantity"] = 2 }, token).ConfigureAwait(false);
            Assert.IsTrue(objResult.Value<decimal>("nuyenRemaining") < decNuyen, objResult.ToString());
            decimal decEssence = objResult.Value<decimal>("essence");
            objResult = (JObject)await CallOnAsync(strRunner, "set_item_values", new JObject { ["id"] = strDatajack, ["grade"] = "Alphaware" }, token).ConfigureAwait(false);
            Assert.IsTrue(objResult.Value<decimal>("essence") > decEssence, objResult.ToString());
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "set_item_values", new JObject { ["id"] = strDatajack, ["grade"] = "Godware" }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            StringAssert.Contains(strText, "Options:");
            decNuyen = objResult.Value<decimal>("nuyenRemaining");
            objResult = (JObject)await CallOnAsync(strRunner, "set_item_values", new JObject { ["id"] = strLifestyle, ["months"] = 3 }, token).ConfigureAwait(false);
            Assert.IsTrue(objResult.Value<decimal>("nuyenRemaining") < decNuyen, objResult.ToString());
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "set_item_values", new JObject { ["id"] = strJacket, ["rating"] = 2 }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);

            // A cyberdeck's attributes can be rearranged
            objResult = (JObject)await CallOnAsync(strRunner, "swap_matrix_attributes", new JObject { ["id"] = strDeck, ["first"] = "Attack", ["second"] = "Firewall" }, token)
                .ConfigureAwait(false);
            StringAssert.Contains(objResult.Value<string>("result"), "Swapped Attack and Firewall");
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "swap_matrix_attributes", new JObject { ["id"] = strIkon, ["first"] = "Attack", ["second"] = "Firewall" }, token)
                .ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);

            // Gear inside other things: a program in a commlink, gear in armor, a commlink in headware, gear in a vehicle
            await CallOnAsync(strRunner, "add_gear", new JObject { ["id"] = "Browse", ["parent"] = strIkon }, token).ConfigureAwait(false);
            await CallOnAsync(strRunner, "add_gear", new JObject { ["id"] = "Micro-Transceiver", ["parent"] = strJacket }, token).ConfigureAwait(false);
            await CallOnAsync(strRunner, "add_cyberware", new JObject { ["id"] = "Commlink" }, token).ConfigureAwait(false);
            lstItems = await ListAllItemsAsync(strRunner, token).ConfigureAwait(false);
            string strHeadware = lstItems.First(x => x.Value<string>("kind") == "cyberware" && x.Value<string>("name").StartsWith("Commlink", StringComparison.Ordinal))
                                         .Value<string>("id");
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "add_gear", new JObject { ["id"] = "Medkit", ["parent"] = strHeadware }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            StringAssert.Contains(strText, "Commlinks");
            await CallOnAsync(strRunner, "add_gear", new JObject { ["id"] = "Meta Link", ["parent"] = strHeadware }, token).ConfigureAwait(false);
            JObject objVehicles = (JObject)await CallToolOkAsync("search_catalog", new JObject { ["kind"] = "vehicle", ["limit"] = 1, ["character"] = strRunner }, token)
                .ConfigureAwait(false);
            await CallOnAsync(strRunner, "add_vehicle", new JObject { ["id"] = objVehicles["items"][0].Value<string>("id") }, token).ConfigureAwait(false);
            lstItems = await ListAllItemsAsync(strRunner, token).ConfigureAwait(false);
            string strCar = lstItems.First(x => x.Value<string>("kind") == "vehicle").Value<string>("id");
            await CallOnAsync(strRunner, "add_gear", new JObject { ["id"] = "Medkit", ["parent"] = strCar }, token).ConfigureAwait(false);
            lstItems = await ListAllItemsAsync(strRunner, token).ConfigureAwait(false);
            bool HasChild(string strParentId, string strChild) => lstItems.First(x => x.Value<string>("id") == strParentId)["contains"] is JArray objChildren
                                                                  && objChildren.Any(x => x.Value<string>("name").StartsWith(strChild, StringComparison.Ordinal));
            Assert.IsTrue(HasChild(strIkon, "Browse"), "Browse is not in the commlink");
            Assert.IsTrue(HasChild(strJacket, "Micro-Transceiver"), "The transceiver is not in the armor");
            Assert.IsTrue(HasChild(strHeadware, "Meta Link"), "The commlink is not in the headware");
            Assert.IsTrue(HasChild(strCar, "Medkit"), "The medkit is not in the vehicle");

            // Weapons inside other things: an underbarrel weapon, a drone's weapon mount, and an accessory on the mounted weapon
            await CallOnAsync(strRunner, "add_weapon", new JObject { ["id"] = "AK-97" }, token).ConfigureAwait(false);
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "add_weapon", new JObject { ["id"] = "Underbarrel Grapple Gun" }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, "An underbarrel weapon can't be bought on its own: " + strText);
            lstItems = await ListAllItemsAsync(strRunner, token).ConfigureAwait(false);
            string strRifle = lstItems.First(x => x.Value<string>("kind") == "weapon" && x.Value<string>("name").StartsWith("AK-97", StringComparison.Ordinal)).Value<string>("id");
            await CallOnAsync(strRunner, "add_weapon", new JObject { ["id"] = "Underbarrel Grapple Gun", ["parent"] = strRifle }, token).ConfigureAwait(false);
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "add_weapon", new JObject { ["id"] = "Ares Predator V", ["parent"] = strRifle }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, "Only underbarrel weapons go under a weapon: " + strText);
            await CallOnAsync(strRunner, "add_vehicle", new JObject { ["id"] = "GM-Nissan Doberman (Medium)" }, token).ConfigureAwait(false);
            lstItems = await ListAllItemsAsync(strRunner, token).ConfigureAwait(false);
            string strMount = lstItems.First(x => x.Value<string>("kind") == "weapon mount").Value<string>("id");
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "add_weapon", new JObject { ["id"] = "Combat Knife", ["parent"] = strMount }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            StringAssert.Contains(strText, "Assault Rifles");
            objResult = (JObject)await CallOnAsync(strRunner, "add_weapon", new JObject { ["id"] = "AK-97", ["parent"] = strMount }, token).ConfigureAwait(false);
            string strMountedRifle = objResult.Value<string>("result").Split(new[] { "(id " }, StringSplitOptions.None)[1].TrimEnd(')', '.');
            await CallOnAsync(strRunner, "add_weapon_accessory", new JObject { ["weapon"] = strMountedRifle, ["id"] = "Laser Sight" }, token).ConfigureAwait(false);
            lstItems = await ListAllItemsAsync(strRunner, token).ConfigureAwait(false);
            Assert.IsTrue(HasChild(strRifle, "Underbarrel Grapple Gun"), "The grapple gun is not under the rifle");
            Assert.IsTrue(HasChild(strMount, "AK-97"), "The rifle is not in the mount");
            Assert.IsTrue(HasChild(strMountedRifle, "Laser Sight"), "The laser sight is not on the mounted rifle");

            // Primary arm and portraits
            await CallOnAsync(strRunner, "set_character_details", new JObject { ["primary_arm"] = "Left" }, token).ConfigureAwait(false);
            Assert.AreEqual("Left", ((JObject)await CallOnAsync(strRunner, "export_character", new JObject { ["section"] = "primaryarm" }, token).ConfigureAwait(false))
                                    .Value<string>("primaryarm"));
            string strImage = Path.Combine(Path.GetTempPath(), "AgentToolsTest_" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                using (System.Drawing.Bitmap bmpTest = new System.Drawing.Bitmap(4, 4))
                    bmpTest.Save(strImage, System.Drawing.Imaging.ImageFormat.Png);
                await CallOnAsync(strRunner, "manage_portrait", new JObject { ["action"] = "add", ["path"] = strImage }, token).ConfigureAwait(false);
                objResult = (JObject)await CallOnAsync(strRunner, "manage_portrait", new JObject { ["action"] = "add", ["path"] = strImage }, token).ConfigureAwait(false);
                Assert.AreEqual(2, objResult.Value<int>("portraits"), objResult.ToString());
                await CallOnAsync(strRunner, "manage_portrait", new JObject { ["action"] = "set_main", ["index"] = 2 }, token).ConfigureAwait(false);
                objResult = (JObject)await CallOnAsync(strRunner, "manage_portrait", new JObject { ["action"] = "remove", ["index"] = 1 }, token).ConfigureAwait(false);
                Assert.AreEqual(1, objResult.Value<int>("portraits"), objResult.ToString());
                (blnIsError, strText) = await TryCallOnAsync(strRunner, "manage_portrait", new JObject { ["action"] = "remove", ["index"] = 5 }, token).ConfigureAwait(false);
                Assert.IsTrue(blnIsError, strText);
            }
            finally
            {
                // Chummer keeps the image file open while the portrait is loaded, as it does in the editor
                try
                {
                    if (File.Exists(strImage))
                        File.Delete(strImage);
                }
                catch (IOException)
                {
                    // Left for the temp folder cleanup
                }
            }

            // Moving to other settings with the same build method opens up more books
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "add_martial_art", new JObject { ["id"] = "Krav Maga" }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            await CallOnAsync(strRunner, "set_build_settings", new JObject { ["settings"] = "Full House" }, token).ConfigureAwait(false);
            await CallOnAsync(strRunner, "add_martial_art", new JObject { ["id"] = "Krav Maga" }, token).ConfigureAwait(false);
            JArray objAllSettings = (JArray)await CallToolOkAsync("list_build_settings", null, token).ConfigureAwait(false);
            JToken objKarmaSettings = objAllSettings.FirstOrDefault(x => x.Value<string>("buildMethod") == "Karma");
            if (objKarmaSettings != null)
            {
                (blnIsError, strText) = await TryCallOnAsync(strRunner, "set_build_settings", new JObject { ["settings"] = objKarmaSettings.Value<string>("key") }, token)
                    .ConfigureAwait(false);
                Assert.IsTrue(blnIsError, "Switching build methods needs convert_build: " + strText);
                StringAssert.Contains(strText, "convert_build");
            }

            // Vehicle weapons have a firing mode; modular cyberware moves between mounts (Chrome Flesh, enabled by Full House)
            await CallOnAsync(strRunner, "set_item_state", new JObject { ["id"] = strMountedRifle, ["fire_mode"] = "RemoteOperated" }, token).ConfigureAwait(false);
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "set_item_state", new JObject { ["id"] = strRifle, ["fire_mode"] = "RemoteOperated" }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, "Only vehicle weapons have a firing mode: " + strText);
            objResult = (JObject)await CallOnAsync(strRunner, "add_cyberware", new JObject { ["id"] = "Modular Connector, Wrist", ["forced_value"] = "Right" }, token)
                .ConfigureAwait(false);
            string strConnector = objResult.Value<string>("result").Split(new[] { "(id " }, StringSplitOptions.None)[1].Split(')')[0];
            objResult = (JObject)await CallOnAsync(strRunner, "add_cyberware", new JObject { ["id"] = "Obvious Hand, Modular", ["forced_value"] = "Right" }, token)
                .ConfigureAwait(false);
            string strHand = objResult.Value<string>("result").Split(new[] { "(id " }, StringSplitOptions.None)[1].Split(')')[0];
            objResult = (JObject)await CallOnAsync(strRunner, "move_modular_cyberware", new JObject { ["id"] = strHand }, token).ConfigureAwait(false);
            StringAssert.Contains(objResult.Value<string>("result"), strConnector);
            await CallOnAsync(strRunner, "move_modular_cyberware", new JObject { ["id"] = strHand, ["mount"] = strConnector }, token).ConfigureAwait(false);
            lstItems = await ListAllItemsAsync(strRunner, token).ConfigureAwait(false);
            Assert.IsTrue(HasChild(strConnector, "Obvious Hand"), "The hand is not on the wrist connector");

            // Lifestyles from the Select Lifestyle dialog: a name, qualities, roommates
            objResult = (JObject)await CallOnAsync(strRunner, "add_lifestyle", new JObject
            {
                ["id"] = "Medium", ["name"] = "Safehouse", ["qualities"] = new JArray("DocWagon Contract, Basic")
            }, token).ConfigureAwait(false);
            string strSafehouse = objResult.Value<string>("result").Split(new[] { "(id " }, StringSplitOptions.None)[1].TrimEnd(')', '.');
            decimal decWithContract = objResult.Value<decimal>("nuyenRemaining");
            lstItems = await ListAllItemsAsync(strRunner, token).ConfigureAwait(false);
            JToken objSafehouse = lstItems.First(x => x.Value<string>("id") == strSafehouse);
            StringAssert.Contains(objSafehouse.Value<string>("name"), "Safehouse");
            Assert.IsTrue(HasChild(strSafehouse, "DocWagon"), objSafehouse.ToString());
            objResult = (JObject)await CallOnAsync(strRunner, "edit_lifestyle", new JObject
            {
                ["id"] = strSafehouse, ["remove_qualities"] = new JArray("DocWagon Contract, Basic"), ["roommates"] = 2, ["split_cost"] = true
            }, token).ConfigureAwait(false);
            Assert.IsTrue(objResult.Value<decimal>("nuyenRemaining") > decWithContract, objResult.ToString());
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "edit_lifestyle", new JObject { ["id"] = strSafehouse, ["area"] = 2 }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, "Points only apply to advanced lifestyles: " + strText);

            // Pets come from the Add Pet button and need no ratings
            await CallOnAsync(strRunner, "add_contact", new JObject { ["kind"] = "pet", ["name"] = "Rex" }, token).ConfigureAwait(false);
            JObject objWithPet = (JObject)await CallOnAsync(strRunner, "get_character_summary", new JObject(), token).ConfigureAwait(false);
            Assert.IsTrue(((JArray)objWithPet["contacts"]).Any(x => x.Value<string>("name") == "Rex" && x.Value<string>("type") == "Pet"), objWithPet["contacts"].ToString());

            // Nested items can be removed directly
            string strLaser = ((JArray)lstItems.First(x => x.Value<string>("id") == strMountedRifle)["contains"])
                              .First(x => x.Value<string>("name").StartsWith("Laser Sight", StringComparison.Ordinal)).Value<string>("id");
            await CallOnAsync(strRunner, "remove_item", new JObject { ["id"] = strLaser }, token).ConfigureAwait(false);
            lstItems = await ListAllItemsAsync(strRunner, token).ConfigureAwait(false);
            Assert.IsFalse(HasChild(strMountedRifle, "Laser Sight"), "The laser sight was not removed");

            // Skills: notes, a knowledge skill's name and type, native languages, and removing knowledge skills
            await CallOnAsync(strRunner, "add_knowledge_skill", new JObject { ["name"] = "Seattle Gangs", ["type"] = "Street", ["base"] = 1 }, token).ConfigureAwait(false);
            await CallOnAsync(strRunner, "set_skill_details", new JObject { ["skill"] = "Seattle Gangs", ["name"] = "Redmond Gangs", ["type"] = "Academic" }, token)
                .ConfigureAwait(false);
            await CallOnAsync(strRunner, "set_skill_details", new JObject { ["skill"] = "Pistols", ["notes"] = "Learned from dad." }, token).ConfigureAwait(false);
            await CallOnAsync(strRunner, "add_knowledge_skill", new JObject { ["name"] = "Japanese", ["type"] = "Language", ["base"] = 1 }, token).ConfigureAwait(false);
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "set_skill_details", new JObject { ["skill"] = "Redmond Gangs", ["native_language"] = true }, token)
                .ConfigureAwait(false);
            Assert.IsTrue(blnIsError, "Only languages can be native: " + strText);
            JObject objSkills = (JObject)await CallOnAsync(strRunner, "get_character_summary", new JObject(), token).ConfigureAwait(false);
            JToken objGangs = ((JArray)objSkills["knowledgeSkills"]).FirstOrDefault(x => x.Value<string>("name") == "Redmond Gangs");
            Assert.IsNotNull(objGangs, objSkills["knowledgeSkills"].ToString());
            Assert.AreEqual("LOG", objGangs.Value<string>("attribute"), "Academic knowledge uses Logic");
            (blnIsError, strText) = await TryCallOnAsync(strRunner, "remove_item", new JObject { ["id"] = "Pistols" }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, "Active skills can't be removed: " + strText);
            await CallOnAsync(strRunner, "remove_item", new JObject { ["id"] = "Redmond Gangs" }, token).ConfigureAwait(false);
            objSkills = (JObject)await CallOnAsync(strRunner, "get_character_summary", new JObject(), token).ConfigureAwait(false);
            Assert.IsFalse(((JArray)objSkills["knowledgeSkills"]).Any(x => x.Value<string>("name") == "Redmond Gangs"), "The knowledge skill was not removed");
        }

        [TestMethod]
        public void CareerMode()
        {
            string strSaveFile = Path.Combine(Path.GetTempPath(), "AgentToolsTest_" + Guid.NewGuid().ToString("N") + ".chum5");
            try
            {
                RunWithMainForm((frmMain, t) => RunCareerModeScenarioAsync(frmMain, strSaveFile, t));
            }
            finally
            {
                if (File.Exists(strSaveFile))
                    File.Delete(strSaveFile);
            }
        }

        private static async Task RunCareerModeScenarioAsync(ChummerMainForm frmMain, string strSaveFile, CancellationToken token)
        {
            const string strVet = "Agent Test Veteran";
            await CallToolOkAsync("create_character", new JObject
            {
                ["metatype"] = "Human",
                ["priorities"] = new JObject { ["heritage"] = "D", ["attributes"] = "B", ["talent"] = "E", ["skills"] = "A", ["resources"] = "C" },
                ["talent"] = "Mundane",
                ["name"] = strVet
            }, token).ConfigureAwait(false);
            (bool blnIsError, string strText) = await TryCallOnAsync(strVet, "log_karma", new JObject { ["amount"] = 5, ["reason"] = "Too early" }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, "Career tools need a created character: " + strText);
            await CallOnAsync(strVet, "set_skill", new JObject { ["name"] = "Pistols", ["base"] = 3 }, token).ConfigureAwait(false);

            // Finishing creation reopens the character in career mode
            JObject objResult = (JObject)await CallOnAsync(strVet, "finish_creation", new JObject { ["path"] = strSaveFile }, token).ConfigureAwait(false);
            StringAssert.Contains(objResult.Value<string>("result"), "career");
            JArray objOpen = (JArray)await CallToolOkAsync("list_open_characters", null, token).ConfigureAwait(false);
            Assert.IsTrue(objOpen.Any(x => x.Value<string>("name") == strVet && x.Value<string>("mode") == "career"), objOpen.ToString());

            // Karma and nuyen go through the expense log
            objResult = (JObject)await CallOnAsync(strVet, "log_karma", new JObject { ["amount"] = 40, ["reason"] = "Run: the docks job" }, token).ConfigureAwait(false);
            int intKarma = objResult.Value<int>("karma");
            Assert.IsTrue(intKarma >= 40, objResult.ToString());
            objResult = (JObject)await CallOnAsync(strVet, "log_nuyen", new JObject { ["amount"] = 5000, ["reason"] = "Run pay" }, token).ConfigureAwait(false);
            decimal decNuyen = objResult.Value<decimal>("nuyen");
            objResult = (JObject)await CallOnAsync(strVet, "log_nuyen", new JObject { ["amount"] = -1000, ["reason"] = "Bribe" }, token).ConfigureAwait(false);
            Assert.AreEqual(decNuyen - 1000, objResult.Value<decimal>("nuyen"), objResult.ToString());
            (blnIsError, strText) = await TryCallOnAsync(strVet, "log_nuyen", new JObject { ["amount"] = -100000000, ["reason"] = "A yacht" }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);

            // Attributes, skills, knowledge skills and specializations cost karma
            objResult = (JObject)await CallOnAsync(strVet, "improve_attribute", new JObject { ["name"] = "BOD" }, token).ConfigureAwait(false);
            Assert.IsTrue(objResult.Value<int>("karma") < intKarma, objResult.ToString());
            intKarma = objResult.Value<int>("karma");
            objResult = (JObject)await CallOnAsync(strVet, "improve_skill", new JObject { ["name"] = "Pistols" }, token).ConfigureAwait(false);
            StringAssert.Contains(objResult.Value<string>("result"), "is now 4");
            Assert.IsTrue(objResult.Value<int>("karma") < intKarma, objResult.ToString());
            await CallOnAsync(strVet, "add_knowledge_skill", new JObject { ["name"] = "Seattle Gangs", ["type"] = "Street" }, token).ConfigureAwait(false);
            objResult = (JObject)await CallOnAsync(strVet, "improve_skill", new JObject { ["name"] = "Seattle Gangs" }, token).ConfigureAwait(false);
            StringAssert.Contains(objResult.Value<string>("result"), "is now 1");
            intKarma = objResult.Value<int>("karma");
            objResult = (JObject)await CallOnAsync(strVet, "set_specialization", new JObject { ["skill"] = "Pistols", ["specialization"] = "Semi-Automatics" }, token)
                .ConfigureAwait(false);
            Assert.IsTrue(objResult.Value<int>("karma") < intKarma, objResult.ToString());
            (blnIsError, strText) = await TryCallOnAsync(strVet, "improve_skill", new JObject { ["name"] = "Pistols", ["times"] = 50 }, token).ConfigureAwait(false);
            Assert.IsFalse(blnIsError, "Raising as far as karma allows is fine: " + strText);
            (blnIsError, strText) = await TryCallOnAsync(strVet, "set_attribute", new JObject { ["name"] = "BOD", ["karma"] = 1 }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            StringAssert.Contains(strText, "improve_attribute");

            // Burning Edge lowers it for good
            JObject objSummary = (JObject)await CallOnAsync(strVet, "get_character_summary", new JObject(), token).ConfigureAwait(false);
            int intEdge = ((JArray)objSummary["specialAttributes"]).First(x => x.Value<string>("name") == "EDG").Value<int>("value");
            objResult = (JObject)await CallOnAsync(strVet, "burn_edge", new JObject(), token).ConfigureAwait(false);
            StringAssert.Contains(objResult.Value<string>("result"), "Edge is now " + (intEdge - 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void AdeptExtrasOpenAndExport()
        {
            string strFolder = Path.Combine(Path.GetTempPath(), "AgentToolsTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                RunWithMainForm((frmMain, t) => RunAdeptExtrasOpenAndExportScenarioAsync(strFolder, t));
            }
            finally
            {
                if (Directory.Exists(strFolder))
                    Directory.Delete(strFolder, true);
            }
        }

        private static async Task RunAdeptExtrasOpenAndExportScenarioAsync(string strFolder, CancellationToken token)
        {
            const string strAdept = "Agent Test Adept";
            await CallToolOkAsync("create_character", new JObject
            {
                ["metatype"] = "Human",
                ["priorities"] = new JObject { ["heritage"] = "E", ["attributes"] = "A", ["talent"] = "B", ["skills"] = "C", ["resources"] = "D" },
                ["talent"] = "Adept",
                ["talent_skills"] = new JArray("Pistols"),
                ["name"] = strAdept
            }, token).ConfigureAwait(false);

            // Choices inside items: a power's skill, a license's subject. (Ammunition's weapon category works live, but its dialog
            // still opens for a moment, which the hidden test window can't do.)
            await CallOnAsync(strAdept, "add_power", new JObject { ["id"] = "Improved Ability (skill)", ["rating"] = 1, ["forced_value"] = "Pistols" }, token)
                .ConfigureAwait(false);
            JObject objResult = (JObject)await CallOnAsync(strAdept, "add_gear", new JObject { ["id"] = "Fake License", ["rating"] = 2, ["forced_value"] = "Concealed Carry" }, token)
                .ConfigureAwait(false);
            StringAssert.Contains(objResult.Value<string>("result"), "Concealed Carry");

            // Chummer wants adepts to have a tradition too
            await CallOnAsync(strAdept, "set_tradition", new JObject { ["id"] = "Shamanic" }, token).ConfigureAwait(false);
            JObject objValidation = (JObject)await CallOnAsync(strAdept, "validate_character", new JObject(), token).ConfigureAwait(false);
            Assert.IsFalse(((JArray)objValidation["errors"]).Any(x => x.ToString().Contains("Tradition")), objValidation.ToString());

            // Knowledge skill types set the linked attribute
            await CallOnAsync(strAdept, "add_knowledge_skill", new JObject { ["name"] = "Street Gangs", ["type"] = "Street", ["base"] = 2 }, token).ConfigureAwait(false);
            JObject objSummary = (JObject)await CallOnAsync(strAdept, "get_character_summary", new JObject(), token).ConfigureAwait(false);
            Assert.AreEqual("INT", ((JArray)objSummary["knowledgeSkills"]).First(x => x.Value<string>("name") == "Street Gangs").Value<string>("attribute"));
            Assert.IsTrue(((JArray)objSummary["powers"]).Any(x => x.Value<string>("name").Contains("Pistols")), objSummary["powers"].ToString());

            // A specialization paid with karma leaves the skill points alone
            int intKarma = objSummary["karma"].Value<int>("remaining");
            int intSkillPoints = objSummary["points"]["skillPoints"].Value<int>("remaining");
            objResult = (JObject)await CallOnAsync(strAdept, "set_specialization", new JObject
            {
                ["skill"] = "Pistols", ["specialization"] = "Semi-Automatics", ["with_karma"] = true
            }, token).ConfigureAwait(false);
            Assert.AreEqual(intSkillPoints, objResult.Value<int>("skillPointsRemaining"), objResult.ToString());
            Assert.IsTrue(objResult.Value<int>("karmaRemaining") < intKarma, objResult.ToString());

            // Karma for nuyen
            decimal decNuyen = objResult.Value<decimal>("nuyenRemaining");
            objResult = (JObject)await CallOnAsync(strAdept, "set_karma_for_nuyen", new JObject { ["karma"] = 2 }, token).ConfigureAwait(false);
            Assert.IsTrue(objResult.Value<decimal>("nuyenRemaining") > decNuyen, objResult.ToString());

            // Character sheets, as HTML and PDF
            string strHtml = Path.Combine(strFolder, "Adept.html");
            await CallOnAsync(strAdept, "export_character_sheet", new JObject { ["format"] = "html", ["path"] = strHtml }, token).ConfigureAwait(false);
            StringAssert.Contains(File.ReadAllText(strHtml), strAdept);
            string strPdf = Path.Combine(strFolder, "Adept.pdf");
            await CallOnAsync(strAdept, "export_character_sheet", new JObject { ["path"] = strPdf }, token).ConfigureAwait(false);
            Assert.IsTrue(new FileInfo(strPdf).Length > 1000, "The PDF sheet is missing or empty");
            (bool blnIsError, string strText) = await TryCallOnAsync(strAdept, "export_character_sheet", new JObject { ["sheet"] = "No Such Sheet" }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            StringAssert.Contains(strText, "Shadowrun 5");

            // Opening a saved character
            string strSkink = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? string.Empty, "TestFiles", "Skink.chum5");
            objResult = (JObject)await CallToolOkAsync("open_character", new JObject { ["path"] = strSkink }, token).ConfigureAwait(false);
            StringAssert.Contains(objResult.Value<string>("result"), "Opened");
            JArray objOpen = (JArray)await CallToolOkAsync("list_open_characters", null, token).ConfigureAwait(false);
            Assert.IsTrue(objOpen.Any(x => x.Value<string>("file") == strSkink), objOpen.ToString());
            objResult = (JObject)await CallToolOkAsync("open_character", new JObject { ["path"] = strSkink }, token).ConfigureAwait(false);
            StringAssert.Contains(objResult.Value<string>("result"), "already open");
        }

        private static async Task RunCreationScenarioAsync(ChummerMainForm frmMain, string strSaveFile, CancellationToken token)
        {
            // The Heritage rows used to embed every metatype in full (~130 KB); they must now be compact
            (bool blnIsError, string strText) = await CallToolAsync("get_priority_table", new JObject { ["category"] = "Heritage" }, token).ConfigureAwait(false);
            Assert.IsFalse(blnIsError, strText);
            Assert.IsTrue(strText.Length < 20000, "Heritage table is " + strText.Length.ToString() + " characters");
            StringAssert.Contains(strText, "Human");
            JObject objTalents = (JObject)await CallToolOkAsync("get_priority_table", new JObject { ["category"] = "Talent" }, token).ConfigureAwait(false);
            StringAssert.Contains(objTalents.ToString(), "freeSkills");

            JArray objSettings = (JArray)await CallToolOkAsync("list_build_settings", null, token).ConfigureAwait(false);
            JToken objPrioritySettings = objSettings.FirstOrDefault(x => x.Value<bool>("isDefault") && x.Value<string>("buildMethod") == "Priority")
                                         ?? objSettings.First(x => x.Value<string>("buildMethod") == "Priority");
            string strSettings = objPrioritySettings.Value<string>("key");

            // Choices the new-character dialog would never offer are refused, and say what is allowed
            JObject objFace = new JObject
            {
                ["settings"] = strSettings,
                ["metatype"] = "Human",
                ["priorities"] = new JObject { ["heritage"] = "D", ["attributes"] = "B", ["talent"] = "E", ["skills"] = "A", ["resources"] = "C" },
                ["talent"] = "Mundane",
                ["name"] = "Agent Test Face"
            };
            JObject objBad = (JObject)objFace.DeepClone();
            objBad["priorities"]["attributes"] = "A";
            (blnIsError, strText) = await CallToolAsync("create_character", objBad, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            StringAssert.Contains(strText, "exactly once");
            objBad = (JObject)objFace.DeepClone();
            objBad["metatype"] = "Troll";
            objBad["priorities"] = new JObject { ["heritage"] = "E", ["attributes"] = "B", ["talent"] = "D", ["skills"] = "A", ["resources"] = "C" };
            objBad["talent"] = null;
            (blnIsError, strText) = await CallToolAsync("create_character", objBad, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            StringAssert.Contains(strText, "Options: Human");
            objBad = (JObject)objFace.DeepClone();
            objBad["priorities"] = new JObject { ["heritage"] = "D", ["attributes"] = "C", ["talent"] = "A", ["skills"] = "B", ["resources"] = "E" };
            objBad["talent"] = "Magician";
            (blnIsError, strText) = await CallToolAsync("create_character", objBad, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            StringAssert.Contains(strText, "Spellcasting");
            Assert.AreEqual(0, await frmMain.OpenCharacterEditorForms.GetCountAsync(token).ConfigureAwait(false), "A refused character must not be opened");

            // A mundane human face
            JObject objCreated = (JObject)await CallToolOkAsync("create_character", objFace, token).ConfigureAwait(false);
            Assert.IsNull(objCreated.Value<string>("warning"), objCreated.ToString());
            Assert.AreEqual(1, await frmMain.OpenCharacterEditorForms.GetCountAsync(token).ConfigureAwait(false));
            Assert.IsInstanceOfType(await frmMain.OpenCharacterEditorForms.GetValueAtAsync(0, token).ConfigureAwait(false), typeof(CharacterCreate));
            JObject objSummary = (JObject)await CallToolOkAsync("get_character_summary", null, token).ConfigureAwait(false);
            Assert.AreEqual("D", objSummary["priorities"].Value<string>("metatype"), objSummary["priorities"].ToString());
            Assert.AreEqual("A", objSummary["priorities"].Value<string>("skills"));
            Assert.AreEqual(140000m, objSummary["points"]["nuyen"].Value<decimal>("starting"));
            Assert.AreEqual(objSummary["points"]["skillPoints"].Value<int>("total"), objSummary["points"]["skillPoints"].Value<int>("remaining"));

            // Swapping two priorities keeps the rest
            await CallToolOkAsync("set_priorities", new JObject { ["priorities"] = new JObject { ["attributes"] = "C", ["resources"] = "B" } }, token)
                .ConfigureAwait(false);
            objSummary = (JObject)await CallToolOkAsync("get_character_summary", null, token).ConfigureAwait(false);
            Assert.AreEqual(275000m, objSummary["points"]["nuyen"].Value<decimal>("starting"));
            Assert.AreEqual("A", objSummary["priorities"].Value<string>("skills"));
            Assert.AreEqual("Human", objSummary.Value<string>("metatype"));

            // Gear: every add costs nuyen, cyberware costs essence, and items over the availability limit are refused
            decimal decNuyen = objSummary["points"]["nuyen"].Value<decimal>("remaining");
            JObject objResult = (JObject)await CallToolOkAsync("add_gear", new JObject { ["id"] = "Meta Link" }, token).ConfigureAwait(false);
            Assert.IsTrue(objResult.Value<decimal>("nuyenRemaining") < decNuyen, objResult.ToString());
            // A fake SIN asks for the name on it; forced_value answers so no dialog appears
            await CallToolOkAsync("add_gear", new JObject { ["id"] = "Fake SIN", ["rating"] = 4, ["forced_value"] = "Jane Doe" }, token).ConfigureAwait(false);
            (blnIsError, strText) = await CallToolAsync("add_gear", new JObject { ["id"] = "Fake SIN", ["rating"] = 5 }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            StringAssert.Contains(strText, "availability");
            await CallToolOkAsync("add_weapon", new JObject { ["id"] = "Ares Predator V" }, token).ConfigureAwait(false);
            await CallToolOkAsync("add_armor", new JObject { ["id"] = "Armor Jacket" }, token).ConfigureAwait(false);
            objResult = (JObject)await CallToolOkAsync("add_cyberware", new JObject { ["id"] = "Datajack", ["grade"] = "Alphaware" }, token).ConfigureAwait(false);
            Assert.IsTrue(objResult.Value<decimal>("essence") < 6m, objResult.ToString());
            (blnIsError, strText) = await CallToolAsync("add_cyberware", new JObject { ["id"] = "Wired Reflexes", ["rating"] = 3 }, token).ConfigureAwait(false);
            Assert.IsTrue(blnIsError, strText);
            StringAssert.Contains(strText, "availability");
            await CallToolOkAsync("add_cyberware", new JObject { ["id"] = "Orthoskin", ["kind"] = "bioware", ["rating"] = 1 }, token).ConfigureAwait(false);
            JObject objVehicles = (JObject)await CallToolOkAsync("search_catalog", new JObject { ["kind"] = "vehicle", ["limit"] = 1 }, token).ConfigureAwait(false);
            await CallToolOkAsync("add_vehicle", new JObject { ["id"] = objVehicles["items"][0].Value<string>("id") }, token).ConfigureAwait(false);
            await CallToolOkAsync("add_lifestyle", new JObject { ["id"] = "Low", ["months"] = 2 }, token).ConfigureAwait(false);

            objSummary = (JObject)await CallToolOkAsync("get_character_summary", null, token).ConfigureAwait(false);
            foreach (string strList in new[] { "gear", "weapons", "armor", "cyberware", "vehicles", "lifestyles" })
                Assert.IsTrue(((JArray)objSummary[strList]).Count > 0, strList + " is empty: " + objSummary[strList]);
            Assert.IsTrue(objSummary["points"]["nuyen"].Value<decimal>("remaining") < decNuyen);

            // Everything bought can be removed again, refunding it
            foreach (string strList in new[] { "weapons", "armor", "cyberware", "vehicles", "lifestyles", "gear" })
            {
                foreach (JToken objItem in (JArray)objSummary[strList])
                {
                    (blnIsError, strText) = await CallToolAsync("remove_item", new JObject { ["id"] = objItem.Value<string>("id") }, token).ConfigureAwait(false);
                    // Weapons that came with other gear go when their parent goes
                    if (blnIsError)
                        StringAssert.Contains(strText, "comes with", strText);
                }
            }

            objSummary = (JObject)await CallToolOkAsync("get_character_summary", null, token).ConfigureAwait(false);
            Assert.AreEqual(decNuyen, objSummary["points"]["nuyen"].Value<decimal>("remaining"), objSummary["points"].ToString());
            Assert.AreEqual(6m, objSummary.Value<decimal>("essence"));

            // Leftover points are reported even while there are errors (here: karma overspent on Body)
            JToken objBody = ((JArray)objSummary["attributes"]).First(x => x.Value<string>("name") == "BOD");
            objResult = (JObject)await CallToolOkAsync("set_attribute", new JObject { ["name"] = "BOD", ["karma"] = objBody.Value<int>("maxKarma") }, token)
                .ConfigureAwait(false);
            Assert.IsNotNull(objResult.Value<string>("warning"), objResult.ToString());
            JObject objValidation = (JObject)await CallToolOkAsync("validate_character", null, token).ConfigureAwait(false);
            Assert.IsFalse(objValidation.Value<bool>("valid"), objValidation.ToString());
            Assert.IsTrue(((JArray)objValidation["errors"]).Count > 0, objValidation.ToString());
            Assert.IsTrue(((JArray)objValidation["warnings"]).Count > 0, objValidation.ToString());
            await CallToolOkAsync("set_attribute", new JObject { ["name"] = "BOD", ["karma"] = 0 }, token).ConfigureAwait(false);

            JObject objSaved = (JObject)await CallToolOkAsync("save_character", new JObject { ["path"] = strSaveFile }, token).ConfigureAwait(false);
            Assert.AreEqual(strSaveFile, objSaved.Value<string>("file"), objSaved.ToString());
            Assert.IsTrue(File.Exists(strSaveFile));
            (blnIsError, strText) = await CallToolAsync("save_character", new JObject { ["path"] = strSaveFile }, token).ConfigureAwait(false);
            Assert.IsFalse(blnIsError, "Saving again to the character's own file must not need overwrite: " + strText);
        }

        private static async Task RunToolScenarioAsync(CancellationToken token)
        {
            JArray objOpen = (JArray)await CallToolOkAsync("list_open_characters", null, token).ConfigureAwait(false);
            Assert.AreEqual(1, objOpen.Count);
            Assert.AreEqual("creation", objOpen[0].Value<string>("mode"));

            JObject objSummary = (JObject)await CallToolOkAsync("get_character_summary", null, token).ConfigureAwait(false);
            Assert.AreEqual("creation", objSummary.Value<string>("mode"));
            int intKarma = objSummary["karma"].Value<int>("remaining");
            Assert.IsNotNull(objSummary["points"]["skillPoints"], objSummary.ToString());
            Assert.IsTrue(((JArray)objSummary["attributes"]).Count >= 8);
            Assert.IsNotNull(objSummary["magic"], "Skink is an adept");

            JObject objValidation = (JObject)await CallToolOkAsync("validate_character", null, token).ConfigureAwait(false);
            Assert.IsNotNull(objValidation["valid"]);
            Assert.IsInstanceOfType(objValidation["errors"], typeof(JArray));

            // Legality filtering must hide something (e.g. qualities forbidden for this character) but leave plenty
            JObject objAll = (JObject)await CallToolOkAsync("search_catalog", new JObject { ["kind"] = "quality", ["legal_only"] = false, ["limit"] = 1 }, token).ConfigureAwait(false);
            JObject objLegal = (JObject)await CallToolOkAsync("search_catalog", new JObject { ["kind"] = "quality", ["limit"] = 200 }, token).ConfigureAwait(false);
            Assert.IsTrue(objLegal.Value<int>("count") > 0);
            Assert.IsTrue(objLegal.Value<int>("count") < objAll.Value<int>("count"));
            JObject objPowers = (JObject)await CallToolOkAsync("search_catalog", new JObject { ["kind"] = "power", ["query"] = "Improved Reflexes", ["legal_only"] = false }, token).ConfigureAwait(false);
            Assert.IsTrue(objPowers.Value<int>("count") >= 1, objPowers.ToString());
            string strPowerId = objPowers["items"][0].Value<string>("id");
            JObject objItem = (JObject)await CallToolOkAsync("get_catalog_item", new JObject { ["kind"] = "power", ["id"] = strPowerId }, token).ConfigureAwait(false);
            Assert.IsNotNull(objItem["data"]);

            JObject objPriorities = (JObject)await CallToolOkAsync("get_priority_table", null, token).ConfigureAwait(false);
            Assert.IsNotNull(objPriorities["categories"]["Heritage"], objPriorities.ToString());
            JArray objMetatypes = (JArray)await CallToolOkAsync("list_metatypes", new JObject { ["category"] = "Metahuman" }, token).ConfigureAwait(false);
            Assert.IsTrue(objMetatypes.Any(x => x.Value<string>("name") == "Elf"));
            JObject objExport = (JObject)await CallToolOkAsync("export_character", null, token).ConfigureAwait(false);
            Assert.IsNotNull(objExport["sections"]["attributes"]);

            // Raising a skill with karma costs karma; going over the maximum is refused
            bool blnRaisedSkill = false;
            foreach (JToken objSkill in (JArray)objSummary["skills"])
            {
                if (objSkill.Value<int>("rating") >= objSkill.Value<int>("maximum"))
                    continue;
                (bool blnIsError, string strText) = await CallToolAsync("set_skill", new JObject
                {
                    ["name"] = objSkill.Value<string>("name"),
                    ["karma"] = objSkill.Value<int>("karma") + 1
                }, token).ConfigureAwait(false);
                if (blnIsError)
                    continue;
                Assert.IsTrue(JObject.Parse(strText).Value<int>("karmaRemaining") < intKarma, strText);
                (blnIsError, strText) = await CallToolAsync("set_skill", new JObject
                {
                    ["name"] = objSkill.Value<string>("name"),
                    ["karma"] = 50
                }, token).ConfigureAwait(false);
                Assert.IsTrue(blnIsError, strText);
                blnRaisedSkill = true;
                break;
            }

            Assert.IsTrue(blnRaisedSkill, "Could not raise any skill with karma");

            (bool blnAttributeError, string strAttributeText) = await CallToolAsync("set_attribute", new JObject { ["name"] = "BOD", ["karma"] = 40 }, token).ConfigureAwait(false);
            Assert.IsTrue(blnAttributeError, strAttributeText);

            int intContacts = ((JArray)objSummary["contacts"]).Count;
            await CallToolOkAsync("add_contact", new JObject { ["name"] = "Agent Test Fixer", ["connection"] = 2, ["loyalty"] = 2 }, token).ConfigureAwait(false);
            await CallToolOkAsync("add_knowledge_skill", new JObject { ["name"] = "Agent Test Lore", ["karma"] = 1 }, token).ConfigureAwait(false);
            JObject objAfter = (JObject)await CallToolOkAsync("get_character_summary", null, token).ConfigureAwait(false);
            Assert.AreEqual(intContacts + 1, ((JArray)objAfter["contacts"]).Count);
            Assert.IsTrue(((JArray)objAfter["knowledgeSkills"]).Any(x => x.Value<string>("name") == "Agent Test Lore"), objAfter["knowledgeSkills"].ToString());
            // Contact details and ratings edit the existing contact; out-of-range ratings are refused
            JObject objContactEdit = (JObject)await CallToolOkAsync("set_contact_details", new JObject
            {
                ["id"] = "Agent Test Fixer",
                ["role"] = "Fixer",
                ["hobbies_vice"] = "Gambling",
                ["loyalty"] = 3
            }, token).ConfigureAwait(false);
            Assert.IsNotNull(objContactEdit["karmaRemaining"], objContactEdit.ToString());
            // The export has a lone contact as an object and several as an array
            JToken objExportedContacts = ((JObject)await CallToolOkAsync("export_character", new JObject { ["section"] = "contacts" }, token).ConfigureAwait(false))["contacts"]["contact"];
            JToken objExportedContact = (objExportedContacts is JArray objContactArray ? (IEnumerable<JToken>)objContactArray : new[] { objExportedContacts })
                                        .First(x => x.Value<string>("name") == "Agent Test Fixer");
            Assert.AreEqual("Fixer", objExportedContact.Value<string>("role"), objExportedContact.ToString());
            Assert.AreEqual("Gambling", objExportedContact.Value<string>("hobbiesvice"), objExportedContact.ToString());
            Assert.AreEqual("3", objExportedContact.Value<string>("loyalty"), objExportedContact.ToString());
            (bool blnContactError, string strContactText) = await CallToolAsync("set_contact_details", new JObject { ["id"] = "Agent Test Fixer", ["loyalty"] = 9 }, token).ConfigureAwait(false);
            Assert.IsTrue(blnContactError, strContactText);
            await CallToolOkAsync("remove_item", new JObject { ["id"] = "Agent Test Fixer" }, token).ConfigureAwait(false);

            // Personal details change only the fields passed, and multi-line text keeps its paragraphs
            await CallToolOkAsync("set_character_details", new JObject
            {
                ["age"] = "31",
                ["concept"] = "Agent test concept",
                ["background"] = "First paragraph.\n\nSecond paragraph."
            }, token).ConfigureAwait(false);
            Assert.AreEqual("31", ((JObject)await CallToolOkAsync("export_character", new JObject { ["section"] = "age" }, token).ConfigureAwait(false)).Value<string>("age"));
            StringAssert.Contains(((JObject)await CallToolOkAsync("export_character", new JObject { ["section"] = "background" }, token).ConfigureAwait(false))
                                  .Value<string>("background"), "Second paragraph.");
            (bool blnDetailsError, string strDetailsText) = await CallToolAsync("set_character_details", new JObject(), token).ConfigureAwait(false);
            Assert.IsTrue(blnDetailsError, strDetailsText);

            // A quality with no choices; if Skink is at the positive quality limit the refusal must say so
            (bool blnQualityError, string strQualityText) = await CallToolAsync("add_quality", new JObject { ["id"] = "Photographic Memory" }, token).ConfigureAwait(false);
            if (blnQualityError)
                StringAssert.Contains(strQualityText.ToLowerInvariant(), "karma", strQualityText);
            else
            {
                JObject objAdded = JObject.Parse(strQualityText);
                if (objAdded.Value<int>("karmaRemaining") < 0)
                {
                    // Overspending is allowed in the editor but must be reported, and must block finishing creation
                    Assert.IsNotNull(objAdded.Value<string>("warning"), strQualityText);
                    JObject objOverspent = (JObject)await CallToolOkAsync("validate_character", null, token).ConfigureAwait(false);
                    Assert.IsFalse(objOverspent.Value<bool>("valid"), objOverspent.ToString());
                    Assert.IsTrue(((JArray)objOverspent["errors"]).Count > 0, objOverspent.ToString());
                }

                await CallToolOkAsync("remove_quality", new JObject { ["id"] = "Photographic Memory" }, token).ConfigureAwait(false);
            }
        }
    }
}
