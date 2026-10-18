using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class SetSyncPoint
    {
        public WavePeakData SharedWavePeaks { get; set; }
        public string SharedWaveVideo { get; set; }
        private ClassicSyncWave _classicWave;
        private void InitializeClassicPoint()
        {
            _classicWave = new ClassicSyncWave(this, () => new Rectangle(videoPlayerContainer1.Left, videoPlayerContainer1.Top, videoPlayerContainer1.Width, labelSubtitle.Top - videoPlayerContainer1.Top - 2), h => videoPlayerContainer1.Height = h, "ClassicSetPointWaveHeight.txt", SeekClassicPoint);
            Resize += (s, e) => UpdateClassicPointWave(); FormClosed += (s, e) => _classicWave.Dispose();
            buttonThreeSecondsBack.Text = "−1 s"; buttonThreeSecondsAhead.Text = "+1 s";
            var path = Path.Combine(Configuration.DataDirectory, "ClassicSetPointSize.txt");
            try { var values = File.ReadAllText(path).Split(';'); if (values.Length == 2 && int.TryParse(values[0], out var w) && int.TryParse(values[1], out var h) && w >= MinimumSize.Width && h >= MinimumSize.Height) Size = new Size(Math.Min(w, Screen.FromControl(this).WorkingArea.Width), Math.Min(h, Screen.FromControl(this).WorkingArea.Height)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            FormClosed += (s, e) => { try { File.WriteAllText(path, Width + ";" + Height); } catch (IOException) { } catch (UnauthorizedAccessException) { } };
        }
        private void UpdateClassicPointWave()
        {
            if (_classicWave == null) return;
            var peaks = string.Equals(VideoFileName, SharedWaveVideo, StringComparison.OrdinalIgnoreCase) ? SharedWavePeaks : null;
            if (_classicWave.Wave.WavePeaks != peaks) _classicWave.Wave.WavePeaks = peaks;
            _classicWave.Layout();
            _classicWave.Update(_subtitle, videoPlayerContainer1.VideoPlayer == null ? timeUpDownLine.TimeCode.TotalSeconds : videoPlayerContainer1.CurrentPosition, subtitleListView1.SelectedItems.Count > 0 ? subtitleListView1.SelectedItems[0].Index : -1);
        }
        private void SeekClassicPoint(double seconds)
        {
            seconds = Math.Max(0, seconds);
            if (videoPlayerContainer1.VideoPlayer != null) { videoPlayerContainer1.Pause(); seconds = Math.Min(videoPlayerContainer1.Duration, seconds); videoPlayerContainer1.CurrentPosition = seconds; videoPlayerContainer1.RefreshProgressBar(); }
            timeUpDownLine.TimeCode = TimeCode.FromSeconds(seconds); UpdateClassicPointWave();
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == UiUtil.HelpKeys) { UiUtil.OpenUrl("https://github.com/SubtitleEdit/subtitleedit/blob/v5.3.0/docs/features/point-sync.md"); return true; }
            if (!timeUpDownLine.ContainsFocus)
            {
                var step = ClassicSyncKeys.SeekSeconds(keyData);
                if (step.HasValue) { SeekClassicPoint((videoPlayerContainer1.VideoPlayer == null ? timeUpDownLine.TimeCode.TotalSeconds : videoPlayerContainer1.CurrentPosition) + step.Value); return true; }
                if (keyData == Keys.Space || keyData == (Keys.Control | Keys.P)) { videoPlayerContainer1.TogglePlayPause(); return true; }
                if (ClassicSyncKeys.VerticalZoom(keyData, _classicWave?.Wave)) return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
