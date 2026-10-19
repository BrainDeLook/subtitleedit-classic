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
using Profile = Nikse.SubtitleEdit.Core.Settings.BeautifyTimeCodesSettings.BeautifyTimeCodesProfile;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicShotColumns
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static object Field(object o, string n) => o.GetType().GetField(n, F).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, F).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethods(F).Single(m => m.Name == n && m.GetParameters().Length == args.Length && m.GetParameters().Select((p,i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(v => v)).Invoke(o, args);
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS: " + name); }
    static void Show(Form f) { f.Location = new Point(-20000, -20000); f.StartPosition = FormStartPosition.Manual; f.ShowInTaskbar = false; f.Show(); }
    static string Text(SubtitleListView g, bool start = true) => g.Items[0].SubItems[start ? g.ColumnIndexShotIn : g.ColumnIndexShotOut].Text;
    static bool Warn(SubtitleListView g, bool start = true) => g.Items[0].SubItems[start ? g.ColumnIndexShotIn : g.ColumnIndexShotOut].BackColor == Configuration.Settings.Tools.ListViewSyntaxErrorColor;
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
        LanguageSettings.Current = new Language();
        var profile = new Profile(Profile.Preset.Netflix);
        var shots = new List<double> { 1, 3, 8 };
        using (var g = new SubtitleListView())
        {
            g.ShowShotColumn(C.ShotIn, "Shot in"); g.ShowShotColumn(C.ShotOut, "Shot out");
            var sub = new Subtitle(); var cue = new Paragraph("<i>Unchanged</i>", 900, 3100); sub.Paragraphs.Add(cue); sub.Renumber(); Call(g, "Fill", sub, null);
            Action<double, double, double, bool> update = (start, end, fps, frames) => { cue.StartTime.TotalMilliseconds = start; cue.EndTime.TotalMilliseconds = end; g.UpdateShotChangeOffsets(shots, profile, fps, frames); };
            update(900, 3100, 25, false);
            Check(Text(g) == "-100" && Text(g, false) == "+100", "Signed milliseconds: before start cut and after end cut");
            Check(Warn(g) && Warn(g, false), "Both boundaries use timing-profile warnings");
            update(1000, 2920, 25, true);
            Check(Text(g) == "0" && Text(g, false) == "-2" && !Warn(g) && !Warn(g, false), "Correct in/out gap targets do not warn");
            update(1480, 2520, 25, true);
            Check(Text(g) == "+12" && Text(g, false) == "-12" && !Warn(g) && !Warn(g, false), "Green outer edges are exclusive");
            update(1440, 2560, 25, true);
            Check(Warn(g) && Warn(g, false), "Inside green zones warns");
            profile.InCuesLeftGreenZone = profile.InCuesRightGreenZone = 0;
            update(720, 2000, 25, true);
            Check(Text(g) == "-7" && Warn(g), "Red edge remains inclusive without a green zone");
            profile = new Profile(Profile.Preset.Netflix);
            update(1200, 2000, 25, true);
            Check(Text(g, false) == "-25", "Equidistant cuts choose the later cut as stable 5.3 does");
            update(2000, 5000, 25, true);
            Check(Text(g) == "-25" && Text(g, false) == "" && !Warn(g, false), "One-second cutoff includes boundary and clears distant offsets");
            profile.InCuesRightGreenZone = 40;
            update(4520, 5000, 25, true);
            Check(Text(g) == "+38" && Warn(g), "Profile wider than one second expands the lookup window");
            update(1009, 2991, 23.976, true);
            Check(Text(g) == "0" && Text(g, false) == "0", "Fractional frame rate rounds cue and cut separately");
            update(1000.5, 2999.5, 25, false);
            Check(Text(g) == "+1" && Text(g, false) == "-1", "Millisecond half values round away from zero");
            g.UpdateShotChangeOffsets(null, profile, 25, false);
            Check(Text(g) == "" && Text(g, false) == "" && !Warn(g), "Removing shot data clears values and warning color");
            g.UpdateShotChangeOffsets(new List<double>(), profile, 25, false);
            Check(Text(g) == "", "Empty list is safe");
            g.UpdateShotChangeOffsets(shots, profile, 0, false); Check(Text(g) == "", "Invalid frame rate is safe");
            update(1040, 2960, 25, false); Call(g, "Fill", sub, null);
            Check(Text(g) == "+40" && Text(g, false) == "-40", "Refilling rows uses the active shot context immediately");
            cue.StartTime.TotalMilliseconds = 1080; g.SetTimeAndText(0, cue, null);
            Check(Text(g) == "+80", "Timing edits update offsets without waiting for the timer");
            g.HideColumn(C.ShotIn); Call(g, "Fill", sub, null); Check(g.ColumnIndexShotIn < 0 && Text(g, false) == "-40", "Either column can be hidden independently");
            g.ShowShotColumn(C.ShotIn, "Shot in");
            Check(Text(g) == "+80" && g.Items[0].SubItems.Count == g.Columns.Count, "Showing a column restores aligned values");
            shots.Clear(); shots.Add(1.08); g.UpdateShotChangeOffsets(shots, profile, 25, false);
            Check(Text(g) == "0", "In-place shot-list changes refresh");
            Check(cue.Text == "<i>Unchanged</i>" && cue.EndTime.TotalMilliseconds == 2960, "Display never changes cue text or timing");
            var large = new Subtitle(); for (int i = 0; i < 10000; i++) large.Paragraphs.Add(new Paragraph("Cue", i * 1000, i * 1000 + 500));
            var manyCuts = Enumerable.Range(0, 20000).Select(i => i * .6).ToList(); Call(g, "Fill", large, null);
            var sw = Stopwatch.StartNew(); g.UpdateShotChangeOffsets(manyCuts, profile, 25, false); sw.Stop();
            Check(sw.ElapsedMilliseconds < 3000 && g.Items.Count == 10000, "Ten thousand cues refresh within three seconds (" + sw.ElapsedMilliseconds + " ms)");
        }
        foreach (var language in new[] { "en-US", "ru-RU" })
        {
            Configuration.Settings.General.Language = language;
            using (var main = new Nikse.SubtitleEdit.Forms.Main())
            {
                Show(main); await Task.Delay(120);
                var g = (SubtitleListView)Field(main, "SubtitleListview1"); var visualizer = (AudioVisualizer)Field(main, "audioVisualizer");
                var source = new Subtitle(); source.Paragraphs.Add(new Paragraph("Cue", 900, 3100)); source.Renumber(); Set(main, "_subtitle", source);
                var prefs = ClassicColumnPreference.Normalize(null, c => c == C.Start || c == C.End || c == C.Duration);
                // Migration must not enable newly introduced keys in an existing layout.
                Call(main, "ApplyClassicColumns", prefs.Where(p => p.Key != "ShotIn" && p.Key != "ShotOut").ToList(), false);
                Check(g.ColumnIndexShotIn < 0 && g.ColumnIndexShotOut < 0, "Older layouts keep new columns hidden: " + language);
                prefs.Single(p => p.Key == "ShotIn").Visible = prefs.Single(p => p.Key == "ShotOut").Visible = true;
                Call(main, "ApplyClassicColumns", prefs, true); visualizer.ShotChanges = new List<double> { 1, 3 };
                Call(main, "RefreshClassicShotColumns"); Check(Text(g) == "", "No video means no shot offset: " + language);
                Set(main, "_videoFileName", Path.Combine(root, "fixture.mkv")); Configuration.Settings.General.CurrentFrameRate = 25;
                Configuration.Settings.General.UseTimeFormatHHMMSSFF = false; Call(main, "RefreshClassicShotColumns");
                Check(Text(g) == "-100" && Text(g, false) == "+100", "Main wires active video and shot list: " + language);
                Configuration.Settings.General.UseTimeFormatHHMMSSFF = true; Call(main, "RefreshClassicShotColumns");
                Check(Text(g) == "-2" && Text(g, false) == "+3", "Editor frame mode changes both displays: " + language);
                visualizer.ShotChanges.Clear(); Call(main, "RefreshClassicShotColumns"); Check(Text(g) == "" && Text(g, false) == "", "Clearing editor shot data clears both cells: " + language);
                using (var dialog = new ClassicGridColumns(prefs, c => (string)Call(main, "ColumnLabel", c), c => (string)Call(main, "ColumnNote", c), () => prefs))
                { Show(dialog); dialog.Size = dialog.MinimumSize; var table = (DataGridView)Field(dialog, "_grid"); table.FirstDisplayedScrollingRowIndex = prefs.FindIndex(p => p.Key == "ShotIn"); using (var bmp = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(bmp, new Rectangle(Point.Empty, dialog.Size)); bmp.Save(Path.Combine(root, language + ".png")); } }
                Set(main, "_videoFileName", null);
            }
            using (var reopened = new Nikse.SubtitleEdit.Forms.Main())
            { var g = (SubtitleListView)Field(reopened, "SubtitleListview1"); Check(g.ColumnIndexShotIn >= 0 && g.ColumnIndexShotOut >= 0, "Restart restores shot-column visibility: " + language); }
        }
    }
}
