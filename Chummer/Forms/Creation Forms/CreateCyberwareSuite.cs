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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Chummer.Backend.Equipment;

namespace Chummer
{
    public sealed partial class CreateCyberwareSuite : Form, IHasCharacterObject
    {
        private readonly Character _objCharacter;
        private readonly Improvement.ImprovementSource _eSource;
        private readonly string _strType;

        public Character CharacterObject => _objCharacter;

        #region Control Events

        public CreateCyberwareSuite(Character objCharacter, Improvement.ImprovementSource eSource = Improvement.ImprovementSource.Cyberware)
        {
            _objCharacter = objCharacter ?? throw new ArgumentNullException(nameof(objCharacter));
            InitializeComponent();
            _eSource = eSource;
            if (_eSource == Improvement.ImprovementSource.Bioware)
                Tag = "Title_CreateBiowareSuite";
            this.UpdateLightDarkMode();
            this.TranslateWinForm();
            this.UpdateParentForToolTipControls();
            _strType = _eSource == Improvement.ImprovementSource.Cyberware ? "cyberware" : "bioware";
            txtFileName.Text = "custom_" + _strType + ".xml";
        }

        private async void cmdOK_Click(object sender, EventArgs e)
        {
            // Make sure the suite and file name fields are populated.
            string strName = await txtName.DoThreadSafeFuncAsync(x => x.Text).ConfigureAwait(false);
            if (string.IsNullOrEmpty(strName))
            {
                await Program.ShowScrollableMessageBoxAsync(this, await LanguageManager.GetStringAsync("Message_CyberwareSuite_SuiteName").ConfigureAwait(false), await LanguageManager.GetStringAsync("MessageTitle_CyberwareSuite_SuiteName").ConfigureAwait(false), MessageBoxButtons.OK, MessageBoxIcon.Information).ConfigureAwait(false);
                return;
            }

            string strFileName = await txtFileName.DoThreadSafeFuncAsync(x => x.Text).ConfigureAwait(false);
            if (string.IsNullOrEmpty(strFileName))
            {
                await Program.ShowScrollableMessageBoxAsync(this, await LanguageManager.GetStringAsync("Message_CyberwareSuite_FileName").ConfigureAwait(false), await LanguageManager.GetStringAsync("MessageTitle_CyberwareSuite_FileName").ConfigureAwait(false), MessageBoxButtons.OK, MessageBoxIcon.Information).ConfigureAwait(false);
                return;
            }

            (string strError, string strErrorTitle) = await WriteSuiteAsync(_objCharacter, _eSource, strName, strFileName).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(strError))
            {
                await Program.ShowScrollableMessageBoxAsync(this, strError, strErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Information).ConfigureAwait(false);
                return;
            }

