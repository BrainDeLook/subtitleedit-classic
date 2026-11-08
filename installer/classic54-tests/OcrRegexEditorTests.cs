using System;
using System.IO;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Dictionaries;
namespace Classic54Tests
{
 public class OcrRegexEditorTests:IDisposable
 {
  private readonly string _folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ocr-regex-"+Guid.NewGuid().ToString("N"));
  private readonly string _file;
  public OcrRegexEditorTests(){Directory.CreateDirectory(_folder);_file=Path.Combine(_folder,"eng_OCRFixReplaceList.xml");File.WriteAllText(_file,"<OCRFixReplaceList><RegularExpressions><RegEx find=\"teh\" replaceWith=\"the\" /></RegularExpressions></OCRFixReplaceList>");}
  public void Dispose(){Directory.Delete(_folder,true);}
  private static string Fix(OcrFixReplaceList list,string text){var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph(text,0,1000));return list.FixOcrErrorViaLineReplaceList(text,sub,0);}
  [Fact]public void BuiltInRuleCanBeDisabledAndRestoredWithoutEditingBaseFile()
  {
   var original=File.ReadAllText(_file);var list=new OcrFixReplaceList(_file);Assert.Equal("the",Fix(list,"teh"));Assert.True(list.SetRegexRuleEnabled("teh","the",false));Assert.Equal("teh",Fix(new OcrFixReplaceList(_file),"teh"));Assert.False(Assert.Single(list.GetRegexRules()).IsEnabled);Assert.False(list.RemoveUserRegexRule("teh"));Assert.Equal(original,File.ReadAllText(_file));Assert.True(list.SetRegexRuleEnabled("teh","the",true));Assert.Equal("the",Fix(new OcrFixReplaceList(_file),"teh"));
  }
  [Fact]public void UserRuleCanBeAddedEditedAndRemovedAcrossReloads()
  {
   var list=new OcrFixReplaceList(_file);Assert.True(list.AddRegexRule("colour","color"));Assert.False(list.AddRegexRule("[","invalid"));Assert.False(list.AddRegexRule("colour","duplicate"));Assert.True(list.UpdateUserRegexRule("colour","colou?r","color"));var reloaded=new OcrFixReplaceList(_file);Assert.Equal("color",Fix(reloaded,"colour"));Assert.True(reloaded.GetRegexRules().Single(r=>r.Find=="colou?r").IsUserRule);Assert.True(reloaded.RemoveUserRegexRule("colou?r"));Assert.False(new OcrFixReplaceList(_file).GetRegexRules().Any(r=>r.Find=="colou?r"));
  }
  [Fact]public void PathologicalUserRegexDoesNotHangOcr()
  {
   var list=new OcrFixReplaceList(_file);Assert.True(list.AddRegexRule("(a+)+$","bad"));var input=new string('a',10000)+"!";Assert.Equal(input,Fix(list,input));Assert.Contains("timed out",list.ErrorMessage);Assert.Equal("aaaa",Fix(list,"aaaa"));
  }
  [Fact]public void UppercaseCHexColorDoesNotEnterLetterGuesser()
  {
   var list=new OcrFixReplaceList(_file);Assert.Equal("#0C0d",list.FixCommonWordErrors("#0C0d"));
  }
 }
}
