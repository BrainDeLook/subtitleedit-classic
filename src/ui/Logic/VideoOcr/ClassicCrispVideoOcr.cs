using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic.VideoOcr
{
    // Transport/CLI adapter for stable v5.2.0 CrispEmbedOcr. Same local server,
    // health polling, image-path JSON, DS2 mode and detector/recognizer protocol.
    public static class ClassicCrispVideoOcr
    {
        public static string ModelFolder => Path.Combine(ClassicPaddleOcr.Root,"CrispEmbed","models");
        public static string EngineFolder(string variant) => Path.Combine(ClassicPaddleOcr.Root,"CrispEmbed","0.17.9",variant);
        public static string Executable(string variant,string name) => Directory.Exists(EngineFolder(variant))
            ? Directory.GetFiles(EngineFolder(variant),name+".exe",SearchOption.AllDirectories).FirstOrDefault() ?? Path.Combine(EngineFolder(variant),name+".exe")
            : Path.Combine(EngineFolder(variant),name+".exe");
        private static readonly Dictionary<string,string> Hashes=new Dictionary<string,string>
        {
            ["cpu"]="dabf9483f1a2e6e801b85a3fe67a6dab2b55359fc604c91e80a44756a21e4b07",
            ["vulkan"]="ce2d3eb91b6dda3b9a50b96cf26e70010854c0b28fa38413c87cefe732dd9fc2",
            ["cuda"]="89bcdde6d81634461278bd877e6e35f477cd6771dec7de40f0cfeeca1c5b732a"
        };
        public static CrispEmbedModel SelectedModel(ClassicVideoOcrOptions options)
        {
            var backend=ClassicCrispOcrCatalog.VideoBackends().FirstOrDefault(b=>b.Name==options.CrispBackend) ?? ClassicCrispOcrCatalog.VideoBackends().First();
            return backend.Models.FirstOrDefault(m=>m.Name==options.CrispModel) ?? backend.Models.First();
        }
        public static async Task EnsureInstalledAsync(ClassicVideoOcrOptions options,CancellationToken cancel,IProgress<SpeechProgress> progress)
        {
            var variant=options.CrispVariant;
            if(!Hashes.ContainsKey(variant)) throw new ArgumentException("Unsupported CrispEmbed build");
            var target=EngineFolder(variant);
            if(!File.Exists(Executable(variant,"crispembed")) || !File.Exists(Path.Combine(target,".complete")))
            {
                var name="crispembed-windows-x86_64"+(variant=="cpu" ? "" : "-"+variant)+".zip";
                var archive=Path.Combine(ClassicPaddleOcr.Root,"Downloads",name);
                var stage=Path.Combine(Path.GetTempPath(),"secrispocr_"+Guid.NewGuid().ToString("N").Substring(0,12));
                try
                {
                    if(!ValidHash(archive,Hashes[variant]))
                        await ClassicSpeechDownload.DownloadFileAsync("https://github.com/CrispStrobe/CrispEmbed/releases/download/v0.17.9/"+name,archive,false,cancel,progress).ConfigureAwait(false);
                    if(!ValidHash(archive,Hashes[variant])) throw new InvalidDataException("CrispEmbed archive checksum mismatch");
                    progress?.Report(new SpeechProgress{Message="Extracting CrispEmbed"});
                    await Task.Run(()=>ClassicSpeechDownload.Extract(archive,stage,cancel),cancel).ConfigureAwait(false);
                    if(Directory.GetFiles(stage,"crispembed-server.exe",SearchOption.AllDirectories).Length==0) throw new InvalidDataException("CrispEmbed server was not found in the archive");
                    Directory.CreateDirectory(target);
                    foreach(var file in Directory.GetFiles(stage,"*",SearchOption.AllDirectories))
                    {
                        cancel.ThrowIfCancellationRequested(); var destination=ClassicSpeechDownload.SafeArchivePath(target,file.Substring(stage.Length+1));
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(file,destination,true);
                    }
                    File.WriteAllText(Path.Combine(target,".complete"),Hashes[variant]);
                }
                finally {if(Directory.Exists(stage))Directory.Delete(stage,true);if(File.Exists(archive))File.Delete(archive);}
            }
            var model=SelectedModel(options);
            await EnsureModel(model.Name,model.Url,model.MinimumSizeBytes,cancel,progress).ConfigureAwait(false);
            if(model.HasDetector) await EnsureModel(model.DetectorName,model.DetectorUrl,model.MinimumDetectorSizeBytes,cancel,progress).ConfigureAwait(false);
        }
        private static bool ValidHash(string file,string expected)
        {
            if(!File.Exists(file)) return false;
            using(var input=File.OpenRead(file))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant()==expected;
        }
        private static async Task EnsureModel(string name,string url,long minimum,CancellationToken cancel,IProgress<SpeechProgress> progress)
        {
            var file=Path.Combine(ModelFolder,name);
            if(File.Exists(file) && new FileInfo(file).Length>=minimum) {using(var input=File.OpenRead(file)){var magic=new byte[4];if(input.Read(magic,0,4)==4 && Encoding.ASCII.GetString(magic)=="GGUF")return;}}
            await ClassicSpeechDownload.DownloadFileAsync(url,file,true,cancel,progress).ConfigureAwait(false);
            if(new FileInfo(file).Length<minimum) throw new InvalidDataException("CrispEmbed model download is truncated");
        }
        public static string NormalizeServerText(string text) => string.Join(Environment.NewLine,(text ?? "").Replace("\r\n","\n").Split('\n').Select(line=>line.Trim())).Trim();

        public static async Task RecognizeAsync(IReadOnlyList<VideoOcrFrameGroup> groups,string job,ClassicVideoOcrOptions options,
            CancellationToken cancel,IProgress<SpeechProgress> progress,Action<int,VideoOcrFrameGroup> resultAvailable=null)
        {
            if(groups.Count==0) return;
            var model=SelectedModel(options);
            using(var client=new HttpClient{Timeout=TimeSpan.FromMinutes(5)})
            using(var serverStop=CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {
                Task server=null; var port=0; var serverLog=new Queue<string>();
                try
                {
                    if(!model.HasDetector)
                    {
                        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
                        var args="--ocr "+VideoOcrProcess.Quote(Path.Combine(ModelFolder,model.Name))+" --host 127.0.0.1 --port "+port;
                        server=VideoOcrProcess.RunAsync(Executable(options.CrispVariant,"crispembed-server"),args,job,serverStop.Token,line=>{lock(serverLog){serverLog.Enqueue(line);while(serverLog.Count>12)serverLog.Dequeue();}});
                        var watch=Stopwatch.StartNew(); var ready=false;
                        while(watch.Elapsed<TimeSpan.FromMinutes(5))
                        {
                            cancel.ThrowIfCancellationRequested();
                            if(server.IsCompleted){await server.ConfigureAwait(false);throw new InvalidOperationException("CrispEmbed server exited before it was ready");}
                            try {using(var response=await client.GetAsync("http://127.0.0.1:"+port+"/health",cancel).ConfigureAwait(false)) if(response.IsSuccessStatusCode){ready=true;break;}}
                            catch(HttpRequestException) {}
                            await Task.Delay(250,cancel).ConfigureAwait(false);
                        }
                        if(!ready) throw new TimeoutException("CrispEmbed model did not load within five minutes");
                    }
                    for(int i=0;i<groups.Count;i++)
                    {
                        cancel.ThrowIfCancellationRequested(); string text="";
                        if(model.HasDetector)
                        {
                            var args="--ocr-pipeline "+VideoOcrProcess.Quote(groups[i].RepresentativeFileName)+" --ocr-engine ppocrv6 --ocr-det "+VideoOcrProcess.Quote(Path.Combine(ModelFolder,model.DetectorName))+" --ocr-rec "+VideoOcrProcess.Quote(Path.Combine(ModelFolder,model.Name))+" -t 4 --json";
                            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel))
                            {
                                timeout.CancelAfter(TimeSpan.FromMinutes(5));
                                await VideoOcrProcess.RunAsync(Executable(options.CrispVariant,"crispembed"),args,job,timeout.Token,line=>
                                {
                                    if(!line.TrimStart().StartsWith("{",StringComparison.Ordinal)) return;
                                    try {var json=JObject.Parse(line);if(json["full_text"]!=null) text=NormalizeServerText((string)json["full_text"]);}catch(JsonException){}
                                }).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            var input=JsonConvert.SerializeObject(new{image=groups[i].RepresentativeFileName.Replace('\\','/')});
                            using(var content=new StringContent(input,Encoding.UTF8,"application/json"))
                            using(var response=await client.PostAsync("http://127.0.0.1:"+port+"/ocr/model",content,cancel).ConfigureAwait(false))
                            {
                                var result=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                                if(!response.IsSuccessStatusCode) throw new InvalidOperationException("CrispEmbed OCR: "+response.StatusCode+" "+result);
                                var json=JObject.Parse(result);text=NormalizeServerText((string)(json["latex"] ?? json["text"]) ?? "");
                            }
                        }
                        groups[i].Text=VideoOcrLineBuilder.CleanOcrResult(text); groups[i].Confidence=1;
                        resultAvailable?.Invoke(i,groups[i]);progress?.Report(new SpeechProgress{Message="OCR "+(i+1)+" / "+groups.Count,Percent=(i+1)*100/groups.Count});
                    }
                }
                finally
                {
                    serverStop.Cancel();
                    if(server!=null){try{await server.ConfigureAwait(false);}catch(OperationCanceledException){} }
                }
            }
        }
    }
}
