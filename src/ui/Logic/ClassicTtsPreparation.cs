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
                var a=HtmlUtil.RemoveHtmlTags(current.Text,true).Trim();var b=HtmlUtil.RemoveHtmlTags(next.Text,true).Trim();
                // Stable v5.3.0 TTS merge defaults: 500 ms / 500 characters.
                if(a.Length+b.Length>=500 || next.StartTime.TotalMilliseconds-current.EndTime.TotalMilliseconds>=500 || next.StartTime.TotalMilliseconds<current.EndTime.TotalMilliseconds)continue;
                if(current.Actor!=next.Actor || SpeechToTextQualityReport.IsNonSpeechLine(next.Text))continue;
                if(Dash(a) || Dash(b) || a.EndsWith("♪") || a.EndsWith("♫") || b.StartsWith("♪") || b.StartsWith("♫"))continue;
                if(!(a.EndsWith("...") || a.EndsWith("…")) && (a.HasSentenceEnding(language) || a.EndsWith("！") || a.EndsWith("．") || a.EndsWith("｡")))continue;
                result.Add(new ClassicTtsSuggestion{Index=i,Preview=current.Text+" → "+next.Text});
            }
            return result;
        }
        private static bool Dash(string text)=>text.Length>0 && "-‐–—".IndexOf(text[0])>=0;
        public static Subtitle Apply(Subtitle original,ISet<int> merge,string language)
        {
            var result=new Subtitle(original);result.Paragraphs.Clear();
            for(int i=0;i<original.Paragraphs.Count;i++)
            {
                var paragraph=new Paragraph(original.Paragraphs[i],false);
                while(i+1<original.Paragraphs.Count && merge.Contains(i))
                {
                    var next=original.Paragraphs[++i];
                    paragraph.Text=Utilities.AutoBreakLine(paragraph.Text.TrimEnd()+" "+next.Text.TrimStart(),language);
                    paragraph.EndTime.TotalMilliseconds=next.EndTime.TotalMilliseconds;
                }
                result.Paragraphs.Add(paragraph);
            }
            return result;
        }
    }
}
