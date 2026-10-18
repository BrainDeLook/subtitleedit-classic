using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class SyncPointsSync
    {
        public WavePeakData SharedWavePeaks { get; set; }
        public string SharedWaveVideo { get; set; }
        private ClassicSyncReference _classicReference;
        private Button _classicBrowse, _classicViaVideo;
        private SubtitleListView _classicFocused;
        private CancellationTokenSource _classicLoad;
        private bool _classicLoading, _classicOther;
        private ImageList _classicImageHeight;
        private Bitmap _classicRowPixel;
        private int _classicImageSize = 64;
        private readonly Dictionary<int, Bitmap> _classicImages = new Dictionary<int, Bitmap>();
        private bool _classicCloseAfterLoad;
        private void ClearClassicImages() { foreach (var image in _classicImages.Values) image?.Dispose(); _classicImages.Clear(); }
        private static string SP(string ru, string en) => Configuration.Settings.General.Language.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? ru : en;

        private void InitializeClassicPoints()
        {
            buttonOK.Enabled = buttonApplySync.Enabled = false;
            _classicFocused = subtitleListView1;
            subtitleListView1.Enter += (s, e) => _classicFocused = subtitleListView1;
            subtitleListView2.Enter += (s, e) => _classicFocused = subtitleListView2;
            listBoxSyncPoints.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back) { RemoveClassicPoint(); e.SuppressKeyPress = true; } };
            var menu = new ContextMenuStrip(); menu.Items.Add(LanguageSettings.Current.PointSync.RemoveSyncPoint, null, (s, e) => RemoveClassicPoint()); listBoxSyncPoints.ContextMenuStrip = menu;
            _classicBrowse = new Button { Text = SP("Открыть эталон…", "Open reference…"), Width = 140, Height = 27, Visible = false };
            _classicViaVideo = new Button { Text = SP("Точка по видео…", "Point via video…"), Width = 140, Height = 27, Visible = false };
            groupBoxImportResult.Controls.Add(_classicBrowse); groupBoxImportResult.Controls.Add(_classicViaVideo);
            _classicBrowse.Click += async (s, e) => await BrowseClassicReference();
            _classicViaVideo.Click += (s, e) => SetClassicVideoPoint(false);
            Resize += (s, e) => LayoutClassicPoints(); Shown += (s, e) => { LayoutClassicPoints(); UpdateClassicGaps(); };
            subtitleListView2.DrawSubItem += DrawClassicReferenceImage;
            FormClosing += (s, e) => { if (_classicLoading) { _classicCloseAfterLoad = true; _classicLoad.Cancel(); e.Cancel = true; } };
            FormClosed += (s, e) => { _classicLoad?.Dispose(); _classicImageHeight?.Dispose(); _classicRowPixel?.Dispose(); ClearClassicImages(); _classicReference?.Dispose(); menu.Dispose(); };
        }
        private void LayoutClassicPoints()
        {
            if (_classicBrowse == null) return;
            if (_classicOther)
            {
                var width = Math.Max(220, (groupBoxImportResult.ClientSize.Width - listBoxSyncPoints.Width - 36) / 2);
                subtitleListView1.Width = subtitleListView2.Width = width;
                listBoxSyncPoints.Left = subtitleListView1.Right + 6;
                subtitleListView2.Left = listBoxSyncPoints.Right + 6;
                buttonSetSyncPoint.Left = buttonRemoveSyncPoint.Left = labelAdjustFactor.Left = labelNoOfSyncPoints.Left = listBoxSyncPoints.Left;
                labelOtherSubtitleFileName.Left = subtitleListView2.Left;
                buttonFindText.Left = subtitleListView1.Right - buttonFindText.Width;
                buttonFindTextOther.Left = subtitleListView2.Right - buttonFindTextOther.Width;
            }
            _classicBrowse.Visible = _classicViaVideo.Visible = _classicOther;
            _classicBrowse.SetBounds(listBoxSyncPoints.Left, 40, listBoxSyncPoints.Width, 27);
            _classicViaVideo.SetBounds(listBoxSyncPoints.Left, 74, listBoxSyncPoints.Width, 27);
            labelOtherSubtitleFileName.AutoSize = false;
            labelOtherSubtitleFileName.Width = subtitleListView2.Width;
            labelOtherSubtitleFileName.AutoEllipsis = true;
            if (_classicOther)
            {
                foreach (var view in new[] { subtitleListView1, subtitleListView2 })
                {
                    if (view.ColumnIndexNumber >= 0) view.Columns[view.ColumnIndexNumber].Width = 42;
                    if (view.ColumnIndexStart >= 0) view.Columns[view.ColumnIndexStart].Width = 90;
                    if (view.ColumnIndexEnd >= 0) view.Columns[view.ColumnIndexEnd].Width = 90;
                    if (view.ColumnIndexDuration >= 0) view.Columns[view.ColumnIndexDuration].Width = 58;
                    if (view.ColumnIndexGap >= 0) view.Columns[view.ColumnIndexGap].Width = 85;
                    if (view.ColumnIndexText >= 0) view.Columns[view.ColumnIndexText].Width = Math.Max(180, view.ClientSize.Width - 368);
                }
            }
        }
        internal void SetClassicReference(ClassicSyncReference reference, string filename)
        {
            ClearClassicImages(); if (_classicReference != reference) _classicReference?.Dispose(); _classicOther = true; _classicReference = reference; _otherSubtitle = new Subtitle(reference.Subtitle, false);
            _synchronizationPoints.Clear(); labelOtherSubtitleFileName.Text = filename;
            subtitleListView1.ShowGapColumn(SP("Пауза после", "Gap after")); subtitleListView2.ShowGapColumn(SP("Пауза после", "Gap after"));
            subtitleListView1.Fill(_subtitle); subtitleListView2.Fill(_otherSubtitle);
            buttonFindTextOther.Enabled = !reference.IsImageBased;
            SetClassicImageHeight(); RefreshSynchronizationPointsUi(); LayoutClassicPoints();
        }
        private async Task BrowseClassicReference()
        {
            using (var dialog = new OpenFileDialog { Filter = ClassicSyncReference.Filter, Title = SP("Открыть эталонные субтитры", "Open reference subtitles") })
                if (dialog.ShowDialog(this) == DialogResult.OK) await LoadClassicReference(dialog.FileName);
        }
        internal async Task<bool> LoadClassicReference(string filename)
        {
            if (_classicLoading) return false;
            _classicLoad?.Dispose(); _classicLoad = new CancellationTokenSource(); _classicLoading = true;
            _classicBrowse.Enabled = buttonSetSyncPoint.Enabled = _classicViaVideo.Enabled = buttonOK.Enabled = buttonApplySync.Enabled = false;
            UseWaitCursor = true;
            try
            {
                var reference = await ClassicSyncReference.LoadAsync(this, filename, _classicLoad.Token);
                _classicLoad.Token.ThrowIfCancellationRequested();
                if (reference == null) return false;
                if (reference.Subtitle.Paragraphs.Count == 0) throw new InvalidDataException(LanguageSettings.Current.Main.NoSubtitlesFound);
                SetClassicReference(reference, filename); return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            finally { _classicLoading = false; UseWaitCursor = false; _classicBrowse.Enabled = buttonSetSyncPoint.Enabled = _classicViaVideo.Enabled = true; RefreshSynchronizationPointsUi(); if (_classicCloseAfterLoad) { DialogResult = DialogResult.Cancel; Close(); } }
        }
        private void SetClassicVideoPoint(bool wave)
        {
            if (_subtitle == null || subtitleListView1.SelectedItems.Count != 1) return;
            var index = subtitleListView1.SelectedItems[0].Index;
            using (var dialog = new SetSyncPoint { SharedWavePeaks = wave ? SharedWavePeaks : null, SharedWaveVideo = SharedWaveVideo })
            {
                dialog.Initialize(_subtitle, _subtitleFileName, index, VideoFileName, _audioTrackNumber);
                if (dialog.ShowDialog(this) == DialogResult.OK) { _synchronizationPoints[index] = (dialog.SynchronizationPoint, SubtitleListView.InvalidIndex); RefreshSynchronizationPointsUi(); SetSyncFactorLabel(); }
                VideoFileName = dialog.VideoFileName;
            }
        }
        private void RemoveClassicPoint()
        {
            if (listBoxSyncPoints.SelectedItem is ListBoxSyncPoint point) _synchronizationPoints.Remove(point.Index);
            RefreshSynchronizationPointsUi(); SetSyncFactorLabel();
        }
        private static void FillClassicGaps(SubtitleListView view, Subtitle source)
        {
            if (source == null || view.ColumnIndexGap < 0) return;
            for (var i = 0; i < source.Paragraphs.Count && i < view.Items.Count; i++)
            {
                var cell = view.Items[i].SubItems[view.ColumnIndexGap]; var gap = i + 1 < source.Paragraphs.Count ? (source.Paragraphs[i + 1].StartTime.TotalMilliseconds - source.Paragraphs[i].EndTime.TotalMilliseconds) / 1000 : 0;
                cell.Text = i + 1 == source.Paragraphs.Count ? "" : gap.ToString("0.###");
                cell.BackColor = gap >= 25 ? Color.FromArgb(53, 155, 77) : gap >= 12 ? Color.FromArgb(98, 185, 113) : gap >= 6 ? Color.FromArgb(153, 211, 163) : gap >= 3 ? Color.FromArgb(205, 235, 210) : view.BackColor;
                cell.ForeColor = gap >= 25 ? Color.White : view.ForeColor;
            }
        }
        private void UpdateClassicGaps() { if (!_classicOther) return; FillClassicGaps(subtitleListView1, _subtitle); FillClassicGaps(subtitleListView2, _otherSubtitle); }
        private void SetClassicImageHeight()
        {
            subtitleListView2.SmallImageList = null; _classicImageHeight?.Dispose(); _classicImageHeight = null; _classicRowPixel?.Dispose(); _classicRowPixel = null;
            if (_classicReference?.IsImageBased != true) return;
            _classicImageHeight = new ImageList { ImageSize = new Size(1, _classicImageSize) }; _classicRowPixel = new Bitmap(1, _classicImageSize); _classicImageHeight.Images.Add(_classicRowPixel);
            subtitleListView2.SmallImageList = _classicImageHeight; subtitleListView2.Invalidate();
        }
        private void DrawClassicReferenceImage(object sender, DrawListViewSubItemEventArgs e)
        {
            if (_classicReference?.IsImageBased != true || e.ColumnIndex != subtitleListView2.ColumnIndexText || e.ItemIndex >= _classicReference.Images.Count) return;
            e.DrawDefault = false;
            using (var background = new SolidBrush(e.Item.Selected ? SystemColors.Highlight : Color.FromArgb(45, 45, 45))) e.Graphics.FillRectangle(background, e.Bounds);
            try
            {
                if (!_classicImages.TryGetValue(e.ItemIndex, out var image))
                {
                    if (_classicImages.Count >= 16) ClearClassicImages();
                    image = _classicReference.Images[e.ItemIndex](); _classicImages[e.ItemIndex] = image;
                }
                if (image == null) return;
                var scale = Math.Min((e.Bounds.Width - 4.0) / image.Width, (e.Bounds.Height - 4.0) / image.Height);
                e.Graphics.DrawImage(image, e.Bounds.Left + 2, e.Bounds.Top + 2, Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidDataException || ex is IndexOutOfRangeException) { TextRenderer.DrawText(e.Graphics, ex.Message, Font, e.Bounds, Color.White); }
        }
        private void ShowClassicReferenceImage()
        {
            if (_classicReference?.IsImageBased != true || subtitleListView2.SelectedIndex < 0) return;
            using (var form = new Form { Text = SP("Изображение субтитра", "Subtitle image"), Width = 900, Height = 420, StartPosition = FormStartPosition.CenterParent, KeyPreview = true })
            using (var picture = new ClassicSyncImageBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(45, 45, 45), TabStop = true })
            {
                form.Controls.Add(picture); var index = subtitleListView2.SelectedIndex;
                form.Shown += (s, e) => picture.Focus();
                Action load = () => { var image = _classicReference.Images[index](); var old = picture.Image; picture.Image = image; old?.Dispose(); subtitleListView2.SelectIndexAndEnsureVisible(index); form.Text = (index + 1) + " / " + _classicReference.Images.Count; };
                form.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) form.Close(); else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up) { index = Math.Max(0, index - 1); load(); } else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Down) { index = Math.Min(_classicReference.Images.Count - 1, index + 1); load(); } };
                try { load(); form.ShowDialog(this); } finally { picture.Image?.Dispose(); picture.Image = null; }
            }
        }
        private sealed class ClassicSyncImageBox : PictureBox
        {
            protected override bool IsInputKey(Keys keyData)
            {
                var key = keyData & Keys.KeyCode;
                return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || base.IsInputKey(keyData);
            }
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == UiUtil.HelpKeys) { UiUtil.OpenUrl("https://github.com/SubtitleEdit/subtitleedit/blob/v5.3.0/docs/features/" + (_classicOther ? "point-sync-via-other.md" : "point-sync.md")); return true; }
            if (keyData == (Keys.Control | Keys.P) && _classicFocused == subtitleListView2 && _classicReference?.IsImageBased == true) { ShowClassicReferenceImage(); return true; }
            if (_classicFocused == subtitleListView2 && _classicReference?.IsImageBased == true && (keyData == (Keys.Control | Keys.Add) || keyData == (Keys.Control | Keys.Oemplus) || keyData == (Keys.Control | Keys.Subtract) || keyData == (Keys.Control | Keys.OemMinus))) { _classicImageSize = Math.Max(32, Math.Min(160, _classicImageSize + ((keyData & Keys.KeyCode) == Keys.Add || (keyData & Keys.KeyCode) == Keys.Oemplus ? 16 : -16))); SetClassicImageHeight(); return true; }
            if (keyData == (Keys.Control | Keys.G)) { var view = _classicFocused ?? subtitleListView1; if (view.Items.Count > 0) using (var dialog = new GoToLine()) { dialog.Initialize(1, view.Items.Count); if (dialog.ShowDialog(this) == DialogResult.OK) view.SelectIndexAndEnsureVisible(dialog.LineNumber - 1, true); } return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
