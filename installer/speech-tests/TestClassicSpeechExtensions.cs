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
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Forms.AudioToText;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicTiming;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicSpeechExtensions
{
 static readonly BindingFlags F=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
 static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static void Pump(Task task){while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(20);}task.GetAwaiter().GetResult();}
 [STAThread] static int Main(string[] args)
 {
  try{
   var folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
   typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,(args[0]=="unit" ? folder : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)+"/Subtitle Edit")+Path.DirectorySeparatorChar);
   Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[2];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.EnableVisualStyles();
   if(args[0]=="unit")Unit(folder);else if(args[0]=="wave")Wave(folder,Path.GetFullPath(args[3]));else if(args[0]=="align")Align(folder,args).GetAwaiter().GetResult();else if(args[0]=="gen-zh")Chinese(folder).GetAwaiter().GetResult();else Native(folder,args).GetAwaiter().GetResult();
   Application.ExitThread();Environment.Exit(0);return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
 static void Unit(string folder)
 {
  var engine=ClassicSpeechCatalog.Get(ClassicSpeechCatalog.Crisp,"parakeet");var model=engine.Models.First();
  Check(engine.Models.Any(m=>m.Name.StartsWith("parakeet-ultra-")) && engine.Models.Any(m=>m.Name.StartsWith("phonon2-")),"Ultra and Phonon-2 catalog entries");
  Check(ClassicSpeechExtras.Catalog.Aligners.Count==14,"Canary Qwen and twelve language aligners");
  foreach(var vad in ClassicSpeechExtras.VadChoices){var opt=new ClassicSpeechOptions{UseVad=true,Vad=vad,DetectSpeakers=true};var a=ClassicSpeechRunner.Arguments(engine,model,opt,"audio.wav","result",null,true);Check(a.Contains("--diarize-method") && a.Contains("sortformer") && a[a.IndexOf("--vad-model")+1]==ClassicSpeechExtras.VadPath(engine,opt),"typed VAD and speaker arguments: "+vad);}
  var other=ClassicSpeechCatalog.Get(ClassicSpeechCatalog.Qwen,null);var qwen=other.Models.First(m=>!m.Aligner);var args=ClassicSpeechRunner.Arguments(other,qwen,new ClassicSpeechOptions{UseVad=true,DetectSpeakers=true},"audio.wav","result","aligner.gguf",true);Check(!args.Contains("--vad") && !args.Contains("--diarize"),"Crisp-only flags cannot leak into Qwen CPP");
  var echo=ClassicSpeechCatalog.Get(ClassicSpeechCatalog.Crisp,"index-echo");var echoArgs=ClassicSpeechRunner.Arguments(echo,echo.Models[0],new ClassicSpeechOptions{Language="en",Vad="webrtc"},"audio.wav","result",null,true);Check(echoArgs.Contains("--target-lang") && !echoArgs.Contains("-l") && echoArgs[echoArgs.IndexOf("--vad-model")+1].EndsWith("ggml-silero-v6.2.0.bin"),"Index-Echo target language and compulsory Silero");
  Check(echo.Models.All(m=>m.Assets.Length==2) && !echoArgs.Contains("--split-on-punct"),"Index-Echo downloads both files and preserves bilingual cue pairs");
  var translated=ClassicSpeechRunner.IndexEchoTranslation("(speaker 2) 中文\n「Translated text」");Check(translated=="(Speaker 2) Translated text","translation keeps speaker and strips Japanese quote wrapper");
  var lines=new Subtitle();lines.Paragraphs.Add(new Paragraph("Hello",0,1000){Actor="Speaker 1"});lines.Paragraphs.Add(new Paragraph("Hello",1000,2000){Actor="Speaker 2"});var calls=0;var post=ClassicSpeechExtras.ProcessByActor(lines,s=>{calls++;return new Subtitle(s);});Check(calls==2 && post.Paragraphs[1].Actor=="Speaker 2","postprocessing cannot merge different speakers");
  var ass=new AdvancedSubStationAlpha().ToText(post,"");Check(ass.Contains("Speaker 1") && ass.Contains("Speaker 2"),"batch ASS preserves actors");
  var opts=new ClassicSpeechOptions{UseVad=true,Vad="firered",DetectSpeakers=true,IsolateSpeech=true};opts.Save();var restored=ClassicSpeechOptions.Load();Check(restored.Vad=="firered" && restored.DetectSpeakers && restored.IsolateSpeech,"new ASR options persist");
  foreach(var choice in new[]{engine,other})using(var f=new ClassicSpeechSettings(choice,opts)){Show(f);Save(f,Path.Combine(folder,choice.Family+"-settings.png"));Check(f.Controls.Cast<Control>().All(c=>!c.Visible || c.Right<=f.ClientSize.Width && c.Bottom<=f.ClientSize.Height),"settings fit minimum-size "+choice.Family+" form");if(choice.Family!="crisp")Check(!f.Controls.Find("checkDetectSpeakers",true)[0].Enabled && !f.Controls.Find("checkIsolateSpeech",true)[0].Enabled,"non-Crisp extras disabled");f.Close();}
 }
 static void Wave(string folder,string input)
 {
  var source=new Subtitle();source.Paragraphs.Add(new Paragraph("Waveform test",1000,3000));source.Paragraphs.Add(new Paragraph("Unselected cue",4000,5000));
  using(var f=new ClassicImproveTimeCodes(source,input,Math.Max(6,Nikse.SubtitleEdit.Logic.ClassicCast.ClassicVoiceAssets.WaveSeconds(input)),24))
  {
   Show(f);var loaded=(Task)f.GetType().GetMethod("LoadWaveformAsync",F).Invoke(f,null);Pump(loaded);while(Field(f,"_waveOperation")!=null){Application.DoEvents();Thread.Sleep(20);}
   var wave=(AudioVisualizer)Field(f,"_waveAligned");Check(wave.WavePeaks!=null,"actual audio waveform loaded");
   var x=(int)Math.Round((1-wave.StartPositionSeconds)*wave.WavePeaks.SampleRate*wave.ZoomFactor);var y=wave.Height/2;var dragFlags=BindingFlags.Instance|BindingFlags.NonPublic;
   wave.GetType().GetMethod("WaveformMouseDown",dragFlags).Invoke(wave,new object[]{wave,new MouseEventArgs(MouseButtons.Left,1,x,y,0)});
   var step=(int)Math.Round(wave.WavePeaks.SampleRate*wave.ZoomFactor*0.1);
   wave.GetType().GetMethod("WaveformMouseMove",dragFlags).Invoke(wave,new object[]{wave,new MouseEventArgs(MouseButtons.Left,0,x+step,y,0)});
   wave.GetType().GetMethod("WaveformMouseMove",dragFlags).Invoke(wave,new object[]{wave,new MouseEventArgs(MouseButtons.Left,0,x+2*step,y,0)});
   wave.GetType().GetMethod("WaveformMouseUp",dragFlags).Invoke(wave,new object[]{wave,new MouseEventArgs(MouseButtons.Left,1,x+2*step,y,0)});
   var grid=(DataGridView)Field(f,"_grid");var changed=TimeCode.ParseToMilliseconds(Convert.ToString(grid.Rows[0].Cells[3].Value))/1000.0;
   Check(changed>1.15,"repeated mouse moves keep waveform and grid synchronized");
   Check(f.ApplyWaveformEdit(0,1.25,3.25),"manual waveform boundary change accepted");Check(source.Paragraphs[0].StartTime.TotalSeconds==1,"source unchanged before Apply");Check(!f.ApplyWaveformEdit(0,-1,0.5),"invalid waveform range rejected");
   f.Size=f.MinimumSize;Application.DoEvents();Save(f,Path.Combine(folder,"timing-waveform.png"));
   f.GetType().GetMethod("Apply",F).Invoke(f,null);Check(f.EditedSubtitle!=null && Math.Abs(f.EditedSubtitle.Paragraphs[0].StartTime.TotalSeconds-1.25)<0.001 && f.EditedSubtitle.Paragraphs[1].StartTime.TotalSeconds==4,"only selected manual time codes applied");f.Close();
  }
 }
 static async Task Native(string folder,string[] args)
 {
  var engine=ClassicSpeechCatalog.Get(ClassicSpeechCatalog.Crisp,args[3]);var model=engine.Models.First(m=>m.Name==args[4]);var opts=new ClassicSpeechOptions{Variant="cuda13",Language=args[6],UseVad=true,Vad=args[7],DetectSpeakers=args.Length>8 && args[8]=="speakers",IsolateSpeech=args.Length>8 && args[8]=="isolate"};
  await ClassicSpeechDownload.EnsureRuntimeAsync(engine,opts.Variant,CancellationToken.None,null);await ClassicSpeechDownload.EnsureModelAsync(engine,model,CancellationToken.None,null);
  var audio=Path.Combine(folder,"input.wav");await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,"-nostdin -y -i "+ClassicSpeechRunner.Quote(Path.GetFullPath(args[5]))+" -ar 16000 -ac 1 -c:a pcm_s16le "+ClassicSpeechRunner.Quote(audio)),CancellationToken.None,null);
  string alignerPath=null;if(!engine.NativeTimings){var aligner=ClassicSpeechExtras.RankedAligners(opts.Language).First().Model;await ClassicSpeechDownload.EnsureModelAsync(engine,aligner,CancellationToken.None,null);alignerPath=ClassicSpeechCatalog.ModelPath(engine,aligner);}
  var result=await ClassicSpeechRunner.TranscribeAsync(engine,model,opts,audio,folder,alignerPath,CancellationToken.None,null);var joined=string.Join(" ",result.Paragraphs.Select(p=>p.Text));File.WriteAllText(Path.Combine(folder,"recognized.txt"),joined);File.WriteAllText(Path.Combine(folder,"result.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result.Paragraphs));Console.WriteLine(joined);Check(result.Paragraphs.Count>0,"native "+model.Name+" / "+opts.Vad+" / "+opts.Language+" speech");if(opts.DetectSpeakers)Check(result.Paragraphs.Any(p=>!string.IsNullOrEmpty(p.Actor)) && result.Paragraphs.All(p=>!p.Text.StartsWith("(speaker",StringComparison.OrdinalIgnoreCase)),"Sortformer labels become actors");
  if(engine.Backend=="index-echo" && opts.Language=="en")Check(!joined.Any(c=>c>=0x4e00 && c<=0x9fff),"Chinese original removed while native translation time codes are preserved");
 }
 static async Task Align(string folder,string[] args)
 {
  var ru=args.Length>4 && args[4]=="ru";var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph(ru ? "Привет!" : "Hello!",0,900));sub.Paragraphs.Add(new Paragraph(ru ? "Сегодня мы проверяем новый голос." : "Today we are checking a new voice.",1400,3400));sub.Paragraphs.Add(new Paragraph(ru ? "Каждое слово должно звучать ясно и естественно." : "Every word should sound clear and natural.",3700,6000));
  var result=await new ClassicRetimingJob().RunAsync(sub,Path.GetFullPath(args[3]),Nikse.SubtitleEdit.Logic.ClassicCast.ClassicVoiceAssets.WaveSeconds(Path.GetFullPath(args[3])),"cuda13",ru ? "canary-ctc" : "wav2vec2-aligner-en",false,ru ? "ru" : "en",new SubtitleRetimer.Options{MaxShiftSeconds=4,MinDurationSeconds=0.5},CancellationToken.None,null);File.WriteAllText(Path.Combine(folder,"alignment.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result));Check(result.All(r=>r.Status!=SubtitleRetimer.LineStatus.Failed),"native "+(ru ? "Russian Canary" : "English wav2vec2")+" alignment");
  Check(result.Any(r=>SubtitleRetimer.IsMove(r.Status)),"aligner proposes actual boundary corrections");
 }
 static async Task Chinese(string folder)
 {
  var settings=ClassicQwenTtsSettings.Load();settings.Variant="cuda13";settings.Clone=settings.Design=settings.Supertonic=false;settings.Language="zh";settings.Instructions="";
  const string text="你好！今天我们测试新的声音。每个词都应该清晰自然。";
  using(var engine=new ClassicQwenTts(settings)){await engine.StartAsync(CancellationToken.None);await engine.SpeakAsync(text,"vivian",Path.Combine(folder,"chinese.wav"),CancellationToken.None);}File.WriteAllText(Path.Combine(folder,"chinese.txt"),text);Check(true,"synthetic Chinese translation fixture generated");
 }
 static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();}
 static void Save(Form f,string file){using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height));image.Save(file);}}
}
