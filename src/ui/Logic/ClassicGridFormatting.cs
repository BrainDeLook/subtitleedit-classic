using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Nikse.SubtitleEdit.Logic
{
    public enum ClassicGridFormattingMode { ShowTags, ShowFormatting, HideTags, ShowFormattingKeepTags }

    public sealed class ClassicGridAppearance
    {
        public ClassicGridFormattingMode Formatting { get; set; }
        public static ClassicGridAppearance Load(string path)
        {
            try
            {
                var value = JsonConvert.DeserializeObject<ClassicGridAppearance>(File.ReadAllText(path));
                if (value != null && Enum.IsDefined(typeof(ClassicGridFormattingMode), value.Formatting)) return value;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (JsonException) { }
            return new ClassicGridAppearance();
        }
        public void Save(string path)
        {
            if (!Enum.IsDefined(typeof(ClassicGridFormattingMode), Formatting)) throw new ArgumentOutOfRangeException(nameof(Formatting));
            var temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(this), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    // Display-only runs. The ListView subitems retain their original text for editing, copying and saving.
    public sealed class ClassicGridRun
    {
        public string Text { get; internal set; }
        public FontStyle Style { get; internal set; }
        public Color? Color { get; internal set; }
        public string Face { get; internal set; }
        public float? Size { get; internal set; }
        public bool Tag { get; internal set; }
    }

    public static class ClassicGridMarkup
    {
        private sealed class State
        {
            public FontStyle Style;
            public Color? Color;
            public string Face;
            public float? Size;
            public State Copy() => (State)MemberwiseClone();
            public void Reset() { Style = FontStyle.Regular; Color = null; Face = null; Size = null; }
        }
        private static readonly Regex Attributes = new Regex(@"\b(color|face|size)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        private static readonly Regex HtmlName = new Regex(@"^/?[a-zA-Z][a-zA-Z0-9]*(?:\s[^<>]*)?/?$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        private const int VisibleLimit = 2048;

        public static List<ClassicGridRun> Parse(string source, ClassicGridFormattingMode mode, string separator = " || ")
        {
            source = source ?? string.Empty;
            var result = new List<ClassicGridRun>();
            var state = new State();
            var buffer = new StringBuilder();
            var stack = new List<KeyValuePair<string, State>>();
            var visible = 0;
            Action flush = () =>
            {
                if (buffer.Length == 0) return;
                result.Add(new ClassicGridRun { Text = buffer.ToString(), Style = mode == ClassicGridFormattingMode.HideTags ? FontStyle.Regular : state.Style,
                    Color = mode == ClassicGridFormattingMode.HideTags ? null : state.Color, Face = mode == ClassicGridFormattingMode.HideTags ? null : state.Face,
                    Size = mode == ClassicGridFormattingMode.HideTags ? null : state.Size });
                buffer.Clear();
            };
            Action<string, bool> append = (value, tag) =>
            {
                if (visible >= VisibleLimit || value.Length == 0) return;
                var length = Math.Min(value.Length, VisibleLimit - visible);
                // Do not split a supplementary character at the display limit.
                if (length < value.Length && length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
                if (tag) { flush(); result.Add(new ClassicGridRun { Text = value.Substring(0, length), Tag = true }); }
                else buffer.Append(value, 0, length);
                visible += length;
                if (length < value.Length) { flush(); result.Add(new ClassicGridRun { Text = "…" }); visible = VisibleLimit; }
            };
            for (var i = 0; i < source.Length && visible < VisibleLimit;)
            {
                if (mode != ClassicGridFormattingMode.ShowTags && source[i] == '{' && i + 1 < source.Length && source[i + 1] == '\\')
                {
                    var end = source.IndexOf('}', i + 2);
                    if (end < 0) { append(source.Substring(i), false); break; }
                    flush();
                    var unknown = new StringBuilder();
                    // Split outside parentheses: formatting inside \t(...) is not an immediate override.
                    foreach (var tag in SplitAss(source, i + 1, end))
                    {
                        if (!ApplyAss(tag, state) && mode == ClassicGridFormattingMode.ShowFormattingKeepTags) unknown.Append('\\').Append(tag);
                    }
                    if (unknown.Length > 0) append("{" + unknown + "}", true);
                    i = end + 1;
                    continue;
                }
                if (mode != ClassicGridFormattingMode.ShowTags && source[i] == '<')
                {
                    if (source.IndexOf("<!--", i, StringComparison.Ordinal) == i)
                    {
                        var endComment = source.IndexOf("-->", i + 4, StringComparison.Ordinal);
                        if (endComment >= 0) { i = endComment + 3; continue; }
                    }
                    var end = source.IndexOf('>', i + 1, Math.Min(500, source.Length - i - 1));
                    if (end >= 0)
                    {
                        var content = source.Substring(i + 1, end - i - 1).Trim();
                        if (HtmlName.IsMatch(content))
                        {
                            flush();
                            var closing = content.StartsWith("/", StringComparison.Ordinal);
                            var name = content.TrimStart('/').Split(new[] { ' ', '\t', '\r', '\n', '/' }, 2)[0].ToLowerInvariant();
                            if (name == "br") append(separator, false);
                            else if (name == "i" || name == "b" || name == "u" || name == "s" || name == "font")
                            {
                                if (closing)
                                {
                                    var found = stack.FindLastIndex(p => p.Key == name);
                                    if (found >= 0) { state = stack[found].Value; stack.RemoveRange(found, stack.Count - found); }
                                }
                                else
                                {
                                    stack.Add(new KeyValuePair<string, State>(name, state.Copy()));
                                    if (name == "font") ApplyFont(content, state);
                                    else state.Style |= name == "i" ? FontStyle.Italic : name == "b" ? FontStyle.Bold : name == "u" ? FontStyle.Underline : FontStyle.Strikeout;
                                }
                            }
                            else if (mode == ClassicGridFormattingMode.ShowFormattingKeepTags) append(source.Substring(i, end - i + 1), true);
                            i = end + 1; continue;
                        }
                    }
                }
                if (source[i] == '\r' || source[i] == '\n' || source[i] == '\u2028')
                {
                    if (source[i] == '\r' && i + 1 < source.Length && source[i + 1] == '\n') i++;
                    append(separator, false); i++; continue;
                }
                if (mode != ClassicGridFormattingMode.ShowTags && source[i] == '\\' && i + 1 < source.Length && (source[i + 1] == 'N' || source[i + 1] == 'n' || source[i + 1] == 'h'))
                { append(source[i + 1] == 'h' ? "\u00a0" : separator, false); i += 2; continue; }
                if (mode != ClassicGridFormattingMode.ShowTags && source[i] == '&')
                {
                    var end = source.IndexOf(';', i + 1, Math.Min(16, source.Length - i - 1));
                    if (end >= 0)
                    {
                        var entity = source.Substring(i, end - i + 1);
                        var decoded = System.Net.WebUtility.HtmlDecode(entity);
                        if (entity != decoded) { append(decoded, false); i = end + 1; continue; }
                    }
                }
                var count = char.IsHighSurrogate(source[i]) && i + 1 < source.Length && char.IsLowSurrogate(source[i + 1]) ? 2 : 1;
                append(source.Substring(i, count), false); i += count;
            }
            flush();
            return result;
        }

        private static IEnumerable<string> SplitAss(string source, int start, int end)
        {
            var pos = start;
            while (pos < end)
            {
                if (source[pos] != '\\') { pos++; continue; }
                var begin = ++pos; var depth = 0;
                while (pos < end)
                {
                    if (source[pos] == '(') depth++;
                    else if (source[pos] == ')' && depth > 0) depth--;
                    else if (source[pos] == '\\' && depth == 0) break;
                    pos++;
                }
                yield return source.Substring(begin, pos - begin).Trim();
            }
        }
        private static bool ApplyAss(string tag, State state)
        {
            if (tag == "r") { state.Reset(); return true; }
            if (tag.Length >= 2 && (tag[0] == 'i' || tag[0] == 'b' || tag[0] == 'u' || tag[0] == 's') && int.TryParse(tag.Substring(1), out var value))
            {
                var style = tag[0] == 'i' ? FontStyle.Italic : tag[0] == 'b' ? FontStyle.Bold : tag[0] == 'u' ? FontStyle.Underline : FontStyle.Strikeout;
                state.Style = value == 0 ? state.Style & ~style : state.Style | style; return true;
            }
            if (tag.StartsWith("fn", StringComparison.Ordinal)) { state.Face = tag.Length <= 102 ? tag.Substring(2) : null; return true; }
            if (tag.StartsWith("fs", StringComparison.Ordinal) && float.TryParse(tag.Substring(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
            { state.Size = size > 0 && !float.IsInfinity(size) ? (float?)size : null; return true; }
            var colorTag = tag.StartsWith("1c", StringComparison.Ordinal) ? tag.Substring(2) : tag.StartsWith("c", StringComparison.Ordinal) && (tag.Length == 1 || tag[1] == '&') ? tag.Substring(1) : null;
            if (colorTag == null) return false;
            if (colorTag.Length == 0) { state.Color = null; return true; }
            var hex = colorTag.Trim('&').TrimStart('H', 'h');
            if (hex.Length <= 8 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bgr))
                state.Color = System.Drawing.Color.FromArgb((int)(bgr & 255), (int)((bgr >> 8) & 255), (int)((bgr >> 16) & 255));
            return true;
        }
        private static void ApplyFont(string content, State state)
        {
            foreach (Match match in Attributes.Matches(content))
            {
                var value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Success ? match.Groups[3].Value : match.Groups[4].Value;
                switch (match.Groups[1].Value.ToLowerInvariant())
                {
                    case "color":
                        try { var color = ColorTranslator.FromHtml(value); if (!color.IsEmpty) state.Color = color; } catch (ArgumentException) { }
                        break;
                    case "face": if (value.Length <= 100) state.Face = value; break;
                    case "size": if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var size) && size > 0 && !float.IsInfinity(size)) state.Size = size; break;
                }
            }
        }
    }

    public sealed class ClassicGridPainter
    {
        private readonly Dictionary<string, List<ClassicGridRun>> _cache = new Dictionary<string, List<ClassicGridRun>>();
        public void Clear() => _cache.Clear();
        public static Color ReadableColor(Color? requested, Color fallback, Color background, bool selected)
        {
            if (selected || !requested.HasValue) return fallback;
            var a = requested.Value;
            var light = Luminance(a); var back = Luminance(background);
            return (Math.Max(light, back) + 0.05) / (Math.Min(light, back) + 0.05) < 3 ? fallback : a;
        }
        private static double Luminance(Color color)
        {
            Func<byte, double> component = value => value / 255.0 <= 0.04045 ? value / 255.0 / 12.92 : Math.Pow((value / 255.0 + 0.055) / 1.055, 2.4);
            return 0.2126 * component(color.R) + 0.7152 * component(color.G) + 0.0722 * component(color.B);
        }
        public void Draw(Graphics graphics, Rectangle bounds, string text, Font font, Color foreground, Color background, bool selected, bool rtl, ClassicGridFormattingMode mode, string separator = " || ")
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            var key = ((int)mode).ToString(CultureInfo.InvariantCulture) + ":" + separator + ":" + text;
            if (!_cache.TryGetValue(key, out var runs))
            {
                runs = ClassicGridMarkup.Parse(text, mode, separator);
                if (_cache.Count >= 512) _cache.Clear();
                // Do not retain huge arbitrary override blocks in the display cache.
                if (text.Length <= 16384) _cache[key] = runs;
            }
            var saved = graphics.Save();
            graphics.SetClip(bounds, System.Drawing.Drawing2D.CombineMode.Intersect);
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping;
            if (rtl) flags |= TextFormatFlags.RightToLeft | TextFormatFlags.Right;
            // Italic glyph ink may extend beyond its advance width on either side.
            // Keep a cell-level clip, but do not clip each styled run to its advance.
            var bearing = Math.Max(1, (int)Math.Ceiling(font.GetHeight(graphics) / 6f));
            var x = rtl ? bounds.Right - bearing : bounds.Left + bearing;
            try
            {
                foreach (var run in runs)
                {
                    var style = font.Style | run.Style;
                    var size = run.Size.HasValue ? Math.Max(8, Math.Min(24, run.Size.Value * 0.8f)) : font.Size;
                    Font styled;
                    try { styled = new Font(string.IsNullOrWhiteSpace(run.Face) ? font.FontFamily.Name : run.Face, size, style, GraphicsUnit.Point); }
                    catch (ArgumentException) { styled = new Font(font.FontFamily, font.Size, font.Style); }
                    using (styled)
                    {
                        var color = ReadableColor(run.Tag ? (Color?)Color.SteelBlue : run.Color, foreground, background, selected);
                        var width = TextRenderer.MeasureText(graphics, run.Text, styled, new Size(int.MaxValue, int.MaxValue), flags).Width;
                        var available = rtl ? x - bounds.Left : bounds.Right - x;
                        if (available <= 0) break;
                        var rectangle = new Rectangle(rtl ? Math.Max(bounds.Left, x - width) : x, bounds.Top, Math.Min(width, available), bounds.Height);
                        TextRenderer.DrawText(graphics, run.Text, styled, rectangle, color, flags | TextFormatFlags.NoClipping | (width > available ? TextFormatFlags.EndEllipsis : 0));
                        if (width > available) break;
                        x += rtl ? -width : width;
                    }
                }
            }
            finally { graphics.Restore(saved); }
        }
    }
}
