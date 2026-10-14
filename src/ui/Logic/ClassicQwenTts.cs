using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic
{
    // Protocol, models and speaker IDs adapted from stable v5.2.0 Qwen3TtsCrispAsr.
    public sealed class ClassicQwenTtsSettings
    {
        public string Variant { get; set; } = "cpu";
        public string Language { get; set; } = "ru";
        public string Voice { get; set; } = "vivian";
        public string Instructions { get; set; } = "";
        public bool Clone { get; set; }
        public string ExportFolder { get; set; } = "";
        public static string Root => Path.Combine(Configuration.DataDirectory,"TextToSpeech","ClassicQwen3");
        public static ClassicQwenTtsSettings Load()
        {
            try { return JsonConvert.DeserializeObject<ClassicQwenTtsSettings>(File.ReadAllText(Path.Combine(Root,"settings.json"))) ?? new ClassicQwenTtsSettings(); }
            catch(IOException) { return new ClassicQwenTtsSettings(); }
            catch(JsonException) { return new ClassicQwenTtsSettings(); }
        }
        public void Save() { Directory.CreateDirectory(Root); File.WriteAllText(Path.Combine(Root,"settings.json"),JsonConvert.SerializeObject(this,Formatting.Indented)); }
    }
    public sealed class ClassicQwenTts : IDisposable
    {
        public static readonly string[] Voices={"aiden","dylan","eric","ono_anna","ryan","serena","sohee","uncle_fu","vivian"};
        public const string Talker="qwen3-tts-12hz-1.7b-customvoice-q8_0.gguf";
        public const string CloneTalker="qwen3-tts-12hz-1.7b-base-q8_0.gguf";
        public const string Codec="qwen3-tts-tokenizer-12hz.gguf";
        private static readonly SpeechEngine Engine=new SpeechEngine{Family="crisp"};
        private readonly ClassicQwenTtsSettings _settings;
        private readonly HttpClient _client=new HttpClient(new HttpClientHandler{UseProxy=false}){Timeout=TimeSpan.FromMinutes(10)};
        private readonly StringBuilder _log=new StringBuilder();
        private Process _server;
        private string _baseUrl;
        public ClassicQwenTts(ClassicQwenTtsSettings settings) { _settings=settings; }
        public static string Models => Path.Combine(ClassicQwenTtsSettings.Root,"models");
        public static bool ValidModel(string path,long bytes)
        {
            if(!File.Exists(path) || new FileInfo(path).Length!=bytes) return false;
            using(var input=File.OpenRead(path)) {var magic=new byte[4];return input.Read(magic,0,4)==4 && Encoding.ASCII.GetString(magic)=="GGUF";}
        }
        public static async Task EnsureInstalledAsync(ClassicQwenTtsSettings settings,CancellationToken cancel,IProgress<SpeechProgress> progress)
        {
            await ClassicSpeechDownload.EnsureRuntimeAsync(Engine,settings.Variant,cancel,progress).ConfigureAwait(false);
            if(settings.Clone)
                await EnsureModel(CloneTalker,"qwen3-tts-1.7b-base-GGUF",2066258176L,"bbb93ab1f4a3f771f94fc6f68404b2b969bdf3b14c6303473dbf64c63011520d",cancel,progress).ConfigureAwait(false);
            else await EnsureModel(Talker,"qwen3-tts-1.7b-customvoice-GGUF",2042225952L,"3985813e21a3f27407110ba3afcd8f12ebc19734a5160ec31992637cd62aa67e",cancel,progress).ConfigureAwait(false);
            await EnsureModel(Codec,"qwen3-tts-tokenizer-12hz-GGUF",358453280L,"70dc95dbfdd9aa5d9d406236ff771d061bf17b0cda02a72513953355606e719b",cancel,progress).ConfigureAwait(false);
        }
        private static async Task EnsureModel(string file,string repository,long bytes,string hash,CancellationToken cancel,IProgress<SpeechProgress> progress)
        {
            var path=Path.Combine(Models,file);
            if(ValidModel(path,bytes)) return;
            // Publish only after length/header/hash validation. Preserve any previous file.
            var candidate=path+"."+Guid.NewGuid().ToString("N")+".download";
            try
            {
                await ClassicSpeechDownload.DownloadFileAsync("https://huggingface.co/cstr/"+repository+"/resolve/main/"+file,candidate,true,cancel,progress).ConfigureAwait(false);
                if(!ValidModel(candidate,bytes)) throw new InvalidDataException("Incomplete TTS model: "+file);
                if(hash!=null)
                {
                    var actual=await Task.Run(()=>{using(var sha=SHA256.Create())using(var input=File.OpenRead(candidate))return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();},cancel).ConfigureAwait(false);
                    if(actual!=hash) throw new InvalidDataException("TTS model checksum mismatch: "+file);
                }
                cancel.ThrowIfCancellationRequested();
                if(File.Exists(path)) File.Replace(candidate,path,null);else File.Move(candidate,path);
            }
            finally {if(File.Exists(candidate))File.Delete(candidate);}
        }
        public async Task StartAsync(CancellationToken cancel,IProgress<SpeechProgress> progress=null)
        {
            await EnsureInstalledAsync(_settings,cancel,progress).ConfigureAwait(false);
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
            _baseUrl="http://127.0.0.1:"+port;
            var voices=Path.Combine(ClassicQwenTtsSettings.Root,"voices");Directory.CreateDirectory(voices);
            var exe=ClassicSpeechCatalog.Executable(Engine,_settings.Variant);
            _server=new Process{StartInfo=new ProcessStartInfo(exe,"--server --backend "+(_settings.Clone ? "qwen3-tts-1.7b-base" : "qwen3-tts-1.7b-customvoice")+" -m "+ClassicSpeechRunner.Quote(Path.Combine(Models,_settings.Clone ? CloneTalker : Talker))+" --codec-model "+ClassicSpeechRunner.Quote(Path.Combine(Models,Codec))+" --voice-dir "+ClassicSpeechRunner.Quote(voices)+" --host 127.0.0.1 --port "+port)
                {WorkingDirectory=Path.GetDirectoryName(exe),UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,StandardErrorEncoding=Encoding.UTF8,StandardOutputEncoding=Encoding.UTF8}};
            DataReceivedEventHandler capture=(s,e)=>{if(e.Data!=null)lock(_log){_log.AppendLine(e.Data);if(_log.Length>16000)_log.Remove(0,_log.Length-16000);}};
            _server.OutputDataReceived+=capture;_server.ErrorDataReceived+=capture;
            _server.Start();_server.BeginOutputReadLine();_server.BeginErrorReadLine();
            progress?.Report(new SpeechProgress{Message="Loading Qwen3 TTS model…"});
            try
            {
                var watch=Stopwatch.StartNew();
                while(watch.Elapsed<TimeSpan.FromMinutes(5))
                {
                    cancel.ThrowIfCancellationRequested();if(_server.HasExited)throw new InvalidOperationException("TTS server exited: "+Log());
                    using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel))
                    {
                        timeout.CancelAfter(2000);
                        try {using(var response=await _client.GetAsync(_baseUrl+"/health",timeout.Token).ConfigureAwait(false))if(response.IsSuccessStatusCode)return;}
                        catch(HttpRequestException) {}catch(OperationCanceledException){cancel.ThrowIfCancellationRequested();}
                    }
                    await Task.Delay(200,cancel).ConfigureAwait(false);
                }
                throw new TimeoutException("TTS model loading timed out: "+Log());
            }
            catch {Dispose();throw;}
        }
        private string Log() {lock(_log)return _log.ToString();}
        public async Task SpeakAsync(string text,string voice,string output,CancellationToken cancel)
        {
            if(_settings.Clone) ClassicQwenReference.Validate(voice);
            else if(Array.IndexOf(Voices,voice)<0)throw new ArgumentException("Select a Qwen3 CustomVoice speaker",nameof(voice));
            var payload=new System.Collections.Generic.Dictionary<string,object>{{"input",text},{"response_format","wav"},{"voice",voice},{"language",_settings.Language}};
            if(_settings.Clone) {
                payload["consent_attestation"]="I have the speaker's consent, or it is my own voice.";
                payload["marking_attestation"]="I will disclose that this audio is AI-generated.";
                payload["spoken_disclaimer"]=false;
            }
            else if(!string.IsNullOrWhiteSpace(_settings.Instructions))payload["instructions"]=_settings.Instructions;
            using(var content=new StringContent(JsonConvert.SerializeObject(payload),Encoding.UTF8,"application/json"))
            using(var response=await _client.PostAsync(_baseUrl+"/v1/audio/speech",content,cancel).ConfigureAwait(false))
            {
                if(!response.IsSuccessStatusCode)throw new InvalidOperationException("TTS request failed: "+await response.Content.ReadAsStringAsync().ConfigureAwait(false)+"\n"+Log());
                var bytes=await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if(bytes.Length<44 || Encoding.ASCII.GetString(bytes,0,4)!="RIFF" || Encoding.ASCII.GetString(bytes,8,4)!="WAVE")throw new InvalidDataException("TTS returned invalid WAV audio");
                cancel.ThrowIfCancellationRequested();File.WriteAllBytes(output,bytes);
            }
        }
        public void Dispose()
        {
            if(_server!=null){try{if(!_server.HasExited){_server.Kill();_server.WaitForExit(5000);}}catch(InvalidOperationException){}finally{_server.Dispose();_server=null;}}
            _client.Dispose();
        }
    }
}

