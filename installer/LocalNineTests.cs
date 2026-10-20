using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic.ClassicFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicMusic;
using Nikse.SubtitleEdit.Logic.VideoPlayers;

internal static class LocalNineTests
{
    private static int _checks; private static string _root;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); _checks++; Console.WriteLine("PASS " + message); }
    private static object Get(object obj, string field) => obj.GetType().GetField(field, Private | BindingFlags.Public).GetValue(obj);
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private | BindingFlags.Public).SetValue(obj, value);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethods(Private).First(m => m.Name == name && m.GetParameters().Length == args.Length && m.GetParameters().Select((p,i) => args[i] == null ? !p.ParameterType.IsValueType : p.ParameterType.IsInstanceOfType(args[i])).All(v => v)).Invoke(obj, args);
    private static void Pump(Func<bool> done, int ms)
    { var until = DateTime.UtcNow.AddMilliseconds(ms); while (!done() && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(10); } Assert(done(), "bounded UI wait"); }
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            _root = Path.GetFullPath(args[0]); Directory.CreateDirectory(_root);
            foreach (var field in new[] { "DataDirectory", "SettingsFileName", "WaveformsDirectory", "AutoBackupDirectory", "DictionariesDirectory" })
                typeof(Configuration).GetField(field).SetValue(null, field == "SettingsFileName" ? Path.Combine(_root, "Settings.xml") : field == "DataDirectory" ? _root + Path.DirectorySeparatorChar : Path.Combine(_root, field) + Path.DirectorySeparatorChar);
            var settings = Nikse.SubtitleEdit.Core.Settings.Settings.GetSettings();
            ((Lazy<Configuration>)typeof(Configuration).GetField("Instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Value.SetSettings(settings);
            settings.General.FFmpegLocation = args[1]; settings.General.Language = "en-US"; settings.General.CheckForUpdates = false;
            Frames(); Shortcuts(); Batch(); Music(); NativeUi(args[2]); SynchronizationContext.SetSynchronizationContext(null); Media(args[2]);
            Console.WriteLine("PASS TOTAL " + _checks); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
    }
    private static void Frames()
    {
        var g = Configuration.Settings.General;
        foreach (var rate in new[] { 23.976, 24, 25, 29.97, 59.94 })
        {
            g.CurrentFrameRate = rate; g.UseAbsoluteFrameNumbers = true; g.UseTimeFormatHHMMSSFF = false;
            foreach (var frame in new[] { -100, 0, 1, 25, 86400 })
            {
                double ms = FrameNumberTime.ToMilliseconds(frame);
                Assert(FrameNumberTime.FromMilliseconds(ms) == frame, "frame round trip " + rate + " / " + frame);
                Assert(new TimeCode(ms).ToDisplayString() == frame.ToString(CultureInfo.InvariantCulture), "absolute display " + rate + " / " + frame);
                using (var control = new NikseTimeUpDown())
                { control.SetTotalMilliseconds(ms); Set(control, "_dirty", true); Assert(Math.Abs(control.GetTotalMilliseconds().Value - ms) < 1, "frame input round trip " + frame); }
            }
            g.UseAbsoluteFrameNumbers = false; g.UseTimeFormatHHMMSSFF = true; g.StoreTimesOnVideoFrames = true;
            int nominal = (int)Math.Round(rate); var hour = FrameNumberTime.ToMilliseconds(nominal * 3600);
            Assert(FrameNumberTime.FormatClock(hour) == "01:00:00:00", "fractional video frame clock " + rate);
            using (var input = new NikseTimeUpDown()) { input.SetTotalMilliseconds(hour); Set(input, "_dirty", true); Assert(Math.Abs(input.GetTotalMilliseconds().Value - hour) < 1, "fractional clock parser " + rate); }
        }
        g.CurrentFrameRate = 23.976; g.CurrentVideoIsSmpte = true;
        Assert(SubtitleFormat.MillisecondsToFrames(1000) == 24 && SubtitleFormat.FramesToMilliseconds(24) == 1000, "SMPTE frame storage uses whole frame clock");
        var clockSubtitle = new Subtitle(); clockSubtitle.Paragraphs.Add(new Paragraph("Clock", 1000, 2000));
        var mediaAdapter = typeof(SubtitleFormat).GetMethod("ToMediaTime", BindingFlags.Static | BindingFlags.NonPublic);
        var media = (Subtitle)mediaAdapter.Invoke(null, new object[] { clockSubtitle });
        Assert(Math.Abs(media.Paragraphs[0].StartTime.TotalMilliseconds - 1001) < .001 && clockSubtitle.Paragraphs[0].StartTime.TotalMilliseconds == 1000, "media export converts a copy of the SMPTE clock");
        typeof(SubtitleFormat).GetMethod("FromMediaTime", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { media });
        Assert(Math.Abs(media.Paragraphs[0].StartTime.TotalMilliseconds - 1000) < .001, "media import restores SMPTE frame clock"); g.CurrentVideoIsSmpte = false;
        g.StoreTimesOnVideoFrames = false; g.UseAbsoluteFrameNumbers = true; Configuration.Settings.Save();
        Assert(File.ReadAllText(Configuration.SettingsFileName).Contains("<UseAbsoluteFrameNumbers>True"), "frame mode persists in Settings.xml");
        g.UseAbsoluteFrameNumbers = false; g.UseTimeFormatHHMMSSFF = false; g.CurrentFrameRate = 23.976;
    }
    private static void Shortcuts()
    {
        var config = new ClassicCustomShortcuts(); config.Slots[0].Key = Keys.Control | Keys.K; config.Slots[0].Steps.Add(new ClassicShortcutStep { Kind = "insert", Value = "Test", Position = "end" }); config.Validate();
        Assert(config.Slots[0].Matches(Keys.Control | Keys.K, false), "normal macro matches"); Assert(!config.Slots[0].Matches(Keys.Control | Keys.K, true), "normal macro does not steal AltGr");
        config.Slots[0].AltGr = true; Assert(config.Slots[0].Matches(Keys.Control | Keys.Alt | Keys.K, true), "AltGr macro matches right Alt"); Assert(!config.Slots[0].Matches(Keys.Control | Keys.Alt | Keys.K, false), "AltGr distinguishes left Ctrl+Alt");
        var regex = new ClassicShortcutStep { Kind = "replace", Regex = true, Value = "(hello)", Replacement = "$1!" }; Assert(regex.Replace("HELLO") == "HELLO!", "regex replacements preserve groups");
        config.Save(); Assert(ClassicCustomShortcuts.Load().Slots[0].Steps.Count == 1, "eight-slot macro persistence");
    }
    private static void Batch()
    {
        var options = new ClassicBatch53Options { TargetLanguages = "ru,en,ru", StyleProperties = "FontSize=43\nFontName=Segoe UI\nPrimary=#FFFF00", Filter = "forced" }; options.Validate();
        Assert(options.Languages.SequenceEqual(new[] { "ru", "en" }), "batch multiple target languages deduplicated");
        var subtitle = new Subtitle(); subtitle.Paragraphs.Add(new Paragraph("Hello", 1000, 2000));
        Assert(options.Skip("test.srt", subtitle, new SubRip()), "forced-only filter rejects normal cues"); subtitle.Paragraphs[0].Forced = true; Assert(!options.Skip("test.srt", subtitle, new SubRip()), "forced-only filter accepts forced cues");
        options.Filter = "extension"; options.FilterValue = "ass"; Assert(options.Skip("test.srt", subtitle, new SubRip()), "extension filter");
        options.Filter = "all"; options.Apply(subtitle, new AdvancedSubStationAlpha(), CancellationToken.None); var style = AdvancedSubStationAlpha.GetSsaStylesFromHeader(subtitle.Header).First();
        Assert(style.FontSize == 43 && style.FontName == "Segoe UI" && style.Primary.ToArgb() == Color.Yellow.ToArgb(), "batch ASS property changes");
        options.EmbedAssFonts = true; options.TrimAssFonts = true; options.Apply(subtitle, new AdvancedSubStationAlpha(), CancellationToken.None); Assert(subtitle.Footer.Contains("[Fonts]") && subtitle.Footer.Contains("fontname:"), "batch embeds real subset font attachments");
        options.OcrLanguagesByFile = "test.sup=rus\nother.sup=eng"; Assert(options.OcrLanguage("C:\\folder\\test.sup", "jpn") == "rus", "OCR language chosen per input file");
        options.Filter = "invented"; bool rejected = false; try { options.Validate(); } catch (ArgumentException) { rejected = true; } Assert(rejected, "unknown batch filter rejected");
    }
    private static void Music()
    {
        int sr = 8000; var samples = new float[sr * 16]; for (int i = 0; i < samples.Length; i++) samples[i] = (float)(.6 * Math.Sin(i * 2 * Math.PI * 220 / sr) * (.6 + .4 * Math.Cos(i * 2 * Math.PI / sr)));
        var clip = new MusicAudio(sr, 1, samples); var loop = MusicLooper.FindLoop(clip, 60); Assert(loop.LengthSeconds > 0, "music loop seam found");
        var output = MusicLooper.Normalize(MusicLooper.Render(clip, loop, 24)); Assert(output.FrameCount == 24 * sr && output.GetPeak() <= .892, "music render length and peak ceiling");
        string streamFile = Path.Combine(_root, "streamed-music.wav"); MusicStreamRenderer.Write(clip, loop, 24, streamFile, CancellationToken.None); var streamed = MusicAudio.ReadWav(streamFile); Assert(streamed.FrameCount == output.FrameCount && streamed.Samples.Zip(output.Samples, (a,b) => Math.Abs(a-b)).Max() < .0001, "streamed music matches normalized reference");
        string file = Path.Combine(_root, "music.wav"); output.WritePcm16Wav(file); var loaded = MusicAudio.ReadWav(file); Assert(loaded.FrameCount == output.FrameCount && loaded.SampleRate == sr, "music WAV round trip");
        var arguments = ClassicMusicGenerator.BuildArguments("soft piano", 100, 60, 42, "cpu", "C:\\my model.gguf", "C:\\my song.wav", true);
        Assert(arguments.Contains("--family ace_step") && arguments.Contains("--out-format float32") && arguments.Contains("bpm=100"), "ACE-Step upstream CLI contract");
        Assert(!ClassicRuntimePins.ApprovedFfmpeg(new byte[100]), "modified runtime archive rejected");
    }
    private static void NativeUi(string source)
    {
        using (var main = new Main())
        {
            main.CreateControl(); main.PerformLayout();
            var subtitle = new Subtitle(); subtitle.Paragraphs.Add(new Paragraph("Hello", 1000, 2000)); Set(main, "_subtitle", subtitle);
            var grid = (SubtitleListView)Get(main, "SubtitleListview1"); Call(grid, "Fill", subtitle, null); grid.SelectIndexAndEnsureVisible(0);
            var slot = new ClassicCustomShortcut { Name = "Macro undo", Steps = new List<ClassicShortcutStep> { new ClassicShortcutStep { Kind = "insert", Value = "!", Position = "end" }, new ClassicShortcutStep { Kind = "replace", Value = "Hello", Replacement = "Hi" } } };
            Call(main, "RunClassicCustomSteps", slot); Assert(subtitle.Paragraphs[0].Text == "Hi!", "macro runs selected-row insert and replacement in order"); Assert(subtitle.HistoryItems.Count == 1, "macro groups text changes into one undo step");
            var timeline = (ClassicTrackTimeline)Get(main, "_classicTracks"); timeline.Visible = true; main.PerformLayout(); Assert(timeline.Height > 0, "track timeline attaches to native main layout");
            using (var options = new Nikse.SubtitleEdit.Forms.Options.Settings(main)) { Assert(((NikseComboBox)Get(options, "comboBoxTimeCodeMode")).Items.Count == 3, "native settings expose all three time modes"); }
            using (var batch = new BatchConvert(SystemIcons.Application))
            {
                batch.ShowInTaskbar = false; batch.StartPosition = FormStartPosition.Manual; batch.Location = new Point(-30000,-30000); batch.Show(); Application.DoEvents();
                var row = batch.Controls.Find("classic53PresetRow", true).Single(); Assert(row != null, "batch preset controls are wired");
                var name = row.Controls.OfType<ComboBox>().Single(); name.Text = "Local test preset";
                var numeric = (NikseUpDown)Get(batch, "numericUpDownMaxCharacters"); var combo = (NikseComboBox)Get(batch, "comboBoxSubtitleFormats");
                numeric.Value = 42; var savedFormat = combo.Text;
                row.Controls.OfType<Button>().First(b=>b.Text.Contains("Save preset")).PerformClick(); numeric.Value = 60; combo.SelectedIndex = (combo.SelectedIndex + 1) % combo.Items.Count;
                row.Controls.OfType<Button>().First(b=>b.Text == "Load").PerformClick(); Assert(numeric.Value == 42 && combo.Text == savedFormat, "batch preset restores native numeric and format controls");
                var batchOptions=(ClassicBatch53Options)Get(batch,"_batch53"); batchOptions.Filter="forced";
                var normalTrack=new ListViewItem(new[]{"movie.mkv","1 MB","Matroska/ASS - eng #2","-"}); var forcedTrack=new ListViewItem(new[]{"movie.mkv","1 MB","Matroska/ASS - eng (forced) #3","-"});
                Assert((bool)Call(batch,"SkipClassicBatchItem",normalTrack) && !(bool)Call(batch,"SkipClassicBatchItem",forcedTrack),"forced-track filter uses container metadata");
                batchOptions.Filter="language"; batchOptions.FilterValue="eng"; Assert(!(bool)Call(batch,"SkipClassicBatchItem",forcedTrack),"language filter recognizes embedded track labels");
                batchOptions.Filter="error"; var statuses=(Dictionary<ListViewItem,string>)Get(batch,"_batch53PriorStatuses"); statuses[normalTrack]="Invalid subtitle timing"; statuses[forcedTrack]=LanguageSettings.Current.BatchConvert.Converted;
                Assert(!(bool)Call(batch,"SkipClassicBatchItem",normalTrack) && (bool)Call(batch,"SkipClassicBatchItem",forcedTrack),"failed-file filter retains prior statuses before queue reset");
            }
            using(var waveform = new AudioVisualizer { Size = new Size(700, 200) })
            {
                waveform.WavePeaks = new WavePeakData(100, Enumerable.Range(0,1000).Select(i=>new WavePeak((short)(8000+7000*Math.Sin(i*.1)), (short)(-8000-7000*Math.Cos(i*.1)))).ToList());
                waveform.SetPosition(0, subtitle, 1.5, 0, grid.SelectedIndices);
                using(var slow = new Bitmap(700,200)) using(var fast = new Bitmap(700,200))
                { waveform.FastRenderer = false; waveform.DrawToBitmap(slow, waveform.ClientRectangle); waveform.FastRenderer = true; waveform.DrawToBitmap(fast, waveform.ClientRectangle); int differences=0; for(int x=0;x<700;x++) for(int y=0;y<200;y++) if(slow.GetPixel(x,y)!=fast.GetPixel(x,y)) differences++; Assert(differences < 700, "fast waveform preserves the legacy rendering"); fast.Save(Path.Combine(_root,"waveform-fast.png")); }
            }
            Set(main,"_videoFileName",source); Set(main,"_videoAudioTrackNumber",0);
            string normalPeak = WavePeakGenerator.GetPeakWaveFileName(source,0), speechPeak = Path.Combine(Path.GetDirectoryName(normalPeak),"speech-"+Path.GetFileName(normalPeak)); Directory.CreateDirectory(Path.GetDirectoryName(normalPeak));
            using(var generator=new WavePeakGenerator(Path.Combine(_root,"music.wav"))) generator.GeneratePeaks(0,normalPeak);
            using(var generator=new WavePeakGenerator(Path.Combine(_root,"streamed-music.wav"))) generator.GeneratePeaks(0,speechPeak);
            var mainWave = (AudioVisualizer)Get(main,"audioVisualizer"); var normalData = WavePeakData.FromDisk(normalPeak); mainWave.WavePeaks=normalData;
            var speechMenu = (ToolStripMenuItem)Get(main,"_speechWaveMenu"); speechMenu.Checked=true; Pump(()=>mainWave.WavePeaks!=normalData,3000); Assert(File.Exists(normalPeak) && File.Exists(speechPeak),"speech-only waveform reuses its own cache and retains normal peaks"); speechMenu.Checked=false; Assert(mainWave.WavePeaks==normalData,"speech-only toggle restores original waveform without extraction");
            Call(main,"InitializeClassicCustomShortcuts"); Call(main,"InitializeClassicWaveformModes"); Call(main,"InitializeClassicFrameModes"); Call(main,"InitializeClassicFfmpeg");
            Assert(main.Controls.OfType<ClassicTrackTimeline>().Count()==1, "reinitialization keeps one track timeline");
            using (var music = new ClassicBackgroundMusic(null, 60)) { Assert(music.MinimumSize.Height >= 570, "native music form has a bounded layout"); Snapshot(music, "music-min", music.MinimumSize); }
            using (var options = new ClassicBatchOptionsForm(new ClassicBatch53Options())) Snapshot(options, "batch-options", new Size(740,690));
        }
    }
    private static void Snapshot(Form form, string name, Size size)
    {
        form.ShowInTaskbar=false; form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-30000,-30000); form.Size=size; form.Show(); Application.DoEvents(); using(var image=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size)); image.Save(Path.Combine(_root,name+".png")); } form.Close();
    }
    private static void Media(string source)
    {
        var info = ClassicMediaJobs.Probe(source, CancellationToken.None).GetAwaiter().GetResult(); Assert(info.Streams.Count(s => s.Type == "audio") == 2, "fixture contains two audio tracks");
        string scc = Path.Combine(_root, "captions.scc"); var sub = new Subtitle(); sub.Paragraphs.Add(new Paragraph("CAPTION ONE", 1000, 2800)); Configuration.Settings.General.CurrentFrameRate = 30000.0 / 1001; File.WriteAllText(scc, new ScenaristClosedCaptions().ToText(sub, "Test"));
        var captionTrack = new ClassicMappedStream { File = scc, Stream = new ClassicMediaStream { Index = 0, Type = "subtitle", Codec = "eia_608" } };
        var streams = info.Streams.Where(s => s.Type == "video" || s.Type == "audio").Select(s => new ClassicMappedStream { File = source, Stream = s }).ToList(); streams.Add(captionTrack);
        string mpeg = Path.Combine(_root, "captions.mpg"); ClassicVideoTools.Remux(source, mpeg, streams, false, CancellationToken.None).GetAwaiter().GetResult(); Assert(File.Exists(mpeg) && Encoding.ASCII.GetString(File.ReadAllBytes(mpeg)).Contains("GA94"), "MPEG output embeds real GA94 caption bytes");
        string mcc = Path.Combine(_root, "captions.mcc"); File.WriteAllText(mcc,new MacCaption10().ToText(sub,"Test")); var cc = Nikse.SubtitleEdit.Core.Cea608.ClosedCaptionBytes.FromFile(mcc); Assert(cc.HasCea708, "MCC input contains CEA-708 packets");
        var mccTracks=streams.Take(streams.Count-1).ToList(); mccTracks.Add(new ClassicMappedStream { File=mcc, Stream=new ClassicMediaStream { Index=0, Type="subtitle", Codec="eia_708" } });
        string mccMpeg=Path.Combine(_root,"captions708.mpg"); ClassicVideoTools.Remux(source,mccMpeg,mccTracks,false,CancellationToken.None).GetAwaiter().GetResult(); var mccBytes=File.ReadAllBytes(mccMpeg); bool dtvcc=false;
        for(int i=0;i<mccBytes.Length-11;i++) if(mccBytes[i]=='G' && mccBytes[i+1]=='A' && mccBytes[i+2]=='9' && mccBytes[i+3]=='4' && mccBytes[i+4]==3) { int count=mccBytes[i+5]&31; for(int n=0;n<count && i+7+n*3<mccBytes.Length;n++) if((mccBytes[i+7+n*3]&6)==6) dtvcc=true; }
        Assert(dtvcc,"MPEG output retains actual CEA-708 cc_data");
        string mixed=Path.Combine(_root,"music-mixed.wav"); ClassicMusicGenerator.Mix(source,Path.Combine(_root,"music.wav"),mixed,0,1,CancellationToken.None,1).GetAwaiter().GetResult(); var mixedAudio=MusicAudio.ReadWav(mixed); Assert(mixedAudio.Channels==2 && Math.Abs(mixedAudio.DurationSeconds-info.Seconds)<.1,"music mixer exports stereo WAV from a selected video audio track");
        double Frequency(double hz) { double a=0,b=0; for(int i=0;i<Math.Min(mixedAudio.FrameCount,mixedAudio.SampleRate);i++) { float sample=mixedAudio.Samples[i*mixedAudio.Channels]; a+=sample*Math.Sin(i*2*Math.PI*hz/mixedAudio.SampleRate); b+=sample*Math.Cos(i*2*Math.PI*hz/mixedAudio.SampleRate); } return a*a+b*b; }
        Assert(Frequency(800)>Frequency(400)*20,"music mixer uses the selected English test track");
        string mov = Path.Combine(_root, "captions.mov"); ClassicVideoTools.Remux(source, mov, streams, false, CancellationToken.None).GetAwaiter().GetResult(); Assert(ClassicMediaJobs.Probe(mov, CancellationToken.None).GetAwaiter().GetResult().Streams.Any(s => s.Codec == "eia_608"), "MOV output contains a CEA-608 track");
        using (var host = new Form { Size = new Size(420, 260), ShowInTaskbar = false, Opacity = 0 })
        {
            host.Show(); bool loaded = false; var player = new ClassicFfmpegPlayer { HardwareDecoding = false, Volume = 0 };
            player.Initialize(host, source, (s,e) => loaded = true, null); Pump(() => loaded, 20000); player.CurrentPosition = 1; Pump(() => player.PresentedPosition >= .99, 20000);
            player.Play(); var start = player.CurrentPosition; Pump(() => player.CurrentPosition > start + .3, 5000); player.Pause(); var paused = player.CurrentPosition; Thread.Sleep(80); Assert(Math.Abs(player.CurrentPosition - paused) < .01, "FFmpeg pause freezes media position");
            Assert(player.LastError == null, "FFmpeg decoding has no recorded error"); Assert(player.AudioTracks.Count == 2, "FFmpeg exposes both named audio tracks"); player.AudioTrackNumber = 1; Assert(player.AudioTrackNumber == 1 && Math.Abs(player.CurrentPosition - paused) < .15, "FFmpeg audio switching keeps position");
            Pump(() => ((double[])Get(player,"_frames")).Length>0,10000); var oldFrame=player.CurrentPosition; player.StepFrame(1); var next=player.CurrentPosition; Assert(next>oldFrame && next-oldFrame<.1,"FFmpeg steps to the next presentation timestamp"); player.StepFrame(-1); Assert(player.CurrentPosition<next,"FFmpeg steps backwards by a frame");
            Assert(player.AudioBytesDelivered>0,"FFmpeg delivered decoded audio to the sound device");
            player.DisposeVideoPlayer(); host.Close();
        }
    }
}


