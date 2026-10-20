using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;

class TestClassicNavigation
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static object Get(object o, string n) => o.GetType().GetField(n, B).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, B).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethods(B).First(m => m.Name == n && m.GetParameters().Length == args.Length).Invoke(o, args);
    static void Check(bool value, string text) { if (!value) throw new Exception(text); checks++; Console.WriteLine("PASS " + text); }
    static IEnumerable<ToolStripMenuItem> Menus(ToolStripItemCollection items)
    {
        foreach (var item in items.OfType<ToolStripMenuItem>()) { yield return item; foreach (var sub in Menus(item.DropDownItems)) yield return sub; }
    }
    static ToolStripMenuItem Find(ToolStripItemCollection items, string name) => Menus(items).First(m => m.Name == name);
    static void Opening(ToolStripMenuItem item) => typeof(ToolStripMenuItem).GetMethod("OnDropDownShow", B).Invoke(item, new object[] { EventArgs.Empty });
    static List<ToolStripMenuItem> Commands(Main main) => Menus(((MenuStrip)Get(main, "menuStrip1")).Items)
        .Concat(Menus(((ContextMenuStrip)Get(main, "contextMenuStripListView")).Items))
        .Concat(Menus(((ContextMenuStrip)Get(main, "contextMenuStripWaveform")).Items)).ToList();
    static void Setup(Main main, Subtitle sub, SubtitleFormat format)
    {
        Set(main, "_subtitle", sub); Set(main, "_currentSubtitleFormat", format); sub.OriginalFormat = format;
        var grid = (SubtitleListView)Get(main, "SubtitleListview1");
        Call(grid, "Fill", sub, null); grid.SelectIndexAndEnsureVisible(0);
        Call(main, "RefreshClassicForcedAvailability"); Call(main, "RefreshClassicAssistedAvailability");
    }
    static void Shot(ToolStripMenuItem item, string file)
    {
        Opening(item);
        var drop = item.DropDown; drop.Show(new Point(40, 40)); Application.DoEvents();
        using (var image = new Bitmap(drop.Width, drop.Height)) { drop.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(file); }
        drop.Close();
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            var root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
            foreach (var field in new[] { "DataDirectory", "SettingsFileName", "WaveformsDirectory", "AutoBackupDirectory", "DictionariesDirectory" })
                typeof(Configuration).GetField(field).SetValue(null, field == "SettingsFileName" ? Path.Combine(root, "Settings.xml") : root + Path.DirectorySeparatorChar);
            Configuration.Settings.General.CheckForUpdates = false; Configuration.Settings.General.AutoSave = false;
            Configuration.Settings.General.Language = "ru-RU";
            using (var main = new Main())
            {
                main.ShowInTaskbar = false; main.StartPosition = FormStartPosition.Manual; main.Location = new Point(-30000, -30000); main.Show(); Application.DoEvents();
                var mainMenu = (MenuStrip)Get(main, "menuStrip1"); var rowMenu = (ContextMenuStrip)Get(main, "contextMenuStripListView");
                var before = File.ReadAllLines(args[1]).Where(l => !string.IsNullOrWhiteSpace(l) && (!l.Contains("|") || l.EndsWith(" | ToolStripMenuItem")))
                    .Select(l => l.Split('|')[0].Trim().Split('/').Last()).Where(n => n.Length > 0).Distinct().ToArray();
                var commands = Commands(main); var missing = before.Where(n => !commands.Any(m => m.Name == n)).ToArray();
                Check(missing.Length == 0, "all " + before.Length + " named commands from previous menus remain reachable: " + string.Join(",", missing));
                foreach (var n in new[] { "toolsToolStripMenuItem", "toolStripMenuItemVideo", "optionsToolStripMenuItem" })
                    Check(((ToolStripMenuItem)Get(main, n)).DropDownItems.OfType<ToolStripMenuItem>().Count(m => m.Available) <= 9, n + " has at most nine top-level entries");
                Check(Find(mainMenu.Items, "toolStripMenuItemExport").DropDownItems.OfType<ToolStripMenuItem>().Count() == 5, "all export formats grouped in five categories");
                Check(Find(mainMenu.Items, "fileToolStripMenuItem").DropDownItems.OfType<ToolStripMenuItem>().Last().Name == "exitToolStripMenuItem", "Exit remains the last File command");
                Check(ReferenceEquals(Get(main, "saveToolStripMenuItem"), Find(mainMenu.Items, "saveToolStripMenuItem")), "Classic Save instance retained");
                var file = (ToolStripMenuItem)Get(main, "fileToolStripMenuItem");
                Check(file.DropDownItems.IndexOf((ToolStripItem)Get(main, "_classicForcedExport")) == file.DropDownItems.IndexOf((ToolStripItem)Get(main, "saveAsToolStripMenuItem")) + 1, "Forced export stays next to Save As");
                var canonical = commands.Where(m => before.Contains(m.Name)).GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.First());
                var keys = canonical.ToDictionary(p => p.Key, p => p.Value.ShortcutKeys);
                Call(main, "InitializeLanguage"); Call(main, "InitializeLanguage"); commands = Commands(main);
                Check(commands.GroupBy(m => m.Name).Where(g => g.Key.Length > 0).All(g => g.Count() == 1), "language refresh does not duplicate menu IDs");
                Check(canonical.All(p => !p.Value.IsDisposed && commands.Contains(p.Value)), "language refresh retains command instances");
                Check(canonical.All(p => p.Value.ShortcutKeys == keys[p.Key]), "language refresh retains shortcut assignments");
                var sub = new Subtitle(); sub.Paragraphs.Add(new Paragraph("First cue.", 1000, 2000)); sub.Paragraphs.Add(new Paragraph("Second cue.", 3000, 4000)); sub.Renumber();
                Setup(main, sub, new SubRip());
                Call(main, "RefreshClassicNavigation");
                var quick = Find(mainMenu.Items, "classicQuickColumns"); Opening(quick);
                Check(quick.DropDownItems.OfType<ToolStripMenuItem>().Count() == ClassicColumnPreference.DefaultOrder.Length, "quick columns cover every configured column");
                Check(!Find(quick.DropDownItems, "classicColumn_Number").Enabled && !Find(quick.DropDownItems, "classicColumn_Network").Enabled, "required and managed columns cannot be toggled");
                Find(quick.DropDownItems, "classicColumn_End").PerformClick();
                Check(!Configuration.Settings.Tools.ListViewShowColumnEndTime && ((SubtitleListView)Get(main, "SubtitleListview1")).ColumnIndexEnd < 0, "quick column updates legacy Settings and actual list");
                using (var header = (ContextMenuStrip)Call(main, "CreateClassicHeaderMenu"))
                {
                    var headerQuick = Find(header.Items, "classicHeaderQuickColumns");
                    Check(!Find(headerQuick.DropDownItems, "classicColumn_End").Checked, "header reads the same End preference");
                    Find(headerQuick.DropDownItems, "classicColumn_End").PerformClick();
                    Find(headerQuick.DropDownItems, "classicColumn_ShotIn").PerformClick();
                    Find(headerQuick.DropDownItems, "classicColumn_Layer").PerformClick();
                    Check(Configuration.Settings.Tools.ListViewShowColumnEndTime, "header restores the same legacy End setting");
                    var saved = (IEnumerable<ClassicColumnPreference>)Call(main, "CaptureClassicColumns");
                    Check(saved.Single(p => p.Key == "ShotIn").Visible && saved.Single(p => p.Key == "Layer").Visible, "header saves new shot and layer preferences");
                    Check(Menus(header.Items).Any(m => m.Name == "classicHeaderFormatting") && Menus(header.Items).Any(m => m.Name == "classicHeaderSpelling"), "header has formatting and spelling access");
                    Check(Menus(header.Items).Any(m => m.Name == "classicHeaderFont") && Menus(header.Items).Any(m => m.Name == "classicHeaderHighlight"), "header links to existing font and error-highlighting pages");
                }
                var toggleForced = (ToolStripMenuItem)Get(main, "_classicForcedToggle"); toggleForced.PerformClick();
                Check(sub.Paragraphs[0].Forced && !sub.Paragraphs[1].Forced, "Forced action changes only selected cue metadata");
                Opening(quick); var forcedColumn = (ToolStripMenuItem)Get(main, "_classicForcedColumn"); forcedColumn.PerformClick();
                Check(sub.Paragraphs[0].Forced && ((SubtitleListView)Get(main, "SubtitleListview1")).ColumnIndexForced < 0, "hiding Forced column does not remove cue marks");
                var gridContext = Find(rowMenu.Items, "classicGridSettingsContext"); var alias = Find(gridContext.DropDownItems, "classicGridAppearanceContext"); Opening(alias);
                alias.DropDownItems[2].PerformClick();
                Check(((SubtitleListView)Get(main, "SubtitleListview1")).GridFormattingMode == ClassicGridFormattingMode.HideTags, "context formatting executes canonical formatting action");
                Check(((ToolStripMenuItem)Get(main, "_classicGridAppearanceMenu")).DropDownItems.OfType<ToolStripMenuItem>().ElementAt(2).Checked, "canonical formatting reflects context change");
                Configuration.Settings.Tools.ListViewShowColumnEndTime = false; Call(main, "RefreshClassicColumnsFromLegacySettings");
                using (var header = (ContextMenuStrip)Call(main, "CreateClassicHeaderMenu"))
                    Check(!Find(header.Items, "classicColumn_End").Checked, "legacy Settings changes appear in header");
                Setup(main, sub, new AdvancedSubStationAlpha());
                var prefs = (List<ClassicColumnPreference>)Call(main, "CaptureClassicColumns"); Call(main, "ApplyClassicColumns", prefs, false);
                Check(((SubtitleListView)Get(main, "SubtitleListview1")).ColumnIndexLayer >= 0, "saved ASS layer preference becomes visible in ASS");
                var palette = (IEnumerable<ClassicPaletteCommand>)Call(main, "BuildClassicPaletteCommands");
                Check(palette.Any(c => c.Id == "menu:" + ClassicCommandPalette.Clean(((ToolStripMenuItem)Get(main, "toolsToolStripMenuItem")).Text) + "/" + ClassicCommandPalette.Clean("Улучшить тайминги по речи…")), "previous unnamed timing command ID remains available for custom steps");
                Check(palette.Any(c => c.Id == "menu:classicShowForcedColumn"), "Forced-column custom shortcut ID remains available");
                Set(main, "_gridSpellBypass", true); Set(main, "_gridSpellingMenuPoint", new Point(50, 45));
                typeof(ContextMenuStrip).GetMethod("OnOpening", B).Invoke(rowMenu, new object[] { new CancelEventArgs() });
                Check(ReferenceEquals(Get(main, "setActorForSelectedLinesToolStripMenuItem"), Find(rowMenu.Items, "setActorForSelectedLinesToolStripMenuItem")), "dynamic ASS context retains original actor command");
                Check(Find(rowMenu.Items, "toolStripMenuItemSelectedLines").DropDownItems.Count > 0, "dynamic selected-line actions remain populated");
                using (var settings = new Nikse.SubtitleEdit.Forms.Options.Settings(main))
                {
                    foreach (var page in new[] { Tuple.Create("appearance", 9), Tuple.Create("syntax", 3), Tuple.Create("shortcuts", 2), Tuple.Create("waveform", 5) })
                    {
                        Call(settings, "SelectNavigationSection", page.Item1);
                        Check((int)Get(settings, "listBoxSection").GetType().GetProperty("SelectedIndex").GetValue(Get(settings, "listBoxSection")) == page.Item2, "existing Settings page opens directly: " + page.Item1);
                    }
                }
                var timingId = palette.Single(c => c.Name == ClassicCommandPalette.Clean("Улучшить тайминги по речи…")).Id;
                Call(main, "SetLanguage", "en-US");
                Check(Find(mainMenu.Items, "classicToolsTiming").Text == "Timing and gaps" && Find(mainMenu.Items, "classicFfmpegPlayer").Text.StartsWith("FFmpeg player"), "new groups and player options follow English language selection");
                Check(((IEnumerable<ClassicPaletteCommand>)Call(main, "BuildClassicPaletteCommands")).Any(c => c.Id == timingId), "custom command ID survives a language change");
                Call(main, "SetLanguage", "ru-RU");
                Check(Find(mainMenu.Items, "classicToolsTiming").Text == "Тайминги и интервалы", "new groups return to Russian without reopening");
                Shot((ToolStripMenuItem)Get(main, "toolsToolStripMenuItem"), Path.Combine(root, "tools.png"));
                Shot((ToolStripMenuItem)Get(main, "toolStripMenuItemVideo"), Path.Combine(root, "video.png"));
                Shot(Find(mainMenu.Items, "classicGridSettings"), Path.Combine(root, "grid-settings.png"));
                Shot(Find(mainMenu.Items, "toolStripMenuItemExport"), Path.Combine(root, "export.png"));
                using (var header = (ContextMenuStrip)Call(main, "CreateClassicHeaderMenu"))
                {
                    header.Show(new Point(40, 40)); Application.DoEvents();
                    using (var image = new Bitmap(header.Width, header.Height)) { header.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(root, "header.png")); }
                    header.Close(); Application.DoEvents();
                }
            }
            using (var reopened = new Main())
            {
                var saved = (List<ClassicColumnPreference>)Call(reopened, "CaptureClassicColumns");
                Check(saved.Single(p => p.Key == "Layer").Visible && saved.Single(p => p.Key == "ShotIn").Visible, "column choices survive restart");
            }
            Console.WriteLine("PASS TOTAL " + checks);
            // Some native settings helpers retain foreground workers after the form
            // is disposed. All fixture scopes have finished; terminate the test host.
            Environment.Exit(0);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.Exit(1); return 1; }
    }
}


