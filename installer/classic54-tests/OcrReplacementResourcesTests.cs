using System;
using System.Linq;
using System.Reflection;
using Nikse.SubtitleEdit.Logic.VideoOcr;
namespace Classic54Tests
{
 public class OcrReplacementResourcesTests
 {
  [Fact] public void EveryPinnedLanguageLoadsWithoutReplacingUserFiles()
  {
   var assembly=typeof(ClassicVideoOcrRules).Assembly;
   var resources=assembly.GetManifestResourceNames().Where(n=>n.StartsWith("ClassicVideoOcrRules.")).ToList();Assert.Equal(19,resources.Count);
   foreach(var name in resources)
   {
    var language=name.Split('.')[1];using(var rules=new ClassicVideoOcrRules(language)){Assert.NotNull(rules.ReplaceList);Assert.True(string.IsNullOrEmpty(rules.ReplaceList.ErrorMessage),language+": "+rules.ReplaceList.ErrorMessage);}
   }
   using(var unknown=new ClassicVideoOcrRules("unknown"))Assert.Null(unknown.ReplaceList);
  }
 }
}
