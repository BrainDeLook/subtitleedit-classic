using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Ocr;


namespace Classic54Tests {

public class SplitterCrumbLineTests
{
    [Theory]
    [InlineData("(heavy breathing)")]
    [InlineData("(gasps)")]
    public void OneLargeLine_IsNotCutIntoABogusSecondLine(string text)
    {
        // Large text with tall parentheses is over 2.2x the minimum line height, so it went
        // through the up/down splitter, which shaved the tip off a parenthesis into a line of
        // its own (read as "*").
        using var bmp = NOcrTrainer.RenderCharacterImage(text, FontFamily.GenericSansSerif.Name, 72, true, false);
        var parent = new NikseBitmap(bmp!);
        parent.MakeTwoColor(200);
        parent.CropTop(0, Color.Transparent);

        var items = NikseBitmapImageSplitter.SplitBitmapToLettersNew(parent, 1, false, true, 25, true);

        Assert.DoesNotContain(items, i => i.SpecialCharacter == Environment.NewLine);
    }
}

}
