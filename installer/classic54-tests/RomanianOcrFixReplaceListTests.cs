using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Nikse.SubtitleEdit.Core.Dictionaries;

namespace Classic54Tests
{

/// <summary>
/// Romanian is written with s/t comma below; the shipped "ron" replace list turns the cedilla
/// look-alikes (old code pages, OCR engines) into them, so the words match the spell check
/// dictionary.
/// </summary>
public class RomanianOcrFixReplaceListTests
{
    private static OcrFixReplaceList LoadShippedList()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

        Assert.NotNull(dir);
        return new OcrFixReplaceList(Path.Combine(dir!.FullName, "Dictionaries", "ron_OCRFixReplaceList.xml"));
    }

    [Theory]
    [InlineData("şi", "și")]
    [InlineData("viaţă", "viață")]
    [InlineData("Ştiu", "Știu")]
    [InlineData("Ţară", "Țară")]
    [InlineData("ŞTIINŢĂ", "ȘTIINȚĂ")]
    [InlineData("mașină", "mașină")]
    public void CedillaLetters_BecomeCommaBelow(string input, string expected)
    {
        Assert.Equal(expected, LoadShippedList().FixCommonWordErrors(input));
    }
}

}
