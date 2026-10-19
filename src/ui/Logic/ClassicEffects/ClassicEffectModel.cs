using System;
using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic.ClassicEffects
{
    // Adapter only: effect algorithms remain those of the pinned stable 5.3 source.
    public sealed class SubtitleLineViewModel
    {
        private readonly Paragraph _paragraph;
        public string Text {get;set;}=string.Empty;
        public TimeSpan StartTime {get;set;}
        public TimeSpan EndTime {get;set;}
        public TimeSpan Duration=>EndTime-StartTime;
        public int Layer {get;set;}
        public SubtitleLineViewModel(){_paragraph=new Paragraph();}
        public SubtitleLineViewModel(Paragraph paragraph)
        {_paragraph=new Paragraph(paragraph,false);Text=paragraph.Text;StartTime=TimeSpan.FromMilliseconds(paragraph.StartTime.TotalMilliseconds);EndTime=TimeSpan.FromMilliseconds(paragraph.EndTime.TotalMilliseconds);Layer=paragraph.Layer;}
        public SubtitleLineViewModel(SubtitleLineViewModel source,bool generateNewId)
        {_paragraph=new Paragraph(source._paragraph,generateNewId);Text=source.Text;StartTime=source.StartTime;EndTime=source.EndTime;Layer=source.Layer;}
        public Paragraph ToParagraph()=>new Paragraph(_paragraph,false){Text=Text,StartTime=new TimeCode(StartTime.TotalMilliseconds),EndTime=new TimeCode(EndTime.TotalMilliseconds),Layer=Layer};
    }
    internal static class EffectMath
    {
        public static int Clamp(int n,int min,int max)=>Math.Max(min,Math.Min(max,n));
        public static double Clamp(double n,double min,double max)=>Math.Max(min,Math.Min(max,n));
    }
}

