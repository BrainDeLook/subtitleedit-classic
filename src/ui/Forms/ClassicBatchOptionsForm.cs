using System;
using System.Drawing;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicBatchOptionsForm : Form
    {
        public ClassicBatch53Options Options { get; private set; }
        private static string L(string ru, string en) => ClassicAssistedText.L(ru, en);
        public ClassicBatchOptionsForm(ClassicBatch53Options current)
        {
            Options = JsonConvert.DeserializeObject<ClassicBatch53Options>(JsonConvert.SerializeObject(current));
            Text = L("Пакетная обработка: дополнительные параметры", "Batch Convert: additional options");
            Size = new Size(740, 690); MinimumSize = new Size(580, 520); StartPosition = FormStartPosition.CenterParent;
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var general = new TabPage(L("Общие", "General")); var ass = new TabPage("ASS / SSA"); var ocr = new TabPage("OCR"); tabs.TabPages.AddRange(new[] { general, ass, ocr });
            TableLayoutPanel Table(TabPage page) { var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12) }; t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56)); page.AutoScroll = true; page.Controls.Add(t); return t; }
            void Row(TableLayoutPanel t, string label, Control control, int height = 40) { int r = t.RowCount++; t.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); t.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, r); control.Dock = DockStyle.Fill; t.Controls.Add(control, 1, r); }
            var g = Table(general); var a = Table(ass); var o = Table(ocr);
            var languages = new TextBox { Text = Options.TargetLanguages }; Row(g, L("Языки перевода через запятую (ru,en)", "Translation targets, comma separated (de,fr)"), languages);
            var timestamp = new CheckBox { Checked = Options.PreserveSourceTimestamp, Text = L("Сохранять дату исходного файла", "Preserve source modification time") }; Row(g, "", timestamp, 55);
            var filter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; var ids = new[] { "all", "format", "extension", "error", "forced", "filename", "language" };
            filter.Items.AddRange(new object[] { L("Все файлы", "All files"), L("Формат", "Format"), L("Расширение", "Extension"), L("С ошибкой", "Failed"), L("Форсированные реплики", "Forced cues"), L("Имя файла содержит", "Filename contains"), L("Код языка в имени", "Language code in filename") }); filter.SelectedIndex = Math.Max(0, Array.IndexOf(ids, Options.Filter)); Row(g, L("Фильтр входных файлов", "Input filter"), filter);
            var value = new TextBox { Text = Options.FilterValue }; Row(g, L("Значение фильтра", "Filter value"), value);
            var style = new TextBox { Text = Options.StyleName }; Row(a, L("Имя стиля (пусто — все)", "Style name (empty = all)"), style);
            var embed = new CheckBox { Checked = Options.EmbedAssFonts, Text = L("Встроить используемые шрифты", "Embed used fonts") }; Row(a, "", embed);
            var trim = new CheckBox { Checked = Options.TrimAssFonts, Text = L("Оставить используемые символы", "Subset fonts to used characters") }; Row(a, "", trim);
            var properties = new TextBox { Multiline = true, ScrollBars = ScrollBars.Both, AcceptsReturn = true, Text = Options.StyleProperties.Replace("\n", Environment.NewLine) }; Row(a, L("Свойства, по одному на строку:\nFontName=Segoe UI\nFontSize=43\nPrimary=#FFFFFF", "One property per line:\nFontName=Segoe UI\nFontSize=43\nPrimary=#FFFFFF"), properties, 270);
            var mapping = new TextBox { Multiline = true, ScrollBars = ScrollBars.Both, AcceptsReturn = true, Text = Options.OcrLanguagesByFile.Replace("\n", Environment.NewLine) }; Row(o, L("Язык для каждого файла:\nfile.sup=rus\nother.sup=eng\nИначе используется общий язык OCR.", "Per-file OCR language:\nfile.sup=rus\nother.sup=eng\nOther files use the common OCR language."), mapping, 330);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(5) };
            var ok = new Button { Text = "OK", AutoSize = true }; var cancel = new Button { Text = L("Отмена", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true }; buttons.Controls.AddRange(new Control[] { cancel, ok });
            ok.Click += (s,e) => { try { Options.TargetLanguages = languages.Text; Options.PreserveSourceTimestamp = timestamp.Checked; Options.Filter = ids[filter.SelectedIndex]; Options.FilterValue = value.Text; Options.StyleName = style.Text; Options.EmbedAssFonts = embed.Checked; Options.TrimAssFonts = trim.Checked; Options.StyleProperties = properties.Text; Options.OcrLanguagesByFile = mapping.Text; Options.Validate(); DialogResult = DialogResult.OK; } catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); } };
            Controls.Add(tabs); Controls.Add(buttons); AcceptButton = ok; CancelButton = cancel;
        }
    }
}
