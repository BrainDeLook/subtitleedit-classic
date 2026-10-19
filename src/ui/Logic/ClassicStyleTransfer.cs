using System;
using System.Collections.Generic;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic
{
    public enum ClassicStyleConflict { Cancel, Overwrite, KeepBoth }
    public sealed class ClassicStyleAnswer
    {
        public ClassicStyleConflict Choice {get;set;}
        public bool ForAll {get;set;}
    }
    public sealed class ClassicStyleTransferResult
    {
        public List<SsaStyle> Styles {get;set;}
        public List<string> AffectedNames {get;set;}
    }
    public static class ClassicStyleTransfer
    {
        public static ClassicStyleTransferResult Plan(IEnumerable<SsaStyle> source,IEnumerable<SsaStyle> target,Func<string,bool,ClassicStyleAnswer> ask)
        {
            var incoming=source.Select(s=>new SsaStyle(s)).ToList();var result=target.Select(s=>new SsaStyle(s)).ToList();var names=new List<string>();
            var conflicts=incoming.Count(s=>result.Any(t=>t.Name.Equals(s.Name,StringComparison.OrdinalIgnoreCase)));ClassicStyleAnswer all=null;
            foreach(var style in incoming)
            {
                if(!ClassicCuePickerData.ValidName(style.Name))throw new ArgumentException("Invalid style name: "+style.Name);
                var index=result.FindIndex(t=>t.Name.Equals(style.Name,StringComparison.OrdinalIgnoreCase));
                if(index>=0)
                {
                    var answer=all??ask(style.Name,conflicts>1);
                    if(answer==null || answer.Choice==ClassicStyleConflict.Cancel)return null;
                    if(answer.ForAll)all=answer;
                    if(answer.Choice==ClassicStyleConflict.Overwrite)
                    {style.Name=result[index].Name;style.RawLine=string.Empty;result[index]=style;names.Add(style.Name);continue;}
                    var original=style.Name;var suffix=2;
                    do{style.Name=original+"_"+suffix++;}while(result.Any(t=>t.Name.Equals(style.Name,StringComparison.OrdinalIgnoreCase)));
                }
                style.RawLine=string.Empty;result.Add(style);names.Add(style.Name);
            }
            return new ClassicStyleTransferResult{Styles=result,AffectedNames=names};
        }
        public static SsaStyle Convert(SsaStyle source,bool fromSsa,bool toSsa)
        {
            var style=new SsaStyle(source);if(fromSsa==toSsa)return style;
            if(toSsa)style.Tertiary=style.Outline;else style.Outline=style.Tertiary;
            var from=fromSsa?new[]{"5","6","7","9","10","11"}:new[]{"7","8","9","4","5","6"};
            var to=toSsa?new[]{"5","6","7","9","10","11"}:new[]{"7","8","9","4","5","6"};
            var index=Array.IndexOf(from,style.Alignment);if(index>=0)style.Alignment=to[index];style.RawLine=string.Empty;return style;
        }
        public static List<int> Order(int count,IEnumerable<int> selected,string move)
        {
            var order=Enumerable.Range(0,count).ToList();var chosen=new HashSet<int>(selected.Where(i=>i>=0 && i<count));
            if(move=="top")return order.Where(chosen.Contains).Concat(order.Where(i=>!chosen.Contains(i))).ToList();
            if(move=="bottom")return order.Where(i=>!chosen.Contains(i)).Concat(order.Where(chosen.Contains)).ToList();
            if(move=="up")for(var i=1;i<count;i++){if(chosen.Contains(order[i]) && !chosen.Contains(order[i-1])){var item=order[i];order[i]=order[i-1];order[i-1]=item;}}
            else if(move=="down")for(var i=count-2;i>=0;i--){if(chosen.Contains(order[i]) && !chosen.Contains(order[i+1])){var item=order[i];order[i]=order[i+1];order[i+1]=item;}}
            return order;
        }
    }
}

