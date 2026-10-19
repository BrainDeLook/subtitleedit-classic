using Nikse.SubtitleEdit.Core.Common;



using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Logic.ClassicEffects {

/// <summary>
/// Slow zoom-out: each subtitle starts at 110% scale and smoothly eases down to 100%
/// over the full subtitle duration.
/// </summary>
public class AdvancedEffectSlowZoomOut : IAdvancedEffectDisplay
{
    public string Name => Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Медленное отдаление","Slow zoom-out");
    public string Description => Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Текст изначально немного крупнее, а затем плавно уменьшается до нормального размера на протяжении всего времени показа субтитров","Text starts slightly larger and gently shrinks to normal size over the subtitle duration");
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
            newSub.Text = $"{{\\fscx110\\fscy110\\t(0,{durationMs},\\fscx100\\fscy100)}}" + sub.Text;
            result.Add(newSub);
        }

        return result;
    }
}


}

