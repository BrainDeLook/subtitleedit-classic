using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic.SpeechToText
{
    // REST protocols from stable 5.3; all requests can use an injected handler for offline verification.
    public sealed class ClassicCloudAsrSettings
    {
        public ClassicCompatibleAsrSettings Api { get; set; } = new ClassicCompatibleAsrSettings();
        public string Region { get; set; } = "us";
        public string KeyFile { get; set; } = "";
        public string Project { get; set; } = "";
        public string Bucket { get; set; } = "";
        public bool DynamicBatching { get; set; } = true;
        public static string FileName(string id) => Path.Combine(ClassicSpeechCatalog.Root, "cloud-" + id + ".json");
        public static ClassicCloudAsrSettings Load(string id)
        {
            try { var saved=JsonConvert.DeserializeObject<ClassicCloudAsrSettings>(File.ReadAllText(FileName(id))); if(saved?.Api!=null)return saved; }
            catch (IOException) { } catch (JsonException) { }
            var s = new ClassicCloudAsrSettings();
            s.Api.Url = id == "openrouter" ? "https://openrouter.ai/api/v1/audio/transcriptions" :
                id == "dashscope" ? "https://dashscope-intl.aliyuncs.com" : "https://us-speech.googleapis.com";
            s.Api.Model = id == "openrouter" ? "openai/whisper-1" : id == "dashscope" ? "qwen3-asr-flash-filetrans" : "chirp_3";
            s.Api.TimeoutSeconds = 3600;
            return s;
        }
        public void Save(string id)
        {
            Api.Validate();
            Directory.CreateDirectory(ClassicSpeechCatalog.Root);
            var path = FileName(id); var part = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(part, JsonConvert.SerializeObject(this, Formatting.Indented)); if (File.Exists(path)) File.Replace(part, path, null); else File.Move(part, path); }
            finally { if (File.Exists(part)) File.Delete(part); }
        }
    }

    public sealed class ClassicCloudAsr : IDisposable
    {
        private readonly string _id;
        private readonly ClassicCloudAsrSettings _settings;
        private readonly HttpClient _http;
        private string _token;
        private DateTime _tokenExpires;
        private string Base => _settings.Api.Url.TrimEnd('/');
        public ClassicCloudAsr(string id, ClassicCloudAsrSettings settings, HttpMessageHandler handler = null)
        {
            _id = id; _settings = settings;
            _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        }
        public async Task<string> TranscribeChunkAsync(string file, string language, CancellationToken cancel)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(_settings.Api.TimeoutSeconds));
                try
                {
                    if (_id == "openrouter") return await OpenRouter(file, language, deadline.Token).ConfigureAwait(false);
                    if (_id == "dashscope") return await DashScope(file, language, deadline.Token).ConfigureAwait(false);
                    return await Google(file, language, deadline.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancel.IsCancellationRequested) { throw new TimeoutException("Cloud transcription timed out."); }
            }
        }
        private static StringContent Json(object value) => new StringContent(JsonConvert.SerializeObject(value), Encoding.UTF8, "application/json");
        private async Task<JObject> Send(HttpMethod method, string url, HttpContent body, CancellationToken cancel, bool authorize = true, Action<HttpRequestMessage> headers = null)
        {
            using (var request = new HttpRequestMessage(method, url) { Content = body })
            {
                if (authorize) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _id=="google" ? await GoogleToken(cancel).ConfigureAwait(false) : _settings.Api.ApiKey);
                headers?.Invoke(request);
                using (var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancel).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) throw new HttpRequestException("Cloud ASR returned HTTP " + (int)response.StatusCode + ".");
                    var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return string.IsNullOrWhiteSpace(text) ? new JObject() : JObject.Parse(text);
                }
            }
        }
        private async Task<string> OpenRouter(string file, string language, CancellationToken cancel)
        {
            var modelName = _settings.Api.Model.Substring(_settings.Api.Model.LastIndexOf('/')+1);
            var plain = modelName.StartsWith("gpt-",StringComparison.OrdinalIgnoreCase) && modelName.IndexOf("transcribe", StringComparison.OrdinalIgnoreCase) >= 0;
            var payload = new JObject { ["model"] = _settings.Api.Model,
                ["input_audio"] = new JObject { ["data"] = Convert.ToBase64String(File.ReadAllBytes(file)), ["format"] = "wav" },
                ["response_format"] = plain ? "json" : "verbose_json" };
            if (!plain) payload["timestamp_granularities"] = new JArray("segment", "word");
            if (!string.IsNullOrWhiteSpace(language) && language != "auto") payload["language"] = language;
            if (!string.IsNullOrWhiteSpace(_settings.Api.Prompt)) payload["prompt"] = _settings.Api.Prompt;
            using (var first = new HttpRequestMessage(HttpMethod.Post, Base) { Content = Json(payload) })
            {
                first.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Api.ApiKey);
                using (var response = await _http.SendAsync(first, cancel).ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode) return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (plain || (int)response.StatusCode != 400) throw new HttpRequestException("OpenRouter ASR returned HTTP " + (int)response.StatusCode + ".");
                }
            }
            payload["response_format"] = "json"; payload.Remove("timestamp_granularities");
            return (await Send(HttpMethod.Post, Base, Json(payload), cancel).ConfigureAwait(false)).ToString();
        }
        private async Task<string> DashScope(string file, string language, CancellationToken cancel)
        {
            var policy = (await Send(HttpMethod.Get, Base + "/api/v1/uploads?action=getPolicy&model=" + Uri.EscapeDataString(_settings.Api.Model), null, cancel).ConfigureAwait(false))["data"];
            if (policy == null) throw new InvalidDataException("DashScope returned no upload policy.");
            var key = (string)policy["upload_dir"] + "/" + Guid.NewGuid().ToString("N") + ".wav";
            using (var form = new MultipartFormDataContent())
            {
                foreach (var field in new[] { new[] { "OSSAccessKeyId", "oss_access_key_id" }, new[] { "policy", "policy" }, new[] { "Signature", "signature" }, new[] { "x-oss-object-acl", "x_oss_object_acl" }, new[] { "x-oss-forbid-overwrite", "x_oss_forbid_overwrite" } })
                    form.Add(new StringContent((string)policy[field[1]] ?? ""), field[0]);
                form.Add(new StringContent(key), "key"); form.Add(new StringContent("200"), "success_action_status");
                form.Add(new ByteArrayContent(File.ReadAllBytes(file)), "file", "audio.wav");
                // The signed upload host must not receive the user's bearer key.
                await Send(HttpMethod.Post, (string)policy["upload_host"], form, cancel, false).ConfigureAwait(false);
            }
            var parameters = new JObject { ["enable_itn"] = false, ["enable_words"] = true };
            if (language != "auto" && !string.IsNullOrWhiteSpace(language)) parameters["language"] = language;
            var submit = await Send(HttpMethod.Post, Base + "/api/v1/services/audio/asr/transcription",
                Json(new { model = _settings.Api.Model, input = new { file_url = "oss://" + key }, parameters }), cancel, true,
                r => { r.Headers.Add("X-DashScope-Async", "enable"); r.Headers.Add("X-DashScope-OssResourceResolve", "enable"); }).ConfigureAwait(false);
            var id = (string)submit["output"]?["task_id"];
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("DashScope returned no task identifier.");
            while (true)
            {
                var task = (await Send(HttpMethod.Get, Base + "/api/v1/tasks/" + Uri.EscapeDataString(id), null, cancel).ConfigureAwait(false))["output"];
                var status = ((string)task?["task_status"] ?? "UNKNOWN").ToUpperInvariant();
                if (status == "SUCCEEDED")
                {
                    var url = (string)(task["result"]?["transcription_url"] ?? task["results"]?.First?["transcription_url"]);
                    if (string.IsNullOrWhiteSpace(url)) throw new InvalidDataException("DashScope returned no transcript URL.");
                    var result = await Send(HttpMethod.Get, url, null, cancel, false).ConfigureAwait(false);
                    var rows = result.SelectTokens("transcripts[*].sentences[*]").Select(s => new JObject {
                        ["start"] = (double)s["begin_time"] / 1000, ["end"] = (double)s["end_time"] / 1000, ["text"] = s["text"] }).ToArray();
                    return new JObject { ["segments"] = new JArray(rows) }.ToString();
                }
                if (status == "FAILED" || status == "UNKNOWN") throw new InvalidOperationException("DashScope transcription failed.");
                await Task.Delay(3000, cancel).ConfigureAwait(false);
            }
        }
        private async Task<string> GoogleToken(CancellationToken cancel)
        {
            if (_token != null && DateTime.UtcNow < _tokenExpires) return _token;
            var path = _settings.KeyFile;
            if (string.IsNullOrWhiteSpace(path)) path = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
            if (string.IsNullOrWhiteSpace(path)) path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcloud", "application_default_credentials.json");
            var credentials = JObject.Parse(File.ReadAllText(path));
            if((string)credentials["type"]!="authorized_user" && (string)credentials["type"]!="service_account")throw new InvalidDataException("Use a Google service-account JSON key or gcloud authorized-user ADC.");
            _settings.Project = string.IsNullOrWhiteSpace(_settings.Project) ? (string)(credentials["project_id"] ?? credentials["quota_project_id"]) : _settings.Project;
            HttpContent body;
            if ((string)credentials["type"] == "authorized_user")
                body = new FormUrlEncodedContent(new[] { new System.Collections.Generic.KeyValuePair<string,string>("grant_type","refresh_token"),
                    new System.Collections.Generic.KeyValuePair<string,string>("refresh_token",(string)credentials["refresh_token"]),
                    new System.Collections.Generic.KeyValuePair<string,string>("client_id",(string)credentials["client_id"]),
                    new System.Collections.Generic.KeyValuePair<string,string>("client_secret",(string)credentials["client_secret"]) });
            else body = new FormUrlEncodedContent(new[] { new System.Collections.Generic.KeyValuePair<string,string>("grant_type","urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new System.Collections.Generic.KeyValuePair<string,string>("assertion",ClassicOnlineTts.CreateGoogleAssertion((string)credentials["client_email"],(string)credentials["private_key"],DateTimeOffset.UtcNow.ToUnixTimeSeconds())) });
            var result = await Send(HttpMethod.Post, "https://oauth2.googleapis.com/token", body, cancel, false).ConfigureAwait(false);
            _token = (string)result["access_token"];
            _tokenExpires = DateTime.UtcNow.AddSeconds(Math.Max(0, ((int?)result["expires_in"] ?? 3600) - 60));
            if (string.IsNullOrWhiteSpace(_token)) throw new InvalidDataException("Google returned no access token.");
            return _token;
        }
        private async Task<string> Google(string file, string language, CancellationToken cancel, bool recover = true)
        {
            await GoogleToken(cancel).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(_settings.Project)) throw new ArgumentException("Enter the Google Cloud project ID.");
            var bucket = _settings.Bucket;
            if (string.IsNullOrWhiteSpace(bucket)) bucket = (_settings.Project.Length > 40 ? _settings.Project.Substring(0, 40) : _settings.Project) + "-subtitle-edit-stt";
            // Create only when absent. Never change the lifecycle of an existing user bucket.
            using (var request = new HttpRequestMessage(HttpMethod.Get, "https://storage.googleapis.com/storage/v1/b/" + Uri.EscapeDataString(bucket)))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                using (var response = await _http.SendAsync(request, cancel).ConfigureAwait(false))
                {
                    if ((int)response.StatusCode == 404) await Send(HttpMethod.Post, "https://storage.googleapis.com/storage/v1/b?project=" + Uri.EscapeDataString(_settings.Project),
                        Json(new { name = bucket, location = _settings.Region == "eu" ? "EU" : "US", lifecycle = new { rule = new[] { new { action = new { type = "Delete" }, condition = new { age = 1 } } } } }), cancel).ConfigureAwait(false);
                    else if (!response.IsSuccessStatusCode) throw new HttpRequestException("Cannot access the Google Cloud bucket.");
                }
            }
            var name = "subtitle-edit/" + Guid.NewGuid().ToString("N") + ".wav";
            var objectUrl = "https://storage.googleapis.com/storage/v1/b/" + Uri.EscapeDataString(bucket) + "/o/" + Uri.EscapeDataString(name);
            var uploaded = false;
            try
            {
                uploaded = true;
                await Send(HttpMethod.Post, "https://storage.googleapis.com/upload/storage/v1/b/" + Uri.EscapeDataString(bucket) + "/o?uploadType=media&name=" + Uri.EscapeDataString(name), new ByteArrayContent(File.ReadAllBytes(file)), cancel).ConfigureAwait(false);
                var config = new JObject { ["autoDecodingConfig"] = new JObject(), ["languageCodes"] = new JArray(language == "en" ? "en-US" : language == "ru" ? "ru-RU" : language == "ja" ? "ja-JP" : language),
                    ["model"] = _settings.Api.Model, ["features"] = new JObject { ["enableWordTimeOffsets"] = true, ["enableAutomaticPunctuation"] = true } };
                var body = new JObject { ["config"] = config, ["files"] = new JArray(new JObject { ["uri"] = "gs://" + bucket + "/" + name }), ["recognitionOutputConfig"] = new JObject { ["inlineResponseConfig"] = new JObject() } };
                if (_settings.DynamicBatching) body["processingStrategy"] = "DYNAMIC_BATCHING";
                var operation = await Send(HttpMethod.Post, Base + "/v2/projects/" + Uri.EscapeDataString(_settings.Project) + "/locations/" + Uri.EscapeDataString(_settings.Region) + "/recognizers/_:batchRecognize", Json(body), cancel).ConfigureAwait(false);
                var operationName = (string)operation["name"];
                if (string.IsNullOrWhiteSpace(operationName)) throw new InvalidDataException("Google returned no operation name.");
                while (true)
                {
                    var state = await Send(HttpMethod.Get, Base + "/v2/" + operationName, null, cancel).ConfigureAwait(false);
                    if ((bool?)state["done"] == true)
                    {
                        if (state["error"] != null) throw new InvalidOperationException("Google Cloud transcription failed.");
                        var parsed = JObject.Parse(ParseGoogle(state));
                        var billed = (double?)parsed["duration"] ?? 0;
                        var covered = parsed["words"].Select(w => (double)w["end"]).DefaultIfEmpty(0).Max();
                        if (recover && billed > 60 && covered < billed * .9)
                        {
                            var resume = Math.Max(0, covered - 1);
                            var tailFile = Path.Combine(Path.GetDirectoryName(file), Guid.NewGuid().ToString("N") + "-tail.wav");
                            try
                            {
                                SeLogger.Error("Google Cloud ASR: recovering transcript tail from " + resume.ToString(System.Globalization.CultureInfo.InvariantCulture) + " seconds.");
                                var args="-hide_banner -nostdin -y -ss "+resume.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" -i "+ClassicSpeechRunner.Quote(file)+" -vn -ac 1 -ar 16000 -c:a pcm_s16le "+ClassicSpeechRunner.Quote(tailFile);
                                await ClassicSpeechRunner.RunProcessAsync(new System.Diagnostics.ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,args),cancel,null).ConfigureAwait(false);
                                var tail=JObject.Parse(await Google(tailFile,language,cancel,false).ConfigureAwait(false));
                                parsed["words"]=MergeGoogleTail((JArray)parsed["words"],(JArray)tail["words"],resume);
                            }
                            finally { if(File.Exists(tailFile))File.Delete(tailFile); }
                        }
                        return parsed.ToString();
                    }
                    await Task.Delay(5000, cancel).ConfigureAwait(false);
                }
            }
            finally
            {
                if (uploaded) using (var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                    try { await Send(HttpMethod.Delete, objectUrl, null, cleanup.Token).ConfigureAwait(false); }
                    catch (Exception e) when (e is HttpRequestException || e is OperationCanceledException) { }
            }
        }
        public static string ParseGoogle(JObject state)
        {
            var words = new JArray();
            var billed=Seconds(state["response"]?["totalBilledDuration"]);
            var largest=state.SelectTokens("response.results.*.inlineResult.transcript.results[*].resultEndOffset").Select(Seconds).DefaultIfEmpty(0).Max();
            var bound=billed>0?billed:largest;double previous=0;
            foreach (var file in (state["response"]?["results"] as JObject ?? new JObject()).Properties())
            {
                if (file.Value["error"] != null) throw new InvalidOperationException("Google Cloud returned a file error.");
                foreach (var result in file.Value.SelectTokens("inlineResult.transcript.results[*]"))
                {
                    var count=words.Count;
                    var alternative = result["alternatives"]?.First;
                    foreach (var word in alternative?["words"] as JArray ?? new JArray())
                    {
                        var start = Seconds(word["startOffset"]); var end = Seconds(word["endOffset"]);
                        if (start >= 0 && end > start && (bound<=0 || end<=bound+1)) { words.Add(new JObject { ["word"] = word["word"], ["start"] = start, ["end"] = end });previous=Math.Max(previous,end); }
                    }
                    if(count==words.Count && !string.IsNullOrWhiteSpace((string)alternative?["transcript"]))
                    {
                        var end=Seconds(result["resultEndOffset"]);if(end>previous && (bound<=0 || end<=bound+1)){words.Add(new JObject{["word"]=alternative["transcript"],["start"]=previous,["end"]=end});previous=end;}
                    }
                }
            }
            if (words.Count == 0) throw new InvalidDataException("Google returned no timed words.");
            return new JObject { ["words"] = words, ["duration"] = billed }.ToString();
        }
        public static JArray MergeGoogleTail(JArray original,JArray tail,double resume)
        {
            var merged=(JArray)original.DeepClone();var covered=original.Select(w=>(double)w["end"]).DefaultIfEmpty(0).Max();
            foreach(var word in tail)
            {
                var start=(double)word["start"]+resume;var end=(double)word["end"]+resume;
                if(start<=covered-.5 || end<=covered)continue;
                merged.Add(new JObject{["word"]=word["word"],["start"]=start,["end"]=end});
            }
            return merged;
        }
        private static double Seconds(JToken token) => token == null ? 0 : double.Parse(((string)token).TrimEnd('s'), System.Globalization.CultureInfo.InvariantCulture);
        public void Dispose() => _http.Dispose();
    }
}
