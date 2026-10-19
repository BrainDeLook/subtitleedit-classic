using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Profile = Nikse.SubtitleEdit.Core.Settings.BeautifyTimeCodesSettings.BeautifyTimeCodesProfile;

namespace Nikse.SubtitleEdit.Controls
{
    public sealed partial class SubtitleListView
    {
        private List<double> _gridShotChanges;
        private Profile _gridShotProfile;
        private double _gridShotFrameRate;
        private bool _gridShotFrames;

        public void ShowShotColumn(SubtitleColumn column, string title)
        {
            if (column != SubtitleColumn.ShotIn && column != SubtitleColumn.ShotOut)
                throw new ArgumentOutOfRangeException(nameof(column));
            if (ColumnSuppressed(column)) return;
            var index = GetColumnIndex(column);
            if (index >= 0) { Columns[index].Text = title; return; }
            index = ColumnIndexText >= 0 ? ColumnIndexText : SubtitleColumns.Count;
            SubtitleColumns.Insert(index, column);
            Columns.Insert(index, new ColumnHeader { Text = title, Width = 80 });
            UpdateColumnIndexes();
            foreach (ListViewItem item in Items)
            {
                item.SubItems.Insert(index, new ListViewItem.ListViewSubItem(item, string.Empty));
                if (item.Tag is Paragraph paragraph) UpdateShotCells(item, paragraph);
            }
            AutoSizeAllColumns(null);
        }

        // Same cue-minus-cut and zone semantics as stable 5.3. Shot lists are sorted seconds.
        public void UpdateShotChangeOffsets(List<double> shots, Profile profile, double frameRate, bool frames)
        {
            _gridShotChanges = shots;
            _gridShotProfile = profile;
            _gridShotFrameRate = frameRate;
            _gridShotFrames = frames;
            if (ColumnIndexShotIn < 0 && ColumnIndexShotOut < 0) return;
            foreach (ListViewItem item in Items)
                if (item.Tag is Paragraph paragraph) UpdateShotCells(item, paragraph);
        }

        private void UpdateShotCells(ListViewItem item, Paragraph paragraph)
        {
            SetShotCell(item, ColumnIndexShotIn, paragraph.StartTime, true);
            SetShotCell(item, ColumnIndexShotOut, paragraph.EndTime, false);
        }

        private void SetShotCell(ListViewItem item, int index, TimeCode cue, bool isIn)
        {
            if (index < 0 || index >= item.SubItems.Count) return;
            var text = string.Empty;
            var warning = false;
            var p = _gridShotProfile;
            var fps = _gridShotFrameRate;
            if (!cue.IsMaxTime && p != null && _gridShotChanges != null && _gridShotChanges.Count > 0 &&
                fps > 0 && fps <= 1000 && !double.IsNaN(cue.TotalMilliseconds))
            {
                var leftGreen = isIn ? p.InCuesLeftGreenZone : p.OutCuesLeftGreenZone;
                var leftRed = isIn ? p.InCuesLeftRedZone : p.OutCuesLeftRedZone;
                var rightGreen = isIn ? p.InCuesRightGreenZone : p.OutCuesRightGreenZone;
                var rightRed = isIn ? p.InCuesRightRedZone : p.OutCuesRightRedZone;
                var gap = isIn ? p.InCuesGap : -p.OutCuesGap;
                var limit = Math.Max((int)Math.Ceiling(fps), Math.Max(Math.Max(leftGreen, leftRed), Math.Max(rightGreen, rightRed)));
                var cutMs = _gridShotChanges.ClosestTo(cue.TotalSeconds) * 1000.0;
                // Round cue and cut separately, as the timing beautifier does.
                var offsetFrames = SubtitleFormat.MillisecondsToFrames(cue.TotalMilliseconds, fps) - SubtitleFormat.MillisecondsToFrames(cutMs, fps);
                if (Math.Abs((long)offsetFrames) <= limit)
                {
                    text = (_gridShotFrames ? offsetFrames : Math.Round(cue.TotalMilliseconds - cutMs, MidpointRounding.AwayFromZero))
                        .ToString("+0;-0;0", CultureInfo.InvariantCulture);
                    warning = offsetFrames != gap &&
                        ((offsetFrames >= -leftRed && offsetFrames <= rightRed) || (offsetFrames > -leftGreen && offsetFrames < rightGreen));
                }
            }
            var cell = item.SubItems[index];
            if (cell.Text != text) cell.Text = text;
            var color = warning ? Configuration.Settings.Tools.ListViewSyntaxErrorColor : BackColor;
            if (cell.BackColor != color) cell.BackColor = color;
        }
    }
}
