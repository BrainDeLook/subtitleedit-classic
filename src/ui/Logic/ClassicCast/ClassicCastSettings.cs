using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Nikse.SubtitleEdit.Logic.ClassicCast
{
 public sealed class ClassicCastRow
 {
  public string Actor {get;set;}="";
  public string Engine {get;set;}="";
  public string Voice {get;set;}="";
  public string Model {get;set;}="";
  public string Instructions {get;set;}="";
  public string Language {get;set;}="";
  public bool Selected {get;set;}
  public ClassicCastRow Copy()=>JsonConvert.DeserializeObject<ClassicCastRow>(JsonConvert.SerializeObject(this));
 }
 public sealed class ClassicCastSettings
 {
  public string OriginalSubtitleFile {get;set;}="";
  public string PerLineVideoFile {get;set;}="";
  public bool PerLineAudioOnly {get;set;}
  public bool CloneConsent {get;set;}
  public List<ClassicCastRow> Rows {get;set;}=new List<ClassicCastRow>();
  public static string PathName=>Path.Combine(Configuration.DataDirectory,"TextToSpeech","ClassicCast.json");
  public static ClassicCastSettings Load(){try{var settings=JsonConvert.DeserializeObject<ClassicCastSettings>(File.ReadAllText(PathName)) ?? new ClassicCastSettings();if(settings.Rows==null)settings.Rows=new List<ClassicCastRow>();return settings;}catch(IOException){return new ClassicCastSettings();}catch(JsonException){return new ClassicCastSettings();}}
  public void Save(){Directory.CreateDirectory(Path.GetDirectoryName(PathName));var temp=PathName+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,JsonConvert.SerializeObject(this,Formatting.Indented));if(File.Exists(PathName))File.Replace(temp,PathName,null);else File.Move(temp,PathName);}finally{if(File.Exists(temp))File.Delete(temp);}}
  public ClassicCastRow Get(string actor)=>Rows.FirstOrDefault(r=>r.Actor==(actor ?? ""));
 }
}
