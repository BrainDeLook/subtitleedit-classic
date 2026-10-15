using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Forms.Tts
{
    public sealed partial class ReviewAudioClips
    {
        private ClassicSpeechWaveform _speechWave;
        private CancellationTokenSource _reviewWaveCancellation;
        private readonly List<double[]> _reviewOriginalTimes = new List<double[]>();
        private Button _fitSpeechButton, _resetSpeechButton;
        private System.Windows.Forms.Timer _reviewCursorTimer;
        private double _reviewSeekSeconds;
        public Task ReviewWaveformTask { get; private set; } = Task.CompletedTask;

        private void InitializeSpeechWaveform()
        {
            foreach (var p in _subtitle.Paragraphs) _reviewOriginalTimes.Add(new[] { p.StartTime.TotalMilliseconds, p.EndTime.TotalMilliseconds });
            var content = listViewAudioClips.Parent;
            var listBounds = listViewAudioClips.Bounds;
            var extra = Math.Max(170, Font.Height * 13);
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + extra);
            MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height + extra);
            listViewAudioClips.Bounds = listBounds;
            var area = new TableLayoutPanel { Name = "speechWaveformArea", Bounds = new Rectangle(listBounds.Left, listBounds.Bottom + 6, listBounds.Width, extra - 10), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom, ColumnCount = 1, RowCount = 2 };
            area.RowStyles.Add(new RowStyle(SizeType.AutoSize)); area.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            _fitSpeechButton = new Button { Name = "buttonFitSpeechDuration", AutoSize = true, Text = TextToSpeech.SessionLabel("Конец по длине аудио", "Fit end to audio") };
            _resetSpeechButton = new Button { Name = "buttonResetSpeechTiming", AutoSize = true, Text = TextToSpeech.SessionLabel("Сбросить тайминги", "Reset timing") };
            var plus = new Button { Text = "+", Width = 30 }; var minus = new Button { Text = "−", Width = 30 };
            bar.Controls.AddRange(new Control[] { _fitSpeechButton, _resetSpeechButton, plus, minus });
            _speechWave = new ClassicSpeechWaveform { Name = "speechWaveform", Dock = DockStyle.Fill };
            area.Controls.Add(bar, 0, 0); area.Controls.Add(_speechWave, 0, 1); content.Controls.Add(area);
            _fitSpeechButton.Click += (s, e) => FitSelectedSpeech(); _resetSpeechButton.Click += (s, e) => ResetSelectedSpeechTiming();
            plus.Click += (s, e) => _speechWave.Zoom(.8); minus.Click += (s, e) => _speechWave.Zoom(1.25);
            _speechWave.TimingEdited += (start, end) => { if (listViewAudioClips.SelectedIndices.Count > 0) ApplySpeechTiming(listViewAudioClips.SelectedIndices[0], start * 1000, end * 1000); };
            _speechWave.SeekRequested += seconds => { if (_libMpv == null) return; buttonStop_Click(null, null); _reviewSeekSeconds = seconds; Play(true); };
            listViewAudioClips.SelectedIndexChanged += (s, e) => RefreshSpeechWaveform();
            _reviewCursorTimer = new System.Windows.Forms.Timer { Interval = 70 };
            _reviewCursorTimer.Tick += (s, e) => { _speechWave.PlaybackSeconds = _libMpv != null && !_libMpv.IsPaused ? _libMpv.CurrentPosition : -1; _speechWave.Invalidate(); };
            _reviewCursorTimer.Start();
            FormClosed += (s, e) => { _reviewWaveCancellation?.Cancel(); _reviewCursorTimer.Stop(); _reviewCursorTimer.Dispose(); Stop(); _mpvDoneTimer?.Dispose(); _libMpv?.Dispose(); _soundPlayer?.Dispose(); };
            RefreshSpeechWaveform();
        }

        public bool ApplySpeechTiming(int index, double startMs, double endMs)
        {
            if (_sessionExportCancellation != null || index < 0 || index >= _subtitle.Paragraphs.Count || double.IsNaN(startMs) || double.IsNaN(endMs) || double.IsInfinity(startMs) || double.IsInfinity(endMs) || startMs < 0 || endMs <= startMs || endMs > int.MaxValue) return false;
            startMs = Math.Round(startMs); endMs = Math.Round(endMs);
            if (endMs <= startMs) return false;
            var p = _subtitle.Paragraphs[index];
            p.StartTime.TotalMilliseconds = startMs; p.EndTime.TotalMilliseconds = endMs;
            RefreshSessionReviewRow(index, false);
            listView1_SelectedIndexChanged(null, EventArgs.Empty);
            if (listViewAudioClips.SelectedIndices.Count > 0 && listViewAudioClips.SelectedIndices[0] == index) _speechWave.SetCue(p.StartTime.TotalSeconds, p.EndTime.TotalSeconds, false);
            return true;
        }
        public bool FitSelectedSpeech()
        {
            if (_speechWave.AudioSeconds <= 0 || _speechWave.Peaks == null || listViewAudioClips.SelectedIndices.Count == 0) return false;
            var index = listViewAudioClips.SelectedIndices[0];
            var p = _subtitle.Paragraphs[index];
            return ApplySpeechTiming(index, p.StartTime.TotalMilliseconds, p.StartTime.TotalMilliseconds + _speechWave.AudioSeconds * 1000);
        }
        public void ResetSelectedSpeechTiming()
        {
            if (listViewAudioClips.SelectedIndices.Count == 0) return;
            var i = listViewAudioClips.SelectedIndices[0]; var times = _reviewOriginalTimes[i];
            if (ApplySpeechTiming(i, times[0], times[1])) _speechWave.SetCue(times[0] / 1000, times[1] / 1000, true);
        }
        private void RefreshSpeechWaveform()
        {
            if (_speechWave == null || IsDisposed) return;
            buttonStop_Click(null, null);
            _reviewWaveCancellation?.Cancel();
            _speechWave.Peaks = null; _speechWave.AudioSeconds = 0; _speechWave.PlaybackSeconds = -1; _fitSpeechButton.Enabled = false;
            if (listViewAudioClips.SelectedIndices.Count == 0) { _speechWave.Text = string.Empty; _speechWave.Invalidate(); return; }
            var index = listViewAudioClips.SelectedIndices[0]; var p = _subtitle.Paragraphs[index];
            _speechWave.Text = TextToSpeech.SessionLabel("Загрузка аудио…", "Loading audio…"); _speechWave.SetCue(p.StartTime.TotalSeconds, p.EndTime.TotalSeconds, true);
            var cancellation = new CancellationTokenSource(); _reviewWaveCancellation = cancellation;
            ReviewWaveformTask = LoadReviewWaveformAsync(index, _fileNames[index].Filename, cancellation);
        }
        private async Task LoadReviewWaveformAsync(int index, string file, CancellationTokenSource cancellation)
        {
            var folder = Path.Combine(Path.GetTempPath(), "se-speech-wave-" + Guid.NewGuid().ToString("N"));
            var token = cancellation.Token;
            try
            {
                Directory.CreateDirectory(folder); var wave = file;
                // Session audio can be compressed; normalize only the visualization copy.
                if (!string.Equals(Path.GetExtension(file), ".wav", StringComparison.OrdinalIgnoreCase))
                {
                    wave = Path.Combine(folder, "audio.wav");
                    await ClassicSpeechRunner.RunProcessAsync(new System.Diagnostics.ProcessStartInfo(Configuration.Settings.General.FFmpegLocation, "-nostdin -y -i " + ClassicSpeechRunner.Quote(file) + " -vn -ac 1 -ar 16000 -c:a pcm_s16le " + ClassicSpeechRunner.Quote(wave)), token, null);
                }
                var source = wave;
                var result = await Task.Run(() => { token.ThrowIfCancellationRequested(); var seconds = ClassicVoiceAssets.WaveSeconds(source); using (var generator = new WavePeakGenerator(source)) return Tuple.Create(generator.GeneratePeaks(0, Path.Combine(folder, "peaks.dat")), seconds); }, token);
                token.ThrowIfCancellationRequested();
                if (IsDisposed || Disposing || _reviewWaveCancellation != cancellation) return;
                _speechWave.Peaks = result.Item1; _speechWave.AudioSeconds = result.Item2;
                var p = _subtitle.Paragraphs[index]; _speechWave.SetCue(p.StartTime.TotalSeconds, p.EndTime.TotalSeconds, true);
                _speechWave.Text = TextToSpeech.SessionLabel("Аудио: ", "Audio: ") + result.Item2.ToString("0.000") + TextToSpeech.SessionLabel(" с · двигай жёлтые границы; Ctrl+колесо — масштаб", " s · drag yellow edges; Ctrl+wheel to zoom");
                _fitSpeechButton.Enabled = result.Item2 > 0;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!IsDisposed && !Disposing && _reviewWaveCancellation == cancellation) { _speechWave.Text = TextToSpeech.SessionLabel("Не удалось загрузить аудио: ", "Could not load audio: ") + ex.Message; _speechWave.Invalidate(); } }
            finally
            {
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                if (_reviewWaveCancellation == cancellation) _reviewWaveCancellation = null;
                cancellation.Dispose();
            }
        }
    }
}
