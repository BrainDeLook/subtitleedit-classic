using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic.Ocr
{
    // Image-matcher-only repairs from Subtitle Edit v5.4.0. Text OCR engines must not
    // interpret a real slash or hash as a glyph-recognition error.
    public sealed class OcrImageMatcherRepairs
    {
        private readonly Func<string, bool> _known;
        public OcrImageMatcherRepairs(Func<string, bool> known) { _known = known; }
        public string RepairLine(string text, bool french)
        {
            text = FixHashMisreadAsApostrophe(text);
            text = FixSlashMisreadAsL(text);
            return FixSpaceBeforeClosingQuote(FixSpacedThousandsSeparator(text), french);
        }
        public string RepairWord(string word)
        {
            if (_known(word.Trim('\'', '’', '-'))) return word;
            if (TryFixBarMisreadByCase(word, out var barFixed))
            {
                word = barFixed;
                if (_known(word.Trim('\'', '’', '-'))) return word;
            }
            if (TrySplitAtOverhang(word, out var split)) return split;
            return word;
        }
    /// <summary>
    /// Image matchers split the dots of a wide-set ellipsis far enough apart to read each as a
    /// word: ". . ." ("voor. . .", "<i>. . .en</i>"). No subtitle means three spaced periods, so
    /// join them (SE4's hardcoded rules did the same at the line start and end). The two-dot
    /// forms ". .." and ".. ." are Tesseract's and stay with the replace lists, which only join
    /// them at the end of a line after a letter or digit.
    /// </summary>
    public static string NormalizeSpacedEllipses(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains(". . .", StringComparison.Ordinal))
        {
            return text;
        }

        return SpacedEllipsis().Replace(text, "...");
    }

    private static readonly Regex SpacedEllipsisRegex = new Regex(@"(?<![.…])\. \. \.(?![.…])", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    private static Regex SpacedEllipsis() => SpacedEllipsisRegex;

    /// <summary>
    /// Tesseract emits typographic single quotes for apostrophes ("‘cause", "didn’t"); subtitles
    /// use the plain apostrophe. A line holding both an opening and a closing curly quote is
    /// quoting something ("La lettera ‘E’") and is left alone. Runs after the replace list so
    /// entries written with the curly forms still match. Like the per-word straightening in
    /// <see cref="OcrFixReplaceList2.FixCommonWordErrors"/> it is one of the hardcoded rules, so
    /// "use hardcoded rules" turns both off together.
    /// </summary>
    public static string NormalizeApostrophes(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var hasOpening = text.Contains('‘');
        var hasClosing = text.Contains('’');
        if (hasOpening == hasClosing)
        {
            return text; // neither, or a quotation pair
        }

        // Tesseract also emits both forms side by side ("'‘cause", "ma‘'am") - collapse that pair,
        // but only that pair: a straight '' elsewhere on the line is not the OCR's doing.
        return text.Replace("'‘", "'").Replace("‘'", "'").Replace("'’", "'").Replace("’'", "'")
            .Replace('‘', '\'').Replace('’', '\'');
    }

    // A word with a "#" between two letters, or next to a slash ("He#s", "isn#t", "/#//").
    private static readonly Regex WordWithHashRegex = new Regex(@"(?<![\p{L}\d#/'’])[\p{L}'’/]*[\p{L}/]#[\p{L}/][\p{L}'’/#]*", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    private static Regex WordWithHash() => WordWithHashRegex;

    /// <summary>
    /// A bold italic apostrophe fits a "#" entry ("He#s", "isn#t", "You#//"). No word has a hash
    /// between two letters, so read it as an apostrophe when the dictionary or names list knows
    /// the result; a word that also has slashes is left to the slash rule to judge.
    /// </summary>
    private string FixHashMisreadAsApostrophe(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('#'))
        {
            return text;
        }

        return WordWithHash().Replace(text, m =>
        {
            var candidate = m.Value.Replace('#', '\'');
            if (candidate.Contains('/'))
            {
                return candidate;
            }

            var bare = candidate.Trim('\'', '’');
            return bare.Length > 1 && _known(bare) ? candidate : m.Value;
        });
    }

    // A run of letters with a slash touching a letter ("p/ease", "/itt/e", "wi/I", "/'m"). A slash
    // right after "<" is a closing tag ("</i>"), not a misread letter - "li" is a word in some
    // dictionaries (Dutch), which turned every "</i>" into "<li>".
    private static readonly Regex WordWithSlashRegex = new Regex(@"(?<![\p{L}\d/'’<])[\p{L}'’/]*(?:[\p{L}'’]/|/['’]?\p{L})[\p{L}'’/]*", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    private static Regex WordWithSlash() => WordWithSlashRegex;

    // A slash standing alone between spaces (or at a line or tag edge): a slanted italic "I".
    private static readonly Regex LoneSlashRegex = new Regex(@"(?<![^\s>])/(?![^\s<])", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    private static Regex LoneSlash() => LoneSlashRegex;

    /// <summary>
    /// Image matchers read a slanted italic "l" as "/" ("p/ease", "re/igions", "/itt/e"), and a
    /// slanted "i" ("w/th", "do/ng") or "I" ("/t's", "/'m", "/ can") too. The line splitter cuts
    /// words at the slash, so the per-word fixes never see the whole word - repair it on the
    /// line: every slash to "l", and an "I" after a lowercase letter to "l" ("wi/I", "teI/ing");
    /// failing that, the other readings of the slashes ("i" anywhere, "I" at the start of the
    /// word). Each is kept only when the dictionary or names list accepts the result. A real
    /// slash between two words ("and/or", "AC/DC") is left alone, and so is a lone slash
    /// separating numbers or two all-caps words ("1 / 2", "PILOTEN / OMGEKOMEN" on a title card).
    /// </summary>
    private string FixSlashMisreadAsL(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('/'))
        {
            return text;
        }

        if (LoneSlash().IsMatch(text) && _known("I"))
        {
            text = LoneSlash().Replace(text, m => IsSeparatorSlash(text, m.Index) ? "/" : "I");
        }

        return WordWithSlash().Replace(text, m =>
        {
            var token = m.Value;
            // "//example.com" after "http:" is a URL; "a//", "wi//" and "ki//ing" are words.
            if (token.StartsWith("//", StringComparison.Ordinal) || m.Index > 0 && text[m.Index - 1] == ':')
            {
                return token;
            }

            var parts = token.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1 && parts.All(p => p.Length > 1 && _known(p)))
            {
                return token; // "and/or", "he/she"
            }

            var chars = token.Replace('/', 'l').ToCharArray();
            for (var i = 1; i < chars.Length; i++)
            {
                if (chars[i] == 'I' && char.IsLower(chars[i - 1]))
                {
                    chars[i] = 'l';
                }
            }

            var candidate = new string(chars);
            if (IsWordOrName(candidate))
            {
                return candidate;
            }

            // The slanted stroke may be an "i" instead ("w/th", "aff/n/ty"), or a capital "I" at
            // the start of the word ("/t's", "/'m"): try every reading of the slashes, "l"
            // first, and keep the first one the dictionary knows.
            var slashes = new List<int>();
            for (var i = 0; i < token.Length && slashes.Count <= MaxSlashesToRead; i++)
            {
                if (token[i] == '/')
                {
                    slashes.Add(i);
                }
            }

            if (slashes.Count > MaxSlashesToRead)
            {
                return token;
            }

            // "/t's" is "it's" mid-sentence and "It's" at the start of one.
            var readings = new string[slashes.Count];
            for (var k = 0; k < readings.Length; k++)
            {
                readings[k] = slashes[k] != 0 ? "li" : IsSentenceStart(text, m.Index) ? "lIi" : "liI";
            }

            foreach (var reading in ReadingsOf(readings))
            {
                for (var k = 0; k < slashes.Count; k++)
                {
                    chars[slashes[k]] = reading[k];
                }

                candidate = new string(chars);
                if (IsWordOrName(candidate))
                {
                    return candidate;
                }
            }

            return token;
        });
    }

    private const int MaxSlashesToRead = 4;

    // "20. 000": a one-to-three digit group, a period, a space and a three-digit group. A four-digit
    // number before the period ("1940. 300") is a year ending a sentence, not a thousands group.
    private static readonly Regex SpacedThousandsSeparatorRegex = new Regex(@"(?<![\d.,])(\d{1,3})\. (\d{3})(?!\d)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    private static Regex SpacedThousandsSeparator() => SpacedThousandsSeparatorRegex;

    /// <summary>
    /// Italic digits lean into the thousands separator, so the space detector puts a word space
    /// after the period: "20. 000" -> "20.000", "klim tot 19. 000 voet" -> "19.000".
    /// </summary>
    internal static string FixSpacedThousandsSeparator(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains(". ", StringComparison.Ordinal))
        {
            return text;
        }

        return SpacedThousandsSeparator().Replace(text, "$1.$2");
    }

    // Sentence end, a space, then a closing quote followed by whitespace, a tag or the end.
    private static readonly Regex SpaceBeforeClosingQuoteRegex = new Regex(@"([.!?…]) ([""'”’»])(?=\s|<|$)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    private static Regex SpaceBeforeClosingQuote() => SpaceBeforeClosingQuoteRegex;

    /// <summary>
    /// A closing quote after an italic period gets a word space in front of it ("voorbij. '",
    /// "beginnen. \""): drop it. A quote that opens the next word ("Kom.' 'Nee.'", "'s avonds")
    /// is not followed by whitespace and stays. French typography puts a space before the
    /// closing guillemet ("« Il est parti. »"), so that one stays in French and next to a «.
    /// </summary>
    internal static string FixSpaceBeforeClosingQuote(string text, bool isFrench = false)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains(' '))
        {
            return text;
        }

        var keepGuillemetSpace = isFrench || text.Contains('«');
        return SpaceBeforeClosingQuote().Replace(text, m =>
            keepGuillemetSpace && m.Groups[2].Value == "»" ? m.Value : m.Groups[1].Value + m.Groups[2].Value);
    }

    /// <summary>
    /// A lone slash next to a number ("1 / 2") or between two all-caps words ("PILOTEN /
    /// OMGEKOMEN") is a separator, not a slanted italic "I" - an italic "I" sits between
    /// ordinary words ("/ can", "and / will").
    /// </summary>
    private static bool IsSeparatorSlash(string text, int index)
    {
        var before = NeighbourWord(text, index, -1);
        var after = NeighbourWord(text, index, 1);
        if (before.Any(char.IsDigit) || after.Any(char.IsDigit))
        {
            return true;
        }

        return IsAllCaps(before) && IsAllCaps(after);
    }

    // Image matchers read the sans-serif capital "I" as "l" ("PlLOTEN"); the per-word rules only
    // repair that after this runs, so an "l" counts as uppercase here.
    private static bool IsAllCaps(string word)
    {
        return word.Any(char.IsUpper) && word.All(c => !char.IsLower(c) || c == 'l');
    }

    /// <summary>
    /// The run of non-whitespace characters before (<paramref name="direction"/> -1) or after
    /// (+1) <paramref name="index"/>, skipping whitespace and tags; empty at a line edge.
    /// </summary>
    private static string NeighbourWord(string text, int index, int direction)
    {
        var i = index + direction;
        while (i >= 0 && i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                i += direction;
            }
            else if (direction < 0 && text[i] == '>')
            {
                var open = text.LastIndexOf('<', i);
                if (open < 0)
                {
                    break;
                }

                i = open - 1;
            }
            else if (direction > 0 && text[i] == '<')
            {
                var close = text.IndexOf('>', i);
                if (close < 0)
                {
                    break;
                }

                i = close + 1;
            }
            else
            {
                break;
            }
        }

        var end = i;
        while (end >= 0 && end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '<' && text[end] != '>')
        {
            end += direction;
        }

        return direction < 0 ? text.Substring(end + 1, i - end) : text.Substring(i, end - i);
    }

    /// <summary>
    /// Nothing but whitespace, dashes, quotes and tags between the start of the text or the end
    /// of a sentence and <paramref name="index"/>.
    /// </summary>
    private static bool IsSentenceStart(string text, int index)
    {
        var i = index - 1;
        while (i >= 0)
        {
            var c = text[i];
            if (c == '>')
            {
                var open = text.LastIndexOf('<', i);
                if (open < 0)
                {
                    return false;
                }

                i = open - 1;
                continue;
            }

            if (char.IsWhiteSpace(c) || c is '-' or '–' or '"' or '\'' or '“' or '‘' or '«' or '(' or '[')
            {
                i--;
                continue;
            }

            return c is '.' or '!' or '?' or '…' or ':';
        }

        return true;
    }

    /// <summary>Every combination of one letter per position, first letters first.</summary>
    private static IEnumerable<string> ReadingsOf(string[] lettersPerPosition)
    {
        var picks = new int[lettersPerPosition.Length];
        while (true)
        {
            yield return string.Concat(picks.Select((pick, k) => lettersPerPosition[k][pick]));

            var position = picks.Length - 1;
            while (position >= 0 && ++picks[position] == lettersPerPosition[position].Length)
            {
                picks[position] = 0;
                position--;
            }

            if (position < 0)
            {
                yield break;
            }
        }
    }

    private bool IsWordOrName(string candidate)
    {
        var bare = candidate.Trim('\'', '’');
        return bare.Length > 1 && _known(bare);
    }

    /// <summary>
    /// Splits a word of letters at the first "f", "r" or "t" followed by "j", "v" or "w" where both
    /// halves are dictionary words or names and the right half is a short function word of at
    /// most three letters ("je", "we", "van"): "Hoorje" -> "Hoor je", "Vijfvan" -> "Vijf van". The
    /// length cap keeps an unlisted compound or interjection whole ("Getverdikkeme" is not "Get
    /// verdikkeme"). A word with digits or punctuation is left alone.
    /// </summary>
    private bool TrySplitAtOverhang(string word, out string split)
    {
        split = word;
        if (word.Length < 4 || !word.All(char.IsLetter))
        {
            return false;
        }

        for (var i = Math.Max(2, word.Length - MaxOverhangRightHalf); i <= word.Length - 2; i++)
        {
            var before = char.ToLowerInvariant(word[i - 1]);
            var after = char.ToLowerInvariant(word[i]);
            if (before is not ('f' or 'r' or 't') || after is not ('j' or 'v' or 'w'))
            {
                continue;
            }

            var left = word.Substring(0, i);
            var right = word.Substring(i);
            if (IsWordOrName(left) && IsWordOrName(right))
            {
                split = left + " " + right;
                return true;
            }
        }

        return false;
    }

    private const int MaxOverhangRightHalf = 3;

    /// <summary>
    /// Per run of letters in <paramref name="word"/>: in a run of capitals (two or more besides the
    /// bars, or one with a bar in front) every "l" becomes "I"; otherwise an "I" right after a lowercase letter, or as the
    /// second letter between a capital and a lowercase letter, becomes "l". "Il", "lk" and
    /// "McIntyre" are left alone.
    /// </summary>
    internal static bool TryFixBarMisreadByCase(string word, out string fixedWord)
    {
        fixedWord = word;
        if (string.IsNullOrEmpty(word) || word.IndexOf('I') < 0 && word.IndexOf('l') < 0)
        {
            return false;
        }

        var chars = word.ToCharArray();
        var start = 0;
        while (start < chars.Length)
        {
            if (!char.IsLetter(chars[start]))
            {
                start++;
                continue;
            }

            var end = start;
            while (end < chars.Length && char.IsLetter(chars[end]))
            {
                end++;
            }

            FixBarsInLetterRun(chars, start, end);
            start = end;
        }

        fixedWord = new string(chars);
        return fixedWord != word;
    }

    private static void FixBarsInLetterRun(char[] chars, int start, int end)
    {
        var uppers = 0;
        var lowersBesidesL = 0;
        var bars = 0;
        for (var i = start; i < end; i++)
        {
            var c = chars[i];
            if (c == 'l')
            {
                bars++;
            }
            else if (char.IsUpper(c))
            {
                uppers++;
            }
            else if (char.IsLower(c))
            {
                lowersBesidesL++;
            }
        }

        // Two capitals besides the bars make a run all-caps ("JUNl", "lFF"); with one, only a bar
        // in front of the capital does ("lN", "lS") - "Il" is Italian and a roman numeral.
        if (bars > 0 && lowersBesidesL == 0 && (uppers >= 2 || uppers == 1 && chars[start] == 'l'))
        {
            for (var i = start; i < end; i++)
            {
                if (chars[i] == 'l')
                {
                    chars[i] = 'I';
                }
            }

            return;
        }

        if (lowersBesidesL == 0 && bars == 0)
        {
            return; // all caps without a bar, or "I" alone
        }

        for (var i = start; i < end; i++)
        {
            if (chars[i] != 'I')
            {
                continue;
            }

            if (i > start && char.IsLower(chars[i - 1]))
            {
                // "McIntyre", "MacIsaac": the capital belongs to the name.
                var isMc = i - start >= 2 && chars[i - 2] == 'M' && chars[i - 1] == 'c';
                var isMac = i - start >= 3 && chars[i - 3] == 'M' && chars[i - 2] == 'a' && chars[i - 1] == 'c';
                if (!isMc && !isMac)
                {
                    chars[i] = 'l';
                }
            }
            else if (i == start + 1 && char.IsUpper(chars[start]))
            {
                // "FIight", "AIvar", "AIIenwood": the bars between the capital and the first
                // lowercase letter.
                var j = i;
                while (j < end && chars[j] == 'I')
                {
                    j++;
                }

                if (j < end && char.IsLower(chars[j]))
                {
                    for (var k = i; k < j; k++)
                    {
                        chars[k] = 'l';
                    }
                }
            }
        }
    }


    }
}
