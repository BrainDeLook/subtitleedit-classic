using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using SkiaSharp;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
namespace Nikse.SubtitleEdit.Logic.ClassicFormats
{
    internal static class ClassicFormatSupport
    {
        public static bool IsStlHeader(string header)=>header!=null && header.Length==1024 && header.Substring(3,3)=="STL";
        public static bool ContainsJapaneseProfileStyling(string text)=>new[]{"bouten-","tts:ruby=","tts:rubyReserve="}.Any(text.Contains);
        public static SKColor GetColor(string text){var color=HtmlUtil.GetColorFromString(text);return new SKColor(color.R,color.G,color.B,color.A);}
        private static readonly Func<TimeCode,string> TimeString=(Func<TimeCode,string>)Delegate.CreateDelegate(typeof(Func<TimeCode,string>),typeof(TimedText10).GetMethod("ConvertToTimeString",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(TimeCode)},null));
        private static readonly Func<string,bool> Frames=(Func<string,bool>)Delegate.CreateDelegate(typeof(Func<string,bool>),typeof(TimedText10).GetMethod("IsFrames",BindingFlags.Static|BindingFlags.NonPublic));
        public static string ConvertToTimeString(TimeCode time)=>TimeString(time);
        private static readonly Func<TimeCode,string,string> SpecifiedTimeString=(Func<TimeCode,string,string>)Delegate.CreateDelegate(typeof(Func<TimeCode,string,string>),typeof(TimedText10).GetMethod("ConvertToTimeString",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(TimeCode),typeof(string)},null));
        public static string ConvertToTimeString(TimeCode time,string format)=>SpecifiedTimeString(time,format);
        public static string EscapeUnsupportedAngleBrackets(string text)=>Regex.Replace(text,@"<(?!(/?(i|b|u)>|font[ >]|/font>|br\s*/?>))","&lt;",RegexOptions.IgnoreCase);
        private static string Attribute(XmlNode node,string name)=>node?.Attributes?[name,"http://www.w3.org/ns/ttml"]?.InnerText??node?.Attributes?[name]?.InnerText??string.Empty;
        public static void ExtractTimeCodes(XmlNode paragraph,Subtitle subtitle,out TimeCode begin,out TimeCode end)
        {
            var b=Attribute(paragraph,"begin");var e=Attribute(paragraph,"end");var d=Attribute(paragraph,"dur");
            if(b.Length==0 && e.Length==0 && d.Length==0 && paragraph.ParentNode?.LocalName=="div"){b=Attribute(paragraph.ParentNode,"begin");e=Attribute(paragraph.ParentNode,"end");d=Attribute(paragraph.ParentNode,"dur");}
            begin=b.Length>0?TimedText10.GetTimeCode(b,Frames(b)):new TimeCode(subtitle.Paragraphs.Count>0?subtitle.Paragraphs.Last().EndTime.TotalMilliseconds:0);
            end=e.Length>0?TimedText10.GetTimeCode(e,Frames(e)):new TimeCode(begin.TotalMilliseconds+(d.Length>0?TimedText10.GetTimeCode(d,Frames(d)).TotalMilliseconds:3000));
        }
        public static string DecodeStyleToggles(string text)
        {
            foreach(var tag in new[]{new[]{"^B","b"},new[]{"^I","i"},new[]{"^U","u"}})
            {
                var open=false;var index=text.IndexOf(tag[0],StringComparison.Ordinal);
                while(index>=0){var replacement=open?"</"+tag[1]+">":"<"+tag[1]+">";text=text.Remove(index,2).Insert(index,replacement);open=!open;index=text.IndexOf(tag[0],index+replacement.Length,StringComparison.Ordinal);}if(open)text+="</"+tag[1]+">";
            }return text;
        }
    }
}
