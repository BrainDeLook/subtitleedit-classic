using Nikse.SubtitleEdit.Core.Common;
using Newtonsoft.Json;

namespace Nikse.SubtitleEdit.Forms
{
    public partial class Main
    {
        private static string ClassicCompareContent(Subtitle subtitle) => JsonConvert.SerializeObject(new { subtitle.Paragraphs, subtitle.Header, subtitle.Footer, subtitle.OriginalFormat, subtitle.OriginalEncoding });

        private bool ApplyClassicCompare(Subtitle baseline, Subtitle result)
        {
            if (baseline == null || result == null || ClassicCompareContent(_subtitle) != ClassicCompareContent(baseline)) return false;
            if (ClassicCompareContent(_subtitle) == ClassicCompareContent(result)) return true;
            var index = FirstSelectedIndex;
            MakeHistoryForUndo("Before applying subtitle comparison");
            _subtitle.Paragraphs.Clear();
            foreach (var paragraph in result.Paragraphs) _subtitle.Paragraphs.Add(new Paragraph(paragraph, false));
            SubtitleListview1.Fill(_subtitle, _subtitleOriginal); _subtitleListViewIndex = -1;
            if (index >= 0 && index < _subtitle.Paragraphs.Count) SubtitleListview1.SelectIndexAndEnsureVisibleFaster(index);
            audioVisualizer.Invalidate(); UpdateSourceView(); SetTitle(); return true;
        }
    }
}
