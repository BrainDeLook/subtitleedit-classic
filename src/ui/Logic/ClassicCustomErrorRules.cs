using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicCustomErrorRule
    {
        public bool Enabled { get; set; } = true;
        public string Name { get; set; } = "";
        public string Pattern { get; set; } = "";
        public bool IgnoreCase { get; set; }
    }
    public static class ClassicCustomErrorRules
    {
        private sealed class CompiledRule
        {
            public ClassicCustomErrorRule Definition;
            public Regex Expression;
            public bool TimedOut;
        }
        private static CompiledRule[] _rules;
        public static string LoadError { get; private set; }
        private static string FileName => Path.Combine(Configuration.DataDirectory, "ClassicCustomErrorRules.json");
        private static CompiledRule[] Compile(IReadOnlyList<ClassicCustomErrorRule> rules)
        {
            if (rules == null || rules.Count > 32) throw new ArgumentException("Use at most 32 custom error rules.");
            return rules.Select(rule =>
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.Name) || string.IsNullOrWhiteSpace(rule.Pattern) || rule.Pattern.Length > 4000)
                    throw new ArgumentException("Every custom rule needs a name and a regular expression (up to 4000 characters).");
                return new CompiledRule { Definition = new ClassicCustomErrorRule { Enabled = rule.Enabled, Name = rule.Name, Pattern = rule.Pattern, IgnoreCase = rule.IgnoreCase }, Expression = new Regex(rule.Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled | (rule.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None), TimeSpan.FromMilliseconds(50)) };
            }).ToArray();
        }
        private static void EnsureLoaded()
        {
            if (_rules != null) return;
            try { _rules = File.Exists(FileName) ? Compile(JsonConvert.DeserializeObject<List<ClassicCustomErrorRule>>(File.ReadAllText(FileName))) : Array.Empty<CompiledRule>(); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is ArgumentException)
            { _rules = Array.Empty<CompiledRule>(); LoadError = ex.Message; }
        }
        public static List<ClassicCustomErrorRule> Definitions()
        {
            EnsureLoaded();
            return _rules.Select(r => new ClassicCustomErrorRule { Enabled = r.Definition.Enabled, Name = r.Definition.Name, Pattern = r.Definition.Pattern, IgnoreCase = r.Definition.IgnoreCase }).ToList();
        }
        public static void Save(IReadOnlyList<ClassicCustomErrorRule> definitions)
        {
            var compiled = Compile(definitions);
            Directory.CreateDirectory(Configuration.DataDirectory);
            var stage = FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(stage, JsonConvert.SerializeObject(definitions, Formatting.Indented));
                if (File.Exists(FileName)) File.Replace(stage, FileName, null); else File.Move(stage, FileName);
                _rules = compiled; LoadError = null;
            }
            finally { if (File.Exists(stage)) File.Delete(stage); }
        }
        public static string Match(string text)
        {
            EnsureLoaded();
            foreach (var rule in _rules)
            {
                if (!rule.Definition.Enabled || rule.TimedOut) continue;
                try { if (rule.Expression.IsMatch(text ?? "")) return rule.Definition.Name; }
                catch (RegexMatchTimeoutException)
                { rule.TimedOut = true; LoadError = "Custom rule timed out and was disabled for this session: " + rule.Definition.Name; }
            }
            return null;
        }
    }
}
