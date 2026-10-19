using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Nikse.SubtitleEdit.Logic {

/// <summary>
/// Headless helpers for the [Fonts] attachment section of ASSA files: parsing embedded
/// fonts, embedding font files, and finding/embedding the fonts a subtitle actually uses.
/// Shared by the font collector, the ASSA styles dialog and batch convert.
/// </summary>
public static class AssaFontEmbedder
{
    public static string ReplaceFont(string footer,string fileName,byte[] bytes)
    {
        var output=new List<string>();bool inFonts=false,skip=false;
        foreach(var line in (footer??string.Empty).SplitToLines())
        {
            var text=line.Trim();
            if(text.Equals("[Fonts]",StringComparison.OrdinalIgnoreCase)){inFonts=true;skip=false;output.Add(line);continue;}
            if(new[]{"[Graphics]","[Events]","[V4 Styles]","[V4+ Styles]","[Script Info]","[Aegisub Extradata]"}.Any(s=>s.Equals(text,StringComparison.OrdinalIgnoreCase))){inFonts=false;skip=false;}
            if(inFonts && (text.StartsWith("fontname:",StringComparison.OrdinalIgnoreCase)||text.StartsWith("filename:",StringComparison.OrdinalIgnoreCase)))skip=Path.GetFileName(text.Substring(9).Trim()).Equals(Path.GetFileName(fileName),StringComparison.OrdinalIgnoreCase);
            if(!skip)output.Add(line);
        }
        return AddFontToFooter(string.Join(Environment.NewLine,output),fileName,bytes);
    }
    /// <summary>Matches inline <c>\fn</c> font-name override tags.</summary>
    public static readonly Regex FontNameTagRegex = new(@"\\fn(?<name>[^\\}]+)", RegexOptions.Compiled);

    /// <summary>Parses the [Fonts] attachment section of an ASSA footer into decoded font files.</summary>
    public static List<(string FileName, byte[] Bytes)> GetEmbeddedFonts(string? footer)
    {
        var result = new List<(string, byte[])>();
        if (string.IsNullOrEmpty(footer))
        {
            return result;
        }

        var inFonts = false;
        var fileName = string.Empty;
        var content = new StringBuilder();

        void Flush()
        {
            if (fileName.Length > 0 && content.Length > 0)
            {
                try
                {
                    result.Add((Path.GetFileName(fileName), ClassicUUEncoding.UUDecode(content.ToString().Trim())));
                }
                catch
                {
                    // a malformed attachment should not break the caller
                }
            }

            content.Clear();
            fileName = string.Empty;
        }

        foreach (var line in footer.SplitToLines())
        {
            var s = line.Trim();
            if (s.Equals("[Fonts]", StringComparison.OrdinalIgnoreCase))
            {
                Flush();
                inFonts = true;
            }
            else if (s == "[Script Info]" || s == "[V4+ Styles]" || s == "[V4 Styles]" || s == "[Events]" ||
                     s.Equals("[Graphics]", StringComparison.OrdinalIgnoreCase))
            {
                // Only exact section headers end the fonts section - the UU-style encoding's
                // alphabet ('!'..'`') contains '[', so encoded lines can start with it.
                Flush();
                inFonts = false;
            }
            else if (!inFonts)
            {
                // skip
            }
            else if (s.StartsWith("fontname:", StringComparison.OrdinalIgnoreCase) ||
                     s.StartsWith("filename:", StringComparison.OrdinalIgnoreCase))
            {
                Flush();
                fileName = s.Remove(0, 9).Trim();
            }
            else if (s.Length == 0)
            {
                Flush();
            }
            else
            {
                content.AppendLine(s);
            }
        }

        Flush();
        return result;
    }

    /// <summary>
    /// Adds a font file to the [Fonts] attachment section of an ASSA footer, creating the
    /// section if needed. A font whose file name is already attached is not added twice.
    /// </summary>
    public static string AddFontToFooter(string? footer, string fontFilePath, byte[] fontBytes)
    {
        var fileName = Path.GetFileName(fontFilePath);
        if (GetEmbeddedFonts(footer).Any(f => f.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
        {
            return footer!;
        }

        var entryLines = new List<string> { "fontname: " + fileName };
        entryLines.AddRange(ClassicUUEncoding.UUEncode(fontBytes).Trim().SplitToLines());

        if (string.IsNullOrWhiteSpace(footer))
        {
            return "[Fonts]" + Environment.NewLine + string.Join(Environment.NewLine, entryLines) + Environment.NewLine;
        }

        var lines = footer.SplitToLines();
        var result = new List<string>(lines.Count + entryLines.Count + 2);
        var inFonts = false;
        var inserted = false;

        void InsertEntry()
        {
            while (result.Count > 0 && result[result.Count - 1].Trim().Length == 0)
            {
                result.RemoveAt(result.Count - 1);
            }

            result.AddRange(entryLines);
            inserted = true;
        }

        foreach (var line in lines)
        {
            var s = line.Trim();
            if (s.Equals("[Fonts]", StringComparison.OrdinalIgnoreCase))
            {
                inFonts = true;
            }
            else if (inFonts && !inserted &&
                     (s == "[Script Info]" || s == "[V4+ Styles]" || s == "[V4 Styles]" || s == "[Events]" ||
                      s.Equals("[Graphics]", StringComparison.OrdinalIgnoreCase) ||
                      s.Equals("[Aegisub Extradata]", StringComparison.OrdinalIgnoreCase)))
            {
                // Only exact section headers end the fonts section (see GetEmbeddedFonts).
                InsertEntry();
                result.Add(string.Empty);
                inFonts = false;
            }

            result.Add(line);
        }

        if (inFonts && !inserted)
        {
            InsertEntry();
        }

        if (!inserted)
        {
            // No [Fonts] section - fonts go first, same order the attachments window writes.
            return "[Fonts]" + Environment.NewLine + string.Join(Environment.NewLine, entryLines) +
                   Environment.NewLine + Environment.NewLine + footer.TrimStart();
        }

        return string.Join(Environment.NewLine, result) + Environment.NewLine;
    }

    /// <summary>
    /// Collects the font names an ASSA renderer would need: fonts of styles that are
    /// actually used by lines, plus inline <c>\fn</c> overrides.
    /// </summary>
    public static List<string> GetUsedFontNames(Subtitle subtitle)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string fontName)
        {
            fontName = fontName.Trim().TrimStart('@'); // "@" prefix = vertical variant of the same font
            if (fontName.Length > 0 && seen.Add(fontName))
            {
                result.Add(fontName);
            }
        }

        var header = subtitle.Header ?? string.Empty;
        var usedStyleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var paragraph in subtitle.Paragraphs)
        {
            usedStyleNames.Add(string.IsNullOrEmpty(paragraph.Extra) ? "Default" : paragraph.Extra.TrimStart('*'));

            foreach (Match match in FontNameTagRegex.Matches(paragraph.Text))
            {
                Add(match.Groups["name"].Value);
            }
        }

        if (header.IndexOf("[V4", StringComparison.Ordinal)>=0)
        {
            foreach (var styleName in AdvancedSubStationAlpha.GetStylesFromHeader(header))
            {
                if (usedStyleNames.Contains(styleName))
                {
                    Add(AdvancedSubStationAlpha.GetSsaStyle(styleName, header).FontName);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// The distinct visible text lines of a subtitle - override tags and drawing commands
    /// removed, <c>\N</c>/<c>\n</c> split into separate lines - i.e. the text a renderer
    /// actually draws. Used to compute the glyphs a trimmed font must keep.
    /// </summary>
    public static List<string> GetUsedTextLines(Subtitle subtitle)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var paragraph in subtitle.Paragraphs)
        {
            // RemoveSsaTags turns \h into a space, but renderers draw it as U+00A0.
            if (paragraph.Text.IndexOf("\\h", StringComparison.Ordinal)>=0 && seen.Add(" "))
            {
                result.Add(" ");
            }

            var text = Utilities.RemoveSsaTags(paragraph.Text, removeDrawingTags: true);
            foreach (var line in text.SplitToLines())
            {
                if (line.Length > 0 && seen.Add(line))
                {
                    result.Add(line);
                }
            }
        }

        return result;
    }


}}

