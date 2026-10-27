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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.XPath;

namespace Chummer
{
    public partial class CreateSpell : Form
    {
        private readonly XPathNavigator _objXmlDocument;
        private bool _blnLoading = true;
        private int _intSkipRefresh;
        private readonly Character _objCharacter;
        private Spell _objSpell;

        #region Control Events

        public CreateSpell(Character objCharacter)
        {
            _objCharacter = objCharacter ?? throw new ArgumentNullException(nameof(objCharacter));
            InitializeComponent();
            this.UpdateLightDarkMode();
            this.TranslateWinForm();
            this.UpdateParentForToolTipControls();
            _objXmlDocument = objCharacter.LoadDataXPath("spells.xml");
        }

        private async void CreateSpell_Load(object sender, EventArgs e)
        {
            await lblDV.DoThreadSafeAsync(x => x.Text = 0.ToString(GlobalSettings.CultureInfo)).ConfigureAwait(false);

            using (new FetchSafelyFromSafeObjectPool<List<ListItem>>(Utils.ListItemListPool,
                                                           out List<ListItem> lstCategory))
            {
                // Populate the list of Spell Categories.
                foreach (XPathNavigator objXmlCategory in _objXmlDocument.SelectAndCacheExpression(
                             "/chummer/categories/category"))
                {
                    string strInnerText = objXmlCategory.Value;
                    lstCategory.Add(new ListItem(strInnerText,
                                                 objXmlCategory.SelectSingleNodeAndCacheExpression("@translate")?.Value
                                                 ?? strInnerText));
                }

                await cboCategory.PopulateWithListItemsAsync(lstCategory).ConfigureAwait(false);
            }

            await cboCategory.DoThreadSafeAsync(x => x.SelectedIndex = 0).ConfigureAwait(false);

            // Populate the list of Spell Types.
            using (new FetchSafelyFromSafeObjectPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstTypes))
            {
                lstTypes.Add(new ListItem("P", await LanguageManager.GetStringAsync("String_DescPhysical").ConfigureAwait(false)));
                lstTypes.Add(new ListItem("M", await LanguageManager.GetStringAsync("String_DescMana").ConfigureAwait(false)));
                await cboType.PopulateWithListItemsAsync(lstTypes).ConfigureAwait(false);
            }

            await cboType.DoThreadSafeAsync(x => x.SelectedIndex = 0).ConfigureAwait(false);

            // Populate the list of Ranges.
            using (new FetchSafelyFromSafeObjectPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstRanges))
            {
                lstRanges.Add(new ListItem("T", await LanguageManager.GetStringAsync("String_SpellRangeTouchLong").ConfigureAwait(false)));
                lstRanges.Add(new ListItem("LOS", await LanguageManager.GetStringAsync("String_SpellRangeLineOfSight").ConfigureAwait(false)));
                await cboRange.PopulateWithListItemsAsync(lstRanges).ConfigureAwait(false);
            }

            await cboRange.DoThreadSafeAsync(x => x.SelectedIndex = 0).ConfigureAwait(false);

            // Populate the list of Durations.
            using (new FetchSafelyFromSafeObjectPool<List<ListItem>>(Utils.ListItemListPool, out List<ListItem> lstDurations))
            {
                lstDurations.Add(new ListItem("I", await LanguageManager.GetStringAsync("String_SpellDurationInstantLong").ConfigureAwait(false)));
                lstDurations.Add(new ListItem("P", await LanguageManager.GetStringAsync("String_SpellDurationPermanentLong").ConfigureAwait(false)));
                lstDurations.Add(new ListItem("S", await LanguageManager.GetStringAsync("String_SpellDurationSustainedLong").ConfigureAwait(false)));
                await cboDuration.PopulateWithListItemsAsync(lstDurations).ConfigureAwait(false);
            }

            await cboDuration.DoThreadSafeAsync(x => x.SelectedIndex = 0).ConfigureAwait(false);

            _blnLoading = false;

