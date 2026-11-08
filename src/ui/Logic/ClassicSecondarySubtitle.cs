using System;
using System.Collections.Generic;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;

namespace Nikse.SubtitleEdit.Logic
{
    public static class ClassicSecondarySubtitle
    {
        public static bool ShouldApplyOffset(Subtitle secondary, double offset, double windowStart, double windowEnd)
        {
            if (offset == 0 || secondary.Paragraphs.Count == 0) return offset != 0;
            var low = windowStart - 1000;
            var high = windowEnd == double.MaxValue ? double.MaxValue : windowEnd + 1000;
            var asIs = secondary.Paragraphs.Count(p => p.StartTime.TotalMilliseconds >= low && p.StartTime.TotalMilliseconds <= high);
            var shifted = secondary.Paragraphs.Count(p => p.StartTime.TotalMilliseconds - offset >= low && p.StartTime.TotalMilliseconds - offset <= high);
            return shifted >= asIs;
        }

        public static Subtitle Build(Subtitle source, string font, double offset, double durationSeconds, int width, int height, Subtitle main)
        {
            var result = new Subtitle(source);
            var start = durationSeconds > 0 || main.Paragraphs.Count == 0 ? 0 : main.Paragraphs.Min(p => p.StartTime.TotalMilliseconds);
            var end = durationSeconds > 0 ? durationSeconds * 1000 : main.Paragraphs.Count == 0 ? double.MaxValue : main.Paragraphs.Max(p => p.EndTime.TotalMilliseconds);
            if (ShouldApplyOffset(source, offset, start, end)) result.AddTimeToAllParagraphs(TimeSpan.FromMilliseconds(-offset));
            var style = new SsaStyle { Name = "Default", FontName = font, FontSize = Math.Max(12m, height * .045m), Alignment = "8", Primary = System.Drawing.Color.White, Outline = System.Drawing.Color.Black, OutlineWidth = 2, ShadowWidth = 1, MarginVertical = 15 };
            result.Header = AdvancedSubStationAlpha.GetHeaderAndStylesFromAdvancedSubStationAlpha(AdvancedSubStationAlpha.DefaultHeader, new List<SsaStyle> { style });
            result.Header = AdvancedSubStationAlpha.AddTagToHeader("PlayResX", "PlayResX: " + width, "[Script Info]", result.Header);
            result.Header = AdvancedSubStationAlpha.AddTagToHeader("PlayResY", "PlayResY: " + height, "[Script Info]", result.Header);
            foreach (var paragraph in result.Paragraphs) paragraph.Extra = "Default";
            return result;
        }
    }
}
