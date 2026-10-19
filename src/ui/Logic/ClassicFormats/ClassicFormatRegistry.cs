using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
namespace Nikse.SubtitleEdit.Logic.ClassicFormats
{
    public static class ClassicFormatRegistry
    {
        private static readonly object Gate=new object();
        private static bool _registered;
        public static List<SubtitleFormat> Added {get;private set;}=new List<SubtitleFormat>();
        public static IEnumerable<SubtitleFormat> Other() => SubtitleFormat.GetTextOtherFormats().Select(f=>Added.FirstOrDefault(n=>n.Name==f.Name)??f);
        public static IEnumerable<SubtitleFormat> Binary(bool forSave) => SubtitleFormat.GetBinaryFormats(forSave).Concat(forSave ? Enumerable.Empty<SubtitleFormat>() : Added.Where(f => !f.IsTextBased));
        public static void Register()
        {
            lock(Gate)
            {
                if(_registered)return;
                var current=SubtitleFormat.AllSubtitleFormats as IList<SubtitleFormat>;
                var defaultOrder=typeof(SubtitleFormat).GetField("_subtitleFormatsWithDefaultOrder",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null) as IList<SubtitleFormat>;
                if(current==null || defaultOrder==null)throw new InvalidOperationException("Classic subtitle format registry is unavailable.");
                Added=typeof(ClassicFormatRegistry).Assembly.GetTypes().Where(t=>!t.IsAbstract && typeof(ClassicSubtitleFormat).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes)!=null).Select(t=>(SubtitleFormat)Activator.CreateInstance(t)).OrderBy(f=>f is FinalCutProXml15?1:0).ThenByDescending(f=>f.Name,StringComparer.Ordinal).ToList();
                // Update both lists: legacy format-order changes rebuild the current list from the default list.
                foreach(var list in new[]{current,defaultOrder}.Distinct())
                {
                    foreach(var format in Added.Where(f=>!(f is TimedTextImage)))
                        for(int i=list.Count-1;i>=0;i--)if(list[i].Name==format.Name)list.RemoveAt(i);
                    var visible=Added.Where(f=>!(f is TimedTextImage)).ToList();for(int i=visible.Count-1;i>=0;i--)list.Insert(Math.Min(1,list.Count),visible[i]);
                }
                _registered=true;
            }
        }
    }
}
