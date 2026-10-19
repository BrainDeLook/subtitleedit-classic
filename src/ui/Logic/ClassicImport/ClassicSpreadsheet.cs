using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic.ClassicImport
{
    public static class ClassicSpreadsheet
    {
        public static List<List<string>> Read(string path)
        {
            if(new FileInfo(path).Length>64*1024*1024)throw new InvalidDataException("Spreadsheet exceeds 64 MB.");
            var ext=Path.GetExtension(path).ToLowerInvariant();var lines=ext==".xlsx"?new UnknownFormatImporterXlsx().ReadLinesFromFile(path):ext==".ods"?new UnknownFormatImporterOds().ReadLinesFromFile(path):File.ReadAllLines(path,LanguageAutoDetect.GetEncodingFromFile(path)).ToList();
            if(lines==null)throw new InvalidDataException("Spreadsheet could not be read.");
            var separator=ext==".xlsx" || ext==".ods"?'\t':new[]{'\t',';',','}.OrderByDescending(c=>CsvUtil.CsvSplitLines(lines.Take(30).ToList(),c).Select(r=>r.Count).DefaultIfEmpty().Average()).First();
            return CsvUtil.CsvSplitLines(lines,separator);
        }
        public static string Quote(string text)=>"\""+(text??string.Empty).Replace("\"","\"\"")+"\"";
        public static Subtitle Convert(List<List<string>> rows,string[] roles,bool header,int numericMode)
        {
            if(!roles.Contains("text"))throw new InvalidDataException("Select a Text column.");
            var parsed=new List<UnknownFormatImporterCsv.CsvLine>();
            foreach(var row in rows.Skip(header?1:0))
            {
                var cells=new List<string>();for(int i=0;i<roles.Length;i++)
                {
                    var text=i<row.Count?row[i]:string.Empty;
                    if(numericMode>0 && (roles[i]=="start" || roles[i]=="end" || roles[i]=="duration") && double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var number))
                    {var ms=numericMode==1?number*1000:numericMode==2?number:numericMode==3?number*1000/Configuration.Settings.General.CurrentFrameRate:number*86400000;text=new TimeCode(ms).ToString();}
                    cells.Add(text);
                }
                string Value(string key){var index=Array.IndexOf(roles,key);return index>=0?cells[index]:string.Empty;}
                parsed.Add(new UnknownFormatImporterCsv.CsvLine{Start=Value("start"),End=Value("end"),Duration=Value("duration"),Text=Value("text"),Character=Value("actor")});
            }
            return new UnknownFormatImporterCsv().ConvertRows(parsed);
        }
    }
}

