using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Compare
    {
        private sealed class ClassicSyncPoint
        {
            public readonly string LeftId, RightId;
            public ClassicSyncPoint(string leftId, string rightId) { LeftId = leftId; RightId = rightId; }
        }

        private sealed class ClassicCompareEdit
        {
            public Subtitle Subtitle;
            public List<ClassicSyncPoint> SyncPoints;
        }

        private readonly List<ClassicSyncPoint> _classicSyncPoints = new List<ClassicSyncPoint>();
        private string _classicPendingLeft, _classicPendingRight, _classicSyncMessage;
        private bool HasClassicPendingSync => _classicPendingLeft != null || _classicPendingRight != null;
        private CheckBox _classicShiftSync;
        private Label _classicSyncStatus;
        private Button _classicPickLeft, _classicPickRight, _classicRemoveSync, _classicClearSync, _classicCancelSync;

        private void InitializeClassicSyncBar()
        {
            var bar = new FlowLayoutPanel { Left = 8, Top = _classicDelta.Top, Width = ClientSize.Width - 16, Height = 31,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, WrapContents = false, AutoScroll = true };
            _classicPickLeft = MergeButton(bar, C("Точка слева", "Use current line"), () => PickClassicSyncHalf(true));
            _classicPickRight = MergeButton(bar, C("Точка справа", "Use reference line"), () => PickClassicSyncHalf(false));
            _classicShiftSync = new CheckBox { Text = C("Сдвигать время слева", "Shift current timing"), Checked = true, AutoSize = true, Margin = new Padding(7, 5, 7, 0) };
            bar.Controls.Add(_classicShiftSync);
            _classicShiftSync.CheckedChanged += (s, e) => RefreshClassicSyncBar();
            _classicRemoveSync = MergeButton(bar, C("Убрать точку", "Remove point"), RemoveClassicSyncPoint);
            _classicClearSync = MergeButton(bar, C("Очистить точки", "Clear points"), ClearClassicSyncPoints);
            _classicCancelSync = MergeButton(bar, C("Отменить выбор", "Cancel picking"), CancelClassicSync);
            _classicSyncStatus = new Label { Left = 9, Top = bar.Bottom + 2, Height = 22, Width = ClientSize.Width - 18,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, AutoEllipsis = true };
            Controls.Add(bar); Controls.Add(_classicSyncStatus);
            _classicDelta.Top = _classicSyncStatus.Bottom + 2;
            MinimumSize = new Size(1160, 610);
            AddClassicSyncMenu(subtitleListView1, true);
            AddClassicSyncMenu(subtitleListView2, false);
        }

        private void AddClassicSyncMenu(ListView list, bool left)
        {
            var menu = list.ContextMenuStrip;
            if (menu == null) { menu = new ContextMenuStrip(components); list.ContextMenuStrip = menu; }
            if (menu.Items.Count > 0) menu.Items.Add(new ToolStripSeparator());
            var pick = new ToolStripMenuItem(left ? C("Точка синхронизации: эта реплика слева", "Sync point: use this current line") : C("Точка синхронизации: эта реплика справа", "Sync point: use this reference line"));
            pick.Click += (s, e) => PickClassicSyncHalf(left);
            var remove = new ToolStripMenuItem(C("Убрать точку синхронизации", "Remove sync point")); remove.Click += (s, e) => RemoveClassicSyncPoint();
            menu.Items.Add(pick); menu.Items.Add(remove);
            menu.Opening += (s, e) =>
            {
                var pair = SelectedClassicPair();
                pick.Enabled = pair != null && (left ? pair.Left : pair.Right) >= 0;
                pick.Text = left && _classicPendingRight != null ? C("Связать с выбранной репликой справа", "Sync with the picked reference line")
                    : !left && _classicPendingLeft != null ? C("Связать с выбранной репликой слева", "Sync with the picked current line")
                    : left ? C("Точка синхронизации: эта реплика слева", "Sync point: use this current line") : C("Точка синхронизации: эта реплика справа", "Sync point: use this reference line");
                remove.Enabled = IsClassicSyncPair(pair);
            };
            list.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                var hit = list.HitTest(e.Location);
                if (hit.Item != null) { list.SelectedItems.Clear(); hit.Item.Selected = true; SelectClassicPair(list); }
            };
        }

        private IEnumerable<ClassicCompareAligner.Pair> AlignClassicComparison()
        {
            var left = _subtitle1.Paragraphs.Select(p => new ClassicCompareAligner.Line(NormalizeClassicCompareText(p.Text), p.StartTime.TimeSpan, p.EndTime.TimeSpan)).ToList();
            var right = _subtitle2.Paragraphs.Select(p => new ClassicCompareAligner.Line(NormalizeClassicCompareText(p.Text), p.StartTime.TimeSpan, p.EndTime.TimeSpan)).ToList();
            var frames = Configuration.Settings.General.UseTimeFormatHHMMSSFF;
            var tolerance = (double)_classicTolerance.Value;
            var displayCache = new Dictionary<long, string>();
            Func<TimeSpan, string> display = time =>
            {
                if (!displayCache.TryGetValue(time.Ticks, out var value)) { value = new TimeCode(time).ToDisplayString(); displayCache[time.Ticks] = value; }
                return value;
            };
            return ClassicCompareAligner.Align(left, right, (a, b) =>
            {
                var delta = Math.Abs((a - b).TotalMilliseconds);
                return delta < 0.1 || delta <= tolerance + 0.000001 || frames && display(a) == display(b);
            }, ValidClassicSyncIndexes());
        }

        private List<(int Left, int Right)> ValidClassicSyncIndexes()
        {
            var result = new List<(int Left, int Right)>();
            if (_subtitle1 == null || _subtitle2 == null) return result;
            foreach (var point in _classicSyncPoints)
            {
                var left = _subtitle1.Paragraphs.FindIndex(p => p.Id == point.LeftId);
                var right = _subtitle2.Paragraphs.FindIndex(p => p.Id == point.RightId);
                if (left >= 0 && right >= 0) result.Add((left, right));
            }
            return result;
        }

        private bool IsClassicSyncPair(ClassicComparePair pair) => pair != null && ValidClassicSyncIndexes().Any(p => p.Left == pair.Left && p.Right == pair.Right);

        private void RefreshClassicSyncBar()
        {
            if (_classicSyncStatus == null) return;
            var pair = SelectedClassicPair();
            _classicPickLeft.Enabled = pair != null && pair.Left >= 0;
            _classicPickRight.Enabled = pair != null && pair.Right >= 0;
            _classicShiftSync.Enabled = _classicEditable;
            _classicRemoveSync.Enabled = IsClassicSyncPair(pair);
            var count = ValidClassicSyncIndexes().Count;
            _classicClearSync.Enabled = count > 0;
            _classicCancelSync.Enabled = HasClassicPendingSync;
            var pending = _classicPendingLeft != null ? _subtitle1?.Paragraphs.Find(p => p.Id == _classicPendingLeft) : _subtitle2?.Paragraphs.Find(p => p.Id == _classicPendingRight);
            _classicSyncStatus.Text = pending != null
                ? (_classicPendingLeft != null ? C("Выбрана слева #", "Picked current #") : C("Выбрана справа #", "Picked reference #")) + pending.Number + C(". Выберите соответствующую реплику с другой стороны и нажмите кнопку точки там.", ". Select the matching line on the other side and use its point button.")
                : !string.IsNullOrEmpty(_classicSyncMessage) ? _classicSyncMessage
                : C("Точек: ", "Points: ") + count + (_classicEditable && _classicShiftSync.Checked
                    ? C(" · Синхронизация сдвигает начало, конец и следующие реплики до следующей точки.", " · Sync shifts the cue and following cues up to the next point.")
                    : C(" · Только сопоставление, время не изменяется.", " · Pairing only; timing is unchanged."));
            toolTip1.SetToolTip(_classicSyncStatus, _classicSyncStatus.Text);
        }

        private void PickClassicSyncHalf(bool left)
        {
            var pair = SelectedClassicPair(); if (pair == null) return;
            var paragraph = left ? _subtitle1.GetParagraphOrDefault(pair.Left) : _subtitle2?.GetParagraphOrDefault(pair.Right);
            if (paragraph == null) return;
            if (left && _classicPendingRight != null) { AddClassicSyncPoint(paragraph.Id, _classicPendingRight); return; }
            if (!left && _classicPendingLeft != null) { AddClassicSyncPoint(_classicPendingLeft, paragraph.Id); return; }
            _classicPendingLeft = left ? paragraph.Id : null; _classicPendingRight = left ? null : paragraph.Id;
            _classicSyncMessage = null; RefreshClassicSyncBar();
        }

        private bool AddClassicSyncPoint(string leftId, string rightId)
        {
            var leftIndex = _subtitle1.Paragraphs.FindIndex(p => p.Id == leftId);
            var rightIndex = _subtitle2.Paragraphs.FindIndex(p => p.Id == rightId);
            if (leftIndex < 0 || rightIndex < 0) { CancelClassicSync(); return false; }
            var points = ValidClassicSyncIndexes();
            var kept = points.Where(p => (long)(p.Left - leftIndex) * (p.Right - rightIndex) > 0).ToList();
            var endIndex = kept.Where(p => p.Left > leftIndex).Select(p => p.Left).DefaultIfEmpty(_subtitle1.Paragraphs.Count).Min();
            var offset = _classicEditable && _classicShiftSync.Checked ? _subtitle2.Paragraphs[rightIndex].StartTime.TotalMilliseconds - _subtitle1.Paragraphs[leftIndex].StartTime.TotalMilliseconds : 0;
            if (offset != 0 && Enumerable.Range(leftIndex, endIndex - leftIndex).Any(i => _subtitle1.Paragraphs[i].StartTime.TotalMilliseconds + offset < 0 || _subtitle1.Paragraphs[i].EndTime.TotalMilliseconds + offset > TimeCode.MaxTimeTotalMilliseconds))
            {
                CancelClassicSync(); _classicSyncMessage = C("Сдвиг выходит за допустимые границы времени. Отключите сдвиг для сопоставления без изменения времени.", "The shift exceeds valid time bounds. Disable timing shift to pair without changing times."); RefreshClassicSyncBar(); return false;
            }
            RememberClassicCompareEdit();
            _classicSyncPoints.RemoveAll(p => { var l = _subtitle1.Paragraphs.FindIndex(x => x.Id == p.LeftId); var r = _subtitle2.Paragraphs.FindIndex(x => x.Id == p.RightId); return l < 0 || r < 0 || (long)(l - leftIndex) * (r - rightIndex) <= 0; });
            _classicSyncPoints.Add(new ClassicSyncPoint(leftId, rightId));
            for (var i = leftIndex; i < endIndex; i++)
            {
                _subtitle1.Paragraphs[i].StartTime.TotalMilliseconds += offset;
                _subtitle1.Paragraphs[i].EndTime.TotalMilliseconds += offset;
            }
            CancelClassicSync();
            _classicSyncMessage = C("Закреплена пара #", "Pinned pair #") + _subtitle1.Paragraphs[leftIndex].Number + " ↔ #" + _subtitle2.Paragraphs[rightIndex].Number + C(" · Сдвиг: ", " · Shift: ") + Delta(offset) + C(" · Реплик: ", " · Cues: ") + (offset == 0 ? 0 : endIndex - leftIndex);
            RecompareClassicMerge();
            for (var row = 0; row < subtitleListView1.Items.Count; row++)
            {
                var pair = subtitleListView1.Items[row].Tag as ClassicComparePair;
                if (pair != null && _subtitle1.GetParagraphOrDefault(pair.Left)?.Id == leftId) { subtitleListView1.SelectIndexAndEnsureVisible(row); break; }
            }
            return true;
        }

        private void RememberClassicCompareEdit() => _classicHistory.Push(new ClassicCompareEdit { Subtitle = new Subtitle(_subtitle1, false), SyncPoints = new List<ClassicSyncPoint>(_classicSyncPoints) });

        private void CancelClassicSync()
        {
            _classicPendingLeft = _classicPendingRight = _classicSyncMessage = null; RefreshClassicSyncBar();
        }

        private void ClearClassicSyncState()
        {
            _classicSyncPoints.Clear(); CancelClassicSync();
        }

        private void ResetClassicReferenceSync()
        {
            ClearClassicSyncState();
            foreach (var state in _classicHistory) state.SyncPoints.Clear();
        }

        private void RemoveClassicSyncPoint()
        {
            var pair = SelectedClassicPair(); if (!IsClassicSyncPair(pair)) return;
            RememberClassicCompareEdit();
            var leftId = _subtitle1.Paragraphs[pair.Left].Id; var rightId = _subtitle2.Paragraphs[pair.Right].Id;
            _classicSyncPoints.RemoveAll(p => p.LeftId == leftId && p.RightId == rightId);
            CancelClassicSync(); RecompareClassicMerge();
        }

        private void ClearClassicSyncPoints()
        {
            if (_classicSyncPoints.Count == 0) return;
            RememberClassicCompareEdit(); ClearClassicSyncState(); RecompareClassicMerge();
        }

        private void MarkClassicSyncPoints()
        {
            var points = new HashSet<(int Left, int Right)>(ValidClassicSyncIndexes());
            var markerColor = Configuration.Settings.General.UseDarkTheme ? Color.DarkSlateBlue : Color.LightSteelBlue;
            for (var row = 0; row < subtitleListView1.Items.Count; row++)
            {
                var pair = subtitleListView1.Items[row].Tag as ClassicComparePair;
                if (pair == null || !points.Contains((pair.Left, pair.Right))) continue;
                subtitleListView1.Items[row].SubItems[0].Text += " ↔";
                subtitleListView2.Items[row].SubItems[0].Text += " ↔";
                subtitleListView1.SetBackgroundColor(row, markerColor, subtitleListView1.ColumnIndexNumber);
                subtitleListView2.SetBackgroundColor(row, markerColor, subtitleListView2.ColumnIndexNumber);
            }
        }
    }
}
