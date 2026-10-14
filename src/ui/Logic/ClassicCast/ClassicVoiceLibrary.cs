using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace Nikse.SubtitleEdit.Logic.ClassicCast
{
 public static class ClassicVoiceLibrary
 {
  public static void Update(ClassicQwenReference voice,string name,string text)
  {
   ClassicQwenReference.Validate(voice.Id);if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(text))throw new ArgumentException("Name and exact transcript are required");
   var root=Path.Combine(ClassicQwenReference.Folder,voice.Id);var oldText=File.ReadAllText(root+".txt");var oldJson=File.ReadAllText(root+".json");var copy=JsonConvert.DeserializeObject<ClassicQwenReference>(oldJson);copy.Name=name.Trim();
   try{AtomicWrite(root+".txt",text.Trim());AtomicWrite(root+".json",JsonConvert.SerializeObject(copy));voice.Name=copy.Name;}catch{AtomicWrite(root+".txt",oldText);AtomicWrite(root+".json",oldJson);throw;}
  }
  private static void AtomicWrite(string path,string text){var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,text,new System.Text.UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
  public static void Export(IEnumerable<ClassicQwenReference> voices,string path)
  {
   var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{using(var zip=ZipFile.Open(temp,ZipArchiveMode.Create))foreach(var voice in voices){ClassicQwenReference.Validate(voice.Id);foreach(var ext in new[]{".wav",".txt",".json"})zip.CreateEntryFromFile(Path.Combine(ClassicQwenReference.Folder,voice.Id+ext),voice.Id+ext);}
    if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
   }finally{if(File.Exists(temp))File.Delete(temp);}
  }
  public static async Task<List<ClassicQwenReference>> ImportPackAsync(string archive,bool consent,CancellationToken cancel)
  {
   if(!consent)throw new ArgumentException("Confirm permission to use these voices");var job=Path.Combine(Path.GetTempPath(),"se-voice-pack-"+Guid.NewGuid().ToString("N"));var imported=new List<ClassicQwenReference>();
   try
   {
    using(var check=ZipFile.OpenRead(archive))if(check.Entries.Sum(e=>e.Length)>536870912L)throw new InvalidDataException("Voice pack exceeds the 512 MB unpacked limit");
    ClassicVoiceAssets.ExtractZip(archive,job,cancel);
    var waves=Directory.GetFiles(job,"*.wav",SearchOption.AllDirectories);if(waves.Length==0 || waves.Length>200)throw new InvalidDataException("Voice pack must contain 1–200 WAV recordings");
    foreach(var wave in waves)if(!File.Exists(Path.ChangeExtension(wave,".txt")) || string.IsNullOrWhiteSpace(File.ReadAllText(Path.ChangeExtension(wave,".txt"))))throw new InvalidDataException("Every voice needs its exact transcript");
    foreach(var wave in waves){cancel.ThrowIfCancellationRequested();var name=Path.GetFileNameWithoutExtension(wave);var meta=Path.ChangeExtension(wave,".json");if(File.Exists(meta))name=JsonConvert.DeserializeObject<ClassicQwenReference>(File.ReadAllText(meta))?.Name ?? name;var seconds=ClassicVoiceAssets.WaveSeconds(wave);if(seconds<1 || seconds>30)throw new InvalidDataException("Voice references must be 1–30 seconds");var voice=await ClassicQwenReference.ImportAsync(wave,name,File.ReadAllText(Path.ChangeExtension(wave,".txt")),0,seconds,true,cancel).ConfigureAwait(false);imported.Add(voice);
     var attribution=Directory.GetFiles(job,"ATTRIBUTION.txt",SearchOption.AllDirectories).FirstOrDefault();
     voice.Attribution=File.Exists(meta) ? JsonConvert.DeserializeObject<ClassicQwenReference>(File.ReadAllText(meta))?.Attribution ?? "" : attribution==null ? "" : File.ReadAllText(attribution);File.WriteAllText(Path.Combine(ClassicQwenReference.Folder,voice.Id+".json"),JsonConvert.SerializeObject(voice));}
    return imported;
   }catch{foreach(var voice in imported)Delete(voice);throw;}finally{if(Directory.Exists(job))Directory.Delete(job,true);}
  }
  public static void Delete(ClassicQwenReference voice){ClassicQwenReference.Validate(voice.Id);foreach(var ext in new[]{".wav",".txt",".json"})File.Delete(Path.Combine(ClassicQwenReference.Folder,voice.Id+ext));}
 }
 public sealed class ClassicVoicePack
 {
  public string Id,Name,Language,Code,Description,Url,License,Hash;public int Count;public long Bytes;
  public ClassicVoicePack(string id,string name,string language,string code,string description,string url,int count,long bytes,string license,string hash){Id=id;Name=name;Language=language;Code=code;Description=description;Url=url;Count=count;Bytes=bytes;License=license;Hash=hash;}
  public override string ToString()=>Name+" ("+Count+") — "+License;
 }
}
