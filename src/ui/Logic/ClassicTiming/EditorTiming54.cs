using System;
using System.Collections.Generic;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic.ClassicTiming
{
 public static class EditorTiming54
 {
    public static List<int> MoveSameTimeStack(Subtitle subtitle,Paragraph before,Paragraph moved)
    {
        var changed=new List<int>();
        for(var i=0;i<subtitle.Paragraphs.Count;i++)
        {
            var p=subtitle.Paragraphs[i];if(ReferenceEquals(p,moved))continue;
            if(Math.Abs(p.StartTime.TotalMilliseconds-before.StartTime.TotalMilliseconds)<.01 && Math.Abs(p.EndTime.TotalMilliseconds-before.EndTime.TotalMilliseconds)<.01)
            {p.StartTime.TotalMilliseconds=moved.StartTime.TotalMilliseconds;p.EndTime.TotalMilliseconds=moved.EndTime.TotalMilliseconds;changed.Add(i);}
        }
        return changed;
    }
    public static bool MoveLines(Subtitle subtitle,IReadOnlyCollection<int> indices,double delta)
    {
        if(indices.Count==0)return false;
        delta=Math.Max(delta,-indices.Min(i=>subtitle.Paragraphs[i].StartTime.TotalMilliseconds));if(Math.Abs(delta)<.001)return false;
        foreach(var i in indices){subtitle.Paragraphs[i].StartTime.TotalMilliseconds+=delta;subtitle.Paragraphs[i].EndTime.TotalMilliseconds+=delta;}return true;
    }
    public static bool PushBoundary(Subtitle subtitle,int index,bool start,double step,int gap,int minimum,int maximum)
    {
        if(index<0 || index>=subtitle.Paragraphs.Count)return false;
        var cue=subtitle.Paragraphs[index];
        if(start)
        {
            var value=Math.Max(0,Math.Max(cue.StartTime.TotalMilliseconds-step,cue.EndTime.TotalMilliseconds-maximum));
            var previous=subtitle.GetParagraphOrDefault(index-1);
            if(previous!=null && previous.EndTime.TotalMilliseconds+gap>value)
            {
                value=Math.Max(value,previous.StartTime.TotalMilliseconds+Math.Min(minimum,previous.DurationTotalMilliseconds)+gap);
                if(value>=cue.StartTime.TotalMilliseconds)return false;
                previous.EndTime.TotalMilliseconds=value-gap;
            }
            if(value>=cue.StartTime.TotalMilliseconds)return false;cue.StartTime.TotalMilliseconds=value;
        }
        else
        {
            var value=Math.Min(cue.EndTime.TotalMilliseconds+step,cue.StartTime.TotalMilliseconds+maximum);
            var next=subtitle.GetParagraphOrDefault(index+1);
            if(next!=null && next.StartTime.TotalMilliseconds-gap<value)
            {
                value=Math.Min(value,next.EndTime.TotalMilliseconds-Math.Min(minimum,next.DurationTotalMilliseconds)-gap);
                if(value<=cue.EndTime.TotalMilliseconds)return false;
                next.StartTime.TotalMilliseconds=value+gap;
            }
            if(value<=cue.EndTime.TotalMilliseconds)return false;cue.EndTime.TotalMilliseconds=value;
        }
        return true;
    }
 }
}
