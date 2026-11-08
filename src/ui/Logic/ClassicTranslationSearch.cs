using System;
using System.Text.RegularExpressions;
using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic
{
 public sealed class ClassicTranslationSearch
 {
  private readonly Regex _regex;private readonly string _replacement;private readonly bool _regexReplacement;
  public ClassicTranslationSearch(string find,string replacement,bool matchCase,bool regex)
  {
   if(string.IsNullOrEmpty(find))throw new ArgumentException("Enter search text.",nameof(find));
   _regex=new Regex(regex?find:Regex.Escape(find),RegexOptions.Multiline|RegexOptions.CultureInvariant|(matchCase?RegexOptions.None:RegexOptions.IgnoreCase),TimeSpan.FromMilliseconds(100));_replacement=replacement;_regexReplacement=regex;
  }
  public int FindNext(Subtitle subtitle,int start)
  {
   if(subtitle.Paragraphs.Count==0)return -1;var first=Math.Max(0,start)%subtitle.Paragraphs.Count;
   for(int n=0;n<subtitle.Paragraphs.Count;n++){var index=(first+n)%subtitle.Paragraphs.Count;if(_regex.IsMatch(subtitle.Paragraphs[index].Text))return index;}return -1;
  }
  public Subtitle Replace(Subtitle subtitle,int? onlyIndex,bool oneOccurrence,out int count)
  {
   if(onlyIndex.HasValue && (onlyIndex.Value<0 || onlyIndex.Value>=subtitle.Paragraphs.Count))throw new ArgumentOutOfRangeException(nameof(onlyIndex));
   var result=new Subtitle(subtitle,false);var total=0;
   for(int i=0;i<result.Paragraphs.Count;i++)
   {
    if(onlyIndex.HasValue && i!=onlyIndex.Value)continue;
    result.Paragraphs[i].Text=_regex.Replace(result.Paragraphs[i].Text,match=>{total++;return _regexReplacement?match.Result(_replacement):_replacement;},oneOccurrence?1:int.MaxValue);
   }
   count=total;return result;
  }
 }
}
