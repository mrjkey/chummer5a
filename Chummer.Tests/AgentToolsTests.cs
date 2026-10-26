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
            await CallToolOkAsync("remove_item", new JObject { ["id"] = "Agent Test Fixer" }, token).ConfigureAwait(false);

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
