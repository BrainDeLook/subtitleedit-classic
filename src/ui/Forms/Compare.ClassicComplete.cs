using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Compare
    {
        private ComboBox _classicView;
        private CheckBox _classicIgnoreNumbers;
        private Button _classicEdit, _classicLoadSaved;
        private string _classicSavedFile;
        private bool _classicComparing, _classicSettingView;
        private ClassicCompareOverview _classicOverview;
        private int _classicAllCount, _classicDifferenceCount, _classicTextCount;
        private readonly Dictionary<ClassicComparePair, int> _classicAllIndexes = new Dictionary<ClassicComparePair, int>();
        private Dictionary<string, Paragraph> _classicOriginalCues = new Dictionary<string, Paragraph>();

        private void InitializeClassicComplete(FlowLayoutPanel actions)
        {
            _classicEdit = MergeButton(actions, C("Править (F2)", "Edit (F2)"), BeginClassicInlineEdit);
            _classicView = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 205 };
            var syncBar = (FlowLayoutPanel)_classicCancelSync.Parent;
            syncBar.Controls.Add(_classicView);
            _classicIgnoreNumbers = new CheckBox { Text = C("Не учитывать номера", "Ignore numbering"), AutoSize = true, Margin = new Padding(7, 5, 0, 0) };
            syncBar.Controls.Add(_classicIgnoreNumbers);
            try { _classicIgnoreNumbers.Checked = File.ReadAllText(Path.Combine(Configuration.DataDirectory, "ClassicCompareIgnoreNumbers.txt")).Trim() == "true"; } catch (IOException) { } catch (UnauthorizedAccessException) { }
            FormClosed += (s, e) => { try { File.WriteAllText(Path.Combine(Configuration.DataDirectory, "ClassicCompareIgnoreNumbers.txt"), _classicIgnoreNumbers.Checked ? "true" : "false"); } catch (IOException) { } catch (UnauthorizedAccessException) { } };
            _classicView.SelectedIndexChanged += (s, e) =>
            {
                if (_classicSettingView) return;
                _classicSettingView = true;
                try { checkBoxShowOnlyDifferences.Checked = _classicView.SelectedIndex > 0; checkBoxOnlyListDifferencesInText.Checked = _classicView.SelectedIndex == 2; }
                finally { _classicSettingView = false; }
                CompareSubtitles(0);
            };
            _classicIgnoreNumbers.CheckedChanged += (s, e) => CompareSubtitles(0);
            checkBoxShowOnlyDifferences.Visible = checkBoxOnlyListDifferencesInText.Visible = false;
            _classicLoadSaved = new Button { Text = C("Сохранённый файл → справа", "Load saved file → reference"), AutoSize = true, Top = buttonReloadSubtitle2.Top };
            _classicLoadSaved.Click += (s, e) => LoadClassicSavedReference(); Controls.Add(_classicLoadSaved);
            _classicOverview = new ClassicCompareOverview { Width = 12, Cursor = Cursors.Hand };
            _classicOverview.Jump = JumpClassicOverview; Controls.Add(_classicOverview);
            toolTip1.SetToolTip(_classicOverview, C("Обзор всего файла: красный — пропуски, зелёный — различия, оранжевый — номера, фиолетовый — правки. Нажмите для перехода.", "Whole-file overview: red — missing cues, green — differences, orange — numbering, violet — edits. Click to navigate."));
            InitializeClassicInlineEditor();
            subtitleListView1.DoubleClick += (s, e) => BeginClassicInlineEdit();
            subtitleListView2.DoubleClick += (s, e) => BeginClassicInlineEdit();
            AddClassicEditMenu(subtitleListView1); AddClassicEditMenu(subtitleListView2);
            RefreshClassicViewCounts();
        }

        private void AddClassicEditMenu(ListView list)
        {
            var menu = list.ContextMenuStrip;
            menu.Items.Add(new ToolStripSeparator());
            var edit = new ToolStripMenuItem(C("Редактировать текущую реплику (F2)", "Edit current cue (F2)")); edit.Click += (s, e) => BeginClassicInlineEdit();
            var text = new ToolStripMenuItem(C("Взять только текст справа", "Take reference text")); text.Click += (s, e) => TakeClassicReference("text");
            var time = new ToolStripMenuItem(C("Взять только время справа", "Take reference timing")); time.Click += (s, e) => TakeClassicReference("timing");
            var both = new ToolStripMenuItem(C("Взять текст и время справа", "Take reference text and timing")); both.Click += (s, e) => TakeClassicReference("both");
            var delete = new ToolStripMenuItem(C("Удалить текущую реплику", "Delete current cue")); delete.Click += (s, e) => DeleteClassicCurrent();
            menu.Items.AddRange(new ToolStripItem[] { edit, text, time, both, delete });
            menu.Opening += (s, e) => { edit.Enabled = _classicEdit.Enabled; text.Enabled = _classicTakeText.Enabled; time.Enabled = _classicTakeTimes.Enabled; both.Enabled = _classicTakeBoth.Enabled; delete.Enabled = _classicDelete.Enabled; };
        }

        private void RefreshClassicComplete()
        {
            if (_classicEdit == null) return;
            var pair = SelectedClassicPair();
            _classicEdit.Enabled = _classicEditable && !ClassicInlineEditing && pair?.Left >= 0;
            _classicLoadSaved.Enabled = _classicEditable && !ClassicInlineEditing && !string.IsNullOrEmpty(_classicSavedFile) && File.Exists(_classicSavedFile);
            if (ClassicInlineEditing)
            {
                _classicTakeBoth.Enabled = _classicTakeText.Enabled = _classicTakeTimes.Enabled = _classicDelete.Enabled = _classicUndo.Enabled = _classicApply.Enabled = false;
                _classicPickLeft.Enabled = _classicPickRight.Enabled = _classicRemoveSync.Enabled = _classicClearSync.Enabled = false;
            }
            buttonOpenSubtitle1.Enabled = buttonOpenSubtitle2.Enabled = !ClassicInlineEditing;
            buttonReloadSubtitle1.Enabled = !ClassicInlineEditing && File.Exists(labelSubtitle1.Text);
            buttonReloadSubtitle2.Enabled = !ClassicInlineEditing && File.Exists(labelSubtitle2.Text);
            if (_classicOverview != null)
            {
                _classicOverview.SelectedRow = pair == null ? -1 : _classicRows.IndexOf(pair);
                _classicOverview.Invalidate();
            }
        }

        private void LoadClassicSavedReference()
        {
            if (!_classicLoadSaved.Enabled) return;
            LoadAndCompare(2, _classicSavedFile); labelSubtitle2.Text = _classicSavedFile; RefreshClassicMerge();
        }

        private bool ClassicCueEdited(Paragraph cue)
        {
            if (!_classicEditable || cue == null) return false;
            _classicOriginalCues.TryGetValue(cue.Id, out var original);
            return original == null || original.Text != cue.Text || original.StartTime.TotalMilliseconds != cue.StartTime.TotalMilliseconds || original.EndTime.TotalMilliseconds != cue.EndTime.TotalMilliseconds;
        }

        private void CompareClassicRows(int selectedIndex)
        {
            if (_loadingConfig || _subtitle1 == null || _subtitle2 == null || _classicComparing || _classicSettingView) return;
            _classicComparing = true; _classicSyncSelection = true; timer1.Stop();
            try
            {
                _classicOriginalCues = _classicBaseline?.Paragraphs.ToDictionary(p => p.Id) ?? new Dictionary<string, Paragraph>();
                _classicRows.Clear(); _classicAllIndexes.Clear(); _classicSelection = null;
                foreach (var pair in AlignClassicComparison())
                {
                    var row = new ClassicComparePair { Left = pair.Left, Right = pair.Right };
                    _classicAllIndexes[row] = _classicRows.Count; _classicRows.Add(row);
                }
                var changed = new HashSet<int>(); var textChanged = new HashSet<int>();
                var marks = new List<Color>(); var edited = 0; var totalWords = 0; var changedWords = 0;
                for (var row = 0; row < _classicRows.Count; row++)
                {
                    var pair = _classicRows[row]; var left = _subtitle1.GetParagraphOrDefault(pair.Left); var right = _subtitle2.GetParagraphOrDefault(pair.Right);
                    Utilities.GetTotalAndChangedWords(NormalizeClassicCompareText(left?.Text), NormalizeClassicCompareText(right?.Text), ref totalWords, ref changedWords, checkBoxIgnoreLineBreaks.Checked, checkBoxIgnoreFormatting.Checked, ShouldBreakToLetter());
                    var missing = left == null || right == null;
                    var text = missing || IsTextDifferent(left, right);
                    var time = missing || !IsTimeEqual(left.StartTime, right.StartTime) || !IsTimeEqual(left.EndTime, right.EndTime);
                    var number = !missing && !_classicIgnoreNumbers.Checked && left.Number != right.Number;
                    if (text || time || number) changed.Add(row);
                    if (text) textChanged.Add(row);
                    var isEdited = ClassicCueEdited(left); if (isEdited) edited++;
                    marks.Add(isEdited ? ClassicEditedColor : missing ? ListViewRed : text || time ? ListViewGreen : number ? Color.Orange : Color.Transparent);
                }
                _classicAllCount = _classicRows.Count; _classicDifferenceCount = changed.Count; _classicTextCount = textChanged.Count;
                var visible = Enumerable.Range(0, _classicRows.Count).Where(i => !checkBoxShowOnlyDifferences.Checked || (checkBoxOnlyListDifferencesInText.Checked ? textChanged : changed).Contains(i)).ToList();
                var leftSub = new Subtitle(); var rightSub = new Subtitle();
                foreach (var row in visible)
                {
                    var pair = _classicRows[row];
                    leftSub.Paragraphs.Add(pair.Left < 0 ? new Paragraph() : new Paragraph(_subtitle1.Paragraphs[pair.Left], false));
                    rightSub.Paragraphs.Add(pair.Right < 0 ? new Paragraph() : new Paragraph(_subtitle2.Paragraphs[pair.Right], false));
                }
                subtitleListView1.Fill(leftSub); subtitleListView2.Fill(rightSub); _differences = new List<int>();
                for (var i = 0; i < visible.Count; i++)
                {
                    var row = visible[i]; var pair = _classicRows[row];
                    subtitleListView1.Items[i].Tag = subtitleListView2.Items[i].Tag = pair;
                    var left = _subtitle1.GetParagraphOrDefault(pair.Left); var right = _subtitle2.GetParagraphOrDefault(pair.Right);
                    if (changed.Contains(row)) _differences.Add(i);
                    if (left == null || right == null)
                    {
                        subtitleListView1.SetBackgroundColor(i, ListViewRed); subtitleListView2.SetBackgroundColor(i, ListViewRed);
                    }
                    else
                    {
                        if (IsTextDifferent(left, right)) { subtitleListView1.SetBackgroundColor(i, ListViewGreen, subtitleListView1.ColumnIndexText); subtitleListView2.SetBackgroundColor(i, ListViewGreen, subtitleListView2.ColumnIndexText); }
                        if (!IsTimeEqual(left.StartTime, right.StartTime)) { subtitleListView1.SetBackgroundColor(i, ListViewGreen, subtitleListView1.ColumnIndexStart); subtitleListView2.SetBackgroundColor(i, ListViewGreen, subtitleListView2.ColumnIndexStart); }
                        if (!IsTimeEqual(left.EndTime, right.EndTime)) { subtitleListView1.SetBackgroundColor(i, ListViewGreen, subtitleListView1.ColumnIndexEnd); subtitleListView2.SetBackgroundColor(i, ListViewGreen, subtitleListView2.ColumnIndexEnd); }
                        if (!_classicIgnoreNumbers.Checked && left.Number != right.Number) { subtitleListView1.SetBackgroundColor(i, Color.Orange, subtitleListView1.ColumnIndexNumber); subtitleListView2.SetBackgroundColor(i, Color.Orange, subtitleListView2.ColumnIndexNumber); }
                    }
                    if (ClassicCueEdited(left)) subtitleListView1.SetBackgroundColor(i, ClassicEditedColor);
                }
                _classicOverview.Marks = marks;
                RefreshClassicViewCounts();
                labelStatus.Text = C("Строк: ", "Rows: ") + _classicAllCount + C(" · Отличий: ", " · Differences: ") + changed.Count + C(" · Текст: ", " · Text: ") + textChanged.Count + C(" · Правок: ", " · Edited: ") + edited;
                if (totalWords > 0) labelStatus.Text += C(" · Изменено слов: ", " · Changed words: ") + (100.0 * changedWords / totalWords).ToString("0.#") + "%";
                toolTip1.SetToolTip(labelStatus, labelStatus.Text);
                labelStatus.AutoSize = false; labelStatus.Width = Math.Max(100, buttonExport.Left - labelStatus.Left - 10); labelStatus.AutoEllipsis = true;
                buttonExport.Enabled = true;
                MarkClassicSyncPoints();
            }
            finally { _classicComparing = false; _classicSyncSelection = false; }
            if (subtitleListView1.Items.Count > 0) subtitleListView1.SelectIndexAndEnsureVisible(Math.Max(0, Math.Min(selectedIndex, subtitleListView1.Items.Count - 1)));
            SelectClassicPair(subtitleListView1); ShowText(); UpdatePreviousAndNextButtons(); RefreshClassicMerge(); timer1.Start();
        }

        private Color ClassicEditedColor => Configuration.Settings.General.UseDarkTheme ? Color.DarkSlateBlue : Color.FromArgb(225, 205, 250);

        private void RefreshClassicViewCounts()
        {
            if (_classicView == null) return;
            _classicSettingView = true;
            try
            {
                _classicView.Items.Clear();
                _classicView.Items.Add(C("Все", "All") + " (" + _classicAllCount + ")");
                _classicView.Items.Add(C("Различия", "Differences") + " (" + _classicDifferenceCount + ")");
                _classicView.Items.Add(C("Различия текста", "Text differences") + " (" + _classicTextCount + ")");
                _classicView.SelectedIndex = !checkBoxShowOnlyDifferences.Checked ? 0 : checkBoxOnlyListDifferencesInText.Checked ? 2 : 1;
            }
            finally { _classicSettingView = false; }
        }

        private void LayoutClassicComplete()
        {
            if (_classicOverview == null) return;
            subtitleListView2.Width = Math.Max(100, Width / 2 - 36); richTextBox2.Width = subtitleListView2.Width;
            _classicLoadSaved.Left = buttonReloadSubtitle2.Right + 8;
            LayoutClassicInlineEditor();
            _classicOverview.SetBounds(subtitleListView2.Right + 3, subtitleListView2.Top, 12, subtitleListView2.Height);
            _classicOverview.Invalidate();
            checkBoxIgnoreLineBreaks.Left = buttonNextDifference.Right + 12;
            checkBoxIgnoreLineBreaks.Top = checkBoxIgnoreWhitespace.Top = checkBoxIgnoreFormatting.Top = buttonNextDifference.Top + 3;
            checkBoxIgnoreWhitespace.Left = checkBoxIgnoreLineBreaks.Right + 12;
            checkBoxIgnoreFormatting.Left = checkBoxIgnoreWhitespace.Right + 12;
            labelStatus.Top = buttonOK.Top + 5;
            labelStatus.Width = Math.Max(100, buttonExport.Left - labelStatus.Left - 10);
        }

        private void JumpClassicOverview(double fraction)
        {
            if (_classicRows.Count == 0 || subtitleListView1.Items.Count == 0) return;
            var at = Math.Max(0, Math.Min(_classicRows.Count - 1, (int)(fraction * _classicRows.Count)));
            var best = Enumerable.Range(0, subtitleListView1.Items.Count).OrderBy(i => Math.Abs(_classicAllIndexes[(ClassicComparePair)subtitleListView1.Items[i].Tag] - at)).First();
            subtitleListView1.SelectIndexAndEnsureVisible(best);
        }

        private sealed class ClassicCompareOverview : Control
        {
            public List<Color> Marks = new List<Color>(); public int SelectedRow = -1; public Action<double> Jump;
            public ClassicCompareOverview() { DoubleBuffered = true; }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(SystemColors.ControlDark);
                for (var i = 0; i < Marks.Count; i++)
                    if (Marks[i] != Color.Transparent) using (var brush = new SolidBrush(Marks[i])) e.Graphics.FillRectangle(brush, 1, i * Height / Math.Max(1, Marks.Count), Math.Max(1, Width - 2), Math.Max(2, Height / Math.Max(1, Marks.Count)));
                if (SelectedRow >= 0 && Marks.Count > 0) e.Graphics.DrawRectangle(Pens.DodgerBlue, 0, SelectedRow * Height / Marks.Count, Math.Max(1, Width - 1), Math.Max(2, Height / Marks.Count - 1));
            }
            protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) Jump?.Invoke((double)e.Y / Math.Max(1, Height)); base.OnMouseDown(e); }
        }
    }
}
