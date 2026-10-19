using System;
using System.Collections.Generic;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicCueChoice
    {
        public string Name { get; set; }
        public int Count { get; set; }
        public int Position { get; set; }
        public string Shortcut { get; set; }
        public bool Current { get; set; }
        public bool IsNew { get; set; }
        public SsaStyle Style { get; set; }
    }
    // Session order is independent of which actors still occur after an edit or Undo.
    public sealed class ClassicActorOrder
    {
        private Subtitle _document;
        private List<string> _names=new List<string>();
        public List<string> Get(Subtitle document)
        {
            var names=document.Paragraphs.Select(p=>p.Actor).Where(n=>!string.IsNullOrEmpty(n)).Distinct().ToList();
            if(!ReferenceEquals(_document,document))
            { _document=document;_names=names.OrderBy(n=>n,StringComparer.CurrentCulture).ToList(); }
            else foreach(var name in names)if(!_names.Contains(name))_names.Add(name);
            return new List<string>(_names);
        }
        public void Set(Subtitle document,IEnumerable<string> names)
        {Get(document);_names=names.Where(n=>!string.IsNullOrEmpty(n)).Distinct().Concat(_names).Distinct().ToList();}
    }
    public static class ClassicCuePickerData
    {
        public static bool ValidName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length<=200 && !name.Any(c=>c==',' || char.IsControl(c));
        public static List<ClassicCueChoice> Filter(List<ClassicCueChoice> items,string filter,bool style)
        {
            filter=(filter??string.Empty).Trim();
            var result=items.Where(i=>filter.Length==0 || i.Name.IndexOf(filter,StringComparison.CurrentCultureIgnoreCase)>=0).ToList();
            if(ValidName(filter) && !items.Any(i=>string.Equals(i.Name,filter,style?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)))
                result.Add(new ClassicCueChoice{Name=filter,IsNew=true,Position=-1,Style=style?new SsaStyle{Name=filter}:null});
            return result;
        }
        public static List<ClassicCueChoice> Actors(Subtitle document,List<string> names,string current,Func<int,string> key)
        {
            var counts=document.Paragraphs.Where(p=>!string.IsNullOrEmpty(p.Actor)).GroupBy(p=>p.Actor).ToDictionary(g=>g.Key,g=>g.Count());
            return names.Select((name,index)=>new ClassicCueChoice{Name=name,Position=index,Count=counts.TryGetValue(name,out var count)?count:0,Current=name==current,Shortcut=key(index)}).ToList();
        }
        public static List<ClassicCueChoice> Styles(Subtitle document,IEnumerable<string> current,Func<int,string> key,bool ssa)
        {
            var header=string.IsNullOrWhiteSpace(document.Header)?(ssa?SubStationAlpha.DefaultHeader:AdvancedSubStationAlpha.DefaultHeader):document.Header;
            var counts=document.Paragraphs.GroupBy(p=>string.IsNullOrEmpty(p.Extra)?"Default":p.Extra).ToDictionary(g=>g.Key,g=>g.Count());
            return AdvancedSubStationAlpha.GetSsaStylesFromHeader(header).Select((s,index)=>new ClassicCueChoice{Name=s.Name,Style=s,Position=index,Count=counts.TryGetValue(s.Name,out var count)?count:0,Current=current.Contains(s.Name),Shortcut=key(index)}).ToList();
        }
        public static string AddStyle(string header,string name,bool ssa)
        {
            if(!ValidName(name))throw new ArgumentException("Invalid style name");
            if(string.IsNullOrWhiteSpace(header))header=ssa?SubStationAlpha.DefaultHeader:AdvancedSubStationAlpha.DefaultHeader;
            if(AdvancedSubStationAlpha.GetStylesFromHeader(header).Any(n=>n.Equals(name,StringComparison.OrdinalIgnoreCase)))return header;
            var lines=header.SplitToLines().ToList();var section=lines.FindIndex(l=>l.Trim().Equals(ssa?"[V4 Styles]":"[V4+ Styles]",StringComparison.OrdinalIgnoreCase));
            if(section<0)throw new ArgumentException("Subtitle style section is missing");
            var end=lines.FindIndex(section+1,l=>l.TrimStart().StartsWith("[",StringComparison.Ordinal));if(end<0)end=lines.Count;
            var format=lines.Skip(section+1).Take(end-section-1).FirstOrDefault(l=>l.TrimStart().StartsWith("Format:",StringComparison.OrdinalIgnoreCase));
            if(format==null)throw new ArgumentException("Subtitle style format is missing");
            var style=new SsaStyle{Name=name};lines.Insert(end,ssa?style.ToRawSsa(format):style.ToRawAss(format));return string.Join(Environment.NewLine,lines);
        }
    }
}
