using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicCast;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestSpeechSession
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Field(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
    static void Pump(Task task){while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();}
    [STAThread] static int Main(string[] args)
    {
        try {
            if(args[0]=="--voice") { FakeEdge(args);return 0; }
            var data=Path.GetFullPath(args[0]);Directory.CreateDirectory(data);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
            typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,data+Path.DirectorySeparatorChar);Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[1];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));
            Application.EnableVisualStyles();Unit(data);return 0;
        } catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void Unit(string data)
    {
        var source=Path.Combine(data,"source");Directory.CreateDirectory(source);var original=Path.Combine(source,"one.wav");var second=Path.Combine(source,"two.wav");WriteWave(original,330);WriteWave(second,440);
        var reference=ClassicQwenReference.ImportAsync(original,"Permitted synthetic session voice","Reference words",0,2,true,CancellationToken.None).GetAwaiter().GetResult();
        var sub=new Subtitle();sub.Header="[Script Info]\r\nPlayResX: 1920\r\nPlayResY: 1080\r\n";
        sub.Paragraphs.Add(new Paragraph("Первая 日本語",1000,6000){Actor="A",Extra="Default",MarginV="40",Forced=true,Bookmark="chapter"});sub.Paragraphs.Add(new Paragraph("Second line",7000,12000){Actor="A",Extra="Default"});sub.Paragraphs.Add(new Paragraph("Unselected original actor",13000,16000){Actor="C"});sub.Paragraphs.Add(new Paragraph("Unselected clone actor",17000,20000){Actor="D"});
        var sounds=new List<TextToSpeech.FileNameAndSpeedFactor>{new TextToSpeech.FileNameAndSpeedFactor{Filename=original,Factor=1.1m},new TextToSpeech.FileNameAndSpeedFactor{Filename=second,Factor=1}};
        var edge=new ClassicOnlineTtsSettings{Executable=Assembly.GetExecutingAssembly().Location,Voice="en-US-GuyNeural"};edge.Credentials.ApiKey="NEVER_EXPORT_THIS_KEY";edge.Save("edge");
        var exportFolder=Path.Combine(data,"export");var json=Path.Combine(exportFolder,"SubtitleEditTts.json");
        using(var form=new TextToSpeech(sub,new AdvancedSubStationAlpha(),null,null)) {
            Show(form);var current=(Subtitle)Field(form,"_subtitle");
            form.RestoreSessionRow(current.Paragraphs[0],new ClassicTtsSessionItem{Text=current.Paragraphs[0].Text,StartMs=1000,EndMs=6000,EngineName="EdgeTts",VoiceName="en-US-GuyNeural",Instruction="First instruction",Actor="A"});
            form.RestoreSessionRow(current.Paragraphs[1],new ClassicTtsSessionItem{Text=current.Paragraphs[1].Text,StartMs=7000,EndMs=12000,EngineName="EdgeTts",VoiceName="ru-RU-DmitryNeural",Instruction="Different instruction",Actor="A"});
            form.RestoreSessionRow(current.Paragraphs[3],new ClassicTtsSessionItem{Text=current.Paragraphs[3].Text,StartMs=17000,EndMs=20000,EngineName="dots.tts (CrispASR)",VoiceName=reference.Id,Actor="D"});
            var sessionJob=Path.Combine(data,"session-job");Directory.CreateDirectory(sessionJob);typeof(TextToSpeech).GetField("_waveFolder",Flags).SetValue(form,sessionJob);
            typeof(TextToSpeech).GetMethod("PreserveSessionReference",Flags).Invoke(form,new object[]{current.Paragraphs[3],reference});ClassicVoiceLibrary.Delete(reference);
            var reviewSub=new Subtitle();reviewSub.Paragraphs.AddRange(current.Paragraphs.Take(2));
            using(var review=new ReviewAudioClips(form,reviewSub,sounds,form.GetParagraphEngine(current.Paragraphs[0]))) {
                Show(review);var list=(ListView)Field(review,"listViewAudioClips");list.Items[1].Checked=false;
                var before=review.SnapshotTake(0);review.RememberTake(0,before);form.RestoreSessionRow(current.Paragraphs[0],new ClassicTtsSessionItem{Text="Changed take",StartMs=1000,EndMs=6000,EngineName="EdgeTts",VoiceName="ru-RU-DmitryNeural"});sounds[0].Filename=second;
                review.RestoreTake(0,before);Check(current.Paragraphs[0].Text=="Первая 日本語" && sounds[0].Filename==original && sounds[0].Factor==1.1m && form.GetParagraphAudio(current.Paragraphs[0])=="en-US-GuyNeural","take history restores text, audio, speed and per-line voice");
                current.Paragraphs[0].Text="Intermediate take two";Pump(form.ReGenerateAudioAsync(current.Paragraphs[0],current.GetParagraphOrDefault(1),"en-US-GuyNeural"));
                current.Paragraphs[0].Text="Intermediate take three";Pump(form.ReGenerateAudioAsync(current.Paragraphs[0],current.GetParagraphOrDefault(1),"ru-RU-DmitryNeural"));
                var history=(Dictionary<int,List<ClassicTtsSessionItem>>)Field(review,"_takeHistory");Check(history[0].Any(t=>t.Text=="Intermediate take two") && history[0].Any(t=>t.Text=="Intermediate take three"),"every regenerated take is retained before the edit dialog is accepted");
                review.RestoreTake(0,before);
                Pump(review.ExportSessionAsync(json,CancellationToken.None));
                var raw=File.ReadAllText(json);Check(!raw.Contains("NEVER_EXPORT_THIS_KEY") && !raw.Contains(source.Replace("\\","\\\\")),"portable session excludes credentials and original clip paths");
                var session=ClassicTtsSession.Load(json);Check(session.Items.Count==4 && session.Items[0].Include && !session.Items[1].Include && !session.Items[2].Include && session.Items[2].Actor=="C" && session.Items[2].AudioFileName=="","full timeline retains unchecked and original-only actors");
                Check(session.Items[0].SpeedFactor==1.1m && session.Items[0].Instruction=="First instruction" && session.Items[1].Instruction=="Different instruction" && session.SubtitleHeader==sub.Header,"timings, ASS metadata and independent same-actor instructions persist");
                Check(File.ReadAllBytes(session.Items[0].AudioFileName).SequenceEqual(File.ReadAllBytes(original)) && File.Exists(session.Items[3].VoiceFileName) && session.Items[3].VoiceTranscript=="Reference words","audio bytes and clone references travel with the session");
                var previous=File.ReadAllText(json);using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{Pump(ClassicTtsSession.ExportAsync(session,json,cancelled.Token));throw new Exception("Cancel ignored");}catch(OperationCanceledException){}}
                Check(File.ReadAllText(json)==previous,"cancel does not replace existing session metadata");
                var missing=ClassicTtsSession.Copy(session);missing.Items[0].AudioFileName=Path.Combine(data,"missing.wav");try{Pump(ClassicTtsSession.ExportAsync(missing,json,CancellationToken.None));throw new Exception("Missing asset ignored");}catch(FileNotFoundException){}
                Check(File.ReadAllText(json)==previous,"failed export preserves previous complete session");
                var asset=session.Items[0].AudioFileName;Pump(ClassicTtsSession.ExportAsync(session,json,CancellationToken.None));Check(File.Exists(asset),"re-export never overwrites or deletes an existing take");
                var bar=review.Controls.Find("sessionReviewToolbar",true).Single();Check(bar.Controls.Cast<Control>().All(c=>c.Bottom<=bar.ClientSize.Height),"review session/history controls fit classic footer");
                using(var image=new Bitmap(review.Width,review.Height)){review.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(data,"review-session.png"));}
                review.Close();
            }
            form.Close();
        }
        var moved=Path.Combine(data,"moved-session");Directory.Move(exportFolder,moved);var movedJson=Path.Combine(moved,"SubtitleEditTts.json");var restored=ClassicTtsSession.Load(movedJson);
        Check(restored.Items[0].AudioFileName.StartsWith(moved,StringComparison.OrdinalIgnoreCase) && File.Exists(restored.Items[3].VoiceFileName),"moving the folder preserves all relative audio/reference paths");
        var attack=JObject.Parse(File.ReadAllText(movedJson));attack["Items"][0]["AudioFileName"]="../source/one.wav";var bad=Path.Combine(moved,"invalid.json");File.WriteAllText(bad,attack.ToString());try{ClassicTtsSession.Load(bad);throw new Exception("Traversal accepted");}catch(InvalidDataException){}Check(true,"relative path traversal rejected before importing assets");
        var legacy=Path.Combine(moved,"legacy.json");File.WriteAllText(legacy,"{\"Items\":[{\"Text\":\"Legacy\",\"StartMs\":1,\"EndMs\":1001,\"AudioFileName\":"+JsonConvert.SerializeObject(((string)JObject.Parse(File.ReadAllText(movedJson))["Items"][0]["AudioFileName"]))+"}]}");Check(ClassicTtsSession.Load(legacy).Items[0].Include,"older stable session defaults remain compatible");
        var permanentTask=ClassicQwenReference.ImportAsync(original,"Existing permanent voice","Permanent words",0,2,true,CancellationToken.None);Pump(permanentTask);var permanent=permanentTask.Result;
        var defaults=new ClassicCastSettings();defaults.Rows.Add(new ClassicCastRow{Actor="Existing actor",Voice=permanent.Id,Engine="Qwen3",Selected=true});defaults.Save();var defaultCast=File.ReadAllText(ClassicCastSettings.PathName);
        using(var form=new TextToSpeech(new Subtitle(),new SubRip(),null,null)) {
            Show(form);Pump(form.LoadSpeechSessionAsync(restored));var loaded=(Subtitle)Field(form,"_subtitle");
            Check(loaded.Paragraphs[0].MarginV=="40" && loaded.Paragraphs[0].Forced && loaded.Paragraphs[0].Bookmark=="chapter","ASS margins and subtitle metadata survive session import");
            Check(loaded.Paragraphs.Count==4 && loaded.Paragraphs[2].Actor=="C" && form.SessionRowEnabled(loaded.Paragraphs[1])==false,"classic import restores cues, actors and include flags");
            Check(form.GetParagraphEngine(loaded.Paragraphs[0]).Id==TextToSpeech.TextToSpeechEngineId.EdgeTts && form.GetParagraphAudio(loaded.Paragraphs[1])=="ru-RU-DmitryNeural","stable engine aliases and per-line voices restore");
            var render=Path.Combine(data,"render");Directory.CreateDirectory(render);typeof(TextToSpeech).GetField("_waveFolder",Flags).SetValue(form,render);
            var files=(List<TextToSpeech.FileNameAndSpeedFactor>)typeof(TextToSpeech).GetMethod("StageSpeechSession",Flags).Invoke(form,null);
            Check(files.Count==4 && files[0]!=null && files[2]==null,"restored audio can be staged without a synthesis engine");
            files[1]=null;
            var merged=(string)typeof(TextToSpeech).GetMethod("MergeAudioParagraphs",Flags).Invoke(form,new object[]{files});
            Check(ClassicVoiceAssets.WaveSeconds(merged)>19,"imported clips assemble to the full timeline without synthesis");
            using(var reader=new BinaryReader(File.OpenRead(merged))){var bytes=File.ReadAllBytes(merged);Check(bytes.Length>24000*2*9,"assembled waveform exists");}
            var generated=form.ReGenerateAudioAsync(loaded.Paragraphs[1],loaded.GetParagraphOrDefault(2),"ru-RU-DmitryNeural");Pump(generated);Check(generated.Result!=null && ClassicVoiceAssets.WaveSeconds(generated.Result.Filename)>1,"imported line regenerates through restored engine and voice");
            var group=(Control)Field(form,"groupBoxSettings");
            Check(new[]{"buttonOnlineTtsSettings","buttonOnlineTtsRefresh"}.All(name=>form.Controls.Find(name,true).Single().Right<=group.ClientSize.Width),"online engine buttons fit after imported actors expand the classic window");
            var bar=form.Controls.Find("classicPlayback",true).Single();Check(bar.Controls.Cast<Control>().All(c=>c.Bottom<=bar.ClientSize.Height),"import control fits classic playback footer");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(data,"import-session.png"));}
            typeof(TextToSpeech).GetMethod("PersistClassicCast",Flags).Invoke(form,null);Check(File.ReadAllText(ClassicCastSettings.PathName)==defaultCast,"session cast never overwrites default actor settings");
            var owned=ClassicQwenReference.List(false).Where(v=>v.Id!=permanent.Id).Select(v=>v.Id).ToArray();Check(owned.Length>0,"imported clone references staged for current session");
            form.Close();Check(!ClassicQwenReference.List(false).Any(v=>owned.Contains(v.Id)),"closing a session cleans imported working references");Check(ClassicQwenReference.List(false).Any(v=>v.Id==permanent.Id),"closing a session preserves existing permanent voices");
        }
    }
    static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());}
    static void FakeEdge(string[] args){var output=args[Array.IndexOf(args,"--write-media")+1];WriteWave(output,args[1].StartsWith("ru-")?440:330);}
    static void WriteWave(string path,int frequency){Directory.CreateDirectory(Path.GetDirectoryName(path));using(var w=new BinaryWriter(File.Create(path))){const int bytes=24000*2*2;w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(bytes+36);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(24000);w.Write(48000);w.Write((short)2);w.Write((short)16);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(bytes);for(int i=0;i<bytes/2;i++)w.Write((short)(Math.Sin(i*2*Math.PI*frequency/24000)*5000));}}
}
