using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Forms.AudioToText;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestSelectedSpeech
{
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Field(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    static void Pump(Task task){var timeout=DateTime.UtcNow.AddMinutes(4);while(!task.IsCompleted){if(DateTime.UtcNow>timeout)throw new TimeoutException();Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();}
    static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());Application.DoEvents();}
    [STAThread] static int Main(string[] args)
    {
        try{
            if(args.Length>0 && args[0]=="--backend")return Fake(args);
            var data=Path.GetFullPath(args[1]);Directory.CreateDirectory(data);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
            typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,data+Path.DirectorySeparatorChar);
            Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[2];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.EnableVisualStyles();
            if(args[0]=="unit")Unit();
            Ui(data,args[0]=="native",args);return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void Unit()
    {
        var a=new Paragraph("Original A",1000,2000){Actor="Actor A",Extra="Style A"};var b=new Paragraph("Original B",3000,5000){Actor="Actor B"};
        var job=new ClassicSelectedSpeech(new[]{Tuple.Create(a,"a.wav"),Tuple.Create(b,"b.wav")});int calls=0;
        Pump(job.RunAsync((row,cancel)=>{calls++;if(row.Source==b)throw new IOException("Engine failed");return Task.FromResult("Recognized A");},false,CancellationToken.None));
        Check(calls==2 && job.Rows[0].State==SelectedSpeechState.Complete && job.Rows[1].State==SelectedSpeechState.Failed && a.Text=="Original A" && b.Text=="Original B","partial failure retains results without touching source cues");
        Pump(job.RunAsync((row,cancel)=>{calls++;return Task.FromResult("Recognized B");},true,CancellationToken.None));
        Check(calls==3 && job.Rows.All(r=>r.State==SelectedSpeechState.Complete),"retry excludes successful rows");
        Check(job.Apply()==2 && a.Text=="Recognized A" && b.Text=="Recognized B" && a.StartTime.TotalMilliseconds==1000 && a.EndTime.TotalMilliseconds==2000 && a.Actor=="Actor A" && a.Extra=="Style A","apply changes text only, preserving timing actor style and identity");
        var empty=new ClassicSelectedSpeech(new[]{Tuple.Create(b,"b.wav")});Pump(empty.RunAsync((row,cancel)=>Task.FromResult("  "),false,CancellationToken.None));Check(empty.Rows[0].State==SelectedSpeechState.Failed && empty.Apply()==0 && b.Text=="Recognized B","empty transcript never erases existing text");
        var interrupted=new ClassicSelectedSpeech(new[]{Tuple.Create(a,"a.wav"),Tuple.Create(b,"b.wav")});using(var cts=new CancellationTokenSource()){
            try{Pump(interrupted.RunAsync((row,cancel)=>{if(row.Source==b){cts.Cancel();cancel.ThrowIfCancellationRequested();}return Task.FromResult("Completed before stop");},false,cts.Token));}catch(OperationCanceledException){}
            Check(interrupted.Rows[0].State==SelectedSpeechState.Complete && interrupted.Rows[1].State==SelectedSpeechState.Pending && !interrupted.Running,"cancellation preserves completed lines and leaves interrupted clip retryable");
        }
    }
    static void Ui(string data,bool native,string[] args)
    {
        var engine=ClassicSpeechCatalog.Get(ClassicSpeechCatalog.Crisp,"parakeet");var model=engine.Models.First(m=>m.Name=="parakeet-tdt-0.6b-v3-q8_0.gguf");
        var options=new ClassicSpeechOptions{Engine=ClassicSpeechCatalog.Crisp,Backend="parakeet",Language="en",Variant=native?"cuda13":"cpu",Model=model.Name,UseVad=false,RemoveRepeats=false,RemoveNonSpeech=false};options.LastModels[engine.Name]=model.Name;options.Save();
        var first=Path.Combine(data,"first.wav");var second=Path.Combine(data,"second.wav");
        if(native)File.Copy(args[3],first,true);else{
            WriteWave(first,1);WriteWave(second,2);Environment.SetEnvironmentVariable("CLASSIC_SELECTED_TEST_ROOT",data);
            var exe=ClassicSpeechCatalog.Executable(engine,"cpu");Directory.CreateDirectory(Path.GetDirectoryName(exe));
            File.Copy(Assembly.GetExecutingAssembly().Location,exe,true);File.Copy(Assembly.GetExecutingAssembly().Location+".config",exe+".config",true);
            foreach(var file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory,"*.dll").Concat(new[]{Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SubtitleEdit.exe")}))File.Copy(file,Path.Combine(Path.GetDirectoryName(exe),Path.GetFileName(file)),true);
            var modelFile=ClassicSpeechCatalog.ModelPath(engine,model);Directory.CreateDirectory(Path.GetDirectoryName(modelFile));using(var f=File.Create(modelFile))f.SetLength(1000001);
        }
        var a=new Paragraph("Original A",10000,12500){Actor="A",Extra="Style A"};var b=new Paragraph("Original B",20000,22500){Actor="B"};
        var clips=new List<AudioClipsGet.AudioClip>{new AudioClipsGet.AudioClip{Paragraph=a,AudioFileName=first},new AudioClipsGet.AudioClip{Paragraph=b,AudioFileName=second}};
        using(var parent=new Form())using(var form=new WhisperAudioToText(first,new Subtitle(),0,parent,null)){
            Show(parent);form.ConfigureSelectedSpeech(clips);Show(form);
            var choice=(NikseComboBox)Field(form,"comboBoxWhisperEngine");Check(choice.Items.Contains(ClassicSpeechCatalog.Crisp) && choice.Items.Contains(ClassicSpeechCatalog.Qwen) && choice.Items.Contains(ClassicSpeechCatalog.CppVulkan),"selected-lines window includes modern engines alongside legacy Whisper");
            ((CheckBox)Field(form,"checkBoxUsePostProcessing")).Checked=false;
            Pump((Task)typeof(WhisperAudioToText).GetMethod("GenerateSelectedSpeech",Flags).Invoke(form,null));
            Check(form.SelectedSpeech.Rows[0].State==SelectedSpeechState.Complete && form.SelectedSpeech.Rows[1].State==SelectedSpeechState.Failed && a.Text=="Original A" && b.Text=="Original B","real form keeps successful transcript pending while failed cue remains original");
            var english=form.SelectedSpeech.Rows[0].Text;Console.WriteLine("English: "+english);
            if(native){File.Copy(args[4],second,true);((ClassicSpeechOptions)Field(form,"_modernOptions")).Language="ru";var languages=(NikseComboBox)Field(form,"comboBoxLanguages");languages.SelectedItem=engine.Languages.First(l=>l.Code=="ru");}
            else File.WriteAllText(Path.Combine(data,"allow-second"),"yes");
            typeof(WhisperAudioToText).GetField("_retrySelectedOnly",Flags).SetValue(form,true);
            Pump((Task)typeof(WhisperAudioToText).GetMethod("GenerateSelectedSpeech",Flags).Invoke(form,null));
            Check(form.SelectedSpeech.Rows.All(r=>r.State==SelectedSpeechState.Complete) && form.SelectedSpeech.Rows[0].Text==english,"form retries only unsuccessful clips and retains earlier result");
            var russian=form.SelectedSpeech.Rows[1].Text;Console.WriteLine("Second result: "+russian);
            if(native)Check(russian.Any(c=>c>='А'&&c<='я') && english.Any(c=>c>='a'&&c<='z'),"native Russian and English clips produce readable text");
            else Check(File.ReadAllLines(Path.Combine(data,"calls")).Length==3,"engine process is called twice initially and once on retry");
            foreach(var size in new[]{form.Size,form.MinimumSize,new Size(1100,780)}){
                form.Size=size;Application.DoEvents();var area=form.Controls.Find("selectedSpeechArea",true).Single();var footer=form.Controls.Find("selectedSpeechFooter",true).Single();
                Check(area.Bottom<=form.ClientSize.Height && area.Top>=((Control)Field(form,"buttonAdvanced")).Bottom,"selected results stay between settings and window edge at "+size);
                Check(footer.Controls.Cast<Control>().All(c=>c.Right<=footer.ClientSize.Width && c.Bottom<=footer.ClientSize.Height),"selected-lines apply/retry/cancel controls fit without clipping");
                var status=(Control)Field(form,"labelProgress");Check(status.Height>=status.Font.Height && status.Top>=0 && status.Bottom<=status.Parent.ClientSize.Height,"selected-ASR progress text is fully visible");
            }
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(data,"selected-asr.png"));}
            typeof(WhisperAudioToText).GetMethod("buttonBatchMode_Click",Flags).Invoke(form,new object[]{null,null});
            Check(a.Text==english && b.Text==russian && a.StartTime.TotalMilliseconds==10000 && b.EndTime.TotalMilliseconds==22500 && a.Actor=="A" && a.Extra=="Style A","apply through classic button preserves original times and metadata");
            form.Close();parent.Close();
        }
        using(var parent=new Form())using(var form=new WhisperAudioToText(first,new Subtitle(),0,parent,null)){
            Show(parent);var longCue=new Paragraph("Long clip keeps its existing times",10000,30000);form.ConfigureSelectedSpeech(new List<AudioClipsGet.AudioClip>{new AudioClipsGet.AudioClip{Paragraph=longCue,AudioFileName=first}});Show(form);
            var menu=(ContextMenuStrip)Field(form,"contextMenuStripWhisperAdvanced");var split=menu.Items.Cast<ToolStripItem>().Single(i=>i.Name=="selectedSpeechSplitClip");
            Check(form.SelectedSpeech.Rows[0].Source.EndTime.TotalMilliseconds==30000,"long selected clip defaults to preserving its existing cue");
            ((ToolStripMenuItem)split).PerformClick();Check(form.DialogResult==DialogResult.Retry && longCue.Text=="Long clip keeps its existing times","explicit split command returns to the existing segmentation workflow without applying pending text");form.Close();parent.Close();
        }
        var jobs=Path.Combine(ClassicSpeechCatalog.Root,"Jobs");Check(!Directory.Exists(jobs) || !Directory.GetDirectories(jobs,"selected-*").Any(),"owned selected-ASR job folders are cleaned");
    }
    static int Fake(string[] args)
    {
        var root=Environment.GetEnvironmentVariable("CLASSIC_SELECTED_TEST_ROOT");File.AppendAllText(Path.Combine(root,"calls"),"call\n");
        var input=args[Array.IndexOf(args,"-f")+1];var output=args[Array.IndexOf(args,"--output-file")+1];
        if(new FileInfo(input).Length>50000 && !File.Exists(Path.Combine(root,"allow-second")))return 2;
        File.WriteAllText(output+".srt","1\n00:00:00,000 --> 00:00:00,800\nRecognized test speech.\n");return 0;
    }
    static void WriteWave(string file,int seconds){using(var w=new BinaryWriter(File.Create(file))){w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+seconds*32000);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(16000);w.Write(32000);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(seconds*32000);w.Write(new byte[seconds*32000]);}}
}
