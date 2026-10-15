// Adapted stable Subtitle Edit v5.3.0 nOCR regression cases; GDI replaces Skia only in bitmap fixtures.
using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.Ocr;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class FactAttribute:Attribute{}
class TheoryAttribute:Attribute{}
[AttributeUsage(AttributeTargets.Method,AllowMultiple=true)]class InlineDataAttribute:Attribute{public object[] Data;public InlineDataAttribute(params object[] data){Data=data;}}
static class Assert {
 public static void True(bool value,string message=""){if(!value)throw new Exception("Expected true "+message);}
 public static void False(bool value,string message="")=>True(!value,message);
 public static void Null(object value)=>True(value==null,"expected null");
 public static void NotNull(object value)=>True(value!=null,"expected non-null");
 public static void Empty(IEnumerable value)=>True(!value.Cast<object>().Any(),"expected empty");
 public static void Equal<T>(T expected,T actual){if(expected is IEnumerable a && actual is IEnumerable b && !(expected is string)){True(a.Cast<object>().SequenceEqual(b.Cast<object>()),"sequence equality");return;}True(object.Equals(expected,actual),"expected ["+expected+"] actual ["+actual+"]");}
 public static void Equal<T>(IEnumerable<T> expected,IEnumerable<T> actual)=>True(expected.SequenceEqual(actual),"sequence equality");
 public static void InRange<T>(T value,T min,T max)where T:IComparable<T> =>True(value.CompareTo(min)>=0 && value.CompareTo(max)<=0,"range");
 public static void Contains<T>(T expected,IEnumerable<T> actual)=>True(actual.Contains(expected),"contains item");
 public static void Contains(string text,string actual)=>True(actual.Contains(text),"contains "+text);
 public static void Contains<T>(IEnumerable<T> items,Func<T,bool> predicate)=>True(items.Any(predicate),"contains predicate");
 public static void All<T>(IEnumerable<T> items,Action<T> action){foreach(var i in items)action(i);}
 public static void Single<T>(IEnumerable<T> items,Func<T,bool> predicate)=>True(items.Count(predicate)==1,"single predicate");
 public static void Single<T>(IEnumerable<T> items)=>True(items.Count()==1,"single");
 public static void StartsWith(string prefix,string actual)=>True(actual.StartsWith(prefix),"prefix");
}
sealed class TestPaint:IDisposable{public Color Color{get;set;}public void Dispose(){}}
sealed class TestCanvas:IDisposable{Graphics g;public TestCanvas(Bitmap b){g=Graphics.FromImage(b);}public void Clear(Color c)=>g.Clear(c);public void DrawRect(float x,float y,float w,float h,TestPaint p){using(var brush=new SolidBrush(p.Color))g.FillRectangle(brush,x,y,w,h);}public void Dispose()=>g.Dispose();}
static class BitmapTestExtensions{public static void Erase(this Bitmap b,Color c){using(var g=Graphics.FromImage(b))g.Clear(c);}}
class UpstreamNOcrTests {
 static int Main(){var temp=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-temp");Directory.CreateDirectory(temp);Environment.SetEnvironmentVariable("TMP",temp);Environment.SetEnvironmentVariable("TEMP",temp);var failures=0;var passed=0;foreach(var type in Assembly.GetExecutingAssembly().GetTypes().Where(t=>t.Namespace=="LibUiLogicTests.Ocr")){foreach(var method in type.GetMethods().Where(m=>m.GetCustomAttributes(typeof(FactAttribute),true).Any() || m.GetCustomAttributes(typeof(TheoryAttribute),true).Any())){var rows=method.GetCustomAttributes(typeof(InlineDataAttribute),true).Cast<InlineDataAttribute>().Select(x=>x.Data).ToList();if(rows.Count==0)rows.Add(new object[0]);foreach(var row in rows){try{method.Invoke(Activator.CreateInstance(type),row);Console.WriteLine("PASS "+type.Name+"."+method.Name);passed++;}catch(Exception ex){Console.WriteLine("FAIL "+type.Name+"."+method.Name+": "+(ex.InnerException ?? ex));failures++;}}}}Console.WriteLine("RESULT "+passed+" passed "+failures+" failed");return failures==0?0:1;}
}
namespace LibUiLogicTests.Ocr
{

/// <summary>
/// Pins the best-match-within-pass semantics of <see cref="NOcrDb.GetMatchSingle"/>: when
/// several candidates fit a pass's error budget, the one with the fewest wrong pixels wins -
/// not the one that happens to come first in the list.
/// </summary>
public class NOcrDbBestMatchTests
{
    /// <summary>10x10 bitmap with an opaque 3px vertical bar at x = 4..6.</summary>
    private static NikseBitmap MakeBarBitmap()
    {
        var bmp = new Bitmap(10, 10);
        using (var canvas = new TestCanvas(bmp))
        {
            canvas.Clear(Color.Transparent);
            using var paint = new TestPaint { Color = Color.White };
            canvas.DrawRect(4, 0, 3, 10, paint);
        }

        return new NikseBitmap(bmp);
    }

