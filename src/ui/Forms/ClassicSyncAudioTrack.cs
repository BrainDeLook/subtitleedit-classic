using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicSyncAudioTrack : Form
    {
        private readonly string _input;
        private readonly int _initialTrack;
        private readonly ComboBox _tracks = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly NumericUpDown _shift = new NumericUpDown { Minimum = -3600000, Maximum = 3600000, Increment = 10, Width = 150 };
        private readonly NumericUpDown _position = new NumericUpDown { Minimum = 0, Maximum = 1000000, DecimalPlaces = 3, Width = 150 };
        private readonly Button _preview = new Button { AutoSize = true };
        private readonly Button _export = new Button { AutoSize = true };
        private readonly Button _cancel = new Button { AutoSize = true };
        private readonly Label _status = new Label { Dock = DockStyle.Fill, AutoSize = true };
        private ClassicMediaInfo _media;
        private CancellationTokenSource _operation;
        public ClassicSyncAudioTrack(string input, int initialTrack, double position)
        {
            UiUtil.PreInitialize(this); _input = input; _initialTrack = initialTrack;
            Text = L("Синхронизировать аудиодорожку", "Sync audio track"); Size = new Size(740, 340); MinimumSize = new Size(600, 320); StartPosition = FormStartPosition.CenterParent;
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 6 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.Controls.Add(new Label { Text = L("Аудиодорожка", "Audio track"), AutoSize = true }, 0, 0); table.Controls.Add(_tracks, 1, 0);
            table.Controls.Add(new Label { Text = L("Сдвиг, мс (+ = позже)", "Offset, ms (+ = later)"), AutoSize = true }, 0, 1); table.Controls.Add(_shift, 1, 1);
            table.Controls.Add(new Label { Text = L("Предпросмотр с секунды", "Preview from second"), AutoSize = true }, 0, 2); table.Controls.Add(_position, 1, 2); _position.Value = (decimal)Math.Max(0, Math.Min(1000000, position));
            var hint = new Label { AutoSize = true, MaximumSize = new Size(650, 0), Text = L("Экспорт меняет временное смещение только выбранной дорожки. Потоки копируются без смешивания. Предпросмотр создаёт короткий перекодированный фрагмент.", "Export shifts only the selected track. Streams are copied without mixing. Preview renders a short encoded clip.") };
            table.Controls.Add(hint, 0, 3); table.SetColumnSpan(hint, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            _preview.Text = L("Предпросмотр 12 секунд", "Preview 12 seconds"); _export.Text = L("Экспорт…", "Export…"); _cancel.Text = L("Закрыть", "Close"); _export.Enabled = _preview.Enabled = false;
            buttons.Controls.AddRange(new Control[] { _preview, _export, _cancel }); table.Controls.Add(buttons, 0, 4); table.SetColumnSpan(buttons, 2); table.Controls.Add(_status, 0, 5); table.SetColumnSpan(_status, 2); Controls.Add(table);
            _preview.Click += async (s, e) => await Run(true); _export.Click += async (s, e) => await Run(false);
            _cancel.Click += (s, e) => { if (_operation != null) _operation.Cancel(); else Close(); };
            FormClosing += (s, e) => { if (_operation != null) { e.Cancel = true; _operation.Cancel(); } };
            Shown += async (s, e) =>
            {
                _status.Text = L("Чтение дорожек; при необходимости загружается FFprobe…", "Reading tracks; FFprobe is downloaded if needed…");
                _operation = new CancellationTokenSource();
                try { _media = await ClassicMediaJobs.Probe(_input, _operation.Token); foreach (var track in _media.Streams.Where(t => t.Type == "audio")) _tracks.Items.Add(track); if (_tracks.Items.Count > 0) _tracks.SelectedIndex = Math.Min(_tracks.Items.Count - 1, Math.Max(0, _initialTrack)); _export.Enabled = _preview.Enabled = _tracks.Items.Count > 0; _status.Text = _tracks.Items.Count > 0 ? L("Выберите дорожку и величину сдвига.", "Select a track and an offset.") : L("Аудиодорожки не найдены.", "No audio tracks found."); }
                catch (OperationCanceledException) { }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Net.Http.HttpRequestException || ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException) { _status.Text = ex.Message; }
                finally { _operation.Dispose(); _operation = null; }
            };
            UiUtil.FixFonts(this);
        }
        private static string L(string ru, string en) => ClassicAssistedText.L(ru, en);
        private async Task Run(bool preview)
        {
            var stream = (ClassicMediaStream)_tracks.SelectedItem;
            string output;
            if (preview) output = Path.Combine(Path.GetTempPath(), "se-audio-sync-" + Guid.NewGuid().ToString("N") + ".mp4");
            else using (var dialog = new SaveFileDialog { Filter = "Matroska|*.mkv|MP4|*.mp4|MOV|*.mov", FileName = Path.GetFileNameWithoutExtension(_input) + "_synced.mkv", InitialDirectory = Path.GetDirectoryName(_input) }) { if (dialog.ShowDialog(this) != DialogResult.OK) return; output = dialog.FileName; }
            _operation = new CancellationTokenSource(); _tracks.Enabled = _shift.Enabled = _position.Enabled = _preview.Enabled = _export.Enabled = false; _status.Text = L("Обработка…", "Processing…");
            try
            {
                await ClassicVideoTools.ShiftAudioTrack(_input, output, _media, stream.Index, (double)_shift.Value / 1000, preview ? (double?)_position.Value : null, _operation.Token);
                if (preview) { using (var player = new PreviewVideo(output, null, new Subtitle())) player.ShowDialog(this); }
                _status.Text = preview ? L("Предпросмотр завершён", "Preview finished") : output;
            }
            catch (OperationCanceledException) { _status.Text = L("Отменено", "Cancelled"); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Net.Http.HttpRequestException || ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException || ex is ArgumentException) { _status.Text = ex.Message; }
            finally { if (preview && File.Exists(output)) File.Delete(output); _operation.Dispose(); _operation = null; _tracks.Enabled = _shift.Enabled = _position.Enabled = _preview.Enabled = _export.Enabled = true; }
        }
    }
}
