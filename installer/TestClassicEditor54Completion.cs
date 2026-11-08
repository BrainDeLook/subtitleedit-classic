using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Forms.Options;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.VideoPlayers;
using Nikse.SubtitleEdit.Logic.CommandLineConvert;

class TestClassicEditor54Completion
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks;
    static bool testClipboard;
    static object Get(object target, string name) => target.GetType().GetField(name, B).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, B).SetValue(target, value);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(B).First(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
    static Subtitle Sample() { var result = new Subtitle(); for (int i = 0; i < 3; i++) result.Paragraphs.Add(new Paragraph("Cue " + i, 10000 + i * 10000, 12000 + i * 10000) { Number = i + 1 }); return result; }
    sealed class DelayedPlayer : VideoPlayer
    {
        public double Actual = 10, Target; public bool Paused = true;
        public override string PlayerName => "Delayed offline fixture";
        public override int Volume { get; set; }
        public override double Duration => 100;
        public override double CurrentPosition { get => Actual; set => Target = value; }
        public override bool IsPaused => Paused;
        public override bool IsPlaying => !Paused;
        public override void Play() { Paused = false; }
        public override void Pause() { Paused = true; }
        public override void Stop() { Paused = true; Actual = 0; }
        public override void Initialize(Control owner, string file, EventHandler loaded, EventHandler ended) { }
        public override void DisposeVideoPlayer() { }
        public override event EventHandler OnVideoLoaded { add { } remove { } }
        public override event EventHandler OnVideoEnded { add { } remove { } }
    }

    [STAThread] static int Main(string[] args)
    {
        testClipboard = args.Contains("--clipboard");
        var result = 1;
        try { Run(); Console.WriteLine("RESULT " + checks + " passed 0 failed"); result = 0; }
        catch (Exception error) { Console.Error.WriteLine(error); }
        Environment.Exit(result);
        return result;
    }
    static void Run()
    {
        var data = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "editor-data-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(data);
        foreach (var name in new[] { "DataDirectory", "DictionariesDirectory", "WaveformsDirectory", "AutoBackupDirectory", "SettingsFileName" }) typeof(Configuration).GetField(name).SetValue(null, name == "SettingsFileName" ? Path.Combine(data, "Settings.xml") : data + Path.DirectorySeparatorChar);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); Application.EnableVisualStyles();
        Configuration.Settings.General.CheckForUpdates = false; Configuration.Settings.General.AutoSave = false; Configuration.Settings.General.Language = "ru-RU"; Configuration.Settings.WordLists.UseOnlineNames = false;
        Nikse.SubtitleEdit.Logic.ClassicFormats.ClassicFormatRegistry.Register();
        var source = Sample(); var shifted = new Subtitle(source); shifted.AddTimeToAllParagraphs(TimeSpan.FromHours(10));
        Check(!ClassicSecondarySubtitle.ShouldApplyOffset(source, 36000000, 0, 60000), "zero-based second subtitle keeps its times");
        Check(ClassicSecondarySubtitle.ShouldApplyOffset(shifted, 36000000, 0, 60000), "broadcast second subtitle uses matching video offset");
        var prepared = ClassicSecondarySubtitle.Build(shifted, "Arial", 36000000, 60, 1920, 1080, source);
        Check(prepared.Paragraphs[0].StartTime.TotalMilliseconds == 10000 && shifted.Paragraphs[0].StartTime.TotalMilliseconds == 36010000, "second preview offsets a copy, preserving source");
        Check(AdvancedSubStationAlpha.GetSsaStyle("Default", prepared.Header).FontName == "Arial", "second subtitle uses chosen font in actual ASS header");
        var preferences = ClassicEditorPreferences.Load(); preferences.SecondarySubtitleFont = "Arial"; preferences.MoveAllFrameForward = "Alt+F9"; preferences.VideoCookiesBrowser = ClassicCookieBrowser.Firefox; preferences.Save(); preferences.Save();
        Check(ClassicEditorPreferences.Load().VideoCookiesBrowser == ClassicCookieBrowser.Firefox && ClassicEditorPreferences.Load().SecondarySubtitleFont == "Arial", "typed cookies/font preferences survive reload");
        Configuration.Settings.General.FavoriteSubtitleFormats = "MicroDVD;SubRip";
        using (var save = new SaveFileDialog()) { typeof(Nikse.SubtitleEdit.Forms.Main).Assembly.GetType("Nikse.SubtitleEdit.Logic.UiUtil").GetMethod("SetSaveDialogFilter", B).Invoke(null, new object[] { save, new SubRip() }); Check(save.Filter.StartsWith("MicroDVD|*.sub|SubRip|*.srt|"), "Save as places favorites first"); Check(save.FilterIndex == 2, "Save as keeps selected format after reordering"); }
        using (var chooser = new ColorChooser()) Check(chooser.Controls.Find("classicImageColorPicker", true).Length == 1, "image colour picker reachable in existing dialog");
        using (var window = new Form { Bounds = new Rectangle(150, 160, 500, 300) }) ClassicWindowPlacement.Save(window, "SpellCheck");
        using (var window = new Form()) { ClassicWindowPlacement.Restore(window, "SpellCheck"); Check(window.Location == new Point(150, 160) && window.Size == new Size(500, 300), "spell-check window placement survives recreated window"); }
        var file = Path.Combine(data, "settings-roundtrip.xml");
        Configuration.Settings.General.SyncListViewWithVideoWhilePlaying = true; Configuration.Settings.General.LayoutSizes = "fixture-layout"; Configuration.Settings.General.SubtitleLineMaximumLength = 43;
        Configuration.Settings.General.Profiles.Add(new Nikse.SubtitleEdit.Core.Common.RulesProfile { Name = "Not selected", SubtitleLineMaximumLength = 5 });
        File.WriteAllText(file, Nikse.SubtitleEdit.Core.Settings.Settings.CustomSerialize(Configuration.Settings));
        var restored = Nikse.SubtitleEdit.Core.Settings.Settings.CustomDeserialize(file);
        Check(restored.General.SyncListViewWithVideoWhilePlaying && restored.General.LayoutSizes == "fixture-layout", "Classic settings load retains player-follow and saved layout");
        Check(restored.General.SubtitleLineMaximumLength == 43 && restored.General.CurrentProfile != "Not selected", "unchosen profile does not replace active rules on load");

        using (var main = new Nikse.SubtitleEdit.Forms.Main())
        {
            main.ShowInTaskbar = false; main.StartPosition = FormStartPosition.Manual; main.Location = new Point(-30000, -30000); main.Show(); Application.DoEvents(); main.Location = new Point(-30000, -30000); main.Activate();
            var player = (VideoPlayerContainer)Get(main, "mediaPlayer"); var fake = new DelayedPlayer(); player.VideoPlayer = fake;
            var grid = (SubtitleListView)Get(main, "SubtitleListview1"); var original = Sample(); Set(main, "_subtitleOriginal", original); Set(main, "_currentSubtitleFormat", new SubRip());
            source = Sample(); Set(main, "_subtitle", source); Call(grid, "Fill", source, original); grid.SelectedIndices.Clear(); grid.Items[1].Selected = true; grid.Items[2].Selected = true; Call(main, "ResetHistory");
            var prefs = (ClassicEditorPreferences)Get(main, "_classicEditorPreferences"); prefs.MoveFollowingFrameForward = "Alt+F8"; ((ToolStripNikseComboBox)Get(main, "toolStripComboBoxFrameRate")).Text = "25";
            var frameMs = 1000 / main.CurrentFrameRate;
            Check((Keys)typeof(ClassicEditorPreferences).Assembly.GetType("Nikse.SubtitleEdit.Logic.UiUtil").GetMethod("GetKeys", B).Invoke(null, new object[] { prefs.MoveFollowingFrameForward }) == (Keys.Alt | Keys.F8), "frame shortcut parses the configured binding");
            Call(main, "ApplyClassicTimingShortcut", 3);
            var moved = (Subtitle)Get(main, "_subtitle"); Check(moved.Paragraphs[0].StartTime.TotalMilliseconds == 10000 && Math.Abs(moved.Paragraphs[1].StartTime.TotalMilliseconds - 20000 - frameMs) < .001 && Math.Abs(moved.Paragraphs[2].StartTime.TotalMilliseconds - 30000 - frameMs) < .001, "following-frame shortcut moves full intended scope");
            Check(original.Paragraphs[1].StartTime.TotalMilliseconds == 20000 && grid.SelectedIndices.Count == 2, "frame edit retains original times and multi-selection");
            main.Location = new Point(-30000, -30000);
            Configuration.Settings.General.MoveVideo100Or500MsPlaySmallSample = false;
            Call(main, "MoveVideoSeconds", .5); Call(main, "MoveVideoSeconds", .5); Call(main, "MoveVideoSeconds", .5);
            Check(Math.Abs(fake.Target - 11.5) < .001, "repeated relative seeks accumulate while native position lags");
            player.CurrentPosition = 40; fake.Actual = 40; Call(main, "MoveVideoSeconds", .5);
            Check(Math.Abs(fake.Target - 40.5) < .001, "manual jump resets relative seek target");
            player.Play(); fake.Actual = 42; player.Pause(); Call(main, "MoveVideoSeconds", .5);
            Check(Math.Abs(fake.Target - 42.5) < .001, "play/pause resets relative seek target");
            Configuration.Settings.General.MoveVideo100Or500MsPlaySmallSample = true;
            var watch = Stopwatch.StartNew(); Call(main, "MoveVideoSeconds", .5); watch.Stop(); Check(watch.ElapsedMilliseconds < 90, "small-sample seek never blocks UI for a sleep");
            var until = DateTime.UtcNow.AddMilliseconds(220); while (DateTime.UtcNow < until) { Application.DoEvents(); System.Threading.Thread.Sleep(5); }
            Check(fake.Paused && fake.Target > 40, "small sample pauses at target instead of stopping at zero"); Configuration.Settings.General.MoveVideo100Or500MsPlaySmallSample = false;

            // Clipboard is shared with the user's desktop; exercise it only in a dedicated run.
            if (testClipboard)
            {
            var copied = Sample(); Call(main, "ClipboardSetText", new SubRip().ToText(copied, "copy"), (double?)36000000);
            copied.AddTimeToAllParagraphs(TimeSpan.FromHours(10)); Configuration.Settings.General.CurrentVideoOffsetInMs = 0; Call(main, "RestoreClassicClipboardTimes", copied);
            Check(copied.Paragraphs[0].StartTime.TotalMilliseconds == 10000, "column paste uses recorded copy offset after video offset changes");
            Clipboard.SetText("external text"); Call(main, "RestoreClassicClipboardTimes", copied); Check(copied.Paragraphs[0].StartTime.TotalMilliseconds == 10000, "foreign clipboard has no hidden timing adjustment");
            }
            player.VideoPlayer = null;
            using (var selection = new ModifySelection(source, new SubRip(), grid))
            {
                var rules = (NikseComboBox)Get(selection, "comboBoxRule"); Check(rules.Items.Cast<object>().Any(item => item.ToString().Contains("ошибками")), "Has errors rule visible for SRT, independent of actor/style support");
            }
            using (var options = new ClassicEditorPreferencesForm()) Shot(options, "editor57-ru.png");
            using (var url = new ClassicOpenVideoUrl()) { Check(url.Controls.Find("classicUrlCookieBrowser", true).Length == 1, "browser cookies selector in URL opening dialog"); Shot(url, "url57-ru.png"); }
            using (var batch = new BatchConvert(SystemIcons.Application))
            {
                Check(batch.Controls.Find("classicBatchFilesDivider", true).Length == 1, "batch file/functions divider is native and resizable");
                var output = Path.Combine(data, "batch"); Directory.CreateDirectory(output); var input = Path.Combine(data, "batch-source.srt"); File.WriteAllText(input, new SubRip().ToText(source, "source"));
                var list = (ListView)Get(batch, "listViewInputFiles"); var item = new ListViewItem(new[] { input, "", "", "" }); list.Items.Add(item);
                foreach (var actionItem in ((IEnumerable<ListViewItem>)Get(batch, "_classicBatchActionItems"))) { var action = (BatchConvert.FixActionItem)actionItem.Tag; actionItem.Checked = action.Action == CommandLineConverter.BatchAction.ChangeFrameRate; action.Checked = actionItem.Checked; }
                ((NikseComboBox)Get(batch, "comboBoxFrameRateTo")).Text = "25"; ((TextBox)Get(batch, "textBoxOutputFolder")).Text = output; ((RadioButton)Get(batch, "radioButtonSaveInSourceFolder")).Checked = false;
                var shiftedCue = new Subtitle(); shiftedCue.Paragraphs.Add(new Paragraph("25 fps cue", 960, 1920));
                var job = new BatchConvert.ThreadDoWorkParameter(false, false, true, false, false, false, (false, false, false, false), item, shiftedCue, new SubRip(), new TextEncoding(System.Text.Encoding.UTF8, TextEncoding.Utf8WithoutBom), "en", input, "MicroDVD", new SubRip(), null);
                Configuration.Settings.General.CurrentFrameRate = 24; Call(batch, "ThreadWorkerCompletedOne", job);
                var generated = Directory.GetFiles(output, "*.sub"); Check(job.Error == null && generated.Length == 1 && File.ReadAllText(generated[0]).Contains("{24}{48}"), "ordinary batch UI save writes target 25 fps instead of source 24 fps");
                job.Subtitle = Sample(); var text = job.Subtitle.Paragraphs[0].Text; job.FixRtl = true; Call(batch, "ApplyFixesStep2", job, "ADD_UNICODE"); Check(job.Subtitle.Paragraphs[0].Text == text, "batch RTL leaves non-RTL cues byte-for-byte unchanged"); Shot(batch, "batch57-ru.png");
            }
        }
        using (var words = new WordLists())
        {
            words.ShowInTaskbar = false; words.StartPosition = FormStartPosition.Manual; words.Location = new Point(-30000, -30000); words.Show(); Application.DoEvents();
            var list = (ListView)Get(words, "listViewNames"); list.Items.Clear(); list.Items.Add("ёжик"); list.Items.Add("жук"); list.Items.Add("яблоко");
            typeof(Control).GetMethod("OnKeyPress", B).Invoke(list, new object[] { new KeyPressEventArgs('я') }); Check(list.SelectedItems.Count == 1 && list.SelectedItems[0].Text == "яблоко", "word-list type-ahead uses real Cyrillic input");
            Check(list.ContextMenuStrip != null && list.ContextMenuStrip.Items.Count == 1, "word-list Remove reachable by right click");
        }
    }
    static void Shot(Form form, string name)
    {
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000); form.Show(); Application.DoEvents(); form.PerformLayout();
        using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name)); }
        form.Hide();
    }
}
