using System;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Core.Translate
{
    public static class TranslationMusic
    {
        private static readonly string[] Symbols = { "♩", "♪", "♫", "♬", "🎵", "🎶" };
        private static readonly char[] DialogDashes = { '-', '‐', '–', '—' };
        public static bool IsKeptUntranslated(string text) => Configuration.Settings.Tools.AutoTranslateKeepMusicLines && IsMusicLine(text);
        public static bool IsMusicLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var clean = HtmlUtil.RemoveHtmlTags(text, true).Trim();
            if (clean.Length == 0) return false;
            if (StartsWithSymbol(clean) && EndsWithSymbol(clean)) return true;
            var found = false;
            foreach (var raw in clean.SplitToLines())
            {
                var line = raw.Trim().TrimStart(DialogDashes).TrimStart();
                if (line.Length == 0) continue;
                if (!StartsWithSymbol(line) && !EndsWithSymbol(line)) return false;
                found = true;
            }
            return found;
        }
        private static bool StartsWithSymbol(string text)
        {
            foreach (var symbol in Symbols) if (text.StartsWith(symbol, StringComparison.Ordinal)) return true;
            return false;
        }
        private static bool EndsWithSymbol(string text)
        {
            foreach (var symbol in Symbols) if (text.EndsWith(symbol, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
