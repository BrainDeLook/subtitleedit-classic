using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.AudioToText;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestXxlMultilingual
{
 static int Main(string[] args){try{Run(args).GetAwaiter().GetResult();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static async Task Run(string[] args)
 {
  var root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);Configuration.Settings.General.FFmpegLocation=args[1];
  using(var c=new CancellationTokenSource(TimeSpan.FromMinutes(25)))
  {
   var runtime=Path.Combine(root,"runtime");var executable=Directory.Exists(runtime)?Directory.GetFiles(runtime,"faster-whisper-xxl.exe",SearchOption.AllDirectories).FirstOrDefault():null;
   if(executable==null){var archive=Path.Combine(root,"Faster-Whisper-XXL_r245.4_windows.7z");if(!File.Exists(archive))await ClassicSpeechDownload.DownloadFileAsync("https://github.com/Purfview/whisper-standalone-win/releases/download/Faster-Whisper-XXL/Faster-Whisper-XXL_r245.4_windows.7z",archive,false,c.Token,new Progress<SpeechProgress>(p=>Console.WriteLine(p.Message)));if(new FileInfo(archive).Length!=1424256246)throw new InvalidDataException("Unexpected pinned XXL archive size.");ClassicSpeechDownload.Extract(archive,runtime,c.Token);executable=Directory.GetFiles(runtime,"faster-whisper-xxl.exe",SearchOption.AllDirectories).Single();}
   var input=Path.Combine(root,"mixed.wav");var filter="[0:a]aresample=16000,aformat=channel_layouts=mono,apad=pad_dur=2[a];[1:a]aresample=16000,aformat=channel_layouts=mono[b];[a][b]concat=n=2:v=0:a=1[out]";
   await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(args[1],"-nostdin -y -i "+ClassicSpeechRunner.Quote(args[2])+" -i "+ClassicSpeechRunner.Quote(args[3])+" -filter_complex "+ClassicSpeechRunner.Quote(filter)+" -map [out] -c:a pcm_s16le "+ClassicSpeechRunner.Quote(input)),c.Token,null);
   var flags=ClassicLegacyAsr.LanguageArguments(WhisperChoice.PurfviewFasterWhisperXxl,"multilingual","");var command=ClassicSpeechRunner.Quote(input)+" --model small --device cpu --compute_type int8 "+flags+" --output_format srt --output_dir "+ClassicSpeechRunner.Quote(root)+" --model_dir "+ClassicSpeechRunner.Quote(Path.Combine(root,"models"));
   Console.WriteLine(command);var start=new ProcessStartInfo(executable,command){WorkingDirectory=Path.GetDirectoryName(executable)};start.EnvironmentVariables["PYTHONIOENCODING"]="utf-8";start.EnvironmentVariables["PYTHONUTF8"]="1";
   await ClassicSpeechRunner.RunProcessAsync(start,c.Token,new Progress<SpeechProgress>(p=>Console.WriteLine(p.Message)));
   var result=ClassicSpeechRunner.ParseSrt(File.ReadAllText(Path.Combine(root,"mixed.srt")));var text=string.Join(" ",result.Paragraphs.Select(p=>p.Text));Console.WriteLine(text);if(!text.Any(ch=>ch>='А'&&ch<='я') || text.IndexOf("Hello",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidDataException("Mixed-language output did not retain both known languages.");Console.WriteLine("PASS: Native XXL per-segment multilingual output retains English and Russian.");
  }
 }
}
