using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicCast;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestExtendedTts
{
    static void Check(bool value, string description) { if (!value) throw new Exception(description); Console.WriteLine("PASS: " + description); }
    static object Field(object value, string name) => value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value);
    [STAThread] static int Main(string[] args)
    {
        try
        {
            var data = Path.GetFullPath(args[1]); Directory.CreateDirectory(data);
            typeof(Configuration).GetField("DataDirectory", BindingFlags.Public | BindingFlags.Static).SetValue(null, data + Path.DirectorySeparatorChar);
            Configuration.Settings.General.Language = "ru-RU";
            Configuration.Settings.General.FFmpegLocation = args[2];
            LanguageSettings.Current = Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages/ru-RU.xml"));
            if (args[0] == "unit") { Application.EnableVisualStyles(); Unit(data); }
            else if (args[0] == "cast") Cast(data);
            else Native(data, args).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static void Unit(string data)
    {
        var inputScript = Path.Combine(data, "read-stdin.ps1");
        var inputOutput = Path.Combine(data, "stdin-result.txt");
        File.WriteAllText(inputScript, "param($destination)\n[Console]::InputEncoding=New-Object System.Text.UTF8Encoding($false)\n[IO.File]::WriteAllText($destination,[Console]::In.ReadToEnd(),(New-Object System.Text.UTF8Encoding($false)))");
        var unicodeInput = "Привет, мир. 日本語のテスト. Hello.";
        var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32/WindowsPowerShell/v1.0/powershell.exe"), "-NoProfile -ExecutionPolicy Bypass -File " + Nikse.SubtitleEdit.Logic.SpeechToText.ClassicSpeechRunner.Quote(inputScript) + " " + Nikse.SubtitleEdit.Logic.SpeechToText.ClassicSpeechRunner.Quote(inputOutput));
        Nikse.SubtitleEdit.Logic.SpeechToText.ClassicSpeechRunner.RunProcessAsync(start, CancellationToken.None, null, unicodeInput).GetAwaiter().GetResult();
        Check(File.ReadAllText(inputOutput) == unicodeInput, "standalone runner sends exact UTF-8 text and closes stdin");
        var additions = ClassicLocalTts.Catalog.Where(e => ClassicExtendedTts.Handles(e.Id) && e.Id != "zonos").ToArray();
        Check(additions.Length == 10, "ten stable catalog additions including both OmniVoice runtimes");
        const string voice = "voice_12345678901234567890123456789012";
        Directory.CreateDirectory(ClassicQwenReference.Folder);
        File.WriteAllBytes(Path.Combine(ClassicQwenReference.Folder, voice + ".wav"), new byte[48]);
        File.WriteAllText(Path.Combine(ClassicQwenReference.Folder, voice + ".txt"), "Reference transcript");
        File.WriteAllText(Path.Combine(ClassicQwenReference.Folder, voice + ".json"), "{\"Consent\":true,\"Id\":\"" + voice + "\",\"Name\":\"Optional transcript test\"}");
        foreach (var e in additions)
        {
            var settings = ClassicLocalTtsSettings.Load(e.Id);
            var selected = e.Id == "kokoro" ? e.Presets[0] : voice;
            var payload = ClassicExtendedTts.Payload(e, settings, "Hello", selected, e.Id != "kokoro");
            if (e.Id == "kokoro") Check(payload.ContainsKey("text") && !payload.ContainsKey("input") && !e.SupportsCloning && e.Presets.Length == 103, "Kokoro dedicated protocol and fixed voices");
            else
            {
                Check(payload.ContainsKey("input") && (bool)payload["spoken_disclaimer"] == false, "clone provenance " + e.Id);
                Check(payload.ContainsKey("voice") == (e.Id == "pocket" || e.Id == "vibe"), "only per-request voice backends receive voice stem " + e.Id);
                Check(payload.ContainsKey("speed") == e.SupportsSpeed, "backend speed capability " + e.Id);
            }
            if (e.Runtime != "omnivoice")
            {
                var arguments = ClassicExtendedTts.ServerArguments(e, settings, selected, "Reference transcript", data, 1234);
                Check(arguments.Contains("--port 1234") && !arguments.Contains("--auto-download"), "explicit local model paths " + e.Id);
                if (e.Id == "confucius") Check(arguments.Contains("--tts-steps 20"), "Confucius ODE step flag");
            }
            foreach (var m in e.Models) foreach (var f in m.Files) Check(f.Hash.Length == 64 && f.Bytes > 0, "complete pinned bundle " + e.Id + " / " + m.Key);
            if (e.RuntimeArchives != null) Check(e.RuntimeArchives.All(a => a.Hash.Length == 64 && a.Bytes > 0 && a.Url.StartsWith("https://")), "pinned standalone runtime " + e.Id);
            using (var form = new ClassicLocalTtsSettingsForm(e.Id, settings))
            {
                form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.Show(); Application.DoEvents();
                var table = (TableLayoutPanel)form.Controls[0];
                Check(table.Controls.Cast<Control>().All(c => c.Top >= table.Padding.Top && c.Bottom <= table.ClientSize.Height - table.Padding.Bottom), "settings rows fit " + e.Id);
                using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(data, e.Id + "-settings.png")); }
                form.Close();
            }
        }
        File.WriteAllText(Path.Combine(ClassicQwenReference.Folder, voice + ".txt"), "");
        Check(ClassicQwenReference.List(false).Any(v => v.Id == voice) && !ClassicQwenReference.List().Any(v => v.Id == voice), "transcript-free references stay out of engines that require text");
        foreach (var id in new[] { "dots", "confucius", "pocket", "vibe", "index", "omni", "moss" })
        {
            var definition = additions.First(e => e.Id == id);
            ClassicExtendedTts.Payload(definition, ClassicLocalTtsSettings.Load(id), "Test", voice, true);
            Check(ClassicLocalTts.Voices(id).OfType<ClassicQwenReference>().Any(v => v.Id == voice), "optional reference transcript accepted " + id);
        }
        Check(!ClassicLocalTts.Voices("f5").OfType<ClassicQwenReference>().Any(v => v.Id == voice), "F5 still requires exact reference transcript");
        using (var writer = new BinaryWriter(File.Create(Path.Combine(ClassicQwenReference.Folder, voice + ".wav"))))
        {
            const int bytes = 24000 * 2;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(bytes + 36);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(24000); writer.Write(48000); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes); writer.Write(new byte[bytes]);
        }
        var optionalPack = Path.Combine(data, "optional-transcript.zip");
        ClassicVoiceLibrary.Export(new[] { ClassicQwenReference.List(false).First(v => v.Id == voice) }, optionalPack);
        var importedPack = ClassicVoiceLibrary.ImportPackAsync(optionalPack, true, CancellationToken.None).GetAwaiter().GetResult();
        Check(importedPack.Count == 1 && File.ReadAllText(Path.Combine(ClassicQwenReference.Folder, importedPack[0].Id + ".txt")) == "", "transcript-free voice pack round trip");
        File.WriteAllText(Path.Combine(ClassicQwenReference.Folder, voice + ".txt"), "Reference transcript");
        var pocket = additions.First(e => e.Id == "pocket"); var p = ClassicLocalTtsSettings.Load("pocket");
        p.Model = "German Q8_0";
        Check(ClassicExtendedTts.ServerArguments(pocket, p, voice, "", data, 1).Contains("--backend pocket-tts-de"), "Pocket language checkpoint changes backend");
        var moss = additions.First(e => e.Id == "moss"); var mSettings = ClassicLocalTtsSettings.Load("moss"); mSettings.Language = "hu";
        Check(ClassicExtendedTts.ServerArguments(moss, mSettings, voice, "Reference", data, 1).Contains("hungarian"), "MOSS spells unmapped target language correctly");
        var sub = new Subtitle(); sub.Paragraphs.Add(new Paragraph("Test", 0, 2000));
        using (var form = new TextToSpeech(sub, new SubRip(), "", null))
        {
            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.Show(); Application.DoEvents();
            var engines = (Nikse.SubtitleEdit.Controls.NikseComboBox)Field(form, "nikseComboBoxEngine");
            foreach (var e in additions)
            {
                engines.Text = e.Name; Application.DoEvents();
                var id = typeof(TextToSpeech).GetProperty("SelectedLocalId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
                Check((string)id == e.Id, "main dialog resolves engine " + e.Id);
            }
            form.Close();
        }
    }
    static void Cast(string data)
    {
        var output = Path.Combine(data, "mixed-cast"); Directory.CreateDirectory(output);
        var sub = new Subtitle();
        sub.Paragraphs.Add(new Paragraph("First actor speaks with OmniVoice.", 0, 3000) { Actor = "A" });
        sub.Paragraphs.Add(new Paragraph("Second actor speaks with Pocket TTS.", 4000, 7000) { Actor = "B" });
        sub.Paragraphs.Add(new Paragraph("This unselected actor must stay silent.", 8000, 11000) { Actor = "C" });
        var cast = new ClassicCastSettings();
        foreach (var pair in new[] { new[] { "A", "omni" }, new[] { "B", "pocket" } })
        {
            var definition = ClassicLocalTts.Catalog.First(e => e.Id == pair[1]);
            var settings = ClassicLocalTtsSettings.Load(pair[1]);
            cast.Rows.Add(new ClassicCastRow { Actor = pair[0], Engine = definition.Name, Voice = settings.Voice, Model = settings.Model, Language = pair[1] == "omni" ? "en" : "", Selected = true });
        }
        cast.Rows.Add(new ClassicCastRow { Actor = "C", Selected = false });
        using (var form = new TextToSpeech(sub, new SubRip(), "", null))
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(TextToSpeech).GetField("_castSettings", flags).SetValue(form, cast);
            typeof(TextToSpeech).GetField("_waveFolder", flags).SetValue(form, output);
            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.Show(); Application.DoEvents();
            var engines = (Nikse.SubtitleEdit.Controls.NikseComboBox)Field(form, "nikseComboBoxEngine"); engines.Text = ClassicLocalTts.Catalog.First(e => e.Id == "omni").Name;
            var actors = (ListView)Field(form, "listViewActors");
            foreach (ListViewItem item in actors.Items) item.Checked = item.Text == "A" || item.Text == "B";
            Check((bool)typeof(TextToSpeech).GetMethod("CaptureSpeechActors", flags).Invoke(form, null), "mixed cast actor selection accepted");
            var task = (Task<bool>)typeof(TextToSpeech).GetMethod("GenerateParagraphAudioAsync", flags).Invoke(form, new object[] { sub, true, null });
            while (!task.IsCompleted) { Application.DoEvents(); Thread.Sleep(10); }
            Check(task.GetAwaiter().GetResult(), "mixed OmniVoice and Pocket cast generated through classic UI");
            Check(ClassicVoiceAssets.WaveSeconds(Path.Combine(output, "0.wav")) > 1 && ClassicVoiceAssets.WaveSeconds(Path.Combine(output, "1.wav")) > 1 && !File.Exists(Path.Combine(output, "2.wav")), "only checked actors create audio clips");
            form.Close();
        }
    }
    static async Task Native(string data, string[] args)
    {
        var id = args[3]; var e = ClassicLocalTts.Catalog.First(d => d.Id == id);
        var settings = ClassicLocalTtsSettings.Load(id); settings.Variant = id == "kokoro" ? "cpu" : id == "omnicpp" ? "cuda" : "cuda13"; settings.Language = e.Languages.Any(l => l.StartsWith("en|")) ? "en" : settings.Language;
        if (args.Length > 6 && args[6] != "-") settings.Model = args[6];
        var transcript = "Today we are testing English subtitles. Every sentence should sound clear and natural.";
        var reference = await ClassicQwenReference.ImportAsync(Path.GetFullPath(args[4]), "Permitted synthetic English test", transcript, 0, Math.Min(15, ClassicVoiceAssets.WaveSeconds(args[4])), true, CancellationToken.None);
        settings.Voice = id == "kokoro" ? e.Presets[0] : reference.Id;
        settings.Save(id);
        using (var engine = new ClassicLocalTts(id, settings))
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(12)))
        {
            await engine.SpeakAsync("The voice should speak clearly and finish every sentence.", settings.Voice, Path.Combine(data, id + "-english.wav"), timeout.Token, new Progress<Nikse.SubtitleEdit.Logic.SpeechToText.SpeechProgress>(p => Console.WriteLine(p.Message)));
            Check(ClassicVoiceAssets.WaveSeconds(Path.Combine(data, id + "-english.wav")) > 1, "native English synthesis " + id);
            if (id == "confucius" || id == "moss" || id == "omni" || id == "omnicpp")
            {
                var russian = await ClassicQwenReference.ImportAsync(Path.GetFullPath(args[5]), "Permitted synthetic Russian test", "Сегодня мы проверяем озвучку русских субтитров. Голос должен произносить слова ясно.", 0, Math.Min(15, ClassicVoiceAssets.WaveSeconds(args[5])), true, timeout.Token);
                settings.Language = "ru";
                await engine.SpeakAsync("Сегодня мы проверяем русскую озвучку. Каждое слово должно звучать ясно и естественно.", russian.Id, Path.Combine(data, id + "-russian.wav"), timeout.Token);
                Check(ClassicVoiceAssets.WaveSeconds(Path.Combine(data, id + "-russian.wav")) > 1, "native Russian synthesis and reference switch " + id);
            }
            if (id == "omni" || id == "omnicpp")
            {
                settings.Language = "en";
                if (id == "omnicpp") settings.Instructions = "whisper";
                await engine.SpeakAsync("This is the built in default voice.", "Default", Path.Combine(data, id + "-default.wav"), timeout.Token);
                Check(ClassicVoiceAssets.WaveSeconds(Path.Combine(data, id + "-default.wav")) > 1, "Omni switches from imported to default voice " + id);
            }
            using (var cancel = new CancellationTokenSource(20))
            {
                var output = Path.Combine(data, id + "-cancel.wav");
                try { await engine.SpeakAsync("Cancel this speech generation.", settings.Voice, output, cancel.Token); throw new Exception("Cancellation ignored"); }
                catch (OperationCanceledException) { Check(!File.Exists(output), "cancel leaves no output " + id); }
            }
        }
    }
}

