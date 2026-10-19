using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using Nikse.SubtitleEdit.Logic.ClassicFormats;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicVideoToolsForm : Form
    {
        private readonly TextBox _path = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        private readonly CheckedListBox _streams = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, HorizontalScrollbar = true };
        private readonly DataGridView _cuts = Grid(), _chapters = Grid();
        private readonly ComboBox _audio = Combo(), _transition = Combo(), _profile = Combo();
        private readonly NumericUpDown _fade = new NumericUpDown { Minimum = 0, Maximum = 10, DecimalPlaces = 2, Value = .5m, Width = 85 };
        private readonly CheckBox _mix = new CheckBox { AutoSize = true };
        private readonly TabControl _tabs = new TabControl { Dock = DockStyle.Fill };
        private readonly Label _status = new Label { Dock = DockStyle.Fill };
        private readonly Button _run = new Button { AutoSize = true };
        private readonly Button _open = new Button { AutoSize = true }, _external = new Button { AutoSize = true };
        private CancellationTokenSource _operation; private ClassicMediaInfo _info; private bool _closing;
        private static string L(string ru, string en) => ClassicAssistedText.L(ru, en);
        private static DataGridView Grid() => new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        private static ComboBox Combo() => new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
        public ClassicVideoToolsForm(string source)
        {
            Text = L("Видеоинструменты: монтаж, перекодирование, дорожки и главы", "Video tools: cuts, encoding, streams and chapters");
            Size = new Size(1050, 730); MinimumSize = new Size(800, 580); StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), RowCount = 4 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 37)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 }; top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            _open.Text = L("Открыть видео…", "Open video…"); top.Controls.Add(_path); top.Controls.Add(_open, 1, 0); _open.Click += async (s, e) => { using (var d = new OpenFileDialog { Filter = "Media|*.mkv;*.mp4;*.mov;*.ts;*.m2ts;*.avi;*.webm|All files|*.*" }) if (d.ShowDialog(this) == DialogResult.OK) await LoadVideo(d.FileName); }; root.Controls.Add(top, 0, 0);
            _cuts.Columns.Add("start", L("Начало, секунды", "Start, seconds")); _cuts.Columns.Add("end", L("Конец, секунды", "End, seconds"));
            _transition.Items.AddRange(new object[] { "none", "fade", "fadeblack", "wipeleft", "slideright" }); _transition.SelectedIndex = 0;
            var cutPanel = Panel(_cuts, new Control[] { new Label { Text = L("Переход", "Transition"), AutoSize = true }, _transition, _fade, new Label { Text = L("сек.; звук:", "sec.; audio:"), AutoSize = true }, _audio });
            Page(L("Монтаж", "Cut / join"), cutPanel);
            _profile.Items.AddRange(new object[] { "H.264 — GOP 12", "H.264 — All-I", "ProRes 422 HQ (MOV/MKV)" }); _profile.SelectedIndex = 0;
            Page(L("Для видеоредактора", "Editing codec"), Panel(new Label { Text = L("Видео перекодируется выбранным профилем. Аудиодорожки выбираются во вкладке «Дорожки» и сохраняются отдельно.", "Video uses the selected profile. Select audio streams on the Streams tab; each remains separate."), Dock = DockStyle.Fill }, new Control[] { _profile }));
            _external.Text = L("Добавить внешний файл…", "Add external file…"); _mix.Text = L("Смешать отмеченные аудиодорожки в одну стереодорожку", "Mix checked audio streams into one stereo stream");
            _external.Click += async (s, e) => { if (_operation != null) return; using (var d = new OpenFileDialog { Filter = "Media / subtitles|*.*" }) if (d.ShowDialog(this) == DialogResult.OK) await AddExternal(d.FileName); };
            Page(L("Дорожки", "Streams"), Panel(_streams, new Control[] { _external, _mix }));
            _chapters.Columns.Add("time", L("Начало, секунды", "Start, seconds")); _chapters.Columns.Add("title", L("Название главы", "Chapter title"));
            var import = new Button { Text = L("Импорт файла глав…", "Import chapter file…"), AutoSize = true }; var export = new Button { Text = L("Экспорт глав…", "Export chapters…"), AutoSize = true };
            import.Click += (s, e) => ImportChapters(); export.Click += (s, e) => ExportChapters();
            Page(L("Главы", "Chapters"), Panel(_chapters, new Control[] { import, export }));
            root.Controls.Add(_tabs, 0, 1); root.Controls.Add(_status, 0, 2);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; var close = new Button { Text = L("Закрыть / отменить", "Close / cancel"), AutoSize = true }; close.Click += (s, e) => { if (_operation != null) _operation.Cancel(); else Close(); };
            _run.Text = L("Сохранить результат…", "Save result…"); _run.Enabled = false; _run.Click += async (s, e) => await Run(); footer.Controls.Add(close); footer.Controls.Add(_run); root.Controls.Add(footer, 0, 3); Controls.Add(root);
            FormClosing += (s, e) => { if (_operation != null) { _closing = true; _operation.Cancel(); e.Cancel = true; } };
            Shown += async (s, e) => { if (File.Exists(source)) await LoadVideo(source); }; UiUtil.FixFonts(this);
        }
        private static Control Panel(Control content, Control[] controls)
        { var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 }; panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true }; bar.Controls.AddRange(controls); panel.Controls.Add(bar, 0, 0); panel.Controls.Add(content, 0, 1); return panel; }
        private void Page(string text, Control panel) { var page = new TabPage(text); page.Controls.Add(panel); _tabs.TabPages.Add(page); }
        private void Busy(bool value) { _tabs.Enabled = !value; _open.Enabled = !value; _run.Enabled = !value && _info != null; }
        private async Task LoadVideo(string file)
        {
            if (_operation != null) return; _operation = new CancellationTokenSource(); Busy(true); _status.Text = L("Чтение видео…", "Reading video…");
            try
            {
                var info = await ClassicMediaJobs.Probe(file, _operation.Token); if (!info.Streams.Any(s => s.Type == "video")) throw new InvalidOperationException("No video stream.");
                var chapters = JObject.Parse(await ClassicMediaJobs.Capture(Path.Combine(Path.GetDirectoryName(Configuration.Settings.General.FFmpegLocation), "ffprobe.exe"), "-v error -show_chapters -of json " + ClassicMediaJobs.Q(file), _operation.Token));
                _info = info; _path.Text = file; _streams.Items.Clear(); _audio.Items.Clear(); _audio.Items.Add(L("Без звука", "No audio"));
                foreach (var stream in info.Streams.Where(s => s.Type == "video" || s.Type == "audio" || s.Type == "subtitle")) { _streams.Items.Add(new ClassicMappedStream { File = file, Stream = stream }, true); if (stream.Type == "audio") _audio.Items.Add(stream); }
                _audio.SelectedIndex = _audio.Items.Count > 1 ? 1 : 0; _cuts.Rows.Clear(); _cuts.Rows.Add(0, info.Seconds.ToString("0.###", CultureInfo.CurrentCulture)); _chapters.Rows.Clear();
                foreach (var c in chapters["chapters"] ?? new JArray()) _chapters.Rows.Add(((double?)c["start_time"] ?? 0).ToString(CultureInfo.CurrentCulture), (string)c["tags"]?["title"] ?? "");
                _status.Text = L("Длительность: ", "Duration: ") + TimeSpan.FromSeconds(info.Seconds) + " | " + info.Width + "×" + info.Height;
            }
            catch (OperationCanceledException) { _status.Text = L("Отменено", "Cancelled"); } catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
            finally { _operation.Dispose(); _operation = null; Busy(false); if (_closing) Close(); }
        }
        private async Task AddExternal(string file)
        {
            _operation = new CancellationTokenSource(); Busy(true);
            try { var info = await ClassicMediaJobs.Probe(file, _operation.Token); foreach (var stream in info.Streams.Where(s => s.Type == "video" || s.Type == "audio" || s.Type == "subtitle")) _streams.Items.Add(new ClassicMappedStream { File = file, Stream = stream }, true); }
            catch (OperationCanceledException) { } catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
            finally { _operation.Dispose(); _operation = null; Busy(false); if (_closing) Close(); }
        }
        private static double Seconds(object value)
        { if (!double.TryParse(Convert.ToString(value), NumberStyles.Float, CultureInfo.CurrentCulture, out var result) && !double.TryParse(Convert.ToString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out result)) throw new FormatException("Enter a time in seconds."); if (double.IsNaN(result) || double.IsInfinity(result)) throw new FormatException("Invalid time."); return result; }
        private List<ClassicVideoChapter> ReadChapters() => _chapters.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => new ClassicVideoChapter { Start = Seconds(r.Cells[0].Value), Title = Convert.ToString(r.Cells[1].Value) }).ToList();
        private void ImportChapters()
        {
            try { using (var d = new OpenFileDialog { Filter = "Chapters|*.xml;*.txt;*.ffmetadata;*.edl|All files|*.*" }) if (d.ShowDialog(this) == DialogResult.OK)
                { var lines = File.ReadAllLines(d.FileName).ToList(); Subtitle parsed = null; ClassicFormatRegistry.Register(); foreach (var format in ClassicFormatRegistry.Added.Where(f => f.Name.IndexOf("chapter", StringComparison.OrdinalIgnoreCase) >= 0 || f is FfmpegMetadataChapters)) { var candidate = new Subtitle(); if (!format.IsMine(lines, d.FileName)) continue; format.LoadSubtitle(candidate, lines, d.FileName); if (candidate.Paragraphs.Count > 0) { parsed = candidate; break; } } if (parsed == null) throw new InvalidDataException("Unrecognized chapter format."); _chapters.Rows.Clear(); foreach (var p in parsed.Paragraphs) _chapters.Rows.Add((p.StartTime.TotalMilliseconds / 1000).ToString(CultureInfo.CurrentCulture), p.Text); } }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
        }
        private void ExportChapters()
        {
            try { _chapters.EndEdit(); var chapters = ReadChapters(); if (_info != null) ClassicVideoTools.ChapterMetadata(chapters, _info.Seconds); using (var d = new SaveFileDialog { Filter = "FFmetadata|*.ffmetadata|Matroska XML|*.xml|YouTube chapters|*.txt", FileName = "chapters" }) if (d.ShowDialog(this) == DialogResult.OK) { var subtitle = new Subtitle(); foreach (var c in chapters) subtitle.Paragraphs.Add(new Paragraph(c.Title, c.Start * 1000, c.Start * 1000 + 4000)); SubtitleFormat format = d.FilterIndex == 2 ? (SubtitleFormat)new MatroskaChaptersXml() : d.FilterIndex == 3 ? new YouTubeChapters() : new FfmpegMetadataChapters(); File.WriteAllText(d.FileName, format.ToText(subtitle, "chapters"), new System.Text.UTF8Encoding(false)); } }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
        }
        private async Task Run()
        {
            if (_operation != null || _info == null) return; _cuts.EndEdit(); _chapters.EndEdit();
            using (var d = new SaveFileDialog { Filter = "Matroska|*.mkv|MP4|*.mp4|QuickTime|*.mov", FileName = Path.GetFileNameWithoutExtension(_path.Text) + "_edited" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                _operation = new CancellationTokenSource(); Busy(true); _status.Text = L("Обработка…", "Processing…");
                try
                {
                    if (_tabs.SelectedIndex == 0) { var clips = _cuts.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => new ClassicCutSegment { Start = Seconds(r.Cells[0].Value), End = Seconds(r.Cells[1].Value) }).ToList(); await ClassicVideoTools.Cut(_path.Text, d.FileName, clips, (_audio.SelectedItem as ClassicMediaStream)?.Index ?? -1, (double)_fade.Value, (string)_transition.SelectedItem, _info.Seconds, _operation.Token); }
                    else if (_tabs.SelectedIndex == 1) await ClassicVideoTools.Reencode(_path.Text, d.FileName, _streams.CheckedItems.Cast<ClassicMappedStream>().Where(s => s.File == _path.Text && s.Stream.Type == "audio").Select(s => s.Stream.Index), _profile.SelectedIndex, _operation.Token);
                    else if (_tabs.SelectedIndex == 2) await ClassicVideoTools.Remux(_path.Text, d.FileName, _streams.CheckedItems.Cast<ClassicMappedStream>().ToList(), _mix.Checked, _operation.Token);
                    else await ClassicVideoTools.Chapters(_path.Text, d.FileName, ReadChapters(), _info.Seconds, _operation.Token);
                    _status.Text = L("Сохранено: ", "Saved: ") + d.FileName;
                }
                catch (OperationCanceledException) { _status.Text = L("Отменено", "Cancelled"); } catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
                finally { _operation.Dispose(); _operation = null; Busy(false); if (_closing) Close(); }
            }
        }
    }
}
