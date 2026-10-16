using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.Ocr;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestRemainingNative
{
 static readonly List<object> Results=new List<object>();
 static IProgress<SpeechProgress> Progress()=>new Progress<SpeechProgress>(p=>{if(!string.IsNullOrEmpty(p.Message))Console.WriteLine(p.Message);});
 static async Task Case(string name,Func<CancellationToken,Task> action)
 {
  Console.WriteLine("START "+name);var watch=Stopwatch.StartNew();try{using(var c=new CancellationTokenSource(TimeSpan.FromMinutes(25)))await action(c.Token);Results.Add(new{name,ok=true,seconds=watch.Elapsed.TotalSeconds});Console.WriteLine("PASS "+name);}catch(Exception e){Results.Add(new{name,ok=false,error=e.ToString(),seconds=watch.Elapsed.TotalSeconds});Console.WriteLine("FAIL "+name+": "+e.Message);}
 }
 static int Main(string[] args){var root=Path.GetFullPath(args[1]);try{Directory.CreateDirectory(root);typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);Configuration.Settings.General.FFmpegLocation=args[2];Run(args,root).GetAwaiter().GetResult();return Results.Any(r=>!(bool)r.GetType().GetProperty("ok").GetValue(r))?1:0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{File.WriteAllText(Path.Combine(root,args[0]+"-native-results.json"),JsonConvert.SerializeObject(Results,Formatting.Indented));}}
 static async Task Run(string[] args,string root)
 {
  if(args[0]=="ocr")
  {
   foreach(var model in ClassicBitmapOcr.Models)await Case("OCR "+model.Name,async cancel=>{
    await ClassicBitmapOcr.EnsureAsync(model.Name,cancel,Progress());
    using(var ocr=new ClassicBitmapOcr("crispembed"))using(var image=new Bitmap(1100,170))using(var g=Graphics.FromImage(image))using(var font=new Font("Segoe UI",42))
    {g.Clear(Color.Black);g.DrawString("Hello from Subtitle Edit",font,Brushes.White,25,35);var text=await ocr.Ocr(image,model.Name,"English",cancel);Console.WriteLine(text);File.WriteAllText(Path.Combine(root,model.Name+".txt"),text);if(text.IndexOf("Subtitle Edit",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidDataException("Known English subtitle was not recovered.");}
   });return;
  }
  if(args[0].StartsWith("asr"))
  {
   foreach(var engine in ClassicSpeechCatalog.Engines.Where(e=>e.Family=="crisp"))await Case("ASR "+engine.Backend,async cancel=>{
    var lang=engine.Languages.FirstOrDefault(l=>l.Code=="en"||l.Code=="eng_Latn")??engine.Languages.First(l=>l.Code=="ru"||l.Code=="rus_Cyrl");
    var model=engine.Models.First(m=>!m.Aligner);var options=new ClassicSpeechOptions{Engine=ClassicSpeechCatalog.Crisp,Backend=engine.Backend,Language=lang.Code,Variant="cuda13",UseVad=false,RetryWithoutVad=true};
    await ClassicSpeechDownload.EnsureRuntimeAsync(engine,options.Variant,cancel,Progress());await ClassicSpeechDownload.EnsureModelAsync(engine,model,cancel,Progress());
    SpeechModel aligner=null;if(!engine.NativeTimings){aligner=ClassicSpeechExtras.RankedAligners(lang.Code).First(a=>ClassicSpeechExtras.SupportsAligner(a,lang.Code)).Model;await ClassicSpeechDownload.EnsureModelAsync(engine,aligner,cancel,Progress());}
    if(args[0]=="asr-download"){await ClassicSpeechExtras.EnsureAsync(engine,options,cancel,Progress());return;}
    var job=Path.Combine(root,"asr-"+engine.Backend);Directory.CreateDirectory(job);var input=Path.Combine(job,"input.wav");
    await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(args[2],"-nostdin -y -i "+ClassicSpeechRunner.Quote(args[lang.Code=="ru"||lang.Code=="rus_Cyrl"?4:3])+" -ac 1 -ar 16000 "+ClassicSpeechRunner.Quote(input)),cancel,null);
    var result=await ClassicSpeechRunner.TranscribeAsync(engine,model,options,input,job,aligner==null?null:ClassicSpeechCatalog.ModelPath(engine,aligner),cancel,Progress());
    var text=string.Join(" ",result.Paragraphs.Select(p=>p.Text));Console.WriteLine(text);File.WriteAllText(Path.Combine(job,"recognized.txt"),text);if(result.Paragraphs.Count==0 || text.Count(char.IsLetter)<3)throw new InvalidDataException("No readable transcription.");
   });return;
  }
  var refs=new List<string>();
  try
  {
   foreach(var sample in new[]{args[3],args[4]}){var voice=await ClassicQwenReference.ImportAsync(sample,"Permitted synthetic native test","",0,3,true,CancellationToken.None,false);voice.PerLine=true;File.WriteAllText(Path.Combine(ClassicQwenReference.Folder,voice.Id+".json"),JsonConvert.SerializeObject(voice));refs.Add(voice.Id);}
   foreach(var d in ClassicLocalTts.Catalog.Where(d=>d.SupportsAudioOnlyPerLineClone))await Case("TTS per-line "+d.Id,async cancel=>{
    var s=ClassicLocalTtsSettings.Load(d.Id);s.Variant="cuda13";s.Language="en";
    if(d.RuntimeArchives!=null && !d.RuntimeArchives.Any(a=>a.Variant==s.Variant))s.Variant=d.RuntimeArchives.First().Variant;
    var installed=d.Models.FirstOrDefault(m=>m.Files.All(f=>File.Exists(Path.Combine(ClassicLocalTtsSettings.Root(d.Id),"models",f.File))));s.Model=(installed??d.Models.First()).Key;
    var license=ClassicAudioCpp.License(d.Id);if(license!=null)s.LicenseAccepted=license[0];
    using(var t=new ClassicLocalTts(d.Id,s))for(var i=0;i<refs.Count;i++){var output=Path.Combine(root,d.Id+"-"+i+".wav");await t.SpeakAsync(i==0?"Hello. This is the first cloned voice.":"This is the second cloned voice.",refs[i],output,cancel,Progress());if(ClassicVoiceAssets.WaveSeconds(output)<.4)throw new InvalidDataException("No usable cloned audio.");}
    var asr=ClassicSpeechCatalog.Get(ClassicSpeechCatalog.Crisp,"parakeet");var asrModel=asr.Models.First(m=>!m.Aligner);await ClassicSpeechDownload.EnsureModelAsync(asr,asrModel,cancel,Progress());
    for(var i=0;i<refs.Count;i++){
     var job=Path.Combine(root,"tts-check-"+d.Id+"-"+i);Directory.CreateDirectory(job);var wave=Path.Combine(job,"input.wav");await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(args[2],"-nostdin -y -i "+ClassicSpeechRunner.Quote(Path.Combine(root,d.Id+"-"+i+".wav"))+" -ar 16000 -ac 1 "+ClassicSpeechRunner.Quote(wave)),cancel,null);
     var result=await ClassicSpeechRunner.TranscribeAsync(asr,asrModel,new ClassicSpeechOptions{Language="en",Variant="cuda13",UseVad=false},wave,job,null,cancel,null);var text=string.Join(" ",result.Paragraphs.Select(p=>p.Text));Console.WriteLine("ASR_CHECK "+d.Id+" "+i+": "+text);File.WriteAllText(Path.Combine(job,"recognized.txt"),text);
     if(new[]{"hello","first","second","voice","clone"}.Count(w=>text.IndexOf(w,StringComparison.OrdinalIgnoreCase)>=0)<2)throw new InvalidDataException("Independent ASR did not recover the expected cloned phrase.");
    }
   });
  }finally{foreach(var id in refs){var voice=ClassicQwenReference.List(false).FirstOrDefault(v=>v.Id==id);if(voice!=null)ClassicVoiceLibrary.Delete(voice);}}
 }
}
