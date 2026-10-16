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
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicCompletion
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static void Pump(Task t){while(!t.IsCompleted){Application.DoEvents();Thread.Sleep(5);}t.GetAwaiter().GetResult();}
 sealed class Handler:HttpMessageHandler{public Func<HttpRequestMessage,Task<HttpResponseMessage>> Run;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c){c.ThrowIfCancellationRequested();return Run(r);}protected override void Dispose(bool d){}}
 static HttpResponseMessage Reply(object value)=>new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(value is string?(string)value:Newtonsoft.Json.JsonConvert.SerializeObject(value),Encoding.UTF8,"application/json")};
 [STAThread]static int Main(string[] args){try{
  var root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);Configuration.Settings.General.FFmpegLocation=args[2];Configuration.Settings.General.Language="ru-RU";LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();
  if(args[0]=="native"){Pump(Native());return 0;}Pump(Unit(root));Ui(root);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{ClassicLlamaServer.Stop();}}
 static async Task Unit(string root)
 {
  Check(TranslationEchoGuard.IsUntranslatedEcho("This sentence should be translated.","THIS sentence should be translated.","en","ru"),"Echo guard detects returned source text");
  Check(!TranslationEchoGuard.IsUntranslatedEcho("Hi John.","Hi John.","en","ru") && !TranslationEchoGuard.IsUntranslatedEcho("This is an unchanged sentence.","This is an unchanged sentence.","Spanish","Spanish (Latin America)"),"Echo guard allows short names and same-language variants");
  Check(ClassicLlamaCatalog.TranslateModels.Count>20 && ClassicLlamaCatalog.ReviewModels.Count>5,"Stable translation and review model catalogs ported");
  var local=new ClassicLlamaSettings{Variant="cuda13"};var args=ClassicLlamaServer.Arguments(local,19001);Check(args.Contains("--host 127.0.0.1") && args.Contains("--reasoning off") && !args.Contains("--chat-template"),"Qwen thinking disabled without conflicting chat template override");
  local.Model="translategemma-4b_Q4_K_M.gguf";Check(ClassicLlamaServer.Arguments(local,19001).Contains("--no-jinja --chat-template") && !ClassicLlamaServer.Arguments(local,19001).Contains("--reasoning off"),"TranslateGemma uses its required template");
  var chunks=AiReviewContextGenerator.BuildParts(new[]{"A short sentence.",new string('日',100),"Another sentence."},30);Check(chunks.Count>=2,"Context generation chunks CJK input for token budget");
  var entries=AiReviewContextGenerator.MergeEntries(new[]{"Mike","Invented","mike","Mike"},"Mike said hello. Mike met John.",60);Check(entries.Count==1 && entries[0]=="Mike","Context discards invented names and chooses source spelling");
  var context=await AiReviewContextGenerator.GenerateAsync(new[]{"Mike is a boxer. Mike won."},"English",(system,user,json,token)=>Task.FromResult("{\"names\":[\"Mike\",\"Invented\"],\"terms\":[\"boxer\"],\"summary\":\"Mike won.\"}"),null,CancellationToken.None);Check(context.Contains("Mike") && context.Contains("boxer") && !context.Contains("Invented"),"Generated context filters hallucinated reference entries");
  ClassicAiPrompts.Save("Test","Keep tags and lines.");Check(ClassicAiPrompts.Load()["Test"]=="Keep tags and lines.","Custom AI template persists separately");
  var s=ClassicTranslationSettings.Load("compatible");s.KeepLineBreaks=false;s.Save("compatible");int calls=0;
  var handler=new Handler{Run=async r=>{var body=JObject.Parse(await r.Content.ReadAsStringAsync());calls++;Check(body["messages"]!=null,"Translation carries structured context");return Reply(new{choices=new[]{new{message=new{content=calls==1?"This sentence needs a translation.":"Это предложение нужно перевести."}}}});}};
  using(var t=new ClassicNewTranslator("compatible",handler)){t.Initialize();Check((await t.Translate("This sentence needs a translation.","en","ru",CancellationToken.None)).Contains("предложение") && calls==2,"Echo triggers exactly one warmer retry");}
  s.KeepLineBreaks=true;s.Save("compatible");calls=0;handler=new Handler{Run=r=>Task.FromResult(Reply(new{choices=new[]{new{message=new{content=++calls==1?"Первая. Вторая.":"Первая.\nВторая."}}}}))};
  using(var t=new ClassicNewTranslator("compatible",handler)){t.Initialize();Check((await t.Translate("First.\nSecond.","en","ru",CancellationToken.None)).Contains("\n") && calls==2,"Line-break drift is retried before applying a cue");}
  var merged=ClassicCloudAsr.MergeGoogleTail(JArray.Parse("[{\"word\":\"First\",\"start\":0,\"end\":20}]"),JArray.Parse("[{\"word\":\"duplicate\",\"start\":0,\"end\":1},{\"word\":\"recovered\",\"start\":2,\"end\":3}]"),19);Check(merged.Count==2 && (double)merged[1]["start"]==21,"ASR tail merge removes overlap and restores absolute timestamps");
  await Recovery(root);
 }
 static async Task Recovery(string root)
 {
  var file=Path.Combine(root,"recovery.wav");using(var w=new BinaryWriter(File.Create(file))){w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+120*32000);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(16000);w.Write(32000);w.Write((short)2);w.Write((short)16);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(120*32000);w.Write(new byte[120*32000]);}
  var s=ClassicCloudAsrSettings.Load("google");s.Project="test-project";s.KeyFile=Path.Combine(root,"adc.json");File.WriteAllText(s.KeyFile,"{\"type\":\"authorized_user\",\"client_id\":\"test\",\"client_secret\":\"test\",\"refresh_token\":\"test\"}");int uploads=0,deletes=0,submits=0;
  var handler=new Handler{Run=async r=>{
   var url=r.RequestUri.AbsoluteUri;if(url.Contains("oauth2"))return Reply(new{access_token="test",expires_in=3600});
   if(r.Method==HttpMethod.Delete){deletes++;return Reply("{}");}
   if(url.Contains("uploadType")){uploads++;var bytes=await r.Content.ReadAsByteArrayAsync();Check(bytes.Length>0 && (uploads==1 || bytes.Length<new FileInfo(file).Length),"Recovery uploads bounded cut audio");return Reply("{}");}
   if(url.Contains("batchRecognize")){submits++;return Reply(new{name="projects/test/operations/"+submits});}
   if(url.Contains("operations/")){var tail=submits==2;var word=new JObject{["word"]=tail?"tail":"first",["startOffset"]=tail?"2s":"0s",["endOffset"]=tail?"90s":"20s"};return Reply(new JObject{["done"]=true,["response"]=new JObject{["totalBilledDuration"]=tail?"101s":"120s",["results"]=new JObject{["file"]=new JObject{["inlineResult"]=new JObject{["transcript"]=new JObject{["results"]=new JArray(new JObject{["alternatives"]=new JArray(new JObject{["words"]=new JArray(word)})})}}}}}});}
   return Reply("{}");
  }};
  using(var cloud=new ClassicCloudAsr("google",s,handler)){var result=JObject.Parse(await cloud.TranscribeChunkAsync(file,"en",CancellationToken.None));Check(result["words"].Count()==2 && (double)result["words"][1]["start"]==21 && uploads==2 && deletes==2,"Google recovery runs once and cleans both owned remote objects");}
  Check(!Directory.GetFiles(root,"*-tail.wav").Any(),"Recovery removes its local tail audio");
 }
 static void Ui(string root)
 {
  foreach(var form in new Form[]{new ClassicLlamaForm(),new ClassicAiPromptForm("Test","Keep tags."),new ClassicTranslationForm("compatible")})using(form){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,form.GetType().Name+".png"));}Check(form.Controls.Count>0,"Classic settings opens "+form.GetType().Name);form.Close();}
 }
 static async Task Native()
 {
  var s=new ClassicLlamaSettings{Variant="cuda13"};s.Save();await ClassicLlamaServer.EnsureInstalled(s,CancellationToken.None,new Progress<SpeechProgress>(p=>Console.WriteLine(p.Message)));var url=await ClassicLlamaServer.Start(s,CancellationToken.None);Console.WriteLine(url);
  using(var client=new ClassicAiClient(new ClassicCompatibleAsrSettings{Url=url,Model="classic-local",TimeoutSeconds=900})){
   foreach(var target in new[]{"Russian","Japanese"}){var result=await client.ChatAsync(new JArray(new JObject{["role"]="system",["content"]="Translate into "+target+". Output only the translation."},new JObject{["role"]="user",["content"]="Hello. How are you today?"}),false,CancellationToken.None);Console.WriteLine(target+": "+result);Check(result.Any(c=>target=="Russian"?c>='А'&&c<='я':c>='\u3040'&&c<='\u9fff'),"Managed native translation "+target);}
   var context=await AiReviewContextGenerator.GenerateAsync(new[]{"Mike Tyson is a boxer. Mike Tyson won the fight."},"English",(system,user,json,token)=>client.ChatAsync(new JArray(new JObject{["role"]="system",["content"]=system},new JObject{["role"]="user",["content"]=user}),json,token),null,CancellationToken.None);Console.WriteLine(context);Check(context.Contains("Tyson"),"Native local AI context generation");
   var reply=await client.ChatAsync(new JArray(new JObject{["role"]="system",["content"]="Fix English grammar."+ClassicAiReview.Protocol},new JObject{["role"]="user",["content"]="{\"lines\":[{\"n\":1,\"text\":\"I has a apple.\"}]}"}),true,CancellationToken.None);Console.WriteLine(reply);Check(ClassicAiReview.Parse(reply,new System.Collections.Generic.Dictionary<int,string>{{1,"I has a apple."}}).Count==1,"Native managed AI review returns a valid source-matched correction");
  }
  var translation=ClassicTranslationSettings.Load("llama");translation.UseLocalLlama=true;translation.Save("llama");using(var translator=new ClassicNewTranslator("llama")){translator.Initialize();var result=await translator.Translate("Hello.\nHow are you today?","English","Russian",CancellationToken.None);Console.WriteLine(result);Check(result.Contains("\n") && result.Any(c=>c>='А'&&c<='я'),"Actual managed translator preserves two subtitle lines");}
  Check(url==await ClassicLlamaServer.Start(s,CancellationToken.None),"Repeated model use retains the owned server");ClassicLlamaServer.Stop();Check(!ClassicLlamaServer.IsRunning,"Owned local server stops cleanly");
 }
}
