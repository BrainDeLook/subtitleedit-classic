using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Ocr;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Forms.Tts;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicNext
{
 static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 [STAThread]static int Main(string[] args){try{var folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);typeof(Configuration).GetField("DataDirectory",BindingFlags.Static|BindingFlags.Public).SetValue(null,folder+Path.DirectorySeparatorChar);Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[2];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.EnableVisualStyles();if(args[0]=="native")Native(folder,args).GetAwaiter().GetResult();else if(args[0]=="playback-native")Pump(PlaybackNative(folder,args[3]));else if(args[0]=="playback")Playback().GetAwaiter().GetResult();else if(args[0]=="nocr")NOcr(folder,args[3]);else Unit(folder);return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
 sealed class FakeVideo:IClassicSpeechVideo {
  System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();double position,changed;bool playing;public bool HasVideo=>true;public bool IsPlaying=>playing;public double Position=>position+(playing ? clock.Elapsed.TotalSeconds-changed : 0);public double Speed=>1;public double Volume{get;set;}=80;public void Play(){if(!playing){changed=clock.Elapsed.TotalSeconds;playing=true;}}public void Pause(){position=Position;playing=false;}public void Seek(double value){position=value;changed=clock.Elapsed.TotalSeconds;}
 }
 sealed class FakeAudio:IClassicSpeechAudio {
  System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();double pos,changed,rate=1;bool playing;public bool Disposed;public int PauseCalls;public double TestDuration=0.25;public double Duration=>TestDuration;public double Position=>Math.Min(Duration,pos+(playing?(clock.Elapsed.TotalSeconds-changed)*rate:0));public bool IsPaused=>!playing || Position>=Duration;public double Speed{set{pos=Position;changed=clock.Elapsed.TotalSeconds;rate=value;}}public Task LoadAudio(string path){pos=0;playing=false;return Task.CompletedTask;}public void Play(){changed=clock.Elapsed.TotalSeconds;playing=true;}public void Pause(){PauseCalls++;pos=Position;playing=false;}public void Dispose(){Disposed=true;playing=false;}
 }
 static async Task Playback(){
  var lines=new[]{new ClassicSpeechLine{Paragraph=new Paragraph("One",0,200)},new ClassicSpeechLine{Paragraph=new Paragraph("Two",350,550)}};var video=new FakeVideo();var audio=new FakeAudio();var calls=0;var waiting=0;var started=new List<int>();
  var reader=new ClassicSpeechReader(async(l,f,c)=>{await Task.Delay(100,c);Interlocked.Increment(ref calls);var p=Path.Combine(f,Guid.NewGuid()+".wav");File.WriteAllText(p,"fixture");return p;},audioFactory:()=>audio);
  using(var timeout=new CancellationTokenSource(10000))await reader.RunAsync(lines,video,i=>started.Add(i),i=>waiting++,timeout.Token);
  Check(started.SequenceEqual(new[]{0,1}) && calls==2,"lookahead speaks each line once in order");Check(video.Volume==80 && !video.IsPlaying && audio.Disposed,"playback restores video volume and disposes audio");
  video=new FakeVideo();audio=new FakeAudio();reader=new ClassicSpeechReader(async(l,f,c)=>{await Task.Delay(2000,c);return null;},audioFactory:()=>audio);
  using(var cancel=new CancellationTokenSource(150)){try{await reader.RunAsync(lines,video,i=>{},i=>{},cancel.Token);throw new Exception("cancel ignored");}catch(OperationCanceledException){}}
  Check(video.Volume==80 && !video.IsPlaying && audio.Disposed,"cancel during generation cleans playback state");
  video=new FakeVideo();audio=new FakeAudio{TestDuration=1.4};lines=new[]{new ClassicSpeechLine{Paragraph=new Paragraph("Pause",0,2000)}};
  var pauseVideo=video;Task controls=null;reader=new ClassicSpeechReader((l,f,c)=>Task.FromResult("fixture"),audioFactory:()=>audio);
  using(var timeout=new CancellationTokenSource(10000))await reader.RunAsync(lines,video,i=>{controls=Task.Run(async()=>{await Task.Delay(650);pauseVideo.Pause();await Task.Delay(500);pauseVideo.Play();});},i=>{},timeout.Token);
  await controls;Check(audio.PauseCalls>0,"user pause holds the speech and resume continues it");
  video=new FakeVideo();audio=new FakeAudio();lines=new[]{new ClassicSpeechLine{Paragraph=new Paragraph("Before seek",0,300)},new ClassicSpeechLine{Paragraph=new Paragraph("After seek",1500,1900)}};started.Clear();var seekVideo=video;reader=new ClassicSpeechReader((l,f,c)=>Task.FromResult("fixture"),audioFactory:()=>audio);
  using(var timeout=new CancellationTokenSource(10000))await reader.RunAsync(lines,video,i=>{started.Add(i);if(i==0)seekVideo.Seek(1.5);},i=>{},timeout.Token);
  Check(started.SequenceEqual(new[]{0,1}),"seek stops the old clip and resumes at the new line");

 }
 static void Pump(Task task){while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(10);}task.GetAwaiter().GetResult();}
 static async Task PlaybackNative(string folder,string reference){
  var dll=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"libmpv-2.dll");File.Copy(dll,Path.Combine(folder,"libmpv-2.dll"),true);
  var video=new FakeVideo();var started=new List<int>();var lines=new[]{new ClassicSpeechLine{Paragraph=new Paragraph("First",0,3500)},new ClassicSpeechLine{Paragraph=new Paragraph("Second",4000,7500)}};
  var type=typeof(ClassicSpeechReader).Assembly.GetType("Nikse.SubtitleEdit.Logic.ClassicSpeechPlayer");
  var reader=new ClassicSpeechReader((l,f,c)=>Task.FromResult(Path.GetFullPath(reference)),audioFactory:()=> (IClassicSpeechAudio)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{0},null));
  using(var cancel=new CancellationTokenSource(30000))await reader.RunAsync(lines,video,i=>started.Add(i),i=>{},cancel.Token);
  Check(started.SequenceEqual(new[]{0,1}),"real muted MPV plays two clips and advances across EOF");Check(video.Volume==80 && !video.IsPlaying,"native speech playback restores source volume and pause state");
 }
 static void Unit(string folder){
  Check(ClassicLocalTts.Catalog.Length==9,"nine classic local engines");
  foreach(var e in ClassicLocalTts.Catalog){foreach(var m in e.Models)foreach(var f in m.Files)Check(f.Bytes>0 && f.Hash.Length==64,"pinned "+e.Id+" "+m.Key);
   if(e.IsAudioCpp){var s=ClassicLocalTtsSettings.Load(e.Id);s.Variant="cuda";s.Language=e.Languages.FirstOrDefault()?.Split('|')[0];var voice=e.Presets.FirstOrDefault() ?? "reference";var p=ClassicAudioCpp.Payload(e,s,"Hello",voice,"Exact reference transcript",folder);Check(!p.ContainsKey("consent_attestation") && !p.ContainsKey("speed"),"audio.cpp-specific protocol "+e.Id);if(e.Id=="firered")Check((string)((Dictionary<string,object>)p["options"])["language"]=="English","FireRed uses model language tags");if(e.Id=="kugel")Check(!p.ContainsKey("voice_ref"),"Kugel preset-only request");}
   using(var f=new ClassicLocalTtsSettingsForm(e.Id,ClassicLocalTtsSettings.Load(e.Id))){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(folder,e.Id+"-settings.png"));}Check(f.Controls[0].Controls.Cast<Control>().All(c=>c.Bottom<=f.Controls[0].Height),"settings fit "+e.Id);f.Close();}
  }
  using(var f=new Nikse.SubtitleEdit.Forms.Ocr.VobSubNOcrTrain()){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(folder,"nocr-training.png"));}f.Size=f.MinimumSize;Application.DoEvents();using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(folder,"nocr-training-min.png"));}f.Close();}
  var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Hello",0,2000));using(var f=new TextToSpeech(sub,new Nikse.SubtitleEdit.Core.SubtitleFormats.SubRip(),"",null)){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(folder,"tts-playback.png"));}f.Close();}
 }
 static NikseBitmap Solid(int w,int h){using(var b=new Bitmap(w,h)){using(var g=Graphics.FromImage(b))g.Clear(Color.White);return new NikseBitmap(b);}}
 static void NOcr(string folder,string database){
  var bar=Solid(6,18);var db=new NOcrDb(Path.Combine(folder,"db.nocr"));var quote=new NOcrChar("'"){Width=3,Height=9};quote.LinesForeground.Add(new NOcrPoint(new Point(1,0),new Point(1,8)));db.Add(quote);Check(db.GetMatchSingle(Solid(8,22),0,true,25,true)==null,"small apostrophe cannot steal a large glyph");Check(db.GetMatchSingle(bar,0,true,25)?.Text=="'","small glyph allows sane scaling");db.Save();db.Save();Check(File.Exists(db.FileName+".bak"),"database save preserves previous copy");var loaded=new NOcrDb(db.FileName);Check(loaded.TotalCharacterCount==1 && loaded.OcrCharacters[0].Text=="'","V2 database round trip");var original=new NOcrDb(database);Check(original.TotalCharacterCount>100,"existing Cyrillic database remains readable");
  foreach(NOcrLineAlgorithm algorithm in Enum.GetValues(typeof(NOcrLineAlgorithm))){
   foreach(var sentence in new[]{"Hello World 123","Привет мир 456"}) {
    var trained=new NOcrDb(Path.Combine(folder,"trained-"+algorithm+"-"+(sentence[0]=='H'?"en":"ru")+".nocr"));var settings=new NOcrTrainerSettings{FontNames=new List<string>{"Arial"},FontSize=50,CharactersToTrain=sentence,LineSegmentAlgorithm=algorithm};var learned=new NOcrTrainer().Train(settings,trained);Check(learned>5,"font trainer learns "+algorithm);trained.Save();var text=Recognize(trained,sentence);Console.WriteLine("RECOGNIZED "+algorithm+": "+text);Check(text==sentence,"glyph round trip "+algorithm+" "+sentence);
    var again=new NOcrTrainer().Train(settings,trained);Check(again==0,"training skips recognized glyphs "+algorithm);var before=trained.TotalCharacterCount;new NOcrTrainer().Train(settings,trained,()=>true);Check(before==trained.TotalCharacterCount,"cancel stops training "+algorithm);
   }

  }
 }
 static string Recognize(NOcrDb db,string text){using(var rendered=NOcrTrainer.RenderCharacterImage(text,"Arial",50,false,false)){var bitmap=new NikseBitmap(rendered);bitmap.MakeTwoColor(200);bitmap.CropTop(0,Color.Transparent);var items=ClassicNOcrBitmap.Split(bitmap,12,false,false,25,false);var output="";for(int i=0;i<items.Count;i++){var item=items[i];if(item.NikseBitmap==null){output+=item.SpecialCharacter;continue;}var match=db.GetMatch(bitmap,items,item,item.Top,true,25);output+=match?.Text ?? "*";if(match!=null && match.ExpandCount>1)i+=match.ExpandCount-1;}return output;}}
 static async Task Native(string folder,string[] args){
  var id=args[3];var reference=Path.GetFullPath(args[4]);var s=ClassicLocalTtsSettings.Load(id);var d=ClassicLocalTts.Catalog.First(e=>e.Id==id);s.Variant="cuda";s.Model=d.Models[0].Key;s.LicenseAccepted=ClassicAudioCpp.License(id)?[0];var r=await ClassicQwenReference.ImportAsync(reference,"Permitted synthetic test","Today we are testing English subtitles. Every sentence should sound clear and natural.",0,Math.Min(15,ClassicVoiceAssets.WaveSeconds(reference)),true,CancellationToken.None);s.Voice=d.Presets.FirstOrDefault() ?? r.Id;s.Language=id=="firered" ? "English" : "en";s.Save(id);
  using(var engine=new ClassicLocalTts(id,s)){
   var output=Path.Combine(folder,id+"-english.wav");await engine.SpeakAsync("Today we are testing English subtitles. Every sentence should sound clear and natural.",s.Voice,output,CancellationToken.None);Check(ClassicVoiceAssets.WaveSeconds(output)>1,id+" real English synthesis");
   if(id!="breeze" && id!="index25"){s.Language=id=="firered"?"Russian":"ru";if(id=="firered"){var ruRef=await ClassicQwenReference.ImportAsync(Path.Combine(Path.GetDirectoryName(reference),"russian.wav"),"Permitted Russian synthetic reference","Сегодня мы проверяем озвучку русских субтитров. Голос должен произносить слова ясно.",0,15,true,CancellationToken.None);s.Voice=ruRef.Id;}await engine.SpeakAsync("Сегодня мы проверяем русскую озвучку. Каждое слово должно звучать ясно и естественно.",s.Voice,Path.Combine(folder,id+"-russian.wav"),CancellationToken.None);Check(true,id+" real Russian request completed");}
   using(var cancel=new CancellationTokenSource(20)){try{await engine.SpeakAsync("This synthesis must be cancelled before it completes.",s.Voice,Path.Combine(folder,id+"-cancel.wav"),cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(!File.Exists(Path.Combine(folder,id+"-cancel.wav")),id+" cancellation leaves no partial WAV");}}
  }
 }
}
