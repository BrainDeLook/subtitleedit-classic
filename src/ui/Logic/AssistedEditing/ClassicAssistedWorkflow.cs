using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic.AssistedEditing
{
    public sealed class ClassicAssistedChoice
    {
        public string Title { get; set; }
        public Subtitle Result { get; set; }
        public Subtitle OriginalResult { get; set; }
        public List<Paragraph> Preview { get; set; }
        public int SelectedIndex { get; set; }
    }
    public static class ClassicAssistedWorkflow
    {
        public static List<ClassicAssistedChoice> Split(Subtitle source, Subtitle original, bool editOriginal, int index)
        {
            var choices = new List<ClassicAssistedChoice>();
            if (source == null || index < 0 || index >= source.Paragraphs.Count) return choices;
            var language = LanguageAutoDetect.AutoDetectGoogleLanguage(source);
            var manager = new ClassicAssistedSplitManager();
            foreach (var suggestion in AssistedSplitCandidateGenerator.Generate(new ClassicAssistedLine(source.Paragraphs[index]), language, manager))
            {
                var result = new Subtitle(source, false); var first = result.Paragraphs[index];
                var second = new Paragraph(first) { NewSection = false };
                CopyTextTime(suggestion.FirstParagraph, first); CopyTextTime(suggestion.SecondParagraph, second);
                if (first.DurationTotalMilliseconds < 1 || second.DurationTotalMilliseconds < 1 || first.EndTime.TotalMilliseconds > second.StartTime.TotalMilliseconds) continue;
                result.Paragraphs.Insert(index + 1, second); result.Renumber();
                Subtitle originalResult = null;
                if (editOriginal && original != null)
                {
                    originalResult = new Subtitle(original, false);
                    var match = Utilities.GetOriginalParagraph(index, source.Paragraphs[index], originalResult.Paragraphs);
                    if (match != null)
                    {
                        var originalIndex = originalResult.Paragraphs.IndexOf(match);
                        var line = new ClassicAssistedLine(match); var lines = new ObservableCollection<ClassicAssistedLine> { line };
                        manager.Split(lines, line, LanguageAutoDetect.AutoDetectGoogleLanguage(original));
                        if (lines.Count == 2)
                        {
                            var originalSecond = new Paragraph(match) { NewSection = false };
                            match.Text = lines[0].Text; originalSecond.Text = lines[1].Text;
                            match.StartTime.TotalMilliseconds = first.StartTime.TotalMilliseconds; match.EndTime.TotalMilliseconds = first.EndTime.TotalMilliseconds;
                            originalSecond.StartTime.TotalMilliseconds = second.StartTime.TotalMilliseconds; originalSecond.EndTime.TotalMilliseconds = second.EndTime.TotalMilliseconds;
                            originalResult.Paragraphs.Insert(originalIndex + 1, originalSecond); originalResult.Renumber();
                        }
                    }
                }
                choices.Add(new ClassicAssistedChoice { Title = suggestion.Title, Result = result, OriginalResult = originalResult, SelectedIndex = index + 1, Preview = new List<Paragraph> { first, second } });
            }
            return choices;
        }
        public static List<ClassicAssistedChoice> Move(Subtitle source, Subtitle original, bool editOriginal, int index)
        {
            var choices = new List<ClassicAssistedChoice>();
            if (source == null || index < 0 || index >= source.Paragraphs.Count) return choices;
            var current = new ClassicAssistedLine(source.Paragraphs[index]);
            var previous = index > 0 ? new ClassicAssistedLine(source.Paragraphs[index - 1]) : null;
            var next = index + 1 < source.Paragraphs.Count ? new ClassicAssistedLine(source.Paragraphs[index + 1]) : null;
            foreach (var suggestion in AssistedMoveCandidateGenerator.Generate(current, previous, next, LanguageAutoDetect.AutoDetectGoogleLanguage(source)))
            {
                var result = new Subtitle(source, false); result.Paragraphs[index].Text = suggestion.NewCurrentText;
                var otherIndex = suggestion.Kind == AssistedMoveKind.WithPrevious ? index - 1 : suggestion.Kind == AssistedMoveKind.WithNext ? index + 1 : index;
                var firstIndex = Math.Min(index, otherIndex); var secondIndex = Math.Max(index, otherIndex);
                if (otherIndex != index)
                {
                    result.Paragraphs[otherIndex].Text = suggestion.NewOtherText;
                    if (suggestion.NewFirstEnd.HasValue && suggestion.NewSecondStart.HasValue)
                    {
                        result.Paragraphs[firstIndex].EndTime.TotalMilliseconds = suggestion.NewFirstEnd.Value.TotalMilliseconds;
                        result.Paragraphs[secondIndex].StartTime.TotalMilliseconds = suggestion.NewSecondStart.Value.TotalMilliseconds;
                    }
                }
                if (result.Paragraphs.Skip(firstIndex).Take(secondIndex - firstIndex + 1).Any(p => p.DurationTotalMilliseconds < 1)) continue;
                Subtitle originalResult = null;
                if (editOriginal && original != null)
                {
                    originalResult = new Subtitle(original, false);
                    foreach (var changed in new[] { firstIndex, secondIndex }.Distinct())
                    {
                        var match = Utilities.GetOriginalParagraph(changed, source.Paragraphs[changed], originalResult.Paragraphs);
                        if (match != null) { match.StartTime.TotalMilliseconds = result.Paragraphs[changed].StartTime.TotalMilliseconds; match.EndTime.TotalMilliseconds = result.Paragraphs[changed].EndTime.TotalMilliseconds; }
                    }
                }
                choices.Add(new ClassicAssistedChoice { Title = suggestion.Title, Result = result, OriginalResult = originalResult, SelectedIndex = index, Preview = new List<Paragraph> { result.Paragraphs[firstIndex] } });
                if (otherIndex != index) choices.Last().Preview.Add(result.Paragraphs[secondIndex]);
            }
            return choices;
        }
        private static void CopyTextTime(Paragraph source, Paragraph target) { target.Text = source.Text; target.StartTime.TotalMilliseconds = source.StartTime.TotalMilliseconds; target.EndTime.TotalMilliseconds = source.EndTime.TotalMilliseconds; }
    }
}
