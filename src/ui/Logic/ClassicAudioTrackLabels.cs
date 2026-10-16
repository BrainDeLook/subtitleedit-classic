using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicAudioTrackLabel
    {
        public string Language { get; set; }
        public string Title { get; set; }
        public string Details { get; set; }
        public string Display(int index, bool russian)
        {
            var code = (Language ?? "").Trim().ToLowerInvariant();
            string language = null;
            var known = new Dictionary<string, string> { ["eng"] = "Английский", ["tur"] = "Турецкий", ["rus"] = "Русский", ["jpn"] = "Японский", ["deu"] = "Немецкий", ["ger"] = "Немецкий", ["fra"] = "Французский", ["fre"] = "Французский", ["spa"] = "Испанский", ["ita"] = "Итальянский", ["zho"] = "Китайский", ["chi"] = "Китайский", ["kor"] = "Корейский", ["ukr"] = "Украинский", ["por"] = "Португальский", ["ara"] = "Арабский" };
            if (russian) known.TryGetValue(code, out language);
            if (language == null && code != "und" && code.Length > 0)
            {
                var culture = CultureInfo.GetCultures(CultureTypes.NeutralCultures).FirstOrDefault(c => c.TwoLetterISOLanguageName == code || c.ThreeLetterISOLanguageName == code || c.ThreeLetterWindowsLanguageName.Equals(code, StringComparison.OrdinalIgnoreCase));
                language = culture?.DisplayName;
            }
            if (language == null) language = code.Length == 0 || code == "und" ? (russian ? "Язык не указан" : "Language unspecified") : code;
            var label = (index + 1) + ". " + language + (code.Length > 0 && code != "und" && code != language ? " [" + code + "]" : "");
            if (!string.IsNullOrWhiteSpace(Title)) label += " — " + Title.Trim();
            return label;
        }
    }
    public static class ClassicAudioTrackLabels
    {
        public static List<ClassicAudioTrackLabel> Parse(string log)
        {
            var result = new List<ClassicAudioTrackLabel>(); ClassicAudioTrackLabel current = null;
            foreach (var line in (log ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.TrimStart().StartsWith("Stream #", StringComparison.Ordinal))
                {
                    current = null;
                    var match = Regex.Match(line, @"^\s*Stream #\d+:\d+(?:\[[^\]]*\])?(?:\((?<language>[^)]*)\))?: Audio:\s*(?<details>.*)$");
                    if (match.Success) { current = new ClassicAudioTrackLabel { Language = match.Groups["language"].Value, Details = match.Groups["details"].Value }; result.Add(current); }
                    continue;
                }
                if (current == null) continue;
                var metadata = Regex.Match(line, @"^\s*(?<key>title|language)\s*:\s*(?<value>.*)$", RegexOptions.IgnoreCase);
                if (metadata.Success) { if (metadata.Groups["key"].Value.Equals("title", StringComparison.OrdinalIgnoreCase)) current.Title = metadata.Groups["value"].Value; else current.Language = metadata.Groups["value"].Value; }
            }
            return result;
        }
        public static List<ClassicAudioTrackLabel> Read(string file)
        {
            using (var process = new Process { StartInfo = new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation, "-hide_banner -i " + ClassicSpeechRunner.Quote(file)) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, StandardErrorEncoding = Encoding.UTF8 } })
            {
                process.Start(); var error = process.StandardError.ReadToEndAsync(); var output = process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit(8000)) { process.Kill(); process.WaitForExit(); throw new IOException("Audio metadata probe timed out."); }
                Task.WaitAll(error, output); return Parse(error.Result);
            }
        }
    }
}
