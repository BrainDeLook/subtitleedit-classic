using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using Nikse.SubtitleEdit.Logic.ClassicImport;
namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicTableExportForm : Form
    {
        private readonly DataGridView _columns = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        private static string L(string ru, string en) => ClassicAssistedText.L(ru, en);
        public ClassicTableExportForm(Subtitle source)
        {
            Text = L("Экспорт CSV / XLSX: столбцы", "CSV / XLSX export: columns"); Size = new Size(720, 520); MinimumSize = new Size(570, 400); StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), RowCount = 3 }; root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; var header = new CheckBox { Text = L("Строка заголовка", "Header row"), Checked = true, AutoSize = true }; var times = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 }; times.Items.AddRange(new object[] { L("Тайм-код", "Time code"), L("Секунды", "Seconds"), L("Миллисекунды", "Milliseconds") }); times.SelectedIndex = 0; bar.Controls.Add(header); bar.Controls.Add(times);
            var field = new DataGridViewComboBoxColumn { HeaderText = L("Содержимое", "Field"), DataSource = ClassicTableExport.Fields }; _columns.Columns.Add(field); _columns.Columns.Add("header", L("Имя столбца", "Column header")); foreach (var value in new[] { "start", "end", "text", "actor" }) _columns.Rows.Add(value, value);
            foreach (var shift in new[] { -1, 1 }) { var step = shift; var move = new Button { Text = step < 0 ? "↑" : "↓", AutoSize = true }; move.Click += (s, e) => { _columns.EndEdit(); var row = _columns.CurrentRow; if (row == null || row.IsNewRow) return; var next = row.Index + step; if (next < 0 || next >= _columns.Rows.Count - 1) return; var values = row.Cells.Cast<DataGridViewCell>().Select(c => c.Value).ToArray(); var index = row.Index; _columns.Rows.RemoveAt(index); _columns.Rows.Insert(next, values); _columns.CurrentCell = _columns.Rows[next].Cells[0]; }; bar.Controls.Add(move); }
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; var close = new Button { Text = L("Закрыть", "Close"), AutoSize = true, DialogResult = DialogResult.Cancel }; var save = new Button { Text = L("Сохранить…", "Save…"), AutoSize = true }; save.Click += (s, e) => { try { _columns.EndEdit(); var columns = _columns.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => new ClassicTableColumn { Field = Convert.ToString(r.Cells[0].Value), Header = Convert.ToString(r.Cells[1].Value) }).ToList(); using (var d = new SaveFileDialog { Filter = "CSV|*.csv|Excel|*.xlsx", FileName = "subtitles" }) if (d.ShowDialog(this) == DialogResult.OK) { ClassicTableExport.Write(source, columns, header.Checked, times.SelectedIndex, d.FileName, d.FilterIndex == 2); DialogResult = DialogResult.OK; } } catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); } }; footer.Controls.Add(close); footer.Controls.Add(save); root.Controls.Add(bar, 0, 0); root.Controls.Add(_columns, 0, 1); root.Controls.Add(footer, 0, 2); Controls.Add(root); UiUtil.FixFonts(this);
        }
    }
}
