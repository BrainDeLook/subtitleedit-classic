using Nikse.SubtitleEdit.Core.Common;



using System;
using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Logic.ClassicEffects {

public class AdvancedEffectEndCreditsScroll : IAdvancedEffectDisplay
{
    public string Name => Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Титры (прокрутка)","Credits scroll");
    public string Description => Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Все строки субтитров прокручиваются снизу вверх в виде непрерывной ленты титров","Scrolls all subtitle lines from bottom to top as a continuous credits roll");
    public bool UsesAudio => false;

    public override string ToString() => Name;

    public List<SubtitleLineViewModel> ApplyEffect(string header, List<SubtitleLineViewModel> subtitles, int width, int height, WavePeakData wavePeaks)
    {
        var result = new List<SubtitleLineViewModel>();
        if (subtitles.Count == 0)
        {
            return result;
        }

        int screenWidth = width > 0 ? width : 1356;
        int screenHeight = height > 0 ? height : 678;
        int centerX = screenWidth / 2;

        double travelDurationMs = 12000;
        double leadTimeMs = 500;
        int internalLineSpacing = 60; // Slightly increased for the bigger font

        int yStartBase = screenHeight + 20;
        int yEndBase = -150;

        foreach (var sub in subtitles)
        {
            string processedText = sub.Text.Replace("\\N", "\n").Replace("\\n", "\n");
            var clean = Utilities.RemoveSsaTags(processedText);
            if (string.IsNullOrWhiteSpace(clean))
            {
                continue;
            }

            var lines = clean.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                var charLine = new SubtitleLineViewModel(sub, generateNewId: true);

                double startTimeMs = sub.StartTime.TotalMilliseconds - leadTimeMs;
                if (startTimeMs < 0)
                {
                    startTimeMs = 0;
                }

                charLine.StartTime = TimeSpan.FromMilliseconds(startTimeMs);
                charLine.EndTime = charLine.StartTime.Add(TimeSpan.FromMilliseconds(travelDurationMs));

                int yOffset = i * internalLineSpacing;

                string tags =
                    $@"\an8\fad(300,300)" +
                    $@"\move({centerX},{yStartBase + yOffset},{centerX},{yEndBase + yOffset},0,{(int)travelDurationMs})";

                charLine.Text = "{" + tags + "}" + lines[i].Trim();
                result.Add(charLine);
            }
        }

        return result;
    }
}


}

