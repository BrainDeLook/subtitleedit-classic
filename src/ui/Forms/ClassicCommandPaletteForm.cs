using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicCommandPaletteForm : Form
    {
        private readonly List<ClassicPaletteCommand> _commands;
        private readonly TextBox _search;
        private readonly DataGridView _list;
        private readonly Label _status;
        private readonly Button _ok;
        private readonly bool _ru;
        public ClassicPaletteCommand SelectedCommand => _list.CurrentRow?.Tag as ClassicPaletteCommand;
        public ClassicCommandPaletteForm(IEnumerable<ClassicPaletteCommand> commands, Action configureShortcut = null)
        {
            UiUtil.PreInitialize(this);
            _commands = commands.ToList();
            _ru = (Configuration.Settings.General.Language ?? "").StartsWith("ru", StringComparison.OrdinalIgnoreCase);
            Text = _ru ? "Палитра команд" : "Command palette";
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false;
            ClientSize = new Size(840, 510); MinimumSize = new Size(620, 360);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 4 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            _search = new TextBox { Name = "paletteSearch", Dock = DockStyle.Fill, AccessibleName = _ru ? "Поиск команды" : "Find a command" };
            _list = new DataGridView { Name = "paletteCommands", Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window, AccessibleName = _ru ? "Команды" : "Commands", TabStop = false };
            _list.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = _ru ? "Команда" : "Command", FillWeight = 55, SortMode = DataGridViewColumnSortMode.NotSortable });
            _list.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = _ru ? "Раздел" : "Group", FillWeight = 28, SortMode = DataGridViewColumnSortMode.NotSortable });
            _list.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = _ru ? "Клавиши" : "Shortcut", FillWeight = 22, SortMode = DataGridViewColumnSortMode.NotSortable });
            _status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = LanguageSettings.Current.General.Cancel, DialogResult = DialogResult.Cancel, AutoSize = true };
            _ok = new Button { Name = "paletteRun", Text = _ru ? "Выполнить" : "Run", AutoSize = true };
            _ok.Click += (s, e) => Choose();
            buttons.Controls.Add(cancel); buttons.Controls.Add(_ok);
            if (configureShortcut != null)
            {
                var shortcut = new Button { Text = _ru ? "Клавиши…" : "Shortcut…", AutoSize = true };
                shortcut.Click += (s,e) => configureShortcut(); buttons.Controls.Add(shortcut);
            }
            root.Controls.Add(_search, 0, 0); root.Controls.Add(_list, 0, 1); root.Controls.Add(_status, 0, 2); root.Controls.Add(buttons, 0, 3); Controls.Add(root);
            CancelButton = cancel; AcceptButton = _ok;
            _search.TextChanged += (s, e) => Filter();
            _list.SelectionChanged += (s, e) => _ok.Enabled = SelectedCommand?.Enabled == true;
            _list.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Choose(); };
            UiUtil.FixFonts(this); Filter(); Shown += (s, e) => _search.Focus();
        }
        private void Filter()
        {
            _list.Rows.Clear();
            foreach (var command in ClassicCommandPalette.Filter(_commands, _search.Text))
            {
                var i = _list.Rows.Add(command.Name, command.Group, command.Shortcut); var row = _list.Rows[i]; row.Tag = command;
                if (!command.Enabled) row.DefaultCellStyle.ForeColor = SystemColors.GrayText;
            }
            if (_list.Rows.Count > 0) { _list.CurrentCell = _list.Rows[0].Cells[0]; _list.Rows[0].Selected = true; }
            _ok.Enabled = SelectedCommand?.Enabled == true;
            _status.Text = _list.Rows.Count == 0 ? (_ru ? "Нет подходящих команд" : "No matching commands") : (_ru ? "Команд: " : "Commands: ") + _list.Rows.Count + "   ·   Enter / ↑ ↓ / PageUp PageDown / Esc";
        }
        private void Choose() { if (SelectedCommand?.Enabled == true) DialogResult = DialogResult.OK; }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == UiUtil.HelpKeys) { UiUtil.OpenUrl("https://github.com/SubtitleEdit/subtitleedit/blob/v5.3.0/docs/features/command-palette.md"); return true; }
            int delta = keyData == Keys.Down ? 1 : keyData == Keys.Up ? -1 : keyData == Keys.PageDown ? 10 : keyData == Keys.PageUp ? -10 : 0;
            if (delta != 0)
            {
                if (_list.Rows.Count > 0)
                {
                    var next = Math.Max(0, Math.Min(_list.Rows.Count - 1, (_list.CurrentRow?.Index ?? 0) + delta));
                    _list.CurrentCell = _list.Rows[next].Cells[0]; _list.Rows[next].Selected = true; _list.FirstDisplayedScrollingRowIndex = next;
                }
                return true;
            }
            if (keyData == Keys.Enter) { Choose(); return true; }
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
