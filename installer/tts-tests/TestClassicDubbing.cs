using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Diagnostics;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicDubbing
{
    static void Main(string[] args)
    {
        Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
        typeof(Configuration).GetField("DataDirectory",BindingFlags.Static|BindingFlags.Public).SetValue(null,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Subtitle Edit")+Path.DirectorySeparatorChar);
        Configuration.Settings.General.FFmpegLocation=args[0];
        var folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);
        var video=Path.Combine(folder,"source.mkv");
        Run("-nostdin -y -f lavfi -i color=c=blue:s=320x180:r=24:d=8 -i "+Q(args[2])+" -f lavfi -i sine=frequency=440:sample_rate=44100:duration=8 -filter_complex \"[1:a][2:a]amix=inputs=2:normalize=0[a]\" -map 0:v -map \"[a]\" -c:v libx264 -preset ultrafast -c:a pcm_s16le -t 8 "+Q(video));
        var settings=new ClassicDubbingSettings{Fast=args.Length<5 || args[4]!="quality",Variant="cpu"};
        var background=ClassicDubbing.BackgroundAsync(video,Path.Combine(folder,"separation"),settings,CancellationToken.None,null).GetAwaiter().GetResult();
        if(new FileInfo(background).Length<100000)throw new Exception("Missing background audio");
        var output=Path.Combine(folder,args.Length>5 && args[5]=="mp4" ? "dubbed.mp4" : "dubbed.mkv");
        ClassicDubbing.MuxAsync(video,Path.GetFullPath(args[3]),background,output,null,CancellationToken.None,null).GetAwaiter().GetResult();
        Run("-nostdin -y -i "+Q(output)+" -map 0:a -c:a pcm_s16le "+Q(Path.Combine(folder,"mixed.wav")));
        if(new FileInfo(Path.Combine(folder,"mixed.wav")).Length<100000)throw new Exception("Missing dubbed audio");
        using(var cancel=new CancellationTokenSource())
        {
            cancel.Cancel();
            try{ClassicDubbing.BackgroundAsync(video,Path.Combine(folder,"cancelled"),settings,cancel.Token,null).GetAwaiter().GetResult();throw new Exception("Cancellation ignored");}
            catch(OperationCanceledException){}
        }
        Console.WriteLine("PASS: native "+(settings.Fast ? "HTDemucs" : "RoFormer")+" background extraction, video/audio mux and cancellation; "+output);
    }
    static string Q(string value)=>Nikse.SubtitleEdit.Logic.SpeechToText.ClassicSpeechRunner.Quote(value);
    static void Run(string args)
    {
        using(var process=Process.Start(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,"-hide_banner -loglevel error "+args){UseShellExecute=false,CreateNoWindow=true}))
        {if(!process.WaitForExit(60000) || process.ExitCode!=0)throw new Exception("FFmpeg failed");}
    }
}
