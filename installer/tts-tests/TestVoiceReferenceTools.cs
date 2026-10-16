using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms.AudioToText;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.VideoPlayers;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestVoiceReferenceTools
{
 static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
 static object Field(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 static void Pump(Task task){var end=DateTime.UtcNow.AddSeconds(40);while(!task.IsCompleted){if(DateTime.UtcNow>end)throw new TimeoutException();Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();}
 static void Show(Form form){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());Application.DoEvents();}
 static void SetTranscript(WhisperAudioToText form,string value){var result=new Subtitle();result.Paragraphs.Add(new Paragraph(value,0,1000));form.GetType().GetField("<TranscribedSubtitle>k__BackingField",Flags).SetValue(form,result);form.DialogResult=DialogResult.OK;}
 [STAThread] static int Main(string[] args){try{
  var data=Path.GetFullPath(args[0]);Directory.CreateDirectory(data);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,data+Path.DirectorySeparatorChar);Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[1];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.EnableVisualStyles();File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"libmpv-2.dll"),Path.Combine(data,"libmpv-2.dll"),true);
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true")Configuration.Settings.General.MpvExtraOptions="--ao=null --vo=null";
  new ClassicSpeechOptions{Engine=ClassicSpeechCatalog.Crisp,Backend="parakeet",Language="en",Variant="cpu",UseVad=false}.Save();
  var wave=Path.Combine(data,"source.wav");WriteWave(wave);var importTask=ClassicQwenReference.ImportAsync(wave,"Synthetic reference one","Original transcript",0,3,true,CancellationToken.None);Pump(importTask);var first=importTask.Result;var other=ClassicQwenReference.ImportAsync(wave,"Synthetic reference two","Second transcript",0,2,true,CancellationToken.None);Pump(other);
  var job=Path.Combine(data,"range");var rangeTask=ClassicReferenceAudio.ExtractAsync(wave,1,1.5,job,CancellationToken.None);Pump(rangeTask);Check(Math.Abs(ClassicVoiceAssets.WaveSeconds(rangeTask.Result)-1.5)<.01,"ASR range extraction uses only selected reference interval");
  using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{Pump(ClassicReferenceAudio.ExtractAsync(wave,0,2,job,cancelled.Token));throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
  Check(Directory.GetFiles(job).Length==1,"cancelled range extraction leaves no partial clip");
  var words=new Subtitle();words.Paragraphs.Add(new Paragraph("First\nline",0,1000));words.Paragraphs.Add(new Paragraph("Second",1000,2000));Check(ClassicReferenceAudio.Transcript(words)=="First line Second","ASR segments flatten into exact editable reference text");
  using(var manager=new ClassicVoiceManager()){
   Show(manager);Pump(manager.ReferencePreviewTask);var voices=(ListBox)Field(manager,"_voices");voices.ClearSelected();voices.SelectedItem=voices.Items.Cast<ClassicQwenReference>().First(v=>v.Id==first.Id);Pump(manager.ReferencePreviewTask);
   var view=(ClassicSpeechWaveform)Field(manager,"_referenceWave");Console.WriteLine("PREVIEW peaks="+(view.Peaks!=null)+" duration="+view.AudioSeconds+" label="+view.Text);Check(view.Peaks!=null && Math.Abs(view.AudioSeconds-3)<.01 && !view.AllowTimingEdit,"manager shows sample waveform without editable subtitle timing");
   for(int i=0;i<10;i++)voices.SelectedIndex=i%2;voices.ClearSelected();voices.SelectedItem=voices.Items.Cast<ClassicQwenReference>().First(v=>v.Id==first.Id);Pump(manager.ReferencePreviewTask);Check(Math.Abs(view.AudioSeconds-3)<.01,"rapid voice changes discard stale waveform results");
   var player=(LibMpvDynamic)Field(manager,"_referenceMpv");Check(player!=null,"reference playback initializes real MPV");player.Volume=0;typeof(ClassicVoiceManager).GetMethod("PlayReference",Flags).Invoke(manager,new object[]{1.0});Pump(Task.Delay(450));Check(player.CurrentPosition>=.9 && !player.IsPaused,"reference playback seeks into real audio and advances");typeof(ClassicVoiceManager).GetMethod("StopReferencePreview",Flags).Invoke(manager,null);Check(player.IsPaused,"stop pauses reference playback");
   var original=File.ReadAllText(Path.Combine(ClassicQwenReference.Folder,first.Id+".txt"));Check(manager.ApplyReferenceTranscript("Recognized editable words"),"transcription fills editor");Check(File.ReadAllText(Path.Combine(ClassicQwenReference.Folder,first.Id+".txt"))==original,"recognized text is not saved before explicit Save");Check(!manager.ApplyReferenceTranscript("  "),"empty recognition does not erase transcript");
   bool recognized=false;using(var timer=new System.Windows.Forms.Timer{Interval=20}){timer.Tick+=(s,e)=>{var form=Application.OpenForms.OfType<WhisperAudioToText>().FirstOrDefault();if(form==null)return;timer.Stop();Check((int)Field(form,"_audioTrackNumber")==0 && (string)Field(form,"_videoFileName")==Path.Combine(ClassicQwenReference.Folder,first.Id+".wav"),"manager ASR opens current reference on audio stream zero");recognized=true;SetTranscript(form,"ASR recognized words");};timer.Start();((Button)Field(manager,"_transcribeReference")).PerformClick();}
   Check(recognized && ((TextBox)Field(manager,"_transcript")).Text=="ASR recognized words" && File.ReadAllText(Path.Combine(ClassicQwenReference.Folder,first.Id+".txt"))==original,"ASR dialog result is staged without overwriting saved text");
   foreach(var size in new[]{manager.Size,manager.MinimumSize,new Size(1100,800)}){manager.Size=size;Application.DoEvents();var transcript=(Control)Field(manager,"_transcript");var area=manager.Controls.Find("referenceWaveArea",true).Single();var toolbar=(Control)Field(manager,"_toolbar");Check(transcript.Bottom<=area.Top && area.Height>=150,"waveform fits below transcript at "+size);Check(toolbar.Controls.Cast<Control>().All(c=>c.Bottom<=toolbar.ClientSize.Height && c.Right<=toolbar.ClientSize.Width),"all voice-manager toolbar buttons fit after wrapping");}
   using(var image=new Bitmap(manager.Width,manager.Height)){manager.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(data,"voice-manager-wave.png"));}
   voices.SelectedIndex=1-voices.SelectedIndex;manager.Close();Pump(manager.ReferencePreviewTask);Check(File.Exists(Path.Combine(ClassicQwenReference.Folder,first.Id+".wav")),"closing manager during waveform loading preserves voice references");
  }
  using(var form=new ClassicQwenReferenceImport(false)){
   Show(form);((TextBox)form.Controls.Find("textReferencePath",true).Single()).Text=wave;((NumericUpDown)form.Controls.Find("referenceStart",true).Single()).Value=1;((NumericUpDown)form.Controls.Find("referenceDuration",true).Single()).Value=1.5m;
   var transcript=(TextBox)form.Controls.Find("textTranscript",true).Single();transcript.Text="Existing text";var permission=(CheckBox)form.Controls.Find("referenceConsent",true).Single();permission.Checked=false;bool recognized=false;
   using(var timer=new System.Windows.Forms.Timer{Interval=20}){timer.Tick+=(s,e)=>{var asr=Application.OpenForms.OfType<WhisperAudioToText>().FirstOrDefault();if(asr==null)return;timer.Stop();var file=(string)Field(asr,"_videoFileName");Check(Math.Abs(ClassicVoiceAssets.WaveSeconds(file)-1.5)<.01 && (int)Field(asr,"_audioTrackNumber")==0,"import ASR receives only selected clip with correct audio stream");recognized=true;SetTranscript(asr,"Selected range words");};timer.Start();((Button)form.Controls.Find("buttonTranscribeImportReference",true).Single()).PerformClick();var end=DateTime.UtcNow.AddSeconds(40);while(!recognized || form.GetType().GetField("_operation",Flags).GetValue(form)!=null){if(DateTime.UtcNow>end)throw new TimeoutException("Reference ASR import did not complete");Application.DoEvents();Thread.Sleep(5);}}
   Check(transcript.Text=="Selected range words" && !permission.Checked && form.Reference==null,"import ASR stages transcript and never bypasses voice permission or imports automatically");
   var button=form.Controls.Find("buttonTranscribeImportReference",true).Single();Check(button.Bottom<=button.Parent.ClientSize.Height && button.Right<=button.Parent.ClientSize.Width,"import ASR button fits classic source toolbar");form.Close();
  }
  return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
 static void WriteWave(string path){using(var w=new BinaryWriter(File.Create(path))){int count=16000*5;w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(16000);w.Write(32000);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);for(int i=0;i<count;i++)w.Write((short)(Math.Sin(2*Math.PI*440*i/16000)*12000));}}
}
