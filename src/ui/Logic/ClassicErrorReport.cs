using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;

namespace Nikse.SubtitleEdit.Logic
{
    public enum ClassicLineErrorType { TooManyLines, ReadingSpeed, TooShort, TooLong, LineTooLong, LineTooWide, Overlap, GapTooShort, WordsPerMinute, MissingTimeCodes }
    public sealed class ClassicLineError
    {
        public int Number { get; set; }
        public string FileName { get; set; }
        public ClassicLineErrorType Type { get; set; }
        public string Detail { get; set; }
        public Paragraph Cue { get; set; }
        public string Category => ClassicErrorReport.Label(Type);
        public string Show => Cue.StartTime.ToDisplayString();
        public string Hide => Cue.EndTime.ToDisplayString();
        public string Text => Cue.Text ?? "";
    }
    // The stable 5.3 per-cue report, mapped to the classic grid's combined duration/CPS switches.
    public static class ClassicErrorReport
    {
        public static string L(string ru,string en) => ClassicTextTools.L(ru,en);
        public static string Title => L("Список ошибок", "List errors");
        public static string Label(ClassicLineErrorType type)
        {
            switch(type)
            {
                case ClassicLineErrorType.TooManyLines: return L("Слишком много строк", "Too many lines");
                case ClassicLineErrorType.ReadingSpeed: return L("Скорость чтения", "Reading speed");
                case ClassicLineErrorType.TooShort: return L("Слишком короткая", "Too short");
                case ClassicLineErrorType.TooLong: return L("Слишком длинная", "Too long");
                case ClassicLineErrorType.LineTooLong: return L("Длина строки", "Line too long");
                case ClassicLineErrorType.LineTooWide: return L("Ширина строки", "Line too wide");
                case ClassicLineErrorType.Overlap: return L("Пересечение", "Overlapping");
                case ClassicLineErrorType.GapTooShort: return L("Короткий зазор", "Gap too short");
                case ClassicLineErrorType.WordsPerMinute: return L("Слов в минуту", "Words per minute");
                default: return L("Нет тайм-кода", "Missing time code");
            }
        }
        public static string ColorHex(ClassicLineErrorType type) => new[]{"#E84393","#E8912D","#F1C40F","#9B59B6","#3498DB","#2980B9","#E74C3C","#1ABC9C","#D35400","#C0392B"}[(int)type];
        public static List<ClassicLineError> Analyze(Subtitle source,SubtitleFormat format,string fileName=null)
        {
            var result=new List<ClassicLineError>(); var settings=Configuration.Settings; var rules=settings.General; var switches=settings.Tools;
            var teletext=(format?.Name=="DVB Teletext") || (format is Ebu && ((source.Header??"").Length==1024 ? source.Header[11]!='0' : source.Paragraphs.Any(p=>(p.Text??"").IndexOf("<font color",StringComparison.OrdinalIgnoreCase)>=0)));
            for(int i=0;i<source.Paragraphs.Count;i++)
            {
                var p=source.Paragraphs[i]; var snapshot=new Paragraph(p,false); var lines=HtmlUtil.RemoveHtmlTags(p.Text??"",true).SplitToLines();
                Action<ClassicLineErrorType,string> add=(type,detail)=>result.Add(new ClassicLineError {Number=i+1,FileName=fileName??source.FileName??"",Type=type,Detail=detail,Cue=snapshot});
                if(p.StartTime.IsMaxTime || p.EndTime.IsMaxTime)add(ClassicLineErrorType.MissingTimeCodes,L("Не задано начало или конец", "Start or end time is not set"));
                else if(p.WordsPerMinute>rules.SubtitleMaximumWordsPerMinute)add(ClassicLineErrorType.WordsPerMinute,Math.Round(p.WordsPerMinute,1)+" > "+rules.SubtitleMaximumWordsPerMinute);
                if(switches.ListViewSyntaxMoreThanXLines && p.NumberOfLines>rules.MaxNumberOfLines) add(ClassicLineErrorType.TooManyLines,p.NumberOfLines+" > "+rules.MaxNumberOfLines);
                if(switches.ListViewSyntaxColorDurationSmall && !p.StartTime.IsMaxTime && !p.EndTime.IsMaxTime)
                {
                    var cps=p.GetCharactersPerSecond(); if(cps>rules.SubtitleMaximumCharactersPerSeconds)add(ClassicLineErrorType.ReadingSpeed,Math.Round(cps,1).ToString(CultureInfo.CurrentCulture)+" > "+rules.SubtitleMaximumCharactersPerSeconds);
                    if(p.DurationTotalMilliseconds<rules.SubtitleMinimumDisplayMilliseconds)add(ClassicLineErrorType.TooShort,Math.Round(p.DurationTotalMilliseconds,3)+" < "+rules.SubtitleMinimumDisplayMilliseconds+" ms");
                }
                if(switches.ListViewSyntaxColorDurationBig && !p.StartTime.IsMaxTime && !p.EndTime.IsMaxTime && p.DurationTotalMilliseconds>rules.SubtitleMaximumDisplayMilliseconds)add(ClassicLineErrorType.TooLong,Math.Round(p.DurationTotalMilliseconds,3)+" > "+rules.SubtitleMaximumDisplayMilliseconds+" ms");
                foreach(var line in lines)
                {
                    if(switches.ListViewSyntaxColorLongLines && line.CountCharacters(false)>rules.SubtitleLineMaximumLength)add(ClassicLineErrorType.LineTooLong,line.CountCharacters(false)+" > "+rules.SubtitleLineMaximumLength);
                    if(switches.ListViewSyntaxColorWideLines) { var width=TextWidth.CalcPixelWidth(line); if(width>rules.SubtitleLineMaximumPixelWidth)add(ClassicLineErrorType.LineTooWide,width+" > "+rules.SubtitleLineMaximumPixelWidth+" px"); }
                    var teletextLimit=(p.Text??"").IndexOf("<font color=",StringComparison.OrdinalIgnoreCase)>=0?36:37;
                    if(teletext && line.Length>teletextLimit)add(ClassicLineErrorType.LineTooLong,L("Телетекст: ","Teletext: ")+line.Length+" > "+teletextLimit);
                }
                Action<Paragraph,bool> neighbour=(other,previous)=>
                {
                    if(other==null || p.StartTime.IsMaxTime || other.StartTime.IsMaxTime || p.EndTime.IsMaxTime || other.EndTime.IsMaxTime)return;
                    double gap=previous?p.StartTime.TotalMilliseconds-other.EndTime.TotalMilliseconds:other.StartTime.TotalMilliseconds-p.EndTime.TotalMilliseconds;
                    var direction=previous?L("от предыдущей: ","from previous: "):L("до следующей: ","to next: ");
                    if(gap<0) { if(switches.ListViewSyntaxColorOverlap)add(ClassicLineErrorType.Overlap,direction+Math.Round(-gap,3)+" ms"); }
                    else if(switches.ListViewSyntaxColorGap && gap<rules.MinimumMillisecondsBetweenLines)add(ClassicLineErrorType.GapTooShort,direction+Math.Round(gap,3)+" < "+rules.MinimumMillisecondsBetweenLines+" ms");
                };
                neighbour(i>0?source.Paragraphs[i-1]:null,true); neighbour(i+1<source.Paragraphs.Count?source.Paragraphs[i+1]:null,false);
            }
            return result;
        }
        public static string Summary(IReadOnlyList<ClassicLineError> rows,int totalLines)
        {
            if(rows.Count==0)return L("Ошибок не найдено.","No errors found.");
            var affected=rows.Select(p=>(p.FileName??"")+"|"+p.Number).Distinct().Count();
            return string.Format(L("Ошибок: {0}; реплик с ошибками: {1} из {2}.","{0} error(s) in {1} of {2} line(s)."),rows.Count,affected,totalLines);
        }
        public static string[] Headers(bool batch) => (batch?new[]{L("Файл","File name")}:new string[0]).Concat(new[]{"#",L("Ошибка","Error"),L("Начало","Show"),L("Конец","Hide"),L("Подробности","Detail"),L("Текст","Text")}).ToArray();
        public static object[] Cells(ClassicLineError row,bool batch) => (batch?new object[]{row.FileName??""}:new object[0]).Concat(new object[]{row.Number,row.Category,row.Show,row.Hide,row.Detail,row.Text}).ToArray();
        private static string Flat(string s) => (s??"").Replace("\r\n"," ").Replace('\r',' ').Replace('\n',' ').Replace('\t',' ');
        public static string Tsv(IEnumerable<ClassicLineError> rows,bool batch) => string.Join("\t",Headers(batch))+Environment.NewLine+string.Join(Environment.NewLine,rows.Select(r=>string.Join("\t",Cells(r,batch).Select(c=>Flat(Convert.ToString(c))))));
        public static string PlainText(IReadOnlyList<ClassicLineError> rows,string summary,string fileName,bool batch) => Title+Environment.NewLine+(fileName??"")+Environment.NewLine+summary+Environment.NewLine+Tsv(rows,batch);
        public static byte[] Xlsx(IEnumerable<ClassicLineError> rows,bool batch) => ClassicXlsxWriter.Create(Title,Headers(batch),rows.Select(r=>(IReadOnlyList<object>)Cells(r,batch)),batch?new double[]{40,6,22,14,14,32,80}:new double[]{6,22,14,14,32,80});
        private static string CsvCell(string text) => "\""+(text??"").Replace("\"","\"\"").Replace("\r","\\r").Replace("\n","\\n")+"\"";
        public static string Csv(IEnumerable<ClassicLineError> rows) => "FileName,LineNumber,Text,Error\r\n"+string.Join("\r\n",rows.Select(r=>CsvCell(r.FileName)+","+r.Number+","+CsvCell(r.Text)+","+CsvCell(r.Category+": "+r.Detail)));
        public static string Html(IReadOnlyList<ClassicLineError> rows,string summary,string fileName,bool batch)
        {
            Func<string,string> enc=s=>HttpUtility.HtmlEncode(s??"");var html=new StringBuilder("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>"+enc(Title)+"</title><style>body{font:15px system-ui;margin:2rem;color:#17212b;background:#f5f7f9}table{border-collapse:collapse;background:white;width:100%}th,td{text-align:left;padding:.6rem;border-bottom:1px solid #ddd;vertical-align:top}td:last-child{white-space:pre-wrap}nav{display:flex;flex-wrap:wrap;gap:.5rem;margin:1rem 0}.chip{padding:.7rem;border:1px solid #aaa;border-radius:.4rem;background:white;cursor:pointer}.dot{display:inline-block;width:.65rem;height:.65rem;border-radius:50%;margin-right:.4rem}input[name=f]{display:none}");
            foreach(var type in rows.Select(r=>r.Type).Distinct())html.Append("#f").Append((int)type).Append(":checked~table tbody tr:not(.t").Append((int)type).Append("){display:none}");
            html.Append("</style></head><body><h1>").Append(enc(Title)).Append("</h1><p>").Append(enc(fileName)).Append("</p><p>").Append(enc(summary)).Append("</p><input type=radio name=f id=all checked>");
            foreach(var type in rows.Select(r=>r.Type).Distinct())html.Append("<input type=radio name=f id=f").Append((int)type).Append('>');
            html.Append("<nav><label class=chip for=all>").Append(enc(L("Все", "All"))).Append(" (").Append(rows.Count).Append(")</label>");
            foreach(var group in rows.GroupBy(r=>r.Type))html.Append("<label class=chip for=f").Append((int)group.Key).Append("><i class=dot style=\"background:").Append(ColorHex(group.Key)).Append("\"></i>").Append(enc(Label(group.Key))).Append(" (").Append(group.Count()).Append(")</label>");
            html.Append("</nav><table><thead><tr>");foreach(var h in Headers(batch))html.Append("<th>").Append(enc(h)).Append("</th>");html.Append("</tr></thead><tbody>");
            foreach(var row in rows) {html.Append("<tr class=t").Append((int)row.Type).Append('>');foreach(var c in Cells(row,batch))html.Append("<td>").Append(enc(Convert.ToString(c))).Append("</td>");html.Append("</tr>");}
            return html.Append("</tbody></table></body></html>").ToString();
        }
    }
}
