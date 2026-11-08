using System;
using System.IO;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
namespace Classic54Tests
{
 public class CustomErrorRulesTests
 {
  [Fact] public void InvalidSaveDoesNotReplaceValidRuleOrFile()
  {
   Assert.Equal(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar),Path.GetFullPath(Configuration.DataDirectory).TrimEnd(Path.DirectorySeparatorChar));
   ClassicCustomErrorRules.Save(new[]{new ClassicCustomErrorRule{Name="Latin warning",Pattern="bad",IgnoreCase=true}});
   var path=Path.Combine(Configuration.DataDirectory,"ClassicCustomErrorRules.json");var before=File.ReadAllText(path);
   Assert.Equal("Latin warning",ClassicCustomErrorRules.Match("BAD word"));
   Assert.Throws<ArgumentException>(()=>ClassicCustomErrorRules.Save(new[]{new ClassicCustomErrorRule{Name="Broken",Pattern="["}}));
   Assert.Equal(before,File.ReadAllText(path));Assert.Equal("Latin warning",ClassicCustomErrorRules.Match("bad"));
   var definitions=ClassicCustomErrorRules.Definitions();definitions[0].Enabled=false;
   Assert.Equal("Latin warning",ClassicCustomErrorRules.Match("bad"));
   ClassicCustomErrorRules.Save(new[]{new ClassicCustomErrorRule{Name="Disabled",Pattern="bad",Enabled=false}});Assert.Null(ClassicCustomErrorRules.Match("bad"));
   ClassicCustomErrorRules.Save(new ClassicCustomErrorRule[0]);
  }
  [Fact] public void PathologicalRegexIsDisabledAfterTimeout()
  {
   ClassicCustomErrorRules.Save(new[]{new ClassicCustomErrorRule{Name="Timeout",Pattern="(a+)+$"}});
   Assert.Null(ClassicCustomErrorRules.Match(new string('a',10000)+"!"));
   Assert.Contains("Timeout",ClassicCustomErrorRules.LoadError);Assert.Null(ClassicCustomErrorRules.Match("aaaa"));
   ClassicCustomErrorRules.Save(new ClassicCustomErrorRule[0]);
  }
 }
}