    [Fact]
    public void GetMatchSingle_PrefersFewestErrors_OverListOrder()
    {
        var bitmap = MakeBarBitmap();

        // Both characters are 20x20 (so the strict size passes are skipped and matching lands
        // in the aspect-only pass with a 3-error budget) and match the bar's foreground.
        // The decoy also has a short background line that lands ON the bar when scaled
        // (2 wrong pixels) - still within the pass budget, so the old first-match-wins
        // algorithm returned it. The correct character matches with 0 errors.
        var decoy = new NOcrChar("#") { Width = 20, Height = 20, MarginTop = 0 };
        decoy.LinesForeground.Add(new NOcrPoint(new Point(10, 0), new Point(10, 19)));
        decoy.LinesBackground.Add(new NOcrPoint(new Point(8, 0), new Point(8, 1)));

        var correct = new NOcrChar("l") { Width = 20, Height = 20, MarginTop = 0 };
        correct.LinesForeground.Add(new NOcrPoint(new Point(10, 0), new Point(10, 19)));
        correct.LinesBackground.Add(new NOcrPoint(new Point(1, 0), new Point(1, 19)));

        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nocr"));
        db.Add(correct);
        db.Add(decoy); // Add inserts at the front, so the decoy is FIRST in the list

        // Sanity: the decoy really does fit the 3-error budget (the old algorithm would
        // have accepted it), and the correct char matches perfectly.
        Assert.True(NOcrDb.IsMatch(bitmap, decoy, 3));
        Assert.False(NOcrDb.IsMatch(bitmap, decoy, 1));
        Assert.True(NOcrDb.IsMatch(bitmap, correct, 0));

        var match = db.GetMatchSingle(bitmap, 0, false, 25);

        Assert.NotNull(match);
        Assert.Equal("l", match!.Text);
    }

    [Fact]
    public void GetMatchSingle_EqualErrors_KeepsNewestFirst()
    {
        var bitmap = MakeBarBitmap();

        // Two equally good candidates: the newest added (front of the list) must win, so a
        // user's just-added correction takes precedence over an equally-matching older entry.
        var older = new NOcrChar("I") { Width = 20, Height = 20, MarginTop = 0 };
        older.LinesForeground.Add(new NOcrPoint(new Point(10, 0), new Point(10, 19)));

        var newer = new NOcrChar("l") { Width = 20, Height = 20, MarginTop = 0 };
        newer.LinesForeground.Add(new NOcrPoint(new Point(10, 0), new Point(10, 19)));

        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nocr"));
        db.Add(older);
        db.Add(newer);

        var match = db.GetMatchSingle(bitmap, 0, false, 25);

        Assert.NotNull(match);
        Assert.Equal("l", match!.Text);
    }
}

}
namespace LibUiLogicTests.Ocr
{

/// <summary>
/// Regression tests for the scaled expanded match (the pass that lets a multi-part expanded
/// character match at a different size than it was trained at). Its error budget must scale
/// with the number of line points walked, not with the target group's area: an area-scaled
/// budget grows with the group being claimed, so a trained expanded letter pair (e.g. "fi")
/// could swallow two ordinary separately-split letters ("t"+"i", "l"+"o") at ~10% wrong
/// points and hand them back merged as one glyph image.
/// </summary>
public class NOcrDbExpandedMatchTests
{
    private static NikseBitmap MakeSolidBitmap(int width, int height)
    {
        var bmp = new Bitmap(width, height);
        using (var canvas = new TestCanvas(bmp))
        {
            canvas.Clear(Color.Transparent);
            using var paint = new TestPaint { Color = Color.White };
            canvas.DrawRect(0, 0, width, height, paint);
        }

        return new NikseBitmap(bmp);
    }

    private static NOcrChar MakeTwoBarPair()
    {
        // An expanded two-part character trained at 28x38: two vertical bars with a verified
        // background gap between them (think "ll" or a large quote pair).
        var pair = new NOcrChar("fi") { Width = 28, Height = 38, MarginTop = 0, ExpandCount = 2 };
        pair.LinesForeground.Add(new NOcrPoint(new Point(3, 1), new Point(3, 36)));
        pair.LinesForeground.Add(new NOcrPoint(new Point(22, 1), new Point(22, 36)));
        pair.LinesBackground.Add(new NOcrPoint(new Point(13, 0), new Point(13, 37)));
        return pair;
    }

    [Fact]
    public void ScaledExpandedMatch_DoesNotClaimGroupWithWrongMiddle()
    {
        // Target group at 32x40 (inside the ±25/±20 size gates and within 15 aspect points of
        // the trained pair): a narrow bar plus a wide solid blob. The blob covers the trained
        // pair's background gap, so a full third of the walked points are wrong - yet the old
        // area-scaled budget (32*40/16 = 80 errors) accepted it and merged the two separate
        // letters into one glyph.
        var parentBmp = new Bitmap(40, 45);
        using (var canvas = new TestCanvas(parentBmp))
        {
            canvas.Clear(Color.Transparent);
            using var paint = new TestPaint { Color = Color.White };
            canvas.DrawRect(2, 0, 6, 40, paint);   // "l"-like bar at x=2..7
            canvas.DrawRect(14, 0, 20, 40, paint); // "o"-like blob at x=14..33
        }

        var parent = new NikseBitmap(parentBmp);
        var bar = new ImageSplitterItem(2, 0, MakeSolidBitmap(6, 40)) { Top = 0 };
        var blob = new ImageSplitterItem(14, 0, MakeSolidBitmap(20, 40)) { Top = 0 };
        var letters = new List<ImageSplitterItem> { bar, blob };

        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nocr"));
        db.Add(MakeTwoBarPair());

        var match = db.GetMatchExpanded(parent, bar, 0, letters);

        Assert.Null(match);
    }

