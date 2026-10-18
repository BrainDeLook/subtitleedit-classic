using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Compare
    {
        private sealed class ClassicComparePair { public int Left; public int Right; }
        private readonly List<ClassicComparePair> _classicRows = new List<ClassicComparePair>();
        private ClassicComparePair _classicSelection;
        private NumericUpDown _classicTolerance;
        private Label _classicDelta;
        private Button _classicTakeBoth, _classicTakeText, _classicTakeTimes, _classicDelete, _classicUndo, _classicApply;
        private readonly Stack<ClassicCompareEdit> _classicHistory = new Stack<ClassicCompareEdit>();
        private Subtitle _classicBaseline;
        private bool _classicEditable;
        private bool _classicSyncSelection;
        public Func<Subtitle, Subtitle, bool> ApplyComparedSubtitle { get; set; }
        private string C(string ru, string en) => Configuration.Settings.General.Language.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? ru : en;

        private void InitializeClassicMerge()
        {
            MinimumSize = new Size(1160, 550);
            var bar = new FlowLayoutPanel { Left = 8, Top = subtitleListView1.Top, Width = ClientSize.Width - 16, Height = 31, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, WrapContents = false, AutoScroll = true };
            bar.Controls.Add(new Label { Text = C("Допуск времени, мс:", "Time tolerance, ms:"), AutoSize = true, Margin = new Padding(3, 7, 3, 0) });
            _classicTolerance = new NumericUpDown { Minimum = 0, Maximum = 10000, Width = 65 };
            try { if (decimal.TryParse(File.ReadAllText(Path.Combine(Configuration.DataDirectory, "ClassicCompareTolerance.txt")), NumberStyles.Integer, CultureInfo.InvariantCulture, out var saved)) _classicTolerance.Value = Math.Max(0, Math.Min(10000, saved)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            bar.Controls.Add(_classicTolerance);
            _classicTakeBoth = MergeButton(bar, C("← Текст и время", "← Text and timing"), () => TakeClassicReference("both"));
            _classicTakeText = MergeButton(bar, C("← Только текст", "← Text only"), () => TakeClassicReference("text"));
            _classicTakeTimes = MergeButton(bar, C("← Только время", "← Timing only"), () => TakeClassicReference("timing"));
            _classicDelete = MergeButton(bar, C("Удалить слева", "Delete left"), DeleteClassicCurrent);
            _classicUndo = MergeButton(bar, C("Отменить правку", "Undo edit"), UndoClassicMerge);
            _classicApply = MergeButton(bar, C("Применить", "Apply"), ApplyClassicMerge);
            _classicDelta = new Label { Left = 9, Top = bar.Bottom + 2, Height = 22, Width = ClientSize.Width - 18, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, AutoEllipsis = true };
            Controls.Add(bar); Controls.Add(_classicDelta);
            InitializeClassicSyncBar();
            InitializeClassicComplete(bar);
            buttonOK.Text = C("Закрыть", "Close");
            toolTip1.SetToolTip(buttonOK, C("Неприменённые правки будут отброшены", "Unapplied edits will be discarded"));
            subtitleListView1.Top = subtitleListView2.Top = _classicDelta.Bottom + 2;
            _classicTolerance.ValueChanged += (s, e) => { CompareSubtitles(0); RefreshClassicMerge(); };
            subtitleListView1.SelectedIndexChanged += (s, e) => SelectClassicPair(subtitleListView1);
            subtitleListView2.SelectedIndexChanged += (s, e) => SelectClassicPair(subtitleListView2);
            FormClosed += (s, e) => { try { File.WriteAllText(Path.Combine(Configuration.DataDirectory, "ClassicCompareTolerance.txt"), _classicTolerance.Value.ToString(CultureInfo.InvariantCulture)); } catch (IOException) { } catch (UnauthorizedAccessException) { } };
            Compare_Resize(null, null); RefreshClassicMerge();
        }

        private static Button MergeButton(FlowLayoutPanel bar, string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true, Height = 25, Margin = new Padding(3, 0, 3, 0) };
            button.Click += (s, e) => action(); bar.Controls.Add(button); return button;
        }

        private void BeginClassicMerge(Subtitle source)
        {
            _classicBaseline = new Subtitle(source, false); _subtitle1 = new Subtitle(source, false); _subtitle2 = new Subtitle();
            _classicEditable = true; _classicHistory.Clear(); ClearClassicSyncState(); RefreshClassicMerge();
        }

        private void DisableClassicMerge()
        {
            CancelClassicInlineEdit();
            _classicEditable = false; _classicHistory.Clear(); ClearClassicSyncState(); RefreshClassicMerge();
        }

        private void SelectClassicPair(ListView list)
        {
            if (_classicSyncSelection) return;
            _classicSelection = list.SelectedItems.Count > 0 ? list.SelectedItems[0].Tag as ClassicComparePair : null;
            if (_classicSelection != null)
            {
                var other = list == subtitleListView1 ? subtitleListView2 : subtitleListView1;
                var row = list.SelectedItems[0].Index;
                if (row < other.Items.Count && (other.SelectedItems.Count != 1 || other.SelectedItems[0].Index != row))
                {
                    _classicSyncSelection = true;
                    try { other.SelectIndexAndEnsureVisible(row); }
                    finally { _classicSyncSelection = false; }
                }
            }
            RefreshClassicMerge();
        }

        private ClassicComparePair SelectedClassicPair() => _classicRows.Contains(_classicSelection) ? _classicSelection : null;

        private void RefreshClassicMerge()
        {
            if (_classicDelta == null) return;
            var pair = SelectedClassicPair();
            var left = pair == null ? null : _subtitle1?.GetParagraphOrDefault(pair.Left);
            var right = pair == null ? null : _subtitle2?.GetParagraphOrDefault(pair.Right);
            _classicTakeBoth.Enabled = _classicEditable && right != null;
            _classicTakeText.Enabled = _classicTakeTimes.Enabled = _classicEditable && left != null && right != null;
            _classicDelete.Enabled = _classicEditable && left != null;
            _classicUndo.Enabled = _classicHistory.Count > 0;
            _classicApply.Enabled = _classicEditable && _classicHistory.Count > 0 && ApplyComparedSubtitle != null;
            RefreshClassicSyncBar();
            RefreshClassicComplete();
            _classicDelta.Text = !_classicEditable ? C("Оба файла только для чтения: слева открыт другой файл.", "Both files are read-only: a different file was opened on the left.") : left != null && right != null
                ? C("Разница справа относительно слева: начало ", "Reference minus current: start ") + Delta(right.StartTime.TotalMilliseconds - left.StartTime.TotalMilliseconds) + C(" · конец ", " · end ") + Delta(right.EndTime.TotalMilliseconds - left.EndTime.TotalMilliseconds)
                : right != null ? C("Реплика есть только справа. «Текст и время» вставит её слева.", "Reference-only cue. Text and timing inserts it on the left.") : C("Слева — рабочая копия. Применить сохраняет правки в редактор; закрытие отбрасывает неприменённые правки.", "Left is a working copy. Apply saves edits to the editor; closing discards unapplied edits.");
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (HandleClassicCompleteKey(ref msg, keyData)) return true;
            if (ClassicInlineEditing) return base.ProcessCmdKey(ref msg, keyData);
            if (keyData == Keys.Escape && HasClassicPendingSync) { CancelClassicSync(); return true; }
            if (keyData == Keys.F8) { ButtonNextDifferenceClick(null, EventArgs.Empty); return true; }
            if (keyData == (Keys.Shift | Keys.F8)) { ButtonPreviousDifferenceClick(null, EventArgs.Empty); return true; }
            if (keyData == (Keys.Control | Keys.Z)) { UndoClassicMerge(); return true; }
            if (keyData == (Keys.Alt | Keys.Left)) { TakeClassicReference("both"); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private static string Delta(double milliseconds) => milliseconds.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture) + " ms";

        private void TakeClassicReference(string part)
        {
            if (!_classicEditable) return;
            var pair = SelectedClassicPair(); if (pair == null) return;
            var reference = _subtitle2?.GetParagraphOrDefault(pair.Right); if (reference == null) return;
            var current = _subtitle1.GetParagraphOrDefault(pair.Left);
            if (current == null && part != "both") return;
            if (current != null && (part == "text" ? current.Text == reference.Text : part == "timing" ? current.StartTime.TotalMilliseconds == reference.StartTime.TotalMilliseconds && current.EndTime.TotalMilliseconds == reference.EndTime.TotalMilliseconds : current.Text == reference.Text && current.StartTime.TotalMilliseconds == reference.StartTime.TotalMilliseconds && current.EndTime.TotalMilliseconds == reference.EndTime.TotalMilliseconds)) return;
            RememberClassicCompareEdit();
            if (current == null)
            {
                var row = _classicRows.IndexOf(pair);
                var insertAt = _subtitle1.Paragraphs.Count;
                for (var i = row + 1; i < _classicRows.Count; i++)
                    if (_classicRows[i].Left >= 0) { insertAt = _classicRows[i].Left; break; }
                _subtitle1.Paragraphs.Insert(insertAt, new Paragraph(reference));
                _subtitle1.Renumber();
            }
            else
            {
                if (part != "timing") current.Text = reference.Text;
                if (part != "text") { current.StartTime.TotalMilliseconds = reference.StartTime.TotalMilliseconds; current.EndTime.TotalMilliseconds = reference.EndTime.TotalMilliseconds; }
            }
            RecompareClassicMerge();
        }

        private void DeleteClassicCurrent()
        {
            var pair = SelectedClassicPair(); if (!_classicEditable || pair == null || pair.Left < 0 || pair.Left >= _subtitle1.Paragraphs.Count) return;
            RememberClassicCompareEdit(); _subtitle1.Paragraphs.RemoveAt(pair.Left); _subtitle1.Renumber(); RecompareClassicMerge();
        }

        private void UndoClassicMerge()
        {
            if (_classicHistory.Count == 0) return;
            var state = _classicHistory.Pop(); _subtitle1 = state.Subtitle;
            _classicSyncPoints.Clear(); _classicSyncPoints.AddRange(state.SyncPoints); CancelClassicSync(); RecompareClassicMerge();
        }

        private void RecompareClassicMerge()
        {
            var row = subtitleListView1.SelectedItems.Count > 0 ? subtitleListView1.SelectedItems[0].Index : 0;
            if (_subtitle2 != null) CompareSubtitles(row);
            else subtitleListView1.Fill(_subtitle1);
            RefreshClassicMerge();
        }

        private void ApplyClassicMerge()
        {
            if (!_classicEditable || _classicHistory.Count == 0 || ApplyComparedSubtitle == null) return;
            if (!ApplyComparedSubtitle(new Subtitle(_classicBaseline, false), new Subtitle(_subtitle1, false)))
            {
                SeMsgBox.MessageBox.Show(C("Субтитры в редакторе изменились. Откройте сравнение заново, чтобы не затереть изменения.", "The editor subtitles have changed. Reopen Compare to avoid overwriting changes.")); return;
            }
            _classicBaseline = new Subtitle(_subtitle1, false); _classicHistory.Clear(); RecompareClassicMerge();
        }
    }
}
