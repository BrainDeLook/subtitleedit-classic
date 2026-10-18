using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicTtsSuggestion
    {
        public int Index { get; set; }
        public bool Noise { get; set; }
        public string Preview { get; set; }
    }
    public static class ClassicTtsPreparation
    {
        public static string Key(Paragraph p)=>p.StartTime.TotalMilliseconds+"|"+p.EndTime.TotalMilliseconds+"|"+p.Text;
        public static List<ClassicTtsSuggestion> Detect(Subtitle subtitle,string language)
        {
            var result=new List<ClassicTtsSuggestion>();
            for(int i=0;i<subtitle.Paragraphs.Count;i++)
            {
                var current=subtitle.Paragraphs[i];
                if(current.IsComment || SpeechToTextQualityReport.IsNonSpeechLine(current.Text) || string.IsNullOrWhiteSpace(HtmlUtil.RemoveHtmlTags(current.Text,true)))
                {result.Add(new ClassicTtsSuggestion{Index=i,Noise=true,Preview=current.Text});continue;}
                if(i+1>=subtitle.Paragraphs.Count)continue;var next=subtitle.Paragraphs[i+1];
                // Share the editing/batch sentence rules, retaining the stable speech defaults.
                if(SpeechToTextQualityReport.IsNonSpeechLine(next.Text) || !ClassicTextTools.CanMerge(current,next,language,500,500))continue;
                result.Add(new ClassicTtsSuggestion{Index=i,Preview=current.Text+" → "+next.Text});
            }
            return result;
        }
        public static Subtitle Apply(Subtitle original,ISet<int> merge,string language) => ClassicTextTools.Merge(original,merge,language);
    }
}