    [Fact]
    public void ScaledExpandedMatch_StillMatchesSameShapeAtOtherSize()
    {
        // The counterpart guard: the same trained pair against a true two-bar group at 30x40
        // (bars where the pair has bars, empty gap where it has background) must still match -
        // that cross-size case is what the scaled pass exists for.
        var parentBmp = new Bitmap(40, 45);
        using (var canvas = new TestCanvas(parentBmp))
        {
            canvas.Clear(Color.Transparent);
            using var paint = new TestPaint { Color = Color.White };
            canvas.DrawRect(2, 0, 6, 40, paint);  // bar 1 at x=2..7
            canvas.DrawRect(26, 0, 6, 40, paint); // bar 2 at x=26..31
        }

        var parent = new NikseBitmap(parentBmp);
        var bar1 = new ImageSplitterItem(2, 0, MakeSolidBitmap(6, 40)) { Top = 0 };
        var bar2 = new ImageSplitterItem(26, 0, MakeSolidBitmap(6, 40)) { Top = 0 };
        var letters = new List<ImageSplitterItem> { bar1, bar2 };

        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nocr"));
        db.Add(MakeTwoBarPair());

        var match = db.GetMatchExpanded(parent, bar1, 0, letters);

        Assert.NotNull(match);
        Assert.Equal("fi", match!.Text);
        Assert.Equal(2, match.ExpandCount);
    }

    [Fact]
    public void ScaledExpandedMatch_RejectsExtremeScaleRatio()
    {
        // A small two-part glyph (10x12, like a trained double quote) must never be "scaled"
        // onto a letter-pair group more than 2.5x its size, even if the walked lines happen to
        // land on text: the absolute ±25/±20 size gates alone would allow it.
        var quote = new NOcrChar("\"") { Width = 10, Height = 12, MarginTop = 0, ExpandCount = 2 };
        quote.LinesForeground.Add(new NOcrPoint(new Point(2, 1), new Point(2, 10)));
        quote.LinesForeground.Add(new NOcrPoint(new Point(7, 1), new Point(7, 10)));

        var parentBmp = new Bitmap(45, 40);
        using (var canvas = new TestCanvas(parentBmp))
        {
            canvas.Clear(Color.Transparent);
            using var paint = new TestPaint { Color = Color.White };
            canvas.DrawRect(2, 0, 6, 31, paint);
            canvas.DrawRect(20, 0, 8, 31, paint);
        }

        // Group bounding box: 26x31 - 2.6x the trained width, inside all absolute gates
        // (|26-10| < 25, |31-12| < 20, aspect 119 vs 120), and the quote's two scaled bars
        // both land fully on the parts, so only the scale-ratio gate can reject it.
        var parent = new NikseBitmap(parentBmp);
        var part1 = new ImageSplitterItem(2, 0, MakeSolidBitmap(6, 31)) { Top = 0 };
        var part2 = new ImageSplitterItem(20, 0, MakeSolidBitmap(8, 31)) { Top = 0 };
        var letters = new List<ImageSplitterItem> { part1, part2 };

        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nocr"));
        db.Add(quote);

        var match = db.GetMatchExpanded(parent, part1, 0, letters);

        Assert.Null(match);
    }
}

}
namespace LibUiLogicTests.Ocr
{

/// <summary>
/// Issue #14768: italic "I'll" came out of nOCR as I ' 'll - the apostrophe twice. The
/// apostrophe matched on its own, and then the trained expanded "'ll" matched again with the
/// scan starting at the first "l": in italics the apostrophe sits inside the x-range of that
/// "l", and the expanded match sampled the parent bitmap, so the apostrophe's real ink
/// satisfied the candidate's apostrophe lines. Foreground points of an expanded candidate must
/// only count ink that belongs to the splitter items the candidate would claim.
/// </summary>
public class NOcrDbExpandedNeighbourInkTests
{
    // Two slanted 6 px bars (top x offset 14 and 28, sliding 12 px left over 40 rows) with a
    // 10x10 apostrophe block at the top-left. The apostrophe ends at x=9, the first bar starts
    // at x=2 near its bottom, so their bounding boxes overlap but their pixels do not.
    private const int BarWidth = 6;
    private const int Height = 40;

    private static int BarLeft(int top, int y) => top - (int)Math.Round(y * 12 / 39.0);

    private static bool IsApostrophe(int x, int y) => x < 10 && y < 10;
    private static bool IsBar1(int x, int y) => x >= BarLeft(14, y) && x < BarLeft(14, y) + BarWidth;
    private static bool IsBar2(int x, int y) => x >= BarLeft(28, y) && x < BarLeft(28, y) + BarWidth;

    private static NikseBitmap Render(int width, int height, int offsetX, int offsetY, Func<int, int, bool> ink)
    {
        var bmp = new NikseBitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (ink(x + offsetX, y + offsetY))
                {
                    bmp.SetPixel(x, y, Color.White);
                }
            }
        }

        return bmp;
    }

    private static (NikseBitmap Parent, List<ImageSplitterItem> Letters) MakeLine()
    {
        var parent = Render(40, Height + 4, 0, 0, (x, y) => IsApostrophe(x, y) || IsBar1(x, y) || IsBar2(x, y));
        var apostrophe = new ImageSplitterItem(0, 0, Render(10, 10, 0, 0, IsApostrophe)) { Top = 0 };
        var bar1 = new ImageSplitterItem(2, 0, Render(18, Height, 2, 0, IsBar1)) { Top = 0 };
        var bar2 = new ImageSplitterItem(16, 0, Render(18, Height, 16, 0, IsBar2)) { Top = 0 };
        return (parent, new List<ImageSplitterItem> { apostrophe, bar1, bar2 });
    }