            await CalculateDrain().ConfigureAwait(false);
        }

        private async void cboCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (await cboCategory.DoThreadSafeFuncAsync(x => x.SelectedValue.ToString()).ConfigureAwait(false) == "Health")
            {
                await chkArea.DoThreadSafeAsync(x =>
                {
                    x.Checked = false;
                    x.Enabled = false;
                }).ConfigureAwait(false);
            }
            else
                await chkArea.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);

            await ChangeModifiers().ConfigureAwait(false);
            await CalculateDrain().ConfigureAwait(false);
        }

        private async void cboType_SelectedIndexChanged(object sender, EventArgs e)
        {
            await CalculateDrain().ConfigureAwait(false);
        }

        private async void cboRange_SelectedIndexChanged(object sender, EventArgs e)
        {
            await CalculateDrain().ConfigureAwait(false);
        }

        private async void cboDuration_SelectedIndexChanged(object sender, EventArgs e)
        {
            await CalculateDrain().ConfigureAwait(false);
        }

        private async void chkModifier_CheckedChanged(object sender, EventArgs e)
        {
            await cboType.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
            if (_intSkipRefresh > 0)
                return;

            switch (await cboCategory.DoThreadSafeFuncAsync(x => x.SelectedValue.ToString()).ConfigureAwait(false))
            {
                case "Combat":
                    {
                        // Direct and Indirect cannot be selected at the same time.
                        if (await chkModifier1.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier2.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                                await chkModifier3.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                                await nudNumberOfEffects.DoThreadSafeAsync(x => x.Enabled = false)
                                                        .ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        else
                        {
                            await chkModifier2.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
                            await chkModifier3.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
                            await nudNumberOfEffects.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
                        }

                        // Indirect Combat Spells must always be physical. Direct and Indirect cannot be selected at the same time.
                        if (await chkModifier2.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier1.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                                await cboType.DoThreadSafeAsync(x =>
                                {
                                    x.SelectedValue = "P";
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        else
                        {
                            await chkModifier1.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
                        }

                        // Elemental effect spells must be Indirect (and consequently physical as well).
                        if (await chkModifier3.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            await chkModifier2.DoThreadSafeAsync(x => x.Checked = true).ConfigureAwait(false);
                        }

                        // Physical damage and Stun damage cannot be selected at the same time.
                        if (await chkModifier4.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier5.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        else
                        {
                            await chkModifier5.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
                        }
                        if (await chkModifier5.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier4.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        else
                        {
                            await chkModifier4.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
                        }

                        break;
                    }
                case "Detection":
                    {
                        // Directional, and Area cannot be selected at the same time.
                        if (await chkModifier1.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier2.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        if (await chkModifier2.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier1.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }

                        // Active and Passive cannot be selected at the same time.
                        if (await chkModifier4.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier5.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        else
                        {
                            await chkModifier5.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
                        }

                        if (await chkModifier5.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier4.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        else
                        {
                            await chkModifier4.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
                        }

                        // If Extended Area is selected, Area must also be selected.
                        if (await chkModifier4.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            await chkModifier1.DoThreadSafeAsync(x => x.Checked = false).ConfigureAwait(false);
                            await chkModifier3.DoThreadSafeAsync(x => x.Checked = false).ConfigureAwait(false);
                            await chkModifier2.DoThreadSafeAsync(x => x.Checked = true).ConfigureAwait(false);
                        }

                        break;
                    }
                case "Health":
                    // Nothing special for Health Spells.
                    break;

                case "Illusion":
                    {
                        // Obvious and Realistic cannot be selected at the same time.
                        if (await chkModifier1.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier2.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        else
                        {
                            await chkModifier2.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
                        }

                        if (await chkModifier2.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier1.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        else
                        {
                            await chkModifier1.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
                        }

                        // Single-Sense and Multi-Sense cannot be selected at the same time.
                        if (await chkModifier3.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier4.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        else
                        {
                            await chkModifier4.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
                        }
                        if (await chkModifier4.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier3.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        else
                        {
                            await chkModifier3.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
                        }

                        break;
                    }
                case "Manipulation":
                    {
                        // Environmental, Mental, and Physical cannot be selected at the same time.
                        if (await chkModifier1.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier2.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                                await chkModifier3.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        if (await chkModifier2.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier1.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                                await chkModifier3.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        if (await chkModifier3.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier1.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                                await chkModifier2.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        await chkModifier1.DoThreadSafeAsync(x => x.Enabled = !chkModifier2.Checked && !chkModifier3.Checked).ConfigureAwait(false);
                        await chkModifier2.DoThreadSafeAsync(x => x.Enabled = !chkModifier1.Checked && !chkModifier3.Checked).ConfigureAwait(false);
                        await chkModifier3.DoThreadSafeAsync(x => x.Enabled = !chkModifier1.Checked && !chkModifier2.Checked).ConfigureAwait(false);

                        // Minor Change and Major Change cannot be selected at the same time.
                        if (await chkModifier4.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier5.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        else
                        {
                            await chkModifier5.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
                        }
                        if (await chkModifier5.DoThreadSafeFuncAsync(x => x.Checked).ConfigureAwait(false))
                        {
                            Interlocked.Increment(ref _intSkipRefresh);
                            try
                            {
                                await chkModifier4.DoThreadSafeAsync(x =>
                                {
                                    x.Checked = false;
                                    x.Enabled = false;
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _intSkipRefresh);
                            }
                        }
                        else
                        {
                            await chkModifier4.DoThreadSafeAsync(x => x.Enabled = true).ConfigureAwait(false);
                        }

                        break;
                    }
            }

            await CalculateDrain().ConfigureAwait(false);
        }

        private async void chkRestricted_CheckedChanged(object sender, EventArgs e)
        {
            await chkVeryRestricted.DoThreadSafeAsync(x => x.Enabled = !chkRestricted.Checked).ConfigureAwait(false);
            await CalculateDrain().ConfigureAwait(false);
            await txtRestriction.DoThreadSafeAsync(x =>
            {
                x.Enabled = chkRestricted.Checked || chkVeryRestricted.Checked;
                if (!x.Enabled)
                    x.Text = string.Empty;
            }).ConfigureAwait(false);
        }

        private async void chkVeryRestricted_CheckedChanged(object sender, EventArgs e)
        {
            await chkRestricted.DoThreadSafeAsync(x => x.Enabled = !chkVeryRestricted.Checked).ConfigureAwait(false);
            await CalculateDrain().ConfigureAwait(false);
            await txtRestriction.DoThreadSafeAsync(x =>
            {
                x.Enabled = chkRestricted.Checked || chkVeryRestricted.Checked;
                if (!x.Enabled)
                    x.Text = string.Empty;
            }).ConfigureAwait(false);
        }

        private async void nudNumberOfEffects_ValueChanged(object sender, EventArgs e)
        {
            await CalculateDrain().ConfigureAwait(false);
        }

        private async void chkArea_CheckedChanged(object sender, EventArgs e)
        {
            await CalculateDrain().ConfigureAwait(false);
        }

        private async void cmdOK_Click(object sender, EventArgs e)
        {
            await AcceptForm().ConfigureAwait(false);
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        #endregion Control Events

        #region Methods

        /// <summary>
        /// Re-calculate the Drain modifiers based on the currently-selected options.
        /// </summary>
        private async Task ChangeModifiers(CancellationToken token = default)
        {
            foreach (Control objControl in await flpModifiers.DoThreadSafeFuncAsync(x => x.Controls, token: token).ConfigureAwait(false))
            {
                switch (objControl)
                {
                    case ColorableCheckBox chkCheckbox:
                    {
                        await chkCheckbox.DoThreadSafeAsync(x =>
                        {
                            x.Enabled = true;
                            x.Checked = false;
                            x.Text = string.Empty;
                            x.Tag = string.Empty;
                            x.Visible = false;
                        }, token: token).ConfigureAwait(false);
                        break;
                    }
                    case Panel panChild:
                    {
                        await panChild.DoThreadSafeAsync(x =>
                        {
                            foreach (CheckBox chkCheckbox in x.Controls.OfType<CheckBox>())
                            {
                                chkCheckbox.Enabled = true;
                                chkCheckbox.Checked = false;
                                chkCheckbox.Text = string.Empty;
                                chkCheckbox.Tag = string.Empty;
                                chkCheckbox.Visible = false;
                            }
                        }, token: token).ConfigureAwait(false);
                        break;
                    }
                }
            }

            await nudNumberOfEffects.DoThreadSafeAsync(x =>
            {
                x.Visible = false;
                x.Enabled = true;
            }, token: token).ConfigureAwait(false);

            string strCategory = await cboCategory.DoThreadSafeFuncAsync(x => x.SelectedValue.ToString(), token: token).ConfigureAwait(false);
            IReadOnlyList<(string LabelKey, int Drain)> lstModifiers = GetCategoryModifiers(strCategory);
            CheckBox[] achkModifiers = ModifierCheckBoxes;
            for (int i = 0; i < lstModifiers.Count; ++i)
            {
                string strText = await LanguageManager.GetStringAsync(lstModifiers[i].LabelKey, token: token).ConfigureAwait(false);
                string strTag = lstModifiers[i].Drain.ToString("+0;-0;+0", GlobalSettings.InvariantCultureInfo);
                await achkModifiers[i].DoThreadSafeAsync(x =>
                {
                    x.Tag = strTag;
                    x.Text = strText;
                }, token: token).ConfigureAwait(false);
            }

            // Combat and Manipulation spells take a number of effects next to their multiplying modifier
            CheckBox chkEffectsModifier;
            switch (strCategory)
            {
                case "Detection":
                case "Health":
                case "Illusion":
                    chkEffectsModifier = null;
                    break;
                case "Manipulation":
                    chkEffectsModifier = chkModifier6;
                    break;
                default:
                    chkEffectsModifier = chkModifier3;
                    break;
            }

            if (chkEffectsModifier != null)
            {
                int intTop = await chkEffectsModifier.DoThreadSafeFuncAsync(x => x.Top, token: token).ConfigureAwait(false);
                await nudNumberOfEffects.DoThreadSafeAsync(x =>
                {
                    x.Visible = true;
                    x.Top = intTop - 1;
                }, token: token).ConfigureAwait(false);
            }

            string strCheckBoxFormat = await LanguageManager.GetStringAsync("String_Space", token: token).ConfigureAwait(false) + "({0})";
            foreach (Control objControl in await flpModifiers.DoThreadSafeFuncAsync(x => x.Controls, token: token).ConfigureAwait(false))
            {
                switch (objControl)
                {
                    case CheckBox chkCheckbox:
                    {
                        await chkCheckbox.DoThreadSafeAsync(x =>
                        {
                            if (!string.IsNullOrEmpty(x.Text))
                            {
                                x.Visible = true;
                                x.Text += StringExtensions.FastFormat(strCheckBoxFormat, x.Tag?.ToString() ?? string.Empty);
                            }
                        }, token: token).ConfigureAwait(false);
                        break;
                    }
                    case Panel pnlControl:
                    {
                        await pnlControl.DoThreadSafeAsync(x =>
                        {
                            foreach (CheckBox chkInnerCheckbox in x.Controls.OfType<CheckBox>())
                            {
                                if (string.IsNullOrEmpty(chkInnerCheckbox.Text))
                                    continue;
                                chkInnerCheckbox.Visible = true;
                                chkInnerCheckbox.Text += StringExtensions.FastFormat(strCheckBoxFormat, chkInnerCheckbox.Tag?.ToString() ?? string.Empty);
                            }
                        }, token: token).ConfigureAwait(false);
                        break;
                    }
                }
            }

            if (await nudNumberOfEffects.DoThreadSafeFuncAsync(x => x.Visible, token: token).ConfigureAwait(false))
            {
                switch (await cboCategory.DoThreadSafeFuncAsync(x => x.SelectedValue.ToString(), token: token).ConfigureAwait(false))
                {
                    case "Combat":
                    {
                        int intBase = await chkModifier3.DoThreadSafeFuncAsync(x => x.Left + x.Width, token: token).ConfigureAwait(false);
                        await nudNumberOfEffects.DoThreadSafeAsync(x => x.Left = intBase + 6, token: token).ConfigureAwait(false);
                        break;
                    }
                    case "Manipulation":
                    {
                        int intBase = await chkModifier6.DoThreadSafeFuncAsync(x => x.Left + x.Width, token: token).ConfigureAwait(false);
                        await nudNumberOfEffects.DoThreadSafeAsync(x => x.Left = intBase + 6, token: token).ConfigureAwait(false);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Calculate the Spell's Drain Value based on the currently-selected options.
        /// </summary>
        private async Task<string> CalculateDrain(CancellationToken token = default)
        {
            if (_blnLoading)
                return string.Empty;

            string strCategory = await cboCategory.DoThreadSafeFuncAsync(x => x.SelectedValue.ToString(), token: token).ConfigureAwait(false);
            HashSet<int> setChecked = await GetCheckedModifiersAsync(token).ConfigureAwait(false);
            string strDrain = await CalculateDrainAsync(strCategory,
                                             await cboType.DoThreadSafeFuncAsync(x => x.SelectedValue.ToString(), token: token).ConfigureAwait(false),
                                             await cboRange.DoThreadSafeFuncAsync(x => x.SelectedValue.ToString(), token: token).ConfigureAwait(false),
                                             await chkArea.DoThreadSafeFuncAsync(x => x.Checked, token: token).ConfigureAwait(false),
                                             await chkRestricted.DoThreadSafeFuncAsync(x => x.Checked, token: token).ConfigureAwait(false),
                                             await chkVeryRestricted.DoThreadSafeFuncAsync(x => x.Checked, token: token).ConfigureAwait(false),
                                             await cboDuration.DoThreadSafeFuncAsync(x => x.SelectedValue.ToString(), token: token).ConfigureAwait(false),
                                             await nudNumberOfEffects.DoThreadSafeFuncAsync(x => x.ValueAsInt, token: token).ConfigureAwait(false),
                                             setChecked, token).ConfigureAwait(false);
            string strText = await strDrain.Replace('/', '÷').Replace('*', '×')
                                           .CheapReplaceAsync(
                                               "F", t => LanguageManager.GetStringAsync("String_SpellForce", token: t), token: token)
                                           .CheapReplaceAsync("Damage Value",
                                                              t => LanguageManager.GetStringAsync(
                                                                  "String_SpellDamageValue", token: t), token: token).ConfigureAwait(false);
            await lblDV.DoThreadSafeAsync(x => x.Text = strText, token: token).ConfigureAwait(false);

            return strDrain;
        }

        /// <summary>
        /// The dialog's modifier checkboxes, in the order <see cref="GetCategoryModifiers"/> lists modifiers.
        /// </summary>
        private CheckBox[] ModifierCheckBoxes => new CheckBox[]
        {
            chkModifier1, chkModifier2, chkModifier3, chkModifier4, chkModifier5, chkModifier6, chkModifier7, chkModifier8, chkModifier9,
            chkModifier10, chkModifier11, chkModifier12, chkModifier13, chkModifier14
        };

        /// <summary>
        /// Which modifiers are ticked, by their 1-based number in the category's list.
        /// </summary>
        private async Task<HashSet<int>> GetCheckedModifiersAsync(CancellationToken token = default)
        {
            HashSet<int> setReturn = new HashSet<int>();
            CheckBox[] achkModifiers = ModifierCheckBoxes;
            for (int i = 0; i < achkModifiers.Length; ++i)
            {
                if (await achkModifiers[i].DoThreadSafeFuncAsync(x => x.Checked && !string.IsNullOrEmpty(x.Tag?.ToString()), token: token).ConfigureAwait(false))
                    setReturn.Add(i + 1);
            }

            return setReturn;
        }

        /// <summary>
        /// Accept the values of the form.
        /// </summary>
        private async Task AcceptForm(CancellationToken token = default)
        {
            string strName = await txtName.DoThreadSafeFuncAsync(x => x.Text, token: token).ConfigureAwait(false);
            string strCategory = await cboCategory.DoThreadSafeFuncAsync(x => x.SelectedValue.ToString(), token: token).ConfigureAwait(false);
            bool blnRestricted = await chkRestricted.DoThreadSafeFuncAsync(x => x.Checked, token: token).ConfigureAwait(false);
            bool blnVeryRestricted = await chkVeryRestricted.DoThreadSafeFuncAsync(x => x.Checked, token: token).ConfigureAwait(false);
            string strRestriction = await txtRestriction.DoThreadSafeFuncAsync(x => x.Text, token: token).ConfigureAwait(false);
            HashSet<int> setChecked = await GetCheckedModifiersAsync(token).ConfigureAwait(false);
            string strMessage = await ValidateDesignAsync(strName, blnRestricted || blnVeryRestricted, strRestriction, strCategory, setChecked, token)
                .ConfigureAwait(false);

            // Show the message if necessary.
            if (!string.IsNullOrEmpty(strMessage))
            {
                await Program.ShowScrollableMessageBoxAsync(this, strMessage, await LanguageManager.GetStringAsync("Title_CreateSpell", token: token).ConfigureAwait(false), MessageBoxButtons.OK, MessageBoxIcon.Error, token: token).ConfigureAwait(false);
                return;
            }

            _objSpell = await CreateCustomSpellAsync(_objCharacter, strName, strCategory,
                                                     await cboType.DoThreadSafeFuncAsync(x => x.SelectedValue.ToString(), token: token).ConfigureAwait(false),
                                                     await cboRange.DoThreadSafeFuncAsync(x => x.SelectedValue.ToString(), token: token).ConfigureAwait(false),
                                                     await chkArea.DoThreadSafeFuncAsync(x => x.Checked, token: token).ConfigureAwait(false),
                                                     await cboDuration.DoThreadSafeFuncAsync(x => x.SelectedValue.ToString(), token: token).ConfigureAwait(false),
                                                     await chkLimited.DoThreadSafeFuncAsync(x => x.Checked, token: token).ConfigureAwait(false),
                                                     blnRestricted, blnVeryRestricted, strRestriction,
                                                     await nudNumberOfEffects.DoThreadSafeFuncAsync(x => x.ValueAsInt, token: token).ConfigureAwait(false),
                                                     setChecked, token).ConfigureAwait(false);

            await this.DoThreadSafeAsync(x =>
            {
                x.DialogResult = DialogResult.OK;
                x.Close();
            }, token: token).ConfigureAwait(false);
        }

        #endregion Methods

        #region Spell design

        private static readonly IReadOnlyDictionary<string, IReadOnlyList<(string LabelKey, int Drain)>> s_DicCategoryModifiers
            = new Dictionary<string, IReadOnlyList<(string LabelKey, int Drain)>>
            {
                ["Combat"] = new[]
                {
                    ("Checkbox_CombatSpell1", 0), ("Checkbox_CombatSpell2", 0), ("Checkbox_CombatSpell3", 2), ("Checkbox_CombatSpell4", 0),
                    ("Checkbox_CombatSpell5", -1)
                },
                ["Detection"] = new[]
                {
                    ("Checkbox_DetectionSpell1", 0), ("Checkbox_DetectionSpell2", 0), ("Checkbox_DetectionSpell3", 0), ("Checkbox_DetectionSpell4", 0),
                    ("Checkbox_DetectionSpell5", 0), ("Checkbox_DetectionSpell6", 0), ("Checkbox_DetectionSpell7", 1), ("Checkbox_DetectionSpell8", 1),
                    ("Checkbox_DetectionSpell9", 2), ("Checkbox_DetectionSpell10", 4), ("Checkbox_DetectionSpell11", 1), ("Checkbox_DetectionSpell12", 2),
                    ("Checkbox_DetectionSpell13", 4), ("Checkbox_DetectionSpell14", 2)
                },
                ["Health"] = new[]
                {
                    ("Checkbox_HealthSpell1", 0), ("Checkbox_HealthSpell2", 4), ("Checkbox_HealthSpell3", -2), ("Checkbox_HealthSpell4", 2),
                    ("Checkbox_HealthSpell5", -2)
                },
                ["Illusion"] = new[]
                {
                    ("Checkbox_IllusionSpell1", -1), ("Checkbox_IllusionSpell2", 0), ("Checkbox_IllusionSpell3", -2), ("Checkbox_IllusionSpell4", 0),
                    ("Checkbox_IllusionSpell5", 2)
                },
                ["Manipulation"] = new[]
                {
                    ("Checkbox_ManipulationSpell1", -2), ("Checkbox_ManipulationSpell2", 0), ("Checkbox_ManipulationSpell3", 0),
                    ("Checkbox_ManipulationSpell4", 0), ("Checkbox_ManipulationSpell5", 2), ("Checkbox_ManipulationSpell6", 2)
                }
            };

        /// <summary>
        /// The drain modifiers the dialog offers for a spell category, in checkbox order: the label's language key and its drain. Unknown categories
        /// get the combat spell modifiers, as the dialog does.
        /// </summary>
        public static IReadOnlyList<(string LabelKey, int Drain)> GetCategoryModifiers(string strCategory)
        {
            return s_DicCategoryModifiers.TryGetValue(strCategory ?? string.Empty, out IReadOnlyList<(string LabelKey, int Drain)> lstReturn)
                ? lstReturn
                : s_DicCategoryModifiers["Combat"];
        }

        /// <summary>
        /// A custom spell's drain value from the dialog's choices.
        /// </summary>
        /// <param name="strType">"P" or "M".</param>
        /// <param name="strRange">"T" or "LOS".</param>
        /// <param name="strDuration">"I", "P" or "S".</param>
        /// <param name="setChecked">The ticked modifiers, by 1-based number in <see cref="GetCategoryModifiers"/>.</param>
        public static async Task<string> CalculateDrainAsync(string strCategory, string strType, string strRange, bool blnArea, bool blnRestricted,
                                                             bool blnVeryRestricted, string strDuration, int intNumberOfEffects, ICollection<int> setChecked,
                                                             CancellationToken token = default)
        {
            int intDV = 0;

            // Type DV.
            if (strType != "M")
                ++intDV;

            // Range DV.
            if (strRange == "T")
                intDV -= 2;

            if (blnArea)
                intDV += 2;

            // Restriction DV.
            if (blnRestricted)
                --intDV;
            if (blnVeryRestricted)
                intDV -= 2;

            // Duration DV.
            // Curative Health Spells do not have a modifier for Permanent duration.
            if (strDuration == "P" && (strCategory != "Health" || !setChecked.Contains(1)))
                intDV += 2;

            // Include any checked modifiers.
            IReadOnlyList<(string LabelKey, int Drain)> lstModifiers = GetCategoryModifiers(strCategory);
            foreach (int intModifier in setChecked)
            {
                if (intModifier < 1 || intModifier > lstModifiers.Count)
                    continue;
                int intDummy = lstModifiers[intModifier - 1].Drain;
                if (intModifier == 3 && strCategory == "Combat")
                    intDV += intDummy * intNumberOfEffects;
                else if (intModifier == 6 && strCategory == "Manipulation")
                    intDV += intDummy * intNumberOfEffects;
                else
                    intDV += intDummy;
            }

            string strBase;
            if (strCategory == "Health" && setChecked.Contains(1))
            {
                // Health Spells use (Damage Value) as their base.
                strBase = "(" + await LanguageManager.GetStringAsync("String_SpellDamageValue", token: token).ConfigureAwait(false) + ")";
            }
            else
            {
                // All other spells use (F/2) as their base.
                strBase = "(F/2)";
            }

            string strDV = intDV.ToString(GlobalSettings.InvariantCultureInfo);
            if (intDV > 0)
                strDV = "+" + strDV;
            if (intDV == 0)
                strDV = string.Empty;
            return strBase + strDV;
        }

        /// <summary>
        /// What the dialog's OK button complains about in a design: a missing name or restriction, or a category's required choices.
        /// </summary>
        /// <returns>The messages, one per line, or an empty string if the design is complete.</returns>
        public static async Task<string> ValidateDesignAsync(string strName, bool blnRestrictionNeeded, string strRestriction, string strCategory,
                                                             ICollection<int> setChecked, CancellationToken token = default)
        {
            using (new FetchSafelyFromObjectPool<StringBuilder>(Utils.StringBuilderPool, out StringBuilder sbdMessage))
            {
                // Make sure a name has been provided.
                if (string.IsNullOrWhiteSpace(strName))
                {
                    sbdMessage.AppendLine(await LanguageManager.GetStringAsync("Message_SpellName", token: token).ConfigureAwait(false));
                }

                // Make sure a Restricted value if the field is enabled.
                if (blnRestrictionNeeded && string.IsNullOrWhiteSpace(strRestriction))
                {
                    sbdMessage.AppendLine(await LanguageManager.GetStringAsync("Message_SpellRestricted", token: token).ConfigureAwait(false));
                }

                switch (strCategory)
                {
                    // Make sure the Spell has met all of its requirements.
                    case "Combat":
                        {
                            // Either Direct or Indirect must be selected.
                            if (!setChecked.Contains(1) && !setChecked.Contains(2))
                            {
                                sbdMessage.AppendLine(await LanguageManager.GetStringAsync("Message_CombatSpellRequirement1", token: token).ConfigureAwait(false));
                            }

                            // Either Physical damage or Stun damage must be selected.
                            if (!setChecked.Contains(4) && !setChecked.Contains(5))
                            {
                                sbdMessage.AppendLine(await LanguageManager.GetStringAsync("Message_CombatSpellRequirement2", token: token).ConfigureAwait(false));
                            }

                            break;
                        }
                    case "Detection":
                        {
                            // Either Directional, Area, or Psychic must be selected.
                            if (!setChecked.Contains(1) && !setChecked.Contains(2) && !setChecked.Contains(3))
                            {
                                sbdMessage.AppendLine(await LanguageManager.GetStringAsync("Message_DetectionSpellRequirement1", token: token).ConfigureAwait(false));
                            }

                            // Either Active or Passive must be selected.
                            if (!setChecked.Contains(4) && !setChecked.Contains(5))
                            {
                                sbdMessage.AppendLine(await LanguageManager.GetStringAsync("Message_DetectionSpellRequirement2", token: token).ConfigureAwait(false));
                            }

                            break;
                        }
                    case "Health":
                        // Nothing special.
                        break;

                    case "Illusion":
                        {
                            // Either Obvious or Realistic must be selected.
                            if (!setChecked.Contains(1) && !setChecked.Contains(2))
                            {
                                sbdMessage.AppendLine(await LanguageManager.GetStringAsync("Message_IllusionSpellRequirement1", token: token).ConfigureAwait(false));
                            }

                            // Either Single-Sense or Multi-Sense must be selected.
                            if (!setChecked.Contains(3) && !setChecked.Contains(4))
                            {
                                sbdMessage.AppendLine(await LanguageManager.GetStringAsync("Message_IllusionSpellRequirement2", token: token).ConfigureAwait(false));
                            }

                            break;
                        }
                    case "Manipulation":
                        {
                            // Either Environmental, Mental, or Physical must be selected.
                            if (!setChecked.Contains(1) && !setChecked.Contains(2) && !setChecked.Contains(3))
                            {
                                sbdMessage.AppendLine(await LanguageManager.GetStringAsync("Message_ManipulationSpellRequirement1", token: token).ConfigureAwait(false));
                            }

                            // Either Minor Change or Major Change must be selected.
                            if (!setChecked.Contains(4) && !setChecked.Contains(5))
                            {
                                sbdMessage.AppendLine(await LanguageManager.GetStringAsync("Message_ManipulationSpellRequirement2", token: token).ConfigureAwait(false));
                            }

                            break;
                        }
                }

                return sbdMessage.Length > 0 ? sbdMessage.ToTrimmedString() : string.Empty;
            }
        }

        /// <summary>
        /// Build a custom spell from a design, as the dialog's OK button does. The caller adds it to the character's spells.
        /// </summary>
        public static async Task<Spell> CreateCustomSpellAsync(Character objCharacter, string strName, string strCategory, string strType, string strRange,
                                                               bool blnArea, string strDuration, bool blnLimited, bool blnRestricted, bool blnVeryRestricted,
                                                               string strRestriction, int intNumberOfEffects, ICollection<int> setChecked,
                                                               CancellationToken token = default)
        {
            string strFullRange = strRange;
            if (blnArea)
                strFullRange += "(A)";

            // If we're made it this far, everything is OK, so create the Spell.
            string strDescriptors = string.Empty;
            switch (strCategory)
            {
                case "Detection":
                    if (setChecked.Contains(4))
                        strDescriptors += "Active, ";
                    if (setChecked.Contains(5))
                        strDescriptors += "Passive, ";
                    if (setChecked.Contains(1))
                        strDescriptors += "Directional, ";
                    if (setChecked.Contains(3))
                        strDescriptors += "Psychic, ";
                    if (setChecked.Contains(2))
                    {
                        if (!setChecked.Contains(14))
                            strDescriptors += "Area, ";
                        else
                            strDescriptors += "Extended Area, ";
                    }
                    break;

                case "Health":
                    if (setChecked.Contains(4))
                        strDescriptors += "Negative, ";
                    break;

                case "Illusion":
                    if (setChecked.Contains(1))
                        strDescriptors += "Obvious, ";
                    if (setChecked.Contains(2))
                        strDescriptors += "Realistic, ";
                    if (setChecked.Contains(3))
                        strDescriptors += "Single-Sense, ";
                    if (setChecked.Contains(4))
                        strDescriptors += "Multi-Sense, ";
                    if (blnArea)
                        strDescriptors += "Area, ";
                    break;

                case "Manipulation":
                    if (setChecked.Contains(1))
                        strDescriptors += "Environmental, ";
                    if (setChecked.Contains(2))
                        strDescriptors += "Mental, ";
                    if (setChecked.Contains(3))
                        strDescriptors += "Physical, ";
                    if (blnArea)
                        strDescriptors += "Area, ";
                    break;

                default:
                    // Combat.
                    if (setChecked.Contains(1))
                        strDescriptors += "Direct, ";
                    if (setChecked.Contains(2))
                        strDescriptors += "Indirect, ";
                    if (strRange.Contains("(A)"))
                        strDescriptors += "Area, ";
                    if (setChecked.Contains(3))
                        strDescriptors += "Elemental, ";
                    break;
            }

            // Remove the trailing ", " from the Descriptors string.
            if (!string.IsNullOrEmpty(strDescriptors))
                strDescriptors = strDescriptors.Substring(0, strDescriptors.Length - 2);

            Spell objSpell = new Spell(objCharacter);
            try
            {
                objSpell.Name = strName;
                objSpell.Source = "SM";
                objSpell.Page = "159";
                objSpell.Category = strCategory;
                objSpell.Descriptors = strDescriptors;
                objSpell.Range = strFullRange;
                objSpell.Type = strType;
                objSpell.Limited = blnLimited;
                if (objSpell.Category == "Combat")
                    objSpell.Damage = setChecked.Contains(4) ? "P" : "S";
                objSpell.DvBase = await CalculateDrainAsync(strCategory, strType, strRange, blnArea, blnRestricted, blnVeryRestricted, strDuration,
                                                            intNumberOfEffects, setChecked, token).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(strRestriction))
                    objSpell.Extra = strRestriction;
                objSpell.Duration = strDuration;
            }
            catch
            {
                await objSpell.RemoveAsync(false, CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            return objSpell;
        }

        #endregion Spell design

        #region Properties

        /// <summary>
        /// Spell that was created in the dialogue.
        /// </summary>
        public Spell SelectedSpell => _objSpell;

        #endregion Properties
    }
}
