using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.VideoPlayers;

internal static class TestClassicPlayer54
{
    private const BindingFlags B = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int _checks;
    private static void Check(bool result, string text) { if (!result) throw new Exception(text); _checks++; Console.WriteLine("PASS " + text); File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "player54-checks.log"), text + Environment.NewLine); }
    private static object Get(object o, string n) => o.GetType().GetField(n, B).GetValue(o);
    private static void Set(object o, string n, object value) => o.GetType().GetField(n, B).SetValue(o, value);
    private static object Call(object o, string n, params object[] args) => o.GetType().GetMethods(B).First(m => m.Name == n && m.GetParameters().Length == args.Length).Invoke(o, args);
    private static void Pump(int milliseconds) { var watch = Stopwatch.StartNew(); while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(10); } }
    private static string AllText(Control control) => control.Text + "\n" + string.Join("\n", control.Controls.Cast<Control>().Select(AllText));
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            var root = AppDomain.CurrentDomain.BaseDirectory;
            var ffmpeg = Environment.GetEnvironmentVariable("CLASSIC_TEST_FFMPEG") ?? args[0];
            if (!Directory.Exists(Path.Combine(root, "Dictionaries"))) throw new Exception("Isolated fixture required.");
            var data = Path.Combine(root, "player-state-" + Guid.NewGuid().ToString("N")) + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(data);
            foreach (var field in new[] { "DataDirectory", "SettingsFileName", "WaveformsDirectory", "AutoBackupDirectory", "DictionariesDirectory" })
                typeof(Configuration).GetField(field).SetValue(null, field == "SettingsFileName" ? Path.Combine(data, "Settings.xml") : data);
            Configuration.Settings.General.Language = "en-US";
            Configuration.Settings.General.CheckForUpdates = false; Configuration.Settings.General.AutoSave = false;
            Configuration.Settings.General.VideoPlayer = "FFmpeg";
            Configuration.Settings.Shortcuts.GeneralGoToNextSubtitle = "Control+PageDown";
            Configuration.Settings.Shortcuts.GeneralGoToPrevSubtitle = "Control+PageUp";
            var aac = Path.Combine(root, "duration54.aac");
            // Three ADTS frames: two ordinary blocks and one with two raw blocks.
            var frame = new byte[] { 0xff, 0xf1, 0x4c, 0x80, 0x01, 0x1f, 0xfc, 0, 0 };
            using (var stream = File.Create(aac)) { stream.Write(frame, 0, frame.Length); stream.Write(frame, 0, frame.Length); frame[6] = 0xfd; stream.Write(frame, 0, frame.Length); }
            Check(Math.Abs(AacDurationReader.TryGetDurationSeconds(aac).Value - 4096.0 / 48000) < 1e-9, "ADTS raw-block count gives exact duration");
            File.WriteAllBytes(aac, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
            Check(AacDurationReader.TryGetDurationSeconds(aac) == null, "Invalid ADTS is rejected");
            using (var encoder = Process.Start(new ProcessStartInfo(ffmpeg, "-nostdin -hide_banner -loglevel error -y -f lavfi -i anullsrc=r=48000:cl=stereo -t 2.2 -c:a aac -f adts " + ClassicMediaJobs.Q(aac)) { UseShellExecute = false, CreateNoWindow = true }))
            { encoder.WaitForExit(); Check(encoder.ExitCode == 0, "Real AAC fixture generated"); }
            var expected = AacDurationReader.TryGetDurationSeconds(aac).Value;
            Check(expected >= 2.2 && expected < 2.25, "Real AAC duration includes whole final frame");
            using (var host = new Form { Size = new Size(320, 180), Location = new Point(-20000, -20000), StartPosition = FormStartPosition.Manual, ShowInTaskbar = false })
            using (var surface = new Panel { Dock = DockStyle.Fill })
            using (var mpv = new LibMpvDynamic())
            {
                host.Controls.Add(surface); host.Show();
                typeof(Configuration).GetField("DataDirectory").SetValue(null, root);
                try { mpv.Initialize(surface, aac, null, null); }
                finally { typeof(Configuration).GetField("DataDirectory").SetValue(null, data); }
                Pump(1200);
                Check(Math.Abs(mpv.Duration - expected) < 1e-8, "Actual mpv exposes exact ADTS duration");
                mpv.Play(); Pump(2600);
                Check(mpv.IsPaused, "Actual mpv pauses at raw AAC end");
            }
            Check(true, "Native mpv disposal returned");
            string startupError = null;
            var diagnostic = new System.Windows.Forms.Timer { Interval = 200 };
            diagnostic.Tick += (sender, e) => { foreach (Form form in Application.OpenForms.Cast<Form>().ToArray()) if (form.GetType().Name == "MessageBoxForm") { startupError = AllText(form); form.Close(); } };
            diagnostic.Start();
            using (var main = new Main())
            {
                diagnostic.Stop(); diagnostic.Dispose();
                Check(startupError == null, "Main initialization: " + startupError);
                Console.WriteLine("Main constructed");
                main.ShowInTaskbar = false; main.Location = new Point(-20000, -20000); main.StartPosition = FormStartPosition.Manual; main.Show(); Pump(100);
                Console.WriteLine("Main shown");
                var subtitle = new Subtitle(); subtitle.Paragraphs.Add(new Paragraph("First", 1000, 2000)); subtitle.Paragraphs.Add(new Paragraph("Second", 3000, 4000)); subtitle.Paragraphs.Add(new Paragraph("Third", 5000, 6000));
                Set(main, "_subtitle", subtitle); Set(main, "_currentSubtitleFormat", new SubRip());
                var grid = (SubtitleListView)Get(main, "SubtitleListview1"); Call(grid, "Fill", subtitle, null); grid.SelectIndexAndEnsureVisible(0);
                var player = (VideoPlayerContainer)Get(main, "mediaPlayer"); var fake = new TestPlayer(); player.VideoPlayer = fake;
                using (var window = new VideoPlayerUndocked(main, player))
                {
                    Check(window.KeyPreview, "Undocked player owns keyboard preview");
                    fake.CurrentPosition = 1.5;
                    var next = new KeyEventArgs(Keys.Control | Keys.PageDown); Call(window, "VideoPlayerUndocked_KeyDown", window, next);
                    Check(next.SuppressKeyPress && Math.Abs(fake.CurrentPosition - 3) < .001, "Fullscreen next-subtitle key routes to actual Main command");
                    fake.CurrentPosition = 5.5;
                    var previous = new KeyEventArgs(Keys.Control | Keys.PageUp); Call(window, "VideoPlayerUndocked_KeyDown", window, previous);
                    Check(previous.SuppressKeyPress && Math.Abs(fake.CurrentPosition - 3) < .001, "Fullscreen previous-subtitle key routes to actual Main command");
                    var space = new KeyEventArgs(Keys.Space); Call(window, "VideoPlayerUndocked_KeyDown", window, space);
                    Check(space.SuppressKeyPress && fake.IsPlaying, "Fullscreen Space handles playback without reaching editor");
                }
                var menu = (MenuStrip)Get(main, "menuStrip1"); menu.Focus();
                var message = Message.Create(main.Handle, 0x100, (IntPtr)Keys.PageUp, IntPtr.Zero);
                var watch = Stopwatch.StartNew(); main.ProcessCmdKeyFromChildForm(ref message, Keys.PageUp);
                Check(watch.ElapsedMilliseconds < 1000, "PageUp over Classic menu returns without hanging");
            }
            File.Delete(aac);
            Directory.Delete(data, true);
            Console.WriteLine("PASS " + _checks + " player checks"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private sealed class TestPlayer : VideoPlayer
    {
        private bool _playing;
        public override string PlayerName => "Fixture"; public override int Volume { get; set; } public override double Duration => 20; public override double CurrentPosition { get; set; }
        public override bool IsPlaying => _playing; public override bool IsPaused => !_playing;
        public override void Play() { _playing = true; } public override void Pause() { _playing = false; } public override void Stop() { _playing = false; }
        public override void DisposeVideoPlayer() { } public override void Initialize(Control owner, string file, EventHandler loaded, EventHandler ended) { }
        public override event EventHandler OnVideoLoaded { add { } remove { } } public override event EventHandler OnVideoEnded { add { } remove { } }
    }
}
