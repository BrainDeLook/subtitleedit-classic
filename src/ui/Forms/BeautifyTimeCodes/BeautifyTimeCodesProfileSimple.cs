using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Forms;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Settings;

namespace Nikse.SubtitleEdit.Forms.BeautifyTimeCodes
{
    public sealed partial class BeautifyTimeCodesProfileSimple : Form
    {
        private readonly double _frameRate;

        public BeautifyTimeCodesSettings.BeautifyTimeCodesProfile ResultProfile { get; }

        public BeautifyTimeCodesProfileSimple(double frameRate, BeautifyTimeCodesSettings.BeautifyTimeCodesProfile profile)
        {
            _frameRate = frameRate;
            ResultProfile = profile;

            UiUtil.PreInitialize(this);
            InitializeComponent();
            UiUtil.FixFonts(this);

            var language = LanguageSettings.Current.BeautifyTimeCodesProfile;
            Text = language.CreateSimpleTitle;
            labelInstructions.Text = language.CreateSimpleInstruction;
            labelGap.Text = language.Gap;
            labelGapSuffix.Text = language.Frames;
            labelGapInstruction.Text = language.CreateSimpleGapInstruction;
            labelInCues.Text = language.CreateSimpleInCues;
            comboBoxInCues.Items.Clear();
            comboBoxInCues.Items.Add(language.CreateSimpleInCues0Frames);
            comboBoxInCues.Items.Add(language.CreateSimpleInCues1Frames);
            comboBoxInCues.Items.Add(language.CreateSimpleInCues2Frames);
            comboBoxInCues.Items.Add(language.CreateSimpleInCues3Frames);
            comboBoxInCues.SelectedIndex = 0;
            labelOutCues.Text = language.CreateSimpleOutCues;
            comboBoxOutCues.Items.Clear();
            comboBoxOutCues.Items.Add(language.CreateSimpleOutCues0Frames);
            comboBoxOutCues.Items.Add(language.CreateSimpleOutCues1Frames);
            comboBoxOutCues.Items.Add(language.CreateSimpleOutCues2Frames);
            comboBoxOutCues.Items.Add(language.CreateSimpleOutCues3Frames);
            comboBoxOutCues.Items.Add(language.CreateSimpleOutCuesGap);
            comboBoxOutCues.SelectedIndex = 0;
            checkBoxSnapClosestCue.Text = language.CreateSimpleSnapClosestCue;
            checkBoxSnapClosestCue.Checked = true;
            labelOffset.Text = language.CreateSimpleMaxOffset;
            labelOffsetSuffix.Text = language.Frames;
            labelOffsetInstruction.Text = language.CreateSimpleMaxOffsetInstruction;
            labelSafeZone.Text = language.CreateSimpleSafeZone;
            labelSafeZoneSuffix.Text = language.Frames;
            labelSafeZoneInstruction.Text = language.CreateSimpleSafeZoneInstruction;
            labelChainingGap.Text = language.CreateSimpleChainingGap;
            labelChainingGapSuffix.Text = language.Milliseconds;
            labelChainingGapInstruction.Text = language.CreateSimpleChainingGapInstruction;
            checkBoxChainingGapAfterShotChanges.Text = language.CreateSimpleChainingGapAfterShotChanges;
            labelChainingGapAfterShotChangesPrefix.Text = language.Maximum;
            labelChainingGapAfterShotChangesSuffix.Text = language.Milliseconds;
            toolTipChaining.SetToolTip(pictureBoxChainingInfo, language.CreateSimpleChainingToolTip);
            buttonLoadNetflixRules.Text = language.CreateSimpleLoadNetflixRules;

            labelGapHint.Left = labelGapSuffix.Left + labelGapSuffix.Width + 15;
            numericUpDownChainingGapAfterShotChanges.Left = labelChainingGapAfterShotChangesPrefix.Left + labelChainingGapAfterShotChangesPrefix.Width + 10;
            labelChainingGapAfterShotChangesSuffix.Left = numericUpDownChainingGapAfterShotChanges.Left + numericUpDownChainingGapAfterShotChanges.Width + 6;

            buttonOK.Text = LanguageSettings.Current.General.Ok;
            buttonCancel.Text = LanguageSettings.Current.General.Cancel;
            UiUtil.FixLargeFonts(this, buttonOK);

            RefreshControls();
        }

