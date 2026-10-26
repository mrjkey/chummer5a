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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace ChummerLauncher
{
    /// <summary>
    /// Starts Chummer\Chummer5.exe from the folder this launcher is in. Chummer looks for its data, settings and saves
    /// next to its own exe, so the release package keeps the app in a subfolder and puts this launcher at the top.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            string strBaseDir = AppDomain.CurrentDomain.BaseDirectory;
            string strChummerDir = Path.Combine(strBaseDir, "Chummer");
            string strChummerExe = Path.Combine(strChummerDir, "Chummer5.exe");
            if (!File.Exists(strChummerExe))
            {
                MessageBox.Show("Could not find " + strChummerExe + Environment.NewLine + Environment.NewLine
                                + "Keep this launcher next to the Chummer folder it came with.",
                                "Chummer5a", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            ProcessStartInfo objStartInfo = new ProcessStartInfo(strChummerExe, GetArguments(Environment.CommandLine))
            {
                WorkingDirectory = strChummerDir,
                UseShellExecute = false
            };
            try
            {
                Process.Start(objStartInfo)?.Dispose();
            }
            catch (Win32Exception e)
            {
                MessageBox.Show("Could not start " + strChummerExe + Environment.NewLine + Environment.NewLine + e.Message,
                                "Chummer5a", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Everything after the launcher's own path, untouched, so quoting of .chum5 paths and switches like /mcpport:N
        /// reaches Chummer exactly as given.
        /// </summary>
        private static string GetArguments(string strCommandLine)
        {
            int intIndex = 0;
            if (strCommandLine.StartsWith("\"", StringComparison.Ordinal))
            {
                intIndex = strCommandLine.IndexOf('"', 1);
                intIndex = intIndex < 0 ? strCommandLine.Length : intIndex + 1;
            }
            else
            {
                while (intIndex < strCommandLine.Length && !char.IsWhiteSpace(strCommandLine[intIndex]))
                    ++intIndex;
            }

            return strCommandLine.Substring(intIndex).TrimStart();
        }
    }
}
