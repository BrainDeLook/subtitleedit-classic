using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.AutoTranslate;
using Nikse.SubtitleEdit.Core.Translate;
namespace Nikse.SubtitleEdit.Logic
{
 public static class ClassicSelectedTranslation
 {
  public static async Task<Subtitle> Translate(Subtitle source,Subtitle translations,IEnumerable<int> selection,IAutoTranslator translator,string from,string to,CancellationToken cancel,IProgress<int> progress=null)
  {
   var indices=selection.Distinct().OrderBy(i=>i).ToArray();
   if(source.Paragraphs.Count!=translations.Paragraphs.Count)throw new ArgumentException("Source and translation rows no longer match.");
   if(indices.Any(i=>i<0 || i>=source.Paragraphs.Count))throw new ArgumentOutOfRangeException(nameof(selection));
   var result=new Subtitle(translations,false);var done=0;
   if(translator is ClassicNewTranslator advanced && advanced.IsAdvanced)
   {
    var selected=new HashSet<int>(indices);
    for(int offset=0;offset<indices.Length;offset+=advanced.AdvancedBatchSize)
    {
     cancel.ThrowIfCancellationRequested();var batch=indices.Skip(offset).Take(advanced.AdvancedBatchSize).ToArray();
     var nearby=batch.SelectMany(i=>Enumerable.Range(Math.Max(0,i-2),Math.Min(source.Paragraphs.Count-1,i+2)-Math.Max(0,i-2)+1)).Distinct().OrderBy(i=>i).Where(i=>!selected.Contains(i)).Take(32).Select(i=>source.Paragraphs[i].Text).ToArray();
     var output=await advanced.TranslateBatchAsync(batch.Select(i=>source.Paragraphs[i].Text).ToArray(),from,to,cancel,nearby).ConfigureAwait(false);
     for(int i=0;i<batch.Length;i++)result.Paragraphs[batch[i]].Text=output[i].Replace("\n",Environment.NewLine);
     done+=batch.Length;progress?.Report(done);
    }
   }
   else foreach(var index in indices)
   {
    cancel.ThrowIfCancellationRequested();var original=source.Paragraphs[index].Text;if(TranslationMusic.IsKeptUntranslated(original)){result.Paragraphs[index].Text=original;progress?.Report(++done);continue;}var formatting=new Formatting();var input=formatting.SetTagsAndReturnTrimmed(original,from);
    var translated=await translator.Translate(input,from,to,cancel).ConfigureAwait(false);translated=translated.Replace("<br />",Environment.NewLine).Replace("<br/>",Environment.NewLine);var text=formatting.ReAddFormatting(translated);
    if(text.StartsWith("- ",StringComparison.Ordinal) && !original.Contains('-'))text=text.TrimStart('-').Trim();
    result.Paragraphs[index].Text=translator is ClassicNewTranslator classic && classic.KeepLineBreaks ? text : Utilities.AutoBreakLine(text);progress?.Report(++done);
   }
   cancel.ThrowIfCancellationRequested();return result;
  }
 }
}
