using System.Drawing;
using Nikse.SubtitleEdit.Core.Common;



using System;
using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Logic.ClassicEffects {

public class AdvancedEffectNeonBurst : IAdvancedEffectDisplay
{
    public string Name => Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Неоновая вспышка","Neon burst");
    public string Description => Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.L("Энергичные субтитры в стиле Shorts с неоновым свечением и анимацией \"выскакивания\"","High-energy 'Shorts' style captions with neon bloom glow and scale-in 'pop' animation");
    public bool UsesAudio => false;

    public override string ToString() => Name;

    public List<SubtitleLineViewModel> ApplyEffect(string header, List<SubtitleLineViewModel> subtitles, int width, int height, WavePeakData wavePeaks)
    {
        var result = new List<SubtitleLineViewModel>();
        if (subtitles.Count == 0)
        {
            return result;
        }

        Random rng = new Random(subtitles[0].Text.GetHashCode());

        foreach (var sub in subtitles)
        {
            // 1. SELECT NEON COLOR
            // Modern shorts use 'Electric Lime', 'Cyan', or 'Hot Pink'
            string[] neonColors = { "&H00FF00&", "&HFFFF00&", "&HFF00FF&", "&H00FFFF&" };
            string chosenColor = neonColors[rng.Next(neonColors.Length)];

            // The two events overlap in time at the same (style-derived) position, so they
            // must sit on different ASSA layers: same-layer unpositioned events trigger
            // libass collision avoidance, which would stack them vertically instead of
            // rendering the glow behind the core. Cloning from the source line keeps its
            // style, actor and margins.
            var cleanText = HtmlUtil.RemoveHtmlTags(sub.Text, true);

            // 2. CREATE THE GLOW LAYER (The 'Bloom')
            // A duplicate line that sits behind the text with a heavy blur
            var glowLayer = new SubtitleLineViewModel(sub, generateNewId: true);
            glowLayer.Layer = sub.Layer;
            // Deep neon glow: High blur, matching color, slightly transparent
            glowLayer.Text = "{\\bord5\\blur8\\shad0\\1c" + chosenColor + "\\3c" + chosenColor + "\\alpha&H60&" +
                             GetPopTags(0, 150) + "}" + cleanText;

            // 3. CREATE THE SHARP TOP LAYER (The 'Core')
            var coreLayer = new SubtitleLineViewModel(sub, generateNewId: true);
            coreLayer.Layer = sub.Layer + 1;
            // Sharp white core with a thin neon border
            coreLayer.Text = "{\\bord2\\blur0.5\\shad0\\1c&HFFFFFF&\\3c" + chosenColor +
                             GetPopTags(0, 150) + "}" + cleanText;

            result.Add(glowLayer);
            result.Add(coreLayer);
        }

        return result;
    }

    private static string GetPopTags(int startOffset, int duration)
    {
        // Starts at 125% size and shrinks down to 100% quickly
        return $"\\fscx125\\fscy125\\t({startOffset},{duration},\\fscx100\\fscy100)";
    }
}


}

