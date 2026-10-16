using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic.SpeechToText
{
 public sealed class ClassicCompatibleAsrSettings
 {
  public string Url {get;set;}="https://api.openai.com/v1";
  public string Model {get;set;}="whisper-1";
  public string Prompt {get;set;}="";
  public int ChunkSeconds {get;set;}=300;
  public int TimeoutSeconds {get;set;}=180;
  public string ProtectedApiKey {get;set;}="";
  public bool Stream {get;set;}
  [JsonIgnore] public string ApiKey {
   get{try{return string.IsNullOrEmpty(ProtectedApiKey)?"":Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedApiKey),null,DataProtectionScope.CurrentUser));}catch(CryptographicException){return "";}catch(FormatException){return "";}}
   set{ProtectedApiKey=string.IsNullOrEmpty(value)?"":Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser));}
  }
  public static string FileName=>Path.Combine(ClassicSpeechCatalog.Root,"compatible-api.json");
  public Uri Endpoint(){if(!Uri.TryCreate(Url,UriKind.Absolute,out var uri) || (uri.Scheme!="http" && uri.Scheme!="https") || uri.UserInfo.Length>0 || uri.Query.Length>0 || uri.Fragment.Length>0)throw new ArgumentException("Enter an HTTP(S) server URL without credentials, query or fragment.");var path=uri.AbsolutePath.TrimEnd('/');return path.EndsWith("/transcriptions",StringComparison.OrdinalIgnoreCase)||path.EndsWith("/stt",StringComparison.OrdinalIgnoreCase) ? uri : new Uri(uri.AbsoluteUri.TrimEnd('/')+"/audio/transcriptions");}
  public void Validate(){Endpoint();if(string.IsNullOrWhiteSpace(Model))throw new ArgumentException("Enter the transcription model name.");if(ChunkSeconds<5 || ChunkSeconds>600 || TimeoutSeconds<10 || TimeoutSeconds>3600)throw new ArgumentException("Invalid chunk duration or request timeout.");}
  public static ClassicCompatibleAsrSettings Load(){try{return JsonConvert.DeserializeObject<ClassicCompatibleAsrSettings>(File.ReadAllText(FileName))??new ClassicCompatibleAsrSettings();}catch(IOException){return new ClassicCompatibleAsrSettings();}catch(JsonException){return new ClassicCompatibleAsrSettings();}}
  public void Save(){Validate();Directory.CreateDirectory(Path.GetDirectoryName(FileName));var temp=FileName+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,JsonConvert.SerializeObject(this,Formatting.Indented));if(File.Exists(FileName))File.Replace(temp,FileName,null);else File.Move(temp,FileName);}finally{if(File.Exists(temp))File.Delete(temp);}}
 }
 public sealed class ClassicCompatibleAsr : IDisposable
 {
  private readonly HttpClient _client;
  private readonly ClassicCompatibleAsrSettings _settings;
  private readonly Func<string,string,CancellationToken,Task<string>> _transcribe;
  public ClassicCompatibleAsr(ClassicCompatibleAsrSettings settings,HttpMessageHandler handler=null,Func<string,string,CancellationToken,Task<string>> transcribe=null){_settings=settings;_transcribe=transcribe;_client=new HttpClient(handler??new HttpClientHandler{AllowAutoRedirect=false});_client.Timeout=Timeout.InfiniteTimeSpan;}
  public async Task<Subtitle> TranscribeAsync(string wave,string job,string language,CancellationToken cancel,IProgress<SpeechProgress> progress=null)
  {
   _settings.Validate();double duration;using(var file=File.OpenRead(wave))duration=new WaveHeader(file).LengthInSeconds;
   if(duration<=0)throw new InvalidDataException("Audio contains no samples.");Directory.CreateDirectory(job);var result=new Subtitle();
   for(double offset=0;offset<duration;offset+=_settings.ChunkSeconds){
    cancel.ThrowIfCancellationRequested();var length=Math.Min(_settings.ChunkSeconds,duration-offset);var file=Path.Combine(job,"api-"+Guid.NewGuid().ToString("N")+".wav");
    try{
     var args="-nostdin -y -ss "+offset.ToString(CultureInfo.InvariantCulture)+" -i "+ClassicSpeechRunner.Quote(wave)+" -t "+length.ToString(CultureInfo.InvariantCulture)+" -vn -ar 16000 -ac 1 -c:a pcm_s16le "+ClassicSpeechRunner.Quote(file);
     await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,args),cancel,null).ConfigureAwait(false);
     if(new FileInfo(file).Length>24*1024*1024)throw new InvalidDataException("Audio chunk exceeds upload limit. Reduce chunk duration.");
     progress?.Report(new SpeechProgress{Message="API: "+((int)(offset/duration*100))+"%",Percent=(int)(offset/duration*100)});
     var json=await SendAsync(file,language,_settings.Stream?"json":"verbose_json",cancel).ConfigureAwait(false);
     var parsed=Parse(json,length,offset,out var estimated);if(estimated)progress?.Report(new SpeechProgress{Message="Server returned text without timestamps; cue times are estimated."});
     result.Paragraphs.AddRange(parsed.Paragraphs);
    }finally{if(File.Exists(file))File.Delete(file);}
   }
   progress?.Report(new SpeechProgress{Message="API: 100%",Percent=100});return result;
  }
  private async Task<string> SendAsync(string file,string language,string format,CancellationToken cancel)
  {
   if(_transcribe!=null)return await _transcribe(file,language,cancel).ConfigureAwait(false);
   using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel)){
    deadline.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
    try{
     using(var body=new MultipartFormDataContent())using(var request=new HttpRequestMessage(HttpMethod.Post,_settings.Endpoint())){
      var audio=new StreamContent(File.OpenRead(file));audio.Headers.ContentType=new MediaTypeHeaderValue("audio/wav");body.Add(audio,"file","audio.wav");body.Add(new StringContent(_settings.Model),"model");body.Add(new StringContent(format),"response_format");
      if(_settings.Stream)body.Add(new StringContent("true"),"stream");
      if(!string.IsNullOrWhiteSpace(language) && language!="auto")body.Add(new StringContent(language),"language");if(!string.IsNullOrWhiteSpace(_settings.Prompt))body.Add(new StringContent(_settings.Prompt),"prompt");
      request.Content=body;var key=_settings.ApiKey;if(!string.IsNullOrEmpty(key))request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
      using(var response=await _client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token).ConfigureAwait(false))
      using(var stopRead=deadline.Token.Register(()=>response.Dispose())){
       if(response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType=="text/event-stream")
        return await ReadEventsAsync(await response.Content.ReadAsStreamAsync().ConfigureAwait(false),deadline.Token).ConfigureAwait(false);
       var text=await response.Content.ReadAsStringAsync().ConfigureAwait(false);deadline.Token.ThrowIfCancellationRequested();
       if(response.IsSuccessStatusCode)return text;
       if((int)response.StatusCode==400 && format=="verbose_json" && text.IndexOf("verbose_json",StringComparison.OrdinalIgnoreCase)>=0)return await SendAsync(file,language,"json",cancel).ConfigureAwait(false);
       throw new InvalidOperationException("Transcription API returned HTTP "+(int)response.StatusCode+". Check server URL, model and authentication.");
      }
     }
    }catch(Exception) when(deadline.IsCancellationRequested){if(cancel.IsCancellationRequested)throw new OperationCanceledException(cancel);throw new TimeoutException("Transcription request timed out.");}
   }
  }
  public static async Task<string> ReadEventsAsync(Stream stream,CancellationToken cancel)
  {
   var text=new StringBuilder();var segments=new JArray();bool complete=false;string final=null;
   using(stream)using(var stop=cancel.Register(()=>stream.Dispose()))using(var reader=new StreamReader(stream,Encoding.UTF8)){
    try{
     var data=new StringBuilder();string line;
     while((line=await reader.ReadLineAsync().ConfigureAwait(false))!=null){
      cancel.ThrowIfCancellationRequested();
      if(line.Length>0){if(line.StartsWith("data:",StringComparison.Ordinal)){if(data.Length>0)data.Append('\n');data.Append(line.Substring(5).TrimStart());}continue;}
      if(data.Length==0)continue;var raw=data.ToString();data.Clear();
      if(raw=="[DONE]"){complete=true;break;}
      var row=JObject.Parse(raw);var type=(string)row["type"]??"";
      if(type.Contains("error") || row["error"]!=null)throw new InvalidDataException("Transcription stream returned an error.");
      if(type=="transcript.text.done"){final=(string)row["text"];complete=true;break;}
      else if(type=="transcript.text.delta")text.Append((string)row["delta"]);
      else if(type=="transcript.text.segment")segments.Add(new JObject{["start"]=row["start"],["end"]=row["end"],["text"]=row["text"]});
      else if(row["choices"]?.First?["delta"]?["content"]!=null)text.Append((string)row["choices"].First["delta"]["content"]);
      if(text.Length>2000000)throw new InvalidDataException("Transcription stream is too large.");
     }
    }catch(Exception) when(cancel.IsCancellationRequested){throw new OperationCanceledException(cancel);}
   }
   if(!complete)throw new InvalidDataException("Transcription stream ended before completion.");
   return new JObject{["text"]=final??text.ToString(),["segments"]=segments}.ToString();
  }
  private static string Stamp(JToken value)=>value is JValue scalar ? Convert.ToString(scalar.Value,CultureInfo.InvariantCulture) : "";
  public static Subtitle Parse(string json,double duration,double offset,out bool estimated)
  {
   estimated=false;var result=new Subtitle();JObject root;try{root=JObject.Parse(json);}catch(JsonException){throw new InvalidDataException("Transcription API returned invalid JSON.");}
   var timed=root["segments"] as JArray;var words=false;if(timed==null || timed.Count==0){timed=root["words"] as JArray;words=true;}
   if(timed!=null && timed.Count>0){
    var text=new StringBuilder();double start=-1,end=0;
    foreach(var row in timed){var content=((string)(row[words?"word":"text"])??"").Trim();if(content.Length==0)continue;
     if(!double.TryParse(Stamp(row["start"]),NumberStyles.Float,CultureInfo.InvariantCulture,out var a)||!double.TryParse(Stamp(row["end"]),NumberStyles.Float,CultureInfo.InvariantCulture,out var b)||double.IsNaN(a)||double.IsInfinity(a)||double.IsNaN(b)||double.IsInfinity(b)||a<0||b<=a)throw new InvalidDataException("Transcription API returned invalid timestamps.");
     if(a>=duration)continue;a=Math.Min(a,duration);b=Math.Min(b,duration);
     if(!words){result.Paragraphs.Add(new Paragraph(content,(offset+a)*1000,(offset+b)*1000));continue;}
     if(start<0)start=a;if(text.Length>0)text.Append(' ');text.Append(content);end=b;
     if(text.Length>=70 || Regex.IsMatch(content,@"[.!?。！？]$")){result.Paragraphs.Add(new Paragraph(text.ToString(),(offset+start)*1000,(offset+end)*1000));text.Clear();start=-1;}
    }
    if(text.Length>0)result.Paragraphs.Add(new Paragraph(text.ToString(),(offset+start)*1000,(offset+end)*1000));return result;
   }
   var raw=((string)root["text"]??"").Trim();if(raw.Length==0)return result;estimated=true;
   var pieces=new List<string>();var line="";foreach(var word in Regex.Split(raw,@"\s+")){if(line.Length>0 && line.Length+word.Length>86){pieces.Add(line);line="";}line+=(line.Length>0?" ":"")+word;if(Regex.IsMatch(word,@"[.!?。！？]$")){pieces.Add(line);line="";}}if(line.Length>0)pieces.Add(line);
   var total=pieces.Sum(s=>s.Length);double at=0;foreach(var piece in pieces){var end=at+duration*piece.Length/total;result.Paragraphs.Add(new Paragraph(piece,(offset+at)*1000,(offset+end)*1000));at=end;}return result;
  }
  public void Dispose()=>_client.Dispose();
 }
}
