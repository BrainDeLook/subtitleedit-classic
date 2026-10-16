using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Nikse.SubtitleEdit.Logic.VideoPlayers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestSpeechPlayer
{
    static void Check(bool value, string description) { if (!value) throw new Exception(description); Console.WriteLine("PASS: " + description); }
    static IClassicSpeechAudio CreateAudio() => (IClassicSpeechAudio)Activator.CreateInstance(typeof(ClassicSpeechReader).Assembly.GetType("Nikse.SubtitleEdit.Logic.ClassicSpeechPlayer"), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { 0 }, null);
    [STAThread] static int Main(string[] args)
    {
        try
        {
            var data = Path.GetFullPath(args[0]); Directory.CreateDirectory(data);
            typeof(Configuration).GetField("DataDirectory", BindingFlags.Public | BindingFlags.Static).SetValue(null, data + Path.DirectorySeparatorChar);
            // Hosted runners have no sound device. Exercise real MPV decoding/clocks with its null sink.
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true") Configuration.Settings.General.MpvExtraOptions = "--ao=null --vo=null";
            File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libmpv-2.dll"), Path.Combine(data, "libmpv-2.dll"), true);
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            var task = Run(Path.GetFullPath(args[1]));
            while (!task.IsCompleted) { Application.DoEvents(); Thread.Sleep(10); }
            task.GetAwaiter().GetResult(); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    sealed class TrackedAudio : IClassicSpeechAudio
    {
        readonly IClassicSpeechAudio inner = CreateAudio();
        public readonly List<double> Progress = new List<double>();
        public double Duration => inner.Duration;
        public double Position { get { var value = inner.Position; if (Progress.Count > 0) Progress[Progress.Count - 1] = Math.Max(Progress.Last(), value); return value; } }
        public bool IsPaused { get { var position = Position; return inner.IsPaused; } }
        public double Speed { set => inner.Speed = value; }
        public async Task LoadAudio(string path) { await inner.LoadAudio(path); Progress.Add(0); }
        public void Play() => inner.Play();
        public void Pause() => inner.Pause();
        public void Dispose() => inner.Dispose();
    }
    static async Task Run(string reference)
    {
        using (var audio = CreateAudio())
        {
            for (int i = 0; i < 2; i++)
            {
                await audio.LoadAudio(reference);
                Check(audio.Duration > 1, "real MPV opens speech clip " + (i + 1));
                audio.Play(); await Task.Delay(900);
                Console.WriteLine("Audio position=" + audio.Position + ", paused=" + audio.IsPaused);
                Check(audio.Position >= 0.5 && !audio.IsPaused, "speech advances after delayed file-load events " + (i + 1));
                audio.Pause(); var held = audio.Position; await Task.Delay(200);
                Check(Math.Abs(audio.Position - held) < 0.1, "speech holds on pause");
                audio.Play(); await Task.Delay(250);
                Check(audio.Position > held + 0.1, "speech resumes after pause");
            }
        }
        using (var source = new LibMpvDynamic())
        {
            var loaded = new TaskCompletionSource<bool>();
            var sourceFile = Path.Combine(Configuration.DataDirectory, "source.wav");
            using (var writer = new BinaryWriter(File.Create(sourceFile)))
            {
                const int bytes = 30 * 16000 * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(bytes + 36);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(16000); writer.Write(32000); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes); writer.Write(new byte[bytes]);
            }
            source.Initialize(null, sourceFile, (s, e) => loaded.TrySetResult(true), null);
            if (await Task.WhenAny(loaded.Task, Task.Delay(5000)) != loaded.Task) throw new Exception("Source MPV load timed out");
            source.Volume = 0;
            source.CurrentPosition = 1;
            var video = new ClassicSpeechVideoAdapter { Open = () => true, Playing = () => source.IsPlaying, GetPosition = () => source.CurrentPosition, GetSpeed = () => source.PlayRate, GetVolume = () => source.Volume, SetVolume = v => source.Volume = (int)v, Start = () => source.Play(), Hold = () => source.Pause() };
            var lines = new[] { new ClassicSpeechLine { Paragraph = new Paragraph("Selected row", 1000, 2200), Index = 7 }, new ClassicSpeechLine { Paragraph = new Paragraph("Following row", 2500, 4000), Index = 8 } };
            var audio = new TrackedAudio(); var spoken = new List<int>();
            var reader = new ClassicSpeechReader((line, folder, cancel) => Task.FromResult(reference), audioFactory: () => audio);
            using (var timeout = new CancellationTokenSource(30000))
                await reader.RunAsync(lines, video, i => spoken.Add(lines[i].Index), i => { }, timeout.Token);
            Check(spoken.SequenceEqual(new[] { 7, 8 }), "real source player starts at selected line and advances to following line");
            Check(audio.Progress.Count == 2 && audio.Progress.All(p => p > 0.5), "both speech clips actually advance alongside real source playback");
            Check(source.IsPaused && source.Volume == 0, "reader restores source volume and stops video on completion");
        }
    }
}
