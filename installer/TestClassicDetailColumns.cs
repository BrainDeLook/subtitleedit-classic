using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using C = Nikse.SubtitleEdit.Controls.SubtitleListView.SubtitleColumn;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicDetailColumns
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static object Field(object o, string n) => o.GetType().GetField(n, F).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, F).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethods(F).Single(m => m.Name == n && m.GetParameters().Length == args.Length && m.GetParameters().Select((p,i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(v => v)).Invoke(o, args);
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS: " + name); }
    static void Show(Form f) { f.Location = new Point(-20000, -20000); f.StartPosition = FormStartPosition.Manual; f.ShowInTaskbar = false; f.Show(); }
    static string Cell(SubtitleListView g, C c, int row = 0) => g.Items[row].SubItems[g.GetColumnIndex(c)].Text;
    [STAThread] static int Main(string[] args)
    {
        var root = Path.Combine(Path.GetFullPath(args[0]), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        typeof(Configuration).GetField("DataDirectory").SetValue(null, root + Path.DirectorySeparatorChar);
        typeof(Configuration).GetField("SettingsFileName").SetValue(null, Path.Combine(root, "Settings.xml"));
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); Application.EnableVisualStyles(); var exit = 0;
        using (var anchor = new Form { ShowInTaskbar = false, Location = new Point(-30000, -30000), StartPosition = FormStartPosition.Manual })
        {
            anchor.Shown += async (s, e) => { try { await Run(root); } catch (Exception ex) { Console.Error.WriteLine(ex); exit = 1; } finally { anchor.Close(); } };
            Application.Run(anchor);
        }
        Console.WriteLine("FEATURE CHECKS: " + checks); return exit;
    }
    static async Task Run(string root)
    {
        LanguageSettings.Current = new Language(); var general = Configuration.Settings.General;
        general.MeasureFontName = "Arial"; general.MeasureFontSize = 24; general.MeasureFontBold = false;
        Configuration.Settings.Tools.ListViewSyntaxColorWideLines = true;
        using (var g = new SubtitleListView())
        {
            g.ShowDetailColumn(C.Layer, "Layer"); g.ShowDetailColumn(C.PixelWidth, "Pixel width");
            var sub = new Subtitle(); var p = new Paragraph("<i>Hello</i>" + Environment.NewLine + "World wide", 1000, 3000) { Layer = 7, Actor = "Alice", Forced = true }; sub.Paragraphs.Add(p); sub.Renumber();
            Call(g, "Fill", sub, null);
            var expected = TextWidth.CalcPixelWidth("World wide");
            Check(Cell(g, C.Layer) == "7", "ASS layer appears independently of cue number");
            Check(Cell(g, C.PixelWidth) == expected.ToString(), "Pixel width is the longest stripped line rather than the sum");
            p.Text = "{\\i1}World wide{\\i0}"; g.SetTimeAndText(0, p, null);
            Check(Cell(g, C.PixelWidth) == expected.ToString(), "ASS formatting does not count towards width");
            p.Text = "<b>World wide</b>"; g.SetText(0, p.Text);
            Check(Cell(g, C.PixelWidth) == expected.ToString(), "HTML formatting does not count towards width");
            p.Text = "日本語 и русский"; g.SetText(0, p.Text);
            Check(int.Parse(Cell(g, C.PixelWidth)) > 0, "Unicode CJK and Cyrillic text measures successfully");
            p.Text = ""; g.SetText(0, p.Text); Check(Cell(g, C.PixelWidth) == "0", "Empty cue width is zero");
            p.Text = "World wide"; g.SetText(0, p.Text); general.SubtitleLineMaximumPixelWidth = expected - 1; g.RefreshDetailColumns();
            Check(g.Items[0].SubItems[g.ColumnIndexPixelWidth].BackColor == Configuration.Settings.Tools.ListViewSyntaxErrorColor, "Excess width is highlighted");
            general.SubtitleLineMaximumPixelWidth = expected; g.RefreshDetailColumns();
            Check(g.Items[0].SubItems[g.ColumnIndexPixelWidth].BackColor == g.BackColor, "Exact width limit is accepted");
            general.SubtitleLineMaximumPixelWidth = 1; Configuration.Settings.Tools.ListViewSyntaxColorWideLines = false; g.RefreshDetailColumns();
            Check(g.Items[0].SubItems[g.ColumnIndexPixelWidth].BackColor == g.BackColor, "Disabling width syntax coloring clears warning");
            Configuration.Settings.Tools.ListViewSyntaxColorWideLines = true;
            general.MeasureFontSize = 48; g.RefreshDetailColumns(); var larger = int.Parse(Cell(g, C.PixelWidth));
            Check(larger > expected, "Measurement font-size changes invalidate cached widths");
            general.MeasureFontBold = true; g.RefreshDetailColumns();
            Check(Cell(g, C.PixelWidth) == TextWidth.CalcPixelWidth(p.Text).ToString(), "Measurement bold changes invalidate cached widths");
            general.MeasureFontName = "Courier New"; g.RefreshDetailColumns();
            Check(Cell(g, C.PixelWidth) == TextWidth.CalcPixelWidth(p.Text).ToString(), "Measurement font-family changes invalidate cached widths");
            p.Layer = -12; g.SetTimeAndText(0, p, null); Check(Cell(g, C.Layer) == "-12", "Negative layers update after edits");
            g.HideColumn(C.Layer); Call(g, "Fill", sub, null); Check(g.ColumnIndexLayer < 0 && int.Parse(Cell(g, C.PixelWidth)) > 0, "Pixel column works with Layer hidden");
            g.ShowDetailColumn(C.Layer, "Layer"); Check(Cell(g, C.Layer) == "-12" && g.Items[0].SubItems.Count == g.Columns.Count, "Restoring Layer keeps subitems aligned");
            g.HideColumn(C.PixelWidth); p.Text = "Changed while hidden"; g.SetTimeAndText(0, p, null); g.ShowDetailColumn(C.PixelWidth, "Pixel width");
            Check(Cell(g, C.PixelWidth) == TextWidth.CalcPixelWidth(p.Text).ToString(), "Restoring width measures the current text");
            Check(p.StartTime.TotalMilliseconds == 1000 && p.EndTime.TotalMilliseconds == 3000 && p.Actor == "Alice" && p.Forced, "Presentation updates preserve timing and metadata");
            var big = new Subtitle(); for (int i = 0; i < 10000; i++) big.Paragraphs.Add(new Paragraph("Cue " + i, i * 1000, i * 1000 + 500)); Call(g, "Fill", big, null);
            var watch = Stopwatch.StartNew(); g.RefreshDetailColumns(); watch.Stop();
            Check(watch.ElapsedMilliseconds < 2000, "Ten thousand cached widths refresh within two seconds (" + watch.ElapsedMilliseconds + " ms)");
        }
        foreach (var language in new[] { "en-US", "ru-RU" })
        {
            Configuration.Settings.General.Language = language;
            using (var main = new Nikse.SubtitleEdit.Forms.Main())
            {
                Show(main); await Task.Delay(120); var g = (SubtitleListView)Field(main, "SubtitleListview1");
                var source = new Subtitle { Header = AdvancedSubStationAlpha.DefaultHeader }; source.Paragraphs.Add(new Paragraph("<i>Layered cue</i>", 1000, 3000) { Layer = 3, Extra = "Default" }); source.Renumber(); Set(main, "_subtitle", source);
                var prefs = ClassicColumnPreference.Normalize(null, c => c == C.Start || c == C.End || c == C.Duration);
                Call(main, "ApplyClassicColumns", prefs.Where(p => p.Key != "Layer" && p.Key != "PixelWidth").ToList(), false);
                Check(g.ColumnIndexLayer < 0 && g.ColumnIndexPixelWidth < 0, "Old layouts leave new columns hidden: " + language);
                prefs.Single(p => p.Key == "Layer").Visible = prefs.Single(p => p.Key == "PixelWidth").Visible = true;
                Call(main, "SetCurrentFormat", new AdvancedSubStationAlpha().FriendlyName); Call(main, "ApplyClassicColumns", prefs, true);
                Check(g.ColumnIndexLayer >= 0 && Cell(g, C.Layer) == "3" && int.Parse(Cell(g, C.PixelWidth)) > 0, "ASS enables both new columns: " + language);
                g.SelectIndexAndEnsureVisible(0); Call(main, "SetLayer", new ToolStripMenuItem("8"), EventArgs.Empty);
                Check(source.Paragraphs[0].Layer == 8 && Cell(g, C.Layer) == "8", "Existing layer context command updates the grid immediately: " + language);
                Call(main, "SetCurrentFormat", new SubRip().FriendlyName);
                Check(g.ColumnIndexLayer < 0 && g.ColumnIndexPixelWidth >= 0, "SRT hides ASS-only Layer and retains width: " + language);
                Call(main, "SetCurrentFormat", new AdvancedSubStationAlpha().FriendlyName);
                Check(g.ColumnIndexLayer >= 0 && g.Items[0].SubItems.Count == g.Columns.Count, "Returning to ASS restores the saved Layer preference: " + language);
                Call(main, "SyncClassicDetailColumns", new SubStationAlpha()); Check(g.ColumnIndexLayer < 0, "SSA Marked is not presented as an ASS layer: " + language);
                Call(main, "SyncClassicDetailColumns", new Ebu());
                Check(g.ColumnIndexPixelWidth >= 0 && g.ColumnIndexLayer < 0, "Binary EBU STL still supports text width: " + language);
                Call(main, "SyncClassicDetailColumns", new Idx());
                Check(g.ColumnIndexPixelWidth < 0 && g.ColumnIndexLayer < 0, "VobSub index hides both text metadata columns: " + language);
                Call(main, "SyncClassicDetailColumns", new AdvancedSubStationAlpha());
                using (var dialog = new ClassicGridColumns(prefs, c => (string)Call(main, "ColumnLabel", c), c => (string)Call(main, "ColumnNote", c), () => prefs))
                {
                    Show(dialog); dialog.Size = dialog.MinimumSize;
                    var table = (DataGridView)Field(dialog, "_grid"); table.FirstDisplayedScrollingRowIndex = prefs.FindIndex(p => p.Key == "Layer");
                    using (var bmp = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(bmp, new Rectangle(Point.Empty, dialog.Size)); bmp.Save(Path.Combine(root, language + ".png")); }
                    Check(table.Rows.Count == Enum.GetValues(typeof(C)).Length, "Columns dialog includes every new type: " + language);
                }
                var hidden = prefs.Select(p => p.Copy()).ToList(); hidden.Single(p => p.Key == "Layer").Visible = false; Call(main, "ApplyClassicColumns", hidden, true);
                Call(main, "SyncClassicDetailColumns", new AdvancedSubStationAlpha()); Check(g.ColumnIndexLayer < 0, "Format refresh respects explicitly hidden Layer: " + language);
            }
            using (var reopened = new Nikse.SubtitleEdit.Forms.Main())
            { var g = (SubtitleListView)Field(reopened, "SubtitleListview1"); Check(g.ColumnIndexLayer < 0 && g.ColumnIndexPixelWidth >= 0, "Restart restores independent column visibility: " + language); }
        }
    }
}
