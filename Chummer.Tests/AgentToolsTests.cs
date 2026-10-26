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

        [TestMethod]
        public void ToolsAgainstOpenCharacter()
        {
            CancellationToken token = TestContext.CancellationToken;
            FileInfo objFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? string.Empty,
                                                         "TestFiles", "Skink.chum5"));
            Assert.IsTrue(objFile.Exists, objFile.FullName);
            ChummerMainForm frmOldMainForm = Program.MainForm;
            ChummerMainForm frmTestForm = null;
            CharacterShared frmCharacterForm = null;
            Character objCharacter = new Character { FileName = objFile.FullName };
            try
            {
                Assert.IsTrue(objCharacter.Load(token: token));
                Utils.IsUnitTestForUI = true;
                frmTestForm = Utils.RunOnMainThread(() => new ChummerMainForm(true, true) { ShowInTaskbar = false }, token: token);
                Program.MainForm = frmTestForm;
                frmTestForm.DoThreadSafe(x => x.Show(), token);
                while (!frmTestForm.IsFinishedLoading)
                    Utils.SafeSleep(token);
                frmCharacterForm = frmTestForm.DoThreadSafeFunc(() => new CharacterCreate(objCharacter), token);
                frmCharacterForm.DoThreadSafe(x =>
                {
                    x.MdiParent = frmTestForm;
                    x.ShowInTaskbar = false;
                    x.Show();
                }, token);
                while (!frmCharacterForm.IsFinishedInitializing)
                    Utils.SafeSleep(token);

                Utils.SafelyRunSynchronously(() => RunToolScenarioAsync(token), token);
            }
            finally
            {
                try
                {
                    frmCharacterForm?.DoThreadSafe(x => x.Close(), token);
                    frmTestForm?.DoThreadSafe(x => x.Close(), token);
                }
                catch (Exception e) when (e is InvalidOperationException || e is ApplicationException)
                {
                    Console.WriteLine("Non-fatal exception while closing test forms: " + e.Message);
                }

                Program.MainForm = frmOldMainForm;
                Utils.IsUnitTestForUI = false;
            }
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
