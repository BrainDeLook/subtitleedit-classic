using System;
using System.IO;
using System.Text.RegularExpressions;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.AutoTranslate;
using Nikse.SubtitleEdit.Logic;
namespace Classic54Tests
{
 public class TranslationSearch54Tests
 {
  private static Subtitle Lines(){var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Hello hello 12",1000,2000){Actor="Alice",Extra="Sign",Layer=3,Forced=true});sub.Paragraphs.Add(new Paragraph("Other 34",3000,4000));return sub;}
  [Fact]public void PlainReplacementKeepsDollarLiteralAndMetadata()
  {
   var input=Lines();var result=new ClassicTranslationSearch("hello","$1",false,false).Replace(input,null,false,out var count);Assert.Equal(2,count);Assert.Equal("$1 $1 12",result.Paragraphs[0].Text);Assert.Equal("Hello hello 12",input.Paragraphs[0].Text);Assert.Equal("Alice",result.Paragraphs[0].Actor);Assert.Equal("Sign",result.Paragraphs[0].Extra);Assert.Equal(3,result.Paragraphs[0].Layer);Assert.True(result.Paragraphs[0].Forced);
  }
  [Fact]public void RegexReplacementHonorsCaptureGroups()
  {
   var result=new ClassicTranslationSearch(@"(\d+)","[$1]",true,true).Replace(Lines(),null,false,out var count);Assert.Equal(2,count);Assert.Equal("Other [34]",result.Paragraphs[1].Text);
  }
  [Fact]public void ReplaceOneDoesNotTouchOtherOccurrences()
  {
   var result=new ClassicTranslationSearch("hello","one",false,false).Replace(Lines(),0,true,out var count);Assert.Equal(1,count);Assert.Equal("one hello 12",result.Paragraphs[0].Text);Assert.Equal("Other 34",result.Paragraphs[1].Text);
  }
  [Fact]public void FindWrapsAndInvalidRegexIsRejected()
  {
   Assert.Equal(0,new ClassicTranslationSearch("Hello","",true,false).FindNext(Lines(),1));Assert.Throws<ArgumentException>(()=>new ClassicTranslationSearch("[","",true,true));
  }
  [Theory][InlineData(null)][InlineData("")][InlineData("global")]
  public void GlobalRegionRetainsConfiguredEndpoint(string region){Assert.Equal("https://custom.invalid/token",MicrosoftTranslator.GetTokenEndpoint("https://custom.invalid/token",region));}
  [Fact]public void AzureRegionBuildsRegionalHostAndRejectsHostInjection()
  {
   Assert.Equal("https://westus2.api.cognitive.microsoft.com/sts/v1.0/issueToken",MicrosoftTranslator.GetTokenEndpoint("https://unused.invalid/","West US 2"));Assert.Throws<ArgumentException>(()=>MicrosoftTranslator.GetTokenEndpoint("https://unused.invalid/","evil.invalid/path"));
  }
 }
}
