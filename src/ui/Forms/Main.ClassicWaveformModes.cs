using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private sealed class WaveformModePreferences { public bool Fast; public bool Speech; public bool HideShotChanges; public double Zoom = 1; public int? Grid; }
        private WaveformModePreferences _waveModes;
        private CancellationTokenSource _speechWaveJob;
        private WavePeakData _regularWavePeaks, _speechWavePeaks;
        private string _speechWaveSource;
        private ToolStripMenuItem _speechWaveMenu;
        private void InitializeClassicWaveformModes()
        {
            if (_waveModes != null) return;
            var file = Path.Combine(Configuration.DataDirectory, "ClassicWaveformModes.json");
            try { _waveModes = JsonConvert.DeserializeObject<WaveformModePreferences>(File.ReadAllText(file)); } catch (IOException) { } catch (JsonException) { }
            if (_waveModes == null) _waveModes = new WaveformModePreferences();
            if (double.IsNaN(_waveModes.Zoom) || double.IsInfinity(_waveModes.Zoom) || _waveModes.Zoom <= 0) _waveModes.Zoom = 1;
            var menu = new ToolStripMenuItem(ClassicAssistedText.L("Осциллограмма", "Waveform")) { Name = "classicWaveformSettings" };
            _speechWaveMenu = new ToolStripMenuItem(ClassicAssistedText.L("Показывать только речь", "Show speech only")) { CheckOnClick = true };
            var fast = new ToolStripMenuItem(ClassicAssistedText.L("Быстрый рендерер (экспериментальный)", "Fast renderer (experimental)")) { CheckOnClick = true, Checked = _waveModes.Fast };
            var shotMarks = new ToolStripMenuItem(ClassicAssistedText.L("Показывать метки смены плана", "Show shot-change markers")) { Name = "classicShotChangeVisibility", CheckOnClick = true, Checked = !_waveModes.HideShotChanges };
            audioVisualizer.ShowShotChanges = shotMarks.Checked;
            shotMarks.CheckedChanged += (s,e) => { _waveModes.HideShotChanges = !shotMarks.Checked; audioVisualizer.ShowShotChanges = shotMarks.Checked; };
            var color = new ToolStripMenuItem(ClassicAssistedText.L("Цвет сетки…", "Grid color…"));
            audioVisualizer.FastRenderer = _waveModes.Fast; audioVisualizer.ZoomFactor = _waveModes.Zoom;
            if (_waveModes.Grid.HasValue) audioVisualizer.GridColor = Color.FromArgb(_waveModes.Grid.Value);
            fast.CheckedChanged += (s,e) => { audioVisualizer.FastRenderer = _waveModes.Fast = fast.Checked; audioVisualizer.Invalidate(); };
            color.Click += (s,e) => { using (var d = new ColorDialog { Color = audioVisualizer.GridColor }) if (d.ShowDialog(this) == DialogResult.OK) { audioVisualizer.GridColor = d.Color; _waveModes.Grid = d.Color.ToArgb(); audioVisualizer.Invalidate(); } };
            _speechWaveMenu.CheckedChanged += async (s,e) => { _waveModes.Speech = _speechWaveMenu.Checked; await SetSpeechWaveform(_speechWaveMenu.Checked); };
            menu.DropDownItems.AddRange(new ToolStripItem[] { _speechWaveMenu, fast, shotMarks, color }); optionsToolStripMenuItem.DropDownItems.Add(menu);
            var watch = new System.Windows.Forms.Timer { Interval = 500 }; watch.Tick += async (s,e) =>
            {
                if (_speechWaveSource != SpeechWaveIdentity())
                {
                    _speechWaveJob?.Cancel(); _speechWaveSource = SpeechWaveIdentity(); _regularWavePeaks = null; _speechWavePeaks = null;
                    if (_speechWaveMenu.Checked && _videoInfo != null) await SetSpeechWaveform(true);
                }
                else if (_speechWaveMenu.Checked && _speechWavePeaks != null && audioVisualizer.WavePeaks != _speechWavePeaks && _speechWaveJob == null)
                { _regularWavePeaks = audioVisualizer.WavePeaks; SwapWaveform(_speechWavePeaks); }
            }; watch.Start();
            FormClosed += (s,e) => { watch.Stop(); watch.Dispose(); _speechWaveJob?.Cancel(); _waveModes.Zoom = audioVisualizer.ZoomFactor; try { File.WriteAllText(file, JsonConvert.SerializeObject(_waveModes)); } catch (IOException) { } catch (UnauthorizedAccessException) { } };
            if (_waveModes.Speech) _speechWaveMenu.Checked = true;
        }
        private string SpeechWaveIdentity() => File.Exists(_videoFileName) ? _videoFileName + "|" + File.GetLastWriteTimeUtc(_videoFileName).Ticks + "|" + ClassicSelectedAudioIndex() : null;
        private void SwapWaveform(WavePeakData peaks)
        {
            var zoom = audioVisualizer.ZoomFactor; var start = audioVisualizer.StartPositionSeconds;
            audioVisualizer.WavePeaks = peaks; audioVisualizer.ZoomFactor = zoom;
            audioVisualizer.SetPosition(start, _subtitle, mediaPlayer.CurrentPosition, FirstSelectedIndex, SubtitleListview1.SelectedIndices); audioVisualizer.Invalidate();
        }
        private async Task SetSpeechWaveform(bool speech)
        {
            _speechWaveJob?.Cancel();
            if (!speech) { if (_regularWavePeaks != null) SwapWaveform(_regularWavePeaks); return; }
            var source = _videoFileName; var identity = SpeechWaveIdentity(); if (identity == null || !RequireFfmpegOk()) return;
            if (_speechWavePeaks != null && _speechWaveSource == identity) { SwapWaveform(_speechWavePeaks); return; }
            var operation = new CancellationTokenSource(); _speechWaveJob = operation; var token = operation.Token;
            var index = ClassicSelectedAudioIndex();
            var peak = WavePeakGenerator.GetPeakWaveFileName(source, index); peak = Path.Combine(Path.GetDirectoryName(peak), "speech-" + Path.GetFileName(peak));
            var job = Path.Combine(Path.GetTempPath(), "se-speech-wave-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(job);
            try
            {
                _speechWaveSource = identity;
                if (!File.Exists(peak) || File.GetLastWriteTimeUtc(peak) < File.GetLastWriteTimeUtc(source))
                {
                    ShowStatus(ClassicAssistedText.L("Подготовка осциллограммы речи…", "Preparing speech-only waveform…"));
                    var wave = Path.Combine(job, "source.wav");
                    await ClassicMediaJobs.Render("-i " + ClassicMediaJobs.Q(source) + " -map 0:a:" + index + " -vn -af aresample=async=1:first_pts=0 -ar 16000 -ac 1 -c:a pcm_s16le", wave, token);
                    var speechWave = await ClassicSpeechExtras.IsolateAsync(wave, job, ClassicSpeechOptions.Load().Variant, token, new Progress<SpeechProgress>(p => { if (!IsDisposed && !Disposing && _speechWaveJob == operation) ShowStatus(p.Message); }));
                    var staged = Path.Combine(job, "peaks.dat");
                    await Task.Run(() => { using (var generator = new WavePeakGenerator(speechWave)) generator.GeneratePeaks(0, staged); }, token);
                    token.ThrowIfCancellationRequested(); Directory.CreateDirectory(Path.GetDirectoryName(peak)); File.Copy(staged, peak, true);
                }
                token.ThrowIfCancellationRequested(); if (IsDisposed || identity != SpeechWaveIdentity() || !_speechWaveMenu.Checked) return;
                _regularWavePeaks = audioVisualizer.WavePeaks; _speechWavePeaks = WavePeakData.FromDisk(peak); SwapWaveform(_speechWavePeaks);
                ShowStatus(ClassicAssistedText.L("Осциллограмма речи готова", "Speech-only waveform ready"));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!IsDisposed && _speechWaveJob == operation && identity == SpeechWaveIdentity()) { ShowStatus(ex.Message); _speechWaveMenu.Checked = false; } }
            finally { if (_speechWaveJob == operation) _speechWaveJob = null; operation.Dispose(); try { Directory.Delete(job, true); } catch (IOException) { } }
        }
    }
}
