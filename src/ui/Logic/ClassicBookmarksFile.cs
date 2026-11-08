using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic
{
 public static class ClassicBookmarksFile
 {
  private static readonly Regex Timestamp=new Regex(@"^\d{1,6}:([0-5]\d):([0-5]\d)[,.]\d{3}$",RegexOptions.Compiled);
  private static string Encode(string text)=>(text??"").Replace("\t"," ").Replace("\r\n","<br />").Replace("\n","<br />");
  public static string ToText(IEnumerable<Paragraph> lines)
  {
   var output=new StringBuilder();
   foreach(var p in lines.Where(p=>p.Bookmark!=null))output.Append('#').Append(p.Number).Append('\t').Append(new TimeCode(p.StartTime.TotalMilliseconds).ToString()).Append('\t').Append(Encode(p.Bookmark)).Append('\t').AppendLine(Encode(p.Text));
   return output.ToString();
  }
  public static int Apply(string text,IList<Paragraph> lines)
  {
   var byTime=lines.GroupBy(p=>Math.Round(p.StartTime.TotalMilliseconds)).ToDictionary(g=>g.Key,g=>g.First());
   var byNumber=lines.GroupBy(p=>p.Number).ToDictionary(g=>g.Key,g=>g.First());int count=0;
   foreach(var row in text.TrimStart('\ufeff').SplitToLines())
   {
    var parts=row.Split('\t');
    if(parts.Length<3 || !int.TryParse(parts[0].TrimStart('#').Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var number) || !Timestamp.IsMatch(parts[1].Trim()))continue;
    var time=Math.Round(TimeCode.ParseToMilliseconds(parts[1].Trim()));
    if(!byTime.TryGetValue(time,out var target) && !byNumber.TryGetValue(number,out target))continue;
    target.Bookmark=parts[2].Replace("<br />",Environment.NewLine);count++;
   }
   return count;
  }
 }
}
