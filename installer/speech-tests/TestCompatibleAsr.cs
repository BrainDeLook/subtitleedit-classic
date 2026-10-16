using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms.AudioToText;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestCompatibleAsr
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static void Pump(Task task){var end=DateTime.UtcNow.AddSeconds(40);while(!task.IsCompleted){if(DateTime.UtcNow>end)throw new TimeoutException();Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();}
 sealed class Handler:HttpMessageHandler{
  public int Calls;public bool Fallback,Fail;public string LastBody;public string Key;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel){cancel.ThrowIfCancellationRequested();Calls++;LastBody=await request.Content.ReadAsStringAsync();Check(request.RequestUri.AbsolutePath=="/v1/audio/transcriptions","request uses configured transcription endpoint");Key=request.Headers.Authorization?.Parameter;if(Fail)return new HttpResponseMessage(HttpStatusCode.Unauthorized){Content=new StringContent("private-provider-body-secret")};if(Fallback && Calls==1)return new HttpResponseMessage(HttpStatusCode.BadRequest){Content=new StringContent("response_format verbose_json is unsupported")};return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Fallback?"{\"text\":\"Hello world. Another sentence.\"}":"{\"segments\":[{\"start\":0.1,\"end\":0.8,\"text\":\"Recognized words\"}]}")};}
 }
 [STAThread]static int Main(string[] args){try{
  var root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[1];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.EnableVisualStyles();
  var settings=new ClassicCompatibleAsrSettings{Url="http://127.0.0.1:8000/v1",Model="local-asr",ChunkSeconds=5,Prompt="Names: Майк Тайсон"};settings.ApiKey="unit-secret-key";settings.Save();Check(!File.ReadAllText(ClassicCompatibleAsrSettings.FileName).Contains("unit-secret-key") && ClassicCompatibleAsrSettings.Load().ApiKey=="unit-secret-key","API key is encrypted for current Windows user");
  Check(new ClassicCompatibleAsrSettings{Url="http://localhost:8000/v1/audio/transcriptions"}.Endpoint().AbsolutePath=="/v1/audio/transcriptions","full transcription endpoint is not appended twice");
  var wave=Path.Combine(root,"source.wav");WriteWave(wave);var handler=new Handler();using(var api=new ClassicCompatibleAsr(settings,handler)){var task=api.TranscribeAsync(wave,Path.Combine(root,"chunks"),"ru",CancellationToken.None);Pump(task);Check(handler.Calls==3 && task.Result.Paragraphs.Count==3 && task.Result.Paragraphs[1].StartTime.TotalMilliseconds==5100 && task.Result.Paragraphs[2].EndTime.TotalMilliseconds==10800,"long audio chunks retain timestamps on the complete timeline");Check(handler.Key=="unit-secret-key" && handler.LastBody.Contains("local-asr") && handler.LastBody.Contains("ru") && handler.LastBody.Contains("Майк Тайсон"),"multipart request carries model language prompt and optional bearer key");Check(!Directory.GetFiles(Path.Combine(root,"chunks")).Any(),"uploaded temporary clips are cleaned");}
  var fallback=new Handler{Fallback=true};settings.ChunkSeconds=30;settings.ApiKey="";using(var api=new ClassicCompatibleAsr(settings,fallback)){var task=api.TranscribeAsync(wave,Path.Combine(root,"fallback"),"auto",CancellationToken.None);Pump(task);Check(fallback.Calls==2 && fallback.Key==null && task.Result.Paragraphs.Count==2 && task.Result.Paragraphs.Last().EndTime.TotalMilliseconds==12000,"vLLM verbose_json rejection retries plain JSON with estimated bounded timing and no key");}
  var errorHandler=new Handler{Fail=true};using(var api=new ClassicCompatibleAsr(settings,errorHandler)){try{Pump(api.TranscribeAsync(wave,Path.Combine(root,"error"),"en",CancellationToken.None));throw new Exception("HTTP error ignored");}catch(InvalidOperationException e){Check(e.Message.Contains("401") && !e.Message.Contains("private-provider-body-secret"),"API errors omit provider body and credentials");}}
  using(var api=new ClassicCompatibleAsr(settings,new Handler()))using(var stop=new CancellationTokenSource()){stop.Cancel();try{Pump(api.TranscribeAsync(wave,Path.Combine(root,"cancel"),"en",stop.Token));throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(true,"cancelled ASR starts no upload");}}
  bool estimated;var words=ClassicCompatibleAsr.Parse("{\"words\":[{\"word\":\"Hello\",\"start\":1,\"end\":1.3},{\"word\":\"world.\",\"start\":1.4,\"end\":2}]}",5,10,out estimated);Check(!estimated && words.Paragraphs.Single().Text=="Hello world." && words.Paragraphs[0].StartTime.TotalMilliseconds==11000,"word-only timestamps group into subtitle cues with offset");
  Loopback(settings,wave,root);
  using(var form=new ClassicCompatibleAsrForm()){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();foreach(var size in new[]{form.Size,form.MinimumSize}){form.Size=size;Application.DoEvents();Check(form.Controls[0].Controls.Cast<Control>().All(c=>c.Right<=form.Controls[0].ClientSize.Width && c.Bottom<=form.Controls[0].ClientSize.Height),"classic API settings fit minimum size");}using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"api-settings.png"));}form.Close();}
  var options=new ClassicSpeechOptions{Engine=ClassicSpeechCatalog.CompatibleApi,Language="en"};options.Save();using(var parent=new Form())using(var form=new WhisperAudioToText(wave,new Subtitle(),0,parent,null)){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();var flags=BindingFlags.Instance|BindingFlags.NonPublic;Check((bool)form.GetType().GetProperty("IsModernSpeech",flags).GetValue(form) && ((SpeechEngine)form.GetType().GetProperty("ModernEngine",flags).GetValue(form)).Family=="compatible-api","normal ASR window restores compatible API engine");var ensure=(Task)form.GetType().GetMethod("EnsureModernDependencies",flags).Invoke(form,new object[]{ClassicSpeechCatalog.Get(ClassicSpeechCatalog.CompatibleApi,""),new SpeechModel{Name="local-asr"},CancellationToken.None,null});Pump(ensure);Check(!Directory.Exists(Path.Combine(ClassicSpeechCatalog.Root,"Engines")),"API ASR requires no local model or runtime download");form.Close();}
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static void Loopback(ClassicCompatibleAsrSettings settings,string wave,string root){
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;
  var server=Task.Run(async()=>{using(var client=await listener.AcceptTcpClientAsync())using(var stream=client.GetStream()){
   var header=new System.Collections.Generic.List<byte>();var one=new byte[1];while(header.Count<65536){if(await stream.ReadAsync(one,0,1)==0)throw new IOException("Incomplete HTTP headers");header.Add(one[0]);if(header.Count>=4 && header[header.Count-4]==13 && header[header.Count-3]==10 && header[header.Count-2]==13 && header[header.Count-1]==10)break;}
   var text=Encoding.ASCII.GetString(header.ToArray());var length=int.Parse(text.Split(new[]{"\r\n"},StringSplitOptions.RemoveEmptyEntries).Single(l=>l.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim());var buffer=new byte[8192];while(length>0){var n=await stream.ReadAsync(buffer,0,Math.Min(buffer.Length,length));if(n==0)throw new IOException("Incomplete multipart body");length-=n;}
   var body=Encoding.UTF8.GetBytes("{\"segments\":[{\"start\":0.5,\"end\":1.5,\"text\":\"Local HTTP recognition\"}]}");var response=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\n\r\n");await stream.WriteAsync(response,0,response.Length);await stream.WriteAsync(body,0,body.Length);return text;
  }});
  try{settings.Url="http://127.0.0.1:"+port+"/v1";settings.ApiKey="";settings.Save();var engine=ClassicSpeechCatalog.Get(ClassicSpeechCatalog.CompatibleApi,"");var run=ClassicSpeechRunner.TranscribeAsync(engine,engine.Models[0],new ClassicSpeechOptions{Language="en"},wave,Path.Combine(root,"http-job"),null,CancellationToken.None,null);Pump(run);Pump(server);Check(run.Result.Paragraphs.Single().Text=="Local HTTP recognition" && server.Result.StartsWith("POST /v1/audio/transcriptions"),"normal ASR runner uploads real multipart HTTP to local server and parses response");}
  finally{listener.Stop();}
 }
 static void WriteWave(string file){using(var w=new BinaryWriter(File.Create(file))){var n=16000*12;w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+n*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(16000);w.Write(32000);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(n*2);w.Write(new byte[n*2]);}}
}
