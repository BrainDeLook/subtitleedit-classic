using System;
using System.Drawing;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Settings;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms.BeautifyTimeCodes
{
    public sealed partial class BeautifyTimeCodesProfile : Form
    {
        private readonly double _frameRate;

        public BeautifyTimeCodesProfile(double frameRate)
        {
            _frameRate = frameRate;

            UiUtil.PreInitialize(this);
            InitializeComponent();
            UiUtil.FixFonts(this);
            
            if (frameRate > 0)
            {
                cuesPreviewViewInCues.FrameRate = (float)frameRate;
                cuesPreviewViewOutCues.FrameRate = (float)frameRate;
                cuesPreviewViewConnectedSubtitlesInCueClosest.FrameRate = (float)frameRate;
                cuesPreviewViewConnectedSubtitlesOutCueClosest.FrameRate = (float)frameRate;
                cuesPreviewViewChainingGeneral.FrameRate = (float)frameRate;
                cuesPreviewViewChainingInCueOnShot.FrameRate = (float)frameRate;
                cuesPreviewViewChainingOutCueOnShot.FrameRate = (float)frameRate;
            }
            
            var language = LanguageSettings.Current.BeautifyTimeCodesProfile;
            Text = language.Title;
            buttonCreateSimple.Text = language.CreateSimple;
            toolStripMenuItemLoadPreset.Text = language.LoadPreset;
            groupBoxGeneral.Text = language.General;
            labelGap.Text = language.Gap;
            labelGapSuffix.Text = language.GapSuffix;
            groupBoxInCues.Text = language.InCues;
            labelInCuesGap.Text = language.Gap;
            labelInCuesZones.Text = language.Zones;
            groupBoxOutCues.Text = language.OutCues;
            labelOutCuesGap.Text = language.Gap;
            labelOutCuesZones.Text = language.Zones;
            groupBoxConnectedSubtitles.Text = language.ConnectedSubtitles;
            tabPageConnectedSubtitlesInCueClosest.Text = language.InCueClosest;
            labelConnectedSubtitlesInCueClosestGaps.Text = language.Gap;
            tabPageConnectedSubtitlesOutCueClosest.Text = language.OutCueClosest;
            labelConnectedSubtitlesOutCueClosestGaps.Text = language.Gap;
            labelConnectedSubtitlesZones.Text = language.Zones;
            labelConnectedSubtitlesTreatConnected.Text = language.TreadAsConnected;
            labelConnectedSubtitlesTreatConnectedSuffix.Text = language.Milliseconds;
            groupBoxChaining.Text = language.Chaining;
            tabPageChainingGeneral.Text = language.General;
            radioButtonChainingGeneralMaxGap.Text = language.MaxGap;
            labelChainingGeneralMaxGapSuffix.Text = language.Milliseconds;
            radioButtonChainingGeneralZones.Text = language.Zones;
            labelChainingGeneralShotChangeBehavior.Text = language.ShotChangeBehavior;
            comboBoxChainingGeneralShotChangeBehavior.Items.Clear();
            comboBoxChainingGeneralShotChangeBehavior.Items.Add(language.DontChain);
            comboBoxChainingGeneralShotChangeBehavior.Items.Add(language.ExtendCrossingShotChange);
            comboBoxChainingGeneralShotChangeBehavior.Items.Add(language.ExtendUntilShotChange);
            tabPageChainingInCueOnShot.Text = language.InCueOnShot;
            radioButtonChainingInCueOnShotMaxGap.Text = language.MaxGap;
            labelChainingInCueOnShotMaxGapSuffix.Text = language.Milliseconds;
            radioButtonChainingInCueOnShotZones.Text = language.Zones;
            checkBoxChainingInCueOnShotCheckGeneral.Text = language.CheckGeneral;
            tabPageChainingOutCueOnShot.Text = language.OutCueOnShot;
            radioButtonChainingOutCueOnShotMaxGap.Text = language.MaxGap;
            labelChainingOutCueOnShotMaxGapSuffix.Text = language.Milliseconds;
            radioButtonChainingOutCueOnShotZones.Text = language.Zones;
            checkBoxChainingOutCueOnShotCheckGeneral.Text = language.CheckGeneral;

            cuesPreviewViewInCues.PreviewText = language.SubtitlePreviewText;
            cuesPreviewViewOutCues.PreviewText = language.SubtitlePreviewText;
            cuesPreviewViewConnectedSubtitlesInCueClosest.PreviewText = language.SubtitlePreviewText;
            cuesPreviewViewConnectedSubtitlesOutCueClosest.PreviewText = language.SubtitlePreviewText;
            cuesPreviewViewChainingGeneral.PreviewText = language.SubtitlePreviewText;
            cuesPreviewViewChainingInCueOnShot.PreviewText = language.SubtitlePreviewText;
            cuesPreviewViewChainingOutCueOnShot.PreviewText = language.SubtitlePreviewText;

            numericUpDownGap.Left = labelGap.Left + labelGap.Width + 12;
            labelGapSuffix.Left = numericUpDownGap.Left + numericUpDownGap.Width + 6;

            numericUpDownConnectedSubtitlesTreatConnected.Left = Math.Max(labelConnectedSubtitlesTreatConnected.Left + labelConnectedSubtitlesTreatConnected.Width + 6, numericUpDownConnectedSubtitlesTreatConnected.Left);
            labelConnectedSubtitlesTreatConnectedSuffix.Left = numericUpDownConnectedSubtitlesTreatConnected.Left + numericUpDownConnectedSubtitlesTreatConnected.Width + 6;

            var comboBoxRight = comboBoxChainingGeneralShotChangeBehavior.Right;
            comboBoxChainingGeneralShotChangeBehavior.Left = Math.Max(labelChainingGeneralShotChangeBehavior.Left + labelChainingGeneralShotChangeBehavior.Width + 6, comboBoxChainingGeneralShotChangeBehavior.Left);
            comboBoxChainingGeneralShotChangeBehavior.Width = comboBoxRight - comboBoxChainingGeneralShotChangeBehavior.Left;

            var dropDownWidth = comboBoxChainingGeneralShotChangeBehavior.Width;
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
            {
                foreach (var item in comboBoxChainingGeneralShotChangeBehavior.Items)
                {
                    var itemWidth = (int)g.MeasureString((string)item, Font).Width + 5;
                    dropDownWidth = Math.Max(itemWidth, dropDownWidth);
                }
            }
            comboBoxChainingGeneralShotChangeBehavior.DropDownWidth = dropDownWidth;

            foreach (BeautifyTimeCodesSettings.BeautifyTimeCodesProfile.Preset preset in Enum.GetValues(typeof(BeautifyTimeCodesSettings.BeautifyTimeCodesProfile.Preset)))
            {
                var toolStripMenuItem = new ToolStripMenuItem(UiUtil.GetBeautifyTimeCodesProfilePresetName(preset));
                toolStripMenuItem.Click += (sender, args) =>
                {
                    ResetSettings(preset);
                };
                toolStripMenuItemLoadPreset.DropDownItems.Add(toolStripMenuItem);
            }

            buttonOK.Text = LanguageSettings.Current.General.Ok;
            buttonCancel.Text = LanguageSettings.Current.General.Cancel;
            UiUtil.FixLargeFonts(this, buttonOK);

            InitializeClassicProfiles();
            LoadSettings();
        }

        private void LoadSettings()
        {
            _loadingClassicProfile = true;
            ResetZoneMinimums();
            var settings = _workingProfile;

            numericUpDownGap.Value = settings.Gap;

            numericUpDownInCuesGap.Value = settings.InCuesGap;
            numericUpDownInCuesLeftRedZone.Value = settings.InCuesLeftRedZone;
            numericUpDownInCuesRightRedZone.Value = settings.InCuesRightRedZone;
            numericUpDownInCuesLeftGreenZone.Value = settings.InCuesLeftGreenZone;
            numericUpDownInCuesRightGreenZone.Value = settings.InCuesRightGreenZone;

            numericUpDownOutCuesGap.Value = settings.OutCuesGap;
            numericUpDownOutCuesLeftRedZone.Value = settings.OutCuesLeftRedZone;
            numericUpDownOutCuesRightRedZone.Value = settings.OutCuesRightRedZone;
            numericUpDownOutCuesLeftGreenZone.Value = settings.OutCuesLeftGreenZone;
            numericUpDownOutCuesRightGreenZone.Value = settings.OutCuesRightGreenZone;

            numericUpDownConnectedSubtitlesInCueClosestLeftGap.Value = settings.ConnectedSubtitlesInCueClosestLeftGap;
            numericUpDownConnectedSubtitlesInCueClosestRightGap.Value = settings.ConnectedSubtitlesInCueClosestRightGap;
            numericUpDownConnectedSubtitlesOutCueClosestLeftGap.Value = settings.ConnectedSubtitlesOutCueClosestLeftGap;
            numericUpDownConnectedSubtitlesOutCueClosestRightGap.Value = settings.ConnectedSubtitlesOutCueClosestRightGap;
            numericUpDownConnectedSubtitlesLeftRedZone.Value = settings.ConnectedSubtitlesLeftRedZone;
            numericUpDownConnectedSubtitlesRightRedZone.Value = settings.ConnectedSubtitlesRightRedZone;
            numericUpDownConnectedSubtitlesLeftGreenZone.Value = settings.ConnectedSubtitlesLeftGreenZone;
            numericUpDownConnectedSubtitlesRightGreenZone.Value = settings.ConnectedSubtitlesRightGreenZone;
            numericUpDownConnectedSubtitlesTreatConnected.Value = settings.ConnectedSubtitlesTreatConnected;

            radioButtonChainingGeneralZones.Checked = settings.ChainingGeneralUseZones;
            radioButtonChainingGeneralMaxGap.Checked = !settings.ChainingGeneralUseZones;
            numericUpDownChainingGeneralMaxGap.Value = settings.ChainingGeneralMaxGap;
            numericUpDownChainingGeneralLeftRedZone.Value = settings.ChainingGeneralLeftRedZone;
            numericUpDownChainingGeneralLeftGreenZone.Value = settings.ChainingGeneralLeftGreenZone;
            comboBoxChainingGeneralShotChangeBehavior.SelectedIndex = (int)settings.ChainingGeneralShotChangeBehavior;

            radioButtonChainingInCueOnShotZones.Checked = settings.ChainingInCueOnShotUseZones;
            radioButtonChainingInCueOnShotMaxGap.Checked = !settings.ChainingInCueOnShotUseZones;
            numericUpDownChainingInCueOnShotMaxGap.Value = settings.ChainingInCueOnShotMaxGap;
            numericUpDownChainingInCueOnShotLeftRedZone.Value = settings.ChainingInCueOnShotLeftRedZone;
            numericUpDownChainingInCueOnShotLeftGreenZone.Value = settings.ChainingInCueOnShotLeftGreenZone;
            checkBoxChainingInCueOnShotCheckGeneral.Checked = settings.ChainingInCueOnShotCheckGeneral;

            radioButtonChainingOutCueOnShotZones.Checked = settings.ChainingOutCueOnShotUseZones;
            radioButtonChainingOutCueOnShotMaxGap.Checked = !settings.ChainingOutCueOnShotUseZones;
            numericUpDownChainingOutCueOnShotMaxGap.Value = settings.ChainingOutCueOnShotMaxGap;
            numericUpDownChainingOutCueOnShotRightRedZone.Value = settings.ChainingOutCueOnShotRightRedZone;
            numericUpDownChainingOutCueOnShotRightGreenZone.Value = settings.ChainingOutCueOnShotRightGreenZone;
            checkBoxChainingOutCueOnShotCheckGeneral.Checked = settings.ChainingOutCueOnShotCheckGeneral;

            _loadingClassicProfile = false;
            ValidateZones();
            RefreshControls();
        }

        private void RefreshControls()
        {
            if (_loadingClassicProfile) return;
            // Radio button toggling
            numericUpDownChainingGeneralMaxGap.Enabled = radioButtonChainingGeneralMaxGap.Checked;
            labelChainingGeneralMaxGapSuffix.Enabled = radioButtonChainingGeneralMaxGap.Checked;
            numericUpDownChainingGeneralLeftGreenZone.Enabled = radioButtonChainingGeneralZones.Checked;
            numericUpDownChainingGeneralLeftRedZone.Enabled = radioButtonChainingGeneralZones.Checked;

            numericUpDownChainingInCueOnShotMaxGap.Enabled = radioButtonChainingInCueOnShotMaxGap.Checked;
            labelChainingInCueOnShotMaxGapSuffix.Enabled = radioButtonChainingInCueOnShotMaxGap.Checked;
            numericUpDownChainingInCueOnShotLeftGreenZone.Enabled = radioButtonChainingInCueOnShotZones.Checked;
            numericUpDownChainingInCueOnShotLeftRedZone.Enabled = radioButtonChainingInCueOnShotZones.Checked;

            numericUpDownChainingOutCueOnShotMaxGap.Enabled = radioButtonChainingOutCueOnShotMaxGap.Checked;
            labelChainingOutCueOnShotMaxGapSuffix.Enabled = radioButtonChainingOutCueOnShotMaxGap.Checked;
            numericUpDownChainingOutCueOnShotRightRedZone.Enabled = radioButtonChainingOutCueOnShotZones.Checked;
            numericUpDownChainingOutCueOnShotRightGreenZone.Enabled = radioButtonChainingOutCueOnShotZones.Checked;
            
            // Connected subtitles page toggling
            cuesPreviewViewConnectedSubtitlesInCueClosest.Visible = tabControlConnectedSubtitles.SelectedIndex == 0;
            cuesPreviewViewConnectedSubtitlesOutCueClosest.Visible = tabControlConnectedSubtitles.SelectedIndex == 1;
            
            // Chaining page toggling
            cuesPreviewViewChainingGeneral.Visible = tabControlChaining.SelectedIndex == 0;
            cuesPreviewViewChainingInCueOnShot.Visible = tabControlChaining.SelectedIndex == 1;
            cuesPreviewViewChainingOutCueOnShot.Visible = tabControlChaining.SelectedIndex == 2;

            // Update cue previews
            cuesPreviewViewInCues.RightGap = Convert.ToInt32(numericUpDownInCuesGap.Value);
            cuesPreviewViewInCues.LeftGreenZone = Convert.ToInt32(numericUpDownInCuesLeftGreenZone.Value);
            cuesPreviewViewInCues.LeftRedZone = Convert.ToInt32(numericUpDownInCuesLeftRedZone.Value);
            cuesPreviewViewInCues.RightRedZone = Convert.ToInt32(numericUpDownInCuesRightRedZone.Value);
            cuesPreviewViewInCues.RightGreenZone = Convert.ToInt32(numericUpDownInCuesRightGreenZone.Value);

            cuesPreviewViewOutCues.LeftGap = Convert.ToInt32(numericUpDownOutCuesGap.Value);
            cuesPreviewViewOutCues.LeftGreenZone = Convert.ToInt32(numericUpDownOutCuesLeftGreenZone.Value);
            cuesPreviewViewOutCues.LeftRedZone = Convert.ToInt32(numericUpDownOutCuesLeftRedZone.Value);
            cuesPreviewViewOutCues.RightRedZone = Convert.ToInt32(numericUpDownOutCuesRightRedZone.Value);
            cuesPreviewViewOutCues.RightGreenZone = Convert.ToInt32(numericUpDownOutCuesRightGreenZone.Value);

            cuesPreviewViewConnectedSubtitlesInCueClosest.LeftGap = Convert.ToInt32(numericUpDownConnectedSubtitlesInCueClosestLeftGap.Value);
            cuesPreviewViewConnectedSubtitlesInCueClosest.RightGap = Convert.ToInt32(numericUpDownConnectedSubtitlesInCueClosestRightGap.Value);
            cuesPreviewViewConnectedSubtitlesInCueClosest.LeftGreenZone = Convert.ToInt32(numericUpDownConnectedSubtitlesLeftGreenZone.Value);
            cuesPreviewViewConnectedSubtitlesInCueClosest.LeftRedZone = Convert.ToInt32(numericUpDownConnectedSubtitlesLeftRedZone.Value);
            cuesPreviewViewConnectedSubtitlesInCueClosest.RightRedZone = Convert.ToInt32(numericUpDownConnectedSubtitlesRightRedZone.Value);
            cuesPreviewViewConnectedSubtitlesInCueClosest.RightGreenZone = Convert.ToInt32(numericUpDownConnectedSubtitlesRightGreenZone.Value);

            cuesPreviewViewConnectedSubtitlesOutCueClosest.LeftGap = Convert.ToInt32(numericUpDownConnectedSubtitlesOutCueClosestLeftGap.Value);
            cuesPreviewViewConnectedSubtitlesOutCueClosest.RightGap = Convert.ToInt32(numericUpDownConnectedSubtitlesOutCueClosestRightGap.Value);
            cuesPreviewViewConnectedSubtitlesOutCueClosest.LeftGreenZone = Convert.ToInt32(numericUpDownConnectedSubtitlesLeftGreenZone.Value);
            cuesPreviewViewConnectedSubtitlesOutCueClosest.LeftRedZone = Convert.ToInt32(numericUpDownConnectedSubtitlesLeftRedZone.Value);
            cuesPreviewViewConnectedSubtitlesOutCueClosest.RightRedZone = Convert.ToInt32(numericUpDownConnectedSubtitlesRightRedZone.Value);
            cuesPreviewViewConnectedSubtitlesOutCueClosest.RightGreenZone = Convert.ToInt32(numericUpDownConnectedSubtitlesRightGreenZone.Value);

            cuesPreviewViewChainingGeneral.LeftGreenZone = Convert.ToInt32(numericUpDownChainingGeneralLeftGreenZone.Value);
            cuesPreviewViewChainingGeneral.LeftRedZone = Convert.ToInt32(numericUpDownChainingGeneralLeftRedZone.Value);

            cuesPreviewViewChainingInCueOnShot.RightGap = Convert.ToInt32(numericUpDownInCuesGap.Value);
            cuesPreviewViewChainingInCueOnShot.LeftGreenZone = Convert.ToInt32(numericUpDownChainingInCueOnShotLeftGreenZone.Value);
            cuesPreviewViewChainingInCueOnShot.LeftRedZone = Convert.ToInt32(numericUpDownChainingInCueOnShotLeftRedZone.Value);

            cuesPreviewViewChainingOutCueOnShot.LeftGap = Convert.ToInt32(numericUpDownOutCuesGap.Value);
            cuesPreviewViewChainingOutCueOnShot.RightRedZone = Convert.ToInt32(numericUpDownChainingOutCueOnShotRightRedZone.Value);
            cuesPreviewViewChainingOutCueOnShot.RightGreenZone = Convert.ToInt32(numericUpDownChainingOutCueOnShotRightGreenZone.Value);
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            SaveControlsTo(_workingProfile);
            Configuration.Settings.BeautifyTimeCodes.Profile = ClassicBeautifyProfiles.Clone(_workingProfile);
            DialogResult = DialogResult.OK;
        }

        private void SaveControlsTo(BeautifyTimeCodesSettings.BeautifyTimeCodesProfile target)
        {
            // Remember value for check later on
            var previousGeneralShotChangeBehavior = target.ChainingGeneralShotChangeBehavior;

            // Save settings
            target.Gap = Convert.ToInt32(numericUpDownGap.Value);

            target.InCuesGap = Convert.ToInt32(numericUpDownInCuesGap.Value);
            target.InCuesLeftGreenZone = Convert.ToInt32(numericUpDownInCuesLeftGreenZone.Value);
            target.InCuesLeftRedZone = Convert.ToInt32(numericUpDownInCuesLeftRedZone.Value);
            target.InCuesRightRedZone = Convert.ToInt32(numericUpDownInCuesRightRedZone.Value);
            target.InCuesRightGreenZone = Convert.ToInt32(numericUpDownInCuesRightGreenZone.Value);

            target.OutCuesGap = Convert.ToInt32(numericUpDownOutCuesGap.Value);
            target.OutCuesLeftGreenZone = Convert.ToInt32(numericUpDownOutCuesLeftGreenZone.Value);
            target.OutCuesLeftRedZone = Convert.ToInt32(numericUpDownOutCuesLeftRedZone.Value);
            target.OutCuesRightRedZone = Convert.ToInt32(numericUpDownOutCuesRightRedZone.Value);
            target.OutCuesRightGreenZone = Convert.ToInt32(numericUpDownOutCuesRightGreenZone.Value);

            target.ConnectedSubtitlesInCueClosestLeftGap = Convert.ToInt32(numericUpDownConnectedSubtitlesInCueClosestLeftGap.Value);
            target.ConnectedSubtitlesInCueClosestRightGap = Convert.ToInt32(numericUpDownConnectedSubtitlesInCueClosestRightGap.Value);
            target.ConnectedSubtitlesOutCueClosestLeftGap = Convert.ToInt32(numericUpDownConnectedSubtitlesOutCueClosestLeftGap.Value);
            target.ConnectedSubtitlesOutCueClosestRightGap = Convert.ToInt32(numericUpDownConnectedSubtitlesOutCueClosestRightGap.Value);
            target.ConnectedSubtitlesLeftGreenZone = Convert.ToInt32(numericUpDownConnectedSubtitlesLeftGreenZone.Value);
            target.ConnectedSubtitlesLeftRedZone = Convert.ToInt32(numericUpDownConnectedSubtitlesLeftRedZone.Value);
            target.ConnectedSubtitlesRightRedZone = Convert.ToInt32(numericUpDownConnectedSubtitlesRightRedZone.Value);
            target.ConnectedSubtitlesRightGreenZone = Convert.ToInt32(numericUpDownConnectedSubtitlesRightGreenZone.Value);
            target.ConnectedSubtitlesTreatConnected = Convert.ToInt32(numericUpDownConnectedSubtitlesTreatConnected.Value);

            target.ChainingGeneralUseZones = radioButtonChainingGeneralZones.Checked;
            target.ChainingGeneralMaxGap = Convert.ToInt32(numericUpDownChainingGeneralMaxGap.Value);
            target.ChainingGeneralLeftGreenZone = Convert.ToInt32(numericUpDownChainingGeneralLeftGreenZone.Value);
            target.ChainingGeneralLeftRedZone = Convert.ToInt32(numericUpDownChainingGeneralLeftRedZone.Value);
            target.ChainingGeneralShotChangeBehavior = (BeautifyTimeCodesSettings.BeautifyTimeCodesProfile.ChainingShotChangeBehaviorEnum)comboBoxChainingGeneralShotChangeBehavior.SelectedIndex;

            target.ChainingInCueOnShotUseZones = radioButtonChainingInCueOnShotZones.Checked;
            target.ChainingInCueOnShotMaxGap = Convert.ToInt32(numericUpDownChainingInCueOnShotMaxGap.Value);
            target.ChainingInCueOnShotLeftGreenZone = Convert.ToInt32(numericUpDownChainingInCueOnShotLeftGreenZone.Value);
            target.ChainingInCueOnShotLeftRedZone = Convert.ToInt32(numericUpDownChainingInCueOnShotLeftRedZone.Value);
            target.ChainingInCueOnShotCheckGeneral = checkBoxChainingInCueOnShotCheckGeneral.Checked;

            target.ChainingOutCueOnShotUseZones = radioButtonChainingOutCueOnShotZones.Checked;
            target.ChainingOutCueOnShotMaxGap = Convert.ToInt32(numericUpDownChainingOutCueOnShotMaxGap.Value);
            target.ChainingOutCueOnShotRightRedZone = Convert.ToInt32(numericUpDownChainingOutCueOnShotRightRedZone.Value);
            target.ChainingOutCueOnShotRightGreenZone = Convert.ToInt32(numericUpDownChainingOutCueOnShotRightGreenZone.Value);
            target.ChainingOutCueOnShotCheckGeneral = checkBoxChainingOutCueOnShotCheckGeneral.Checked;

            // Update hidden shot change behavior settings if value for general bahavior has changed
            if (previousGeneralShotChangeBehavior != target.ChainingGeneralShotChangeBehavior)
            {
                target.ChainingInCueOnShotShotChangeBehavior = target.ChainingGeneralShotChangeBehavior;
                target.ChainingOutCueOnShotShotChangeBehavior = target.ChainingGeneralShotChangeBehavior;
            }

        }

        private void ResetSettings(BeautifyTimeCodesSettings.BeautifyTimeCodesProfile.Preset preset)
        {
            if (MessageBox.Show(this, LanguageSettings.Current.BeautifyTimeCodesProfile.ResetWarning, LanguageSettings.Current.General.Warning, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                _workingProfile = new BeautifyTimeCodesSettings.BeautifyTimeCodesProfile(preset);
                LoadSettings();
            }
        }

        private void radioButtonChaining_CheckedChanged(object sender, EventArgs e)
        {
            RefreshControls();
        }

        private void tabControlChaining_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshControls();
        }

        private void numericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (_loadingClassicProfile) return;
            ValidateZones();
            RefreshControls();
        }

        private void tabControlConnectedSubtitles_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshControls();
        }

        private void numericUpDownGap_ValueChanged(object sender, EventArgs e)
        {
            if (_loadingClassicProfile) return;
            if (numericUpDownOutCuesGap.Value > 0)
            {
                numericUpDownOutCuesGap.Value = numericUpDownGap.Value;
            }

            if (numericUpDownConnectedSubtitlesInCueClosestLeftGap.Value > 0 && numericUpDownConnectedSubtitlesInCueClosestRightGap.Value == 0)
            {
                numericUpDownConnectedSubtitlesInCueClosestLeftGap.Value = numericUpDownGap.Value;
            }

            if (numericUpDownConnectedSubtitlesInCueClosestLeftGap.Value == 0 && numericUpDownConnectedSubtitlesInCueClosestRightGap.Value > 0)
            {
                numericUpDownConnectedSubtitlesInCueClosestRightGap.Value = numericUpDownGap.Value;
            }

            if (numericUpDownConnectedSubtitlesOutCueClosestRightGap.Value > 0 && numericUpDownConnectedSubtitlesOutCueClosestLeftGap.Value == 0)
            {
                numericUpDownConnectedSubtitlesOutCueClosestRightGap.Value = numericUpDownGap.Value;
            }

            if (numericUpDownConnectedSubtitlesOutCueClosestRightGap.Value == 0 && numericUpDownConnectedSubtitlesOutCueClosestLeftGap.Value > 0)
            {
                numericUpDownConnectedSubtitlesOutCueClosestLeftGap.Value = numericUpDownGap.Value;
            }
        }

        private void ValidateZones()
        {
            numericUpDownInCuesLeftGreenZone.Minimum = numericUpDownInCuesLeftRedZone.Value;
            numericUpDownInCuesRightGreenZone.Minimum = numericUpDownInCuesRightRedZone.Value;
            numericUpDownOutCuesLeftGreenZone.Minimum = numericUpDownOutCuesLeftRedZone.Value;
            numericUpDownOutCuesRightGreenZone.Minimum = numericUpDownOutCuesRightRedZone.Value;

            numericUpDownConnectedSubtitlesLeftGreenZone.Minimum = numericUpDownConnectedSubtitlesLeftRedZone.Value;
            numericUpDownConnectedSubtitlesRightGreenZone.Minimum = numericUpDownConnectedSubtitlesRightRedZone.Value;

            numericUpDownChainingGeneralLeftGreenZone.Minimum = numericUpDownChainingGeneralLeftRedZone.Value;
            numericUpDownChainingInCueOnShotLeftGreenZone.Minimum = numericUpDownChainingInCueOnShotLeftRedZone.Value;
            numericUpDownChainingOutCueOnShotRightGreenZone.Minimum = numericUpDownChainingOutCueOnShotRightRedZone.Value;
        }

        private void buttonCreateSimple_Click(object sender, EventArgs e)
        {
            using (var form = new BeautifyTimeCodesProfileSimple(_frameRate, ClassicBeautifyProfiles.Clone(_workingProfile)))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    _workingProfile = form.ResultProfile;
                    LoadSettings();
                }
            }
        }

        private void BeautifyTimeCodesProfile_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
            }
            else if (e.KeyData == UiUtil.HelpKeys)
            {
                UiUtil.ShowHelp("#beautify_time_codes");
            }
        }
    }
}