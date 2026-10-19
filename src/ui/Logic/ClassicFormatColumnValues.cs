using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic
{
    // Stable 5.3 display semantics, without changing SE4's core/settings binary.
    public static class ClassicFormatColumnValues
    {
        private static readonly Regex Classes = new Regex(@"<c\.[\.a-zA-Z\d#_-]+>", RegexOptions.Compiled);

        public static string WebVttClasses(string text)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Classes.Matches(text ?? string.Empty))
                foreach (var name in match.Value.Substring(3, match.Length - 4).Split('.'))
                    if (name.Length > 0 && seen.Add(name)) names.Add(name);
            return string.Join(", ", names);
        }

        public static string WebVttVoice(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var start = text.IndexOf("<v ", StringComparison.Ordinal);
            if (start < 0) return string.Empty;
            var end = text.IndexOf('>', start);
            return end < 0 ? string.Empty : text.Substring(start + 3, end - start - 3).Trim();
        }

        public static string Teletext(Paragraph paragraph, string text = null)
        {
            text = text ?? paragraph.Text ?? string.Empty;
            var alignment = string.Empty;
            if (text.StartsWith(@"{\an1}") || text.StartsWith(@"{\an4}") || text.StartsWith(@"{\an7}")) alignment = "L";
            else if (text.StartsWith(@"{\an3}") || text.StartsWith(@"{\an6}") || text.StartsWith(@"{\an9}")) alignment = "R";
            else if (text.StartsWith(@"{\an2}") || text.StartsWith(@"{\an5}") || text.StartsWith(@"{\an8}")) alignment = "C";
            return int.TryParse(paragraph.MarginV, out var row) && row >= 1 && row <= 23
                ? row.ToString(CultureInfo.InvariantCulture) + " " + (alignment.Length == 0 ? "C" : alignment)
                : alignment;
        }
    }
}