            await Program.ShowScrollableMessageBoxAsync(this, StringExtensions.FastFormat(await LanguageManager.GetStringAsync("Message_CyberwareSuite_SuiteCreated").ConfigureAwait(false), strName),
                await LanguageManager.GetStringAsync("MessageTitle_CyberwareSuite_SuiteCreated").ConfigureAwait(false), MessageBoxButtons.OK, MessageBoxIcon.Information).ConfigureAwait(false);
            await this.DoThreadSafeAsync(x =>
            {
                x.DialogResult = DialogResult.OK;
                x.Close();
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Why the character's ware can't be saved as a suite (all of it must share one grade), as a message and title, or empty strings if it can.
        /// </summary>
        public static async Task<(string Error, string Title)> CheckCanCreateSuiteAsync(Character objCharacter, Improvement.ImprovementSource eSource,
                                                                                         CancellationToken token = default)
        {
            string strGrade = string.Empty;
            foreach (Cyberware objCyberware in await (await objCharacter.GetCyberwareAsync(token).ConfigureAwait(false)).ToListAsync(token).ConfigureAwait(false))
            {
                if (objCyberware.SourceType != eSource)
                    continue;
                string strLoop = (await objCyberware.GetGradeAsync(token).ConfigureAwait(false)).ToString();
                if (string.IsNullOrEmpty(strGrade))
                    strGrade = strLoop;
                else if (strGrade != strLoop)
                    return (await LanguageManager.GetStringAsync("Message_CyberwareGradeMismatch", token: token).ConfigureAwait(false),
                            await LanguageManager.GetStringAsync("MessageTitle_CyberwareGradeMismatch", token: token).ConfigureAwait(false));
            }

            // The character has no Cyberware!
            if (string.IsNullOrEmpty(strGrade))
                return (await LanguageManager.GetStringAsync("Message_NoCyberware", token: token).ConfigureAwait(false),
                        await LanguageManager.GetStringAsync("MessageTitle_NoCyberware", token: token).ConfigureAwait(false));
            return (string.Empty, string.Empty);
        }

        /// <summary>
        /// Save the character's cyberware or bioware as a suite in a custom data file in the data folder, as the dialog's OK button does.
        /// </summary>
        /// <returns>An error message and its title, or empty strings if the suite was written.</returns>
        public static async Task<(string Error, string Title)> WriteSuiteAsync(Character objCharacter, Improvement.ImprovementSource eSource, string strName,
                                                                               string strFileName, CancellationToken token = default)
        {
            string strType = eSource == Improvement.ImprovementSource.Cyberware ? "cyberware" : "bioware";
            strFileName = strFileName.CleanForFileName();

            // Make sure the file name starts with custom and ends with _cyberware.xml.
            if (!strFileName.StartsWith("custom_", StringComparison.OrdinalIgnoreCase) || !strFileName.EndsWith("_" + strType + ".xml", StringComparison.OrdinalIgnoreCase))
            {
                return (StringExtensions.FastFormat(await LanguageManager.GetStringAsync("Message_CyberwareSuite_InvalidFileName", token: token).ConfigureAwait(false), strType),
                        await LanguageManager.GetStringAsync("MessageTitle_CyberwareSuite_InvalidFileName", token: token).ConfigureAwait(false));
            }

            // See if a Suite with this name already exists for the Custom category.
            // This was originally done without the XmlManager, but because amends and overrides and toggling custom data directories can change names, we need to use it.
            if ((await objCharacter.LoadDataXPathAsync(strType + ".xml", token: token).ConfigureAwait(false)).TryGetNodeByNameOrId("/chummer/suites/suite", strName) != null)
            {
                return (StringExtensions.FastFormat(await LanguageManager.GetStringAsync("Message_CyberwareSuite_DuplicateName", token: token).ConfigureAwait(false), strName),
                        await LanguageManager.GetStringAsync("MessageTitle_CyberwareSuite_DuplicateName", token: token).ConfigureAwait(false));
            }

            string strPath = Path.Combine(Utils.GetDataFolderPath, strFileName);

            bool blnNewFile = !File.Exists(strPath);

            // If this is not a new file, read in the existing contents.
            using (new FetchSafelyFromSafeObjectPool<XmlDocument>(Utils.XmlDocumentPool, out XmlDocument objXmlCurrentDocument))
            {
                if (!blnNewFile)
                {
                    try
                    {
                        await objXmlCurrentDocument.LoadStandardAsync(strPath, token: token).ConfigureAwait(false);
                    }
                    catch (IOException ex)
                    {
                        return (ex.ToString(), string.Empty);
                    }
                    catch (XmlException ex)
                    {
                        return (ex.ToString(), string.Empty);
                    }
                }

                using (FileStream objStream = new FileStream(strPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (XmlWriter objWriter = Utils.GetStandardXmlWriter(objStream))
                    {
                        await objWriter.WriteStartDocumentAsync().ConfigureAwait(false);

                        // <chummer>
                        await objWriter.WriteStartElementAsync("chummer").ConfigureAwait(false);
                        if (!blnNewFile)
                        {
                            // <cyberwares>
                            await objWriter.WriteStartElementAsync(strType + "s").ConfigureAwait(false);
                            using (XmlNodeList xmlCyberwareList = objXmlCurrentDocument.SelectNodes("/chummer/" + strType + "s"))
                                if (xmlCyberwareList?.Count > 0)
                                    foreach (XmlNode xmlCyberware in xmlCyberwareList)
                                        xmlCyberware.WriteContentTo(objWriter);
                            // </cyberwares>
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                        }

                        // <suites>
                        await objWriter.WriteStartElementAsync("suites").ConfigureAwait(false);

                        // If this is not a new file, write out the current contents.
                        if (!blnNewFile)
                        {
                            using (XmlNodeList xmlCyberwareList = objXmlCurrentDocument.SelectNodes("/chummer/suites"))
                                if (xmlCyberwareList?.Count > 0)
                                    foreach (XmlNode xmlCyberware in xmlCyberwareList)
                                        xmlCyberware.WriteContentTo(objWriter);
                        }

                        // Determine the Grade of Cyberware.
                        Cyberware objFirstWare = await objCharacter.Cyberware.FirstOrDefaultAsync(x => x.SourceType == eSource)
                            .ConfigureAwait(false);
                        string strGrade
                            = objFirstWare != null ? (await objFirstWare.GetGradeAsync().ConfigureAwait(false)).Name : string.Empty;

                        // <suite>
                        await objWriter.WriteStartElementAsync("suite").ConfigureAwait(false);
                        // <name />
                        await objWriter.WriteElementStringAsync("id", Guid.NewGuid().ToString()).ConfigureAwait(false);
                        // <name />
                        await objWriter.WriteElementStringAsync("name", strName).ConfigureAwait(false);
                        // <grade />
                        await objWriter.WriteElementStringAsync("grade", strGrade).ConfigureAwait(false);
                        // <cyberwares>
                        await objWriter.WriteStartElementAsync(strType + "s").ConfigureAwait(false);

                        // Write out the Cyberware.
                        await objCharacter.Cyberware.ForEachAsync(async objCyberware =>
                        {
                            if (objCyberware.SourceType != eSource)
                                return;

                            // <cyberware>
                            // ReSharper disable AccessToDisposedClosure
                            await objWriter.WriteStartElementAsync(strType).ConfigureAwait(false);
                            await objWriter.WriteElementStringAsync("name", objCyberware.Name).ConfigureAwait(false);
                            int intRating = await objCyberware.GetRatingAsync().ConfigureAwait(false);
                            if (intRating > 0)
                                await objWriter
                                      .WriteElementStringAsync(
                                          "rating", intRating.ToString(GlobalSettings.InvariantCultureInfo))
                                      .ConfigureAwait(false);
                            // Write out child items.
                            if (await (await objCyberware.GetChildrenAsync().ConfigureAwait(false)).GetCountAsync().ConfigureAwait(false) > 0)
                            {
                                // <cyberwares>
                                await objWriter.WriteStartElementAsync(strType + "s").ConfigureAwait(false);
                                await (await objCyberware.GetChildrenAsync().ConfigureAwait(false)).ForEachAsync(async objChild =>
                                {
                                    // Do not include items that come with the base item by default.
                                    if (objChild.Capacity != "[*]")
                                    {
                                        await objWriter.WriteStartElementAsync(strType).ConfigureAwait(false);
                                        await objWriter.WriteElementStringAsync("name", objChild.Name)
                                                       .ConfigureAwait(false);
                                        int intChildRating = await objChild.GetRatingAsync().ConfigureAwait(false);
                                        if (intChildRating > 0)
                                            await objWriter
                                                  .WriteElementStringAsync(
                                                      "rating",
                                                      intChildRating.ToString(GlobalSettings.InvariantCultureInfo))
                                                  .ConfigureAwait(false);
                                        // </cyberware>
                                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                                    }
                                }).ConfigureAwait(false);

                                // </cyberwares>
                                await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                            }

                            // </cyberware>
                            await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                            // ReSharper restore AccessToDisposedClosure
                        }).ConfigureAwait(false);

                        // </cyberwares>
                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                        // </suite>
                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);
                        // </chummer>
                        await objWriter.WriteEndElementAsync().ConfigureAwait(false);

                        await objWriter.WriteEndDocumentAsync().ConfigureAwait(false);
                    }
                }
            }

            // Reload the data so the new entry can be found (and its name can't be reused) without restarting
            await objCharacter.LoadDataAsync(strType + ".xml", string.Empty, true, token).ConfigureAwait(false);
            return (string.Empty, string.Empty);
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void CreateCyberwareSuite_Load(object sender, EventArgs e)
        {
            txtName.Left = lblName.Left + lblName.Width + 6;
            txtName.Width = Width - txtName.Left - 19;
            txtFileName.Left = txtName.Left;
            txtFileName.Width = txtName.Width;
        }

        #endregion Control Events
    }
}
