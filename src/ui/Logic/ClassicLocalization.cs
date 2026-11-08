using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic
{
    /// <summary>Adapts pinned upstream labels without replacing Classic's XML language schema.</summary>
    public static class ClassicLocalization
    {
        private const string Resource = "Classic54Localization.zip";
        private static readonly Lazy<Dictionary<string,string>> Aliases = new Lazy<Dictionary<string,string>>(() => ReadEntry("aliases.json"));
        private static readonly ConcurrentDictionary<string, Dictionary<string,string>> Cultures = new ConcurrentDictionary<string,Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);
        public static IEnumerable<string> SupportedCultures => Aliases.Value.Values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(culture=>culture, StringComparer.OrdinalIgnoreCase);
        public static string Text(string russian, string english) => ForCulture(Configuration.Settings.General.Language, russian, english);
        public static string ForCulture(string culture, string russian, string english)
        {
            var requested = (culture ?? "en").Replace('_','-').ToLowerInvariant();
            if (requested == "ru" || requested.StartsWith("ru-", StringComparison.Ordinal)) return russian;
            if (requested == "en" || requested.StartsWith("en-", StringComparison.Ordinal)) return english;
            if (!Aliases.Value.TryGetValue(requested, out var canonical))
            {
                var primary = requested.Split('-')[0];
                if (!Aliases.Value.TryGetValue(primary, out canonical)) return english;
            }
            var labels = Cultures.GetOrAdd(canonical, name=>ReadEntry(name+".json"));
            var key = Whitespace.Replace(english.Replace("&", "").Replace("_", "").Replace("…", "...").Trim().TrimEnd('.', ':', ' '), " ").ToLowerInvariant();
            if (!labels.TryGetValue(key, out var translated)) return english;
            var trimmed = english.Trim();
            if (trimmed.EndsWith(":", StringComparison.Ordinal) && !translated.TrimEnd().EndsWith(":", StringComparison.Ordinal)) translated += ":";
            if ((trimmed.EndsWith("…", StringComparison.Ordinal) || trimmed.EndsWith("...", StringComparison.Ordinal)) && !translated.EndsWith("…", StringComparison.Ordinal) && !translated.EndsWith("...", StringComparison.Ordinal)) translated += "…";
            return english.Substring(0, english.Length-english.TrimStart().Length) + translated.Trim() + english.Substring(english.TrimEnd().Length);
        }
        private static Dictionary<string,string> ReadEntry(string name)
        {
            using (var stream = typeof(ClassicLocalization).Assembly.GetManifestResourceStream(Resource))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            using (var entry = archive.GetEntry(name).Open())
            using (var reader = new StreamReader(entry))
                return JsonConvert.DeserializeObject<Dictionary<string,string>>(reader.ReadToEnd());
        }
    }
}
