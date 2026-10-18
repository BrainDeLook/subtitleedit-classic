using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Compare
    {
        private Panel _classicInlinePanel;
        private TextBox _classicInlineText;
        private NikseTimeUpDown _classicInlineStart, _classicInlineEnd;
        private Label _classicInlineCaption;
        private string _classicInlineId, _classicInlineReferenceId;
        private int _classicReferencePreviewHeight;
        private bool ClassicInlineEditing => _classicInlineId != null;

        private void InitializeClassicInlineEditor()
        {
            _classicReferencePreviewHeight = richTextBox2.Height;
            _classicInlinePanel = new Panel { Visible = false, BorderStyle = BorderStyle.FixedSingle, Height = 153 };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(4), ColumnCount = 1, RowCount = 4 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            _classicInlineCaption = new Label { Dock = DockStyle.Fill, AutoEllipsis = true };
            var times = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            times.Controls.Add(new Label { Text = C("Начало:", "Start:"), AutoSize = true, Margin = new Padding(0, 5, 3, 0) });
            _classicInlineStart = new NikseTimeUpDown { Size = new Size(135, 23) }; times.Controls.Add(_classicInlineStart);
            times.Controls.Add(new Label { Text = C("Конец:", "End:"), AutoSize = true, Margin = new Padding(9, 5, 3, 0) });
            _classicInlineEnd = new NikseTimeUpDown { Size = new Size(135, 23) }; times.Controls.Add(_classicInlineEnd);
            _classicInlineText = new TextBox { Multiline = true, AcceptsReturn = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
            MergeButton(buttons, C("Сохранить (Ctrl+Enter)", "Save (Ctrl+Enter)"), SaveClassicInlineEdit);
            MergeButton(buttons, C("Отменить (Esc)", "Cancel (Esc)"), CancelClassicInlineEdit);
            MergeButton(buttons, C("← Текст", "← Text"), () => TakeClassicInlineReference("text"));
            MergeButton(buttons, C("← Время", "← Timing"), () => TakeClassicInlineReference("timing"));
            layout.Controls.Add(_classicInlineCaption, 0, 0); layout.Controls.Add(times, 0, 1); layout.Controls.Add(_classicInlineText, 0, 2); layout.Controls.Add(buttons, 0, 3);
            _classicInlinePanel.Controls.Add(layout); Controls.Add(_classicInlinePanel);
        }

        private void BeginClassicInlineEdit()
        {
            if (!_classicEditable || ClassicInlineEditing) return;
            var pair = SelectedClassicPair(); var cue = pair == null ? null : _subtitle1.GetParagraphOrDefault(pair.Left);
            if (cue == null) return;
            CancelClassicSync();
            _classicInlineId = cue.Id; _classicInlineReferenceId = _subtitle2?.GetParagraphOrDefault(pair.Right)?.Id;
            _classicInlineCaption.Text = C("Правка реплики #", "Editing cue #") + cue.Number;
            _classicInlineText.Text = cue.Text; _classicInlineStart.TimeCode = new TimeCode(cue.StartTime.TotalMilliseconds); _classicInlineEnd.TimeCode = new TimeCode(cue.EndTime.TotalMilliseconds);
            _classicInlinePanel.Visible = true; richTextBox1.Visible = false; Compare_Resize(null, null); RefreshClassicMerge();
            _classicInlineText.Focus(); _classicInlineText.SelectAll();
        }

        private void SaveClassicInlineEdit()
        {
            if (!ClassicInlineEditing || !_classicEditable) return;
            var cue = _subtitle1.GetParagraphOrDefaultById(_classicInlineId);
            if (cue == null) { CancelClassicInlineEdit(); return; }
            var start = _classicInlineStart.TimeCode.TotalMilliseconds; var end = _classicInlineEnd.TimeCode.TotalMilliseconds;
            if (start < 0 || end < start || end > TimeCode.MaxTimeTotalMilliseconds)
            {
                _classicInlineCaption.Text = C("Ошибка: конец должен быть не раньше начала, время — в допустимом диапазоне.", "Invalid times: end must not precede start; times must be within bounds."); return;
            }
            var id = cue.Id;
            if (cue.Text != _classicInlineText.Text || cue.StartTime.TotalMilliseconds != start || cue.EndTime.TotalMilliseconds != end)
            {
                RememberClassicCompareEdit(); cue.Text = _classicInlineText.Text; cue.StartTime.TotalMilliseconds = start; cue.EndTime.TotalMilliseconds = end;
            }
            CancelClassicInlineEdit(); RecompareClassicMerge(); FocusClassicCurrentId(id);
        }

        private void CancelClassicInlineEdit()
        {
            if (_classicInlinePanel == null) return;
            _classicInlineId = _classicInlineReferenceId = null; _classicInlinePanel.Visible = false; richTextBox1.Visible = true;
            Compare_Resize(null, null); RefreshClassicMerge();
        }

        private void TakeClassicInlineReference(string part)
        {
            if (!ClassicInlineEditing) return;
            var cue = _subtitle2?.GetParagraphOrDefaultById(_classicInlineReferenceId); if (cue == null) return;
            if (part != "timing") _classicInlineText.Text = cue.Text;
            if (part != "text") { _classicInlineStart.TimeCode = new TimeCode(cue.StartTime.TotalMilliseconds); _classicInlineEnd.TimeCode = new TimeCode(cue.EndTime.TotalMilliseconds); }
        }

        private void FocusClassicCurrentId(string id)
        {
            for (var row = 0; row < subtitleListView1.Items.Count; row++)
            {
                var pair = subtitleListView1.Items[row].Tag as ClassicComparePair;
                if (pair != null && _subtitle1.GetParagraphOrDefault(pair.Left)?.Id == id) { subtitleListView1.SelectIndexAndEnsureVisible(row); subtitleListView1.Focus(); break; }
            }
        }

        private bool HandleClassicCompleteKey(ref Message msg, Keys keys)
        {
            if (keys == UiUtil.HelpKeys) { UiUtil.OpenUrl("https://github.com/SubtitleEdit/subtitleedit/blob/v5.3.0/docs/features/compare.md"); return true; }
            if (ClassicInlineEditing)
            {
                if (keys == (Keys.Control | Keys.Enter)) { SaveClassicInlineEdit(); return true; }
                if (keys == Keys.Escape) { CancelClassicInlineEdit(); return true; }
                if (keys == (Keys.Alt | Keys.Left)) { TakeClassicInlineReference("both"); return true; }
                return false;
            }
            if (keys == Keys.F2) { BeginClassicInlineEdit(); return true; }
            if (keys == Keys.Delete && (subtitleListView1.Focused || subtitleListView2.Focused)) { DeleteClassicCurrent(); return true; }
            return false;
        }

        private void LayoutClassicInlineEditor()
        {
            if (_classicInlinePanel == null) return;
            if (ClassicInlineEditing)
            {
                var top = richTextBox1.Bottom - _classicInlinePanel.Height;
                _classicInlinePanel.SetBounds(subtitleListView1.Left, top, subtitleListView1.Width, _classicInlinePanel.Height);
                subtitleListView1.Height = subtitleListView2.Height = Math.Max(30, top - subtitleListView1.Top - 6);
                richTextBox2.SetBounds(subtitleListView2.Left, top, subtitleListView2.Width, _classicInlinePanel.Height);
                _classicInlinePanel.BringToFront();
            }
            else richTextBox2.SetBounds(subtitleListView2.Left, richTextBox1.Bottom - _classicReferencePreviewHeight, subtitleListView2.Width, _classicReferencePreviewHeight);
        }
    }
}
