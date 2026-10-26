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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Chummer.Backend.Enums;

namespace Chummer
{
    public sealed partial class SelectBuildMethod : Form, IHasCharacterObject
    {
        private readonly Character _objCharacter;
        private readonly bool _blnForExistingCharacter;
        private CharacterBuildMethod _eStartingBuildMethod;
        private int _intLoading = 1;

        private CancellationTokenSource _objProcessCharacterSettingIndexChangedCancellationTokenSource;
        private CancellationTokenSource _objRepopulateCharacterSettingsCancellationTokenSource;
        private readonly CancellationTokenSource _objGenericCancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken _objGenericToken;

        public Character CharacterObject => _objCharacter;

        #region Control Events

        public SelectBuildMethod(Character objCharacter, bool blnUseCurrentValues = false)
        {
            _objCharacter = objCharacter ?? throw new ArgumentNullException(nameof(objCharacter));
            _objGenericToken = _objGenericCancellationTokenSource.Token;
            _blnForExistingCharacter = blnUseCurrentValues;
            InitializeComponent();
            this.UpdateLightDarkMode();
            this.TranslateWinForm();
            this.UpdateParentForToolTipControls();
        }

        /// <summary>
        /// Put a character on other settings, as OK does. Switching an existing character in creation to settings with another build method
        /// converts the build after <paramref name="funcConfirmBuildMethodSwitch"/> agrees.
        /// </summary>
        /// <param name="objCharacter">Character to change.</param>
        /// <param name="objSelectedGameplayOption">Settings to use.</param>
        /// <param name="blnIgnoreRules">Whether the character ignores the rules.</param>
        /// <param name="blnForExistingCharacter">Whether the character already exists (rather than being created).</param>
        /// <param name="eStartingBuildMethod">The build method the character had before.</param>
        /// <param name="funcConfirmBuildMethodSwitch">Asked before converting the build to another build method; false keeps the old settings.</param>
        /// <param name="token">Cancellation token to listen to.</param>
        /// <returns>Whether the settings were applied.</returns>
        internal static async Task<bool> ApplySettingsAsync(Character objCharacter, CharacterSettings objSelectedGameplayOption, bool blnIgnoreRules,
                                                            bool blnForExistingCharacter, CharacterBuildMethod eStartingBuildMethod,
                                                            Func<CharacterBuildMethod, CancellationToken, Task<bool>> funcConfirmBuildMethodSwitch,
                                                            CancellationToken token = default)
        {
            CharacterBuildMethod eSelectedBuildMethod = objSelectedGameplayOption.BuildMethod;
            if (blnForExistingCharacter
                && !await objCharacter.GetCreatedAsync(token).ConfigureAwait(false)
                && await (await objCharacter.GetSettingsAsync(token).ConfigureAwait(false)).GetBuildMethodAsync(
                    token).ConfigureAwait(false) == await objCharacter.GetEffectiveBuildMethodAsync(token).ConfigureAwait(false)
                && eSelectedBuildMethod != eStartingBuildMethod)
            {
                if (!await funcConfirmBuildMethodSwitch(eSelectedBuildMethod, token).ConfigureAwait(false))
                    return false;
                string strOldCharacterSettingsKey =
                    await objCharacter.GetSettingsKeyAsync(token).ConfigureAwait(false);
                await objCharacter.SetSettingsKeyAsync(
                        (await SettingsManager.GetLoadedCharacterSettingsAsync(token)
                            .ConfigureAwait(false))
                        .FirstOrDefault(x => ReferenceEquals(x.Value, objSelectedGameplayOption)).Key,
                        token)
                    .ConfigureAwait(false);
                // If the character is loading, make sure we only switch build methods after we've loaded, otherwise we might cause all sorts of nastiness
                if (objCharacter.IsLoading)
                    await objCharacter
                        .EnqueuePostLoadAsyncMethodAsync(
                            x => objCharacter.SwitchBuildMethods(eStartingBuildMethod, eSelectedBuildMethod,
                                strOldCharacterSettingsKey, x), token).ConfigureAwait(false);
                else if (!await objCharacter.SwitchBuildMethods(eStartingBuildMethod, eSelectedBuildMethod,
                             strOldCharacterSettingsKey, token).ConfigureAwait(false))
                    return false;
            }
            else
            {
                await objCharacter.SetSettingsKeyAsync((await SettingsManager.GetLoadedCharacterSettingsAsync(token).ConfigureAwait(false))
                    .FirstOrDefault(
                        x => ReferenceEquals(
                            x.Value, objSelectedGameplayOption)).Key, token).ConfigureAwait(false);
            }

            await objCharacter.SetIgnoreRulesAsync(blnIgnoreRules, token).ConfigureAwait(false);
            return true;
        }

