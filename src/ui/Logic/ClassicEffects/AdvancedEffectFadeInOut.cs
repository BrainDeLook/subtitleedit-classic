using Nikse.SubtitleEdit.Core.Common;



using System;
using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Logic.ClassicEffects {

/// <summary>
/// Fade-in / fade-out: subtitle fades in at the start and fades out at the end
/// using the ASS \fad tag applied directly to each subtitle line.
/// </summary>
public class AdvancedEffectFadeInOut : IAdvancedEffectDisplay
{
    public string Name => Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Плавное нарастание/затухание","Fade in/out");
    public string Description => Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Текст плавно появляется в начале и плавно исчезает в конце каждого субтитра","Text fades in at the start and fades out at the end of each subtitle");
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
            int durationMs = (int)sub.Duration.TotalMilliseconds;
            int fadeMs = Math.Max(0, (int)Math.Min(500, durationMs / 3.0));

            var fadeSub = new SubtitleLineViewModel(sub, generateNewId: true);
            fadeSub.Text = $"{{\\fad({fadeMs},{fadeMs})}}" + sub.Text;
            result.Add(fadeSub);
        }

        return result;
    }
}

}

