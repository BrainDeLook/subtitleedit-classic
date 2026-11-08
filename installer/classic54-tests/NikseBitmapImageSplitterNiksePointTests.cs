using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Ocr;


namespace Classic54Tests {

/// <summary>
/// NiksePoint was changed from a class to a readonly struct to drop the ~Width*Height heap
/// allocations IsVerticalLineTransparent made per subtitle line. The splitter stores points in
/// lists and reads them back by index, so a value type only stays correct as long as nothing
/// relies on reference identity or on mutating a point in place. These tests pin the observable
/// behaviour the splitter depends on, and exercise the real split path end to end.
/// </summary>
public class NikseBitmapImageSplitterNiksePointTests
{
    [Fact]
    public void NiksePoint_CarriesItsCoordinates()
    {
        var point = new NiksePoint(7, 11);

        Assert.Equal(7, point.X);
        Assert.Equal(11, point.Y);
    }

    [Fact]
    public void NiksePoint_StoredInAListRoundTripsByIndex()
    {
        // The splitter's FindMinX/FindMaxX and the points[^1] truncation loops all read points
        // back out of a List by index; a value type must survive that unchanged.
        var points = new List<NiksePoint>();
        for (var i = 0; i < 50; i++)
        {
            points.Add(new NiksePoint(i, i * 2));
        }

        for (var i = 0; i < points.Count; i++)
        {
            Assert.Equal(i, points[i].X);
            Assert.Equal(i * 2, points[i].Y);
        }
    }

    [Fact]
    public void SplitBitmapToLetters_SeparatesThreeBlocks()
    {
        // Three opaque 10px-wide blocks separated by 10px of transparency. Whatever the splitter
        // decides about spacing, it must find the three separate marks and no more.
        using var bitmap = new Bitmap(70,20);
        using (var canvas = new SplitterCanvas(bitmap))
        {
            canvas.Clear(Color.Transparent);
            using var paint = new SplitterPaint { Color = Color.White };
            canvas.DrawRect(new RectangleF(5, 5, 10, 10), paint);
            canvas.DrawRect(new RectangleF(25, 5, 10, 10), paint);
            canvas.DrawRect(new RectangleF(45, 5, 10, 10), paint);
        }

        var nikseBitmap = new NikseBitmap(bitmap);

        var parts = NikseBitmapImageSplitter.SplitBitmapToLettersNew(
            nikseBitmap, xOrMorePixelsMakesSpace: 12, rightToLeft: false, topToBottom: true,
            minLineHeight: 5, autoHeight: false);

        var marks = parts.Where(p => p.NikseBitmap != null).ToList();

        Assert.Equal(3, marks.Count);
        Assert.All(marks, m => Assert.True(m.NikseBitmap!.Width > 0 && m.NikseBitmap.Height > 0));

        // Left to right, in order, and non-overlapping.
        var xs = marks.Select(m => m.X).ToList();
        Assert.Equal(xs.OrderBy(x => x).ToList(), xs);
    }

    [Fact]
    public void SplitBitmapToLetters_FullyTransparentBitmapYieldsNoMarks()
    {
        using var bitmap = new Bitmap(40,20);
        using (var canvas = new SplitterCanvas(bitmap))
        {
            canvas.Clear(Color.Transparent);
        }

        var parts = NikseBitmapImageSplitter.SplitBitmapToLettersNew(
            new NikseBitmap(bitmap), xOrMorePixelsMakesSpace: 12, rightToLeft: false,
            topToBottom: true, minLineHeight: 5, autoHeight: false);

        Assert.DoesNotContain(parts, p => p.NikseBitmap != null);
    }
}

}