        private async void cmdOK_Click(object sender, EventArgs e)
        {
            try
            {
                if (!(await cboCharacterSetting.DoThreadSafeFuncAsync(x => x.SelectedValue, _objGenericToken).ConfigureAwait(false) is CharacterSettings objSelectedGameplayOption))
                    return;
                if (!await ApplySettingsAsync(_objCharacter, objSelectedGameplayOption,
                                              await chkIgnoreRules.DoThreadSafeFuncAsync(x => x.Checked, _objGenericToken).ConfigureAwait(false),
                                              _blnForExistingCharacter, _eStartingBuildMethod,
                                              async (eSelectedBuildMethod, t) => await Program.ShowScrollableMessageBoxAsync(this,
                                                      StringExtensions.FastFormat(
                                                          await LanguageManager
                                                              .GetStringAsync("Message_SelectBP_SwitchBuildMethods", token: t)
                                                              .ConfigureAwait(false),
                                                          await LanguageManager
                                                              .GetStringAsync("String_" + eSelectedBuildMethod, token: t)
                                                              .ConfigureAwait(false),
                                                          await LanguageManager
                                                              .GetStringAsync("String_" + _eStartingBuildMethod, token: t)
                                                              .ConfigureAwait(false)),
                                                      await LanguageManager
                                                          .GetStringAsync("MessageTitle_SelectBP_SwitchBuildMethods", token: t)
                                                          .ConfigureAwait(false), MessageBoxButtons.YesNo,
                                                      MessageBoxIcon.Warning, token: t).ConfigureAwait(false) == DialogResult.Yes,
                                              _objGenericToken).ConfigureAwait(false))
                    return;
                await this.DoThreadSafeAsync(x =>
                {
                    x.DialogResult = DialogResult.OK;
                    x.Close();
                }, _objGenericToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                //swallow this
            }
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private async void cmdEditCharacterOption_Click(object sender, EventArgs e)
        {
            try
            {
                CursorWait objCursorWait = await CursorWait.NewAsync(this, token: _objGenericToken).ConfigureAwait(false);
                try
                {
                    Interlocked.Increment(ref _intLoading);
                    try
                    {
                        await this.DoThreadSafeAsync(x => x.SuspendLayout(), _objGenericToken).ConfigureAwait(false);
                        try
                        {
                            CharacterSettings objOldSelected = await cboCharacterSetting
                                                                     .DoThreadSafeFuncAsync(x => x.SelectedValue, _objGenericToken)
                                                                     .ConfigureAwait(false) as CharacterSettings;
                            using (ThreadSafeForm<EditCharacterSettings> frmOptions
                                   = await ThreadSafeForm<EditCharacterSettings>.GetAsync(
                                                                                    () => new EditCharacterSettings(
                                                                                        objOldSelected), _objGenericToken)
                                                                                .ConfigureAwait(false))
                                await frmOptions.ShowDialogSafeAsync(this, _objGenericToken).ConfigureAwait(false);

                            await RepopulateCharacterSettings(token: _objGenericToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            await this.DoThreadSafeAsync(x => x.ResumeLayout(), _objGenericToken).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _intLoading);
                    }
                }
                finally
                {
                    await objCursorWait.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                //swallow this
            }
        }

        private async void SelectBuildMethod_Load(object sender, EventArgs e)
        {
            try
            {
                CursorWait objCursorWait = await CursorWait.NewAsync(this, token: _objGenericToken).ConfigureAwait(false);
                try
                {
                    await this.DoThreadSafeAsync(x => x.SuspendLayout(), _objGenericToken).ConfigureAwait(false);
                    try
                    {
                        CharacterSettings objSelectSettings = null;
                        if (_blnForExistingCharacter)
                        {
                            _eStartingBuildMethod = await (await _objCharacter.GetSettingsAsync(_objGenericToken).ConfigureAwait(false)).GetBuildMethodAsync(_objGenericToken).ConfigureAwait(false);
                            IReadOnlyDictionary<string, CharacterSettings> dicCharacterSettings
                                = await SettingsManager.GetLoadedCharacterSettingsAsync(_objGenericToken).ConfigureAwait(false);
                            if (dicCharacterSettings.TryGetValue(
                                    await _objCharacter.GetSettingsKeyAsync(_objGenericToken).ConfigureAwait(false), out CharacterSettings objSetting))
                                objSelectSettings = objSetting;
                        }

                        await RepopulateCharacterSettings(objSelectSettings, _objGenericToken).ConfigureAwait(false);
                        await chkIgnoreRules.SetToolTipTextAsync(
                                                await LanguageManager.GetStringAsync("Tip_SelectKarma_IgnoreRules", token: _objGenericToken)
                                                                     .ConfigureAwait(false), _objGenericToken)
                                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        await this.DoThreadSafeAsync(x => x.ResumeLayout(), _objGenericToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    await objCursorWait.DisposeAsync().ConfigureAwait(false);
                }

                Interlocked.Decrement(ref _intLoading);
            }
            catch (OperationCanceledException)
            {
                //swallow this
            }
        }

        private void SelectBuildMethod_Closing(object sender, FormClosingEventArgs e)
        {
            CancellationTokenSource objOldCancellationTokenSource = Interlocked.Exchange(ref _objProcessCharacterSettingIndexChangedCancellationTokenSource, null);
            if (objOldCancellationTokenSource?.IsCancellationRequested == false)
            {
                objOldCancellationTokenSource.Cancel(false);
                objOldCancellationTokenSource.Dispose();
            }
            objOldCancellationTokenSource = Interlocked.Exchange(ref _objRepopulateCharacterSettingsCancellationTokenSource, null);
            if (objOldCancellationTokenSource?.IsCancellationRequested == false)
            {
                objOldCancellationTokenSource.Cancel(false);
                objOldCancellationTokenSource.Dispose();
            }
            _objGenericCancellationTokenSource.Cancel(false);
        }

        private async Task RepopulateCharacterSettings(CharacterSettings objSelected = null,
                                                            CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            CancellationTokenSource objNewCancellationTokenSource = new CancellationTokenSource();
            CancellationToken objNewToken = objNewCancellationTokenSource.Token;
            CancellationTokenSource objOldCancellationTokenSource = Interlocked.Exchange(ref _objRepopulateCharacterSettingsCancellationTokenSource, objNewCancellationTokenSource);
            if (objOldCancellationTokenSource?.IsCancellationRequested == false)
            {
                objOldCancellationTokenSource.Cancel(false);
                objOldCancellationTokenSource.Dispose();
            }
            using (CancellationTokenSource objJoinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, objNewToken))
            {
                token = objJoinedCancellationTokenSource.Token;
                CursorWait objCursorWait = await CursorWait.NewAsync(this, token: token).ConfigureAwait(false);
                try
                {
                    Interlocked.Increment(ref _intLoading);
                    try
                    {
                        object objOldSelected = objSelected ?? await cboCharacterSetting
                                                                     .DoThreadSafeFuncAsync(
                                                                         x => x.SelectedValue, token: token)
                                                                     .ConfigureAwait(false);
                        // Populate the Gameplay Settings list.
                        using (new FetchSafelyFromSafeObjectPool<List<ListItem>>(
                                   Utils.ListItemListPool, out List<ListItem> lstCharacterSettings))
                        {
                            IReadOnlyDictionary<string, CharacterSettings> dicCharacterSettings
                                = await SettingsManager.GetLoadedCharacterSettingsAsync(_objGenericToken).ConfigureAwait(false);
                            bool blnSuccess = dicCharacterSettings.TryGetValue(
                                GlobalSettings.DefaultCharacterSetting, out CharacterSettings objSetting);
                            await dicCharacterSettings.ForEachAsync(async (x, t) =>
                            {
                                lstCharacterSettings.Add(new ListItem(x.Value,
                                                                      await x.Value
                                                                             .GetCurrentDisplayNameAsync(t)
                                                                             .ConfigureAwait(false)));
                            }, token).ConfigureAwait(false);

                            lstCharacterSettings.Sort(CompareListItems.CompareNames);
                            await cboCharacterSetting.PopulateWithListItemsAsync(lstCharacterSettings, token: token)
                                                     .ConfigureAwait(false);
                            await cboCharacterSetting.DoThreadSafeAsync(x =>
                            {
                                if (objOldSelected != null)
                                    x.SelectedValue = objOldSelected;
                                if (objOldSelected == null || x.SelectedIndex == -1)
                                {
                                    if (blnSuccess)
                                        x.SelectedValue = objSetting;
                                    if (x.SelectedIndex == -1 && lstCharacterSettings.Count > 0)
                                    {
                                        x.SelectedIndex = 0;
                                    }
                                }
                            }, token: token).ConfigureAwait(false);
                        }

                        await ProcessCharacterSettingIndexChanged(token).ConfigureAwait(false);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _intLoading);
                    }
                }
                finally
                {
                    await objCursorWait.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        private async void cboCharacterSetting_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_intLoading > 0)
                return;
            try
            {
                CursorWait objCursorWait = await CursorWait.NewAsync(this, token: _objGenericToken).ConfigureAwait(false);
                try
                {
                    await this.DoThreadSafeAsync(x => x.SuspendLayout(), _objGenericToken).ConfigureAwait(false);
                    try
                    {
                        await ProcessCharacterSettingIndexChanged(_objGenericToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        await this.DoThreadSafeAsync(x => x.ResumeLayout(), _objGenericToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    await objCursorWait.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                //swallow this
            }
        }

        private async Task ProcessCharacterSettingIndexChanged(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            CancellationTokenSource objNewCancellationTokenSource = new CancellationTokenSource();
            CancellationToken objNewToken = objNewCancellationTokenSource.Token;
            CancellationTokenSource objOldCancellationTokenSource = Interlocked.Exchange(ref _objProcessCharacterSettingIndexChangedCancellationTokenSource, objNewCancellationTokenSource);
            if (objOldCancellationTokenSource?.IsCancellationRequested == false)
            {
                objOldCancellationTokenSource.Cancel(false);
                objOldCancellationTokenSource.Dispose();
            }
            using (CancellationTokenSource objJoinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, objNewToken))
            {
                token = objJoinedCancellationTokenSource.Token;
                // Load the Priority information.
                if (await cboCharacterSetting.DoThreadSafeFuncAsync(x => x.SelectedValue, token).ConfigureAwait(false) is CharacterSettings objSelectedGameplayOption)
                {
                    string strBuildMethod = await LanguageManager.GetStringAsync("String_" + await objSelectedGameplayOption.GetBuildMethodAsync(token).ConfigureAwait(false), token: token).ConfigureAwait(false);
                    await lblBuildMethod.DoThreadSafeAsync(x => x.Text = strBuildMethod, token).ConfigureAwait(false);
                    switch (objSelectedGameplayOption.BuildMethod)
                    {
                        case CharacterBuildMethod.Priority:
                            {
                                string strText1 = await LanguageManager.GetStringAsync("Label_SelectBP_Priorities", token: token)
                                                               .ConfigureAwait(false);
                                await lblBuildMethodParamLabel.DoThreadSafeAsync(x =>
                                {
                                    x.Text = strText1;
                                    x.Visible = true;
                                }, token).ConfigureAwait(false);
                                string strText2 = await objSelectedGameplayOption.GetPriorityArrayAsync(token).ConfigureAwait(false);
                                await lblBuildMethodParam.DoThreadSafeAsync(x =>
                                {
                                    x.Text = strText2;
                                    x.Visible = true;
                                }, token).ConfigureAwait(false);
                                break;
                            }
                        case CharacterBuildMethod.SumtoTen:
                            {
                                string strText1 = await LanguageManager.GetStringAsync("String_SumtoTen", token: token)
                                                               .ConfigureAwait(false);
                                await lblBuildMethodParamLabel.DoThreadSafeAsync(x =>
                                {
                                    x.Text = strText1;
                                    x.Visible = true;
                                }, token).ConfigureAwait(false);
                                string strText2 = (await objSelectedGameplayOption.GetSumtoTenAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.CultureInfo);
                                await lblBuildMethodParam.DoThreadSafeAsync(x =>
                                {
                                    x.Text = strText2;
                                    x.Visible = true;
                                }, token).ConfigureAwait(false);
                                break;
                            }
                        default:
                            await lblBuildMethodParamLabel.DoThreadSafeAsync(x => x.Visible = false, token).ConfigureAwait(false);
                            await lblBuildMethodParam.DoThreadSafeAsync(x => x.Visible = false, token).ConfigureAwait(false);
                            break;
                    }

                    string strNone = await LanguageManager.GetStringAsync("String_None", token: token).ConfigureAwait(false);
                    string strMaxAvail = (await objSelectedGameplayOption.GetMaximumAvailabilityAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.CultureInfo);
                    await lblMaxAvail.DoThreadSafeAsync(x => x.Text = strMaxAvail, token).ConfigureAwait(false);
                    string strKarma = (await objSelectedGameplayOption.GetBuildKarmaAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.CultureInfo);
                    await lblKarma.DoThreadSafeAsync(x => x.Text = strKarma, token).ConfigureAwait(false);
                    string strMaxNuyen = (await objSelectedGameplayOption.GetNuyenMaximumBPAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.CultureInfo);
                    await lblMaxNuyen.DoThreadSafeAsync(x => x.Text = strMaxNuyen, token).ConfigureAwait(false);
                    string strQualityKarma = (await objSelectedGameplayOption.GetQualityKarmaLimitAsync(token).ConfigureAwait(false)).ToString(GlobalSettings.CultureInfo);
                    await lblQualityKarma.DoThreadSafeAsync(x => x.Text = strQualityKarma, token).ConfigureAwait(false);

                    string strBookList = await objSelectedGameplayOption.TranslatedBookListAsync(StringExtensions.JoinFast(";",
                        await objSelectedGameplayOption.GetBooksAsync(token).ConfigureAwait(false)), token: token).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(strBookList))
                        strBookList = strNone;
                    await lblBooks.DoThreadSafeAsync(x => x.Text = strBookList, token).ConfigureAwait(false);

                    string strCustomDataText;
                    using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool,
                               out StringBuilder sbdCustomDataDirectories))
                    {
                        foreach (CustomDataDirectoryInfo objLoopInfo in await objSelectedGameplayOption
                                     .GetEnabledCustomDataDirectoryInfosAsync(token).ConfigureAwait(false))
                            sbdCustomDataDirectories.AppendLine(await objLoopInfo.GetCurrentDisplayNameAsync(token)
                                .ConfigureAwait(false));

                        strCustomDataText = sbdCustomDataDirectories.ToString();
                    }
                    if (string.IsNullOrEmpty(strCustomDataText))
                        strCustomDataText = strNone;
                    await lblCustomData.DoThreadSafeAsync(x => x.Text = strCustomDataText, token).ConfigureAwait(false);
                }
            }
        }

        #endregion Control Events
    }
}
