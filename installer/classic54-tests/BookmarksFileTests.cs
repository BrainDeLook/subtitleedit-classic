using System;
using System.Collections.Generic;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
namespace Classic54Tests
{
 public class BookmarksFileTests
 {
  private static List<Paragraph> Lines()=>new List<Paragraph>{new Paragraph("One",1000,2000){Number=1},new Paragraph("Two\nlines",3456,4000){Number=2},new Paragraph("Three",5000,6000){Number=3}};
  [Fact]public void ExportImportPreservesUnicodeAndEmptyBookmarks()
  {
   var source=Lines();source[1].Bookmark="Проверить"+Environment.NewLine+"перевод";source[2].Bookmark="";var text=ClassicBookmarksFile.ToText(source);var target=Lines();Assert.Equal(2,ClassicBookmarksFile.Apply("\ufeff"+text,target));Assert.Null(target[0].Bookmark);Assert.Equal(source[1].Bookmark,target[1].Bookmark);Assert.Equal("",target[2].Bookmark);Assert.Equal("Two\nlines",target[1].Text);
  }
  [Fact]public void TimeMatchWinsOverNumberAndOtherBookmarksRemain()
  {
   var target=Lines();target[1].Bookmark="Existing";Assert.Equal(1,ClassicBookmarksFile.Apply("#1\t00:00:05,000\tMoved\tThree",target));Assert.Null(target[0].Bookmark);Assert.Equal("Existing",target[1].Bookmark);Assert.Equal("Moved",target[2].Bookmark);
  }
  [Fact]public void NumberIsFallbackWhenTimeDoesNotMatch()
  {
   var target=Lines();Assert.Equal(1,ClassicBookmarksFile.Apply("#3\t01:00:00,000\tLate\tThree",target));Assert.Equal("Late",target[2].Bookmark);
  }
  [Theory][InlineData("some header\n#x\t00:00:01,000\tNo")][InlineData("#1\tbroken\tNo")][InlineData("#1\t999999999999999999999:00:00,000\tNo")][InlineData("#1\t00:99:01,000\tNo")]
  public void MalformedRowsAreSkipped(string text)
  {
   var target=Lines();Assert.Equal(0,ClassicBookmarksFile.Apply(text,target));Assert.All(target,p=>Assert.Null(p.Bookmark));
  }
 }
}
