using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.AudioToText;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms.AudioToText;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using Nikse.SubtitleEdit.Logic;
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

internal static class TestClassicSpeech
{
    private static BindingFlags Flags = BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    private static object Field(object obj,string name) => obj.GetType().GetField(name,Flags).GetValue(obj);
    private static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    private static void Expect<T>(Action action) where T:Exception { try{action();}catch(T){return;} throw new Exception("Expected "+typeof(T).Name); }
    [STAThread]
    private static void Main(string[] args)
    {
        Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
        Configuration.Settings.General.Language="ru-RU";
        LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages","ru-RU.xml"));
        Application.EnableVisualStyles();
        Directory.CreateDirectory(args[1]);
        if(args[0]=="unit") { Unit(args[1]); return; }
        if(args[0]=="clean-install") { CleanInstall(args[1]); return; }
        if(args[0]=="ui") { Ui(args[1]); return; }
        if(args[0]=="ui-run") { UiRun(args[1]); return; }
        if(args[0]=="ui-fixture") { UiFixture(args[1]); return; }
        if(args[0]=="real") { Real(args.Skip(1).ToArray()); return; }
        if(args[0]=="installed-cache")
        {
            typeof(Configuration).GetField("DataDirectory",BindingFlags.Static|BindingFlags.Public).SetValue(null,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Subtitle Edit") + Path.DirectorySeparatorChar);
            Real(args.Skip(1).ToArray()); return;
        }
        if(args[0]=="prepare") { Prepare(args[1]); return; }
        if(args[0]=="segmentation") { Segmentation(args[1]); return; }
        throw new Exception("Unknown test mode");
    }
    private static void Unit(string directory)
    {
        Check(ClassicSpeechCatalog.Engines.Count==17,"Stable catalog count");
        foreach(var catalogEngine in ClassicSpeechCatalog.Engines)
        {
            Check(catalogEngine.Models.Count>0 && catalogEngine.Languages.Count>0,"Missing models/languages "+catalogEngine.Name);
            foreach(var catalogModel in catalogEngine.Models)
            {
                Check(catalogModel.Name==Path.GetFileName(catalogModel.Name),"Unsafe model name");
                Check(catalogModel.Urls.All(url=>new Uri(url).Scheme=="https"),"Invalid model URL");
            }
        }
        var srt=ClassicSpeechRunner.ParseSrt("\uFEFF1\n00:00:01,250 --> 00:00:03,000\nРусский текст\n\n2\n00:00:04,000 --> 00:00:06,100\nEnglish\n");
        Check(srt.Paragraphs.Count==2 && srt.Paragraphs[0].StartTime.TotalMilliseconds==1250,"SRT timestamp/Unicode parsing");
        var json=ClassicSpeechRunner.ParseWordJson("{\"words\":[{\"word\":\"Привет,\",\"start\":0.2,\"end\":0.7},{\"word\":\"мир!\",\"start\":0.8,\"end\":1.3},{\"word\":\"Hello.\",\"start\":2.0,\"end\":2.4}]}");
        Check(json.Paragraphs.Count==2 && json.Paragraphs[0].Text=="Привет, мир!" && json.Paragraphs[0].EndTime.TotalMilliseconds==1300,"Sentence-aware aligned word parsing");
        Expect<InvalidDataException>(()=>ClassicSpeechRunner.ParseWordJson("{\"words\":[{\"word\":\"x\",\"start\":-1,\"end\":1}]}"));
        Expect<InvalidDataException>(()=>ClassicSpeechRunner.ParseWordJson("{\"text\":\"untimed\"}"));
        Expect<InvalidDataException>(()=>ClassicSpeechRunner.ParseWordJson("{\"words\":[{\"word\":\"x\",\"start\":\"NaN\",\"end\":1}]}"));
        Expect<InvalidDataException>(()=>ClassicSpeechDownload.SafeArchivePath(directory,"../outside"));
        Check(SpeechToTextQualityReport.IsNonSpeechLine("[Музыка] (смех)"),"Sound-only detection");
        Check(!SpeechToTextQualityReport.IsNonSpeechLine("[Даниил] Привет"),"Speaker text must be kept");
        Check(SpeechToTextQualityReport.IsRepeatOf("Hello, world!","<i>HELLO WORLD</i>"),"Repeated-line normalization");
        var defects=new Subtitle(); defects.Paragraphs.Add(new Paragraph("A",0,200)); defects.Paragraphs.Add(new Paragraph("[Music]",100,5000));
        defects.Paragraphs.Add(new Paragraph("[Music]",6000,16000));
        var report=new SpeechToTextQualityReport(); report.Analyze(defects,1000,8000,25);
        Check(report.Count(SpeechToTextQualityIssueType.Overlap)==1 && report.Count(SpeechToTextQualityIssueType.NonSpeech)==2 && report.Count(SpeechToTextQualityIssueType.TooLong)>0,"Quality analysis");
        var engine=ClassicSpeechCatalog.Engines.First(e=>e.Backend=="gigaam"); var model=engine.Models.First(m=>!m.Aligner);
        var options=new ClassicSpeechOptions {Language="ru"};
        var job=Path.Combine(directory,"аргументы с пробелом"); Directory.CreateDirectory(job);
        var mock=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MockSpeechEngine.exe");
        Environment.SetEnvironmentVariable("MOCK_SPEECH_MODE","success");
        var transcript=ClassicSpeechRunner.TranscribeAsync(engine,model,options,"C:\\папка с пробелом\\голос.wav",job,null,CancellationToken.None,null,mock).GetAwaiter().GetResult();
        Check(transcript.Paragraphs.Count==1 && transcript.Paragraphs[0].Text=="Привет, это тест.","UTF8 process result");
        Check(File.ReadAllLines(Path.Combine(job,"transcript.args")).Contains("C:\\папка с пробелом\\голос.wav"),"argv quoting");
        Environment.SetEnvironmentVariable("MOCK_SPEECH_MODE","empty-vad"); options.UseVad=true;
        transcript=ClassicSpeechRunner.TranscribeAsync(engine,model,options,"audio.wav",job,null,CancellationToken.None,null,mock).GetAwaiter().GetResult();
        Check(transcript.Paragraphs.Count==1 && File.ReadAllLines(Path.Combine(job,"transcript.runs")).Contains("vad"),"VAD retry");
        Environment.SetEnvironmentVariable("MOCK_SPEECH_MODE","failure");
        Expect<InvalidOperationException>(()=>ClassicSpeechRunner.TranscribeAsync(engine,model,options,"audio.wav",job,null,CancellationToken.None,null,mock).GetAwaiter().GetResult());
        Environment.SetEnvironmentVariable("MOCK_SPEECH_MODE","hang"); options.UseVad=false;
        using(var cancel=new CancellationTokenSource())
        {
            var task=ClassicSpeechRunner.TranscribeAsync(engine,model,options,"audio.wav",job,null,cancel.Token,null,mock);
            var wait=Stopwatch.StartNew(); while(!File.Exists(Path.Combine(job,"transcript.child")) && wait.ElapsedMilliseconds<5000) Thread.Sleep(20);
            Check(File.Exists(Path.Combine(job,"transcript.child")),"Fixture child did not start");
            var childId=int.Parse(File.ReadAllText(Path.Combine(job,"transcript.child"))); cancel.Cancel();
            Expect<OperationCanceledException>(()=>task.GetAwaiter().GetResult());
            try{using(var child=Process.GetProcessById(childId)) Check(child.HasExited,"Cancellation left a child process");}catch(ArgumentException){}
        }
        Environment.SetEnvironmentVariable("MOCK_SPEECH_MODE",null);
        Console.WriteLine("PASS: catalog, parsers, quality, Unicode argv, failures, VAD retry, cancellation and child-process cleanup");
    }
    private static void CleanInstall(string directory)
    {
        var engine=ClassicSpeechCatalog.Get(ClassicSpeechCatalog.Crisp,"parakeet");
        var model=engine.Models.First(m=>m.Name=="parakeet-tdt_ctc-110m-q4_k.gguf");
        Check(!File.Exists(ClassicSpeechCatalog.Executable(engine,"cpu")),"Clean test unexpectedly has an engine already");
        Check(!File.Exists(ClassicSpeechCatalog.ModelPath(engine,model)),"Clean test unexpectedly has a model already");
        ClassicSpeechDownload.EnsureRuntimeAsync(engine,"cpu",CancellationToken.None,null).GetAwaiter().GetResult();
        ClassicSpeechDownload.EnsureModelAsync(engine,model,CancellationToken.None,null).GetAwaiter().GetResult();
        var result=ClassicSpeechRunner.TranscribeAsync(engine,model,new ClassicSpeechOptions{Variant="cpu",Language="en"},
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"english-sample.wav"),directory,null,CancellationToken.None,null).GetAwaiter().GetResult();
        Check(result.Paragraphs.Count>0,"Downloaded engine/model returned no transcript");
        Console.WriteLine("PASS: fresh CPU runtime/model download, SHA256 validation, extraction and real transcription");
    }
    private static void UiRun(string directory)
    {
        Configuration.Settings.General.SubtitleMinimumDisplayMilliseconds=0;
        Configuration.Settings.General.SubtitleMaximumDisplayMilliseconds=60000;
        Configuration.Settings.General.SubtitleMaximumCharactersPerSeconds=1000;
        var audio=Path.Combine(directory,"..","speech-engine-tests","audio","english-zira.wav");
        using(var parent=new Form())
        using(var form=new WhisperAudioToText(audio,new Subtitle(),0,parent,null))
        {
            form.Shown-=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),form,"AudioToText_Shown");
            form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-20000,-20000); form.Show();
            ((NikseComboBox)Field(form,"comboBoxWhisperEngine")).SelectedItem=ClassicSpeechCatalog.Crisp;
            var backend=(ComboBox)Field(form,"_modernBackend"); backend.SelectedItem=backend.Items.Cast<SpeechEngine>().First(e=>e.Backend=="parakeet");
            var models=(NikseComboBox)Field(form,"comboBoxModels"); models.SelectedItem=models.Items.Cast<SpeechModel>().First(m=>m.Name=="parakeet-tdt-0.6b-v3-q4_k.gguf");
            var languages=(NikseComboBox)Field(form,"comboBoxLanguages"); languages.SelectedItem=languages.Items.Cast<SpeechLanguage>().First(l=>l.Code=="en");
            ((CheckBox)Field(form,"checkBoxUsePostProcessing")).Checked=false;
            var options=(ClassicSpeechOptions)Field(form,"_modernOptions"); options.Variant="vulkan"; options.Aligner=""; options.UseVad=false;
            var task=(Task)form.GetType().GetMethod("GenerateModernSpeech",Flags).Invoke(form,null);
            var watch=Stopwatch.StartNew(); var pumps=0;
            while(!task.IsCompleted && watch.Elapsed.TotalSeconds<120){Application.DoEvents(); Thread.Sleep(10); pumps++;}
            Check(task.IsCompleted,"UI transcription timeout"); task.GetAwaiter().GetResult();
            Check(form.DialogResult==DialogResult.OK && form.TranscribedSubtitle.Paragraphs.Count>0,"UI did not return transcription");
            Check(pumps>0 && Field(form,"_modernCancellation")==null,"UI loop/operation cleanup failed");
            File.WriteAllText(Path.Combine(directory,"ui-result.srt"),new Nikse.SubtitleEdit.Core.SubtitleFormats.SubRip().ToText(form.TranscribedSubtitle,null));
            Console.WriteLine("PASS: real transcription through classic form, responsive message loop, result and cleanup");
        }
    }
    private static void UiFixture(string directory)
    {
        Configuration.Settings.General.SubtitleMinimumDisplayMilliseconds=0;
        Configuration.Settings.General.SubtitleMaximumDisplayMilliseconds=60000;
        Configuration.Settings.General.SubtitleMaximumCharactersPerSeconds=1000;
        var engine=ClassicSpeechCatalog.Get(ClassicSpeechCatalog.Crisp,"parakeet");
        var exe=ClassicSpeechCatalog.Executable(engine,"vulkan"); var backup=exe+".fixture-backup";
        Check(!File.Exists(backup),"Fixture backup already exists"); File.Move(exe,backup);
        try
        {
            File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MockSpeechEngine.exe"),exe);
            foreach(var batch in new[]{false,true})
            {
                var audio=Path.Combine(directory,batch ? "batch.wav" : "cancel.wav");
                File.Copy(Path.Combine(directory,"..","speech-engine-tests","audio","english-zira.wav"),audio,true);
                var protectedOutput=Path.Combine(directory,"batch.asr.parakeet.srt");
                if(batch) File.WriteAllText(protectedOutput,"KEEP EXISTING SUBTITLE");
                using(var parent=new Form())
                using(var form=new WhisperAudioToText(batch ? "" : audio,new Subtitle(),0,parent,null))
                using(var timer=new System.Windows.Forms.Timer{Interval=100})
                {
                    form.Shown-=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),form,"AudioToText_Shown");
                    form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-20000,-20000); form.Show();
                    ((NikseComboBox)Field(form,"comboBoxWhisperEngine")).SelectedItem=ClassicSpeechCatalog.Crisp;
                    var backend=(ComboBox)Field(form,"_modernBackend"); backend.SelectedItem=backend.Items.Cast<SpeechEngine>().First(e=>e.Backend=="parakeet");
                    var models=(NikseComboBox)Field(form,"comboBoxModels"); models.SelectedItem=models.Items.Cast<SpeechModel>().First(m=>m.Name=="parakeet-tdt-0.6b-v3-q4_k.gguf");
                    ((CheckBox)Field(form,"checkBoxUsePostProcessing")).Checked=false;
                    var options=(ClassicSpeechOptions)Field(form,"_modernOptions"); options.Variant="vulkan"; options.Aligner=""; options.UseVad=false;
                    if(batch) ((ListView)Field(form,"listViewInputFiles")).Items.Add(audio);
                    Environment.SetEnvironmentVariable("MOCK_SPEECH_MODE",batch ? "success" : "hang");
                    var watch=Stopwatch.StartNew(); bool cancelled=false;
                    timer.Tick+=(s,e)=>
                    {
                        if(!batch && watch.ElapsedMilliseconds>1500 && !cancelled) { form.GetType().GetMethod("CancelModernSpeech",Flags).Invoke(form,null); cancelled=true; }
                        if(batch) foreach(Form dialog in Application.OpenForms.Cast<Form>().ToArray())
                            if(dialog!=form && dialog!=parent) { dialog.DialogResult=DialogResult.OK; dialog.Close(); }
                    };
                    timer.Start();
                    var task=(Task)form.GetType().GetMethod("GenerateModernSpeech",Flags).Invoke(form,null);
                    while(!task.IsCompleted && watch.Elapsed.TotalSeconds<30){Application.DoEvents(); Thread.Sleep(10);}
                    timer.Stop(); Check(task.IsCompleted,"Fixture UI operation did not finish"); task.GetAwaiter().GetResult();
                    Check(Field(form,"_modernCancellation")==null && ((Control)Field(form,"buttonGenerate")).Enabled,"UI operation did not restore controls");
                    if(batch)
                    {
                        Check(File.ReadAllText(protectedOutput)=="KEEP EXISTING SUBTITLE","Batch overwrote existing subtitles");
                        Check(Directory.GetFiles(directory,"batch.asr.parakeet.*.srt").Length>0,"Batch did not create a unique SRT");
                    }
                    else Check(cancelled && form.DialogResult!=DialogResult.OK,"Cancellation returned a successful result");
                }
            }
            Console.WriteLine("PASS: cancellation through classic form; batch output preserves existing subtitles and restores controls");
        }
        finally { Environment.SetEnvironmentVariable("MOCK_SPEECH_MODE",null); File.Delete(exe); File.Move(backup,exe); }
    }
    private static void Ui(string directory)
    {
        Configuration.Settings.Tools.WhisperChoice=WhisperChoice.Cpp;
        using(var parent=new Form())
        using(var form=new WhisperAudioToText("test.wav",new Subtitle(),0,parent,null))
        {
            form.Shown-=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),form,"AudioToText_Shown");
            form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-20000,-20000); form.Show();
            var engines=(NikseComboBox)Field(form,"comboBoxWhisperEngine");
            foreach(var engine in new[]{ClassicSpeechCatalog.Crisp,ClassicSpeechCatalog.Qwen,ClassicSpeechCatalog.CppVulkan,ClassicSpeechCatalog.WhisperX})
            {
                engines.SelectedItem=engine;
                foreach(var width in new[]{800,1050})
                {
                    form.ClientSize=new Size(width,form.ClientSize.Height); Application.DoEvents();
                    var models=(Control)Field(form,"groupBoxModels"); var backend=(Control)Field(form,"_modernBackend");
                    var status=(Control)Field(form,"_modernStatus"); var generate=(Control)Field(form,"buttonGenerate");
                    var quality=(Control)Field(form,"_qualityButton"); var advanced=(Control)Field(form,"buttonAdvanced");
                    Check(status.Right<=models.Width && backend.Bottom<models.Height,"Backend row clipped");
                    Check(quality.Left>advanced.Right && generate.Bottom<=form.ClientSize.Height,"Buttons clipped");
                    var progress=(Control)Field(form,"labelProgress"); var elapsed=(Control)Field(form,"labelElapsed");
                    Check(progress.Right<elapsed.Left,"Footer text overlaps");
                    using(var bitmap=new Bitmap(form.Width,form.Height))
                    {form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(directory,engine.Replace(' ','_')+"-"+width+".png"));}
                }
                var effective=ClassicSpeechCatalog.Get(engine,"gigaam");
                using(var settings=new ClassicSpeechSettings(effective,new ClassicSpeechOptions()))
                {
                    settings.StartPosition=FormStartPosition.Manual; settings.Location=new Point(-20000,-20000); settings.Show();
                    using(var bitmap=new Bitmap(settings.Width,settings.Height)) {settings.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(directory,"settings-"+engine.Replace(' ','_')+".png"));}
                }
            }
            Console.WriteLine("PASS: all added engines rendered at two widths; footer, backend row and buttons checked");
        }
    }
    private static void Prepare(string downloads)
    {
        foreach(var pair in new[]{new[]{ClassicSpeechCatalog.Crisp,"gigaam","vulkan","crisp-vulkan.zip"},new[]{ClassicSpeechCatalog.Crisp,"gigaam","cpu","crisp-cpu.zip"},
            new[]{ClassicSpeechCatalog.Crisp,"gigaam","cpu-legacy","crisp-cpu-legacy.zip"},new[]{ClassicSpeechCatalog.Crisp,"gigaam","cuda13","crisp-cuda13.zip"},
            new[]{ClassicSpeechCatalog.Qwen,"","cpu","qwen-cpu.zip"},new[]{ClassicSpeechCatalog.Qwen,"","vulkan","qwen-vulkan.zip"},
            new[]{ClassicSpeechCatalog.CppVulkan,"","vulkan","whisper-vulkan.zip"},new[]{ClassicSpeechCatalog.WhisperX,"","cpu","whisperx.7z"}})
        {
            var engine=ClassicSpeechCatalog.Get(pair[0],pair[1]);
            ClassicSpeechDownload.VerifyRuntimeArchive(ClassicSpeechCatalog.RuntimeUrl(engine,pair[2]),Path.Combine(downloads,pair[3]));
            ClassicSpeechDownload.Extract(Path.Combine(downloads,pair[3]),ClassicSpeechCatalog.EngineDirectory(engine,pair[2]),CancellationToken.None);
            Check(File.Exists(ClassicSpeechCatalog.Executable(engine,pair[2])),"Missing executable "+pair[0]);
        }
        foreach(var engine in ClassicSpeechCatalog.Engines.Concat(new[]{ClassicSpeechCatalog.Get(ClassicSpeechCatalog.CppVulkan,"")}))
            foreach(var model in engine.Models)
            {
                var source=Path.Combine(downloads,model.Name);
                if(!File.Exists(source)) continue;
                var destination=ClassicSpeechCatalog.ModelPath(engine,model); Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source,destination,true);
            }
        Console.WriteLine("PASS: production archive extractor and executable discovery for all added engine families");
    }
    private static void Real(string[] args)
    {
        var engine=ClassicSpeechCatalog.Get(args[1],args[2]);
        var model=engine.Models.First(m=>m.Name==args[3]);
        var options=new ClassicSpeechOptions {Variant=args[4],Language=args[5]};
        if(args.Length>8) { options.UseVad=args[8]=="vad"; options.WhisperXAlignWords=args[8]!="no-align"; }
        var aligner=args.Length>7 ? args[7] : null;
        var watch=Stopwatch.StartNew();
        var result=ClassicSpeechRunner.TranscribeAsync(engine,model,options,args[6],args[0],aligner,CancellationToken.None,null).GetAwaiter().GetResult();
        Check(result.Paragraphs.Count>0,"Empty live transcript");
        File.WriteAllText(Path.Combine(args[0],"result.srt"),new Nikse.SubtitleEdit.Core.SubtitleFormats.SubRip().ToText(result,null));
        Console.WriteLine("PASS "+engine.Name+" "+options.Variant+" "+options.Language+" "+watch.Elapsed.TotalSeconds.ToString("F2")+"s: "+string.Join(" | ",result.Paragraphs.Select(p=>p.Text)));
    }
    private static void Segmentation(string output)
    {
        typeof(Configuration).GetField("DataDirectory",BindingFlags.Static|BindingFlags.Public).SetValue(null,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Subtitle Edit") + Path.DirectorySeparatorChar);
        Configuration.Settings.General.SubtitleLineMaximumLength=42;
        Configuration.Settings.General.MaxNumberOfLines=2;
        Configuration.Settings.Tools.AudioToTextLineMaxChars=86;
        var engine=ClassicSpeechCatalog.Get(ClassicSpeechCatalog.Crisp,"parakeet");
        var model=engine.Models.First(m=>m.Name=="parakeet-tdt-0.6b-v3-q8_0.gguf");
        var options=new ClassicSpeechOptions{Variant="cuda13",Language="en",UseVad=false};
        var wave=Path.Combine(output,"..","speech-segmentation","tyson-120.wav");
        var result=ClassicSpeechRunner.TranscribeAsync(engine,model,options,wave,output,null,CancellationToken.None,null).GetAwaiter().GetResult();
        Check(result.Paragraphs.Count>=20,"Engine still emits transcription chunks instead of subtitle cues");
        Check(result.Paragraphs.All(p=>p.Duration.TotalMilliseconds<20000),"Engine cue spans too much continuous speech");
        var rawWords=string.Join(" ",result.Paragraphs.Select(p=>p.Text)).Split(new[]{' ','\r','\n'},StringSplitOptions.RemoveEmptyEntries).Length;
        var post=new AudioToTextPostProcessor("en");
        result=post.Fix(AudioToTextPostProcessor.Engine.Whisper,result,true,false,true,false,true,true);
        Check(result.Paragraphs.Count>=20 && result.Paragraphs.All(p=>p.Duration.TotalMilliseconds<20000),"Postprocessing merged cues back into chunks");
        var postWords=string.Join(" ",result.Paragraphs.Select(p=>p.Text)).Split(new[]{' ','\r','\n'},StringSplitOptions.RemoveEmptyEntries).Length;
        Check(rawWords==postWords,"Postprocessing lost or duplicated recognised words");
        File.WriteAllText(Path.Combine(output,"segmented.srt"),new Nikse.SubtitleEdit.Core.SubtitleFormats.SubRip().ToText(result,null));
        Console.WriteLine("PASS: 120s real source without VAD; "+result.Paragraphs.Count+" subtitle cues after postprocessing; max duration "+result.Paragraphs.Max(p=>p.Duration.TotalSeconds).ToString("F2")+"s; words preserved");
    }
}
