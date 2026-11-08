using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
namespace Nikse.SubtitleEdit.Logic.ClassicCast
{
 public static class ClassicVoiceAssets
 {
  public static double WaveSeconds(string path){using(var stream=File.OpenRead(path))return new Nikse.SubtitleEdit.Core.Common.WaveHeader(stream).LengthInSeconds;}
  public static async Task EnsureFileAsync(string url,string path,long size,string hash,bool gguf,CancellationToken cancel,IProgress<SpeechProgress> progress)
  {
   if(File.Exists(path) && new FileInfo(path).Length==size)return;
   var temp=path+"."+Guid.NewGuid().ToString("N")+".download";
   try{await ClassicSpeechDownload.DownloadFileAsync(url,temp,gguf,cancel,progress).ConfigureAwait(false);if(new FileInfo(temp).Length!=size)throw new InvalidDataException("Incomplete model or runtime download");await CheckHashAsync(temp,hash,cancel).ConfigureAwait(false);cancel.ThrowIfCancellationRequested();if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
   finally{if(File.Exists(temp))File.Delete(temp);}
  }
  public static Task CheckHashAsync(string path,string hash,CancellationToken cancel)=>Task.Run(()=>{using(var input=File.OpenRead(path))using(var sha=SHA256.Create()){var actual=BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();if(hash!=null && actual!=hash)throw new InvalidDataException("Download checksum mismatch");}cancel.ThrowIfCancellationRequested();},cancel);
  public static void ExtractZip(string zip,string folder,CancellationToken cancel)
  {
   Directory.CreateDirectory(folder);var root=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   using(var archive=ZipFile.OpenRead(zip))foreach(var entry in archive.Entries)
   {cancel.ThrowIfCancellationRequested();var dest=Path.GetFullPath(Path.Combine(folder,entry.FullName));if(!dest.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Unsafe archive path");if(string.IsNullOrEmpty(entry.Name)){Directory.CreateDirectory(dest);continue;}Directory.CreateDirectory(Path.GetDirectoryName(dest));entry.ExtractToFile(dest,true);}
  }
 }
 public static class ClassicSidon
 {
  public static string Root=>Path.Combine(Nikse.SubtitleEdit.Core.Common.Configuration.DataDirectory,"TextToSpeech","ClassicSidon");
  public static async Task<string> CleanAsync(string input,string job,string backend,CancellationToken cancel,IProgress<SpeechProgress> progress)
  {
   input=Path.GetFullPath(input);job=Path.GetFullPath(job);
   if(backend!="cuda" && backend!="vulkan")backend="cpu";
   await Nikse.SubtitleEdit.Logic.ClassicAudioCpp.EnsureAsync(backend,cancel,progress).ConfigureAwait(false);
   var runtime=Nikse.SubtitleEdit.Logic.ClassicAudioCpp.Root(backend);var executable=Path.Combine(runtime,"audiocpp_cli.exe");
   var model=Path.Combine(Root,"models","sidon-v0.1-f32.gguf");
   await ClassicVoiceAssets.EnsureFileAsync("https://huggingface.co/audio-cpp/audio.cpp-gguf/resolve/6f45df32f8fb283e4cfce3d3e1cb7688986fcbe6/Sidon-GGUF/sidon-v0.1-f32.gguf",model,984276544L,"5af689683a8142764dd69c956a87455c34f8b685cce90ac4bcb722037bc8e360",true,cancel,progress).ConfigureAwait(false);
   var cleaned=Path.Combine(job,"sidon.wav");
   await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(executable,"--task s2s --family sidon --model "+ClassicSpeechRunner.Quote(model)+" --backend "+backend+" --audio "+ClassicSpeechRunner.Quote(input)+" --out "+ClassicSpeechRunner.Quote(cleaned)){WorkingDirectory=runtime},cancel,progress).ConfigureAwait(false);
   if(!File.Exists(cleaned) || new FileInfo(cleaned).Length<44)throw new InvalidDataException("Sidon produced no reference audio");return cleaned;
  }
 }
}
