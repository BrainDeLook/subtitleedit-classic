// Isolated FFmpeg preview regression. Requires a three-second 320x180 testsrc2 clip with two AAC tracks (tur, eng) named ocr-preview-synthetic.mp4 beside the test executable.
using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms.Ocr;
using Nikse.SubtitleEdit.Logic.VideoPlayers;
class TestOcrVideo54
{
 [STAThread] static int Main()
 {
  Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
  Configuration.Settings.General.FFmpegLocation=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","upstream54","ffprobe","ffmpeg.exe"));
  Configuration.Settings.General.VideoPlayer="FFmpeg";
  Configuration.Settings.General.VideoPlayerDefaultVolume=0;
  ClassicFfmpegPlayer.PreferHardware=false;
  Application.EnableVisualStyles();
  Configuration.Settings.General.Language="ru";
  var file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ocr-preview-synthetic.mp4");
  using(var form=new ClassicOcrVideoPreview(file) { AudioTrackNumber=1 })
  {
   form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.ShowInTaskbar=false;
   var cue=new Paragraph("Выбранная реплика",500,1100);form.SelectCue(cue);form.Show();
   Pump(()=>form.Player.Duration>2.5,10000);
   Pump(()=>form.Player.VideoPlayer is ClassicFfmpegPlayer player && player.PresentedPosition>=0.45,10000);
   var playerWithTracks=(ClassicFfmpegPlayer)form.Player.VideoPlayer;
   if(playerWithTracks.AudioTracks.Count!=2 || playerWithTracks.AudioTrackNumber!=1 || playerWithTracks.AudioTracks[1].Language!="eng")throw new Exception("Selected audio track was not preserved");
   if(!form.Player.IsPaused)throw new Exception("Preview must start paused");
   if(Math.Abs(form.Player.CurrentPosition-.5)>.15)throw new Exception("Initial cue seek");
   cue.StartTime.TotalMilliseconds=2000;
   form.PlayCue();
   Pump(()=>form.Player.IsPaused && form.Player.CurrentPosition>=1.1,10000);
   if(form.Player.CurrentPosition>1.5)throw new Exception("Playback went past selected cue");
   form.SelectCue(new Paragraph("Следующая реплика",1500,2000));
   if(Math.Abs(form.Player.CurrentPosition-1.5)>.15 || !form.Player.IsPaused)throw new Exception("Selection must seek and pause");
   Pump(()=>((ClassicFfmpegPlayer)form.Player.VideoPlayer).PresentedPosition>=1.45,10000);
   Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ocr-video-preview-ru.png"));}
   form.Close();
  }
  Console.WriteLine("PASS real FFmpeg OCR preview: load paused, cue seek, source clone, selected audio track, stop at cue end, next selection and disposal");return 0;
 }
 static void Pump(Func<bool> done,int limit)
 {
  var watch=Stopwatch.StartNew();while(!done()){Application.DoEvents();Thread.Sleep(15);if(watch.ElapsedMilliseconds>limit)throw new Exception("Preview timeout");}Application.DoEvents();
 }
}
