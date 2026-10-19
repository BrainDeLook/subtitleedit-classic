using Nikse.SubtitleEdit.Core.Common;



using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Logic.ClassicEffects {

/// <summary>
/// Slow zoom-in: each subtitle starts at 100% scale and smoothly grows to 110%
/// over the full subtitle duration — a barely-noticeable creeping zoom.
/// </summary>
public class AdvancedEffectSlowZoomIn : IAdvancedEffectDisplay
{
    public string Name => Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Медленное приближение","Slow zoom-in");
    public string Description => Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Текст начинается с обычного размера и постепенно увеличивается в течение всего времени показа субтитров","Text starts at normal size and gently grows slightly over the subtitle duration");
    public bool UsesAudio => false;

    public override string ToString() => Name;

    public List<SubtitleLineViewModel> ApplyEffect(string header, List<SubtitleLineViewModel> subtitles, int width, int height, WavePeakData wavePeaks)
    {
        var result = new List<SubtitleLineViewModel>();
        if (subtitles.Count == 0)
        {
            return result;
        }

        foreach (var sub in subtitles)
        {
            var newSub = new SubtitleLineViewModel(sub, generateNewId: true);
            int durationMs = (int)sub.Duration.TotalMilliseconds;
            newSub.Text = $"{{\\fscx100\\fscy100\\t(0,{durationMs},\\fscx110\\fscy110)}}" + sub.Text;
            result.Add(newSub);
        }

        return result;
    }
}


}

