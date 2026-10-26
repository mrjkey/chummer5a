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
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace Chummer.Agent
{
    /// <summary>
    /// Minimal Model Context Protocol server (Streamable HTTP transport, JSON responses only) so that an AI agent can inspect and edit open characters.
    /// Only binds to localhost.
    /// </summary>
    public sealed class McpServer : IDisposable
    {
        private static readonly Lazy<Logger> s_ObjLogger = new Lazy<Logger>(LogManager.GetCurrentClassLogger);
        private static Logger Log => s_ObjLogger.Value;

        public const int DefaultPort = 51723;
        private const string DefaultProtocolVersion = "2025-06-18";

        private readonly HttpListener _objListener = new HttpListener();
        private readonly CancellationTokenSource _objCancellationTokenSource = new CancellationTokenSource();
        private readonly IReadOnlyDictionary<string, AgentTool> _dicTools;
        private readonly string _strSessionId = Guid.NewGuid().ToString("N");

        public int Port { get; }

        public McpServer(int intPort, IEnumerable<AgentTool> lstTools)
        {
            Port = intPort;
            _dicTools = lstTools.ToDictionary(x => x.Name);
            _objListener.Prefixes.Add("http://localhost:" + intPort.ToString(GlobalSettings.InvariantCultureInfo) + "/");
        }

        /// <summary>
        /// Start an MCP server with the default tool set, or return null (and log why) if it could not be started.
        /// </summary>
        public static McpServer TryStart(int intPort)
        {
            McpServer objServer = new McpServer(intPort, AgentTools.CreateAll());
            try
            {
                objServer._objListener.Start();
            }
            catch (Exception e) when (e is HttpListenerException || e is InvalidOperationException)
            {
                Log.Warn(e, "Could not start agent MCP server on port " + intPort.ToString(GlobalSettings.InvariantCultureInfo));
                objServer.Dispose();
                return null;
            }

            _ = Task.Run(() => objServer.ListenLoopAsync(objServer._objCancellationTokenSource.Token));
            Log.Info("Agent MCP server listening on http://localhost:" + intPort.ToString(GlobalSettings.InvariantCultureInfo) + "/mcp");
            return objServer;
        }

        /// <summary>
        /// Port requested on the command line via /mcpport:N, or the default port.
        /// </summary>
        public static int GetPortFromCommandLine(IEnumerable<string> lstArgs)
        {
            foreach (string strArg in lstArgs)
            {
                if (strArg.StartsWith("/mcpport:", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(strArg.Substring(9), out int intPort) && intPort > 0 && intPort < 65536)
                    return intPort;
            }

            return DefaultPort;
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _objListener.IsListening)
            {
                HttpListenerContext objContext;
                try
                {
                    objContext = await _objListener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception e) when (e is HttpListenerException || e is ObjectDisposedException || e is InvalidOperationException)
                {
                    // Listener was stopped
                    return;
                }

                _ = Task.Run(() => HandleContextAsync(objContext, token), token);
            }
        }

        private async Task HandleContextAsync(HttpListenerContext objContext, CancellationToken token)
        {
            HttpListenerResponse objResponse = objContext.Response;
            try
            {
                HttpListenerRequest objRequest = objContext.Request;
                // Guard against DNS rebinding: browsers always send Origin, local CLI clients do not.
                string strOrigin = objRequest.Headers["Origin"];
                if (!string.IsNullOrEmpty(strOrigin)
                    && (!Uri.TryCreate(strOrigin, UriKind.Absolute, out Uri objOrigin) || !objOrigin.IsLoopback))
                {
                    objResponse.StatusCode = 403;
                    return;
                }

                if (!objRequest.Url.AbsolutePath.TrimEnd('/').Equals("/mcp", StringComparison.OrdinalIgnoreCase))
                {
                    objResponse.StatusCode = 404;
                    return;
                }

                if (objRequest.HttpMethod == "DELETE")
                {
                    objResponse.StatusCode = 200;
                    return;
                }

                if (objRequest.HttpMethod != "POST")
                {
                    // No server-initiated SSE stream
                    objResponse.StatusCode = 405;
                    objResponse.AddHeader("Allow", "POST, DELETE");
                    return;
                }

                string strBody;
                using (StreamReader objReader = new StreamReader(objRequest.InputStream, Encoding.UTF8))
                    strBody = await objReader.ReadToEndAsync().ConfigureAwait(false);

                JToken objResult;
                try
                {
                    JToken objMessage = JToken.Parse(strBody);
                    if (objMessage is JArray objBatch)
                    {
                        JArray objResults = new JArray();
                        foreach (JToken objItem in objBatch)
                        {
                            JObject objItemResult = await HandleMessageAsync(objItem as JObject, token).ConfigureAwait(false);
                            if (objItemResult != null)
                                objResults.Add(objItemResult);
                        }

                        objResult = objResults.Count > 0 ? objResults : null;
                    }
                    else
                        objResult = await HandleMessageAsync(objMessage as JObject, token).ConfigureAwait(false);
                }
                catch (JsonException e)
                {
                    objResult = MakeError(null, -32700, "Parse error: " + e.Message);
                }

                objResponse.AddHeader("Mcp-Session-Id", _strSessionId);
                if (objResult == null)
                {
                    // Notifications and responses get no body
                    objResponse.StatusCode = 202;
                    return;
                }

                byte[] achrBytes = Encoding.UTF8.GetBytes(objResult.ToString(Formatting.None));
                objResponse.StatusCode = 200;
                objResponse.ContentType = "application/json; charset=utf-8";
                objResponse.ContentLength64 = achrBytes.Length;
                await objResponse.OutputStream.WriteAsync(achrBytes, 0, achrBytes.Length, token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Warn(e, "Error while handling agent MCP request");
                try
                {
                    objResponse.StatusCode = 500;
                }
                catch (InvalidOperationException)
                {
                    // Headers already sent
                }
            }
            finally
            {
                try
                {
                    objResponse.Close();
                }
                catch (Exception e) when (e is HttpListenerException || e is ObjectDisposedException)
                {
                    // Client went away
                }
            }
        }

        /// <summary>
        /// Handle one JSON-RPC message. Returns null for notifications.
        /// </summary>
        internal async Task<JObject> HandleMessageAsync(JObject objMessage, CancellationToken token)
        {
            if (objMessage == null)
                return MakeError(null, -32600, "Invalid request");
            JToken objId = objMessage["id"];
            string strMethod = objMessage.Value<string>("method");
            if (string.IsNullOrEmpty(strMethod))
                // A response to something we never sent, or garbage; nothing to answer either way
                return objId == null ? null : MakeError(objId, -32600, "Invalid request: missing method");
            if (objId == null || objId.Type == JTokenType.Null)
                return null;
            JObject objParams = objMessage["params"] as JObject ?? new JObject();
            switch (strMethod)
            {
                case "initialize":
                    return MakeResult(objId, new JObject
                    {
                        ["protocolVersion"] = objParams.Value<string>("protocolVersion") ?? DefaultProtocolVersion,
                        ["capabilities"] = new JObject { ["tools"] = new JObject { ["listChanged"] = false } },
                        ["serverInfo"] = new JObject
                        {
                            ["name"] = "chummer",
                            ["version"] = Utils.CurrentChummerVersionLabel
                        },
                        ["instructions"] = AgentTools.ServerInstructions
                    });
                case "ping":
                    return MakeResult(objId, new JObject());
                case "tools/list":
                    return MakeResult(objId, new JObject
                    {
                        ["tools"] = new JArray(_dicTools.Values.Select(x => x.ToJson()))
                    });
                case "tools/call":
                    return MakeResult(objId, await CallToolAsync(objParams, token).ConfigureAwait(false));
                default:
                    return MakeError(objId, -32601, "Method not found: " + strMethod);
            }
        }

        private async Task<JObject> CallToolAsync(JObject objParams, CancellationToken token)
        {
            string strName = objParams.Value<string>("name");
            if (string.IsNullOrEmpty(strName) || !_dicTools.TryGetValue(strName, out AgentTool objTool))
                return MakeToolResult("Unknown tool: " + strName, true);
            JObject objArguments = objParams["arguments"] as JObject ?? new JObject();
            try
            {
                object objResult = await objTool.Handler(objArguments, token).ConfigureAwait(false);
                string strText = objResult as string
                                 ?? JsonConvert.SerializeObject(objResult, Formatting.Indented);
                return MakeToolResult(strText, false);
            }
            catch (AgentToolException e)
            {
                return MakeToolResult(e.Message, true);
            }
            catch (OperationCanceledException)
            {
                return MakeToolResult("Operation was cancelled.", true);
            }
            catch (Exception e)
            {
                Log.Warn(e, "Agent tool " + strName + " failed");
                return MakeToolResult("Tool " + strName + " failed: " + e.GetType().Name + ": " + e.Message, true);
            }
        }

        private static JObject MakeToolResult(string strText, bool blnIsError)
        {
            return new JObject
            {
                ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = strText }),
                ["isError"] = blnIsError
            };
        }

        private static JObject MakeResult(JToken objId, JToken objResult)
        {
            return new JObject { ["jsonrpc"] = "2.0", ["id"] = objId, ["result"] = objResult };
        }

        private static JObject MakeError(JToken objId, int intCode, string strMessage)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = objId ?? JValue.CreateNull(),
                ["error"] = new JObject { ["code"] = intCode, ["message"] = strMessage }
            };
        }

        public void Dispose()
        {
            try
            {
                _objCancellationTokenSource.Cancel();
                if (_objListener.IsListening)
                    _objListener.Stop();
                _objListener.Close();
            }
            catch (ObjectDisposedException)
            {
                // Already gone
            }

            _objCancellationTokenSource.Dispose();
        }
    }
}
