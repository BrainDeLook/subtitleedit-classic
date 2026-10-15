using System;
using System.Linq;
using System.Collections.Generic;
using Nikse.SubtitleEdit.Core.Common;
using System.Drawing;

namespace Nikse.SubtitleEdit.Logic.Ocr
{

public sealed class ExpandedOcrGroup
{
    public IReadOnlyList<ImageSplitterItem> Items { get; }
    public ImageSplitterItem FirstItem => Items[0];
    public Rectangle Bounds { get; }
    public NikseBitmap PreviewBitmap { get; }
    public int MinTop { get; }
    public int MinY { get; }
    public int Count => Items.Count;
    public int PreviewTopMargin => FirstItem.Top - MinTop;

    private ExpandedOcrGroup(
        List<ImageSplitterItem> items,
        Rectangle bounds,
        NikseBitmap previewBitmap,
        int minTop,
        int minY)
    {
        Items = items;
        Bounds = bounds;
        PreviewBitmap = previewBitmap;
        MinTop = minTop;
        MinY = minY;
    }

    public static ExpandedOcrGroup Create(NikseBitmap sourceBitmap, IReadOnlyList<ImageSplitterItem> letters, int startIndex, int count)
    {
        if (count <= 1 || startIndex < 0 || startIndex + count > letters.Count)
        {
            return null;
        }

        var items = new List<ImageSplitterItem>(count);
        var minTop = int.MaxValue;
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        for (var i = startIndex; i < startIndex + count; i++)
        {
            var letter = letters[i];
            if (letter.NikseBitmap == null)
            {
                return null;
            }

            items.Add(letter);
            minTop = Math.Min(minTop, letter.Top);
            minX = Math.Min(minX, letter.X);
            minY = Math.Min(minY, letter.Y);
            maxX = Math.Max(maxX, letter.X + letter.NikseBitmap.Width);
            maxY = Math.Max(maxY, letter.Y + letter.NikseBitmap.Height);
        }

        var bounds = Rectangle.FromLTRB(minX, minY, maxX, maxY);
        var previewBitmap = sourceBitmap.CopyRectangle(new Rectangle(minX, minY, maxX - minX, maxY - minY));
        return new ExpandedOcrGroup(items, bounds, previewBitmap, minTop, minY);
    }

    public NOcrChar CreateNOcrChar()
    {
        return new NOcrChar
        {
            Width = PreviewBitmap.Width,
            Height = PreviewBitmap.Height,
            MarginTop = PreviewTopMargin,
            ExpandCount = Count,
        };
    }

}
}
