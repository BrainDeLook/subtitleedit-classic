using System;
using System.Collections.Generic;
using Nikse.SubtitleEdit.Logic.Ocr;
namespace Classic54Tests
{
 public class ImageMatcherRepairs54Tests
 {
  private static readonly HashSet<string> Words = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
  { "he's", "isn't", "it's", "you'll", "will", "please", "little", "with", "doing", "I", "and", "or", "he", "she", "Hoor", "je", "Vijf", "van", "of", "we", "Get", "verdikkeme" };
  private static OcrImageMatcherRepairs Engine() => new OcrImageMatcherRepairs(Words.Contains);
  [Theory]
  [InlineData("<i>He#s doing it.</i>", "<i>He's doing it.</i>")]
  [InlineData("He isn#t winning.", "He isn't winning.")]
  [InlineData("You#// do it.", "You'll do it.")]
  [InlineData("p/ease /itt/e wi/I w/th do/ng", "please little will with doing")]
  [InlineData("/ can and / will", "I can and I will")]
  [InlineData("20. 000 and 19. 000", "20.000 and 19.000")]
  [InlineData("beginnen. \"", "beginnen.\"")]
  public void ImageMatcherMistakesUseDictionaryEvidence(string input,string expected) => Assert.Equal(expected,Engine().RepairLine(input,false));
  [Theory]
  [InlineData("C# is a language")]
  [InlineData("the #1 fan")]
  [InlineData("A#B is no word")]
  [InlineData("and/or he/she AC/DC")]
  [InlineData("1 / 2")]
  [InlineData("PILOTEN / OMGEKOMEN")]
  [InlineData("<i>please</i>")]
  [InlineData("http://example.com")]
  [InlineData("1940. 300")]
  public void RealSeparatorsAndMarkupStayUntouched(string input) => Assert.Equal(input,Engine().RepairLine(input,false));
  [Fact]public void FrenchGuillemetKeepsItsSpace() { Assert.Equal("Il est parti. »",Engine().RepairLine("Il est parti. »",true));Assert.Equal("« Il est parti. »",Engine().RepairLine("« Il est parti. »",false)); }
  [Theory]
  [InlineData("CowsIip","Cowslip")]
  [InlineData("controI","control")]
  [InlineData("FIight","Flight")]
  [InlineData("JUNl","JUNI")]
  [InlineData("lFF","IFF")]
  [InlineData("AUSTRALlERS","AUSTRALIERS")]
  [InlineData("McIntyre","McIntyre")]
  [InlineData("MacIsaac","MacIsaac")]
  [InlineData("Il","Il")]
  [InlineData("Hoorje","Hoor je")]
  [InlineData("Vijfvan","Vijf van")]
  [InlineData("ofwe","of we")]
  [InlineData("Getverdikkeme","Getverdikkeme")]
  public void BarCaseAndOverhangRespectKnownWords(string input,string expected) => Assert.Equal(expected,Engine().RepairWord(input));
  [Fact]public void CorrectDictionaryWordWinsOverShapeGuess() => Assert.Equal("CowsIip",new OcrImageMatcherRepairs(w=>w=="CowsIip").RepairWord("CowsIip"));
  [Theory]
  [InlineData("voor. . .", "voor...")]
  [InlineData("<i>. . .en</i>", "<i>...en</i>")]
  [InlineData("a . ..", "a . ..")]
  public void WideEllipsesKeepTagsAndAvoidTwoDotForms(string input,string expected) => Assert.Equal(expected,OcrImageMatcherRepairs.NormalizeSpacedEllipses(input));
 }
}
