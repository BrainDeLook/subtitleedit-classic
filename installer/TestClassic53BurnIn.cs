using System;
using System.Drawing;
using System.IO;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
class TestClassic53BurnIn {
 static void Main(string[] args) {
  Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
  Configuration.Settings.General.FFmpegLocation=args[1];Directory.CreateDirectory(args[0]);
  var ass=Path.Combine(args[0],"test.ass");File.Copy(args[2],ass,true);
  foreach(int mode in new[]{1,2}) {
   var output=Path.Combine(args[0],"stereo-"+mode+".mp4");
   using(var p=VideoPreviewGenerator.GenerateHardcodedVideoFile(args[3],ass,output,640,360,"libx264","ultrafast","yuv420p","23","aac",true,"48000","","128k","","",null,null,"-t 2","",new ClassicBurnInOptions{StereoMode=mode,StereoDepth=3})) {p.Start();if(!p.WaitForExit(30000) || p.ExitCode!=0)throw new Exception("Stereo encode failed");}
   var image=Path.Combine(args[0],"stereo-"+mode+".png");
   using(var p=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(args[1],"-hide_banner -loglevel error -y -ss 1 -i \""+output+"\" -frames:v 1 \""+image+"\""){UseShellExecute=false,CreateNoWindow=true})) {p.WaitForExit();if(p.ExitCode!=0)throw new Exception("Frame failed");}
   using(var bitmap=new Bitmap(image)) {
    int a=0,b=0;for(int y=0;y<360;y++)for(int x=0;x<640;x++){var c=bitmap.GetPixel(x,y);if(c.R>180 && c.G>180 && c.B>180){if(mode==1 ? x<320 : y<180)a++;else b++;}}
    if(a<50 || b<50)throw new Exception("Missing subtitle eye "+a+"/"+b);
    Console.WriteLine("PASS: stereo "+mode+" both eyes contain rendered text: "+a+"/"+b);
   }
  }
  if(ClassicVideoPresetOptions.Migrate("h264_nvenc","hq")!="p7" || ClassicVideoPresetOptions.MigrateTune("hevc_nvenc","losslesshp")!="lossless" || ClassicVideoPresetOptions.GetNvencPresets().Contains("default"))throw new Exception("Preset migration failed");
  using(var p=VideoPreviewGenerator.GenerateHardcodedVideoFile(args[3],ass,Path.Combine(args[0],"nvenc.mp4"),640,360,"h264_nvenc","losslesshp","yuv420p","23","aac",true,"48000","","128k","","",null,null,"-t 1")) {
   if(!p.StartInfo.Arguments.Contains("-preset p1") || !p.StartInfo.Arguments.Contains("-tune lossless") || p.StartInfo.Arguments.Contains("-cq"))throw new Exception("Lossless migration ignored");
   p.Start();if(!p.WaitForExit(30000) || p.ExitCode!=0)throw new Exception("Native NVENC migration failed");
  }
  Console.WriteLine("PASS: real NVIDIA lossless encode with migrated preset");
 }
}
