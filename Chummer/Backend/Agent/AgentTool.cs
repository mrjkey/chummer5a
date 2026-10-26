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
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Chummer.Agent
{
    /// <summary>
    /// A tool exposed to AI agents over MCP.
    /// </summary>
    public sealed class AgentTool
    {
        public string Name { get; }
        public string Description { get; }
        public JObject InputSchema { get; }

        /// <summary>
        /// Takes the tool arguments and returns either a string or an object that is serialized to JSON.
        /// </summary>
        public Func<JObject, CancellationToken, Task<object>> Handler { get; }

        public AgentTool(string strName, string strDescription, JObject objInputSchema,
                         Func<JObject, CancellationToken, Task<object>> funcHandler)
        {
            Name = strName;
            Description = strDescription;
            InputSchema = objInputSchema ?? Schema();
            Handler = funcHandler;
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["name"] = Name,
                ["description"] = Description,
                ["inputSchema"] = InputSchema
            };
        }

        /// <summary>
        /// Build an object JSON schema. Each property is (name, type, description); names ending in '!' are required.
        /// </summary>
        public static JObject Schema(params (string Name, string Type, string Description)[] aobjProperties)
        {
            JObject objProperties = new JObject();
            JArray objRequired = new JArray();
            foreach ((string strName, string strType, string strDescription) in aobjProperties)
            {
                string strPropertyName = strName.TrimEnd('!');
                if (strName.EndsWith("!", StringComparison.Ordinal))
                    objRequired.Add(strPropertyName);
                objProperties[strPropertyName] = new JObject { ["type"] = strType, ["description"] = strDescription };
            }

            JObject objSchema = new JObject { ["type"] = "object", ["properties"] = objProperties };
            if (objRequired.Count > 0)
                objSchema["required"] = objRequired;
            return objSchema;
        }
    }

    /// <summary>
    /// An expected failure (bad arguments, rule violation) whose message is returned to the agent as a tool error.
    /// </summary>
    public sealed class AgentToolException : Exception
    {
        public AgentToolException(string strMessage) : base(strMessage)
        {
        }

        public AgentToolException()
        {
        }

        public AgentToolException(string strMessage, Exception objInnerException) : base(strMessage, objInnerException)
        {
        }
    }
}
