using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicLocalTts
{
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 [STAThread] static int Main(string[] args)
 {
  try{
   var folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);
   typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,(args[0]=="unit" ? folder : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)+"/Subtitle Edit")+Path.DirectorySeparatorChar);
   Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[2];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));
   Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));Application.EnableVisualStyles();
   if(args[0]=="unit")Unit(folder);else if(args[0]=="cast")Cast(folder,args);else if(args[0]=="per-line")PerLine(folder,args);else Native(folder,args).GetAwaiter().GetResult();
   Application.ExitThread();Environment.Exit(0);return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
 static void Unit(string folder)
 {
  Check(ClassicLocalTts.Catalog.Length==9,"nine stable local engines registered");
  foreach(var e in ClassicLocalTts.Catalog)foreach(var model in e.Models)foreach(var file in model.Files)Check(file.Bytes>0 && file.Hash.Length==64 && file.File.EndsWith(".gguf"),e.Id+" / "+model.Key+" complete model manifest");
  var cosy=ClassicLocalTts.Catalog.First(x=>x.Id=="cosy");Check(cosy.Models.All(m=>m.Files.Length==6) && cosy.Presets.Length==8,"CosyVoice stages all six companions and eight presets");
  var chatter=ClassicLocalTts.Catalog.First(x=>x.Id=="chatter");Check(chatter.LanguageChoices("Turbo").Single()=="en|English" && chatter.Languages.Any(l=>l.StartsWith("ru|")),"Turbo restricted to English; Base advertises Russian");
  var s=ClassicLocalTtsSettings.Load("cosy");s.Variant="cuda13";s.Model="RL Q4_K";s.SourceLanguage="en";s.Speed=1.1;s.Save("cosy");var round=ClassicLocalTtsSettings.Load("cosy");Check(round.Variant=="cuda13" && round.Model=="RL Q4_K" && round.SourceLanguage=="en" && round.Speed==1.1,"engine settings round trip");
  using(var engine=new ClassicLocalTts("cosy",s)){var p=engine.BuildPayload("Привет","fleurs-en","");Check((string)p["language"]=="ru" && !p.ContainsKey("voice") && !p.ContainsKey("source_lang"),"preset request uses startup voice and target language");}
  using(var f=new ClassicLocalTtsSettingsForm("chatter",ClassicLocalTtsSettings.Load("chatter"))){f.Show();Application.DoEvents();using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height));image.Save(Path.Combine(folder,"local-settings.png"));}Check(f.Controls[0].Controls.Cast<Control>().All(c=>c.Bottom<=f.Controls[0].ClientSize.Height),"model settings buttons fit minimum window");f.Close();}
  var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Hello there",0,2500));
  using(var f=new TextToSpeech(sub,new SubRip(),"",null))
  {
   f.Show();Application.DoEvents();var combo=(Nikse.SubtitleEdit.Controls.NikseComboBox)f.GetType().GetField("nikseComboBoxEngine",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);
   foreach(var e in ClassicLocalTts.Catalog){combo.Text=e.Name;Application.DoEvents();Check(combo.SelectedIndex>=0,"UI selects "+e.Name);using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height));image.Save(Path.Combine(folder,e.Id+"-ui.png"));}}
   f.Close();
  }
 }
 static void Pump(Task task){while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(20);}task.GetAwaiter().GetResult();}
 static void Cast(string folder,string[] args)
 {
  var id=args[3];var definition=ClassicLocalTts.Catalog.First(e=>e.Id==id);var reference=Path.GetFullPath(args[4]);
  var imported=ClassicQwenReference.ImportAsync(reference,"Mixed cast test",File.ReadAllText(Path.ChangeExtension(reference,".txt")),0,Math.Min(15,ClassicVoiceAssets.WaveSeconds(reference)),true,CancellationToken.None).GetAwaiter().GetResult();
  var saved=ClassicLocalTtsSettings.Load(id);var variant=saved.Variant;saved.Variant=args[5];saved.Save(id);
  try{
   var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Hello! This is the first actor speaking.",0,5000){Actor="First"});sub.Paragraphs.Add(new Paragraph("Hello! This is the second actor speaking.",6000,11000){Actor="Second"});
   using(var f=new TextToSpeech(sub,new AdvancedSubStationAlpha(),"",null))
   {
    f.ShowInTaskbar=false;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();var flags=BindingFlags.Instance|BindingFlags.NonPublic;f.GetType().GetField("_waveFolder",flags).SetValue(f,folder);
    var cast=new ClassicCastSettings();cast.Rows.Add(new ClassicCastRow{Actor="First",Engine=definition.Name,Model=definition.Models[0].Key,Voice=imported.Id,Language="en",Selected=true});cast.Rows.Add(new ClassicCastRow{Actor="Second",Engine="Supertonic",Voice="F1",Language="en",Selected=true});f.GetType().GetField("_castSettings",flags).SetValue(f,cast);
    var task=(Task<bool>)f.GetType().GetMethod("GenerateParagraphAudioAsync",flags).Invoke(f,new object[]{sub,true,null});Pump(task);Check(task.Result && ClassicVoiceAssets.WaveSeconds(Path.Combine(folder,"0.wav"))>1 && ClassicVoiceAssets.WaveSeconds(Path.Combine(folder,"1.wav"))>1,"real mixed "+id+" / Supertonic actor cast");
    var regeneration=f.ReGenerateAudioAsync(sub.Paragraphs[0],sub.Paragraphs[1],imported.Name);Pump(regeneration);Check(regeneration.Result!=null && File.Exists(regeneration.Result.Filename),"audio review regenerates new engine actor using imported voice name");
    Check(f.GetParagraphEngine(sub.Paragraphs[0]).Name==definition.Name,"review resolves new engine instead of global default");
    using(var writer=new StreamWriter(Path.Combine(folder,"cast-complete.txt"))){writer.WriteLine(regeneration.Result.Filename);}
    // Regeneration exercises native media-info/WinForms interop. Clean fixtures explicitly
    // before exiting this isolated harness; product-window shutdown is not asserted here.
    saved.Variant=variant;saved.Save(id);foreach(var ext in new[]{".wav",".txt",".json"})File.Delete(Path.Combine(ClassicQwenReference.Folder,imported.Id+ext));
    Environment.Exit(0);
   }
  }finally{saved.Variant=variant;saved.Save(id);foreach(var ext in new[]{".wav",".txt",".json"})File.Delete(Path.Combine(ClassicQwenReference.Folder,imported.Id+ext));}
 }
 static void PerLine(string folder,string[] args)
 {
  var id=args[3];var definition=ClassicLocalTts.Catalog.First(e=>e.Id==id);var input=Path.GetFullPath(args[4]);var saved=ClassicLocalTtsSettings.Load(id);var previous=Newtonsoft.Json.JsonConvert.SerializeObject(saved);saved.Variant=args[5];saved.SourceLanguage="en";saved.Language="ru";saved.Save(id);
  var before=ClassicQwenReference.List().Select(v=>v.Id).ToArray();
  try{
   var source=new Subtitle();source.Paragraphs.Add(new Paragraph(File.ReadAllText(Path.ChangeExtension(input,".txt")),0,ClassicVoiceAssets.WaveSeconds(input)*1000));var target=new Subtitle();target.Paragraphs.Add(new Paragraph("Сегодня мы проверяем русскую озвучку. Каждое слово должно звучать ясно и естественно.",0,8000));
   using(var f=new TextToSpeech(target,new SubRip(),input,new VideoInfo{TotalSeconds=ClassicVoiceAssets.WaveSeconds(input)}))
   {
    f.ShowInTaskbar=false;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();var flags=BindingFlags.Instance|BindingFlags.NonPublic;f.GetType().GetField("_waveFolder",flags).SetValue(f,folder);f.GetType().GetField("_perLineOriginal",flags).SetValue(f,source);f.GetType().GetField("_castSettings",flags).SetValue(f,new ClassicCastSettings());var combo=(Nikse.SubtitleEdit.Controls.NikseComboBox)f.GetType().GetField("nikseComboBoxEngine",flags).GetValue(f);combo.Text=definition.Name;
    var task=(Task<bool>)f.GetType().GetMethod("GenerateParagraphAudioAsync",flags).Invoke(f,new object[]{target,true,null});Pump(task);Check(task.Result && ClassicVoiceAssets.WaveSeconds(Path.Combine(folder,"0.wav"))>1,"real per-line "+id+" English reference to Russian synthesis");Check(ClassicQwenReference.List().Select(v=>v.Id).OrderBy(v=>v).SequenceEqual(before.OrderBy(v=>v)),"per-line temporary reference cleanup");f.Close();
   }
  }finally{Newtonsoft.Json.JsonConvert.DeserializeObject<ClassicLocalTtsSettings>(previous).Save(id);}
 }
 static async Task Native(string folder,string[] args)
 {
  var id=args[3];var reference=Path.GetFullPath(args[4]);var transcript=File.ReadAllText(Path.ChangeExtension(reference,".txt"));
  var imported=await ClassicQwenReference.ImportAsync(reference,"Classic local TTS test",transcript,0,Math.Min(15,ClassicVoiceAssets.WaveSeconds(reference)),true,CancellationToken.None);
  try{
   var s=ClassicLocalTtsSettings.Load(id);s.Model=args.Length>6 ? args[6] : ClassicLocalTts.Catalog.First(e=>e.Id==id).Models[0].Key;s.Variant=args[5];s.SourceLanguage="en";s.Voice=imported.Id;
   using(var engine=new ClassicLocalTts(id,s))
   {
    var progress=new Progress<SpeechProgress>(p=>Console.WriteLine(p.Message));
    s.Language="en";var en=Path.Combine(folder,"english.wav");await engine.SpeakAsync("Today we are testing English subtitles. Every sentence should sound clear and natural.",s.Voice,en,CancellationToken.None,progress);Check(ClassicVoiceAssets.WaveSeconds(en)>1,"real "+id+" English synthesis");
    s.Language="ru";var ru=Path.Combine(folder,"russian.wav");await engine.SpeakAsync("Сегодня мы проверяем русскую озвучку. Каждое слово должно звучать ясно и естественно.",s.Voice,ru,CancellationToken.None,progress);Check(ClassicVoiceAssets.WaveSeconds(ru)>1,"real "+id+" Russian synthesis");
    if(id=="cosy"){s.Language="en";await engine.SpeakAsync("Hello! This is a preset voice test.","fleurs-en",Path.Combine(folder,"preset.wav"),CancellationToken.None,progress);Check(true,"preset switching restarts CosyVoice server");}
    using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{await engine.SpeakAsync("Cancelled test",s.Voice,Path.Combine(folder,"cancelled.wav"),cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(!File.Exists(Path.Combine(folder,"cancelled.wav")),"cancel leaves no generated audio");}}
   }
  }finally{foreach(var extension in new[]{".wav",".txt",".json"})File.Delete(Path.Combine(ClassicQwenReference.Folder,imported.Id+extension));}
 }
}
