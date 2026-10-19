using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;
namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicMediaStream
    {
        public int Index {get;set;} public string Type {get;set;} public string Codec {get;set;} public string Language {get;set;} public string Title {get;set;} public int Channels {get;set;}
        public override string ToString()=>$"#{Index} {Type} — {Language} {Title} [{Codec}{(Channels>0?", "+Channels+" ch":"")}]";
    }
    public sealed class ClassicMediaInfo
    {public List<ClassicMediaStream> Streams {get;set;}=new List<ClassicMediaStream>();public double Seconds {get;set;}public int Width {get;set;}public int Height {get;set;}}
    public static class ClassicMediaJobs
    {
        public static string Q(string text)=>ClassicSpeechRunner.Quote(text);
        public static async Task<ClassicMediaInfo> Probe(string file,CancellationToken token)
        {
            var ffprobe=Path.Combine(Path.GetDirectoryName(Configuration.Settings.General.FFmpegLocation),"ffprobe.exe");
            var json=await Capture(ffprobe,"-v error -show_streams -show_format -of json "+Q(file),token);var data=JObject.Parse(json);var result=new ClassicMediaInfo();
            foreach(var item in data["streams"]??new JArray())
            {
                result.Streams.Add(new ClassicMediaStream{Index=(int)item["index"],Type=(string)item["codec_type"],Codec=(string)item["codec_name"],Channels=(int?)item["channels"]??0,Language=(string)item["tags"]?["language"],Title=(string)item["tags"]?["title"]});
                if((string)item["codec_type"]=="video" && result.Width==0){result.Width=(int?)item["width"]??0;result.Height=(int?)item["height"]??0;}
            }
            double.TryParse((string)data["format"]?["duration"],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var seconds);result.Seconds=seconds;return result;
        }
        public static async Task<string> Capture(string executable,string arguments,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();using(var process=new Process{StartInfo=new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8}})
            {
                process.Start();using(token.Register(()=>{try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}}))
                {var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();await Task.Run(()=>process.WaitForExit());var output=await stdout;var error=await stderr;token.ThrowIfCancellationRequested();if(process.ExitCode!=0)throw new InvalidOperationException(string.Join(Environment.NewLine,error.SplitToLines().AsEnumerable().Reverse().Take(6).Reverse()));return output;}
            }
        }
        public static async Task Render(string arguments,string output,CancellationToken token,IProgress<SpeechProgress> progress=null)
        {await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,"-nostdin -hide_banner -y "+arguments+" "+Q(output)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true},token,progress);}
        public static void CheckOutput(string input,string output)
        {if(Path.GetFullPath(input).Equals(Path.GetFullPath(output),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The output must differ from the input.");}
        public static void Commit(string staged,string output)
        {if(!File.Exists(staged) || new FileInfo(staged).Length==0)throw new IOException("No output was generated.");if(File.Exists(output))File.Replace(staged,output,null);else File.Move(staged,output);}
    }
}

