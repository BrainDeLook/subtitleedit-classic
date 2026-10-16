using System.Linq;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private void InitializeClassicAiMenu()
        {
            if (toolsToolStripMenuItem.DropDownItems.ContainsKey("classicAiReview")) return;
            var review = new ToolStripMenuItem(VoiceReferenceRu ? "AI-проверка субтитров…" : "AI subtitle review…") { Name = "classicAiReview" };
            var assistant = new ToolStripMenuItem(VoiceReferenceRu ? "AI-помощник текущей строки…" : "Current-line AI assistant…") { Name = "classicAiAssistant" };
            review.Click += (s,e) => OpenClassicAiReview(false);
            assistant.Click += (s,e) => OpenClassicAiReview(true);
            toolsToolStripMenuItem.DropDownItems.Add(review); toolsToolStripMenuItem.DropDownItems.Add(assistant);
        }
        private void OpenClassicAiReview(bool assistant)
        {
            if (_subtitle.Paragraphs.Count == 0) return;
            var index = FirstSelectedIndex;
            if (assistant && index < 0) return;
            using (var form = new ClassicAiReviewForm(_subtitle, assistant ? new[] { index } : Enumerable.Range(0, _subtitle.Paragraphs.Count).ToArray(), assistant))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                MakeHistoryForUndo("Before AI text changes");
                _subtitle = form.EditedSubtitle; SubtitleListview1.Fill(_subtitle, _subtitleOriginal); _subtitleListViewIndex = -1;
                if (index >= 0) SubtitleListview1.SelectIndexAndEnsureVisibleFaster(index);
                audioVisualizer.Invalidate();
            }
        }
    }
}
