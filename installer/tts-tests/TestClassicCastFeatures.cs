using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicCast;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicCastFeatures
{
 static readonly BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
 static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);Console.WriteLine("PASS: "+msg);}
 [STAThread] static void Main(string[] args)
 {
  var folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
  typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,((args[0]=="unit" || args[0]=="pack") ? folder : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)+"/Subtitle Edit")+Path.DirectorySeparatorChar);
  Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[2];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.EnableVisualStyles();
  if(args[0]=="unit")Unit(folder);else if(args[0]=="design")Design(folder,args.Length>3 ? args[3] : "en");else if(args[0]=="sidon")Sidon(folder,args[3]);else if(args[0]=="cast")Cast(folder);else if(args[0]=="per-line")PerLine(folder,args[3]);else if(args[0]=="check-speech")CheckSpeech(folder,args[3],args[4]);else if(args[0]=="pack")Pack(folder);else if(args[0]=="diarize")Diarize(folder,args[3],args.Length>4 ? args[4] : "en");
  Application.ExitThread();
  Environment.Exit(0);
 }
 static void Unit(string folder)
 {
  var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("MIKE: Hello there.",0,2000));sub.Paragraphs.Add(new Paragraph("Continue speaking.",2000,4000));sub.Paragraphs.Add(new Paragraph("[НАРРАТОР] Это проверка.",4000,6000));sub.Paragraphs.Add(new Paragraph("[music]",6000,8000));
  var candidates=TextSpeakerDetector.Detect(sub);TextSpeakerDetector.Apply(sub,candidates.Where(c=>TextSpeakerDetector.IsConfidentSpeakerName(c.Speaker)).ToList(),true);
  Check(sub.Paragraphs[0].Actor=="MIKE" && sub.Paragraphs[1].Actor=="MIKE" && sub.Paragraphs[2].Actor=="НАРРАТОР" && sub.Paragraphs[3].Text=="[music]","speaker tags move to actors; continuation and sound annotations retained");
  var diarized=new Subtitle();diarized.Paragraphs.Add(new Paragraph("(Speaker 1) Hello",0,2000));diarized.Paragraphs.Add(new Paragraph("SPEAKER_02: Hi",2000,4000));Check(SpeakerLabelParser.MoveLabelsToActors(diarized)==2,"diarization labels parsed");
  var mapping=SpeakerLabelParser.AssignSpeakersByOverlap(sub.Paragraphs,diarized.Paragraphs);Check(mapping[sub.Paragraphs[0]]=="Speaker 1" && mapping[sub.Paragraphs[1]]=="Speaker 2" && !mapping.ContainsKey(sub.Paragraphs[3]),"actors assigned by actual overlap without guessing gaps");
  var cast=new ClassicCastSettings();cast.Rows.Add(new ClassicCastRow{Actor="MIKE",Engine="Qwen3",Model="VoiceDesign",Voice="ryan",Language="en",Instructions="Calm voice",Selected=true});cast.Save();var loaded=ClassicCastSettings.Load().Get("MIKE");Check(loaded.Engine=="Qwen3" && loaded.Instructions=="Calm voice" && loaded.Selected,"cast settings round trip");
  var synthetic=Path.Combine(folder,"fixture.wav");Run("-nostdin -y -f lavfi -i sine=frequency=220:duration=3 -ar 24000 -ac 1 -c:a pcm_s16le "+Q(synthetic));
  var voice=ClassicQwenReference.ImportAsync(synthetic,"Fixture","Exact test text.",0,3,true,CancellationToken.None).GetAwaiter().GetResult();ClassicVoiceLibrary.Update(voice,"Renamed","Updated exact text.");Check(File.ReadAllText(Path.Combine(ClassicQwenReference.Folder,voice.Id+".txt"))=="Updated exact text.","voice rename/transcript update");
  var zip=Path.Combine(folder,"pack.zip");ClassicVoiceLibrary.Export(new[]{voice},zip);var imported=ClassicVoiceLibrary.ImportPackAsync(zip,true,CancellationToken.None).GetAwaiter().GetResult();Check(imported.Count==1 && imported[0].Id!=voice.Id && imported[0].Name=="Renamed","voice-pack round trip uses new IDs without replacing originals");
  var beforeCancel=ClassicQwenReference.List().Count;using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{ClassicVoiceLibrary.ImportPackAsync(zip,true,cancel.Token).GetAwaiter().GetResult();throw new Exception("cancel ignored");}catch(OperationCanceledException){Check(ClassicQwenReference.List().Count==beforeCancel,"cancelled pack leaves library unchanged");}}
  using(var f=new TextToSpeech(sub,new AdvancedSubStationAlpha(),null,null))
  {
   Show(f);var list=(ListView)Get(f,"listViewActors");Check(list.CheckedItems.Count==1,"cast selection restored in main window");
   Modal(f,"EditClassicCast",folder,"cast.png");Save(f,Path.Combine(folder,"tts.png"));f.Dispose();
  }
  using(var manager=new ClassicVoiceManager()){Show(manager);manager.Size=manager.MinimumSize;Application.DoEvents();Save(manager,Path.Combine(folder,"voice-manager.png"));Check(manager.Controls.OfType<FlowLayoutPanel>().SelectMany(c=>c.Controls.OfType<Button>()).All(b=>b.Bottom<=b.Parent.ClientSize.Height),"voice manager buttons fit");manager.Dispose();}
  using(var crop=new ClassicQwenReferenceImport(voice)){Show(crop);Save(crop,Path.Combine(folder,"trim-reference.png"));crop.Dispose();}
  var video=Path.Combine(folder,"source.mp4");Run("-nostdin -y -f lavfi -i color=c=blue:s=640x360:r=24:d=4 -i "+Q(synthetic)+" -c:v libx264 -preset ultrafast -c:a aac -shortest "+Q(video));
  using(var f=new GenerateVideoWithHardSubs(sub,new SubRip(),video,new VideoInfo{Width=640,Height=360,TotalSeconds=4,TotalMilliseconds=4000,FramesPerSecond=24,TotalFrames=96,Success=true},30,false))
  {
   Set(f,"_mpvOn",false);Show(f);((Control)Get(f,"comboBoxVideoEncoding")).Text="h264_nvenc";((Control)Get(f,"comboBoxAudioEnc")).Text="aac";var target=(Control)Get(f,"numericUpDownTargetFileSize");target.GetType().GetProperty("Value").SetValue(target,999999m,null);((CheckBox)Get(f,"checkBoxTargetFileSize")).Checked=true;Call(f,"numericUpDownTargetFileSize_ValueChanged",null,EventArgs.Empty);Call(f,"LayoutClassicHints");
   var hint=(Control)Get(f,"labelCrfHint");var cq=(Control)Get(f,"comboBoxCrf");var rate=(Control)Get(f,"labelVideoBitrate");var group=(Control)Get(f,"groupBoxVideo");Check(!string.IsNullOrWhiteSpace(hint.Text),"NVENC quality hint populated");Check(hint.Left>cq.Right && hint.Right<=group.ClientSize.Width,"CQ hint never overlaps input");Check(rate.Top>target.Bottom && rate.Right<group.Right,"large bitrate stays in left column");Save(f,Path.Combine(folder,"burn-in-hints.png"));f.Dispose();
  }
  using(var tts=new TextToSpeech(sub,new AdvancedSubStationAlpha(),video,new VideoInfo{Width=640,Height=360,TotalSeconds=4,TotalMilliseconds=4000,FramesPerSecond=24,Success=true})){Show(tts);Modal(tts,"EditVideoVoices",folder,"video-voices.png");tts.Dispose();}
  var parts=ClassicVideoVoices.PickReferences(sub.Paragraphs);Check(parts.Count>0 && parts.Sum(p=>p.Duration.TotalSeconds)<=30,"reference clips remain within 30 seconds");
 }
 static void Design(string folder,string language)
 {
  var settings=ClassicQwenTtsSettings.Load();settings.Design=true;settings.Clone=false;settings.Language=language;settings.Instructions="A clear warm adult male voice, natural pacing.";
  using(var engine=new ClassicQwenTts(settings)){engine.StartAsync(CancellationToken.None).GetAwaiter().GetResult();engine.SpeakAsync(language=="ru" ? "Привет! Сегодня мы проверяем новый голос. Каждое слово должно звучать ясно и естественно." : "Hello! Today we are checking a new voice. Every word should sound clear and natural.","vivian",Path.Combine(folder,"design.wav"),CancellationToken.None).GetAwaiter().GetResult();}
  Check(Seconds(Path.Combine(folder,"design.wav"))>1,"native VoiceDesign synthesis");
 }
 static void Sidon(string folder,string input)
 {
  var output=ClassicSidon.CleanAsync(input,folder,"cpu",CancellationToken.None,null).GetAwaiter().GetResult();Check(Seconds(output)>1,"native Sidon reference cleaning");
 }
 static void Cast(string folder)
 {
  var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Hello! This is Alice speaking clearly.",0,5000){Actor="Alice"});sub.Paragraphs.Add(new Paragraph("And this is Bob. We are testing two different voices.",5000,11000){Actor="Bob"});
  using(var f=new TextToSpeech(sub,new AdvancedSubStationAlpha(),null,null))
  {
   Show(f);Set(f,"_waveFolder",folder);var settings=new ClassicCastSettings();settings.Rows.Add(new ClassicCastRow{Actor="Alice",Engine="Qwen3",Model="CustomVoice",Voice="ryan",Language="en",Selected=true});settings.Rows.Add(new ClassicCastRow{Actor="Bob",Engine="Supertonic",Voice="F1",Language="en",Selected=true});Set(f,"_castSettings",settings);
   var task=(Task<bool>)Call(f,"GenerateParagraphAudioAsync",sub,true,null);Pump(task);Check(task.Result && Seconds(Path.Combine(folder,"0.wav"))>1 && Seconds(Path.Combine(folder,"1.wav"))>1,"native mixed Qwen3/Supertonic actor cast");
   var regeneration=f.ReGenerateAudioAsync(sub.Paragraphs[0],sub.Paragraphs[1],"aiden");Pump(regeneration);Check(regeneration.Result!=null && File.Exists(regeneration.Result.Filename),"review regenerates mapped actor with its own engine and unique output");
   Check(f.GetParagraphEngine(sub.Paragraphs[0]).Id==TextToSpeech.TextToSpeechEngineId.Qwen3Crisp && f.GetParagraphEngine(sub.Paragraphs[1]).Id==TextToSpeech.TextToSpeechEngineId.SupertonicCrisp,"review resolves each actor's engine");
   var cancelled=(Task<bool>)Call(f,"GenerateParagraphAudioAsync",sub,true,"cancel.wav");((CancellationTokenSource)Get(f,"_castCancellation")).Cancel();Pump(cancelled);Check(!cancelled.Result && Get(f,"_castCancellation")==null,"cast cancellation clears owned process/session state");f.Dispose();
  }
 }
 static void CheckSpeech(string folder,string input,string language)
 {
  var engine=Nikse.SubtitleEdit.Logic.SpeechToText.ClassicSpeechCatalog.Engines.First(e=>e.Family=="crisp" && e.Backend==(language=="ru" ? "gigaam" : "parakeet"));var model=engine.Models.First(m=>!m.Aligner && (language!="ru" || m.Name.Contains("v3")));
  Nikse.SubtitleEdit.Logic.SpeechToText.ClassicSpeechDownload.EnsureRuntimeAsync(engine,"cpu",CancellationToken.None,null).GetAwaiter().GetResult();Nikse.SubtitleEdit.Logic.SpeechToText.ClassicSpeechDownload.EnsureModelAsync(engine,model,CancellationToken.None,null).GetAwaiter().GetResult();
  var wave=Path.Combine(folder,"input.wav");Run("-nostdin -y -i "+Q(input)+" -ar 16000 -ac 1 -c:a pcm_s16le "+Q(wave));var text=Nikse.SubtitleEdit.Logic.SpeechToText.ClassicSpeechRunner.TranscribeAsync(engine,model,new Nikse.SubtitleEdit.Logic.SpeechToText.ClassicSpeechOptions{Variant="cpu",Language=language,UseVad=true},wave,folder,null,CancellationToken.None,null).GetAwaiter().GetResult();var joined=string.Join(" ",text.Paragraphs.Select(p=>p.Text));File.WriteAllText(Path.Combine(folder,"recognized.txt"),joined);Console.WriteLine(joined);Check(joined.Length>10,"independent ASR recognizes generated speech");
 }
 static void Pack(string folder)
 {
  var pack=VoicePackCatalog.All.First();var zip=Path.Combine(folder,"voices.zip");ClassicVoiceAssets.EnsureFileAsync(pack.Url,zip,pack.Bytes,pack.Hash,false,CancellationToken.None,null).GetAwaiter().GetResult();var voices=ClassicVoiceLibrary.ImportPackAsync(zip,true,CancellationToken.None).GetAwaiter().GetResult();Check(voices.Count==pack.Count,"downloaded stable English voice pack imports every reference");Check(voices.All(v=>File.Exists(Path.Combine(ClassicQwenReference.Folder,v.Id+".txt"))),"downloaded references carry transcripts");
 }
 static void PerLine(string folder,string video)
 {
  var source=new Subtitle();source.Paragraphs.Add(new Paragraph("Hello! This is Alice speaking clearly.",0,3500));var target=new Paragraph("Привет! Сейчас мы проверяем клонирование голоса из видео.",0,4500);
  using(var f=new TextToSpeech(source,new SubRip(),video,new VideoInfo{TotalSeconds=4,TotalMilliseconds=4000}))
  {
   Show(f);Set(f,"_perLineOriginal",source);Set(f,"_waveFolder",folder);var task=(Task<string>)Call(f,"PerLineReferenceAsync",target,CancellationToken.None);Pump(task);var voice=task.Result;Check(!string.IsNullOrEmpty(voice),"per-line reference extracted from matching original subtitle");
   Check(File.ReadAllText(Path.Combine(ClassicQwenReference.Folder,voice+".txt")).Contains("Alice") && !File.ReadAllText(Path.Combine(ClassicQwenReference.Folder,voice+".txt")).Contains("клонирование"),"reference transcript uses original speech rather than translation");
   try{var settings=ClassicQwenTtsSettings.Load();settings.Clone=true;settings.Design=false;settings.Language="ru";using(var engine=new ClassicQwenTts(settings)){var start=engine.StartAsync(CancellationToken.None);Pump(start);var speaking=engine.SpeakAsync(target.Text,voice,Path.Combine(folder,"cloned-ru.wav"),CancellationToken.None);Pump(speaking);}Check(Seconds(Path.Combine(folder,"cloned-ru.wav"))>1,"native per-line English reference to Russian clone synthesis");}
   finally{Call(f,"CleanupPerLineReferences");Check(!File.Exists(Path.Combine(ClassicQwenReference.Folder,voice+".wav")),"temporary reference removed without touching user voices");f.Dispose();}
  }
 }
 static void Diarize(string folder,string video,string language){var result=ClassicVideoVoices.FindAsync(video,0,language,"cpu",folder,CancellationToken.None,null).GetAwaiter().GetResult();Check(result.Paragraphs.Any(p=>!string.IsNullOrEmpty(p.Actor)),"native ASR and Sortformer speaker detection");Console.WriteLine("Detected actors: "+string.Join(", ",result.Paragraphs.Select(p=>p.Actor).Distinct()));File.WriteAllText(Path.Combine(folder,"diarization.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result.Paragraphs));File.WriteAllText(Path.Combine(folder,"diarization.srt"),new SubRip().ToText(result,""));}
 static void Pump(Task task){while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(20);}task.GetAwaiter().GetResult();}
 static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();}
 static void Save(Form f,string path){using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(path);}}
 static void Modal(Form owner,string name,string folder,string file){bool found=false;using(var timer=new System.Windows.Forms.Timer{Interval=250}){timer.Tick+=(s,e)=>{var f=Application.OpenForms.Cast<Form>().FirstOrDefault(w=>w!=owner);if(f==null)return;timer.Stop();found=true;f.ShowInTaskbar=false;f.Location=new Point(-20000,-20000);f.Size=f.MinimumSize.Width>0 ? f.MinimumSize : f.Size;Application.DoEvents();Save(f,Path.Combine(folder,file));f.DialogResult=DialogResult.Cancel;f.Close();};timer.Start();Call(owner,name);}Check(found,"classic cast dialog opens");}
 static double Seconds(string path){using(var r=new BinaryReader(File.OpenRead(path))){r.ReadBytes(12);int rate=0;long length=0;while(r.BaseStream.Position+8<=r.BaseStream.Length){var name=System.Text.Encoding.ASCII.GetString(r.ReadBytes(4));int size=r.ReadInt32();if(name=="fmt "){var bytes=r.ReadBytes(size);rate=BitConverter.ToInt32(bytes,8);}else if(name=="data"){length=size;break;}else r.BaseStream.Position+=size+(size%2);}return rate>0 ? length/(double)rate : 0;}}
 static string Q(string s)=>Nikse.SubtitleEdit.Logic.SpeechToText.ClassicSpeechRunner.Quote(s);
 static void Run(string args){using(var p=Process.Start(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,"-hide_banner -loglevel error "+args){UseShellExecute=false,CreateNoWindow=true})){if(!p.WaitForExit(30000)||p.ExitCode!=0)throw new Exception("FFmpeg fixture failed");}}
}
