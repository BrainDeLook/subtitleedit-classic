using Nikse.SubtitleEdit.Core.Common;



using System;
using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Logic.ClassicEffects {

public class AdvancedEffectTypewriterWithHighlight : IAdvancedEffectDisplay
{
    public string Name => Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Пишущая машинка с выделением текста","Typewriter with highlight");
    public string Description => Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Текст отображается по одному символу, при этом текущий символ подсвечивается","Reveals text one character at a time with a glowing highlight on the current character");
    public bool UsesAudio => false;

    public override string ToString() => Name;

    public List<SubtitleLineViewModel> ApplyEffect(string header, List<SubtitleLineViewModel> subtitles, int width, int height, WavePeakData wavePeaks)
    {
        var result = new List<SubtitleLineViewModel>();

        foreach (var subtitle in subtitles)
        {
            var cleanText = Utilities.RemoveSsaTags(subtitle.Text);
            if (string.IsNullOrEmpty(cleanText))
            {
                result.Add(AdvancedEffectUtil.PassThrough(subtitle));
                continue;
            }

            var text = cleanText.Replace("\r\n", "\n").Replace("\r", "\n");
            var chars = text.ToCharArray();
            var charCount = chars.Length;
            var msPerChar = subtitle.Duration.TotalMilliseconds / charCount;

            for (var i = 0; i < charCount; i++)
            {
                var line = new SubtitleLineViewModel(subtitle, generateNewId: true);

                var start = subtitle.StartTime.Add(TimeSpan.FromMilliseconds(i * msPerChar));
                var end = (i == charCount - 1)
                    ? subtitle.EndTime
                    : subtitle.StartTime.Add(TimeSpan.FromMilliseconds((i + 1) * msPerChar));

                line.StartTime = start;
                line.EndTime = end;

                string previousText = new string(chars, 0, i).Replace("\n", "\\N");
                string activeChar = chars[i].ToString().Replace("\n", "\\N");

                // TAGS:
                // {\r} - Reset revealed text to your default style (clean and sharp).
                // {\bord3\blur5\3c&HFFFFFF&} - Adds a 3px white border, then blurs it heavily.
                // This creates a "glow" halo around the active character only.
                line.Text = "{\\r}" + previousText +
                            "{\\bord3\\blur8\\3c&HFFFFFF&\\1c&HFFFFFF&}" + activeChar;

                result.Add(line);
            }
        }
        return result;
    }
}


}

