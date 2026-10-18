using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Forms;
using Nikse.SubtitleEdit.Core.Settings;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using Beautify = Nikse.SubtitleEdit.Forms.BeautifyTimeCodes.BeautifyTimeCodes;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicBeautifyPreview
{
 static BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static object Call(object o,string n,params object[] a){try{return o.GetType().GetMethod(n,F).Invoke(o,a);}catch(TargetInvocationException e){throw e.InnerException;}}
 static void Check(bool b,string m){if(!b)throw new Exception(m);Console.WriteLine("PASS: "+m);}
 static string Json(object o)=>JsonConvert.SerializeObject(o);
 static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();}
 static async Task Wait(Func<bool> done){for(int i=0;i<600;i++){if(done())return;await Task.Delay(20);}throw new Exception("Timed out waiting for preview");}
 static async Task Ready(Beautify f)=>await Wait(()=>f.FixedSubtitle!=null && Field(f,"_previewOperation")==null);
 static Subtitle Sample(){var s=new Subtitle{Header="[Script Info]"};s.Paragraphs.Add(new Paragraph("<i>First</i>",1015,2985){Number=5,Actor="A",Extra="StyleA"});s.Paragraphs.Add(new Paragraph("Second",7017,8993){Number=8,Actor="B",Extra="StyleB"});return s;}
 [STAThread]static int Main(string[] args)
 {
  try{
   var root=Path.Combine(Path.GetFullPath(args[0]),"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Console.WriteLine("TEST DATA: "+root);typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);
   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();int result=0;
   using(var anchor=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),Size=new Size(1,1)}){anchor.Shown+=async(s,e)=>{try{await Unit(root,args.Length>1?args[1]:null);}catch(Exception ex){Console.Error.WriteLine(ex);result=1;}finally{anchor.Close();}};Application.Run(anchor);}return result;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
 static async Task Unit(string root,string ffmpeg)
 {
  Configuration.Settings.General.CurrentFrameRate=25;Configuration.Settings.BeautifyTimeCodes.AlignTimeCodes=true;Configuration.Settings.BeautifyTimeCodes.ExtractExactTimeCodes=false;Configuration.Settings.BeautifyTimeCodes.SnapToShotChanges=false;
  Configuration.Settings.BeautifyTimeCodes.Profile=new BeautifyTimeCodesSettings.BeautifyTimeCodesProfile(BeautifyTimeCodesSettings.BeautifyTimeCodesProfile.Preset.Default);
  foreach(var language in new[]{"ru-RU","en-US"})
  {
   Configuration.Settings.General.Language=language;LanguageSettings.Current=language=="en-US"?new Language():Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages",language+".xml"));
   var source=Sample();var original=Json(source);var expected=new Subtitle(source,false);new TimeCodesBeautifier(expected,25,new List<double>(),new List<double>()).Beautify();
   using(var form=new Beautify(source,null,null,new List<double>()))
   {
    Show(form);await Ready(form);Check(Json(form.FixedSubtitle)==Json(expected),"Live preview uses the actual beautifier result: "+language);
    Check(Json(source)==original && form.FixedSubtitle.Paragraphs[0].Actor=="A" && form.FixedSubtitle.Paragraphs[1].Extra=="StyleB","Preview preserves editor input, actors, styles and header");
    var changed=(List<int>)Field(form,"_previewChanges");Check(changed.Count==2,"Navigator indexes changed cues only");
    var before=(AudioVisualizer)Field(form,"_beforeWave");var after=(AudioVisualizer)Field(form,"_afterWave");Check(!before.ShowWaveform && !after.ShowWaveform && ((Label)Field(form,"_audioStatus")).Text.Length>0,"No-audio preview is explicitly labeled as a cue timeline");
    before.Locked=false;after.Locked=false;Check(before.Locked && after.Locked,"Read-only timelines cannot be unlocked");
    Call(form,"NavigateChange",1);Check((int)Field(form,"_changeAt")==1 && ((Label)Field(form,"_previewPosition")).Text.Contains("#8"),"Navigator retains original cue numbers for selected subsets");
    Check(((Label)Field(form,"_previewDetail")).Text.Contains("ms") && ((Label)Field(form,"_previewDetail")).Text.Contains("→"),"Detail reports old/new times, milliseconds and frames");
    Check(((Label)Field(form,"_previewReason")).Text.Length>20,"Changed cue includes inferred reason");
    Check(after.EndPositionSeconds-after.StartPositionSeconds<12,"Navigator focuses a useful local time span without audio");
    after.ZoomIn();Check(Math.Abs(after.ZoomFactor-before.ZoomFactor)<.000001,"Waveform zoom is synchronized");
    Call(form,"ProcessCmdKey",Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),Keys.Home);Check((int)Field(form,"_changeAt")==0,"Home navigates to the first change");
    Call(form,"ProcessCmdKey",Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),Keys.End);Check((int)Field(form,"_changeAt")==1,"End navigates to the last change");
    using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(0,0,form.Width,form.Height));bmp.Save(Path.Combine(root,"beautify-preview-"+language+".png"));}
    form.Size=form.MinimumSize;await Task.Delay(50);var apply=(Button)Field(form,"buttonOK");Check(apply.Visible && apply.Parent.Height>=apply.Height && apply.Parent.RectangleToScreen(apply.Parent.ClientRectangle).Bottom<=form.RectangleToScreen(form.ClientRectangle).Bottom,"Apply footer remains fully visible at minimum size");
    var cached=new WavePeakData(10,Enumerable.Range(0,120).Select(i=>new WavePeak((short)500,(short)-500)).ToArray());Call(form,"SetPreviewPeaks",cached);Check(before.WavePeaks==cached && after.WavePeaks==cached && before.ShowWaveform,"Both previews reuse the same actual audio peaks");
    var snap=(CheckBox)Field(form,"checkBoxSnapToShotChanges");snap.Checked=true;await Task.Delay(250);Check(form.FixedSubtitle==null && !apply.Enabled,"Unavailable selected shot changes invalidate preview and block Apply");Check(!((Label)Field(form,"_previewPosition")).Text.Contains(language=="ru-RU"?"Изменений нет":"No changes"),"Blocked preview never reports zero changes");snap.Checked=false;await Ready(form);
    Configuration.Settings.BeautifyTimeCodes.Profile.Gap=6;Call(form,"QueueLivePreview");Check(!apply.Enabled,"Changing profile marks previous preview stale immediately");await Ready(form);
    var accepted=Json(form.FixedSubtitle);Call(form,"buttonOK_Click",form,EventArgs.Empty);Check(form.DialogResult==DialogResult.OK && Json(form.FixedSubtitle)==accepted && Json(source)==original,"Apply returns precisely the preview without mutating the source");
   }
   var stable=new Subtitle();stable.Paragraphs.Add(new Paragraph("Stable cue",1000,2000));
   using(var form=new Beautify(stable,null,null,new List<double>())){Show(form);await Ready(form);Check(((List<int>)Field(form,"_previewChanges")).Count==0 && ((Label)Field(form,"_previewPosition")).Text.Contains(language=="ru-RU"?"Изменений нет":"No changes"),"A valid unchanged result still reports no changes");}
   using(var form=new Beautify(source,null,null,new List<double>())){Show(form);await Ready(form);form.DialogResult=DialogResult.Cancel;form.Close();Check(Json(source)==original,"Cancel discards timing proposals");}
  }
  var many=new Subtitle();for(int i=0;i<10000;i++)many.Paragraphs.Add(new Paragraph("Test",i*3000+15,i*3000+1987));var manyOriginal=Json(many);
  using(var form=new Beautify(many,null,null,new List<double>()))
  {
   Show(form);var task=(Task)Call(form,"RecalculatePreviewAsync");form.Close();await task;await Wait(()=>form.IsDisposed);Check(Json(many)==manyOriginal,"Closing cancels background preview and preserves a large source");
  }
  if(File.Exists(ffmpeg))
  {
   Configuration.Settings.General.FFmpegLocation=ffmpeg;var media=Path.Combine(root,"two-audio-tracks.mkv");
   var info=new ProcessStartInfo(ffmpeg,"-nostdin -y -f lavfi -i anullsrc=r=16000:cl=mono -f lavfi -i sine=frequency=440:sample_rate=16000:duration=3 -map 0:a -map 1:a -t 3 -c:a pcm_s16le "+ClassicSpeechRunner.Quote(media)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
   using(var process=Process.Start(info)){var error=process.StandardError.ReadToEndAsync();await Task.Run(()=>process.WaitForExit());Check(process.ExitCode==0,"Synthetic two-track media is generated locally");await error;}
   using(var form=new Beautify(Sample(),new VideoInfo{FramesPerSecond=25,TotalMilliseconds=3000},media,new List<double>(),null,1))
   {
    Show(form);await Ready(form);await (Task)Call(form,"LoadPreviewAudioAsync");var wave=(AudioVisualizer)Field(form,"_afterWave");Check(wave.ShowWaveform && wave.WavePeaks.HighestPeak>100,"Waveform extraction uses selected second audio track instead of silent first track");Check(Field(form,"_waveOperation")==null,"Waveform job releases its operation state");
    form.Close();
   }
  }
 }
}