    /// <summary>
    /// "'ll" as trained from the whole 34x40 group: a line through the apostrophe, one through
    /// each bar at row 20 and a background line in the gap between the bars.
    /// </summary>
    private static NOcrChar MakeApostropheLl(int expandCount)
    {
        var c = new NOcrChar("'ll") { Width = 34, Height = Height, MarginTop = 0, ExpandCount = expandCount, Italic = true };
        c.LinesForeground.Add(new NOcrPoint(new Point(3, 1), new Point(3, 8)));
        c.LinesForeground.Add(new NOcrPoint(new Point(10, 19), new Point(10, 21)));
        c.LinesForeground.Add(new NOcrPoint(new Point(24, 19), new Point(24, 21)));
        c.LinesBackground.Add(new NOcrPoint(new Point(17, 19), new Point(17, 21)));
        return c;
    }

    private static NOcrDb MakeDb(params NOcrChar[] chars)
    {
        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nocr"));
        foreach (var c in chars)
        {
            db.Add(c);
        }

        return db;
    }

    [Fact]
    public void ExpandedMatch_DoesNotUseInkOfNeighbourOutsideGroup()
    {
        // A "'ll" trained as two parts (apostrophe+l fused, l). Starting the scan at the first
        // bar, its group is 32x40 - within the exact size gate of the 34 px trained glyph - and
        // the shifted apostrophe line lands on the real apostrophe in the parent bitmap.
        var (parent, letters) = MakeLine();
        var db = MakeDb(MakeApostropheLl(2));

        var match = db.GetMatchExpanded(parent, letters[1], 1, letters);

        Assert.Null(match);
    }

    [Fact]
    public void ExpandedMatch_StillMatchesWhenGroupOwnsAllInk()
    {
        // The counterpart guard: the same lines as a three-part glyph, scanned from the
        // apostrophe, claim exactly the three items and must still match.
        var (parent, letters) = MakeLine();
        var db = MakeDb(MakeApostropheLl(3));

        var match = db.GetMatchExpanded(parent, letters[0], 0, letters);

        Assert.NotNull(match);
        Assert.Equal("'ll", match.Text);
    }
}

}
namespace LibUiLogicTests.Ocr
{

/// <summary>
/// SE 4 had an "un-italic" retry in nOCR matching: a glyph that matched nothing upright was
/// de-slanted by the italic factor and matched once more, with a hit returned as italic. The
/// pass was lost in the SE 5 port, so italic subtitles produced unknown glyph after unknown
/// glyph (#13660). These tests pin the restored behavior.
/// </summary>
public class NOcrDbItalicRetryTests
{
    private const double Slant = 0.25;

    private static NikseBitmap MakeBar(bool italic)
    {
        // A 4x24 vertical bar; the italic variant leans right by Slant (top further right).
        const int h = 24;
        const int w = 4;
        var maxShift = italic ? (int)(h * Slant) : 0;
        var width = w + maxShift;
        var data = new byte[width * h * 4];
        for (var y = 0; y < h; y++)
        {
            var shift = italic ? (int)Math.Round((h - 1 - y) * Slant) : 0;
            for (var x = 0; x < w; x++)
            {
                var i = (x + shift + y * width) * 4;
                data[i] = data[i + 1] = data[i + 2] = data[i + 3] = 255;
            }
        }

        return new NikseBitmap(width, h, data);
    }

    private static NOcrDb MakeDbWithUprightBar()
    {
        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), $"nocr_italic_test_{Guid.NewGuid():N}.nocr"));
        var ch = new NOcrChar { Text = "l", Width = 4, Height = 24, MarginTop = 0 };
        // Foreground strokes down the bar, background strokes just outside its corners.
        ch.LinesForeground.Add(new NOcrPoint(new Point(1, 1), new Point(1, 22)));
        ch.LinesForeground.Add(new NOcrPoint(new Point(2, 1), new Point(2, 22)));
        db.Add(ch);
        return db;
    }

    [Fact]
    public void UprightBar_MatchesWithoutRetry()
    {
        var db = MakeDbWithUprightBar();
        var bar = MakeBar(italic: false);
        var item = new ImageSplitterItem(0, 0, bar);
        var letters = new List<ImageSplitterItem> { item };

        var match = db.GetMatch(bar, letters, item, 0, true, 6);

        Assert.NotNull(match);
        Assert.Equal("l", match.Text);
        Assert.False(match.Italic);
    }

    [Fact]
    public void ItalicBar_FailsUpright_ButMatchesViaUnItalicRetry()
    {
        var db = MakeDbWithUprightBar();
        var bar = MakeBar(italic: true);
        var item = new ImageSplitterItem(0, 0, bar);
        var letters = new List<ImageSplitterItem> { item };

        var upright = db.GetMatch(bar, letters, item, 0, true, 6);
        Assert.Null(upright); // slanted glyph matches nothing upright

        var retried = db.GetMatch(bar, letters, item, 0, true, 6, italicFactor: Slant);
        Assert.NotNull(retried);
        Assert.Equal("l", retried.Text);
        Assert.True(retried.Italic); // the retry marks the hit as italic
    }

    [Fact]
    public void ItalicRetry_DoesNotMutateTheDatabaseEntry()
    {
        var db = MakeDbWithUprightBar();
        var bar = MakeBar(italic: true);
        var item = new ImageSplitterItem(0, 0, bar);
        var letters = new List<ImageSplitterItem> { item };

        _ = db.GetMatch(bar, letters, item, 0, true, 6, italicFactor: Slant);

        Assert.False(db.OcrCharacters[0].Italic); // the stored character stays upright
    }
}

}
namespace LibUiLogicTests.Ocr
{

/// <summary>
/// Verifies that the match-result cache in <see cref="NOcrDb"/> is transparent: repeated calls
/// return the same results, and database mutations (Add/Remove) invalidate cached results.
/// </summary>
public class NOcrDbMatchCacheTests
{
    private static string TestFontName => "Arial";

