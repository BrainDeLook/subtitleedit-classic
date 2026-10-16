using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using Nikse.SubtitleEdit.Logic.VideoPlayers;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicAudioSelection
{
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static void Until(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(60);while(!done()){if(DateTime.UtcNow>end)throw new TimeoutException();Application.DoEvents();Thread.Sleep(10);}}
 static void Pump(Task task){Until(()=>task.IsCompleted);task.GetAwaiter().GetResult();}
 [STAThread]static int Main(){try{
  var data=Environment.GetEnvironmentVariable("CLASSIC_AUDIO_TEST_DATA");var source=Environment.GetEnvironmentVariable("CLASSIC_AUDIO_TEST_SOURCE");var ffmpeg=Environment.GetEnvironmentVariable("CLASSIC_AUDIO_TEST_FFMPEG");Directory.CreateDirectory(data);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,data+Path.DirectorySeparatorChar);Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=ffmpeg;Configuration.Settings.General.VideoPlayer="MPV";Configuration.Settings.General.WaveformAutoGenWhenOpeningVideo=true;Configuration.Settings.General.UseFFmpegForWaveExtraction=true;Configuration.Settings.VideoControls.GenerateSpectrogram=false;LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.EnableVisualStyles();File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"libmpv-2.dll"),Path.Combine(data,"libmpv-2.dll"),true);
  var labels=ClassicAudioTrackLabels.Read(source);Check(labels.Count==2 && labels[0].Language=="tur" && labels[1].Language=="eng","reported MKV contains Turkish then English audio");
  using(var player=new LibMpvDynamic()){
   bool ready=false;player.Initialize(null,source,(s,e)=>ready=true,null);var early=player.AudioTracks;Until(()=>ready);Check(player.AudioTracks.Count==2,"early MPV track query does not cache an empty list");player.Volume=0;player.AudioTrackNumber=1;Until(()=>player.AudioTrackNumber==1);Console.WriteLine("MPV tracks: "+string.Join(", ",player.AudioTracks.Select(t=>t.ToString())));Check(player.AudioTrackNumber==1,"real MPV selects English by ordinal despite native track IDs");player.AudioTrackNumber=0;Until(()=>player.AudioTrackNumber==0);Check(player.AudioTrackNumber==0,"real MPV switches back to Turkish");
  }
  var clip=Path.Combine(data,"two-tracks.mkv");Pump(ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(ffmpeg,"-nostdin -y -i "+ClassicSpeechRunner.Quote(source)+" -t 8 -map 0:v:0 -map 0:a -c copy "+ClassicSpeechRunner.Quote(clip)),CancellationToken.None,null));
  using(var main=new Nikse.SubtitleEdit.Forms.Main()){
   main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-20000,-20000);main.Show();Application.DoEvents();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
   var subtitle=(Subtitle)Field(main,"_subtitle");subtitle.Paragraphs.Add(new Paragraph("Existing subtitle",1000,3000));var original=subtitle.Paragraphs[0].Text;
   bool prompted=false;using(var timer=new System.Windows.Forms.Timer{Interval=20}){timer.Tick+=(s,e)=>{var chooser=Application.OpenForms.OfType<ChooseAudioTrack>().FirstOrDefault();if(chooser==null)return;timer.Stop();prompted=true;((ListBox)Field(chooser,"listBoxTracks")).SelectedIndex=1;((Button)Field(chooser,"buttonOK")).PerformClick();};timer.Start();typeof(Nikse.SubtitleEdit.Forms.Main).GetMethod("OpenVideo",F,null,new[]{typeof(string),typeof(int)},null).Invoke(main,new object[]{clip,-1});}
   var media=Field(main,"mediaPlayer");Until(()=>media.GetType().GetProperty("VideoPlayer").GetValue(media) is LibMpvDynamic p && p.AudioTracks.Count==2 && p.AudioTrackNumber==1);Pump((Task)Field(main,"_classicWaveformTask"));Check(prompted && main.VideoAudioTrackNumber==1,"opening video with existing subtitles prompts and applies English");Check(subtitle.Paragraphs[0].Text==original,"opening audio selection preserves existing subtitles");Check(File.Exists(WavePeakGenerator.GetPeakWaveFileName(clip,1)),"automatic waveform cache belongs to English track");
   var item=new ToolStripMenuItem{Tag="0"};typeof(Nikse.SubtitleEdit.Forms.Main).GetMethod("ChooseAudioTrack",F).Invoke(main,new object[]{item,EventArgs.Empty});Pump((Task)Field(main,"_classicWaveformTask"));Check(main.VideoAudioTrackNumber==0 && ((LibMpvDynamic)media.GetType().GetProperty("VideoPlayer").GetValue(media)).AudioTrackNumber==0,"menu ordinal switches both player and selected waveform track");Check(File.Exists(WavePeakGenerator.GetPeakWaveFileName(clip,0)),"switching to uncached track builds its own waveform");
   item.Tag="1";typeof(Nikse.SubtitleEdit.Forms.Main).GetMethod("ChooseAudioTrack",F).Invoke(main,new object[]{item,EventArgs.Empty});item.Tag="0";typeof(Nikse.SubtitleEdit.Forms.Main).GetMethod("ChooseAudioTrack",F).Invoke(main,new object[]{item,EventArgs.Empty});Pump((Task)Field(main,"_classicWaveformTask"));Check(main.VideoAudioTrackNumber==0,"rapid audio switching retains latest requested track");
   bool cancelled=false;using(var timer=new System.Windows.Forms.Timer{Interval=20}){timer.Tick+=(s,e)=>{var chooser=Application.OpenForms.OfType<ChooseAudioTrack>().FirstOrDefault();if(chooser==null)return;timer.Stop();cancelled=true;chooser.DialogResult=DialogResult.Cancel;};timer.Start();typeof(Nikse.SubtitleEdit.Forms.Main).GetMethod("OpenVideo",F,null,new[]{typeof(string),typeof(int)},null).Invoke(main,new object[]{source,-1});}Check(cancelled && (string)Field(main,"_videoFileName")==clip && subtitle.Paragraphs[0].Text==original,"cancelled audio selection retains current video and subtitles");
   ((LibMpvDynamic)media.GetType().GetProperty("VideoPlayer").GetValue(media)).Dispose();
  }
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
