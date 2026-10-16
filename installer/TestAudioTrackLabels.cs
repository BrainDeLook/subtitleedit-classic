using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestAudioTrackLabels
{
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static string Run(string exe,string args,string workingDirectory=null){using(var p=new System.Diagnostics.Process{StartInfo=new System.Diagnostics.ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=workingDirectory??Environment.CurrentDirectory}}){p.Start();var text=p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception(text);return text;}}
 [STAThread] static int Main(string[] args){try{
  var data=Path.GetFullPath(args[0]);Directory.CreateDirectory(data);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,data+Path.DirectorySeparatorChar);Configuration.Settings.General.FFmpegLocation=args[1];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.EnableVisualStyles();
  var parsed=ClassicAudioTrackLabels.Parse("Metadata:\n title : Container title\n Stream #0:0: Video: h264\n Stream #0:1(tur): Audio: eac3, 48000 Hz, 5.1(side)\n Metadata:\n title : Türkçe Оригинал\n Stream #0:2(eng): Subtitle: subrip\n Metadata:\n title : Subtitle title\n Stream #0:3[0x4](eng): Audio: aac, stereo\n Metadata:\n title : English dubbing\n Stream #0:4: Audio: ac3, 5.1\n");
  Check(parsed.Count==3 && parsed[0].Title=="Türkçe Оригинал" && parsed[1].Title=="English dubbing" && parsed[2].Title==null,"parser retains Unicode titles, language and audio order without leaking subtitle/container metadata");
  Check(parsed[0].Display(0,true).Contains("Турецкий [tur]") && parsed[1].Display(1,true).Contains("Английский [eng]") && parsed[2].Display(2,true).Contains("Язык не указан"),"labels identify languages and handle missing tags");
  var video=Path.Combine(data,"two-tracks.mkv");
  Run(args[1],"-nostdin -y -f lavfi -i color=c=blue:s=640x360:r=24:d=2 -f lavfi -i anullsrc=channel_layout=5.1:sample_rate=48000 -t 2 -map 0:v -map 1:a -map 1:a -c:v libx264 -preset ultrafast -c:a ac3 -metadata:s:a:0 language=tur -metadata:s:a:0 title="+ClassicSpeechRunner.Quote("Türkçe Оригинал")+" -metadata:s:a:1 language=eng -metadata:s:a:1 title="+ClassicSpeechRunner.Quote("English dubbing")+" "+ClassicSpeechRunner.Quote(video));
  var sourceFile=args.Length>2?args[2]:video;
  var source=ClassicAudioTrackLabels.Read(sourceFile);Check(source.Count==2 && source[0].Language=="tur" && source[1].Language=="eng","original MKV identifies Turkish and English tracks");
  foreach(var input in new[]{video,sourceFile}.Distinct()){
   var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Test",0,1500));
   using(var form=new GenerateVideoWithHardSubs(sub,new SubRip(),input,new VideoInfo{Width=640,Height=360,TotalSeconds=2,TotalMilliseconds=2000,FramesPerSecond=24,TotalFrames=48,Success=true},30,false)){
    typeof(GenerateVideoWithHardSubs).GetField("_mpvOn",Flags).SetValue(form,false);form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
    var list=(ListView)Field(form,"listViewAudioTracks");Check(list.Items.Count==2 && list.Items[0].Text.Contains("Турецкий") && list.Items[1].Text.Contains("Английский"),"burn-in window displays readable languages");
    if(input==video)Check(list.Items[0].Text.Contains("Türkçe Оригинал") && list.Items[1].Text.Contains("English dubbing"),"burn-in window displays actual track names");
    foreach(var size in new[]{form.Size,form.MinimumSize,new Size(1100,900)}){form.Size=size;Application.DoEvents();var group=(Control)Field(form,"groupBoxAudio");var rate=(Control)Field(form,"comboBoxAudioBitRate");var preview=(Control)Field(form,"videoPlayerContainer1");Check(list.Top>rate.Bottom && list.Bottom<=group.ClientSize.Height && list.Left>=0 && list.Right<=group.ClientSize.Width && group.Bottom<=preview.Top,"full-width track list fits below audio settings and above preview at "+size);}
    list.Items[0].Checked=false;list.Items[1].Checked=true;((Control)Field(form,"comboBoxVideoEncoding")).Text="libx264";((Control)Field(form,"comboBoxPreset")).Text="ultrafast";((Control)Field(form,"comboBoxAudioEnc")).Text="aac";((CheckBox)Field(form,"checkBoxMakeStereo")).Checked=true;
    var ass=Path.Combine(data,"test.ass");File.WriteAllText(ass,new AdvancedSubStationAlpha().ToText(sub,null));var output=Path.Combine(data,"selected.mp4");
    var process=(System.Diagnostics.Process)typeof(GenerateVideoWithHardSubs).GetMethod("GetFfmpegProcess",Flags).Invoke(form,new object[]{input,output,ass,null,null,false});
    Check(process.StartInfo.Arguments.Contains("-map 0:a:1?") && !process.StartInfo.Arguments.Contains("-map 0:a:0?") && process.StartInfo.Arguments.Contains("-ac 2"),"choosing named English track retains correct stream mapping and stereo conversion");
    if(input==video){Run(process.StartInfo.FileName,process.StartInfo.Arguments,process.StartInfo.WorkingDirectory);var tracks=ClassicAudioTrackLabels.Read(output);Check(tracks.Count==1 && tracks[0].Language=="eng" && tracks[0].Details.Contains("stereo"),"actual hard-sub export keeps only English audio in stereo");}
    process.Dispose();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(data,input==video?"named-tracks.png":"user-tracks.png"));}form.Close();
   }
  }return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
