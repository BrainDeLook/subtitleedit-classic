using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicOnlineVoice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Language { get; set; }
        public override string ToString() => Name == Id ? Name : Name + " [" + Id + "]";
    }
    public sealed class ClassicOnlineTtsSettings
    {
        public ClassicCompatibleProvider Credentials { get; set; } = new ClassicCompatibleProvider();
        public string KeyFile { get; set; } = "";
        public string Executable { get; set; } = "";
        public string Python { get; set; } = "";
        public string Voice { get; set; } = "";
        public string Model { get; set; } = "voxtral-mini-tts-2603";
        public string Rate { get; set; } = "+0%";
        public string Pitch { get; set; } = "+0Hz";
        public string Volume { get; set; } = "+0%";
        public static string Root(string id) => Path.Combine(Configuration.DataDirectory, "TextToSpeech", "ClassicOnline", id);
        public static ClassicOnlineTtsSettings Load(string id)
        {
            try { return JsonConvert.DeserializeObject<ClassicOnlineTtsSettings>(File.ReadAllText(Path.Combine(Root(id), "settings.json"))) ?? new ClassicOnlineTtsSettings(); }
            catch (IOException) { return new ClassicOnlineTtsSettings(); } catch (JsonException) { return new ClassicOnlineTtsSettings(); }
        }
        public void Save(string id)
        {
            Directory.CreateDirectory(Root(id));
            File.WriteAllText(Path.Combine(Root(id), "settings.json"), JsonConvert.SerializeObject(this, Formatting.Indented), new UTF8Encoding(false));
        }
    }
    // Protocols and voice catalogs ported from stable v5.3.0; Google REST avoids adding a second CLR runtime.
    public sealed class ClassicOnlineTts : IDisposable
    {
        private readonly string _id;
        private readonly ClassicOnlineTtsSettings _settings;
        private readonly HttpClient _client;
        private string _googleToken;
        private DateTime _tokenExpires;
        public ClassicOnlineTts(string id, ClassicOnlineTtsSettings settings, HttpMessageHandler handler = null)
        {
            if (!new[] { "edge", "mistral", "google" }.Contains(id)) throw new ArgumentException("Unknown online speech engine");
            _id = id; _settings = settings;
            _client = handler == null ? new HttpClient() : new HttpClient(handler);
            _client.Timeout = TimeSpan.FromMinutes(5);
        }
        public static string ResolveVoice(string id, string value, string fallback)
        {
            var voice = Voices(id).FirstOrDefault(v => v.Id == value || v.ToString() == value);
            return voice?.Id ?? fallback;
        }
        public static string LanguageCode(string voice)
        {
            var parts = voice.Split('-');
            if (parts.Length < 2) throw new ArgumentException("Select a valid language-tagged voice");
            return parts[0] + "-" + parts[1];
        }
        public static ClassicOnlineVoice[] ParseVoices(string id, string json)
        {
            var root = JToken.Parse(json);
            var array = root as JArray ?? (JArray)root[id == "google" ? "voices" : "items"];
            if (array == null) throw new InvalidDataException("The service returned no voice list");
            return array.Select(v => new ClassicOnlineVoice {
                Id = (string)v[id == "google" ? "name" : "id"], Name = (string)v["name"],
                Language = id == "google" ? ((string)v["languageCodes"]?.FirstOrDefault() ?? LanguageCode((string)v["name"])) : (string)v["languages"]?.FirstOrDefault()
            }).Where(v => !string.IsNullOrWhiteSpace(v.Id)).ToArray();
        }
        public static ClassicOnlineVoice[] Voices(string id)
        {
            var cache = Path.Combine(ClassicOnlineTtsSettings.Root(id), "voices.json");
            if (File.Exists(cache)) try { var cached = JsonConvert.DeserializeObject<ClassicOnlineVoice[]>(File.ReadAllText(cache)); if (cached?.Length > 0) return cached; } catch (IOException) {} catch (JsonException) {}
            if (id == "edge") return new[] { "ru-RU-DmitryNeural", "ru-RU-SvetlanaNeural", "en-US-AriaNeural", "en-US-GuyNeural", "ja-JP-NanamiNeural", "ja-JP-KeitaNeural" }.Select(v => new ClassicOnlineVoice { Id = v, Name = v, Language = LanguageCode(v) }).ToArray();
            using (var stream = typeof(ClassicOnlineTts).Assembly.GetManifestResourceStream("ClassicFinalTtsVoices.json"))
            using (var reader = new StreamReader(stream)) return ParseVoices(id, new JObject { [id == "google" ? "voices" : "items"] = JObject.Parse(reader.ReadToEnd())[id] }.ToString());
        }
        public async Task<ClassicOnlineVoice[]> RefreshVoicesAsync(CancellationToken cancel)
        {
            ClassicOnlineVoice[] result;
            if (_id == "edge")
            {
                var output = await RunEdgeAsync("--list-voices", cancel).ConfigureAwait(false);
                result = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => Regex.Match(l.Trim(), @"^(\S+)\s+(Male|Female|Neutral)\s+")).Where(m => m.Success).Select(m => new ClassicOnlineVoice { Id = m.Groups[1].Value, Name = m.Groups[1].Value, Language = LanguageCode(m.Groups[1].Value) }).ToArray();
            }
            else
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, _id == "google" ? "https://texttospeech.googleapis.com/v1/voices" : "https://api.mistral.ai/v1/audio/voices?limit=10000"))
                {
                    await AuthorizeAsync(request, cancel).ConfigureAwait(false);
                    using (var response = await _client.SendAsync(request, cancel).ConfigureAwait(false))
                    {
                        await CheckResponseAsync(response).ConfigureAwait(false);
                        result = ParseVoices(_id, await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    }
                }
            }
            if (result.Length == 0) throw new InvalidDataException("The service returned an empty voice list; the previous list was retained.");
            cancel.ThrowIfCancellationRequested();
            Directory.CreateDirectory(ClassicOnlineTtsSettings.Root(_id));
            var path = Path.Combine(ClassicOnlineTtsSettings.Root(_id), "voices.json");
            var part = path + "." + Guid.NewGuid().ToString("N");
            try { File.WriteAllText(part, JsonConvert.SerializeObject(result), new UTF8Encoding(false)); if (File.Exists(path)) File.Replace(part, path, null); else File.Move(part, path); }
            finally { if (File.Exists(part)) File.Delete(part); }
            return result;
        }
        public static string BuildRequest(string id, string text, string voice, string model)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(voice)) throw new ArgumentException("Enter text and select a voice");
            if (id == "mistral") return JsonConvert.SerializeObject(new { model, input = text, voice_id = voice, response_format = "mp3" });
            if (id == "google") return JsonConvert.SerializeObject(new { input = new { text }, voice = new { languageCode = LanguageCode(voice), name = voice }, audioConfig = new { audioEncoding = "MP3" } });
            throw new ArgumentException("Unsupported speech request");
        }
        public async Task SpeakAsync(string text, string voice, string output, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(voice)) throw new ArgumentException("Select a voice");
            var job = Path.Combine(Path.GetTempPath(), "ClassicOnlineTts", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(job);
            var audio = Path.Combine(job, "speech.mp3");
            var wave = Path.Combine(job, "speech.wav");
            try
            {
                if (_id == "edge")
                {
                    var input = Path.Combine(job, "input.txt"); File.WriteAllText(input, text, new UTF8Encoding(false));
                    var args = "--voice " + ClassicSpeechRunner.Quote(voice) + " --file " + ClassicSpeechRunner.Quote(input) + " --write-media " + ClassicSpeechRunner.Quote(audio);
                    foreach (var pair in new[] { new[] { "rate", _settings.Rate, "%" }, new[] { "pitch", _settings.Pitch, "Hz" }, new[] { "volume", _settings.Volume, "%" } })
                    { var value = NormalizeProsody(pair[1], pair[2]); if (value.Length > 0) args += " --" + pair[0] + "=" + value; }
                    for (var attempt = 0; ; attempt++)
                    {
                        try { if (File.Exists(audio)) File.Delete(audio); await RunEdgeAsync(args, cancel).ConfigureAwait(false); if (!File.Exists(audio) || new FileInfo(audio).Length == 0) throw new InvalidDataException("Edge returned no audio"); break; }
                        catch (InvalidOperationException ex) { if (attempt == 2 || ex.Message.IndexOf("ValueError", StringComparison.OrdinalIgnoreCase) >= 0 || ex.Message.IndexOf("Invalid voice", StringComparison.OrdinalIgnoreCase) >= 0) throw; await Task.Delay(2000 * (attempt + 1), cancel).ConfigureAwait(false); }
                    }
                }
                else
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Post, _id == "google" ? "https://texttospeech.googleapis.com/v1/text:synthesize" : "https://api.mistral.ai/v1/audio/speech"))
                    {
                        await AuthorizeAsync(request, cancel).ConfigureAwait(false);
                        request.Content = new StringContent(BuildRequest(_id, text, voice, _settings.Model), Encoding.UTF8, "application/json");
                        using (var response = await _client.SendAsync(request, cancel).ConfigureAwait(false))
                        {
                            await CheckResponseAsync(response).ConfigureAwait(false);
                            byte[] bytes;
                            var media = response.Content.Headers.ContentType?.MediaType ?? "";
                            if (_id == "google" || media.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                var data = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                                var encoded = (string)data[_id == "google" ? "audioContent" : "audio_data"];
                                if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidDataException("The service returned no audio data");
                                bytes = Convert.FromBase64String(encoded);
                            }
                            else bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                            if (bytes.Length == 0) throw new InvalidDataException("The service returned empty audio");
                            cancel.ThrowIfCancellationRequested(); File.WriteAllBytes(audio, bytes);
                        }
                    }
                }
                await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation, "-nostdin -y -i " + ClassicSpeechRunner.Quote(audio) + " -ar 24000 -ac 1 -c:a pcm_s16le " + ClassicSpeechRunner.Quote(wave)), cancel, null).ConfigureAwait(false);
                cancel.ThrowIfCancellationRequested();
                if (ClassicVoiceAssets.WaveSeconds(wave) <= 0) throw new InvalidDataException("The service returned no playable audio");
                File.Copy(wave, output, true);
            }
            finally { if (Directory.Exists(job)) Directory.Delete(job, true); }
        }
        private async Task CheckResponseAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;
            // Never include response bodies: services may echo subtitle text or credentials.
            throw new InvalidOperationException(_id + " TTS: HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase);
        }
        private async Task AuthorizeAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            var key = _id == "google" ? await GoogleTokenAsync(cancel).ConfigureAwait(false) : _settings.Credentials.ApiKey.Trim();
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Enter the Mistral API key in settings");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }
        private async Task<string> GoogleTokenAsync(CancellationToken cancel)
        {
            if (_googleToken != null && DateTime.UtcNow < _tokenExpires) return _googleToken;
            if (!File.Exists(_settings.KeyFile)) throw new FileNotFoundException("Select the Google service-account JSON key file in settings");
            var key = JObject.Parse(File.ReadAllText(_settings.KeyFile));
            if ((string)key["type"] != "service_account" || string.IsNullOrWhiteSpace((string)key["client_email"])) throw new InvalidDataException("Select a Google service-account JSON key file");
            var assertion = CreateGoogleAssertion((string)key["client_email"], (string)key["private_key"], DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            using (var body = new FormUrlEncodedContent(new Dictionary<string, string> { { "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer" }, { "assertion", assertion } }))
            using (var response = await _client.PostAsync("https://oauth2.googleapis.com/token", body, cancel).ConfigureAwait(false))
            {
                await CheckResponseAsync(response).ConfigureAwait(false);
                var token = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                _googleToken = (string)token["access_token"];
                if (string.IsNullOrWhiteSpace(_googleToken)) throw new InvalidDataException("Google returned no access token");
                _tokenExpires = DateTime.UtcNow.AddSeconds(Math.Max(0, ((int?)token["expires_in"] ?? 3600) - 60));
                return _googleToken;
            }
        }
        private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        public static string CreateGoogleAssertion(string email, string pem, long now)
        {
            var payload = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}")) + "." + Base64Url(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { iss = email, scope = "https://www.googleapis.com/auth/cloud-platform", aud = "https://oauth2.googleapis.com/token", iat = now, exp = now + 3600 })));
            var data = Convert.FromBase64String(pem.Replace("-----BEGIN PRIVATE KEY-----", "").Replace("-----END PRIVATE KEY-----", "").Replace("\r", "").Replace("\n", "").Trim());
            using (var outer = new BinaryReader(new MemoryStream(data)))
            using (var pkcs = new BinaryReader(new MemoryStream(ReadDer(outer, 0x30))))
            {
                ReadDer(pkcs, 2); ReadDer(pkcs, 0x30);
                using (var rsaDer = new BinaryReader(new MemoryStream(ReadDer(pkcs, 4))))
                using (var fields = new BinaryReader(new MemoryStream(ReadDer(rsaDer, 0x30))))
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp = false; ReadDer(fields, 2);
                    Func<byte[]> integer = () => { var n = ReadDer(fields, 2); return n.Length > 1 && n[0] == 0 ? n.Skip(1).ToArray() : n; };
                    var n = integer(); var e = integer();
                    Func<int, byte[]> padded = size => { var v = integer(); if (v.Length > size) throw new InvalidDataException("Invalid RSA key"); return new byte[size - v.Length].Concat(v).ToArray(); };
                    rsa.ImportParameters(new RSAParameters { Modulus = n, Exponent = e, D = padded(n.Length), P = padded(n.Length / 2), Q = padded(n.Length / 2), DP = padded(n.Length / 2), DQ = padded(n.Length / 2), InverseQ = padded(n.Length / 2) });
                    return payload + "." + Base64Url(rsa.SignData(Encoding.ASCII.GetBytes(payload), "SHA256"));
                }
            }
        }
        private static byte[] ReadDer(BinaryReader reader, int tag)
        {
            if (reader.ReadByte() != tag) throw new InvalidDataException("Invalid PKCS#8 private key");
            int length = reader.ReadByte();
            if ((length & 128) != 0) { var count = length & 127; if (count < 1 || count > 4) throw new InvalidDataException("Invalid DER length"); length = 0; for (int i = 0; i < count; i++) length = checked(length * 256 + reader.ReadByte()); }
            if (length < 0 || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Truncated DER key");
            return reader.ReadBytes(length);
        }
        public static string NormalizeProsody(string value, string unit)
        {
            var match = Regex.Match(value?.Trim() ?? "", @"^([+-]?)(\d+)(?:%|Hz)?$", RegexOptions.IgnoreCase);
            return match.Success ? (match.Groups[1].Value == "-" ? "-" : "+") + match.Groups[2].Value + unit : "";
        }
        public static string EdgeExecutable(ClassicOnlineTtsSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.Executable)) return settings.Executable;
            return Path.Combine(ClassicOnlineTtsSettings.Root("edge"), "venv", "Scripts", "edge-tts.exe");
        }
        public static string FindPython()
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            using (var core = hive.OpenSubKey(@"Software\Python\PythonCore"))
                if (core != null) foreach (var version in core.GetSubKeyNames().Where(v => Version.TryParse(v, out var parsed) && parsed >= new Version(3, 10)).OrderByDescending(v => Version.Parse(v)))
                    using (var path = core.OpenSubKey(version + @"\InstallPath"))
                    { var exe = path?.GetValue("ExecutablePath") as string; if (File.Exists(exe)) return exe; exe = Path.Combine(path?.GetValue("") as string ?? "", "python.exe"); if (File.Exists(exe)) return exe; }
            return null;
        }
        public static async Task InstallEdgeAsync(ClassicOnlineTtsSettings settings, CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            var python = string.IsNullOrWhiteSpace(settings.Python) ? FindPython() : settings.Python;
            if (!File.Exists(python)) throw new FileNotFoundException("Install Python 3.10 or newer, or select Python / an existing edge-tts.exe in settings");
            var venv = Path.Combine(ClassicOnlineTtsSettings.Root("edge"), "venv");
            Directory.CreateDirectory(Path.GetDirectoryName(venv));
            await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(python, "-m venv " + ClassicSpeechRunner.Quote(venv)), cancel, progress).ConfigureAwait(false);
            await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Path.Combine(venv, "Scripts", "python.exe"), "-m pip --disable-pip-version-check install edge-tts==7.3.1"), cancel, progress).ConfigureAwait(false);
            if (!File.Exists(Path.Combine(venv, "Scripts", "edge-tts.exe"))) throw new FileNotFoundException("Edge TTS installation did not produce an executable");
            settings.Executable = ""; settings.Save("edge");
        }
        private async Task<string> RunEdgeAsync(string arguments, CancellationToken cancel)
        {
            var exe = EdgeExecutable(_settings);
            if (!File.Exists(exe)) throw new FileNotFoundException("Install Edge TTS using Download / Settings");
            var captured = new StringBuilder();
            await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(exe, arguments), cancel, new InlineProgress(p => { lock (captured) captured.AppendLine(p.Message); })).ConfigureAwait(false);
            return captured.ToString();
        }
        private sealed class InlineProgress : IProgress<SpeechProgress> { private readonly Action<SpeechProgress> _action; public InlineProgress(Action<SpeechProgress> action) { _action = action; } public void Report(SpeechProgress value) { _action(value); } }
        public void Dispose() { _client.Dispose(); }
    }
}
