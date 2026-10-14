using System;
using System.IO;
using System.Threading;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
class TestTtsEndings {
 static void Main(string[] args) {
  Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
  Configuration.Settings.General.FFmpegLocation=Environment.GetEnvironmentVariable("CLASSIC_TTS_TEST_FFMPEG");
  if(ClassicTtsSilenceThreshold.ThresholdDbfs(-20.8)!=-60.8 || ClassicTtsSilenceThreshold.ThresholdDbfs(-90)!=-70 || ClassicTtsSilenceThreshold.ThresholdDbfs(null)!=-40)throw new Exception("Upstream threshold rules changed");
  var peak=ClassicTtsSilenceThreshold.MeasureAsync(args[0],CancellationToken.None).GetAwaiter().GetResult();
  using(var process=VideoPreviewGenerator.TrimSilenceStartAndEnd(args[0],args[1],ClassicTtsSilenceThreshold.Amplitude(peak))) {process.Start();if(!process.WaitForExit(30000))throw new Exception("Trim timeout");if(process.ExitCode!=0)throw new Exception("Trim failed");}
  if(new FileInfo(args[1]).Length<46560)throw new Exception("Quiet audio edges were cut");
  Console.WriteLine("PASS: quiet leading/trailing audio preserved; peak="+peak+" dBFS");
 }
}
