using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicShortcutStep
    {
        public string Kind { get; set; } = "command";
        public string Value { get; set; } = "";
        public string Replacement { get; set; } = "";
        public bool Regex { get; set; }
        public bool CaseSensitive { get; set; }
        public string Position { get; set; } = "cursor";
        public string Replace(string text) => Regex
            ? System.Text.RegularExpressions.Regex.Replace(text, Value, Replacement, CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2))
            : System.Text.RegularExpressions.Regex.Replace(text, System.Text.RegularExpressions.Regex.Escape(Value), m => Replacement, CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
    }
    public sealed class ClassicCustomShortcut
    {
        public string Name { get; set; } = "";
        public Keys Key { get; set; }
        public bool AltGr { get; set; }
        public string Scope { get; set; } = "everywhere";
        public List<ClassicShortcutStep> Steps { get; set; } = new List<ClassicShortcutStep>();
        public bool Matches(Keys key, bool rightAlt) => Key != Keys.None && (AltGr
            ? rightAlt && (Key & Keys.KeyCode) == (key & Keys.KeyCode) && (Key & Keys.Shift) == (key & Keys.Shift)
            : !rightAlt && Key == key);
    }
    public sealed class ClassicCustomShortcuts
    {
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<ClassicCustomShortcut> Slots { get; set; } = Enumerable.Range(1, 8).Select(i => new ClassicCustomShortcut { Name = "Custom " + i }).ToList();
        public static string FileName => Path.Combine(Configuration.DataDirectory, "ClassicCustomShortcuts.json");
        public static ClassicCustomShortcuts Load()
        {
            try { var result = JsonConvert.DeserializeObject<ClassicCustomShortcuts>(File.ReadAllText(FileName)) ?? new ClassicCustomShortcuts(); result.Validate(); return result; }
            catch (IOException) { return new ClassicCustomShortcuts(); }
            catch (JsonException) { return new ClassicCustomShortcuts(); }
            catch (ArgumentException) { return new ClassicCustomShortcuts(); }
            catch (UnauthorizedAccessException) { return new ClassicCustomShortcuts(); }
        }
        public void Validate()
        {
            if (Slots == null || Slots.Count != 8) throw new ArgumentException("Exactly eight shortcut slots are required.");
            foreach (var s in Slots)
            {
                if (s == null || s.Steps == null || s.Steps.Count > 100 || !new[] { "everywhere", "grid", "text", "waveform" }.Contains(s.Scope)) throw new ArgumentException("Invalid shortcut scope or steps.");
                if (s.Key != Keys.None && (s.Key & (Keys.Control | Keys.Alt)) == 0 && !s.AltGr && (s.Key & Keys.KeyCode) < Keys.F1) throw new ArgumentException("Use Ctrl/Alt or a function key for custom shortcuts.");
                foreach (var step in s.Steps)
                {
                    if (step == null || !new[] { "command", "insert", "replace" }.Contains(step.Kind)) throw new ArgumentException("Unknown shortcut step.");
                    if (step.Kind == "replace") { if (string.IsNullOrEmpty(step.Value)) throw new ArgumentException("A search pattern is required."); step.Replace(""); }
                    if (step.Kind == "insert" && !new[] { "cursor", "start", "end" }.Contains(step.Position)) throw new ArgumentException("Invalid insert position.");
                }
            }
            if (Slots.Where(s => s.Key != Keys.None).GroupBy(s => new { s.Key, s.AltGr }).Any(g => g.Count() > 1)) throw new ArgumentException("Duplicate custom shortcut key.");
        }
        public void Save()
        {
            Validate(); Directory.CreateDirectory(Path.GetDirectoryName(FileName));
            var part = FileName + ".tmp";
            File.WriteAllText(part, JsonConvert.SerializeObject(this, Formatting.Indented));
            if (File.Exists(FileName)) File.Replace(part, FileName, null); else File.Move(part, FileName);
        }
    }
}
