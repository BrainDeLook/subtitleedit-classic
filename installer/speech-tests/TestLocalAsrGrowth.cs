using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.AudioToText;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms.AudioToText;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestLocalAsrGrowth
{
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static void Pump(Task task){var end=DateTime.UtcNow.AddMinutes(15);while(!task.IsCompleted){if(DateTime.UtcNow>end)throw new TimeoutException();Application.DoEvents();Thread.Sleep(10);}task.GetAwaiter().GetResult();}
 [STAThread]static int Main(string[] args){try{
  var root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[2];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.EnableVisualStyles();if(args[0]=="anime"){Anime(root,args);return 0;}if(args[0]=="native"){Native(root,args);return 0;}
  var engines=ClassicSpeechCatalog.Engines.Where(e=>e.Family=="crisp").ToList();Check(engines.Count==17,"stable catalog contains seventeen Crisp backends");
  var options=new ClassicSpeechOptions{Engine=ClassicSpeechCatalog.Crisp,Backend="parakeet",Language="en",UseVad=false};options.Save();
  using(var parent=new Form())using(var form=new WhisperAudioToText("",new Subtitle(),0,parent,null)){
   form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();var backends=(ComboBox)form.GetType().GetField("_modernBackend",F).GetValue(form);Check(backends.Items.Count==engines.Count,"every stable Crisp backend is selectable in classic ASR");
   foreach(var engine in engines){backends.SelectedItem=engine;Application.DoEvents();var model=engine.Models.First(m=>!m.Aligner);var lang=engine.Languages.First(l=>l.Code!="auto").Code;var arguments=ClassicSpeechRunner.Arguments(engine,model,new ClassicSpeechOptions{Language=lang},"input.wav","output",engine.NativeTimings?null:"aligner.gguf",ClassicSpeechExtras.NeedsVad(engine));Check(arguments.Contains(engine.Backend) && arguments.Contains(lang) && arguments.Contains("--output-srt"),"backend command and language: "+engine.Backend);}
   var actual=(ClassicSpeechOptions)form.GetType().GetField("_modernOptions",F).GetValue(form);actual.Aligner="canary-ctc-aligner-q8_0.gguf";actual.Language="ja";backends.SelectedItem=engines.First(e=>e.Backend=="sensevoice");actual.Language="ja";var aligner=(SpeechModel)form.GetType().GetMethod("ModernAligner",F).Invoke(form,new object[]{engines.First(e=>e.Backend=="sensevoice")});Check(aligner!=null && aligner.Name!="canary-ctc-aligner-q8_0.gguf","Japanese cannot retain incompatible Canary aligner from previous backend");
   using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"all-backends.png"));}form.Close();
  }
  Check(ClassicSpeechExtras.NormalizeLanguage("eng_Latn")=="en" && ClassicSpeechExtras.NormalizeLanguage("rus_Cyrl")=="ru","OmniASR script language tags map correctly for alignment");
  Check(ClassicLegacyAsr.LanguageArguments(WhisperChoice.PurfviewFasterWhisperXxl,"multilingual","")=="--multilingual True " && ClassicLegacyAsr.LanguageArguments(WhisperChoice.PurfviewFasterWhisperXxl,"multilingual","--multilingual False")=="","XXL per-segment mode omits fixed language and preserves explicit override");
  Configuration.Settings.Tools.WhisperChoice=WhisperChoice.PurfviewFasterWhisperXxl;Configuration.Settings.Tools.WhisperExtraSettings="";
  Check(ClassicLegacyAsr.LanguageArguments(WhisperChoice.OpenAi,"auto","")=="" && ClassicLegacyAsr.LanguageArguments(WhisperChoice.Cpp,"auto","")=="--language auto ","automatic language uses each legacy engine's accepted syntax");
  Configuration.Settings.Tools.WhisperChoice=WhisperChoice.PurfviewFasterWhisperXxl;Check(ClassicLegacyAsr.Models(new WhisperPurfviewFasterWhisperModel()).Any(m=>m.Name==ClassicLegacyAsr.AnimeName && m.Urls.Any(u=>u.EndsWith("/preprocessor_config.json"))),"anime-whisper download includes required 128-band preprocessor config");
  var modelFolder=Path.Combine(root,"anime-config");Directory.CreateDirectory(modelFolder);var file=Path.Combine(modelFolder,"config.json");File.WriteAllText(file,"{\"alignment_heads\":[[7,2],[25,3]],\"other\":42}");Check(ClassicLegacyAsr.RepairAnimeAlignment(modelFolder) && !ClassicLegacyAsr.RepairAnimeAlignment(modelFolder),"distilled anime alignment repair is idempotent");var json=JObject.Parse(File.ReadAllText(file));Check(((JArray)json["alignment_heads"]).Count==20 && (int)json["other"]==42 && File.Exists(file+".classic-original"),"anime repair keeps unrelated config and backs up original");
  var anime=ClassicSpeechCatalog.Get(ClassicSpeechCatalog.WhisperX,"").Models.Single(m=>m.Name==ClassicLegacyAsr.AnimeName);var animeArgs=ClassicSpeechRunner.Arguments(ClassicSpeechCatalog.Get(ClassicSpeechCatalog.WhisperX,""),anime,new ClassicSpeechOptions{Language="ja",TranslateToEnglish=true},"input.wav","out",null,false);Check(animeArgs.Contains(ClassicLegacyAsr.AnimeFolder) && !animeArgs.Contains("translate"),"WhisperX uses local anime model path and transcription-only task");
  using(var combo=new Nikse.SubtitleEdit.Controls.NikseComboBox()){typeof(WhisperAudioToText).GetMethod("InitializeLanguageNames",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{combo});Check(combo.Items.Contains(ClassicLegacyAsr.MultilingualName) && combo.Items.Contains(ClassicLegacyAsr.AutoName),"legacy XXL exposes auto and per-segment languages");}
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static void Anime(string root,string[] args)
 {
  var engine=ClassicSpeechCatalog.Get(ClassicSpeechCatalog.WhisperX,"");
  var model=engine.Models.Single(m=>m.Name==ClassicLegacyAsr.AnimeName);
  var progress=new Progress<SpeechProgress>(p=>Console.WriteLine(p.Message));
  Pump(ClassicSpeechDownload.EnsureModelAsync(engine,model,CancellationToken.None,progress));
  var job=Path.Combine(root,"anime-ja");Directory.CreateDirectory(job);
  var task=ClassicSpeechRunner.TranscribeAsync(engine,model,new ClassicSpeechOptions{Language="ja",Variant="cpu",WhisperXAlignWords=false},Path.GetFullPath(args[3]),job,null,CancellationToken.None,progress);
  Pump(task);
  var text=string.Join(" ",task.Result.Paragraphs.Select(p=>p.Text));
  Console.WriteLine("anime.ja: "+text);
  File.WriteAllText(Path.Combine(job,"recognized.txt"),text);
  Check(text.Contains("もちろん") && text.Contains("任せ"),"native anime Japanese dialogue matches reference phrases");
 }
 static void Native(string root,string[] args){foreach(var request in new[]{new[]{"canary","en",args[3]},new[]{"canary","ru",args[4]},new[]{"sensevoice","en",args[3]},new[]{"sensevoice","ja",args[5]}}){var engine=ClassicSpeechCatalog.Engines.First(e=>e.Backend==request[0]);var model=engine.Models.First(m=>!m.Aligner && m.Name.Contains("q4_k"));var options=new ClassicSpeechOptions{Engine=ClassicSpeechCatalog.Crisp,Backend=engine.Backend,Language=request[1],Variant="cuda13",UseVad=false};Pump(ClassicSpeechDownload.EnsureRuntimeAsync(engine,options.Variant,CancellationToken.None,null));Pump(ClassicSpeechDownload.EnsureModelAsync(engine,model,CancellationToken.None,null));SpeechModel aligner=null;if(!engine.NativeTimings){aligner=ClassicSpeechExtras.RankedAligners(options.Language).First(a=>ClassicSpeechExtras.SupportsAligner(a,options.Language)).Model;Pump(ClassicSpeechDownload.EnsureModelAsync(engine,aligner,CancellationToken.None,null));}
   var job=Path.Combine(root,engine.Backend+"-"+options.Language);Directory.CreateDirectory(job);var wave=Path.Combine(job,"input.wav");Pump(ClassicSpeechRunner.RunProcessAsync(new System.Diagnostics.ProcessStartInfo(args[2],"-nostdin -y -i "+ClassicSpeechRunner.Quote(request[2])+" -ar 16000 -ac 1 "+ClassicSpeechRunner.Quote(wave)),CancellationToken.None,null));var task=ClassicSpeechRunner.TranscribeAsync(engine,model,options,wave,job,aligner==null?null:ClassicSpeechCatalog.ModelPath(engine,aligner),CancellationToken.None,null);Pump(task);var text=string.Join(" ",task.Result.Paragraphs.Select(p=>p.Text));File.WriteAllText(Path.Combine(job,"recognized.txt"),text);Console.WriteLine(engine.Backend+" "+options.Language+": "+text);Check(task.Result.Paragraphs.Count>0 && text.Any(c=>options.Language=="ru"?c>='А'&&c<='я':options.Language=="ja"?c>='\u3040'&&c<='\u9fff':c>='a'&&c<='z'),"native readable transcription "+engine.Backend+" "+options.Language);}}
}
