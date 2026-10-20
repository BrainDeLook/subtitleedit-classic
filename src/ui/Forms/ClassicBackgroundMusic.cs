using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicMusic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicBackgroundMusic : Form
    {
        private readonly TextBox _prompt = new TextBox { Multiline = true, Dock = DockStyle.Fill };
        private readonly ComboBox _preset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill }, _backend = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly NumericUpDown _bpm = Number(40, 240, 100), _seconds = Number(20, 240, 60), _seed = Number(-1, int.MaxValue, 42), _target = Number(1, 10800, 60), _musicLevel = Number(0, 200, 35), _originalLevel = Number(0, 200, 100);
        private readonly Button _generate = new Button { AutoSize = true }, _mix = new Button { AutoSize = true }, _cancel = new Button { AutoSize = true };
        private readonly Label _status = new Label { Dock = DockStyle.Fill, AutoEllipsis = true };
        private CancellationTokenSource _operation; private string _source, _music; private bool _closing; private SoundPlayer _preview;
        private static string L(string ru, string en) => ClassicAssistedText.L(ru, en);
        private static NumericUpDown Number(int min, int max, int value) => new NumericUpDown { Minimum = min, Maximum = max, Value = value, Dock = DockStyle.Fill };
        public ClassicBackgroundMusic(string source = null, double seconds = 60)
        {
            _source = source; Text = L("Фоновая музыка — ACE-Step 1.5", "Background music — ACE-Step 1.5"); Size = new Size(730, 660); MinimumSize = new Size(600, 570); StartPosition = FormStartPosition.CenterParent;
            _target.Value = Math.Max(1, Math.Min(10800, (decimal)seconds));
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12), ColumnCount = 2, RowCount = 12, AutoScroll = true }; table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 215)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var labels = new[] { L("Пресет", "Preset"), L("Описание музыки", "Music prompt"), "BPM", L("Генерировать, сек.", "Generate, seconds"), "Seed (-1 = random)", L("Длина результата, сек.", "Output length, seconds"), L("Вычисления", "Compute"), L("Музыка, %", "Music volume, %"), L("Исходный звук, %", "Original audio, %") };
            Control[] fields = { _preset, _prompt, _bpm, _seconds, _seed, _target, _backend, _musicLevel, _originalLevel };
            for (int i = 0; i < fields.Length; i++) { table.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 1 ? 120 : 34)); table.Controls.Add(new Label { Text = labels[i], Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, i); table.Controls.Add(fields[i], 1, i); }
            _preset.Items.AddRange(new object[] { L("Спокойное пианино", "Calm piano"), "Lo-fi", L("Технологии", "Technology"), L("Свое описание", "Custom") });
            _preset.SelectedIndexChanged += (s,e) => { string[] prompts = { "relaxing soft felt piano, slow gentle melody, warm reverb", "chill lo-fi hip hop, mellow rhodes piano, soft boom bap drums, warm bass", "modern clean electronic background music, soft synth plucks, light electronic beat" }; if (_preset.SelectedIndex < 3) _prompt.Text = prompts[_preset.SelectedIndex] + ", unobtrusive underscore for someone talking, instrumental, no vocals, steady tempo"; };
            _preset.SelectedIndex = 0; _backend.Items.AddRange(new object[] { "cpu", "vulkan", "cuda" }); _backend.SelectedIndex = 1;
            table.Controls.Add(new Label { Dock = DockStyle.Fill, AutoSize = true, Text = L("При первом запуске загрузится модель 5,8 ГБ. Музыка повторяется с музыкально согласованными стыками, нормализуется и плавно затихает.", "First use downloads a 5.8 GB model. Output uses musically aligned loop seams, normalization and fade-out.") }, 0, 9); table.SetColumnSpan(table.GetControlFromPosition(0, 9), 2); table.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; _generate.Text = L("Создать и сохранить WAV…", "Generate and save WAV…"); _mix.Text = L("Смешать с видео / озвучкой…", "Mix with video / speech…"); _mix.Enabled = false; _cancel.Text = L("Отмена", "Cancel");
            var preview = new Button { Text = L("Прослушать / остановить", "Preview / stop"), AutoSize = true };
            preview.Click += (s,e) => { if (_preview != null) { _preview.Stop(); _preview.Dispose(); _preview = null; } else if (File.Exists(_music)) { _preview = new SoundPlayer(_music); _preview.Play(); } };
            var load = new Button { Text = L("Открыть музыку…", "Open music…"), AutoSize = true };
            load.Click += (s,e) => { if (_operation != null) return; using (var dialog = new OpenFileDialog { Filter = "WAV|*.wav", Title = load.Text }) if (dialog.ShowDialog(this) == DialogResult.OK) { _preview?.Stop(); _preview?.Dispose(); _preview = null; _music = dialog.FileName; _mix.Enabled = true; _status.Text = Path.GetFileName(_music); } };
            buttons.Controls.AddRange(new Control[] { _generate, preview, load, _mix, _cancel }); table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); table.Controls.Add(buttons, 0, 10); table.SetColumnSpan(buttons, 2); table.Controls.Add(_status, 0, 11); table.SetColumnSpan(_status, 2); var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true }; scroll.Controls.Add(table); Controls.Add(scroll);
            _generate.Click += async (s,e) => { if (_operation != null) return; using (var d = new SaveFileDialog { Filter = "WAV|*.wav", FileName = "background_music.wav" }) if (d.ShowDialog(this) == DialogResult.OK) await Work(async token => { await ClassicMusicGenerator.Generate(_prompt.Text, (int)_bpm.Value, (int)_seconds.Value, (long)_seed.Value, _backend.Text, (double)_target.Value, d.FileName, token, new Progress<SpeechProgress>(p => _status.Text = p.Message)); _music = d.FileName; }); };
            _mix.Click += async (s,e) =>
            {
                if (_operation != null || !File.Exists(_music)) return;
                await Work(async token =>
                {
                    using (var dialog = new OpenFileDialog { Title = L("Видео или готовая озвучка", "Video or rendered speech"), FileName = File.Exists(_source) ? _source : "", Filter = "Media|*.mkv;*.mp4;*.mov;*.wav;*.mp3;*.m4a|All files|*.*" }) { if (dialog.ShowDialog(this) != DialogResult.OK) return; _source = dialog.FileName; }
                    var info = await ClassicMediaJobs.Probe(_source, token); var tracks = info.Streams.Where(track => track.Type == "audio").ToList(); int audioTrack = 0;
                    if (tracks.Count > 1) using (var choose = new ChooseAudioTrack(tracks.Select(track => track.ToString()).ToList(), 0)) { if (choose.ShowDialog(this) != DialogResult.OK) return; audioTrack = choose.SelectedTrack; }
                    using (var save = new SaveFileDialog { Filter = "Matroska|*.mkv|MP4|*.mp4|WAV|*.wav", FileName = Path.GetFileNameWithoutExtension(_source) + "_music" }) if (save.ShowDialog(this) == DialogResult.OK) await ClassicMusicGenerator.Mix(_source, _music, save.FileName, (double)_musicLevel.Value / 100, (double)_originalLevel.Value / 100, token, audioTrack);
                });
            };
            _cancel.Click += (s,e) => { if (_operation != null) _operation.Cancel(); else Close(); };
            FormClosing += (s,e) => { if (_operation != null) { _closing = true; e.Cancel = true; _operation.Cancel(); } }; FormClosed += (s,e) => { _preview?.Stop(); _preview?.Dispose(); };
        }
        private async Task Work(Func<CancellationToken, Task> work)
        {
            _preview?.Stop(); _preview?.Dispose(); _preview = null;
            _operation = new CancellationTokenSource(); _generate.Enabled = _mix.Enabled = false;
            try { await work(_operation.Token); _status.Text = L("Готово", "Done"); }
            catch (OperationCanceledException) { _status.Text = L("Отменено", "Cancelled"); }
            catch (Exception ex) { _status.Text = ex.Message; MessageBox.Show(this, ex.Message, Text); }
            finally { _operation.Dispose(); _operation = null; _generate.Enabled = true; _mix.Enabled = File.Exists(_music); if (_closing) Close(); }
        }
    }
}