        private void RefreshControls()
        {
            numericUpDownGap_ValueChanged(numericUpDownGap, EventArgs.Empty);
            checkBoxChainingGapAfterShotChanges_CheckedChanged(checkBoxChainingGapAfterShotChanges, EventArgs.Empty);
            comboBoxInCues_SelectedIndexChanged(comboBoxInCues, EventArgs.Empty);
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            // Validation
            if (numericUpDownSafeZone.Value < numericUpDownOffset.Value)
            {
                MessageBox.Show(this, LanguageSettings.Current.BeautifyTimeCodesProfile.OffsetSafeZoneError, LanguageSettings.Current.General.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
                return;
            }

            // Save settings
            int gap;
            try
            {
                gap = Convert.ToInt32(numericUpDownGap.Value);
            }
            catch (Exception exception)
            {
                throw new Exception($"Unable to set gap from numericUpDownGap.Value to Int32: numericUpDownGap.Value = '{numericUpDownGap.Value}'", exception);
            }

            var inCuesGap = comboBoxInCues.SelectedIndex;
            var outCuesGap = comboBoxOutCues.SelectedIndex;
            if (comboBoxOutCues.SelectedIndex == comboBoxOutCues.Items.Count - 1)
            {
                outCuesGap = gap;
            }

            int redZone;
            try
            {
                redZone = Convert.ToInt32(numericUpDownOffset.Value);
            }
            catch (Exception exception)
            {
                throw new Exception($"Unable to set redZone from numericUpDownOffset.Value to Int32: numericUpDownOffset.Value = '{numericUpDownOffset.Value}'", exception);
            }

            int greenZone;
            try
            {
                greenZone = Convert.ToInt32(numericUpDownSafeZone.Value);
            }
            catch (Exception exception)
            {
                throw new Exception($"Unable to set greenZone from numericUpDownSafeZone.Value to Int32: numericUpDownSafeZone.Value = '{numericUpDownSafeZone.Value}'", exception);
            }


            ResultProfile.Gap = gap;

            ResultProfile.InCuesGap = inCuesGap;
            ResultProfile.InCuesLeftGreenZone = greenZone;
            ResultProfile.InCuesLeftRedZone = redZone;
            ResultProfile.InCuesRightRedZone = redZone;
            ResultProfile.InCuesRightGreenZone = greenZone;

            ResultProfile.OutCuesGap = outCuesGap;
            ResultProfile.OutCuesLeftGreenZone = greenZone;
            ResultProfile.OutCuesLeftRedZone = redZone;
            ResultProfile.OutCuesRightRedZone = redZone;
            ResultProfile.OutCuesRightGreenZone = greenZone;

            ResultProfile.ConnectedSubtitlesInCueClosestLeftGap = Math.Max(gap, outCuesGap);
            ResultProfile.ConnectedSubtitlesInCueClosestRightGap = inCuesGap;
            ResultProfile.ConnectedSubtitlesOutCueClosestLeftGap = checkBoxSnapClosestCue.Checked ? inCuesGap : Math.Max(gap, outCuesGap);
            ResultProfile.ConnectedSubtitlesOutCueClosestRightGap = checkBoxSnapClosestCue.Checked ? Math.Max(gap, outCuesGap) : inCuesGap;
            ResultProfile.ConnectedSubtitlesLeftGreenZone = greenZone;
            ResultProfile.ConnectedSubtitlesLeftRedZone = redZone;
            ResultProfile.ConnectedSubtitlesRightRedZone = redZone;
            ResultProfile.ConnectedSubtitlesRightGreenZone = greenZone;


            double treadConnectedMs = 0;
            try
            {
                treadConnectedMs = Math.Round(SubtitleFormat.FramesToMilliseconds(gap, _frameRate) * 1.5);
            }
            catch (Exception exception)
            {
                SeLogger.Error(exception, "Error when executing: treadConnectedMs = Math.Round(SubtitleFormat.FramesToMilliseconds(gap, _frameRate) * 1.5); );");
                SeLogger.Error("BeautifyTimeCodesProfileSimple.buttonOK_Click: Gap is " + gap);
                SeLogger.Error("BeautifyTimeCodesProfileSimple.buttonOK_Click: FrameRate is " + _frameRate);
                SeLogger.Error("BeautifyTimeCodesProfileSimple.buttonOK_Click: treadConnectedMs is " + treadConnectedMs);
                throw new Exception($"Unable to calculate treadConnectedMs from gap '{gap}' and frame rate {_frameRate}", exception);
            }

            try
            {
                ResultProfile.ConnectedSubtitlesTreatConnected = Convert.ToInt32(treadConnectedMs);
            }
            catch (Exception exception)
            {
                SeLogger.Error(exception, "Error when executing: ResultProfile.ConnectedSubtitlesTreatConnected = Convert.ToInt32(treadConnectedMs);");
                SeLogger.Error("BeautifyTimeCodesProfileSimple.buttonOK_Click: Gap is " + gap);
                SeLogger.Error("BeautifyTimeCodesProfileSimple.buttonOK_Click: FrameRate is " + _frameRate);
                SeLogger.Error("BeautifyTimeCodesProfileSimple.buttonOK_Click: treadConnectedMs is " + treadConnectedMs);
                throw new Exception($"Unable to set ConnectedSubtitlesTreatConnected from treadConnectedMs '{treadConnectedMs}'", exception);
            }

            ResultProfile.ChainingGeneralUseZones = false;
            ResultProfile.ChainingGeneralMaxGap = Convert.ToInt32(numericUpDownChainingGap.Value);
            ResultProfile.ChainingGeneralLeftGreenZone = GetChainingZoneFrames(ResultProfile.ChainingGeneralMaxGap);
            ResultProfile.ChainingGeneralLeftRedZone = GetChainingZoneFrames(ResultProfile.ChainingGeneralMaxGap) - 1;
            ResultProfile.ChainingGeneralShotChangeBehavior = BeautifyTimeCodesSettings.BeautifyTimeCodesProfile.ChainingShotChangeBehaviorEnum.ExtendUntilShotChange;

            ResultProfile.ChainingInCueOnShotUseZones = false;
            ResultProfile.ChainingInCueOnShotMaxGap = Convert.ToInt32(numericUpDownChainingGap.Value);
            ResultProfile.ChainingInCueOnShotLeftGreenZone = GetChainingZoneFrames(ResultProfile.ChainingInCueOnShotMaxGap);
            ResultProfile.ChainingInCueOnShotLeftRedZone = GetChainingZoneFrames(ResultProfile.ChainingInCueOnShotMaxGap) - 1;

            ResultProfile.ChainingOutCueOnShotUseZones = false;
            ResultProfile.ChainingOutCueOnShotMaxGap = checkBoxChainingGapAfterShotChanges.Checked ? Convert.ToInt32(numericUpDownChainingGapAfterShotChanges.Value) : Convert.ToInt32(numericUpDownChainingGap.Value);
            ResultProfile.ChainingOutCueOnShotRightRedZone = GetChainingZoneFrames(ResultProfile.ChainingOutCueOnShotMaxGap) - 1;
            ResultProfile.ChainingOutCueOnShotRightGreenZone = GetChainingZoneFrames(ResultProfile.ChainingOutCueOnShotMaxGap);

            DialogResult = DialogResult.OK;
        }

        private int GetChainingZoneFrames(double valueMs)
        {
            var frameDurationMs = TimeCodesBeautifierUtils.GetFrameDurationMs(_frameRate);
            return Convert.ToInt32(Math.Floor(valueMs / frameDurationMs));
        }

        private void numericUpDownGap_ValueChanged(object sender, EventArgs e)
        {
            var gapFrames = Convert.ToInt32(numericUpDownGap.Value);
            double gapMs = SubtitleFormat.FramesToMilliseconds(gapFrames, _frameRate);

            labelGapHint.Text = string.Format(LanguageSettings.Current.BeautifyTimeCodesProfile.GapInMsFormat, Math.Round(gapMs), Math.Round(_frameRate, 3));
        }

        private void checkBoxChainingGapAfterShotChanges_CheckedChanged(object sender, EventArgs e)
        {
            labelChainingGapAfterShotChangesPrefix.Enabled = checkBoxChainingGapAfterShotChanges.Checked;
            numericUpDownChainingGapAfterShotChanges.Enabled = checkBoxChainingGapAfterShotChanges.Checked;
            labelChainingGapAfterShotChangesSuffix.Enabled = checkBoxChainingGapAfterShotChanges.Checked;
        }

        private void comboBoxInCues_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxInCues.SelectedIndex != 0 || comboBoxOutCues.SelectedIndex != 0)
            {
                checkBoxSnapClosestCue.Checked = false;
            }
        }

        private void buttonLoadNetflixRules_Click(object sender, EventArgs e)
        {
            numericUpDownGap.Value = 2;
            comboBoxInCues.SelectedIndex = 0;
            comboBoxOutCues.SelectedIndex = 2;
            checkBoxSnapClosestCue.Checked = false;
            numericUpDownOffset.Value = 7;
            numericUpDownSafeZone.Value = 12;
            numericUpDownChainingGap.Value = 500;
            checkBoxChainingGapAfterShotChanges.Checked = false;

            RefreshControls();
        }

        private void BeautifyTimeCodesProfileSimple_KeyDown(object sender, KeyEventArgs e)
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
