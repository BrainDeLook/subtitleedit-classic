using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Core.TextToSpeech;
using Nikse.SubtitleEdit.Core.Translate;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicCast;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestTtsModelExtras
{
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 sealed class Handler:HttpMessageHandler {
  public HttpStatusCode Status=HttpStatusCode.OK;public bool Wait;public JObject Body;public string Path,Key;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel){Path=request.RequestUri.AbsolutePath;Key=request.Headers.GetValues("xi-api-key").Single();Body=JObject.Parse(await request.Content.ReadAsStringAsync());if(Wait)await Task.Delay(10000,cancel);return new HttpResponseMessage(Status){Content=new ByteArrayContent(new byte[]{1,2,3})};}
 }
 [STAThread] static int Main(string[] args){try{
  var data=System.IO.Path.GetFullPath(args[1]);Directory.CreateDirectory(data);Directory.CreateDirectory(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));typeof(Configuration).GetField("DataDirectory",BindingFlags.Static|BindingFlags.Public).SetValue(null,data+System.IO.Path.DirectorySeparatorChar);Configuration.Settings.General.FFmpegLocation=args[2];LanguageSettings.Current=Language.Load(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.EnableVisualStyles();
  if(args[0]=="unit")Unit(data);else Native(data,args[3]);return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
 static void Unit(string data){
  var text="One <break time='500ms'/> two <break time=\"1.5s\"/> three <break time='2s'/>.";
  foreach(var model in ClassicElevenLabs.Models){var body=ClassicElevenLabs.BuildBody(text,"voice-id",model,"ru",.35,.67,1.12);var v3=model=="eleven_v3";var sent=(string)(v3?body["inputs"][0]["text"]:body["text"]);Check(!body.ToString().Contains("apply_text_normalization"),"normalization retains API default for "+model);if(model.StartsWith("eleven_v"))Check(sent.Contains("[short pause]") && sent.Contains("[pause]") && sent.Contains("[long pause]") && !sent.Contains("<break"),"expressive pause conversion for "+model);else Check((bool)body["enable_ssml_parsing"],"SSML parsing enabled only for explicit breaks on "+model);if(v3)Check(body["inputs"][0]["language_code"]==null && (double)body["settings"]["stability"]==.35 && body["settings"]["speed"]==null && (string)body["language_code"]=="ru","v3 places stability/language at top level and omits unsupported speed");}
  Check(ClassicElevenLabs.BuildBody("Plain <i>text</i>","id","eleven_multilingual_v2","",.5,.5,1)["enable_ssml_parsing"]==null,"ordinary markup does not activate SSML");
  Check(ClassicElevenLabs.Languages("eleven_v4").Length==91 && ClassicElevenLabs.Languages("eleven_v3").Length==74 && ClassicElevenLabs.Languages("eleven_turbo_v2_5").Any(p=>p[1]=="ko"),"stable language catalogs and ISO codes preserved");
  using(var handler=new Handler())using(var client=new System.Net.Http.HttpClient(handler)){
   var bytes=ClassicElevenLabs.SpeakAsync(client,"Привет","voice-id","eleven_v3","ru","TEST_KEY",.35,.67,1.12,CancellationToken.None).GetAwaiter().GetResult();Check(bytes.Length==3 && handler.Path=="/v1/text-to-dialogue" && handler.Key=="TEST_KEY","v3 HTTP protocol checked with local handler, without paid requests");
   ClassicElevenLabs.SpeakAsync(client,"Hello","voice-id","eleven_v4_turbo","en","TEST_KEY",.35,.67,1.12,CancellationToken.None).GetAwaiter().GetResult();Check(handler.Path=="/v1/text-to-speech/voice-id" && (double)handler.Body["voice_settings"]["speed"]==1.12,"v4 uses speech endpoint and precise voice settings");
   handler.Status=HttpStatusCode.Unauthorized;try{ClassicElevenLabs.SpeakAsync(client,"SECRET_TEXT","id","eleven_v4","","SECRET_KEY",.5,.5,1,CancellationToken.None).GetAwaiter().GetResult();throw new Exception("HTTP failure ignored");}catch(InvalidOperationException ex){Check(!ex.Message.Contains("SECRET"),"HTTP errors do not expose key or subtitle text");}
   handler.Status=HttpStatusCode.OK;handler.Wait=true;using(var cancel=new CancellationTokenSource(50)){try{ClassicElevenLabs.SpeakAsync(client,"test","id","eleven_v4","","TEST",.5,.5,1,cancel.Token).GetAwaiter().GetResult();throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(true,"request cancellation reaches HTTP transport");}}
  }
  var input=System.IO.Path.Combine(data,"input");Directory.CreateDirectory(input);var file=System.IO.Path.Combine(input,"Мой голос.onnx");File.WriteAllBytes(file,new byte[]{1,2,3});
  try{ClassicPiperVoices.Import(file);throw new Exception("Missing config accepted");}catch(FileNotFoundException){}File.WriteAllText(file+".json","{}");try{ClassicPiperVoices.Import(file);throw new Exception("Invalid config accepted");}catch(InvalidDataException){}
  File.WriteAllText(file+".json","{\"audio\":{\"sample_rate\":22050},\"phoneme_id_map\":{\"a\":[1]}}");var imported=ClassicPiperVoices.Import(file);var duplicate=ClassicPiperVoices.Import(file);Check(imported!=duplicate && File.ReadAllBytes(imported).SequenceEqual(File.ReadAllBytes(file)),"custom import preserves Unicode name and never overwrites existing voice");
  var voices=new List<PiperModel>{new PiperModel("Known","Official","medium",System.IO.Path.GetFileName(imported),System.IO.Path.GetFileName(imported)+".json")};ClassicPiperVoices.AddCustom(voices);Check(voices.Count==2 && voices[1].Language=="Custom","installed official models are not duplicated as custom voices");
  Ui(data,false);
 }
 static void Show(Form form){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());Application.DoEvents();}
 static void Ui(string data,bool native){
  var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Test speech",1000,6000));
  using(var form=new TextToSpeech(sub,new SubRip(),null,null)){
   Show(form);var engines=(NikseComboBox)Field(form,"nikseComboBoxEngine");engines.SelectedIndex=((List<TextToSpeech.TextToSpeechEngine>)Field(form,"_engines")).First(e=>e.Id==TextToSpeech.TextToSpeechEngineId.Piper).Index;Application.DoEvents();
   var voices=(NikseComboBox)Field(form,"nikseComboBoxVoice");Check(voices.Items.Cast<object>().Any(v=>v.ToString().Contains("Custom")),"custom Piper voices appear in normal voice selector");var import=(Control)Field(form,"_piperImportButton");var test=(Control)Field(form,"buttonTestVoice");Check(import.Visible && import.Left>=test.Right && import.Right<=voices.Right,"Piper import button fits classic voice toolbar");
   if(native){
    var folder=System.IO.Path.Combine(data,"render");Directory.CreateDirectory(folder);typeof(TextToSpeech).GetField("_waveFolder",Flags).SetValue(form,folder);
    foreach(var name in new[]{"English custom","Russian custom"}){
     voices.SelectedItem=voices.Items.Cast<object>().First(v=>v.ToString().Contains(name));sub.Paragraphs[0].Text=name.StartsWith("English")?"Hello. This is a test of the imported voice.":"Привет. Это проверка импортированного голоса.";
     var ok=(bool)typeof(TextToSpeech).GetMethod("GenerateParagraphAudioPiperTts",Flags).Invoke(form,new object[]{sub,false,name+".wav"});var output=System.IO.Path.Combine(folder,name+".wav");Check(ok && ClassicVoiceAssets.WaveSeconds(output)>1,"real synthesis with imported "+name);}
   }
   engines.SelectedIndex=((List<TextToSpeech.TextToSpeechEngine>)Field(form,"_engines")).First(e=>e.Id==TextToSpeech.TextToSpeechEngineId.ElevenLabs).Index;Application.DoEvents();Check(!import.Visible,"Piper import control hides for other engines");var models=(NikseComboBox)Field(form,"nikseComboBoxRegion");Check(ClassicElevenLabs.Models.All(m=>models.Items.Contains(m)),"all five stable ElevenLabs models appear in classic selector");
   models.Text="eleven_v4_turbo";var languages=(NikseComboBox)Field(form,"nikseComboBoxLanguage");Check(languages.Items.Count==92 && languages.Items.Cast<TranslationPair>().Any(p=>p.Code=="ru"),"v4 language selector exposes stable list plus automatic choice");
   languages.SelectedItem=languages.Items.Cast<TranslationPair>().First(p=>p.Code=="ru");var snapshot=form.CaptureSessionRow(sub.Paragraphs[0],null,true);Check(snapshot.Model=="eleven_v4_turbo" && snapshot.Language=="ru","speech session retains ElevenLabs model and language");
   models.Text="eleven_v3";Check(!((Control)Field(form,"nikseUpDownSpeed")).Enabled && !((Control)Field(form,"nikseUpDownSimilarity")).Enabled,"v3 disables unsupported speed and similarity controls");models.Text="eleven_v4_turbo";
   using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(System.IO.Path.Combine(data,"eleven-extras.png"));}form.Close();
  }
 }
 static void Native(string data,string assets){
  Directory.CreateDirectory(ClassicPiperVoices.Folder);var runtime=System.IO.Path.Combine(assets,"runtime");foreach(var dir in Directory.GetDirectories(runtime,"*",SearchOption.AllDirectories))Directory.CreateDirectory(System.IO.Path.Combine(ClassicPiperVoices.Folder,dir.Substring(runtime.Length+1)));foreach(var file in Directory.GetFiles(runtime,"*",SearchOption.AllDirectories))File.Copy(file,System.IO.Path.Combine(ClassicPiperVoices.Folder,file.Substring(runtime.Length+1)),true);
  foreach(var pair in new[]{new[]{"en","en_US-lessac-low.onnx","English custom.onnx"},new[]{"ru","ru_RU-irina-medium.onnx","Russian custom.onnx"}}){var source=System.IO.Path.Combine(data,pair[2]);File.Copy(System.IO.Path.Combine(assets,pair[0],pair[1]),source,true);File.Copy(System.IO.Path.Combine(assets,pair[0],pair[1]+".json"),source+".json",true);ClassicPiperVoices.Import(source);}Ui(data,true);
 }
}
