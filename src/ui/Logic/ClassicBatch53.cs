using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicBatch53Options
    {
        [Description("Comma-separated target language codes; leave empty for the selected target language.")]
        public string TargetLanguages { get; set; } = "";
        public bool PreserveSourceTimestamp { get; set; }
        public bool EmbedAssFonts { get; set; }
        public bool TrimAssFonts { get; set; }
        public string StyleName { get; set; } = "";
        [Description("One ASS style property per line, e.g. FontName=Segoe UI, FontSize=43, Primary=#FFFFFF.")]
        public string StyleProperties { get; set; } = "";
        public string OcrLanguagesByFile { get; set; } = "";
        public string Filter { get; set; } = "all";
        public string FilterValue { get; set; } = "";
        public string[] Languages => TargetLanguages.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        public void Validate()
        {
            if (!new[] { "all", "format", "extension", "error", "forced", "filename", "language" }.Contains(Filter)) throw new ArgumentException("Unknown filter.");
            if (TrimAssFonts && !EmbedAssFonts) throw new ArgumentException("Enable font embedding before font subsetting.");
            var probe = new SsaStyle(); ApplyStyle(probe);
            foreach (var code in Languages) if (code.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_')) throw new ArgumentException("Invalid language code.");
        }
        private void ApplyStyle(SsaStyle style)
        {
            foreach (var line in StyleProperties.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int split = line.IndexOf('='); if (split < 1) throw new ArgumentException("Use Property=Value for ASS styles.");
                string name = line.Substring(0, split).Trim(), value = line.Substring(split + 1).Trim();
                var property = typeof(SsaStyle).GetProperty(name);
                if (property == null || !property.CanWrite || new[] { "Name", "RawLine", "LoadedFromHeader" }.Contains(name)) throw new ArgumentException("Unknown or unsupported ASS property: " + name);
                object converted = property.PropertyType == typeof(Color) ? (object)ColorTranslator.FromHtml(value) : Convert.ChangeType(value, property.PropertyType, CultureInfo.InvariantCulture);
                property.SetValue(style, converted);
            }
        }
        public void Apply(Subtitle subtitle, SubtitleFormat target, CancellationToken token)
        {
            if (!string.IsNullOrWhiteSpace(StyleProperties) || EmbedAssFonts)
            {
                if (!(target is AdvancedSubStationAlpha) && !(target is SubStationAlpha)) throw new ArgumentException("ASS style properties and embedded fonts require an ASS/SSA target format.");
                if (string.IsNullOrWhiteSpace(subtitle.Header) || !subtitle.Header.Contains("[Script Info]")) subtitle.Header = AdvancedSubStationAlpha.DefaultHeader;
                var styles = AdvancedSubStationAlpha.GetSsaStylesFromHeader(subtitle.Header);
                if (styles.Count == 0) styles.Add(new SsaStyle());
                if (!string.IsNullOrWhiteSpace(StyleName) && !styles.Any(style => style.Name.Equals(StyleName, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("ASS style was not found: " + StyleName);
                foreach (var style in styles) { token.ThrowIfCancellationRequested(); if (string.IsNullOrWhiteSpace(StyleName) || style.Name.Equals(StyleName, StringComparison.OrdinalIgnoreCase)) ApplyStyle(style); }
                subtitle.Header = AdvancedSubStationAlpha.GetHeaderAndStylesFromAdvancedSubStationAlpha(subtitle.Header, styles);
                if (EmbedAssFonts)
                {
                    var used = AssaFontEmbedder.GetUsedFontNames(subtitle);
                    var fonts = ClassicFontCollection.Scan(subtitle, token).Where(f => used.Contains(f.Family, StringComparer.OrdinalIgnoreCase)).GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
                    var missing = used.Where(n => !fonts.Any(f => f.Family.Equals(n, StringComparison.OrdinalIgnoreCase))).ToList();
                    if (missing.Count > 0) throw new InvalidDataException("Missing fonts: " + string.Join(", ", missing));
                    foreach (var font in fonts)
                    {
                        token.ThrowIfCancellationRequested(); var bytes = font.Read();
                        if (TrimAssFonts) bytes = FontTrimmer.Trim(bytes, AssaFontEmbedder.GetUsedTextLines(subtitle)).Bytes;
                        subtitle.Footer = AssaFontEmbedder.AddFontToFooter(subtitle.Footer, font.Path, bytes);
                    }
                }
            }
        }
        public bool Skip(string file, Subtitle subtitle, SubtitleFormat format, string status = "", string language = "")
        {
            switch (Filter)
            {
                case "format": return format?.Name?.IndexOf(FilterValue, StringComparison.OrdinalIgnoreCase) < 0;
                case "extension": return !Path.GetExtension(file).TrimStart('.').Equals(FilterValue.TrimStart('.'), StringComparison.OrdinalIgnoreCase);
                case "language": return !language.Equals(FilterValue.Trim(), StringComparison.OrdinalIgnoreCase) && Path.GetFileNameWithoutExtension(file).Split('.', '_', '-').All(part => !part.Equals(FilterValue.Trim(), StringComparison.OrdinalIgnoreCase));
                case "forced": return !subtitle.Paragraphs.Any(p => p.Forced);
                case "filename": return Path.GetFileName(file).IndexOf(FilterValue, StringComparison.OrdinalIgnoreCase) < 0;
                case "error": return status.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0 && status.IndexOf("failed", StringComparison.OrdinalIgnoreCase) < 0 && status.IndexOf("ошиб", StringComparison.OrdinalIgnoreCase) < 0;
                default: return false;
            }
        }
        public string OcrLanguage(string file, string fallback)
        {
            foreach (var line in OcrLanguagesByFile.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            { int at = line.LastIndexOf('='); if (at > 0 && (line.Substring(0, at).Trim().Equals(Path.GetFileName(file), StringComparison.OrdinalIgnoreCase) || line.Substring(0, at).Trim().Equals(file, StringComparison.OrdinalIgnoreCase))) return line.Substring(at + 1).Trim(); }
            return fallback;
        }
    }
    public sealed class ClassicBatch53Preset
    {
        public ClassicBatch53Options Options { get; set; } = new ClassicBatch53Options();
        public Dictionary<string, string> Controls { get; set; } = new Dictionary<string, string>();
        public List<string> Actions { get; set; } = new List<string>();
    }
}
