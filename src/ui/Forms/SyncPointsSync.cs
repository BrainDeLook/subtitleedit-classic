using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class SyncPointsSync : PositionAndSizeForm
    {
        public class ListBoxSyncPoint
        {
            public int Index { get; set; }
            public string Text { get; set; }

            public override string ToString()
            {
                return Text;
            }
        }

        private string _subtitleFileName;
        private int _audioTrackNumber;
        private Subtitle _subtitle;
        private Subtitle _originalSubtitle;
        private Subtitle _otherSubtitle;
        private SortedDictionary<int, (TimeSpan Time, int OtherIndex)> _synchronizationPoints = new SortedDictionary<int, (TimeSpan, int)>();
        private readonly Keys _mainGeneralGoToNextSubtitle = UiUtil.GetKeys(Configuration.Settings.Shortcuts.GeneralGoToNextSubtitle);
        private readonly Keys _mainGeneralGoToNextSubtitlePlayTranslate = UiUtil.GetKeys(Configuration.Settings.Shortcuts.GeneralGoToNextSubtitlePlayTranslate);
        private readonly Keys _mainGeneralGoToPrevSubtitle = UiUtil.GetKeys(Configuration.Settings.Shortcuts.GeneralGoToPrevSubtitle);
        private readonly Keys _mainGeneralGoToPrevSubtitlePlayTranslate = UiUtil.GetKeys(Configuration.Settings.Shortcuts.GeneralGoToPrevSubtitlePlayTranslate);

        public string VideoFileName { get; private set; }

        public Subtitle FixedSubtitle => _subtitle;

        public SyncPointsSync()
        {
            UiUtil.PreInitialize(this);
            InitializeComponent();
            UiUtil.FixFonts(this);

            buttonSetSyncPoint.Text = LanguageSettings.Current.PointSync.SetSyncPoint;
            buttonRemoveSyncPoint.Text = LanguageSettings.Current.PointSync.RemoveSyncPoint;
            buttonOK.Text = LanguageSettings.Current.General.Ok;
            buttonApplySync.Text = LanguageSettings.Current.PointSync.ApplySync;
            buttonCancel.Text = LanguageSettings.Current.General.Cancel;
            labelNoOfSyncPoints.Text = string.Format(LanguageSettings.Current.PointSync.SyncPointsX, 0);
            labelSyncInfo.Text = LanguageSettings.Current.PointSync.Info;
            buttonFindText.Text = LanguageSettings.Current.VisualSync.FindText;
            buttonFindTextOther.Text = LanguageSettings.Current.VisualSync.FindText;
            subtitleListView1.InitializeLanguage(LanguageSettings.Current.General, Configuration.Settings);
            subtitleListView2.InitializeLanguage(LanguageSettings.Current.General, Configuration.Settings);
            subtitleListView1.InitializeTimestampColumnWidths(this);
            subtitleListView2.InitializeTimestampColumnWidths(this);
            UiUtil.InitializeSubtitleFont(subtitleListView1);
            UiUtil.InitializeSubtitleFont(subtitleListView2);
            subtitleListView1.AutoSizeAllColumns(this);
            subtitleListView2.AutoSizeAllColumns(this);
            UiUtil.FixLargeFonts(this, buttonOK);
            labelAdjustFactor.Text = string.Empty;
            InitializeClassicPoints();
        }

        public void Initialize(Subtitle subtitle, string subtitleFileName, string videoFileName, int audioTrackNumber)
        {
            Text = LanguageSettings.Current.PointSync.Title;
            labelSubtitleFileName.Text = subtitleFileName;
            _subtitle = new Subtitle(subtitle, false);
            _originalSubtitle = new Subtitle(subtitle, false);
            _subtitleFileName = subtitleFileName;
            VideoFileName = videoFileName;
            _audioTrackNumber = audioTrackNumber;
            subtitleListView1.Fill(subtitle);
            if (subtitleListView1.Items.Count > 0)
            {
                subtitleListView1.Items[0].Selected = true;
            }

            subtitleListView1.Anchor = AnchorStyles.Left;
            buttonSetSyncPoint.Anchor = AnchorStyles.Left;
            buttonRemoveSyncPoint.Anchor = AnchorStyles.Left;
            labelNoOfSyncPoints.Anchor = AnchorStyles.Left;
            listBoxSyncPoints.Anchor = AnchorStyles.Left;
            groupBoxImportResult.Anchor = AnchorStyles.Left;
            labelOtherSubtitleFileName.Visible = false;
            subtitleListView2.Visible = false;
            buttonFindTextOther.Visible = false;
            groupBoxImportResult.Width = listBoxSyncPoints.Left + listBoxSyncPoints.Width + 20;
            Width = groupBoxImportResult.Left + groupBoxImportResult.Width + 15;
            subtitleListView1.Anchor = AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Right;
            buttonSetSyncPoint.Anchor = AnchorStyles.Right;
            buttonRemoveSyncPoint.Anchor = AnchorStyles.Right;
            labelNoOfSyncPoints.Anchor = AnchorStyles.Right;
            listBoxSyncPoints.Anchor = AnchorStyles.Right;
            groupBoxImportResult.Anchor = AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Right;
            buttonFindText.Left = subtitleListView1.Left + subtitleListView1.Width - buttonFindText.Width;
            Width = 900;
            groupBoxImportResult.Width = Width - groupBoxImportResult.Left * 3;
            labelAdjustFactor.Left = listBoxSyncPoints.Left;
            labelAdjustFactor.Anchor = listBoxSyncPoints.Anchor;
            MinimumSize = new Size(Width - 50, MinimumSize.Height);
        }

        public void Initialize(Subtitle subtitle, string subtitleFileName, string videoFileName, int audioTrackNumber, string otherSubtitleFileName, Subtitle otherSubtitle)
        {
            Text = LanguageSettings.Current.PointSync.TitleViaOtherSubtitle;
            labelSubtitleFileName.Text = subtitleFileName;
            _subtitle = new Subtitle(subtitle, false);
            _otherSubtitle = new Subtitle(otherSubtitle, false);
            _originalSubtitle = new Subtitle(subtitle, false);
            _subtitleFileName = subtitleFileName;
            VideoFileName = videoFileName;
            _audioTrackNumber = audioTrackNumber;
            subtitleListView1.Fill(subtitle);
            if (subtitleListView1.Items.Count > 0)
            {
                subtitleListView1.Items[0].Selected = true;
            }

            labelOtherSubtitleFileName.Text = otherSubtitleFileName;
            subtitleListView2.Fill(otherSubtitle);

            subtitleListView1.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            subtitleListView2.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            buttonSetSyncPoint.Anchor = AnchorStyles.Left;
            buttonRemoveSyncPoint.Anchor = AnchorStyles.Left;
            labelNoOfSyncPoints.Anchor = AnchorStyles.Left;
            listBoxSyncPoints.Anchor = AnchorStyles.Left;
            labelAdjustFactor.Anchor = listBoxSyncPoints.Anchor;
            labelOtherSubtitleFileName.Visible = true;
            subtitleListView2.Visible = true;
            buttonFindTextOther.Visible = true;
            Width = Math.Min(1420, Screen.FromControl(this).WorkingArea.Width - 40);
            MinimumSize = new Size(Width - 50, MinimumSize.Height);
        }

        private void RefreshSynchronizationPointsUi()
        {
            buttonOK.Enabled = buttonApplySync.Enabled = !_classicLoading && _synchronizationPoints.Count > 0;
            labelNoOfSyncPoints.Text = string.Format(LanguageSettings.Current.PointSync.SyncPointsX, _synchronizationPoints.Count);

            listBoxSyncPoints.Items.Clear();

            for (var i = 0; i < subtitleListView1.Items.Count; i++)
            {
                if (_synchronizationPoints.ContainsKey(i))
                {
                    var p = new Paragraph { StartTime = { TotalMilliseconds = _synchronizationPoints[i].Time.TotalMilliseconds } };
                    p.EndTime.TotalMilliseconds = p.StartTime.TotalMilliseconds + _subtitle.Paragraphs[i].DurationTotalMilliseconds;
                    subtitleListView1.SetStartTimeAndDuration(i, p, _subtitle.GetParagraphOrDefault(i + 1), _subtitle.GetParagraphOrDefault(i - 1));

                    var item = new ListBoxSyncPoint { Index = i, Text = _subtitle.Paragraphs[i].Number + " - " + p.StartTime };
                    listBoxSyncPoints.Items.Add(item);
                    subtitleListView1.SetNumber(i, "* * * *");
                }
                else
                {
                    subtitleListView1.SetNumber(i, _subtitle.Paragraphs[i].Number.ToString(CultureInfo.InvariantCulture));
                    subtitleListView1.SetStartTimeAndDuration(i, _subtitle.Paragraphs[i], _subtitle.GetParagraphOrDefault(i + 1), _subtitle.GetParagraphOrDefault(i - 1));
                }
            }

            SetViewColors(_otherSubtitle, subtitleListView2, _synchronizationPoints.Values.Select(v => v.OtherIndex), GetSelectedParagraph(_subtitle, subtitleListView1));
            SetViewColors(_subtitle, subtitleListView1, _synchronizationPoints.Keys, GetSelectedParagraph(_otherSubtitle, subtitleListView2));
            UpdateClassicGaps();
        }

        private void buttonSetSyncPoint_Click(object sender, EventArgs e)
        {
            if (subtitleListView2.Visible)
            {
                SetSyncPointViaOtherSubtitle();
            }
            else
            {
                SetClassicVideoPoint(true);
            }

            SetSyncFactorLabel();
        }

        private void SetSyncPointViaOtherSubtitle()
        {
            if (subtitleListView1.SelectedItems.Count != 1 || subtitleListView2.SelectedItems.Count != 1)
            {
                return;
            }

            if (_otherSubtitle != null && subtitleListView2.SelectedItems.Count == 1)
            {
                var index = subtitleListView1.SelectedItems[0].Index;
                var indexOther = subtitleListView2.SelectedItems[0].Index;

                _synchronizationPoints[index] = (TimeSpan.FromMilliseconds(_otherSubtitle.Paragraphs[indexOther].StartTime.TotalMilliseconds), indexOther);

                RefreshSynchronizationPointsUi();
            }

            SetSyncFactorLabel();
        }

        private void buttonRemoveSyncPoint_Click(object sender, EventArgs e)
        {
            if (listBoxSyncPoints.SelectedItem is ListBoxSyncPoint selectedPoint) { RemoveClassicPoint(); return; }
            if (subtitleListView1.SelectedItems.Count == 1 && _subtitle != null)
            {
                var index = subtitleListView1.SelectedItems[0].Index;
                if (_synchronizationPoints.ContainsKey(index))
                {
                    _synchronizationPoints.Remove(index);
                }

                RefreshSynchronizationPointsUi();
            }

            SetSyncFactorLabel();
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (!buttonApplySync.Enabled || ClassicPointSync.Apply(_originalSubtitle, _synchronizationPoints) == null) return;
            if (buttonApplySync.Enabled)
            {
                buttonSync_Click(null, null);
            }

            DialogResult = DialogResult.OK;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void SyncPointsSync_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
            }
            else if (e.KeyData == UiUtil.HelpKeys)
            {
                UiUtil.ShowHelp("#sync");
                e.SuppressKeyPress = true;
            }
            else if (_mainGeneralGoToNextSubtitle == e.KeyData || _mainGeneralGoToNextSubtitlePlayTranslate == e.KeyData)
            {
                var selectedIndex = 0;
                if (subtitleListView1.SelectedItems.Count > 0)
                {
                    selectedIndex = subtitleListView1.SelectedItems[0].Index;
                    selectedIndex++;
                }

                subtitleListView1.SelectIndexAndEnsureVisible(selectedIndex);
                e.SuppressKeyPress = true;
            }
            else if (_mainGeneralGoToPrevSubtitle == e.KeyData || _mainGeneralGoToPrevSubtitlePlayTranslate == e.KeyData)
            {
                var selectedIndex = 0;
                if (subtitleListView1.SelectedItems.Count > 0)
                {
                    selectedIndex = subtitleListView1.SelectedItems[0].Index;
                    selectedIndex--;
                }

                subtitleListView1.SelectIndexAndEnsureVisible(selectedIndex);
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == (Keys.Control | Keys.G))
            {
                var subView = subtitleListView1;
                if (subtitleListView2 != null && subtitleListView2.Visible && !subtitleListView1.Focused)
                {
                    var x = PointToClient(MousePosition).X;
                    if (x >= subtitleListView2.Left && x <= subtitleListView2.Left + subtitleListView2.Width)
                    {
                        subView = subtitleListView2;
                    }
                }

                using (var gotoForm = new GoToLine())
                {
                    gotoForm.Initialize(1, subView.Items.Count);
                    if (gotoForm.ShowDialog() == DialogResult.OK)
                    {
                        subView.SelectIndexAndEnsureVisible(gotoForm.LineNumber - 1, true);
                    }
                }
            }
        }

        private void SetSyncFactorLabel()
        {
            labelAdjustFactor.Text = string.Empty;
            if (_synchronizationPoints.Count == 1)
            {
                var startPos = _synchronizationPoints.First().Value.Time.TotalMilliseconds / TimeCode.BaseUnit;
                var subStart = _originalSubtitle.Paragraphs[_synchronizationPoints.First().Key].StartTime.TotalMilliseconds / TimeCode.BaseUnit;

                var adjustment = startPos - subStart;
                labelAdjustFactor.Text = $"{adjustment:+0.000;-0.000}";
            }
            else if (_synchronizationPoints.Count == 2)
            {
                var startPos = _synchronizationPoints.First().Value.Time.TotalMilliseconds / TimeCode.BaseUnit;
                var endPos = _synchronizationPoints.Last().Value.Time.TotalMilliseconds / TimeCode.BaseUnit;

                var subStart = _originalSubtitle.Paragraphs[_synchronizationPoints.First().Key].StartTime.TotalMilliseconds / TimeCode.BaseUnit;
                var subEnd = _originalSubtitle.Paragraphs[_synchronizationPoints.Last().Key].StartTime.TotalMilliseconds / TimeCode.BaseUnit;

                var subDiff = subEnd - subStart;
                var realDiff = endPos - startPos;

                // speed factor
                var factor = Math.Abs(subDiff) < 0.001 ? 1 : realDiff / subDiff;

                // adjust to starting position
                var adjust = startPos - subStart * factor;

                labelAdjustFactor.Text = $"*{factor:0.000}, {adjust:+0.000;-0.000}";
            }
        }

        private void buttonSync_Click(object sender, EventArgs e)
        {
            var result = ClassicPointSync.Apply(_originalSubtitle, _synchronizationPoints);
            if (result == null)
            {
                MessageBox.Show(this, SP("Проверь порядок точек и границы тайм-кодов. Изменения не применены.", "Check the order of the points and time-code bounds. No changes were applied."), Text);
                return;
            }
            _subtitle = result;
            subtitleListView1.Fill(_subtitle);
            RefreshSynchronizationPointsUi();
        }
        private void listBoxSyncPoints_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxSyncPoints.SelectedIndex >= 0)
            {
                var item = (ListBoxSyncPoint)listBoxSyncPoints.Items[listBoxSyncPoints.SelectedIndex];
                subtitleListView1.SelectIndexAndEnsureVisible(item.Index);
            }
        }

        private void SubtitleListView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (subtitleListView1.SelectedItems.Count == 1)
            {
                var index = subtitleListView1.SelectedItems[0].Index;
                if (_synchronizationPoints.ContainsKey(index))
                {
                    buttonRemoveSyncPoint_Click(null, null);
                }
                else
                {
                    buttonSetSyncPoint_Click(null, null);
                }
            }
        }

        private void SyncPointsSyncResize(object sender, EventArgs e)
        {
            if (subtitleListView2.Visible)
            {
                var widthInMiddle = listBoxSyncPoints.Width;
                subtitleListView1.Width = (groupBoxImportResult.Width - widthInMiddle) / 2 - 12;
                subtitleListView2.Width = subtitleListView1.Width;
                subtitleListView2.Left = subtitleListView1.Left + subtitleListView1.Width + widthInMiddle + 10;
                listBoxSyncPoints.Left = subtitleListView1.Left + subtitleListView1.Width + 5;
                buttonSetSyncPoint.Left = listBoxSyncPoints.Left;
                buttonRemoveSyncPoint.Left = listBoxSyncPoints.Left;
                labelAdjustFactor.Left = listBoxSyncPoints.Left;
                labelNoOfSyncPoints.Left = listBoxSyncPoints.Left;
                labelOtherSubtitleFileName.Left = subtitleListView2.Left;
                buttonFindText.Left = subtitleListView1.Left + subtitleListView1.Width - buttonFindText.Width;
            }
        }

        private void SyncPointsSyncShown(object sender, EventArgs e)
        {
            SyncPointsSyncResize(null, null);
        }

        private void ButtonFindTextClick(object sender, EventArgs e)
        {
            using (var findSubtitle = new FindSubtitleLine())
            {
                findSubtitle.Initialize(_subtitle.Paragraphs, string.Empty);
                findSubtitle.ShowDialog();
                if (findSubtitle.SelectedIndex >= 0)
                {
                    subtitleListView1.SelectIndexAndEnsureVisible(findSubtitle.SelectedIndex);
                }
            }
        }

        private void ButtonFindTextOtherClick(object sender, EventArgs e)
        {
            using (var findSubtitle = new FindSubtitleLine())
            {
                findSubtitle.Initialize(_otherSubtitle.Paragraphs, string.Empty);
                findSubtitle.ShowDialog();
                if (findSubtitle.SelectedIndex >= 0)
                {
                    subtitleListView2.SelectIndexAndEnsureVisible(findSubtitle.SelectedIndex);
                }
            }
        }

        private void SubtitleListview1_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetViewColors(_otherSubtitle, subtitleListView2, _synchronizationPoints.Values.Select(v => v.OtherIndex), GetSelectedParagraph(_subtitle, subtitleListView1));
        }

        private void subtitleListView2_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetViewColors(_subtitle, subtitleListView1, _synchronizationPoints.Keys, GetSelectedParagraph(_otherSubtitle, subtitleListView2));
            UpdateClassicGaps();
        }

        private static Paragraph GetSelectedParagraph(Subtitle subtitle, SubtitleListView view)
        {
            var selectedIndex = view.SelectedIndex;
            return subtitle == null || selectedIndex < 0 || selectedIndex >= subtitle.Paragraphs.Count ? null : subtitle.Paragraphs[selectedIndex];
        }


        private void SetViewColors(Subtitle subtitle, SubtitleListView view, IEnumerable<int> marked, Paragraph syncParagraph = null)
        {
            if (!view.Visible || subtitle == null) return;
            var markedHashSet = marked.Where(v => v != SubtitleListView.InvalidIndex).ToHashSet();
            if (syncParagraph is null)
            {
                for (var i = 0; i < view.Items.Count; i++)
                {
                    view.SetBackgroundColor(i, CalculateBackgroundColor(view.BackColor, markedHashSet.Contains(i), 1));
                    view.SetForegroundColor(i, CalculateForegroundColor(view.ForeColor, view.GetBackgroundColor(i).Luminance(), 1));
                }
                return;
            }

            var selectedLocation = syncParagraph.StartTime.TotalMilliseconds;
            var selectedDuration = syncParagraph.DurationTotalMilliseconds;
            if (selectedDuration == 0)
                selectedDuration = 1000;


            (int Index, double Dinstance) closesIndex = (SubtitleListView.InvalidIndex, 0d);
            for (var i = 0; i < view.Items.Count; i++)
            {
                var distanceFromSelected = Math.Abs(subtitle.Paragraphs[i].StartTime.TotalMilliseconds - selectedLocation);
                var percentageDistance = distanceFromSelected / 5 / selectedDuration;
                view.SetBackgroundColor(i, CalculateBackgroundColor(view.BackColor, markedHashSet.Contains(i), percentageDistance));
                view.SetForegroundColor(i, CalculateForegroundColor(view.ForeColor, view.GetBackgroundColor(i).Luminance(), percentageDistance));
                if (closesIndex.Dinstance > distanceFromSelected || closesIndex.Index == SubtitleListView.InvalidIndex)
                {
                    closesIndex = (i, distanceFromSelected);
                }
            }

            if (closesIndex.Index == SubtitleListView.InvalidIndex)
                return;
            view.SetBackgroundColor(closesIndex.Index, CalculateBackgroundColor(view.BackColor, markedHashSet.Contains(closesIndex.Index), 0));
            view.SetForegroundColor(closesIndex.Index, CalculateForegroundColor(view.ForeColor, view.GetBackgroundColor(closesIndex.Index).Luminance(), 0));
            view.Items[closesIndex.Index].EnsureVisible();
        }
        private Color MarkedBackgroundColor { get; } = ColorTranslator.FromHtml("#6ebe6e");
        private Color VisualMarkBackgroundColor { get; } = Configuration.Settings.General.UseDarkTheme ? Color.LightGray : Color.DarkGray;

        private Color CalculateBackgroundColor(Color baseColor, bool shouldUseSyncColor, double percentageDistance)
        {
            var color = shouldUseSyncColor ? MarkedBackgroundColor : baseColor;
            if (percentageDistance >= 1) return color;

            var visualMarkedColorWithOffset = VisualMarkBackgroundColor.Blend(baseColor, percentageDistance);
            return shouldUseSyncColor
                ? visualMarkedColorWithOffset.Blend(MarkedBackgroundColor)
                : visualMarkedColorWithOffset;
        }

        private static Color CalculateForegroundColor(Color baseColor, double backGroundColorLuminance, double percentageDistance)
        {
            if (percentageDistance >= 1)
                return baseColor;
            if (backGroundColorLuminance < 0.5)
            {
                return Color.White;
            }

            return Color.Black;
        }
    }
}
