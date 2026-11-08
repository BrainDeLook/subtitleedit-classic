using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.Core.Common.TextLengthCalculator
{
    public static class CalcFactory
    {
        public static List<ICalcLength> Calculators = new List<ICalcLength>
        {
            new CalcAll(),
            new CalcNoSpaceCpsOnly(),
            new CalcNoSpace(),
            new CalcCjk(),
            new CalcCjkNoSpace(),
            new CalcIncludeCompositionCharacters(),
            new CalcIncludeCompositionCharactersNotSpace(),
            new CalcNoSpaceOrPunctuation(),
            new CalcNoSpaceOrPunctuationCpsOnly(),
        };

        public static string RemoveTags(string text)
        {
            var s = HtmlUtil.RemoveHtmlTags(text, true);
            return RemoveIgnoredTags(s);
        }

        private static volatile Regex _ignoreTagsRegex;

        /// <summary>
        /// A user pattern for markup SE does not know - a client platform's own tags like
        /// &lt;=title&gt; or &lt;@&gt; inside SRT/DFXP (#16163). Matches are not text, so the
        /// counters (and the grid's "Hide tags" mode) drop them. Empty or invalid clears it.
        /// </summary>
        public static void SetIgnoreTagsPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                _ignoreTagsRegex = null;
                return;
            }

            try
            {
                _ignoreTagsRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            }
            catch (ArgumentException)
            {
                _ignoreTagsRegex = null;
            }
        }

        public static bool HasIgnoreTagsPattern => _ignoreTagsRegex != null;

        /// <summary>
        /// Strips the matches of <see cref="SetIgnoreTagsPattern"/>; a no-op without a pattern.
        /// </summary>
        public static string RemoveIgnoredTags(string text)
        {
            var regex = _ignoreTagsRegex;
            if (regex == null || string.IsNullOrEmpty(text))
            {
                return text;
            }

            try
            {
                return regex.Replace(text, string.Empty);
            }
            catch (RegexMatchTimeoutException)
            {
                return text;
            }
        }

        public static ICalcLength MakeCalculator(string strategy)
        {
            var c = Calculators.FirstOrDefault(calculator => calculator.GetType().Name == strategy);
            return c ?? new CalcAll();
        }
    }
}