    private static (NOcrDb Db, NikseBitmap Parent, List<ImageSplitterItem> Letters) SetUp(string trainChars, string renderText)
    {
        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nocr"));
        var trainer = new NOcrTrainer();
        trainer.Train(new NOcrTrainerSettings
        {
            FontNames = { TestFontName },
            FontSize = 50,
            CharactersToTrain = trainChars,
        }, db);

        using var bmp = NOcrTrainer.RenderCharacterImage(renderText, TestFontName, 50, false, false);
        var parent = new NikseBitmap(bmp!);
        parent.MakeTwoColor(200);
        parent.CropTop(0, Color.Transparent);
        var letters = ClassicNOcrBitmap.Split(parent, 10, false, false, 25, false);
        return (db, parent, letters);
    }

    [Fact]
    public void GetMatch_RepeatedCalls_ReturnSameResults()
    {
        var (db, parent, letters) = SetUp("abc", "abc abc abc");

        var first = new List<string?>();
        var second = new List<string?>();
        foreach (var round in new[] { first, second })
        {
            foreach (var item in letters.Where(l => l.NikseBitmap != null))
            {
                var match = db.GetMatch(parent, letters, item, item.Top, true, 25);
                round.Add(match?.Text);
            }
        }

        Assert.Equal(first, second);
        Assert.Contains("a", first);
        Assert.Contains("b", first);
        Assert.Contains("c", first);
    }

    [Fact]
    public void GetMatch_AddInvalidatesCachedMiss()
    {
        var (db, parent, letters) = SetUp("ab", "abz");

        var glyphs = letters.Where(l => l.NikseBitmap != null).ToList();
        var zItem = glyphs[glyphs.Count-1];

        // Cache the miss for 'z'.
        var missing = db.GetMatch(parent, letters, zItem, zItem.Top, true, 25);
        Assert.Null(missing);

        // Now train 'z' into the same db - the cached miss must be discarded.
        var trainer = new NOcrTrainer();
        trainer.Train(new NOcrTrainerSettings
        {
            FontNames = { TestFontName },
            FontSize = 50,
            CharactersToTrain = "z",
        }, db);

        var found = db.GetMatch(parent, letters, zItem, zItem.Top, true, 25);
        Assert.NotNull(found);
        Assert.Equal("z", found!.Text);
    }

    [Fact]
    public void GetMatch_RemoveInvalidatesCachedHit()
    {
        var (db, parent, letters) = SetUp("ab", "ab");

        var glyphs = letters.Where(l => l.NikseBitmap != null).ToList();
        var aItem = glyphs[0];

        var match = db.GetMatch(parent, letters, aItem, aItem.Top, true, 25);
        Assert.NotNull(match);

        db.Remove(match!);

        var afterRemove = db.GetMatch(parent, letters, aItem, aItem.Top, true, 25);
        Assert.True(afterRemove == null || !ReferenceEquals(afterRemove, match));
    }

    [Fact]
    public void GetMatch_DifferentParameters_DoNotShareCacheEntries()
    {
        var (db, parent, letters) = SetUp("a", "a");

        var item = letters.First(l => l.NikseBitmap != null);

        // Prime the cache with a strict budget, then query with a generous one; the two
        // parameter sets must not alias each other.
        var strict = db.GetMatch(parent, letters, item, item.Top, false, 0);
        var generous = db.GetMatch(parent, letters, item, item.Top, true, 25);

        Assert.NotNull(generous);
        Assert.True(strict == null || strict.Text == generous!.Text);
    }
}

}
namespace LibUiLogicTests.Ocr
{

/// <summary>
/// Pins the small-glyph protections in <see cref="NOcrDb"/>: solid tiny glyphs (dot, dash,
/// apostrophe) can't be pixel-discriminated, so the matcher must gate them by aspect ratio,
/// and expanded characters must match scaled to the target group's actual size.
/// </summary>
public class NOcrDbSensitiveTests
{
    private static NikseBitmap MakeSolidBitmap(int width, int height)
    {
        var bmp = new Bitmap(width, height);
        using (var canvas = new TestCanvas(bmp))
        {
            canvas.Clear(Color.Transparent);
            using var paint = new TestPaint { Color = Color.White };
            canvas.DrawRect(0, 0, width, height, paint);
        }

        return new NikseBitmap(bmp);
    }

