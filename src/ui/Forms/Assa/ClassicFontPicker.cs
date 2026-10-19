using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
namespace Nikse.SubtitleEdit.Forms.Assa
{
    public sealed class ClassicFontPicker : Form
    {
        private readonly TabControl _tabs = new TabControl { Dock = DockStyle.Fill };
        private readonly TextBox _search = new TextBox { Dock = DockStyle.Fill };
        private readonly ListBox _installed = new ListBox { Dock = DockStyle.Fill }, _collected = new ListBox { Dock = DockStyle.Fill };
        private readonly Label _sample = new Label { Dock = DockStyle.Fill, Text = "Aa Бб — Пример / Sample", TextAlign = ContentAlignment.MiddleCenter };
        private readonly string[] _systemFonts, _collectionFonts;
        private Font _previewFont;
        public string SelectedFont => (_tabs.SelectedIndex == 0 ? _installed : _collected).SelectedItem as string;
        private static string L(string ru, string en) => ClassicAssistedText.L(ru, en);
        public ClassicFontPicker(string current)
        {
            Text = L("Выбор шрифта", "Choose font"); Size = new Size(600, 560); MinimumSize = new Size(430, 400); StartPosition = FormStartPosition.CenterParent;
            using (var fonts = new System.Drawing.Text.InstalledFontCollection()) _systemFonts = fonts.Families.Select(f => f.Name).Distinct().OrderBy(s => s).ToArray();
            _collectionFonts = ClassicFontCollection.CollectedNames().OrderBy(s => s).ToArray();
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), RowCount = 4, ColumnCount=1 }; root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            var system = new TabPage(L("Установленные", "Installed")); system.Controls.Add(_installed); var collected = new TabPage(L("Коллекция Subtitle Edit", "Subtitle Edit collection")); collected.Controls.Add(_collected); _tabs.TabPages.Add(system); _tabs.TabPages.Add(collected);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; var cancel = new Button { Text = L("Отмена", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true }; var ok = new Button { Text = L("Выбрать", "Select"), AutoSize = true }; ok.Click += (s, e) => { if (SelectedFont != null) DialogResult = DialogResult.OK; }; footer.Controls.Add(cancel); footer.Controls.Add(ok);
            root.Controls.Add(_search, 0, 0); root.Controls.Add(_tabs, 0, 1); root.Controls.Add(_sample, 0, 2); root.Controls.Add(footer, 0, 3); Controls.Add(root); AcceptButton = ok; CancelButton = cancel;
            _search.TextChanged += (s, e) => RefreshFonts(); _installed.SelectedIndexChanged += (s, e) => Preview(); _collected.SelectedIndexChanged += (s, e) => Preview(); _tabs.SelectedIndexChanged += (s, e) => Preview();
            _installed.DoubleClick += (s, e) => ok.PerformClick(); _collected.DoubleClick += (s, e) => ok.PerformClick(); RefreshFonts(); _installed.SelectedItem = current; _collected.SelectedItem = current; if (_installed.SelectedIndex < 0 && _collected.SelectedIndex >= 0) _tabs.SelectedIndex = 1;
            FormClosed += (s, e) => _previewFont?.Dispose(); UiUtil.FixFonts(this);
        }
        private void RefreshFonts() { _installed.Items.Clear(); _collected.Items.Clear(); _installed.Items.AddRange(_systemFonts.Where(f => f.IndexOf(_search.Text, StringComparison.OrdinalIgnoreCase) >= 0).ToArray()); _collected.Items.AddRange(_collectionFonts.Where(f => f.IndexOf(_search.Text, StringComparison.OrdinalIgnoreCase) >= 0).ToArray()); }
        private void Preview() { if (SelectedFont == null) return; try { var font = new Font(SelectedFont, 22); var old = _previewFont; _previewFont=font; _sample.Font = font; old?.Dispose(); } catch (ArgumentException) { } }
    }
}
