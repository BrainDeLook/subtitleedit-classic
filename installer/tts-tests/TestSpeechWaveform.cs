using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestSpeechWaveform
{
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Field(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    static void Pump(Task task){var end=DateTime.UtcNow.AddSeconds(40);while(!task.IsCompleted){if(DateTime.UtcNow>end)throw new TimeoutException();Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();}
    static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());Application.DoEvents();}
    static void Select(ListView list,int i){foreach(ListViewItem item in list.Items)item.Selected=false;list.Items[i].Selected=true;Application.DoEvents();}
    [STAThread] static int Main(string[] args)
    {
        try{
            var data=Path.GetFullPath(args[0]);Directory.CreateDirectory(data);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
            typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,data+Path.DirectorySeparatorChar);
            if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true")Configuration.Settings.General.MpvExtraOptions="--ao=null --vo=null";
            Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.FFmpegLocation=args[1];LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.EnableVisualStyles(); File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"libmpv-2.dll"),Path.Combine(data,"libmpv-2.dll"),true);
            var first=Path.Combine(data,"first.wav");var second=Path.Combine(data,"second.wav");WriteWave(first,2.25);WriteWave(second,1.5);
            var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Первая строка",1000,2000){Actor="A"});sub.Paragraphs.Add(new Paragraph("Second line",6000,10000){Actor="B"});
            using(var tts=new TextToSpeech(sub,new SubRip(),null,null)){
                Show(tts);var speaking=(Subtitle)Field(tts,"_subtitle");
                var sounds=new List<TextToSpeech.FileNameAndSpeedFactor>{new TextToSpeech.FileNameAndSpeedFactor{Filename=first,Factor=1.5m},new TextToSpeech.FileNameAndSpeedFactor{Filename=second,Factor=1}};
                using(var review=new ReviewAudioClips(tts,speaking,sounds,tts.GetParagraphEngine(speaking.Paragraphs[0]))){
                    Show(review);var list=(ListView)Field(review,"listViewAudioClips");Select(list,0);Pump(review.ReviewWaveformTask);
                    var wave=(ClassicSpeechWaveform)Field(review,"_speechWave");Check(wave.Peaks!=null && Math.Abs(wave.AudioSeconds-2.25)<.002,"actual generated WAV duration and peaks load");
                    Check(review.FitSelectedSpeech() && speaking.Paragraphs[0].EndTime.TotalMilliseconds==3250 && sounds[0].Factor==1.5m,"fit uses already-adjusted clip duration without applying speed twice");
                    Check(review.ApplySpeechTiming(0,1500,3800),"manual cue shift and end adjustment accepted");
                    var json=Path.Combine(data,"session.json");Pump(review.ExportSessionAsync(json,CancellationToken.None));var saved=ClassicTtsSession.Load(json);
                    Check(saved.Items[0].StartMs==1500 && saved.Items[0].EndMs==3800,"manual waveform timings survive session export/import");
                    review.ResetSelectedSpeechTiming();Check(speaking.Paragraphs[0].StartTime.TotalMilliseconds==1000 && speaking.Paragraphs[0].EndTime.TotalMilliseconds==2000,"reset restores both times captured when review opened");
                    Check(!review.ApplySpeechTiming(0,-1,3000) && !review.ApplySpeechTiming(0,3000,2000) && !review.ApplySpeechTiming(0,double.NaN,3000) && !review.ApplySpeechTiming(0,0,double.PositiveInfinity),"invalid drag timings rejected");
                    for(int i=0;i<12;i++)Select(list,i%2);Select(list,1);Pump(review.ReviewWaveformTask);Application.DoEvents();
                    Check(Math.Abs(wave.AudioSeconds-1.5)<.002 && wave.CueStart==6,"rapid row changes cannot publish stale waveform results");
                    Check(review.FitSelectedSpeech() && speaking.Paragraphs[1].EndTime.TotalMilliseconds==7500 && speaking.Paragraphs[0].EndTime.TotalMilliseconds==2000,"fit changes selected cue only");
                    // Exercise the real mouse event path rather than only the form helper.
                    var native=(Nikse.SubtitleEdit.Logic.VideoPlayers.LibMpvDynamic)Field(review,"_libMpv"); Check(native!=null,"review initializes real MPV audio player"); native.Volume=0; typeof(ClassicSpeechWaveform).GetMethod("OnMouseDown",Flags).Invoke(wave,new object[]{new MouseEventArgs(MouseButtons.Left,1,wave.TimeToX(wave.CueStart+.4),50,0)}); Pump(Task.Delay(350)); Check(native.CurrentPosition>=.3,"waveform click seeks and plays generated audio through real MPV"); typeof(ReviewAudioClips).GetMethod("buttonStop_Click",Flags).Invoke(review,new object[]{null,null}); var x=wave.TimeToX(wave.CueEnd);typeof(ClassicSpeechWaveform).GetMethod("OnMouseDown",Flags).Invoke(wave,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,50,0)});
                    typeof(ClassicSpeechWaveform).GetMethod("OnMouseMove",Flags).Invoke(wave,new object[]{new MouseEventArgs(MouseButtons.Left,0,x+20,50,0)});
                    typeof(ClassicSpeechWaveform).GetMethod("OnMouseUp",Flags).Invoke(wave,new object[]{new MouseEventArgs(MouseButtons.Left,1,x+20,50,0)});
                    Check(speaking.Paragraphs[1].EndTime.TotalMilliseconds>7500,"dragging waveform end updates the real subtitle");
                    foreach(var size in new[]{review.Size,review.MinimumSize,new Size(1200,800)}){
                        review.Size=size;Application.DoEvents();var area=review.Controls.Find("speechWaveformArea",true).Single();var toolbar=review.Controls.Find("sessionReviewToolbar",true).Single();
                        Console.WriteLine("BOUNDS list="+list.Bounds+" area="+area.Bounds+" info="+((Control)Field(review,"labelParagraphInfo")).Bounds+" footer="+toolbar.Bounds+" client="+review.ClientSize); using(var debugImage=new Bitmap(review.Width,review.Height)){review.DrawToBitmap(debugImage,new Rectangle(Point.Empty,debugImage.Size));debugImage.Save(Path.Combine(data,"layout-debug.png"));} Check(area.Top>=list.Bottom && area.Bottom<=((Control)Field(review,"labelParagraphInfo")).Top && toolbar.Bottom<=review.ClientSize.Height,"waveform, cue information and session footer do not overlap at "+size);
                        var bar=area.Controls[0];Check(bar.Controls.Cast<Control>().All(c=>c.Bottom<=bar.ClientSize.Height && c.Right<=bar.ClientSize.Width),"waveform toolbar fits without clipping");
                    }
                    review.Size=new Size(1000,730);using(var image=new Bitmap(review.Width,review.Height)){review.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(data,"speech-waveform.png"));}
                    var compressed=Path.Combine(data,"compressed.mp3");var start=new System.Diagnostics.ProcessStartInfo(args[1],"-nostdin -y -i \""+second+"\" -c:a libmp3lame \""+compressed+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
                    using(var encoder=System.Diagnostics.Process.Start(start)){var log=encoder.StandardError.ReadToEnd();encoder.WaitForExit();if(encoder.ExitCode!=0)throw new Exception(log);}
                    sounds[0].Filename=compressed;Select(list,0);Pump(review.ReviewWaveformTask);Check(wave.Peaks!=null && Math.Abs(wave.AudioSeconds-1.5)<.05 && sounds[0].Filename==compressed,"compressed session audio is visualized without replacing the original clip");
                    Select(list,1);
                    sounds[0].Filename=Path.Combine(data,"missing.wav");Select(list,0);Pump(review.ReviewWaveformTask);Check(wave.Peaks==null && !review.FitSelectedSpeech(),"missing clip clears old waveform and disables fit");
                    Select(list,1);review.Close();Pump(review.ReviewWaveformTask);Check(true,"closing review during waveform loading cancels safely");
                }tts.Close();
            }return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static void WriteWave(string file,double seconds){var count=(int)(16000*seconds);using(var w=new BinaryWriter(File.Create(file))){w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(16000);w.Write(32000);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);for(int i=0;i<count;i++)w.Write((short)(Math.Sin(2*Math.PI*440*i/16000)*12000));}}
}
