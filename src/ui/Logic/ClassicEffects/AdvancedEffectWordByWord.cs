using Nikse.SubtitleEdit.Core.Common;



using System;
using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Logic.ClassicEffects {

/// <summary>
/// Reveals text one word at a time: produces N sequential subtitle entries where entry i
/// shows the first i+1 words and spans 1/N of the original duration.
/// </summary>
public class AdvancedEffectWordByWord : IAdvancedEffectDisplay
{
    public string Name => Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Слово за словом","Word by word");
    public string Description => Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Текст отображается по одному слову на протяжении всего времени показа субтитров","Reveals text one word at a time over the subtitle duration");
    public bool UsesAudio => false;
    public override string ToString() => Name;

    public List<SubtitleLineViewModel> ApplyEffect(string header, List<SubtitleLineViewModel> subtitles, int width, int height, WavePeakData wavePeaks)
    {
        var result = new List<SubtitleLineViewModel>();
        foreach (var subtitle in subtitles)
        {
            var cleanText = Utilities.RemoveSsaTags(subtitle.Text)
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ");

            if (string.IsNullOrWhiteSpace(cleanText))
            {
                result.Add(AdvancedEffectUtil.PassThrough(subtitle));
                continue;
            }

            var words = cleanText.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 1)
            {
                result.Add(AdvancedEffectUtil.PassThrough(subtitle));
                continue;
            }

            var totalMs = subtitle.Duration.TotalMilliseconds;
            var msPerWord = totalMs / words.Length;

            for (var i = 0; i < words.Length; i++)
            {
                var line = new SubtitleLineViewModel(subtitle, generateNewId: true);
                var start = subtitle.StartTime.Add(TimeSpan.FromMilliseconds(i * msPerWord));
                var end = i == words.Length - 1
                    ? subtitle.EndTime
                    : subtitle.StartTime.Add(TimeSpan.FromMilliseconds((i + 1) * msPerWord));
                line.StartTime = start;
                line.EndTime = end;
                line.Text = string.Join(" ", words, 0, i + 1);
                result.Add(line);
            }
        }
        return result;
    }
}


}