    [Fact]
    public void SmallGlyph_AspectRatioGate_BlocksSolidBlobSteal()
    {
        // A solid 12x4 dash. A dot-like 6x6 DB entry with only foreground lines scores ZERO
        // pixel errors on it (all its lines land on the solid bar), and the old 8px
        // size-tolerance pass had no aspect gate at all - so "." claimed "-". The aspect-ratio
        // gate for small glyphs (area < 150) must reject it: 100% vs 33% aspect is a 3x ratio.
        var dash = MakeSolidBitmap(12, 4);

        var dot = new NOcrChar(".") { Width = 6, Height = 6, MarginTop = 20 };
        dot.LinesForeground.Add(new NOcrPoint(new Point(1, 1), new Point(4, 1)));
        dot.LinesForeground.Add(new NOcrPoint(new Point(1, 3), new Point(4, 3)));
        dot.LinesForeground.Add(new NOcrPoint(new Point(2, 1), new Point(2, 4)));

        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nocr"));
        db.Add(dot);

        // Sanity: pixel evidence alone can't tell them apart.
        Assert.True(NOcrDb.IsMatch(dash, dot, 0));

        var match = db.GetMatchSingle(dash, 20, true, 25, lastDitch: true);

        Assert.Null(match);
    }

    [Fact]
    public void SmallGlyph_SameAspect_StillMatches()
    {
        // Same setup, but the DB entry has a dash-like aspect - it must still match.
        var dash = MakeSolidBitmap(12, 4);

        var trainedDash = new NOcrChar("-") { Width = 10, Height = 4, MarginTop = 20 };
        trainedDash.LinesForeground.Add(new NOcrPoint(new Point(1, 1), new Point(8, 1)));
        trainedDash.LinesForeground.Add(new NOcrPoint(new Point(1, 2), new Point(8, 2)));

        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nocr"));
        db.Add(trainedDash);

        var match = db.GetMatchSingle(dash, 20, true, 25, lastDitch: true);

        Assert.NotNull(match);
        Assert.Equal("-", match!.Text);
    }

    [Fact]
    public void ExpandedMatch_ScalesToTargetGroupSize()
    {
        // A two-part expanded character (like a quote: two bars) trained at 12x10, recognized
        // against a group whose actual bounding box is 10x8. The old "scaled" pass walked the
        // lines at the character's own size, so any size difference failed and the single-part
        // fallback turned every cross-size " into '. The scaled pass must map lines onto the
        // group's real size.
        var parentBmp = new Bitmap(30, 20);
        using (var canvas = new TestCanvas(parentBmp))
        {
            canvas.Clear(Color.Transparent);
            using var paint = new TestPaint { Color = Color.White };
            canvas.DrawRect(4, 0, 2, 8, paint);  // bar 1 at x=4..5
            canvas.DrawRect(12, 0, 2, 8, paint); // bar 2 at x=12..13
        }

        var parent = new NikseBitmap(parentBmp);
        var bar1 = new ImageSplitterItem(4, 0, MakeSolidBitmap(2, 8)) { Top = 0 };
        var bar2 = new ImageSplitterItem(12, 0, MakeSolidBitmap(2, 8)) { Top = 0 };
        var letters = new List<ImageSplitterItem> { bar1, bar2 };

        var quote = new NOcrChar("\"") { Width = 12, Height = 10, MarginTop = 0, ExpandCount = 2 };
        quote.LinesForeground.Add(new NOcrPoint(new Point(1, 1), new Point(1, 8)));
        quote.LinesForeground.Add(new NOcrPoint(new Point(9, 1), new Point(9, 8)));
        quote.LinesBackground.Add(new NOcrPoint(new Point(5, 0), new Point(5, 9)));

        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nocr"));
        db.Add(quote);

        var match = db.GetMatchExpanded(parent, bar1, 0, letters);

        Assert.NotNull(match);
        Assert.Equal("\"", match!.Text);
        Assert.Equal(2, match.ExpandCount);
    }
}

}
namespace LibUiLogicTests.Ocr
{

/// <summary>
/// Matching scales a database entry to the glyph, so a tiny entry with a few lines (an
/// apostrophe) fitted the stem of a much bigger letter: "test" was read as "'est" on Linux CI.
/// </summary>
public class NOcrDbSmallGlyphScaleTests
{
    private static NikseBitmap Bar(int width, int height)
    {
        using var bitmap = new Bitmap(width, height);
        bitmap.Erase(Color.White);
        return new NikseBitmap(bitmap);
    }

    private static NOcrDb DbWithApostrophe()
    {
        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nocr"));
        var apostrophe = new NOcrChar("'") { Width = 3, Height = 9, MarginTop = 0 };
        apostrophe.LinesForeground.Add(new NOcrPoint(new Point(1, 0), new Point(1, 8)));
        db.Add(apostrophe);
        return db;
    }

    [Fact]
    public void SmallEntry_DoesNotMatchGlyphManyTimesItsSize()
    {
        var db = DbWithApostrophe();

        Assert.Null(db.GetMatchSingle(Bar(8, 22), 0, true, 25, lastDitch: true));
    }

    [Fact]
    public void SmallEntry_StillMatchesAtTwiceItsSize()
    {
        var db = DbWithApostrophe();

        Assert.Equal("'", db.GetMatchSingle(Bar(6, 18), 0, true, 25)?.Text);
    }
}

}
namespace LibUiLogicTests.Ocr
{

public class NOcrLineWalkPointsTests
{
    /// <summary>
    /// WalkPoints/ScaledWalkPoints are allocation-free replacements used by the nOCR matcher's
    /// inner loop - they must yield exactly the same points as the iterator-based
    /// GetPoints/ScaledGetPoints they replace, or match results change.
    /// </summary>
    [Fact]
    public void WalkPoints_YieldsSamePointsAsGetPoints()
    {
        foreach (var line in MakeLines())
        {
            var expected = line.GetPoints().ToList();

            var actual = new List<Point>();
            foreach (var p in line.WalkPoints())
            {
                actual.Add(p);
            }

            AssertSamePoints(expected, actual, line);
        }
    }

