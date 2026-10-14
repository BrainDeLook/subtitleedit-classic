using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicCompatibleProvider
    {
        public string Url {get;set;}
        public string Model {get;set;}="gpt-4o-mini-tts";
        public string Voice {get;set;}="alloy";
        public string Format {get;set;}="auto";
        public string Instructions {get;set;}="";
        public double Speed {get;set;}=1;
        public string ProtectedApiKey {get;set;}="";
        [JsonIgnore] public string ApiKey
        {
            get{try{return string.IsNullOrEmpty(ProtectedApiKey) ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedApiKey),null,DataProtectionScope.CurrentUser));}catch(CryptographicException){return "";}catch(FormatException){return "";}}
            set{ProtectedApiKey=string.IsNullOrEmpty(value) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser));}
        }
    }
    public sealed class ClassicCompatibleTtsSettings
    {
        public string Provider {get;set;}="OpenAI";
        public Dictionary<string,ClassicCompatibleProvider> Providers {get;set;}=new Dictionary<string,ClassicCompatibleProvider>();
        public static string Root=>Path.Combine(Configuration.DataDirectory,"TextToSpeech","ClassicCompatible");
        public ClassicCompatibleProvider Selected()
        {
            if(!new[]{"OpenAI","OpenRouter","Custom"}.Contains(Provider))Provider="OpenAI";
            if(Providers==null)Providers=new Dictionary<string,ClassicCompatibleProvider>();
            if(!Providers.TryGetValue(Provider,out var settings) || settings==null)
            {
                Providers[Provider]=settings=new ClassicCompatibleProvider{Url=Provider=="OpenRouter" ? "https://openrouter.ai/api/v1/audio/speech" : Provider=="Custom" ? "http://127.0.0.1:8000/v1/audio/speech" : "https://api.openai.com/v1/audio/speech",Model=Provider=="OpenRouter" ? ClassicCompatibleTts.Models("OpenRouter").FirstOrDefault() ?? "" : Provider=="Custom" ? "tts" : "gpt-4o-mini-tts"};
                settings.Voice=ClassicCompatibleTts.Voices(Provider,settings.Model).FirstOrDefault() ?? "alloy";
            }
            return settings;
        }
        public static ClassicCompatibleTtsSettings Load(){try{return JsonConvert.DeserializeObject<ClassicCompatibleTtsSettings>(File.ReadAllText(Path.Combine(Root,"settings.json"))) ?? new ClassicCompatibleTtsSettings();}catch(IOException){return new ClassicCompatibleTtsSettings();}catch(JsonException){return new ClassicCompatibleTtsSettings();}}
        public void Save(){Directory.CreateDirectory(Root);File.WriteAllText(Path.Combine(Root,"settings.json"),JsonConvert.SerializeObject(this,Formatting.Indented));}
    }
    // Stable v5.3.0 OpenAiCompatibleSpeech: provider-specific settings, mp3/pcm fallback,
    // preset voices and a custom endpoint. WAV normalization uses the existing classic pipeline.
    public sealed class ClassicCompatibleTts : IDisposable
    {
        private static readonly ConcurrentDictionary<string,bool> PcmOnly=new ConcurrentDictionary<string,bool>();
        private readonly ClassicCompatibleProvider _settings;
        private readonly HttpClient _client;
        private static readonly string[] OpenAiVoices={"alloy","ash","coral","echo","fable","onyx","nova","sage","shimmer"};
        public ClassicCompatibleTts(ClassicCompatibleProvider settings,HttpMessageHandler handler=null){_settings=settings;_client=handler==null ? new HttpClient() : new HttpClient(handler);_client.Timeout=TimeSpan.FromMinutes(5);}
        public static string[] Models(string provider)=>provider=="OpenAI" ? new[]{"gpt-4o-mini-tts","tts-1","tts-1-hd"} : provider=="OpenRouter" ? RouterCatalog().Select(x=>(string)x["id"]).Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray() : Array.Empty<string>();
        public static string[] Voices(string provider,string model)
        {
            if(provider=="OpenAI")return model.StartsWith("tts-1",StringComparison.OrdinalIgnoreCase) ? OpenAiVoices : OpenAiVoices.Concat(new[]{"ballad","verse","marin","cedar"}).ToArray();
            if(provider!="OpenRouter")return Array.Empty<string>();
            var found=RouterCatalog().FirstOrDefault(x=>(string)x["id"]==model);
            return found?["supported_voices"] is JArray voices ? voices.Select(x=>(string)x).Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray() : Array.Empty<string>();
        }
        private static IEnumerable<JToken> RouterCatalog()
        {
            using(var stream=typeof(ClassicCompatibleTts).Assembly.GetManifestResourceStream("ClassicOpenRouterTtsModels.json"))
            using(var reader=new StreamReader(stream))return ((JArray)JObject.Parse(reader.ReadToEnd())["data"]).ToArray();
        }
        public static string BuildRequestJson(string text,string model,string voice,string format,double speed=1,string instructions="")
        {
            var body=new Dictionary<string,object>{{"model",model},{"input",text},{"voice",voice},{"response_format",format}};
            if(Math.Abs(speed-1)>0.001 && speed>0)body["speed"]=Math.Round(speed,2);
            if(!string.IsNullOrWhiteSpace(instructions) && !model.StartsWith("tts-1",StringComparison.OrdinalIgnoreCase))body["instructions"]=instructions.Trim();
            return JsonConvert.SerializeObject(body);
        }
        public Task SpeakAsync(string text,string voice,string output,CancellationToken cancel)=>SpeakAsync(text,voice,output,cancel,null);
        public async Task SpeakAsync(string text,string voice,string output,CancellationToken cancel,string instructions)
        {
            if(!Uri.TryCreate(_settings.Url,UriKind.Absolute,out var uri) || uri.Scheme!="https" && uri.Scheme!="http")throw new ArgumentException("Enter the full HTTP(S) speech endpoint");
            if(string.IsNullOrWhiteSpace(_settings.Model) || string.IsNullOrWhiteSpace(voice))throw new ArgumentException("Select a model and voice");
            var key=_settings.Url+"|"+_settings.Model;
            var format=_settings.Format=="auto" ? PcmOnly.ContainsKey(key) ? "pcm" : "mp3" : _settings.Format;
            for(int attempt=0;attempt<2;attempt++)
            {
                using(var request=new HttpRequestMessage(HttpMethod.Post,uri))
                {
                    if(!string.IsNullOrEmpty(_settings.ApiKey))request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",_settings.ApiKey);
                    request.Content=new StringContent(BuildRequestJson(text,_settings.Model,voice,format,_settings.Speed,instructions ?? _settings.Instructions),Encoding.UTF8,"application/json");
                    using(var response=await _client.SendAsync(request,HttpCompletionOption.ResponseContentRead,cancel).ConfigureAwait(false))
                    {
                        if(!response.IsSuccessStatusCode)
                        {
                            var error=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if(attempt==0 && _settings.Format=="auto" && error.IndexOf("response_format",StringComparison.OrdinalIgnoreCase)>=0 && error.IndexOf("pcm",StringComparison.OrdinalIgnoreCase)>=0){PcmOnly[key]=true;format="pcm";continue;}
                            throw new InvalidOperationException("TTS HTTP "+(int)response.StatusCode+": "+error);
                        }
                        var audio=await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        var normalized=ToAudioFile(audio,format,response.Content.Headers.ContentType?.ToString());
                        var temporary=output+"."+Guid.NewGuid().ToString("N")+normalized.Item2;
                        try
                        {
                            cancel.ThrowIfCancellationRequested();File.WriteAllBytes(temporary,normalized.Item1);
                            await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,"-nostdin -y -i "+ClassicSpeechRunner.Quote(temporary)+" -ar 24000 -ac 1 -c:a pcm_s16le "+ClassicSpeechRunner.Quote(output)),cancel,null).ConfigureAwait(false);
                        }
                        finally{if(File.Exists(temporary))File.Delete(temporary);}
                        return;
                    }
                }
            }
        }
        public static Tuple<byte[],string> ToAudioFile(byte[] audio,string requested,string contentType)
        {
            if(audio==null || audio.Length==0)throw new InvalidDataException("TTS returned empty audio");
            if(audio.Length>=12 && Encoding.ASCII.GetString(audio,0,4)=="RIFF" && Encoding.ASCII.GetString(audio,8,4)=="WAVE")return Tuple.Create(audio,".wav");
            var mp3=audio.Length>=3 && (Encoding.ASCII.GetString(audio,0,3)=="ID3" || audio[0]==255 && (audio[1]&224)==224);
            if(mp3 || requested!="pcm" && (contentType==null || contentType.IndexOf("pcm",StringComparison.OrdinalIgnoreCase)<0))return Tuple.Create(audio,".mp3");
            int rate=24000,channels=1;
            foreach(var part in (contentType ?? "").Split(';')){var pieces=part.Split('=');if(pieces.Length!=2 || !int.TryParse(pieces[1].Trim().Trim('"'),out var value) || value<=0)continue;var name=pieces[0].Trim();if(name=="rate" || name=="sample_rate" || name=="samplerate")rate=value;else if(name=="channels")channels=value;}
            if(channels>8 || rate>384000)throw new InvalidDataException("Unsupported PCM audio header");
            int length=audio.Length/(2*channels)*(2*channels);
            using(var stream=new MemoryStream(44+length))using(var writer=new BinaryWriter(stream))
            {writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(length+36);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)channels);writer.Write(rate);writer.Write(rate*channels*2);writer.Write((short)(channels*2));writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(length);writer.Write(audio,0,length);writer.Flush();return Tuple.Create(stream.ToArray(),".wav");}
        }
        public void Dispose(){_client.Dispose();}
    }
}
