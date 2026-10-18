using System;
using System.Collections.Generic;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic
{
    internal static class ClassicPointSync
    {
        // Always transform the frozen source. Applying a point twice must not compound it.
        public static Subtitle Apply(Subtitle source, SortedDictionary<int, (TimeSpan Time, int OtherIndex)> points)
        {
            if (source == null || points.Count == 0) return null;
            var keys = points.Keys.ToArray();
            if (keys.Any(k => k < 0 || k >= source.Paragraphs.Count)) return null;
            var result = new Subtitle(source, false);
            for (var i = 0; i < result.Paragraphs.Count; i++)
            {
                var segment = 0;
                while (segment < keys.Length - 2 && i >= keys[segment + 1]) segment++;
                var first = keys[segment]; var factor = 1.0;
                if (keys.Length > 1)
                {
                    var second = keys[segment + 1];
                    var delta = source.Paragraphs[second].StartTime.TotalMilliseconds - source.Paragraphs[first].StartTime.TotalMilliseconds;
                    var target = points[second].Time.TotalMilliseconds - points[first].Time.TotalMilliseconds;
                    if (delta <= 0 || target <= 0) return null;
                    factor = target / delta;
                }
                var offset = points[first].Time.TotalMilliseconds - source.Paragraphs[first].StartTime.TotalMilliseconds * factor;
                var p = result.Paragraphs[i];
                var start = p.StartTime.TotalMilliseconds * factor + offset;
                var end = p.EndTime.TotalMilliseconds * factor + offset;
                if (double.IsNaN(start) || double.IsInfinity(end) || start < 0 || end < start || end > TimeCode.MaxTimeTotalMilliseconds) return null;
                p.StartTime.TotalMilliseconds = start; p.EndTime.TotalMilliseconds = end;
            }
            return result;
        }
    }
}
