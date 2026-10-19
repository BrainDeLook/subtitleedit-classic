using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic.ClassicImport
{
    public sealed class ClassicTableColumn { public string Field { get; set; } public string Header { get; set; } }
    public static class ClassicTableExport
    {
        public static readonly string[] Fields = { "number", "start", "end", "duration", "text", "actor", "style", "layer", "forced" };
        public static string Value(Paragraph p, string field, int timeMode)
        {
            string Time(double ms) => timeMode == 1 ? (ms / 1000).ToString("0.###", CultureInfo.InvariantCulture) : timeMode == 2 ? ms.ToString("0", CultureInfo.InvariantCulture) : new TimeCode(ms).ToString();
            switch (field) { case "number": return p.Number.ToString(); case "start": return Time(p.StartTime.TotalMilliseconds); case "end": return Time(p.EndTime.TotalMilliseconds); case "duration": return Time(p.DurationTotalMilliseconds); case "text": return p.Text; case "actor": return p.Actor; case "style": return p.Extra; case "layer": return p.Layer.ToString(); case "forced": return p.Forced ? "1" : "0"; default: throw new ArgumentException("Unknown export field: " + field); }
        }
        public static void Write(Subtitle source, IList<ClassicTableColumn> columns, bool header, int timeMode, string output, bool xlsx)
        {
            if (columns.Count == 0 || columns.Any(c => !Fields.Contains(c.Field))) throw new ArgumentException("Select export columns.");
            var rows = new List<string[]>(); if (header) rows.Add(columns.Select(c => c.Header ?? c.Field).ToArray()); rows.AddRange(source.Paragraphs.Select(p => columns.Select(c => Value(p, c.Field, timeMode) ?? "").ToArray()));
            var stage = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)), ".se-table-" + Guid.NewGuid().ToString("N"));
            try
            {
                if (!xlsx) File.WriteAllText(stage, string.Join("\r\n", rows.Select(r => string.Join(",", r.Select(c => "\"" + c.Replace("\"", "\"\"") + "\"")))), new UTF8Encoding(true));
                else using (var file = File.Create(stage)) using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                {
                    Entry(zip, "[Content_Types].xml", "<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Default Extension='rels' ContentType='application/vnd.openxmlformats-package.relationships+xml'/><Default Extension='xml' ContentType='application/xml'/><Override PartName='/xl/workbook.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml'/><Override PartName='/xl/worksheets/sheet1.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml'/></Types>");
                    Entry(zip, "_rels/.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument' Target='xl/workbook.xml'/></Relationships>");
                    Entry(zip, "xl/workbook.xml", "<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Subtitles' sheetId='1' r:id='rId1'/></sheets></workbook>");
                    Entry(zip, "xl/_rels/workbook.xml.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet' Target='worksheets/sheet1.xml'/></Relationships>");
                    using (var stream = zip.CreateEntry("xl/worksheets/sheet1.xml").Open()) using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
                    {
                        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"; writer.WriteStartElement("worksheet", ns); writer.WriteStartElement("sheetData", ns);
                        for (int y = 0; y < rows.Count; y++) { writer.WriteStartElement("row", ns); writer.WriteAttributeString("r", (y + 1).ToString()); for (int x = 0; x < rows[y].Length; x++) { writer.WriteStartElement("c", ns); writer.WriteAttributeString("r", ColumnName(x) + (y + 1)); writer.WriteAttributeString("t", "inlineStr"); writer.WriteStartElement("is", ns); writer.WriteStartElement("t", ns); writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve"); writer.WriteString(rows[y][x]); writer.WriteEndElement(); writer.WriteEndElement(); writer.WriteEndElement(); } writer.WriteEndElement(); }
                        writer.WriteEndElement(); writer.WriteEndElement();
                    }
                }
                ClassicMediaJobs.Commit(stage, output);
            }
            finally { if (File.Exists(stage)) File.Delete(stage); }
        }
        private static string ColumnName(int index) { var result = ""; do { result = (char)('A' + index % 26) + result; index = index / 26 - 1; } while (index >= 0); return result; }
        private static void Entry(ZipArchive zip, string path, string content) { using (var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false))) writer.Write(content); }
    }
}
