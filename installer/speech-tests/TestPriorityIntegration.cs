using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Forms.AudioToText;
using Nikse.SubtitleEdit.Forms.Translate;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Ocr;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestPriorityIntegration
{
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS: "+name);}
 static void Pump(Task task){var end=DateTime.UtcNow.AddMinutes(20);while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(5);if(DateTime.UtcNow>end)throw new TimeoutException();}task.GetAwaiter().GetResult();}
 sealed class Handler:HttpMessageHandler
 {
  public Func<HttpRequestMessage,int,Task<HttpResponseMessage>> Run;public int Count;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel)=>Run(request,++Count);
  protected override void Dispose(bool disposing){}
 }
 static HttpResponseMessage Reply(string json,int code=200)=>new HttpResponseMessage((HttpStatusCode)code){Content=new StringContent(json,Encoding.UTF8,"application/json")};
 [STAThread]static int Main(string[] args){try{
  var root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);typeof(Configuration).GetField("DataDirectory",BindingFlags.Static|BindingFlags.Public).SetValue(null,root+Path.DirectorySeparatorChar);
  Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[2];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();
  if(args[0]=="lens-native"){using(var bitmap=new Bitmap(800,160)){using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Black);using(var f=new Font("Arial",36))g.DrawString("Hello from Subtitle Edit",f,Brushes.White,20,35);}using(var lens=new ClassicLensSharp()){var task=lens.Ocr(ClassicBitmapOcr.Flatten(bitmap),832,192,"en",CancellationToken.None);Pump(task);Console.WriteLine(task.Result);Check(task.Result.Contains("Subtitle Edit"),"Google Lens Sharp recognizes synthetic subtitle via live free endpoint");}}return 0;}
  if(args[0]=="ocr-native"){NativeOcr(root);return 0;}
  if(args[0]=="tts-native"){NativeTts(root,args);return 0;}
  if(args[0]=="madlad-native"){NativeMadlad();return 0;}
  Pump(Unit(root));var uiSubtitle=new Subtitle();uiSubtitle.Paragraphs.Add(new Paragraph("hello teh world",1000,3000));uiSubtitle.Paragraphs.Add(new Paragraph("Context only",4000,5000));Ui(root,uiSubtitle);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static async Task Unit(string root)
 {
  await LensTests();
  var wave=Path.Combine(root,"tiny.wav");File.WriteAllBytes(wave,new byte[64]);
  var open=ClassicCloudAsrSettings.Load("openrouter");open.Api.ApiKey="test-secret";int openCalls=0;
  var handler=new Handler{Run=async(r,n)=>{var json=JObject.Parse(await r.Content.ReadAsStringAsync());Check(json["input_audio"]?["format"].ToString()=="wav" && json["input_audio"]?["data"].ToString().Length>0,"OpenRouter sends JSON base64 WAV");openCalls++;if(n==1)return Reply("unsupported verbose format",400);Check((string)json["response_format"]=="json" && json["timestamp_granularities"]==null,"OpenRouter retries incompatible verbose format");return Reply("{\"text\":\"Hello.\"}");}};
  using(var cloud=new ClassicCloudAsr("openrouter",open,handler)){var result=await cloud.TranscribeChunkAsync(wave,"en",CancellationToken.None);Check((string)JObject.Parse(result)["text"]=="Hello." && openCalls==2,"OpenRouter complete fallback workflow");}
  var dash=ClassicCloudAsrSettings.Load("dashscope");dash.Api.ApiKey="dash-secret";
  handler=new Handler{Run=async(r,n)=>{
   if(n==1)return Reply("{\"data\":{\"upload_dir\":\"temp\",\"upload_host\":\"https://storage.test/upload\",\"oss_access_key_id\":\"access\",\"policy\":\"policy\",\"signature\":\"signature\",\"x_oss_object_acl\":\"private\",\"x_oss_forbid_overwrite\":\"true\"}}");
   if(n==2){Check(r.Headers.Authorization==null && r.Content is MultipartFormDataContent,"DashScope signed storage upload does not leak bearer key");return Reply("");}
   if(n==3){var json=JObject.Parse(await r.Content.ReadAsStringAsync());Check(r.Headers.Contains("X-DashScope-Async") && (string)json["parameters"]?["language"]=="ja" && ((string)json["input"]?["file_url"]).StartsWith("oss://"),"DashScope submits asynchronous transcription");return Reply("{\"output\":{\"task_id\":\"job\"}}");}
   if(n==4)return Reply("{\"output\":{\"task_status\":\"SUCCEEDED\",\"results\":[{\"transcription_url\":\"https://storage.test/result\"}]}}");
   Check(r.Headers.Authorization==null,"DashScope result URL receives no API key");return Reply("{\"transcripts\":[{\"sentences\":[{\"begin_time\":150,\"end_time\":1350,\"text\":\"こんにちは。\"}]}]}");
  }};
  using(var cloud=new ClassicCloudAsr("dashscope",dash,handler)){var result=JObject.Parse(await cloud.TranscribeChunkAsync(wave,"ja",CancellationToken.None));Check((double)result["segments"][0]["start"]==.15 && (double)result["segments"][0]["end"]==1.35,"DashScope millisecond timestamps map to seconds");}
  var google=ClassicCloudAsrSettings.Load("google");google.KeyFile=Path.Combine(root,"adc-test.json");File.WriteAllText(google.KeyFile,"{\"type\":\"authorized_user\",\"client_id\":\"test\",\"client_secret\":\"test\",\"refresh_token\":\"test\",\"quota_project_id\":\"test-project\"}");
  handler=new Handler{Run=async(r,n)=>{
   var url=r.RequestUri.AbsoluteUri;
   if(n==1)return Reply("{\"access_token\":\"oauth-token\",\"expires_in\":3600}");
   if(n==2)return Reply("{}");
   if(n==3){Check(url.Contains("uploadType=media"),"Google uploads owned temporary audio");return Reply("{}");}
   if(n==4){var body=JObject.Parse(await r.Content.ReadAsStringAsync());Check(body["config"]?["languageCodes"]?[0].ToString()=="en-US" && (string)body["processingStrategy"]=="DYNAMIC_BATCHING","Google v2 recognition config and dynamic batching");return Reply("{\"name\":\"projects/test/locations/us/operations/job\"}");}
   if(n==5)return Reply("{\"done\":true,\"response\":{\"results\":{\"file\":{\"inlineResult\":{\"transcript\":{\"results\":[{\"alternatives\":[{\"transcript\":\"Hello world.\",\"words\":[{\"word\":\"Hello\",\"endOffset\":\"0.5s\"},{\"word\":\"world.\",\"startOffset\":\"0.6s\",\"endOffset\":\"1.2s\"}]}]}]}}}}}}");
   Check(r.Method==HttpMethod.Delete && url.Contains("subtitle-edit%2F"),"Google removes only the uploaded object");return Reply("");
  }};
  using(var cloud=new ClassicCloudAsr("google",google,handler)){var json=JObject.Parse(await cloud.TranscribeChunkAsync(wave,"en",CancellationToken.None));Check((double)json["words"][0]["start"]==0 && json["words"].Count()==2,"Google retains first word with absent zero offset");}
  handler=new Handler{Run=(r,n)=>Task.FromResult(n==5?Reply("{\"done\":true,\"error\":{\"message\":\"failure\"}}"):n==1?Reply("{\"access_token\":\"oauth-token\"}"):n==4?Reply("{\"name\":\"projects/test/operations/fail\"}"):Reply("{}"))};
  using(var cloud=new ClassicCloudAsr("google",google,handler)){try{await cloud.TranscribeChunkAsync(wave,"en",CancellationToken.None);throw new Exception("Expected Google error");}catch(InvalidOperationException){Check(handler.Count==6,"Google cleans uploaded object after recognition failure");}}
  var sse="data: {\"type\":\"transcript.text.delta\",\"delta\":\"Привет\"}\n\ndata: {\"type\":\"transcript.text.done\",\"text\":\"Привет!\"}\n\n";
  var stream=JObject.Parse(await ClassicCompatibleAsr.ReadEventsAsync(new MemoryStream(Encoding.UTF8.GetBytes(sse)),CancellationToken.None));Check((string)stream["text"]=="Привет!","SSE combines deltas and accepts final Unicode transcript");
  try{await ClassicCompatibleAsr.ReadEventsAsync(new MemoryStream(Encoding.UTF8.GetBytes("data: {\"type\":\"transcript.text.delta\",\"delta\":\"partial\"}\n\n")),CancellationToken.None);throw new Exception("Incomplete stream accepted");}catch(InvalidDataException){Check(true,"Incomplete SSE cannot become a successful transcript");}
  var subtitle=new Subtitle();subtitle.Paragraphs.Add(new Paragraph("hello teh world",1000,3000){Actor="A",Extra="Style"});subtitle.Paragraphs.Add(new Paragraph("Context only",4000,5000));
  var reply="{\"changes\":[{\"n\":2,\"orig\":\"hello teh world\",\"text\":\"Hello the world.\",\"reason\":\"Spelling\"},{\"n\":2,\"orig\":\"Context only\",\"text\":\"Unwanted change\"}]}";
  var changes=ClassicAiReview.Parse(reply,new System.Collections.Generic.Dictionary<int,string>{{1,"hello teh world"}});
  Check(changes.Count==1 && changes[0].Number==1,"AI review remaps unique echoed original and rejects context edits");
  Check(ClassicAiReview.Parse("{\"changes\":[{\"n\":1,\"orig\":\"<i>Hi</i>\",\"text\":\"Hi!\"}]}",new System.Collections.Generic.Dictionary<int,string>{{1,"<i>Hi</i>"}}).Count==0,"AI review rejects removed formatting tags");
  Check((int)ClassicAiReview.Request(subtitle,new[]{0})["context_after"][0]["n"]==2,"AI review separates editable lines from actor-aware context");
  var ai=new ClassicCompatibleAsrSettings{Url="http://localhost:1234/v1",Model="test",ApiKey="ai-secret"};
  handler=new Handler{Run=(r,n)=>Task.FromResult(n==1?Reply("temperature unsupported",400):n==2?Reply("response_format unsupported",400):Reply("{\"choices\":[{\"message\":{\"content\":\"corrected\"}}]}"))};
  using(var client=new ClassicAiClient(ai,handler)){Check(await client.ChatAsync(new JArray(new JObject{["role"]="user",["content"]="Hi"}),true,CancellationToken.None)=="corrected" && handler.Count==3,"AI retries unsupported temperature and JSON format without paid calls");}
  foreach(var id in new[]{"compatible","llama","nvidia","route","perplexity","lara"}){
   var settings=ClassicTranslationSettings.Load(id);settings.Api.ApiKey="translator-secret";settings.AccessId="access";settings.Save(id);
   handler=new Handler{Run=async(r,n)=>{
    var body=await r.Content.ReadAsStringAsync();var json=JObject.Parse(body);
    if(id=="lara"){Check(r.Headers.GetValues("Authorization").Single().StartsWith("Lara access:"),"Lara signs request with HMAC");return Reply("{\"content\":{\"translation\":\"Привет!\"}}");}
    if(id=="perplexity"){Check(json["input"]!=null && json["instructions"]!=null,"Perplexity uses Responses protocol");return Reply("{\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"Привет!\"}]}]}");}
    Check(json["messages"]!=null,"Compatible translator request "+id);return Reply("{\"choices\":[{\"message\":{\"content\":\"Привет!\"}}]}");
   }};
   using(var translator=new ClassicNewTranslator(id,handler)){translator.Initialize();Check(await translator.Translate("Hello!","en","ru",CancellationToken.None)=="Привет!","Translation response "+id);}
   Check(!File.ReadAllText(ClassicTranslationSettings.FileName(id)).Contains("translator-secret"),"Translation key encrypted "+id);
  }
  Check(ClassicMadladLanguages.List().Count==419 && ClassicMadladLanguages.IsSupported("ja") && !ClassicMadladLanguages.IsSupported("yue"),"MADLAD uses exact stable language-token catalog");
  var voice="voice_"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(ClassicQwenReference.Folder);File.WriteAllBytes(Path.Combine(ClassicQwenReference.Folder,voice+".wav"),new byte[64]);File.WriteAllText(Path.Combine(ClassicQwenReference.Folder,voice+".txt"),"");var meta=Path.Combine(ClassicQwenReference.Folder,voice+".json");File.WriteAllText(meta,JsonConvert.SerializeObject(new ClassicQwenReference{Id=voice,Name="Per line",Consent=true,PerLine=true}));
  Check(ClassicExtendedTts.UsesRequestVoice("pocket",voice) && ClassicExtendedTts.UsesRequestVoice("vibe",voice) && ClassicExtendedTts.UsesRequestVoice("moss",voice),"Pocket Vibe and per-line MOSS change reference per request");
  foreach(var id in new[]{"pocket","vibe","moss"}){var definition=ClassicLocalTts.Catalog.Single(d=>d.Id==id);var settings=ClassicLocalTtsSettings.Load(id);Check((string)ClassicExtendedTts.Payload(definition,settings,"Hi",voice,true)["voice"]==voice,"Per-request voice payload "+id);}
  File.WriteAllText(meta,JsonConvert.SerializeObject(new ClassicQwenReference{Id=voice,Name="Regular",Consent=true}));
  Check(!ClassicExtendedTts.UsesRequestVoice("moss",voice) && !ClassicExtendedTts.Payload(ClassicLocalTts.Catalog.Single(d=>d.Id=="moss"),ClassicLocalTtsSettings.Load("moss"),"Hi",voice,true).ContainsKey("voice"),"Ordinary MOSS voices retain startup conditioning");
  using(var bitmap=new Bitmap(250,80)){using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);g.DrawString("Hello",SystemFonts.DefaultFont,Brushes.White,15,15);}
   var settings=ClassicBitmapSettings.Load("mistral");settings.Api.ApiKey="ocr-secret";
   handler=new Handler{Run=async(r,n)=>{var body=JObject.Parse(await r.Content.ReadAsStringAsync());Check((string)body["document"]?["type"]=="image_url" && ((string)body["document"]?["image_url"]).StartsWith("data:image/png;base64,"),"Mistral OCR sends PNG data URI");return Reply("{\"pages\":[{\"markdown\":\"  Hello!  \"}]}");}};
   using(var ocr=new ClassicBitmapOcr("mistral",settings,handler))Check(await ocr.Ocr(bitmap,"mistral-ocr-latest","English",CancellationToken.None)=="Hello!","Mistral OCR parses page text");
   using(var png=new Bitmap(new MemoryStream(ClassicBitmapOcr.Flatten(bitmap))))Check(png.GetPixel(0,0).A==255 && png.GetPixel(0,0).R==0 && png.Width==282,"Transparent OCR bitmap flattened with black margin");
  }

 }
 static byte[] Proto(params byte[][] fields)=>fields.SelectMany(x=>x).ToArray();
 static byte[] Bytes(int field,byte[] value){using(var s=new MemoryStream()){s.WriteByte((byte)(field*8+2));var n=value.Length;while(n>=128){s.WriteByte((byte)(n|128));n>>=7;}s.WriteByte((byte)n);s.Write(value,0,value.Length);return s.ToArray();}}
 static byte[] Float(int field,float value)=>Proto(new[]{(byte)(field*8+5)},BitConverter.GetBytes(value));
 static byte[] LensLine(string text,float x,float y)=>Bytes(2,Proto(Bytes(1,Bytes(2,Encoding.UTF8.GetBytes(text))),Bytes(2,Bytes(1,Proto(Float(1,x),Float(2,y),Float(3,.2f),Float(4,.1f))))));
 static async Task LensTests()
 {
  var fixture=Bytes(2,Bytes(3,Bytes(1,Bytes(1,Proto(LensLine("right",.7f,.2f),LensLine("bottom",.3f,.7f),LensLine("Top",.3f,.2f))))));
  Check(ClassicLensSharp.ReadText(fixture)=="Top right"+Environment.NewLine+"bottom","Lens restores interleaved subtitle chunks in visual reading order");
  try{ClassicLensSharp.ReadText(new byte[]{18,255,255});throw new Exception("Truncated protobuf accepted");}catch(IOException){Check(true,"Lens rejects truncated response lengths");}
  var h=new Handler{Run=async(r,n)=>{var payload=await r.Content.ReadAsByteArrayAsync();Check(r.Content.Headers.ContentType.MediaType=="application/x-protobuf" && payload[0]==10 && r.Headers.Authorization==null,"Lens uses stable protobuf protocol without user credentials");return n==1?Reply("retry",503):new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(fixture)};}};
  using(var lens=new ClassicLensSharp(h))Check((await lens.Ocr(new byte[]{1,2},100,50,"ja",CancellationToken.None)).StartsWith("Top right") && h.Count==2,"Lens retries transient failures and parses response");
 }
 static void Ui(string root,Subtitle subtitle)
 {
  subtitle.Paragraphs[0].Actor="Speaker A";subtitle.Paragraphs[0].Extra="Style A";
  using(var form=new ClassicAiReviewForm(subtitle,new[]{0,1})){
   form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
   var grid=Desc(form).OfType<DataGridView>().Single();
   var first=new ClassicAiChange{Number=1,Original=subtitle.Paragraphs[0].Text,Text="Fixed first"};var second=new ClassicAiChange{Number=2,Original=subtitle.Paragraphs[1].Text,Text="Fixed second"};
   grid.Rows[grid.Rows.Add(true,1,first.Original,first.Text,"")].Tag=first;
   grid.Rows[grid.Rows.Add(false,2,second.Original,second.Text,"")].Tag=second;
   grid.Sort(grid.Columns[1],System.ComponentModel.ListSortDirection.Descending);
   var apply=Desc(form).OfType<Button>().Single(b=>b.Name=="aiReviewApply");apply.Enabled=true;apply.PerformClick();
   Check(form.EditedSubtitle.Paragraphs[0].Text==first.Text && form.EditedSubtitle.Paragraphs[1].Text==second.Original,"AI apply follows checked suggestions after grid sorting");
   Check(form.EditedSubtitle.Paragraphs[0].StartTime.TotalMilliseconds==1000 && form.EditedSubtitle.Paragraphs[0].EndTime.TotalMilliseconds==3000 && form.EditedSubtitle.Paragraphs[0].Actor=="Speaker A" && form.EditedSubtitle.Paragraphs[0].Extra=="Style A","AI apply preserves timing actors styles and source subtitle");
   Check(subtitle.Paragraphs[0].Text==first.Original,"AI review never mutates source before confirmation");
  }
  foreach(Form form in new Form[]{new ClassicCloudAsrForm("google"),new ClassicCloudAsrForm("dashscope"),new ClassicCompatibleAsrForm(),new ClassicAiReviewForm(subtitle,new[]{0}),new ClassicTranslationForm("madlad"),new Nikse.SubtitleEdit.Forms.Ocr.ClassicBitmapForm("crispembed")}){
   using(form){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
    using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,form.GetType().Name+"-"+Guid.NewGuid().ToString("N")+".png"));}
    foreach(var button in Desc(form).OfType<Button>().Where(b=>b.Visible)){var point=form.PointToClient(button.PointToScreen(Point.Empty));Check(point.Y>=0 && point.Y+button.Height<=form.ClientSize.Height,"Settings action button fits "+form.GetType().Name);}
    form.Close();
   }
  }
  using(var form=new AutoTranslate(subtitle,new Subtitle(),"",Encoding.UTF8)){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();var engines=(System.Collections.IEnumerable)form.GetType().GetField("_autoTranslatorEngines",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);Check(engines.Cast<object>().OfType<ClassicNewTranslator>().Count()==ClassicNewTranslator.Ids.Length,"All modern translation adapters appear in classic window");
   var picker=Desc(form).OfType<Nikse.SubtitleEdit.Controls.NikseComboBox>().Single(c=>c.Name=="nikseComboBoxEngine");picker.SelectedItem=engines.Cast<object>().OfType<ClassicNewTranslator>().Single(e=>e.Id=="madlad");Application.DoEvents();
   Check(!ClassicNewTranslator.MadladModelInstalled,"Missing MADLAD model is not treated as installed");bool opened=false;
   using(var timer=new System.Windows.Forms.Timer{Interval=20}){timer.Tick+=(s,e)=>{var settings=Application.OpenForms.OfType<ClassicTranslationForm>().FirstOrDefault();if(settings==null)return;opened=true;timer.Stop();Check(Desc(settings).OfType<TextBox>().Any(t=>t.Name=="installed" && t.Text.Contains("2 ГБ")),"Missing-model settings explain download size and action");settings.DialogResult=DialogResult.Cancel;};timer.Start();form.GetType().GetMethod("buttonTranslate_Click",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,new object[]{null,EventArgs.Empty});}
   Check(opened && !(bool)form.GetType().GetField("_translationInProgress",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form),"Translate opens MADLAD installation settings before starting and cancelling keeps idle state");
   Check(form.TranslatedSubtitle.Paragraphs.All(p=>string.IsNullOrEmpty(p.Text)),"Cancelling missing-model setup preserves untranslated cues");form.Close();}
  Configuration.Settings.VobSubOcr.LastOcrMethod="Classic:mistral";
  using(var ocr=new Nikse.SubtitleEdit.Forms.Ocr.VobSubOcr()){var methods=Desc(ocr).OfType<Nikse.SubtitleEdit.Controls.NikseComboBox>().Single(c=>c.Name=="comboBoxOcrMethod");Check(methods.Items.Contains("Mistral OCR") && methods.Items.Contains("CrispEmbed (local GGUF)") && methods.Items.Contains("llama.cpp / compatible Vision") && methods.Items.Contains("Google Lens Sharp"),"Bitmap OCR engines integrated in classic import window");}
  var options=new ClassicSpeechOptions{Engine=ClassicSpeechCatalog.CloudNames[0]};options.Save();using(var parent=new Form())using(var form=new WhisperAudioToText("",new Subtitle(),0,parent,null)){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();var combo=Desc(form).OfType<Nikse.SubtitleEdit.Controls.NikseComboBox>().Single(c=>c.Name=="comboBoxWhisperEngine");Check(ClassicSpeechCatalog.CloudNames.All(n=>combo.Items.Contains(n)),"Cloud ASR engines integrated in normal selector");form.Close();}
 }
 static System.Collections.Generic.IEnumerable<Control> Desc(Control root){foreach(Control c in root.Controls){yield return c;foreach(var child in Desc(c))yield return child;}}
 static void NativeMadlad()
 {
  Pump(ClassicNewTranslator.EnsureMadladAsync("cuda13",CancellationToken.None,new Progress<SpeechProgress>(p=>Console.WriteLine(p.Message))));
  var settings=ClassicTranslationSettings.Load("madlad");settings.Variant="cuda13";settings.Save("madlad");
  using(var translator=new ClassicNewTranslator("madlad")){translator.Initialize();foreach(var language in new[]{"ru","ja"}){var task=translator.Translate("Hello. How are you today?","en",language,CancellationToken.None);Pump(task);Console.WriteLine(language+": "+task.Result);Check(task.Result.Any(c=>language=="ru"?c>='А'&&c<='я':c>='\u3040'&&c<='\u9fff'),"Native MADLAD translation to "+language);}}
 }
 static void NativeTts(string root,string[] args)
 {
  var references=new System.Collections.Generic.List<string>();
  foreach(var input in new[]{args[3],args[4]}){var task=ClassicQwenReference.ImportAsync(input,"test clone","",0,3,true,CancellationToken.None,false);Pump(task);var voice=task.Result;voice.PerLine=true;File.WriteAllText(Path.Combine(ClassicQwenReference.Folder,voice.Id+".json"),JsonConvert.SerializeObject(voice));references.Add(voice.Id);}
  foreach(var id in new[]{"pocket","vox","cosy"}){
   var definition=ClassicLocalTts.Catalog.Single(d=>d.Id==id);var settings=ClassicLocalTtsSettings.Load(id);settings.Variant="cuda13";settings.Language="en";
   settings.Model=definition.Models.First(m=>m.Files.All(f=>File.Exists(Path.Combine(ClassicLocalTtsSettings.Root(id),"models",f.File)))).Key;
   using(var engine=new ClassicLocalTts(id,settings)){
    int pid=0;for(int i=0;i<2;i++){var output=Path.Combine(root,id+"-"+i+".wav");Pump(engine.SpeakAsync(i==0?"Hello. This is the first cloned voice.":"This is the second cloned voice.",references[i],output,CancellationToken.None,new Progress<SpeechProgress>(p=>Console.WriteLine(p.Message))));Check(Nikse.SubtitleEdit.Logic.ClassicCast.ClassicVoiceAssets.WaveSeconds(output)>1,"Native audio-only clone "+id+" reference "+i);var process=(System.Diagnostics.Process)typeof(ClassicLocalTts).GetField("_server",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(engine);if(i==0)pid=process.Id;else Check(pid==process.Id,"Reference switch reuses model server "+id);}
   }
  }
 }
 static void NativeOcr(string root)
 {
  var model="PP-OCRv6_medium_rec-f16.gguf";Pump(ClassicBitmapOcr.EnsureAsync(model,CancellationToken.None,new Progress<SpeechProgress>(p=>Console.WriteLine(p.Message))));
  using(var bitmap=new Bitmap(1000,160)){using(var g=Graphics.FromImage(bitmap))using(var font=new Font("Segoe UI",42)){g.Clear(Color.Black);g.DrawString("Hello from Subtitle Edit",font,Brushes.White,25,35);}bitmap.Save(Path.Combine(root,"ocr-source.png"));using(var ocr=new ClassicBitmapOcr("crispembed")){var task=ocr.Ocr(bitmap,model,"English",CancellationToken.None);Pump(task);Console.WriteLine(task.Result);Check(task.Result.IndexOf("Subtitle Edit",StringComparison.OrdinalIgnoreCase)>=0,"Native CrispEmbed subtitle bitmap recognition");}}
 }
}
