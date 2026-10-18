using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicActorOptions
    {
        public int From { get; set; }
        public int To { get; set; } = 1;
        public int? Casing { get; set; }
        public bool OnlyNames { get; set; }
        public bool UseColor { get; set; }
        public int ColorArgb { get; set; } = Color.Yellow.ToArgb();
    }
    public sealed class ClassicTextToolSettings
    {
        public int MaxGap { get; set; } = 500;
        public int MaxCharacters { get; set; } = 84;
        public ClassicActorOptions Actors { get; set; } = new ClassicActorOptions();
        public Dictionary<string,string> Replacements { get; set; } = new Dictionary<string,string>();
        public bool BatchMerge { get; set; }
        public bool BatchActors { get; set; }
        public bool BatchControls { get; set; }
        public static string SettingsPath => Path.Combine(Configuration.DataDirectory, "ClassicTextTools.json");
        public static ClassicTextToolSettings Load()
        {
            ClassicTextToolSettings s = null;
            try { s = JsonConvert.DeserializeObject<ClassicTextToolSettings>(File.ReadAllText(SettingsPath)); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException) { }
            s = s ?? new ClassicTextToolSettings(); s.Actors = s.Actors ?? new ClassicActorOptions(); s.Replacements = s.Replacements ?? new Dictionary<string,string>();
            s.MaxGap = Math.Max(0, Math.Min(10000, s.MaxGap)); s.MaxCharacters = Math.Max(20, Math.Min(1000, s.MaxCharacters));
            s.Actors.From = Math.Max(0,Math.Min(3,s.Actors.From)); s.Actors.To = Math.Max(0,Math.Min(3,s.Actors.To));
            if (s.Actors.Casing < 0 || s.Actors.Casing > 3) s.Actors.Casing = null;
            return s;
        }
        public void Save()
        {
            var temp = SettingsPath + ".tmp";
            try { File.WriteAllText(temp, JsonConvert.SerializeObject(this, Formatting.Indented)); if (File.Exists(SettingsPath)) File.Replace(temp, SettingsPath, null); else File.Move(temp, SettingsPath); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
    public sealed class ClassicMergeCandidate
    {
        public int Index { get; set; }
        public bool Selected { get; set; } = true;
        public string Before { get; set; }
        public string After { get; set; }
    }
    public sealed class ClassicUnicodeCharacter
    {
        public string Character { get; set; }
        public int CodePoint { get; set; }
        public int Count { get; set; }
        public List<int> Lines { get; } = new List<int>();
        public string Replacement { get; set; }
        public bool Selected { get; set; } = true;
    }
    // Shared by the interactive tools, batch conversion and speech preparation.
    public static class ClassicTextTools
    {
        public static string L(string ru, string en) => (Configuration.Settings.General.Language ?? "").StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? ru : en;
        public static bool CanMerge(Paragraph a, Paragraph b, string language, int maxGap, int maxChars)
        {
            var first = HtmlUtil.RemoveHtmlTags(a.Text ?? "", true).Trim(); var second = HtmlUtil.RemoveHtmlTags(b.Text ?? "", true).Trim();
            // Different speakers/styles and overlapping cues must retain their independent identity.
            if (a.IsComment || b.IsComment || a.Actor != b.Actor || a.Extra != b.Extra || a.Layer != b.Layer ||
                b.StartTime.TotalMilliseconds < a.EndTime.TotalMilliseconds || b.StartTime.TotalMilliseconds-a.EndTime.TotalMilliseconds >= maxGap || first.Length+second.Length >= maxChars) return false;
            if (Dash(first) || Dash(second) || first.EndsWith("♪") || first.EndsWith("♫") || second.StartsWith("♪") || second.StartsWith("♫")) return false;
            if (first.Length == 0 || first.EndsWith("...") || first.EndsWith("…")) return true;
            return !first.HasSentenceEnding(language) && !first.EndsWith("！") && !first.EndsWith("．") && !first.EndsWith("｡");
        }
        private static bool Dash(string text) => text.Length > 0 && "-‐–—".IndexOf(text[0]) >= 0;
        public static List<ClassicMergeCandidate> DetectMerges(Subtitle source, string language, int gap, int chars)
        {
            var result = new List<ClassicMergeCandidate>();
            for (var i=0; i+1<source.Paragraphs.Count; i++) if (CanMerge(source.Paragraphs[i],source.Paragraphs[i+1],language,gap,chars))
                result.Add(new ClassicMergeCandidate { Index=i, Before=source.Paragraphs[i].Text + " → " + source.Paragraphs[i+1].Text, After=MergeText(source.Paragraphs[i].Text,source.Paragraphs[i+1].Text,language) });
            return result;
        }
        private static string MergeText(string a, string b, string language) => Utilities.AutoBreakLine((a??"").TrimEnd()+" "+(b??"").TrimStart(),language);
        public static Subtitle Merge(Subtitle source, ISet<int> selected, string language)
        {
            var result = new Subtitle(source,false); result.Paragraphs.Clear();
            for (var i=0;i<source.Paragraphs.Count;i++)
            {
                var p = new Paragraph(source.Paragraphs[i],false);
                while (i+1<source.Paragraphs.Count && selected.Contains(i)) { var next=source.Paragraphs[++i]; p.Text=MergeText(p.Text,next.Text,language); p.EndTime.TotalMilliseconds=next.EndTime.TotalMilliseconds; }
                result.Paragraphs.Add(p);
            }
            result.Renumber(); return result;
        }
        public static ClassicActorConverter ActorConverter(Subtitle source, SubtitleFormat format, ClassicActorOptions options)
        {
            return new ClassicActorConverter(format,LanguageAutoDetect.AutoDetectGoogleLanguage(source)) { ToSquare=options.To==0, ToParentheses=options.To==1, ToColon=options.To==2, ToActor=options.To==3 };
        }
        public static ClassicActorConverterResult ConvertActor(ClassicActorConverter converter, Paragraph source, ClassicActorOptions o)
        {
            var p = new Paragraph(source,false); var text=p.Text??""; Color? color=o.UseColor?(Color?)Color.FromArgb(o.ColorArgb):null;
            if (o.From==3 && !string.IsNullOrEmpty(p.Actor)) return converter.FixActorsFromActor(p,o.Casing,color);
            if (o.From==2 && text.Contains(":")) return converter.FixActorsFromBeforeColon(p,':',o.Casing,color);
            var start=o.From==0?'[':'('; var end=o.From==0?']':')';
            if (o.From<2 && text.IndexOf(start)>=0 && text.IndexOf(end)>text.IndexOf(start)) return converter.FixActors(p,start,end,o.Casing,color);
            return null;
        }
        public static Subtitle ConvertActors(Subtitle source, SubtitleFormat format, ClassicActorOptions options, ISet<int> selected=null)
        {
            var result=new Subtitle(source,false); result.Paragraphs.Clear(); var converter=ActorConverter(source,format,options);
            for (int i=0;i<source.Paragraphs.Count;i++)
            {
                var old=source.Paragraphs[i]; var fix=selected==null || selected.Contains(i)?ConvertActor(converter,old,options):null;
                if (fix==null || fix.Skip || (options.OnlyNames && !fix.Selected)) result.Paragraphs.Add(new Paragraph(old,false));
                else { result.Paragraphs.Add(fix.Paragraph); if (fix.NextParagraph!=null) result.Paragraphs.Add(fix.NextParagraph); }
            }
            result.Renumber(); return result;
        }
        private static IEnumerable<string> Scalars(string text)
        {
            for (int i=0;i<(text??"").Length;i++)
            {
                if (char.IsHighSurrogate(text[i]) && i+1<text.Length && char.IsLowSurrogate(text[i+1])) yield return text.Substring(i++,2);
                else yield return text[i].ToString(); // Preserve malformed UTF-16 without throwing or losing adjacent text.
            }
        }
        public static List<ClassicUnicodeCharacter> UnicodeCharacters(Subtitle source, IDictionary<string,string> saved)
        {
            var found=new Dictionary<string,ClassicUnicodeCharacter>();
            for (int i=0;i<source.Paragraphs.Count;i++) foreach(var scalar in Scalars(source.Paragraphs[i].Text))
            {
                int code=scalar.Length==2?char.ConvertToUtf32(scalar,0):scalar[0]; if (code<=255) continue;
                if (!found.TryGetValue(scalar,out var entry)) { entry=new ClassicUnicodeCharacter { Character=scalar,CodePoint=code,Replacement=saved.TryGetValue(scalar,out var value)?value:"" }; found.Add(scalar,entry); }
                entry.Count++; if (entry.Lines.Count==0 || entry.Lines.Last()!=i+1) entry.Lines.Add(i+1);
            }
            return found.Values.OrderBy(p=>p.CodePoint).ToList();
        }
        public static Subtitle ReplaceUnicode(Subtitle source, IDictionary<string,string> replacements)
        {
            var result=new Subtitle(source,false);
            foreach(var p in result.Paragraphs)
            {
                // A single scan prevents replacement text from being replaced again by another mapping.
                var text=new StringBuilder(); foreach(var scalar in Scalars(p.Text)) text.Append(replacements.TryGetValue(scalar,out var replacement)?replacement:scalar); p.Text=text.ToString();
            }
            return result;
        }
    }
}
