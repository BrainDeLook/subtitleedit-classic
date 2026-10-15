using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic.Ocr
{

/// <summary>
/// Trains an nOCR database by rendering characters with real fonts (white fill, black outline,
/// like typical image subtitles) and generating line segments for each glyph the database does
/// not already recognize. Port of SE4's "Train nOCR" (VobSubNOcrTrain).
/// </summary>
public sealed class NOcrTrainerSettings
{
    public List<string> FontNames { get; set; } = new();
    public float FontSize { get; set; } = 30;
    public bool IncludeBold { get; set; }
    public bool IncludeItalic { get; set; }
    public int NumberOfLineSegments { get; set; } = 60;

    /// <summary>All characters to train, as a plain string (whitespace is ignored).</summary>
    public string CharactersToTrain { get; set; } = NOcrTrainer.DefaultTrainingCharacters;

    /// <summary>Space separated multi-character combinations (2-3 chars, e.g. ligature-prone pairs like "fi ff rn").</summary>
    public string MergedLetterCombinations { get; set; } = string.Empty;

    public NOcrLineAlgorithm LineSegmentAlgorithm { get; set; } = NOcrLineAlgorithm.Random;
}

public sealed class NOcrTrainerProgress
{
    public int CharactersLearned { get; set; }
    public int CharactersSkipped { get; set; }
    public string CurrentFontName { get; set; } = string.Empty;
    public string CurrentText { get; set; } = string.Empty;
}

public sealed class NOcrTrainer
{
    public const string DefaultTrainingCharacters =
        "abcdefghijklmnopqrstuvwxyz" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
        "0123456789" +
        ".,!?\"'()[]:;-+/%&$#@*=" +
        "æøåäöüßéèêëáàâíìîóòôúùûñç" +
        "ÆØÅÄÖÜÉÈÊËÁÀÂÍÌÎÓÒÔÚÙÛÑÇ";

    // Matches the runtime OCR pipeline (OcrViewModel/seconv): two-color threshold and the
    // wrong-pixel budget used when checking whether a glyph is already recognized.
    private const int TwoColorThreshold = 200;
    private const int MaxWrongPixels = 25;
    private const int PixelsAreSpace = 10;
    private const int MinLineHeight = 25;
    private const float OutlineWidth = 2.0f;

    /// <summary>
    /// Trains <paramref name="db"/> in place. Call from a background thread; use
    /// <paramref name="abortRequested"/> to cancel between characters.
    /// </summary>
    /// <returns>The number of characters learned (added to the database).</returns>
    public int Train(NOcrTrainerSettings settings, NOcrDb db, Func<bool> abortRequested = null, Action<NOcrTrainerProgress> progress = null)
    {
        var learned = 0;
        var skipped = 0;

        var singleCharacters = new List<string>();
        foreach (var ch in settings.CharactersToTrain)
        {
            var s = ch.ToString();
            if (!char.IsWhiteSpace(ch) && !singleCharacters.Contains(s))
            {
                singleCharacters.Add(s);
            }
        }

        var mergedCombinations = settings.MergedLetterCombinations
            .Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim())
            .Where(s => s.Length > 1 && s.Length <= 3)
            .Distinct()
            .ToList();

        foreach (var fontName in settings.FontNames)
        {
            var lookalikes = new Dictionary<(bool Bold, bool Italic), Dictionary<string, NikseBitmap>>();
            foreach (var style in GetStyles(settings))
            {
                lookalikes[style] = RenderSinglePartGlyphs(singleCharacters, fontName, settings.FontSize, style.Bold, style.Italic);
            }

            foreach (var text in singleCharacters.Concat(mergedCombinations))
            {
                if (abortRequested?.Invoke() == true)
                {
                    return learned;
                }

                var isMerged = text.Length > 1;
                foreach (var (bold, italic) in GetStyles(settings))
                {
                    if (TrainCharacter(settings, db, text, fontName, bold, italic, isMerged, lookalikes[(bold, italic)]))
                    {
                        learned++;
                    }
                    else
                    {
                        skipped++;
                    }
                }

                progress?.Invoke(new NOcrTrainerProgress
                {
                    CharactersLearned = learned,
                    CharactersSkipped = skipped,
                    CurrentFontName = fontName,
                    CurrentText = text,
                });
            }
        }

        return learned;
    }

    private static IEnumerable<(bool Bold, bool Italic)> GetStyles(NOcrTrainerSettings settings)
    {
        yield return (false, false);

        if (settings.IncludeBold)
        {
            yield return (true, false);
        }

        if (settings.IncludeItalic)
        {
            yield return (false, true);
        }
    }

    /// <summary>
    /// Renders every character that comes out as one connected glyph, keyed by its text. Used as
    /// the set of lookalikes a newly trained character must not match.
    /// </summary>
    private static Dictionary<string, NikseBitmap> RenderSinglePartGlyphs(List<string> characters, string fontName, float fontSize, bool bold, bool italic)
    {
        var result = new Dictionary<string, NikseBitmap>();
        foreach (var text in characters)
        {
            using var bitmap = RenderCharacterImage("H   " + text, fontName, fontSize, bold, italic);
            if (bitmap == null)
            {
                continue;
            }

            var nikseBitmap = new NikseBitmap(bitmap);
            nikseBitmap.MakeTwoColor(TwoColorThreshold);
            nikseBitmap.CropTop(0, Color.Transparent);
            var list = ClassicNOcrBitmap.Split(nikseBitmap, PixelsAreSpace, false, false, MinLineHeight, false);
            if (list.Count == 3 && list[2].NikseBitmap != null)
            {
                result[text] = list[2].NikseBitmap!;
            }
        }

        return result;
    }

    private static bool TrainCharacter(NOcrTrainerSettings settings, NOcrDb db, string text, string fontName, bool bold, bool italic, bool isMerged, Dictionary<string, NikseBitmap> lookalikes)
    {
        // "H" establishes the line's cap height so the target glyph gets a realistic top
        // margin; the wide gap makes the splitter separate it from the target.
        using var bitmap = RenderCharacterImage("H   " + text, fontName, settings.FontSize, bold, italic);
        if (bitmap == null)
        {
            return false;
        }

        var nikseBitmap = new NikseBitmap(bitmap);
        nikseBitmap.MakeTwoColor(TwoColorThreshold);
        nikseBitmap.CropTop(0, Color.Transparent);
        var list = ClassicNOcrBitmap.Split(nikseBitmap, PixelsAreSpace, false, false, MinLineHeight, false);

        // Expected: [H][space][target...]
        if (list.Count == 3 && list[2].NikseBitmap != null)
        {
            var item = list[2];
            var match = db.GetMatchSingle(item.NikseBitmap!, item.Top, false, MaxWrongPixels);
            if (match != null && (match.Text == text || IsSameShapeInSansSerif(match.Text, text)))
            {
                return false; // already recognized
            }

            if (text is "I" or "|" && IsSolidBar(item.NikseBitmap!))
            {
                // A plain bar is also the "l" of most sans-serif fonts: an "I" entry would only
                // tie with those and win or lose by list order. Keep reading bars as "l"; the
                // fix engine settles I/l from the dictionary.
                return false;
            }

            var nOcrChar = new NOcrChar(text)
            {
                Width = item.NikseBitmap!.Width,
                Height = item.NikseBitmap.Height,
                MarginTop = item.Top,
                Italic = italic,
            };
            var segments = settings.NumberOfLineSegments + (isMerged ? 20 : 0);
            NOcrChar.GenerateLineSegments(segments, false, nOcrChar, item.NikseBitmap, settings.LineSegmentAlgorithm);
            foreach (var (otherText, otherBitmap) in lookalikes.Select(x=>(x.Key,x.Value)))
            {
                // Case pairs (o/O, s/S, ...) share the shape; size and position tell them apart.
                if (!otherText.Equals(text, StringComparison.OrdinalIgnoreCase))
                {
                    AddDiscriminativeLines(nOcrChar, item.NikseBitmap, otherBitmap);
                }
            }

            db.Add(nOcrChar);
            return true;
        }

        // Multi-part glyphs (e.g. ", %, ?) - train as an expanded character spanning all parts.
        // Merged letter combinations are only trained when they render as one connected glyph
        // (same as SE4); separate glyphs are already covered by the single characters.
        if (!isMerged && list.Count is 4 or 5 && CanBeMultiPart(text))
        {
            var group = ExpandedOcrGroup.Create(nikseBitmap, list, 2, list.Count - 2);
            if (group == null)
            {
                return false;
            }

            var existing = db.GetMatchExpanded(nikseBitmap, list[2], 2, list);
            if (existing != null && existing.Text == text)
            {
                return false;
            }

            var nOcrChar = group.CreateNOcrChar();
            nOcrChar.Text = text;
            nOcrChar.Italic = italic;
            var segments = settings.NumberOfLineSegments + (list.Count == 5 ? 10 : 5);
            NOcrChar.GenerateLineSegments(segments, false, nOcrChar, group.PreviewBitmap, settings.LineSegmentAlgorithm);
            db.Add(nOcrChar);
            return true;
        }

        return false;
    }

    /// <summary>
    /// "I", "l" and "|" are the same bar in sans-serif fonts. Training one when the database
    /// already reads the bar as another only adds an identical entry with a different label that
    /// then wins ties at random; the OCR fix engine settles I/l from the dictionary.
    /// </summary>
    private static bool IsSameShapeInSansSerif(string a, string b)
    {
        return a is "I" or "l" or "|" && b is "I" or "l" or "|";
    }

    /// <summary>
    /// A plain vertical bar: tall, thin and almost fully inked, like "I" and "l" in sans-serif
    /// fonts. Two bars differ by a column of anti-aliasing at most, and the matcher scales the
    /// rest away.
    /// </summary>
    private static bool IsSolidBar(NikseBitmap bitmap)
    {
        if (bitmap.Height < bitmap.Width * 3)
        {
            return false;
        }

        var ink = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetAlpha(x, y) > 150)
                {
                    ink++;
                }
            }
        }

        return ink * 10 >= bitmap.Width * bitmap.Height * 9;
    }

    /// <summary>
    /// The random line generator only keeps lines whose pixels and their neighbours are all ink,
    /// so strokes narrower than three pixels - the slash in "ø", an accent, the bar of "e" in a
    /// light font - get no lines at all, and the trained character then matches its lookalike
    /// ("o", "i", "c") with zero errors. When <paramref name="nOcrChar"/> would match
    /// <paramref name="lookalike"/>, add precise lines on the pixels where the two glyphs
    /// differ until the lookalike clearly fails.
    /// </summary>
    internal static void AddDiscriminativeLines(NOcrChar nOcrChar, NikseBitmap own, NikseBitmap lookalike)
    {
        var ownAspect = own.Height / (double)own.Width;
        var otherAspect = lookalike.Height / (double)lookalike.Width;
        if (Math.Max(ownAspect, otherAspect) / Math.Min(ownAspect, otherAspect) > 1.6)
        {
            return; // the matcher's aspect gate already keeps these apart
        }

        var (errors, points) = CountErrors(nOcrChar, lookalike);
        var wanted = Math.Max(8, points * 6 / 100);
        if (points == 0 || errors >= Math.Max(4, points * 3 / 100))
        {
            return; // already tells them apart
        }

        // Start lines only where the glyphs differ: ink here but not in the lookalike (a
        // foreground line) or the other way round (a background line). Random starts anywhere
        // almost never hit a one pixel slash.
        var inkOnlyHere = new List<Point>();
        var inkOnlyThere = new List<Point>();
        for (var y = 0; y < own.Height; y++)
        {
            for (var x = 0; x < own.Width; x++)
            {
                var p = new NOcrPoint(new Point(x, y), new Point(x, y)).GetScaledStart(nOcrChar, lookalike.Width, lookalike.Height);
                if ((uint)p.X >= (uint)lookalike.Width || (uint)p.Y >= (uint)lookalike.Height)
                {
                    continue;
                }

                var here = own.GetAlpha(x, y) > 150;
                var there = lookalike.GetAlpha(p.X, p.Y) > 150;
                if (here && !there)
                {
                    inkOnlyHere.Add(new Point(x, y));
                }
                else if (!here && there)
                {
                    inkOnlyThere.Add(new Point(x, y));
                }
            }
        }

        if (inkOnlyHere.Count == 0 && inkOnlyThere.Count == 0)
        {
            return; // identical at this size - nothing to tell them apart by
        }

        var r = new Random(Guid.NewGuid().GetHashCode());
        for (var attempt = 0; attempt < 3000 && errors < wanted; attempt++)
        {
            var foreground = inkOnlyThere.Count == 0 || (inkOnlyHere.Count > 0 && attempt % 2 == 0);
            var starts = foreground ? inkOnlyHere : inkOnlyThere;
            var start = starts[r.Next(starts.Count)];
            var end = new Point(
                Math.Max(0,Math.Min(nOcrChar.Width - 1,start.X + r.Next(-8, 9))),
                Math.Max(0,Math.Min(nOcrChar.Height - 1,start.Y + r.Next(-8, 9))));
            if (Math.Abs(start.X - end.X) < 2 && Math.Abs(start.Y - end.Y) < 2)
            {
                continue;
            }

            var line = new NOcrPoint(start, end);
            var fits = foreground
                ? NOcrChar.IsMatchPointForeGround(line, false, own, nOcrChar)
                : NOcrChar.IsMatchPointBackGround(line, false, own, nOcrChar);
            if (!fits)
            {
                continue;
            }

            var lineErrors = 0;
            foreach (var p in line.ScaledWalkPoints(nOcrChar, lookalike.Width, lookalike.Height))
            {
                if ((uint)p.X < (uint)lookalike.Width && (uint)p.Y < (uint)lookalike.Height &&
                    lookalike.GetAlpha(p.X, p.Y) > 150 != foreground)
                {
                    lineErrors++;
                }
            }

            if (lineErrors >= 2)
            {
                (foreground ? nOcrChar.LinesForeground : nOcrChar.LinesBackground).Add(line);
                errors += lineErrors;
            }
        }
    }

    private static (int Errors, int Points) CountErrors(NOcrChar nOcrChar, NikseBitmap bitmap)
    {
        var errors = 0;
        var points = 0;
        foreach (var line in nOcrChar.LinesForeground)
        {
            foreach (var p in line.ScaledWalkPoints(nOcrChar, bitmap.Width, bitmap.Height))
            {
                if ((uint)p.X < (uint)bitmap.Width && (uint)p.Y < (uint)bitmap.Height)
                {
                    points++;
                    if (bitmap.GetAlpha(p.X, p.Y) <= 150)
                    {
                        errors++;
                    }
                }
            }
        }

        foreach (var line in nOcrChar.LinesBackground)
        {
            foreach (var p in line.ScaledWalkPoints(nOcrChar, bitmap.Width, bitmap.Height))
            {
                if ((uint)p.X < (uint)bitmap.Width && (uint)p.Y < (uint)bitmap.Height)
                {
                    points++;
                    if (bitmap.GetAlpha(p.X, p.Y) > 150)
                    {
                        errors++;
                    }
                }
            }
        }

        return (errors, points);
    }

    /// <summary>
    /// Only characters that are drawn in separate pieces by design (a letter plus diacritic, or
    /// punctuation like " % ; :) may be trained as an expanded character. A plain letter that
    /// falls apart is a thin font losing hairlines at the two-color threshold; stored as an
    /// expanded "f" or "m" it later claims pairs of ordinary glyphs ("t." read as "f").
    /// </summary>
    internal static bool CanBeMultiPart(string text)
    {
        if (text.Length != 1)
        {
            return false;
        }

        var c = text[0];
        if (char.IsLetterOrDigit(c))
        {
            // Cyrillic ы/Ы are a soft sign plus a separate bar.
            return text.Normalize(System.Text.NormalizationForm.FormD).Length > 1 || c is 'Ø' or 'ø' or 'ы' or 'Ы';
        }

        return c is '"' or '%' or ';' or ':' or '?' or '!' or '=' or '÷' or '¡' or '¿' or '‰' or
            '«' or '»' or '‹' or '›' or '“' or '”' or '„' or '‟' or '″' or '‴' or '…' or '‼' or '⁇' or '⁈' or '⁉';
    }

    /// <summary>
    /// Renders text like a typical image subtitle: white fill with a black outline on a
    /// transparent background.
    /// </summary>
    public static Bitmap RenderCharacterImage(string text,string fontName,float fontSize,bool bold,bool italic)
    {
        using(var font=new Font(fontName,fontSize,(bold?FontStyle.Bold:FontStyle.Regular)|(italic?FontStyle.Italic:FontStyle.Regular),GraphicsUnit.Pixel))
        using(var measure=new Bitmap(1,1))using(var g=Graphics.FromImage(measure))
        using(var format=(StringFormat)StringFormat.GenericTypographic.Clone()) {
            format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            var width=(int)Math.Ceiling(g.MeasureString(text,font,10000,format).Width)+32;
            var height=(int)Math.Ceiling(font.GetHeight(g))+32;
            var bitmap=new Bitmap(width,height);using(var target=Graphics.FromImage(bitmap))using(var path=new GraphicsPath()){
                target.SmoothingMode=SmoothingMode.AntiAlias;target.Clear(Color.Transparent);float x=12;
                foreach(char c in text){path.AddString(c.ToString(),font.FontFamily,(int)font.Style,fontSize,new PointF((float)Math.Round(x),10),format);x+=g.MeasureString(c.ToString(),font,10000,format).Width;}
                using(var pen=new Pen(Color.Black,OutlineWidth){LineJoin=LineJoin.Round})target.DrawPath(pen,path);target.FillPath(Brushes.White,path);
            }return bitmap;
        }
    }
}
}
