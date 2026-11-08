using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicSyncToAudio : Form
    {
        private readonly Subtitle _source;
        private readonly WavePeakData _peaks;
        private readonly double _frameRate;
        private readonly NumericUpDown _offset = new NumericUpDown { Minimum = 1, Maximum = 3600, Value = 120, Width = 90 };
        private readonly CheckBox _rates = new CheckBox { AutoSize = true, Checked = true };
        private readonly Button _analyze = new Button { AutoSize = true };
        private readonly Button _apply = new Button { AutoSize = true, Enabled = false };
        private readonly Button _cancel = new Button { AutoSize = true };
        private readonly Label _status = new Label { AutoSize = true, MaximumSize = new Size(880, 0) };
        private readonly DataGridView _preview = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
        private CancellationTokenSource _operation;
        private AudioSubtitleSync.Result _result;
        public Subtitle EditedSubtitle { get; private set; }

        public ClassicSyncToAudio(Subtitle subtitle, WavePeakData peaks, double frameRate)
        {
            UiUtil.PreInitialize(this);
            _source = new Subtitle(subtitle); _peaks = peaks; _frameRate = frameRate;
            Text = L("Синхронизировать субтитры со звуком", "Sync subtitle to audio");
            Size = new Size(920, 620); MinimumSize = new Size(700, 430); StartPosition = FormStartPosition.CenterParent;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), RowCount = 4, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var controls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            controls.Controls.Add(new Label { Text = L("Максимальный сдвиг, сек.", "Maximum offset, sec."), AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            _rates.Text = L("Проверить частоту кадров", "Detect frame-rate mismatch");
            _analyze.Text = L("Анализировать", "Analyze"); controls.Controls.AddRange(new Control[] { _offset, _rates, _analyze });
            _status.Text = L("Анализ использует загруженную осциллограмму. Перед применением проверьте предложенный результат.", "Analysis uses the loaded waveform. Review the proposed result before applying it.");
            foreach (var title in new[] { "№", L("Начало до", "Start before"), L("Начало после", "Start after"), L("Конец после", "End after"), L("Текст", "Text") }) _preview.Columns.Add(title, title);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            _apply.Text = L("Применить", "Apply"); _cancel.Text = L("Отмена", "Cancel"); footer.Controls.AddRange(new Control[] { _apply, _cancel });
            layout.Controls.Add(controls, 0, 0); layout.Controls.Add(_status, 0, 1); layout.Controls.Add(_preview, 0, 2); layout.Controls.Add(footer, 0, 3); Controls.Add(layout);
            _analyze.Click += async (s, e) => await Analyze();
            _apply.Click += (s, e) =>
            {
                if (_result.Confidence < AudioSubtitleSync.LowConfidence && MessageBox.Show(this, L("Совпадение слабое. Всё равно применить предложенный сдвиг?", "The match is weak. Apply the proposed timing anyway?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                EditedSubtitle = new Subtitle(_source);
                foreach (var cue in EditedSubtitle.Paragraphs)
                {
                    cue.StartTime.TotalMilliseconds = cue.StartTime.TotalMilliseconds * _result.Ratio + _result.OffsetSeconds * 1000;
                    cue.EndTime.TotalMilliseconds = cue.EndTime.TotalMilliseconds * _result.Ratio + _result.OffsetSeconds * 1000;
                }
                DialogResult = DialogResult.OK;
            };
            _cancel.Click += (s, e) => { if (_operation != null) _operation.Cancel(); else Close(); };
            FormClosing += (s, e) => { if (_operation != null) { e.Cancel = true; _operation.Cancel(); } };
            _offset.ValueChanged += (s, e) => _apply.Enabled = false; _rates.CheckedChanged += (s, e) => _apply.Enabled = false;
            UiUtil.FixFonts(this);
        }
        private static string L(string ru, string en) => ClassicAssistedText.L(ru, en);
        private async Task Analyze()
        {
            _operation = new CancellationTokenSource(); _analyze.Enabled = _rates.Enabled = _offset.Enabled = _apply.Enabled = false;
            var options = new AudioSubtitleSync.Options((double)_offset.Value, _rates.Checked, _frameRate);
            var lines = _source.Paragraphs.Select(p => (p.StartTime.TotalSeconds, p.EndTime.TotalSeconds)).ToList();
            var progress = new Progress<double>(p => _status.Text = L("Анализ: ", "Analyzing: ") + (p * 100).ToString("0") + "%");
            try
            {
                _result = await Task.Run(() => { var speech = AudioSubtitleSync.SpeechFromPeaks(_peaks); return speech == null ? null : AudioSubtitleSync.Find(speech, lines, options, progress, _operation.Token); });
                _preview.Rows.Clear();
                if (_result == null) { _status.Text = L("Не удалось найти устойчивое совпадение. Проверьте дорожку и субтитры.", "No stable match found. Check the audio track and subtitle."); return; }
                _status.Text = L("Сдвиг: ", "Offset: ") + _result.OffsetSeconds.ToString("+0.000;-0.000;0") + L(" сек.; коэффициент: ", " sec.; ratio: ") + _result.Ratio.ToString("0.000000") + L("; уверенность: ", "; confidence: ") + _result.Confidence.ToString("P0");
                foreach (var cue in _source.Paragraphs)
                    _preview.Rows.Add(cue.Number, cue.StartTime.ToString(), new TimeCode(cue.StartTime.TotalMilliseconds * _result.Ratio + _result.OffsetSeconds * 1000).ToString(), new TimeCode(cue.EndTime.TotalMilliseconds * _result.Ratio + _result.OffsetSeconds * 1000).ToString(), cue.Text);
                _apply.Enabled = true;
            }
            catch (OperationCanceledException) { _status.Text = L("Отменено", "Cancelled"); }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException) { _status.Text = ex.Message; }
            finally { _operation.Dispose(); _operation = null; _analyze.Enabled = _rates.Enabled = _offset.Enabled = true; }
        }
    }
}
