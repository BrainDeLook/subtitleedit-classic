using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicPaletteCommand
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }
        public string Shortcut { get; set; }
        public Func<bool> Execute { get; set; }
        public bool Enabled { get; set; } = true;
    }

    // Stable 5.3 ranking and session-only history, independent of the UI toolkit.
    public sealed class ClassicCommandPalette
    {
        private static readonly List<string> Recent = new List<string>();
        public static string Clean(string text) => (text ?? "").Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&").Trim().TrimEnd('.', '…');
        private static string Initials(string text)
        {
            var result = new StringBuilder(); bool start = true;
            foreach (var c in text) { if (char.IsLetterOrDigit(c)) { if (start) result.Append(c); start = false; } else start = true; }
            return result.ToString();
        }
        private static bool WordStart(string name, string token)
        {
            var at = name.IndexOf(token, StringComparison.Ordinal);
            while (at >= 0) { if (at == 0 || !char.IsLetterOrDigit(name[at - 1])) return true; at = name.IndexOf(token, at + 1, StringComparison.Ordinal); }
            return false;
        }
        private static int Score(ClassicPaletteCommand item, string query, string[] tokens)
        {
            var name = item.Name.ToLowerInvariant();
            var searchable = (item.Name + " " + item.Group + " " + item.Shortcut).ToLowerInvariant();
            var initials = tokens.Length == 1 && query.Length > 1 && Initials(name).StartsWith(query, StringComparison.Ordinal);
            if (!initials && tokens.Any(t => !searchable.Contains(t))) return -1;
            if (name.StartsWith(query, StringComparison.Ordinal)) return 0;
            if (initials) return 1;
            if (tokens.All(t => WordStart(name, t))) return 2;
            if (name.Contains(query)) return 3;
            return tokens.All(t => name.Contains(t)) ? 4 : 5;
        }
        private static int RecentRank(ClassicPaletteCommand command) { var rank = Recent.IndexOf(command.Id); return rank < 0 ? int.MaxValue : rank; }
        public static List<ClassicPaletteCommand> Filter(IEnumerable<ClassicPaletteCommand> commands, string query)
        {
            query = (query ?? "").Trim().ToLowerInvariant(); var tokens = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return commands.Select(c => new { Command = c, Score = tokens.Length == 0 ? 0 : Score(c, query, tokens) }).Where(c => c.Score >= 0)
                .OrderBy(c => c.Score).ThenBy(c => RecentRank(c.Command)).ThenBy(c => c.Command.Name, StringComparer.CurrentCultureIgnoreCase).Select(c => c.Command).ToList();
        }
        public static bool Run(ClassicPaletteCommand command)
        {
            if (command == null || !command.Enabled || command.Execute == null || !command.Execute()) return false;
            Recent.Remove(command.Id); Recent.Insert(0, command.Id); if (Recent.Count > 10) Recent.RemoveAt(10); return true;
        }
    }
}
