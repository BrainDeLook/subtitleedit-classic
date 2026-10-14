using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicLocalTtsFile
    {
        public string File { get; set; }
        public string Repository { get; set; }
        public long Bytes { get; set; }
        public string Hash { get; set; }
    }
    public sealed class ClassicLocalTtsModel
    {
        public string Key { get; set; }
        public ClassicLocalTtsFile[] Files { get; set; }
        public override string ToString() => Key + " (" + (Files.Sum(f => f.Bytes) / 1073741824.0).ToString("0.0") + " GB)";
    }
    public sealed class ClassicLocalTtsDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Backend { get; set; }
        public string[] Presets { get; set; }
        public string[] Languages { get; set; }
        public ClassicLocalTtsModel[] Models { get; set; }
        public bool Codec { get; set; }
        public ClassicLocalTtsModel Model(string key) => Models.FirstOrDefault(m => m.Key == key) ?? throw new ArgumentException("Unknown " + Name + " model: " + key);
        public string[] LanguageChoices(string model) => Id == "chatter" && model == "Turbo" ? new[] {"en|English"} : Languages;
    }
    public sealed class ClassicLocalTtsSettings
    {
        public string Model { get; set; }
        public string Variant { get; set; } = "cpu";
        public string Voice { get; set; }
        public string Language { get; set; } = "ru";
        public string SourceLanguage { get; set; } = "auto";
        public double Speed { get; set; } = 1;
        public static string Root(string id) => Path.Combine(Configuration.DataDirectory, "TextToSpeech", "ClassicLocal", id);
        public static ClassicLocalTtsSettings Load(string id)
        {
            ClassicLocalTtsSettings s = null;
            try { s = JsonConvert.DeserializeObject<ClassicLocalTtsSettings>(File.ReadAllText(Path.Combine(Root(id), "settings.json"))); }
            catch (IOException) {} catch (JsonException) {}
            s = s ?? new ClassicLocalTtsSettings();
            var e = ClassicLocalTts.Catalog.First(x => x.Id == id);
            if (!e.Models.Any(m => m.Key == s.Model)) s.Model = e.Models[0].Key;
            if (string.IsNullOrEmpty(s.Voice)) s.Voice = e.Presets.FirstOrDefault() ?? ClassicQwenReference.List().FirstOrDefault()?.Id;
            return s;
        }
        public void Save(string id)
        {
            Directory.CreateDirectory(Root(id));
            File.WriteAllText(Path.Combine(Root(id), "settings.json"), JsonConvert.SerializeObject(this, Formatting.Indented), new UTF8Encoding(false));
        }
    }
    // Backend/model definitions are from Subtitle Edit's stable v5.3.0 tag.
    public sealed class ClassicLocalTts : IDisposable
    {
        public static readonly ClassicLocalTtsDefinition[] Catalog = LoadCatalog();
        private static ClassicLocalTtsDefinition[] LoadCatalog()
        {
            using (var stream = typeof(ClassicLocalTts).Assembly.GetManifestResourceStream("ClassicLocalTtsCatalog.json"))
            using (var reader = new StreamReader(stream)) return JsonConvert.DeserializeObject<ClassicLocalTtsDefinition[]>(reader.ReadToEnd());
        }
        private readonly ClassicLocalTtsDefinition _definition;
        private readonly ClassicLocalTtsSettings _settings;
        private readonly HttpClient _client = new HttpClient(new HttpClientHandler {UseProxy = false}) {Timeout = TimeSpan.FromMinutes(15)};
        private readonly StringBuilder _log = new StringBuilder();
        private readonly string _job = Path.Combine(Path.GetTempPath(), "SubtitleEditTts", Guid.NewGuid().ToString("N"));
        private Process _server;
        private string _baseUrl, _activeVoice;
        private bool _ready;
        public ClassicLocalTts(string id, ClassicLocalTtsSettings settings) { _definition = Catalog.First(e => e.Id == id); _settings = settings; }
        public static object[] Voices(string id)
        {
            var e = Catalog.First(x => x.Id == id);
            return e.Presets.Cast<object>().Concat(ClassicQwenReference.List().Cast<object>()).ToArray();
        }
        public static async Task EnsureInstalledAsync(string id, ClassicLocalTtsSettings settings, CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            var e = Catalog.First(x => x.Id == id);
            var model = e.Model(settings.Model);
            await ClassicSpeechDownload.EnsureRuntimeAsync(new SpeechEngine {Family = "crisp"}, settings.Variant, cancel, progress).ConfigureAwait(false);
            foreach (var file in model.Files)
            {
                cancel.ThrowIfCancellationRequested();
                var path = Path.Combine(ClassicLocalTtsSettings.Root(id), "models", file.File);
                await ClassicVoiceAssets.EnsureFileAsync("https://huggingface.co/" + file.Repository + "/resolve/main/" + file.File, path, file.Bytes, file.Hash, true, cancel, progress).ConfigureAwait(false);
            }
        }
        public static string ResolveVoice(string voice)
        {
            return ClassicQwenReference.List().FirstOrDefault(v => v.Id == voice || v.Name == voice)?.Id ?? voice;
        }
        public static string ReferenceLanguage(string text, string configured)
        {
            if (!string.IsNullOrEmpty(configured) && configured != "auto") return configured;
            var sub = new Subtitle(); sub.Paragraphs.Add(new Paragraph(text, 0, 1000));
            return LanguageAutoDetect.AutoDetectGoogleLanguageOrNull(sub) ?? "";
        }
        public Dictionary<string, object> BuildPayload(string text, string voice, string transcript)
        {
            var imported = !_definition.Presets.Contains(voice);
            var payload = new Dictionary<string, object> {{"input", text}, {"response_format", "wav"}};
            if (_definition.Id != "chatter") payload["speed"] = _settings.Speed;
            if (_definition.LanguageChoices(_settings.Model).Length > 0)
            {
                if (!_definition.LanguageChoices(_settings.Model).Any(x => x.Split('|')[0] == _settings.Language))
                    throw new ArgumentException("Select a supported language for " + _definition.Name + " / " + _settings.Model);
                payload["language"] = _settings.Language;
            }
            if (imported)
            {
                ClassicQwenReference.Validate(voice);
                payload["consent_attestation"] = "I have the speaker's consent, or it is my own voice.";
                payload["marking_attestation"] = "I will disclose that this audio is AI-generated.";
                payload["spoken_disclaimer"] = false;
                // Chatterbox consumes the basename relative to its working directory.
                if (_definition.Id == "chatter") payload["voice"] = voice + ".wav";
                if (_definition.Id != "vox")
                {
                    var source = ReferenceLanguage(transcript, _settings.SourceLanguage);
                    if (!string.IsNullOrEmpty(source)) payload["source_lang"] = source;
                }
            }
            return payload;
        }
        public async Task SpeakAsync(string text, string voice, string output, CancellationToken cancel, IProgress<SpeechProgress> progress = null)
        {
            voice = ResolveVoice(voice);
            var imported = !_definition.Presets.Contains(voice);
            if (imported) ClassicQwenReference.Validate(voice);
            var transcript = imported ? File.ReadAllText(Path.Combine(ClassicQwenReference.Folder, voice + ".txt")).Trim() : "";
            var payload = BuildPayload(text, voice, transcript);
            if (!_ready) { await EnsureInstalledAsync(_definition.Id, _settings, cancel, progress).ConfigureAwait(false); _ready = true; }
            Directory.CreateDirectory(_job);
            if (imported && !File.Exists(Path.Combine(_job, voice + ".wav")))
            {
                var args = "-nostdin -y -i " + ClassicSpeechRunner.Quote(Path.Combine(ClassicQwenReference.Folder, voice + ".wav")) + " -vn -ar " + (_definition.Id == "cosy" ? "16000" : "24000") + " -ac 1 -c:a pcm_s16le " + ClassicSpeechRunner.Quote(Path.Combine(_job, voice + ".wav"));
                await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation, args), cancel, progress).ConfigureAwait(false);
            }
            if (_server == null || (_definition.Id != "chatter" && _activeVoice != voice))
            {
                StopServer(); await StartAsync(voice, transcript, cancel, progress).ConfigureAwait(false); _activeVoice = voice;
            }
            using (var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"))
            using (var response = await _client.PostAsync(_baseUrl + "/v1/audio/speech", content, cancel).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException(_definition.Name + ": " + await response.Content.ReadAsStringAsync().ConfigureAwait(false) + "\n" + Log());
                var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (bytes.Length < 44 || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" || Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE") throw new InvalidDataException("TTS returned invalid WAV audio");
                cancel.ThrowIfCancellationRequested();
                File.WriteAllBytes(output, bytes);
                if (ClassicVoiceAssets.WaveSeconds(output) <= 0) throw new InvalidDataException("TTS returned empty audio");
            }
        }
        private async Task StartAsync(string voice, string transcript, CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            var model = _definition.Model(_settings.Model);
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            _baseUrl = "http://127.0.0.1:" + port;
            var exe = ClassicSpeechCatalog.Executable(new SpeechEngine {Family = "crisp"}, _settings.Variant);
            var backend = _definition.Id == "chatter" && _settings.Model == "Turbo" ? "chatterbox-turbo" : _definition.Backend;
            var args = "--server --backend " + backend + " -m " + ClassicSpeechRunner.Quote(Path.Combine(ClassicLocalTtsSettings.Root(_definition.Id), "models", model.Files[0].File));
            if (_definition.Codec) args += " --codec-model " + ClassicSpeechRunner.Quote(Path.Combine(ClassicLocalTtsSettings.Root(_definition.Id), "models", model.Files[1].File));
            args += " --host 127.0.0.1 --port " + port + " --voice-dir " + ClassicSpeechRunner.Quote(_job);
            if (_definition.Id != "chatter")
            {
                args += " --voice " + ClassicSpeechRunner.Quote(_definition.Presets.Contains(voice) ? voice : Path.Combine(_job, voice + ".wav"));
                if (!string.IsNullOrEmpty(transcript)) args += " --ref-text " + ClassicSpeechRunner.Quote(transcript);
            }
            _server = new Process {StartInfo = new ProcessStartInfo(exe, args) {WorkingDirectory = _job, UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, StandardErrorEncoding = Encoding.UTF8, StandardOutputEncoding = Encoding.UTF8}};
            DataReceivedEventHandler capture = (s, e) => { if (e.Data != null) lock (_log) { _log.AppendLine(e.Data); if (_log.Length > 16000) _log.Remove(0, _log.Length - 16000); } };
            _server.OutputDataReceived += capture; _server.ErrorDataReceived += capture;
            _server.Start(); _server.BeginOutputReadLine(); _server.BeginErrorReadLine();
            progress?.Report(new SpeechProgress {Message = "Loading " + _definition.Name + "…"});
            try
            {
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed < TimeSpan.FromMinutes(5))
                {
                    cancel.ThrowIfCancellationRequested();
                    if (_server.HasExited) throw new InvalidOperationException("TTS server exited: " + Log());
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
                    {
                        timeout.CancelAfter(2000);
                        try { using (var response = await _client.GetAsync(_baseUrl + "/health", timeout.Token).ConfigureAwait(false)) if (response.IsSuccessStatusCode) return; }
                        catch (HttpRequestException) {} catch (OperationCanceledException) {cancel.ThrowIfCancellationRequested();}
                    }
                    await Task.Delay(200, cancel).ConfigureAwait(false);
                }
                throw new TimeoutException("TTS model loading timed out: " + Log());
            }
            catch {StopServer(); throw;}
        }
        private string Log() {lock (_log) return _log.ToString();}
        private void StopServer()
        {
            if (_server == null) return;
            try {if (!_server.HasExited) {_server.Kill(); _server.WaitForExit(5000);}}
            catch (InvalidOperationException) {}
            finally {_server.Dispose(); _server = null;}
        }
        public void Dispose()
        {
            StopServer(); _client.Dispose();
            if (Directory.Exists(_job)) Directory.Delete(_job, true);
        }
    }
}
