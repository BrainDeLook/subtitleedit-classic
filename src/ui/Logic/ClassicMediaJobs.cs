using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Http;
using System.Security.Cryptography;
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
        private static readonly SemaphoreSlim ProbeDownload = new SemaphoreSlim(1, 1);
        public static string ProbePath
        {
            get
            {
                var configured = Configuration.Settings.General.FFmpegLocation;
                var beside = string.IsNullOrWhiteSpace(configured) ? "" : Path.Combine(Path.GetDirectoryName(configured), "ffprobe.exe");
                return File.Exists(beside) ? beside : Path.Combine(Configuration.DataDirectory, "ffmpeg", "ffprobe.exe");
            }
        }
        private static async Task<string> EnsureProbe(CancellationToken token)
        {
            await ProbeDownload.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var path = ProbePath;
                if (File.Exists(path)) return path;
                var x64 = IntPtr.Size == 8;
                var url = "https://github.com/SubtitleEdit/support-files/releases/download/ffmpeg-v8/" + (x64 ? "ffprobe.zip" : "ffprobe-win32.zip");
                var expected = x64 ? "d2fda9b2abab69b4bbfe57e135dcc38312368c62ce3419476738ac2d96bcecf9e0ec3147b3c131199155ef6bc237b70b9eebe7546e4e5d13b414f20aa54c2a6d" : "41db945aea3a16b2d9f2ec967831cb5da4d22a3567e7be3110a5f64bfd40a84f33be48f5162403c968769101c0a1a5329dabba32968697e927fc5213a37701ff";
                using (var http = DownloaderFactory.MakeHttpClient())
                using (var archive = new MemoryStream())
                {
                    await http.DownloadAsync(url, archive, cancellationToken: token).ConfigureAwait(false);
                    using (var sha = SHA512.Create())
                    {
                        archive.Position = 0;
                        var hash = BitConverter.ToString(sha.ComputeHash(archive)).Replace("-", "");
                        if (!hash.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("FFprobe download checksum mismatch.");
                    }
                    archive.Position = 0;
                    using (var zip = ZipExtractor.Open(archive))
                    {
                        var entry = zip.ReadCentralDir().Single(e => Path.GetFileName(e.FilenameInZip).Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase));
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        var stage = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                        try { zip.ExtractFile(entry, stage); token.ThrowIfCancellationRequested(); File.Move(stage, path); }
                        finally { if (File.Exists(stage)) File.Delete(stage); }
                    }
                }
                return path;
            }
            finally { ProbeDownload.Release(); }
        }
        public static string Q(string text)=>ClassicSpeechRunner.Quote(text);
        public static async Task<ClassicMediaInfo> Probe(string file,CancellationToken token)
        {
            var ffprobe=await EnsureProbe(token).ConfigureAwait(false);
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
            using var preventSleep = Nikse.SubtitleEdit.Logic.ClassicPowerRequest.Acquire("Processing subtitles and media");
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

