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
    public sealed class ClassicAssistedEditing : Form
    {
        private readonly List<ClassicAssistedChoice> _choices;
        private readonly bool _split;
        private readonly Panel _cards;
        private bool _fittingCards;
        public ClassicAssistedChoice SelectedChoice { get; private set; }
        public ClassicAssistedEditing(Paragraph source, List<ClassicAssistedChoice> choices, bool split, Action shortcuts)
        {
            UiUtil.PreInitialize(this); _choices = choices; _split = split;
            Text = split ? ClassicAssistedText.L("Умное разделение реплики", "Assisted split") : ClassicAssistedText.L("Умный перенос текста", "Assisted move");
            Size = new Size(850, 760); MinimumSize = new Size(570, 470); StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; ShowInTaskbar = false;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            var original = new TextBox { ReadOnly = true, Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Text = source.Text, AccessibleName = ClassicAssistedText.L("Исходная реплика", "Original subtitle") };
            _cards = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AccessibleName = ClassicAssistedText.L("Варианты изменения", "Suggestions") };
            for (var i = 0; i < choices.Count; i++) AddCard(i);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 5, 0, 0) };
            var cancel = new Button { Text = LanguageSettings.Current.General.Cancel, AutoSize = true, DialogResult = DialogResult.Cancel }; footer.Controls.Add(cancel);
            var keys = new Button { Text = ClassicAssistedText.L("Горячие клавиши…", "Shortcuts…"), AutoSize = true }; keys.Click += (s, e) => shortcuts?.Invoke(); footer.Controls.Add(keys);
            root.Controls.Add(original, 0, 0); root.Controls.Add(_cards, 0, 1); root.Controls.Add(footer, 0, 2); Controls.Add(root); CancelButton = cancel;
            _cards.SizeChanged += (s, e) => FitCards(); Shown += (s, e) => FitCards(); UiUtil.FixFonts(this);
        }
        private void AddCard(int index)
        {
            var choice = _choices[index]; var card = new TableLayoutPanel { ColumnCount = 1, RowCount = 1 + choice.Preview.Count * 2, Padding = new Padding(6), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 3, 0, 5), Height = 43 + choice.Preview.Count * 83, Width = 750 };
            var select = new Button { Text = (index + 1) + ". " + choice.Title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AccessibleName = (index + 1) + ". " + choice.Title };
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); card.Controls.Add(select, 0, 0); select.Click += (s, e) => Pick(index); card.Click += (s, e) => Pick(index);
            for (var i = 0; i < choice.Preview.Count; i++)
            {
                var p = choice.Preview[i]; var visible = HtmlUtil.RemoveHtmlTags(p.Text, true).Count(c => !char.IsControl(c)); var cps = p.DurationTotalMilliseconds > 0 ? visible * 1000.0 / p.DurationTotalMilliseconds : 0;
                var info = new Label { Dock = DockStyle.Fill, Text = p.StartTime + " → " + p.EndTime + "     " + visible + ClassicAssistedText.L(" симв., ", " chars, ") + cps.ToString("0.#") + ClassicAssistedText.L(" симв/с", " CPS"), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
                var preview = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Text = p.Text, AccessibleName = ClassicAssistedText.L("Результат ", "Result ") + (i + 1) };
                card.RowStyles.Add(new RowStyle(SizeType.Absolute, 23)); card.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); card.Controls.Add(info, 0, i * 2 + 1); card.Controls.Add(preview, 0, i * 2 + 2); info.Click += (s, e) => Pick(index);
            }
            _cards.Controls.Add(card);
        }
        private void FitCards()
        {
            if (_fittingCards) return;
            _fittingCards = true;
            try
            {
                var width = Math.Max(300, _cards.Width - SystemInformation.VerticalScrollBarWidth - 5);
                var scroll = -_cards.AutoScrollPosition.Y;
                _cards.AutoScroll = false;
                var y = 5;
                foreach (Control card in _cards.Controls) { card.SetBounds(0, y, width, card.Height); y += card.Height + 8; }
                _cards.AutoScrollMinSize = new Size(0, y);
                _cards.AutoScroll = true;
                _cards.AutoScrollPosition = new Point(0, scroll);
            }
            finally { _fittingCards = false; }
        }
        private void Pick(int index) { if (index < 0 || index >= _choices.Count) return; SelectedChoice = _choices[index]; DialogResult = DialogResult.OK; }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == UiUtil.HelpKeys) { UiUtil.OpenUrl("https://github.com/SubtitleEdit/subtitleedit/blob/v5.3.0/docs/features/assisted-split.md"); return true; }
            var code = keyData & Keys.KeyCode;
            if ((keyData & Keys.Modifiers) == Keys.None && ((code >= Keys.D1 && code <= Keys.D6) || (code >= Keys.NumPad1 && code <= Keys.NumPad6))) { Pick(code >= Keys.NumPad1 ? code - Keys.NumPad1 : code - Keys.D1); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