    [Fact]
    public void ScaledWalkPoints_YieldsSamePointsAsScaledGetPoints()
    {
        var ocrChar = new NOcrChar { Width = 24, Height = 40, MarginTop = 3 };
        var sizes = new[] { (24, 40), (11, 17), (48, 80), (7, 60), (60, 7) };

        foreach (var line in MakeLines())
        {
            foreach (var (width, height) in sizes)
            {
                var expected = line.ScaledGetPoints(ocrChar, width, height).ToList();

                var actual = new List<Point>();
                foreach (var p in line.ScaledWalkPoints(ocrChar, width, height))
                {
                    actual.Add(p);
                }

                AssertSamePoints(expected, actual, line);
            }
        }
    }

    private static void AssertSamePoints(List<Point> expected, List<Point> actual, NOcrPoint line)
    {
        Assert.True(expected.Count == actual.Count, $"Point count differs for line {line}: {expected.Count} vs {actual.Count}");
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(expected[i].X == actual[i].X && expected[i].Y == actual[i].Y,
                $"Point {i} differs for line {line}: ({expected[i].X},{expected[i].Y}) vs ({actual[i].X},{actual[i].Y})");
        }
    }

    private static List<NOcrPoint> MakeLines()
    {
        var lines = new List<NOcrPoint>
        {
            // Degenerate: single point (factor becomes NaN in both implementations).
            new(new Point(5, 5), new Point(5, 5)),
            // Pure horizontal / vertical, both directions.
            new(new Point(0, 0), new Point(10, 0)),
            new(new Point(10, 0), new Point(0, 0)),
            new(new Point(3, 2), new Point(3, 12)),
            new(new Point(3, 12), new Point(3, 2)),
            // Diagonals (45 degrees hits the |dx| == |dy| tie -> vertical walk).
            new(new Point(0, 0), new Point(9, 9)),
            new(new Point(9, 9), new Point(0, 0)),
            new(new Point(0, 9), new Point(9, 0)),
            // Shallow and steep slopes, all four directions.
            new(new Point(1, 2), new Point(20, 7)),
            new(new Point(20, 7), new Point(1, 2)),
            new(new Point(2, 1), new Point(7, 20)),
            new(new Point(7, 20), new Point(2, 1)),
            new(new Point(0, 30), new Point(25, 0)),
        };

        // Deterministic pseudo-random lines to cover odd slopes and rounding edges.
        var random = new Random(42);
        for (var i = 0; i < 500; i++)
        {
            lines.Add(new NOcrPoint(
                new Point(random.Next(-5, 60), random.Next(-5, 90)),
                new Point(random.Next(-5, 60), random.Next(-5, 90))));
        }

        return lines;
    }
}

}
namespace LibUiLogicTests.Ocr
{

/// <summary>
/// End-to-end verification of the nOCR engine: train a database from rendered glyphs
/// (<see cref="NOcrTrainer"/>), then run the same recognition pipeline the app uses
/// (two-color + crop + splitter + <see cref="NOcrDb.GetMatch"/>) over rendered sentences and
/// verify the text round-trips.
/// </summary>
public class NOcrTrainerTests
{
    private const float FontSize = 50f;
    private const int MaxWrongPixels = 25;
    // The app default (SeOcr.NOcrPixelsAreSpace / seconv PixelsAreSpaceDefault).
    private const int PixelsAreSpace = 12;

    private static string TestFontName => "Arial";

    private static NOcrDb Train(string characters, bool italic = false)
    {
        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nocr"));
        var trainer = new NOcrTrainer();
        var settings = new NOcrTrainerSettings
        {
            FontNames = { TestFontName },
            FontSize = FontSize,
            CharactersToTrain = characters,
            IncludeItalic = italic,
        };
        var learned = trainer.Train(settings, db);
        Assert.True(learned > 0, "training should learn at least one character");
        return db;
    }

    private static string Recognize(NOcrDb db, string text, bool italic = false)
    {
        using var bmp = NOcrTrainer.RenderCharacterImage(text, TestFontName, FontSize, false, italic);
        Assert.NotNull(bmp);
        var parent = new NikseBitmap(bmp!);
        parent.MakeTwoColor(200);
        parent.CropTop(0, Color.Transparent);
        var letters = ClassicNOcrBitmap.Split(parent, PixelsAreSpace, false, false, 25, false);

        var sb = new StringBuilder();
        var i = 0;
        while (i < letters.Count)
        {
            var item = letters[i];
            if (item.NikseBitmap == null)
            {
                sb.Append(item.SpecialCharacter);
            }
            else
            {
                var match = db.GetMatch(parent, letters, item, item.Top, true, MaxWrongPixels);
                if (match is { ExpandCount: > 0 })
                {
                    i += match.ExpandCount - 1;
                }

                sb.Append(match?.Text ?? "*");
            }

            i++;
        }

        return sb.ToString().Trim();
    }

    [Fact]
    public void TrainedDb_RoundTripsRenderedSentence()
    {
        var db = Train("HeloWrd123");

        var result = Recognize(db, "Hello World 123");

        Assert.Equal("Hello World 123", result);
    }

    [Fact]
    public void TrainedDb_RoundTripsAfterSaveAndReload()
    {
        var db = Train("Subtiles");
        try
        {
            db.Save();
            var reloaded = new NOcrDb(db.FileName);
            Assert.Equal(db.TotalCharacterCount, reloaded.TotalCharacterCount);

            var result = Recognize(reloaded, "Subtitle test");
            Assert.Equal("Subtitle test", result);
        }
        finally
        {
            if (File.Exists(db.FileName))
            {
                File.Delete(db.FileName);
            }
        }
    }

