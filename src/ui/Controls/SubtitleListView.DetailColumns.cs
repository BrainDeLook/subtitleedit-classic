using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Controls
{
    public sealed partial class SubtitleListView
    {
        private sealed class WidthCache
        {
            public string Text, Font;
            public int Size, Width;
            public bool Bold;
        }
        // Weak keys avoid retaining deleted cues or closed documents. No measurement when hidden.
        private readonly ConditionalWeakTable<Paragraph, WidthCache> _pixelWidths = new ConditionalWeakTable<Paragraph, WidthCache>();

        public void ShowDetailColumn(SubtitleColumn column, string title)
        {
            if (column != SubtitleColumn.Layer && column != SubtitleColumn.PixelWidth)
                throw new ArgumentOutOfRangeException(nameof(column));
            if (ColumnSuppressed(column)) return;
            var index = GetColumnIndex(column);
            if (index >= 0) { Columns[index].Text = title; return; }
            index = ColumnIndexText >= 0 ? ColumnIndexText : SubtitleColumns.Count;
            SubtitleColumns.Insert(index, column);
            Columns.Insert(index, new ColumnHeader { Text = title, Width = column == SubtitleColumn.Layer ? 65 : 105 });
            UpdateColumnIndexes();
            foreach (ListViewItem item in Items)
            {
                item.SubItems.Insert(index, new ListViewItem.ListViewSubItem(item, string.Empty));
                if (item.Tag is Paragraph paragraph) UpdateDetailCells(item, paragraph);
            }
            AutoSizeAllColumns(null);
        }

        public void RefreshDetailColumns()
        {
            if (ColumnIndexLayer < 0 && ColumnIndexPixelWidth < 0) return;
            foreach (ListViewItem item in Items)
                if (item.Tag is Paragraph paragraph) UpdateDetailCells(item, paragraph);
        }

        private void UpdateDetailCells(ListViewItem item, Paragraph paragraph, string text = null)
        {
            if (ColumnIndexLayer >= 0 && ColumnIndexLayer < item.SubItems.Count)
            {
                var layer = paragraph.Layer.ToString(CultureInfo.InvariantCulture);
                if (item.SubItems[ColumnIndexLayer].Text != layer) item.SubItems[ColumnIndexLayer].Text = layer;
            }
            if (ColumnIndexPixelWidth < 0 || ColumnIndexPixelWidth >= item.SubItems.Count) return;
            var general = Configuration.Settings.General;
            var cache = _pixelWidths.GetOrCreateValue(paragraph);
            text = text ?? paragraph.Text ?? string.Empty;
            if (cache.Text != text || cache.Font != general.MeasureFontName || cache.Size != general.MeasureFontSize || cache.Bold != general.MeasureFontBold)
            {
                var max = 0;
                foreach (var line in HtmlUtil.RemoveHtmlTags(text, true).SplitToLines())
                    max = Math.Max(max, TextWidth.CalcPixelWidth(line));
                cache.Text = text; cache.Font = general.MeasureFontName; cache.Size = general.MeasureFontSize;
                cache.Bold = general.MeasureFontBold; cache.Width = max;
            }
            var cell = item.SubItems[ColumnIndexPixelWidth];
            var value = cache.Width.ToString(CultureInfo.InvariantCulture);
            if (cell.Text != value) cell.Text = value;
            var color = Configuration.Settings.Tools.ListViewSyntaxColorWideLines && cache.Width > general.SubtitleLineMaximumPixelWidth
                ? Configuration.Settings.Tools.ListViewSyntaxErrorColor : BackColor;
            if (cell.BackColor != color) cell.BackColor = color;
        }
    }
}
