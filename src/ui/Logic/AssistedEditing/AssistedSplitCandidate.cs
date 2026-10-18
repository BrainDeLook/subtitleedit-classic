// Adapted from Subtitle Edit v5.3.0; GPL-3.0. UI-independent classic adapter.
namespace Nikse.SubtitleEdit.Logic.AssistedEditing
{

public class AssistedSplitCandidate
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TextIndex { get; set; }
    public string FirstText { get; set; } = string.Empty;
    public string SecondText { get; set; } = string.Empty;
    public string FirstInfo { get; set; } = string.Empty;
    public string SecondInfo { get; set; } = string.Empty;
    public Nikse.SubtitleEdit.Core.Common.Paragraph FirstParagraph { get; set; }
    public Nikse.SubtitleEdit.Core.Common.Paragraph SecondParagraph { get; set; }
}

}