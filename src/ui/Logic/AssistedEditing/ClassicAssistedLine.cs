using System;
using System.Collections.Generic;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic.AssistedEditing
{
    public sealed class ClassicAssistedLine
    {
        public Paragraph Paragraph { get; }
        public string Text { get => Paragraph.Text; set => Paragraph.Text = value; }
        public string OriginalText { get; set; }
        public TimeSpan StartTime { get => Paragraph.StartTime.TimeSpan; set => Paragraph.StartTime.TotalMilliseconds = value.TotalMilliseconds; }
        public TimeSpan EndTime { get => Paragraph.EndTime.TimeSpan; set => Paragraph.EndTime.TotalMilliseconds = value.TotalMilliseconds; }
        public TimeSpan Duration => EndTime - StartTime;
        public ClassicAssistedLine(Paragraph paragraph) { Paragraph = new Paragraph(paragraph, false); }
        public ClassicAssistedLine(ClassicAssistedLine source, bool newId) { Paragraph = new Paragraph(source.Paragraph, newId); OriginalText = source.OriginalText; }
        public void SetStartTimeOnly(TimeSpan time) { StartTime = time; }
    }
    public static class ClassicAssistedText
    {
        private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
        {
            ["SplitAtDialogDash"] = new[] { "Разделить диалог", "Split at dialog dash" },
            ["SplitAtSentenceEnd"] = new[] { "После конца предложения", "Split at end of sentence" },
            ["SplitAtLineBreak"] = new[] { "По переносу строки", "Split at line break" },
            ["SplitAtComma"] = new[] { "После запятой", "Split at comma" },
            ["SplitNearMiddle"] = new[] { "По пробелу ближе к середине", "Split near middle" },
            ["MoveUnfinishedSentenceToNextSubtitle"] = new[] { "Перенести незаконченную фразу в следующую реплику", "Move unfinished sentence to next subtitle" },
            ["BalanceWithNextSubtitle"] = new[] { "Сбалансировать со следующей репликой", "Balance with next subtitle" },
            ["MoveLastWordToNextSubtitle"] = new[] { "Перенести последнее слово в следующую реплику", "Move last word to next subtitle" },
            ["FetchFirstWordFromNextSubtitle"] = new[] { "Забрать первое слово следующей реплики", "Fetch first word from next subtitle" },
            ["FetchRestOfSentenceFromNextSubtitle"] = new[] { "Забрать конец предложения из следующей реплики", "Fetch rest of sentence from next subtitle" },
            ["MoveRestOfSentenceToPreviousSubtitle"] = new[] { "Перенести конец предложения в предыдущую реплику", "Move rest of sentence to previous subtitle" },
            ["BalanceWithPreviousSubtitle"] = new[] { "Сбалансировать с предыдущей репликой", "Balance with previous subtitle" },
            ["MoveFirstWordToPreviousSubtitle"] = new[] { "Перенести первое слово в предыдущую реплику", "Move first word to previous subtitle" },
            ["FetchLastWordFromPreviousSubtitle"] = new[] { "Забрать последнее слово предыдущей реплики", "Fetch last word from previous subtitle" },
            ["FetchUnfinishedSentenceFromPreviousSubtitle"] = new[] { "Забрать незаконченную фразу из предыдущей реплики", "Fetch unfinished sentence from previous subtitle" },
            ["MoveLastWordFromFirstLineDownCurrentSubtitle"] = new[] { "Перенести последнее слово первой строки вниз", "Move last word from first line down" },
            ["MoveFirstWordFromNextLineUpCurrentSubtitle"] = new[] { "Перенести первое слово второй строки вверх", "Move first word from second line up" },
        };
        public static bool Russian => Configuration.Settings.General.Language?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) == true;
        public static string L(string ru, string en) => Russian ? ru : en;
        public static string Get(string key) => Texts.TryGetValue(key, out var text) ? text[Russian ? 0 : 1] : key;
    }
}
