using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicYttStyles : Form
    {
        private readonly YttHead _head;
        private readonly TabControl _tabs = new TabControl { Dock = DockStyle.Fill };
        private readonly ListBox[] _lists = new ListBox[3];
        private readonly PropertyGrid[] _properties = new PropertyGrid[3];
        public string Header => _head.ToHeader();
        public int? PenId { get; private set; }
        public int? PositionId { get; private set; }
        public int? WindowStyleId { get; private set; }
        private readonly CheckBox _assign = new CheckBox { AutoSize = true };
        public bool AssignToSelection => _assign.Checked;

        public ClassicYttStyles(string header)
        {
            UiUtil.PreInitialize(this); _head = YttHead.Parse(header);
            Text = "YouTube timed text — " + L("стили и позиции", "styles and positions");
            Size = new Size(830, 600); MinimumSize = new Size(660, 430); StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), RowCount = 2 };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var names = new[] { L("Стиль текста (pen)", "Text style (pen)"), L("Позиция окна", "Window position"), L("Стиль окна", "Window style") };
            for (int i = 0; i < 3; i++)
            {
                int pageIndex = i;
                var page = new TabPage(names[i]); var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 230 };
                var list = new ListBox { Dock = DockStyle.Fill, DisplayMember = "Id" }; var grid = new PropertyGrid { Dock = DockStyle.Fill, ToolbarVisible = false };
                _lists[i] = list; _properties[i] = grid;
                list.SelectedIndexChanged += (s, e) => grid.SelectedObject = list.SelectedItem;
                grid.PropertyValueChanged += (s, e) => list.Refresh();
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
                var add = new Button { Text = L("Добавить", "Add"), AutoSize = true };
                add.Click += (s, e) =>
                {
                    object item;
                    if (pageIndex == 0) { var pen = new YttPen { Id = _head.GetNextPenId() }; _head.Pens.Add(pen); item = pen; }
                    else if (pageIndex == 1) { var position = new YttWindowPosition { Id = _head.GetNextWindowPositionId() }; _head.WindowPositions.Add(position); item = position; }
                    else { var style = new YttWindowStyle { Id = _head.GetNextWindowStyleId() }; _head.WindowStyles.Add(style); item = style; }
                    list.Items.Add(item); list.SelectedItem = item;
                };
                // Existing IDs stay available: removing a referenced pen would silently change other lines.
                buttons.Controls.Add(add); split.Panel1.Controls.Add(list); split.Panel1.Controls.Add(buttons); split.Panel2.Controls.Add(grid); page.Controls.Add(split); _tabs.TabPages.Add(page);
            }
            foreach (var pen in _head.Pens) _lists[0].Items.Add(pen);
            foreach (var position in _head.WindowPositions) _lists[1].Items.Add(position);
            foreach (var style in _head.WindowStyles) _lists[2].Items.Add(style);
            for (int i = 0; i < 3; i++) if (_lists[i].Items.Count > 0) _lists[i].SelectedIndex = 0;
            var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button { Text = L("Применить", "Apply"), AutoSize = true }; var cancel = new Button { Text = L("Отмена", "Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
            _assign.Text = L("Назначить выбранные значения выделенным репликам", "Assign selected values to selected cues");
            ok.Click += (s, e) =>
            {
                var ids = new[] { _head.Pens.Select(p => p.Id), _head.WindowPositions.Select(p => p.Id), _head.WindowStyles.Select(p => p.Id) };
                if (ids.Any(values => values.Any(id => id < 0) || values.Distinct().Count() != values.Count()))
                { MessageBox.Show(this, L("ID должны быть неотрицательными и уникальными внутри своей группы.", "IDs must be nonnegative and unique within their group.")); return; }
                PenId = (_lists[0].SelectedItem as YttPen)?.Id; PositionId = (_lists[1].SelectedItem as YttWindowPosition)?.Id; WindowStyleId = (_lists[2].SelectedItem as YttWindowStyle)?.Id;
                DialogResult = DialogResult.OK;
            };
            footer.Controls.AddRange(new Control[] { ok, cancel, _assign }); root.Controls.Add(_tabs, 0, 0); root.Controls.Add(footer, 0, 1); Controls.Add(root); CancelButton = cancel; UiUtil.FixFonts(this);
        }
        private static string L(string ru, string en) => ClassicAssistedText.L(ru, en);
    }
}
