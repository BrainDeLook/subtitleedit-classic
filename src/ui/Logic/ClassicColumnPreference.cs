using System;
using System.Collections.Generic;
using System.Linq;
using Nikse.SubtitleEdit.Controls;
using C = Nikse.SubtitleEdit.Controls.SubtitleListView.SubtitleColumn;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicColumnPreference
    {
        public string Key { get; set; }
        public bool Visible { get; set; }
        public ClassicColumnPreference Copy() => new ClassicColumnPreference { Key=Key,Visible=Visible };
        public static readonly C[] DefaultOrder = {C.Number,C.Start,C.End,C.Duration,C.Forced,C.Text,C.TextOriginal,C.Extra,C.Gap,C.Actor,C.Region,C.CharactersPerSeconds,C.WordsPerMinute,C.ShotIn,C.ShotOut,C.Layer,C.PixelWidth,C.Teletext,C.Network};
        public static bool CanToggle(C column) => column!=C.Number && column!=C.Text && column!=C.TextOriginal && column!=C.Network;
        public static bool TryColumn(string key,out C column) => Enum.TryParse(key,out column) && Enum.IsDefined(typeof(C),column);
        public static List<ClassicColumnPreference> Normalize(IEnumerable<ClassicColumnPreference> values,Func<C,bool> missing)
        {
            var result=new List<ClassicColumnPreference>();var seen=new HashSet<C>();
            if(values!=null) foreach(var value in values)
                if(value!=null && TryColumn(value.Key,out var column) && seen.Add(column))
                    result.Add(new ClassicColumnPreference {Key=column.ToString(),Visible=CanToggle(column)?value.Visible:true});
            foreach(var column in DefaultOrder) if(seen.Add(column))result.Add(new ClassicColumnPreference {Key=column.ToString(),Visible=CanToggle(column)?missing(column):true});
            return result;
        }
    }
}
