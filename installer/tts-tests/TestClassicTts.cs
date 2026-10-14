using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicTts {
 [STAThread] static void Main(string[] args) {
  Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
  Configuration.Settings.General.Language="ru-RU";
  Nikse.SubtitleEdit.Logic.LanguageSettings.Current=Nikse.SubtitleEdit.Logic.Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));
  Directory.CreateDirectory(args[1]);Application.EnableVisualStyles();
  if(args[0]=="ui") {UI(args[1]);return;} if(args[0]=="export") {Export(args[1]);return;}
  typeof(Configuration).GetField("DataDirectory",BindingFlags.Static|BindingFlags.Public).SetValue(null,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Subtitle Edit")+Path.DirectorySeparatorChar);
  if(args[0]=="ui-clone" || args[0]=="ui-clone-cancel") {UiClone(args[1],args[0]=="ui-clone-cancel");return;} if(args[0]=="clone") {Clone(args[1]).GetAwaiter().GetResult();return;} if(args[0]=="ui-run" || args[0]=="ui-cancel") {UiRun(args[1],args[0]=="ui-cancel");return;} Live(args[1]).GetAwaiter().GetResult();
 }
 static async Task Live(string folder) {
  var settings=new ClassicQwenTtsSettings{Variant="cuda13",Language="ru",Voice="vivian"};
  var last=DateTime.MinValue;
  var progress=new Progress<SpeechProgress>(p=>{if((DateTime.UtcNow-last).TotalSeconds>10){last=DateTime.UtcNow;Console.WriteLine(p.Message);}});
  using(var engine=new ClassicQwenTts(settings)) {
   await engine.StartAsync(CancellationToken.None,progress);
   await engine.SpeakAsync("Привет! Сегодня мы проверяем озвучку русских субтитров.","vivian",Path.Combine(folder,"russian.wav"),CancellationToken.None);
   settings.Language="en";
   await engine.SpeakAsync("Hello! Today we are testing speech generation for English subtitles.","ryan",Path.Combine(folder,"english.wav"),CancellationToken.None);
  }
  settings.Language="ru";settings.Save();
  Console.WriteLine("PASS: native Russian and English Qwen3 TTS, CUDA13");
 }
 static void UiRun(string folder,bool cancel) {
  var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Привет! Сегодня мы проверяем озвучку русских субтитров.",1000,6500));sub.Paragraphs.Add(new Paragraph("Вторая реплика начинается после паузы.",9000,14500));
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  using(var form=new TextToSpeech(sub,new SubRip(),null,null)) {
   form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
   var combo=typeof(TextToSpeech).GetField("nikseComboBoxEngine",flags).GetValue(form);combo.GetType().GetProperty("Text").SetValue(combo,"Qwen3 TTS (CrispASR / local)");
   typeof(TextToSpeech).GetField("_waveFolder",flags).SetValue(form,Path.GetFullPath(folder));
   var before=System.Diagnostics.Process.GetProcessesByName("crispasr").Select(p=>p.Id).ToArray();
   var task=(Task<bool>)typeof(TextToSpeech).GetMethod("GenerateQwenAudioAsync",flags).Invoke(form,new object[]{sub,true,null});
   bool sent=false;var watch=System.Diagnostics.Stopwatch.StartNew();
   while(!task.IsCompleted && watch.Elapsed.TotalMinutes<5) {
    Application.DoEvents();Thread.Sleep(10);
    if(cancel && !sent && System.Diagnostics.Process.GetProcessesByName("crispasr").Any(p=>!before.Contains(p.Id))) {
     ((Button)typeof(TextToSpeech).GetField("buttonCancel",flags).GetValue(form)).PerformClick();sent=true;
    }
   }
   if(!task.IsCompleted)throw new Exception("UI TTS timeout");
   var success=task.GetAwaiter().GetResult();
   if(cancel) {if(success || !sent)throw new Exception("Cancellation failed");}
   else {
    if(!success || !File.Exists(Path.Combine(folder,"0.wav")) || !File.Exists(Path.Combine(folder,"1.wav")))throw new Exception("Missing generated cues");
    Configuration.Settings.General.FFmpegLocation=Environment.GetEnvironmentVariable("CLASSIC_TTS_TEST_FFMPEG");
    var clips=typeof(TextToSpeech).GetMethod("FixParagraphAudioSpeed",flags).Invoke(form,new object[]{sub,null});
    var mixed=(string)typeof(TextToSpeech).GetMethod("MergeAudioParagraphs",flags).Invoke(form,new[]{clips});
    File.Copy(mixed,Path.Combine(folder,"mixed.wav"),true);
   }
   if(System.Diagnostics.Process.GetProcessesByName("crispasr").Any(p=>!before.Contains(p.Id)))throw new Exception("Owned TTS server still running");
   if(typeof(TextToSpeech).GetField("_qwenCancellation",flags).GetValue(form)!=null)throw new Exception("Operation state not cleared");
   using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,"completed.png"));}
  }
  Console.WriteLine(cancel ? "PASS: cancellation after native TTS server start; no server remained" : "PASS: real classic form generation, timed cue adjustment and audio mixing");
 }
 static async Task Clone(string folder) {
  Configuration.Settings.General.FFmpegLocation=Environment.GetEnvironmentVariable("CLASSIC_TTS_TEST_FFMPEG");
  var input=Path.GetFullPath(Path.Combine(folder,"..","tts-tests-live","russian.wav"));
  var voice=await ClassicQwenReference.ImportAsync(input,"Synthetic test reference","Привет! Сегодня мы проверяем озвучку русских субтитров.",0,8,true,CancellationToken.None);
  try {
   ClassicQwenReference.Validate(voice.Id);
   var settings=new ClassicQwenTtsSettings{Variant="cuda13",Clone=true,Voice=voice.Id,Language="ru"};
   var last=DateTime.MinValue;
   using(var engine=new ClassicQwenTts(settings)) {
    await engine.StartAsync(CancellationToken.None,new Progress<SpeechProgress>(p=>{if((DateTime.UtcNow-last).TotalSeconds>10){last=DateTime.UtcNow;Console.WriteLine(p.Message);}}));
    await engine.SpeakAsync("Это новая реплика, произнесенная голосом из образца.",voice.Id,Path.Combine(folder,"cloned-russian.wav"),CancellationToken.None);
    settings.Language="en";
    await engine.SpeakAsync("Welcome back. This sentence was generated with the imported reference voice.",voice.Id,Path.Combine(folder,"cloned-english.wav"),CancellationToken.None);
   }
   Console.WriteLine("PASS: normalized reference with transcript; real new Russian and English clone requests");
  } finally {foreach(var ext in new[]{".wav",".txt",".json"})File.Delete(Path.Combine(ClassicQwenReference.Folder,voice.Id+ext));}
 }
 static void Export(string folder) {
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Test",0,1000));
  var job=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(job);
  var input=Path.Combine(job,"mixed.wav");File.WriteAllText(input,"old render test");
  var destination=Path.GetFullPath(Path.Combine(folder,"chosen-output.wav"));File.WriteAllText(destination,"previous output");
  using(var form=new TextToSpeech(sub,new SubRip(),null,null)) {
   typeof(TextToSpeech).GetField("_waveFolder",flags).SetValue(form,job);
   typeof(TextToSpeech).GetField("_ttsExportPath",flags).SetValue(form,destination);
   typeof(TextToSpeech).GetMethod("PublishSpeechExport",flags).Invoke(form,new object[]{input});
   typeof(TextToSpeech).GetMethod("CleanupSpeechJob",flags).Invoke(form,null);
   if(Directory.Exists(job) || File.ReadAllText(destination)!="old render test")throw new Exception("Export/cleanup failed");
   typeof(TextToSpeech).GetField("_waveFolder",flags).SetValue(form,folder);
   typeof(TextToSpeech).GetMethod("CleanupSpeechJob",flags).Invoke(form,null);
   if(!Directory.Exists(folder))throw new Exception("Non-job directory removed");
  }
  using(var settings=new ClassicQwenCloneSettings(new ClassicQwenTtsSettings())) {
   settings.StartPosition=FormStartPosition.Manual;settings.Location=new Point(-20000,-20000);settings.Show();Application.DoEvents();
   using(var bitmap=new Bitmap(settings.Width,settings.Height)){settings.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,"clone-settings.png"));}
  }
  using(var import=new ClassicQwenReferenceImport()) {
   import.StartPosition=FormStartPosition.Manual;import.Location=new Point(-20000,-20000);import.Show();Application.DoEvents();
   using(var bitmap=new Bitmap(import.Width,import.Height)){import.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,"clone-import.png"));}
  }
  Console.WriteLine("PASS: destination replacement and owned-job cleanup; unrelated directory preserved; classic clone dialogs rendered");
 }
 static void UiClone(string folder,bool cancel) {
  Configuration.Settings.General.FFmpegLocation=Environment.GetEnvironmentVariable("CLASSIC_TTS_TEST_FFMPEG");
  var settingsPath=Path.Combine(ClassicQwenTtsSettings.Root,"settings.json");var previous=File.ReadAllText(settingsPath);
  var input=Path.GetFullPath(Path.Combine(folder,"..","tts-tests-live","russian.wav"));
  var voice=ClassicQwenReference.ImportAsync(input,"UI synthetic test reference","Привет! Сегодня мы проверяем озвучку русских субтитров.",0,8,true,CancellationToken.None).GetAwaiter().GetResult();
  try {
   var settings=ClassicQwenTtsSettings.Load();settings.Clone=true;settings.Voice=voice.Id;settings.Save();
   UiRun(folder,cancel);
  } finally {
   File.WriteAllText(settingsPath,previous);
   foreach(var ext in new[]{".wav",".txt",".json"})File.Delete(Path.Combine(ClassicQwenReference.Folder,voice.Id+ext));
  }
 }
 static void UI(string folder) {
  var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Привет!",1000,3000));
  using(var form=new TextToSpeech(sub,new SubRip(),null,null)) {
   form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
   var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var combo=typeof(TextToSpeech).GetField("nikseComboBoxEngine",flags).GetValue(form);
   combo.GetType().GetProperty("Text").SetValue(combo,"Qwen3 TTS (CrispASR / local)");Application.DoEvents();
   var voice=typeof(TextToSpeech).GetField("nikseComboBoxVoice",flags).GetValue(form);
   var count=(int)voice.GetType().GetProperty("Items").GetValue(voice).GetType().GetProperty("Count").GetValue(voice.GetType().GetProperty("Items").GetValue(voice));
   if(count!=9)throw new Exception("Missing fixed voices");
   var download=(Button)typeof(TextToSpeech).GetField("_qwenDownloadButton",flags).GetValue(form);
   if(download==null || !download.Visible || !download.Parent.ClientRectangle.Contains(download.Bounds))throw new Exception("Download button clipped");
   using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,"tts-window.png"));}
  }
  Console.WriteLine("PASS: classic TTS layout, nine voices and model download button");
 }
}





