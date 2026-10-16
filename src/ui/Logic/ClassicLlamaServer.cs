using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicLlamaSettings
    {
        public string Variant { get; set; } = "vulkan";
        public string Model { get; set; } = "Qwen_Qwen3-4B-Instruct-2507-Q4_K_M.gguf";
        public string Folder { get; set; } = "";
        public int Context { get; set; } = 8192;
        public int GpuLayers { get; set; } = 99;
        public static string FileName => Path.Combine(Configuration.DataDirectory, "ClassicAI", "llama.json");
        public static ClassicLlamaSettings Load() { try { return JsonConvert.DeserializeObject<ClassicLlamaSettings>(File.ReadAllText(FileName)) ?? new ClassicLlamaSettings(); } catch (IOException) { } catch (JsonException) { } return new ClassicLlamaSettings(); }
        public void Validate()
        {
            if (!new[] { "cpu", "vulkan", "cuda", "cuda13" }.Contains(Variant)) throw new ArgumentException("Select cpu, vulkan, cuda or cuda13.");
            if (Context < 2048 || Context > 32768 || GpuLayers < 0 || GpuLayers > 999) throw new ArgumentException("Invalid context size or GPU layer count.");
            if (string.IsNullOrWhiteSpace(Model) || Path.GetFileName(Model) != Model) throw new ArgumentException("Select a model file name.");
            if (Folder.Length > 0 && !Path.IsPathRooted(Folder)) throw new ArgumentException("Use an absolute model folder path.");
        }
        public void Save() { Validate(); Directory.CreateDirectory(Path.GetDirectoryName(FileName)); var temp=FileName+"."+Guid.NewGuid().ToString("N")+".tmp"; try { File.WriteAllText(temp,JsonConvert.SerializeObject(this,Formatting.Indented));if(File.Exists(FileName))File.Replace(temp,FileName,null);else File.Move(temp,FileName); } finally { if(File.Exists(temp))File.Delete(temp); } }
    }
    public static class ClassicLlamaServer
    {
        public const string Release = "b11553";
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static Process _server;
        private static string _identity, _url;
        private static readonly StringBuilder Log = new StringBuilder();
        static ClassicLlamaServer() { AppDomain.CurrentDomain.ProcessExit += (s,e) => Stop(); }
        public static string Root => Path.Combine(Configuration.DataDirectory,"ClassicAI","llama");
        public static string Models(ClassicLlamaSettings s) => s.Folder.Length > 0 ? s.Folder : Path.Combine(Root,"models");
        public static string Runtime(ClassicLlamaSettings s) => Path.Combine(Root,Release,s.Variant);
        public static string Executable(ClassicLlamaSettings s) => Path.Combine(Runtime(s),"llama-server.exe");
        public static bool IsRunning => _server != null && !_server.HasExited;
        public static LlamaCppModel Definition(string file) => ClassicLlamaCatalog.TranslateModels.Concat(ClassicLlamaCatalog.ReviewModels).Concat(ClassicLlamaCatalog.OcrModels).FirstOrDefault(m=>m.FileName==file);
        public static bool Installed(ClassicLlamaSettings s) => File.Exists(Executable(s)) && File.Exists(Path.Combine(Runtime(s),"verified.txt")) && File.Exists(Path.Combine(Models(s),s.Model));
        public static async Task EnsureInstalled(ClassicLlamaSettings s,CancellationToken cancel,IProgress<SpeechProgress> progress)
        {
            s.Validate(); await Gate.WaitAsync(cancel).ConfigureAwait(false);
            try
            {
                if (!File.Exists(Executable(s)) || !File.Exists(Path.Combine(Runtime(s),"verified.txt")))
                {
                    var suffix=s.Variant=="cuda13"?"cuda-13.4":s.Variant=="cuda"?"cuda-12.4":s.Variant;
                    var hash=s.Variant=="cpu"?"5b1d1648f176d981a22e45e069eb52732437f824ea6a6159ca78e4a4bf792b07":s.Variant=="vulkan"?"fd9b554903bf55d696f60c86e259027608cb67ebd2f632c3684cdcda044d7380":s.Variant=="cuda13"?"3f1e149ec3cbcfe75a542f4063f50b29d689ed0abcaea0f3a00021eec5a8b7db":"2696e34b159ffe5640452d4e37bb6c063307b3c611e0e549aa55b63f2a157d42";
                    await InstallArchive("llama-"+Release+"-bin-win-"+suffix+"-x64.zip",hash,s,cancel,progress).ConfigureAwait(false);
                    if(s.Variant=="cuda" || s.Variant=="cuda13") await InstallArchive("cudart-llama-bin-win-"+suffix+"-x64.zip",s.Variant=="cuda13"?"738f8c251ac22b70c3ae6f83a10cf222725df0395246a2cf58f32bdb85fbe668":"8c79a9b226de4b3cacfd1f83d24f962d0773be79f1e7b75c6af4ded7e32ae1d6",s,cancel,progress).ConfigureAwait(false);
                    File.WriteAllText(Path.Combine(Runtime(s),"verified.txt"),Release);
                }
                var definition=Definition(s.Model);
                if(definition==null && !File.Exists(Path.Combine(Models(s),s.Model)))throw new FileNotFoundException("Import a GGUF model or select a catalog model first.");
                if(definition!=null)
                foreach(var item in new[]{new[]{definition.FileName,definition.Url},new[]{definition.MmprojFileName,definition.MmprojUrl}})
                    if(!string.IsNullOrEmpty(item[0]) && !File.Exists(Path.Combine(Models(s),item[0])))await ClassicSpeechDownload.DownloadFileAsync(item[1],Path.Combine(Models(s),item[0]),true,cancel,progress).ConfigureAwait(false);
            } finally { Gate.Release(); }
        }
        private static async Task InstallArchive(string name,string hash,ClassicLlamaSettings s,CancellationToken cancel,IProgress<SpeechProgress> progress)
        {
            var stage=Path.Combine(Root,Guid.NewGuid().ToString("N")+".staging");var zip=stage+".zip";
            try
            {
                await ClassicSpeechDownload.DownloadFileAsync("https://github.com/ggml-org/llama.cpp/releases/download/"+Release+"/"+name,zip,false,cancel,progress).ConfigureAwait(false);
                await ClassicVoiceAssets.CheckHashAsync(zip,hash,cancel).ConfigureAwait(false);
                await Task.Run(()=>ClassicSpeechDownload.Extract(zip,stage,cancel),cancel).ConfigureAwait(false);
                Directory.CreateDirectory(Runtime(s));
                foreach(var file in Directory.GetFiles(stage,"*",SearchOption.AllDirectories))File.Copy(file,Path.Combine(Runtime(s),Path.GetFileName(file)),true);
            } finally { if(File.Exists(zip))File.Delete(zip);if(Directory.Exists(stage))Directory.Delete(stage,true); }
        }
        public static string Arguments(ClassicLlamaSettings s,int port)
        {
            s.Validate();var d=Definition(s.Model);
            return "--model "+ClassicSpeechRunner.Quote(Path.Combine(Models(s),s.Model))+" --alias classic-local --host 127.0.0.1 --port "+port+" --ctx-size "+s.Context+" --parallel 1 --n-gpu-layers "+(s.Variant=="cpu"?0:s.GpuLayers)+
                (d?.NoThinking==true?" --reasoning off":"")+(d?.NoJinja==true?" --no-jinja":"")+(d?.ChatTemplate!=null?" --chat-template "+ClassicSpeechRunner.Quote(d.ChatTemplate):"")+
                (d?.MmprojFileName!=null?" --mmproj "+ClassicSpeechRunner.Quote(Path.Combine(Models(s),d.MmprojFileName)):"");
        }
        public static async Task<string> Start(ClassicLlamaSettings s,CancellationToken cancel,IProgress<SpeechProgress> progress=null)
        {
            await Gate.WaitAsync(cancel).ConfigureAwait(false);
            try
            {
                s.Validate();var identity=JsonConvert.SerializeObject(s);
                if(IsRunning && identity==_identity)return _url;
                Stop();if(!Installed(s))throw new FileNotFoundException("Open Local llama.cpp settings and download the engine and model first.");
                var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();_url="http://127.0.0.1:"+port+"/v1/chat/completions";
                Log.Clear();_server=new Process{StartInfo=new ProcessStartInfo(Executable(s),Arguments(s,port)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Runtime(s),StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8}};
                DataReceivedEventHandler capture=(o,e)=>{if(e.Data!=null)lock(Log){Log.AppendLine(e.Data);if(Log.Length>12000)Log.Remove(0,Log.Length-12000);}};
                _server.OutputDataReceived+=capture;_server.ErrorDataReceived+=capture;_server.Start();_server.BeginOutputReadLine();_server.BeginErrorReadLine();progress?.Report(new SpeechProgress{Message="Loading "+s.Model});
                try
                {
                    using(var http=new HttpClient(new HttpClientHandler{UseProxy=false}){Timeout=TimeSpan.FromSeconds(2)})
                    for(var attempt=0;attempt<600;attempt++)
                    {
                        cancel.ThrowIfCancellationRequested();if(_server.HasExited)throw new InvalidOperationException("llama.cpp exited while loading. "+Log.ToString());
                        try { using(var r=await http.GetAsync("http://127.0.0.1:"+port+"/health",cancel).ConfigureAwait(false))if(r.IsSuccessStatusCode){_identity=identity;return _url;} }
                        catch(HttpRequestException){}catch(OperationCanceledException){cancel.ThrowIfCancellationRequested();}
                        await Task.Delay(500,cancel).ConfigureAwait(false);
                    }
                    throw new TimeoutException("llama.cpp model loading timed out.");
                } catch { Stop();throw; }
            } finally { Gate.Release(); }
        }
        public static void Stop()
        {
            var process=_server;_server=null;_identity=null;
            if(process==null)return;
            try { if(!process.HasExited){process.Kill();process.WaitForExit(5000);} }catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}finally{process.Dispose();}
        }
    }
}
