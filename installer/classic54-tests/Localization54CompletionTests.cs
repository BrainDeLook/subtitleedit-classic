using System;
using System.Linq;
using System.Collections.Generic;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
namespace Classic54Tests
{
 public class Localization54CompletionTests
 {
  [Theory]
  [InlineData("de-DE","Enthält Fehler (in der Liste hervorgehoben)")]
  [InlineData("fr-FR","Contient des erreurs (surlignées dans la liste)")]
  [InlineData("hr-HR","Ima greške (istaknuto na popisu)")]
  [InlineData("ro-RO","Are erori (evidențiate în listă)")]
  [InlineData("ja-JP","エラーあり (リストで強調表示)")]
  public void ReviewedFeatureAliasUsesPinnedTranslation(string culture,string expected)=>Assert.Equal(expected,ClassicLocalization.ForCulture(culture,"С ошибками","Has errors"));
  [Fact]public void PortugueseVariantsAndChineseAliasesRemainDistinct()
  {
   Assert.Equal("Tem erros (realçados na lista)",ClassicLocalization.ForCulture("pt-PT","RU","Has errors"));
   Assert.Equal("Tem erros (destacados na lista)",ClassicLocalization.ForCulture("pt-BR","RU","Has errors"));
   Assert.Equal("卸载",ClassicLocalization.ForCulture("zh-CN","RU","Uninstall"));
   Assert.Equal("解除安裝",ClassicLocalization.ForCulture("zh-TW","RU","Uninstall"));
   Assert.Equal("アンインストール",ClassicLocalization.ForCulture("jp-JP","RU","Uninstall"));
  }
  [Fact]public void RussianEnglishAndUnmatchedForkCopyArePreservedExactly()
  {
   Assert.Equal("  Наш русский текст… ",ClassicLocalization.ForCulture("ru-RU","  Наш русский текст… ","English"));
   Assert.Equal("  Exact English… ",ClassicLocalization.ForCulture("en-US","RU","  Exact English… "));
   Assert.Equal("Fork-only phrase 42",ClassicLocalization.ForCulture("de-DE","RU","Fork-only phrase 42"));
   Assert.Equal("Fork-only phrase 42",ClassicLocalization.ForCulture("unknown","RU","Fork-only phrase 42"));
  }
  [Fact]public void AllPinnedLocalesLoadAndNewControlsAreTranslated()
  {
   var cultures=ClassicLocalization.SupportedCultures.ToArray();Assert.Equal(36,cultures.Length);
   foreach(var culture in cultures)
   {
    foreach(var label in new[]{"Has errors","Uninstall","From image…","Keep music/lyric lines untranslated","Move selected and following cues one frame back","Move all cues one frame forward","Refresh model list","Second subtitle font"})
    {
     var text=ClassicLocalization.ForCulture(culture,"Проверка",label);Assert.False(string.IsNullOrWhiteSpace(text),culture+" "+label);
     if(!culture.StartsWith("en") && !culture.StartsWith("ru"))Assert.NotEqual(label,text);
    }
   }
  }
  [Fact]public void MnemonicsEllipsisAndConcatenationSpacingAreAdapted()
  {
   Assert.Equal("Deinstallieren…",ClassicLocalization.ForCulture("de-DE","RU","Uninstall…"));
   Assert.Equal("  Deinstallieren: ",ClassicLocalization.ForCulture("de-DE","RU","  Uninstall: "));
   Assert.Equal("Annuler",ClassicLocalization.ForCulture("fr-FR","RU","Cancel"));
  }
  [Fact]public void NullLanguageDefaultsAtSharedBoundaryIncludingSpeechHelper()
  {
   var previous=Configuration.Settings.General.Language;
   try
   {
    Configuration.Settings.General.Language=null;
    Assert.Equal("Uninstall",ClassicLocalization.Text("Удалить","Uninstall"));
    var helper=typeof(Nikse.SubtitleEdit.Forms.AudioToText.ClassicSpeechSettings).GetMethod("L",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
    Assert.Equal("English label",(string)helper.Invoke(null,new object[]{"Русская надпись","English label"}));
   }finally{Configuration.Settings.General.Language=previous;}
  }
  [Fact]public void ExistingCentralHelpersUseCurrentLanguageWithoutRestart()
  {
   var previous=Configuration.Settings.General.Language;
   try
   {
    Configuration.Settings.General.Language="de-DE";Assert.Equal("Deinstallieren",ClassicAssistedText.L("Удалить","Uninstall"));Assert.Equal("Deinstallieren",ClassicTextTools.L("Удалить","Uninstall"));
    Configuration.Settings.General.Language="fr-FR";Assert.Equal("Désinstaller",ClassicAssistedText.L("Удалить","Uninstall"));
    Configuration.Settings.General.Language="ru-RU";Assert.Equal("Удалить",ClassicAssistedText.L("Удалить","Uninstall"));
   }finally{Configuration.Settings.General.Language=previous;}
  }
 }
}