    [Fact]
    public void Trainer_SkipsAlreadyKnownCharacters()
    {
        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nocr"));
        var trainer = new NOcrTrainer();
        var settings = new NOcrTrainerSettings
        {
            FontNames = { TestFontName },
            FontSize = FontSize,
            CharactersToTrain = "ABC",
        };

        var firstRun = trainer.Train(settings, db);
        var countAfterFirst = db.TotalCharacterCount;
        var secondRun = trainer.Train(settings, db);

        Assert.True(firstRun > 0);
        Assert.Equal(countAfterFirst, db.TotalCharacterCount);
        Assert.Equal(0, secondRun);
    }

    [Fact]
    public void Trainer_UnknownGlyph_RecognizesAsAsterisk()
    {
        var db = Train("Helo");

        var result = Recognize(db, "Hello Zebra");

        Assert.StartsWith("Hello ", result);
        Assert.Contains("*", result);
    }

    [Theory]
    [InlineData("é", true)]
    [InlineData("ø", true)]
    [InlineData("\"", true)]
    [InlineData("%", true)]
    [InlineData("«", true)]
    [InlineData("»", true)]
    [InlineData("‹", true)]
    [InlineData("›", true)]
    [InlineData("“", true)]
    [InlineData("”", true)]
    [InlineData("„", true)]
    [InlineData("…", true)]
    [InlineData("‼", true)]
    [InlineData("ы", true)]
    [InlineData("Ы", true)]
    [InlineData("ь", false)]
    [InlineData("f", false)]
    [InlineData("m", false)]
    [InlineData("2", false)]
    [InlineData("(", false)]
    public void CanBeMultiPart_OnlyForGlyphsDrawnInPieces(string text, bool expected)
    {
        // A plain letter falling apart is a thin font losing hairlines at the threshold; stored
        // as an expanded "f" it later claimed "t." as one character.
        Assert.Equal(expected, (bool)typeof(NOcrTrainer).GetMethod("CanBeMultiPart",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{text}));
    }

    // « and » are left out: how far apart their two chevrons render depends on the
    // platform's default font (they fail on the Linux CI font), CanBeMultiPart covers them.
    [Theory]
    [InlineData("„")]
    [InlineData("“")]
    [InlineData("ы")]
    public void Trainer_LearnsMultiPartGlyphs(string text)
    {
        var db = new NOcrDb(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nocr"));
        var settings = new NOcrTrainerSettings
        {
            FontNames = { TestFontName },
            FontSize = FontSize,
            CharactersToTrain = text,
        };

        new NOcrTrainer().Train(settings, db);

        Assert.Contains(db.OcrCharactersExpanded, c => c.Text == text);
    }

    [Fact]
    public void AddDiscriminativeLines_SeparatesThinFeatureFromLookalike()
    {
        // "ø" vs "o": a ring with and without a one pixel slash through the hole. The random
        // generator puts no lines on a stroke that thin, so the entry matched both.
        var own = Ring(withSlash: true);
        var lookalike = Ring(withSlash: false);
        var nOcrChar = new NOcrChar("ø") { Width = own.Width, Height = own.Height };
        nOcrChar.LinesForeground.Add(new NOcrPoint(new Point(1, 2), new Point(1, 17)));
        Assert.True(NOcrDb.IsMatch(lookalike, nOcrChar, 0));

        typeof(NOcrTrainer).GetMethod("AddDiscriminativeLines",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{nOcrChar,own,lookalike});

        Assert.True(NOcrDb.IsMatch(own, nOcrChar, 0));
        Assert.False(NOcrDb.IsMatch(lookalike, nOcrChar, 3));
    }

    private static NikseBitmap Ring(bool withSlash)
    {
        using var bitmap = new Bitmap(20, 20);
        bitmap.Erase(Color.Transparent);
        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                var inHole = x >= 5 && x < 15 && y >= 5 && y < 15;
                var onSlash = withSlash && inHole && x == y;
                if (!inHole || onSlash)
                {
                    bitmap.SetPixel(x, y, Color.White);
                }
            }
        }

        return new NikseBitmap(bitmap);
    }

    [Fact]
    public void TrainedDb_RecognizesTextAtTwiceTheTrainedSize()
    {
        // Top margins, aspect gates and error budgets used to be absolute pixels at the trained
        // size, so "." and "i" (tall, thin, or small) never matched at another size.
        var db = Train("This.");

        var result = RecognizeAtSize(db, "This is his.", FontSize * 2);

        Assert.Equal("This is his.", result);
    }

    private static string RecognizeAtSize(NOcrDb db, string text, float fontSize)
    {
        using var bmp = NOcrTrainer.RenderCharacterImage(text, TestFontName, fontSize, false, false);
        Assert.NotNull(bmp);
        var parent = new NikseBitmap(bmp!);
        parent.MakeTwoColor(200);
        parent.CropTop(0, Color.Transparent);
        var letters = ClassicNOcrBitmap.Split(parent, PixelsAreSpace * 2, false, false, 25, false);
        var sb = new StringBuilder();
        foreach (var item in letters)
        {
            sb.Append(item.NikseBitmap == null
                ? item.SpecialCharacter
                : db.GetMatch(parent, letters, item, item.Top, true, MaxWrongPixels)?.Text ?? "*");
        }

        return sb.ToString().Trim();
    }
}

}