using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class VisualSync
    {
        public WavePeakData SharedWavePeaks { get; set; }
        public string SharedWaveVideo { get; set; }
        private ClassicSyncWave _classicStartWave, _classicEndWave;
        private Button _classicManual;
        private Subtitle _classicPreview;
        private Subtitle ClassicVisualPreview()
        {
            if (_classicPreview == null) _classicPreview = new Subtitle(_inputSubtitle, false);
            _classicPreview.Paragraphs.Clear(); if (_paragraphs != null) _classicPreview.Paragraphs.AddRange(_paragraphs);
            return _classicPreview;
        }
        private static string CS(string ru, string en) => Configuration.Settings.General.Language.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? ru : en;

        private void InitializeClassicSync()
        {
            _classicStartWave = new ClassicSyncWave(groupBoxStartScene, () => new Rectangle(MediaPlayerStart.Left, MediaPlayerStart.Top, MediaPlayerStart.Width, panelControlsStart.Top - MediaPlayerStart.Top - 2), h => MediaPlayerStart.Height = h, "ClassicVisualSyncWaveHeight.txt", seconds => { _isStartSceneActive = true; MediaPlayerStart.CurrentPosition = seconds; MediaPlayerStart.RefreshProgressBar(); });
            _classicEndWave = new ClassicSyncWave(groupBoxEndScene, () => new Rectangle(MediaPlayerEnd.Left, MediaPlayerEnd.Top, MediaPlayerEnd.Width, panelControlsEnd.Top - MediaPlayerEnd.Top - 2), h => MediaPlayerEnd.Height = h, "ClassicVisualSyncWaveHeight.txt", seconds => { _isStartSceneActive = false; MediaPlayerEnd.CurrentPosition = seconds; MediaPlayerEnd.RefreshProgressBar(); });
            _classicStartWave.HeightChanged += h => _classicEndWave.SetHeight(h); _classicEndWave.HeightChanged += h => _classicStartWave.SetHeight(h);
            _classicStartWave.Wave.Enter += (s, e) => _isStartSceneActive = true; _classicEndWave.Wave.Enter += (s, e) => _isStartSceneActive = false;
            buttonStartThreeSecondsBack.Text = buttonThreeSecondsBack.Text = "−1 s";
            buttonStartHalfASecondBack.Text = buttonEndHalfASecondBack.Text = "+1 s";
            _classicManual = new Button { Text = CS("Ручная синхронизация…", "Manual sync…"), AutoSize = true, Left = 12, Top = buttonSync.Top, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            _classicManual.Click += (s, e) => ShowClassicManualSync(); Controls.Add(_classicManual);
            labelTip.AutoSize = false; labelTip.SetBounds(12, buttonSync.Top - 32, buttonSync.Left - 24, 28);
            FormClosed += (s, e) => { _classicStartWave.Dispose(); _classicEndWave.Dispose(); };
        }

        private void UpdateClassicWaves()
        {
            if (_classicStartWave == null) return;
            var peaks = string.Equals(VideoFileName, SharedWaveVideo, StringComparison.OrdinalIgnoreCase) ? SharedWavePeaks : null;
            if (_classicStartWave.Wave.WavePeaks != peaks) { _classicStartWave.Wave.WavePeaks = peaks; _classicEndWave.Wave.WavePeaks = peaks; }
            _classicStartWave.Layout(); _classicEndWave.Layout();
            if (_paragraphs == null) return;
            var sub = ClassicVisualPreview();
            UiUtil.ShowSubtitle(sub, MediaPlayerStart, _inputSubtitle.OriginalFormat ?? new Core.SubtitleFormats.SubRip());
            UiUtil.ShowSubtitle(sub, MediaPlayerEnd, _inputSubtitle.OriginalFormat ?? new Core.SubtitleFormats.SubRip());
            _classicStartWave.Update(sub, MediaPlayerStart.CurrentPosition, comboBoxStartTexts.SelectedIndex);
            _classicEndWave.Update(sub, MediaPlayerEnd.CurrentPosition, comboBoxEndTexts.SelectedIndex);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == UiUtil.HelpKeys) { UiUtil.OpenUrl("https://github.com/SubtitleEdit/subtitleedit/blob/v5.3.0/docs/features/visual-sync.md"); return true; }
            var step = ClassicSyncKeys.SeekSeconds(keyData);
            if (step.HasValue)
            {
                var player = _isStartSceneActive ? MediaPlayerStart : MediaPlayerEnd;
                if (player.VideoPlayer != null) { player.Pause(); player.CurrentPosition = Math.Max(0, Math.Min(player.Duration, player.CurrentPosition + step.Value)); player.RefreshProgressBar(); UpdateClassicWaves(); }
                return true;
            }
            if (ClassicSyncKeys.VerticalZoom(keyData, (_isStartSceneActive ? _classicStartWave : _classicEndWave)?.Wave)) return true;
            if (keyData == (Keys.Control | Keys.P) || keyData == Keys.Space) { (_isStartSceneActive ? MediaPlayerStart : MediaPlayerEnd).TogglePlayPause(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ShowClassicManualSync()
        {
            using (var dialog = new Form { Text = CS("Ручная синхронизация", "Manual sync"), StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(430, 145), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
            {
                var offset = new NumericUpDown { Left = 185, Top = 15, Width = 210, DecimalPlaces = 3, Minimum = -359999, Maximum = 359999, Increment = .1m };
                var speed = new NumericUpDown { Left = 185, Top = 50, Width = 210, DecimalPlaces = 6, Minimum = .001m, Maximum = 1000, Value = 1, Increment = .001m };
                dialog.Controls.Add(new Label { Text = CS("Сдвиг, секунды:", "Offset, seconds:"), Left = 12, Top = 20, AutoSize = true }); dialog.Controls.Add(offset);
                dialog.Controls.Add(new Label { Text = CS("Коэффициент скорости:", "Speed factor:"), Left = 12, Top = 55, AutoSize = true }); dialog.Controls.Add(speed);
                var apply = new Button { Text = CS("Применить", "Apply"), Left = 205, Top = 100, Width = 90 }; var cancel = new Button { Text = LanguageSettings.Current.General.Cancel, Left = 305, Top = 100, Width = 90, DialogResult = DialogResult.Cancel };
                apply.Click += (s, e) => { if (ApplyClassicManual((double)speed.Value, (double)offset.Value)) dialog.DialogResult = DialogResult.OK; else MessageBox.Show(CS("Получаются недопустимые тайм-коды.", "The resulting time codes are outside valid bounds.")); };
                dialog.Controls.Add(apply); dialog.Controls.Add(cancel); dialog.AcceptButton = apply; dialog.CancelButton = cancel; dialog.ShowDialog(this);
            }
        }

        private bool ApplyClassicManual(double factor, double seconds)
        {
            if (_paragraphs == null || factor <= 0 || double.IsNaN(factor) || double.IsInfinity(factor) || double.IsNaN(seconds) || double.IsInfinity(seconds)) return false;
            if (_inputSubtitle.Paragraphs.Any(p => p.StartTime.TotalMilliseconds * factor + seconds * 1000 < 0 || p.EndTime.TotalMilliseconds * factor + seconds * 1000 > TimeCode.MaxTimeTotalMilliseconds)) return false;
            var current = new Subtitle(_inputSubtitle, false); foreach (var p in current.Paragraphs) p.Adjust(factor, seconds);
            if (current.Paragraphs.Any(p => p.StartTime.TotalMilliseconds < 0 || p.EndTime.TotalMilliseconds > TimeCode.MaxTimeTotalMilliseconds || p.EndTime.TotalMilliseconds < p.StartTime.TotalMilliseconds)) return false;
            _paragraphs = current.Paragraphs;
            if (_inputOriginalSubtitle != null) { var original = new Subtitle(_inputOriginalSubtitle, false); foreach (var p in original.Paragraphs) p.Adjust(factor, seconds); _paragraphsOriginal = original.Paragraphs; }
            var start = comboBoxStartTexts.SelectedIndex; var end = comboBoxEndTexts.SelectedIndex; FillStartAndEndTexts();
            comboBoxStartTexts.SelectedIndex = start; comboBoxEndTexts.SelectedIndex = end; UpdateClassicWaves(); labelSyncDone.Text = _language.SynchronizationDone; return true;
        }
    }
}
