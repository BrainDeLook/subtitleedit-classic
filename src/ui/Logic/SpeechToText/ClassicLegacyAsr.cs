using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.AudioToText;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic.SpeechToText
{
 public static class ClassicLegacyAsr
 {
  public const string AutoName="Auto detect";
  public const string MultilingualName="Multilingual (per segment)";
  public const string AnimeName="anime.ja";
  private static readonly string[] AnimeFiles={"model.bin","config.json","tokenizer.json","vocabulary.json","preprocessor_config.json"};
  public static WhisperModel AnimeModel()=>new WhisperModel{Name=AnimeName,Size="768 MB Japanese",Folder="faster-whisper-anime.ja",Urls=AnimeFiles.Select(n=>"https://huggingface.co/quantumcookie/anime-whisper-ct2-int8/resolve/main/"+n).ToArray()};
  public static WhisperModel[] Models(IWhisperModel provider){var models=provider.Models;return (Configuration.Settings.Tools.WhisperChoice==WhisperChoice.PurfviewFasterWhisperXxl || Configuration.Settings.Tools.WhisperChoice==WhisperChoice.CTranslate2) && !models.Any(m=>m.Name==AnimeName) ? models.Concat(new[]{AnimeModel()}).ToArray() : models;}
  public static string LanguageArguments(string choice,string language,string extra){
   if(language=="multilingual"){if(choice!=WhisperChoice.PurfviewFasterWhisperXxl)throw new ArgumentException("Per-segment language detection requires Faster-Whisper-XXL.");return System.Text.RegularExpressions.Regex.IsMatch(extra??"",@"(?:^|\s)--multilingual(?:\s|=|$)") ? "" : "--multilingual True ";}
   if(language=="auto")return choice==WhisperChoice.Cpp || choice==WhisperChoice.CppCuBlas || choice==WhisperChoice.ConstMe ? "--language auto " : "";
   return "--language "+language+" ";
  }
  public static string AnimeFolder=>Path.Combine(new WhisperPurfviewFasterWhisperModel().ModelFolder,AnimeModel().Folder);
  public static string ModelArgument(string name){if(name!=AnimeName)return WhisperHelper.GetWhisperModelForCmdLine(name);var root=Path.Combine(WhisperHelper.GetWhisperModel().ModelFolder,AnimeModel().Folder);RepairAnimeAlignment(root);return root;}
  public static async Task EnsureAnimeAsync(CancellationToken cancel,IProgress<SpeechProgress> progress){foreach(var name in AnimeFiles){var file=Path.Combine(AnimeFolder,name);if(!File.Exists(file) || new FileInfo(file).Length==0)await ClassicSpeechDownload.DownloadFileAsync("https://huggingface.co/quantumcookie/anime-whisper-ct2-int8/resolve/main/"+name,file,false,cancel,progress).ConfigureAwait(false);}RepairAnimeAlignment(AnimeFolder);}
  public static bool RepairAnimeAlignment(string folder)
  {
   var file = Path.Combine(folder, "config.json");
   if (!File.Exists(file)) return false;
   JObject config;
   try { config = JObject.Parse(File.ReadAllText(file)); }
   catch (Newtonsoft.Json.JsonException) { return false; }
   if (!(config["alignment_heads"] is JArray heads) || heads.All(h => h is JArray p && p.Count == 2 &&
       p[0].Type == JTokenType.Integer && p[1].Type == JTokenType.Integer &&
       (int)p[0] >= 0 && (int)p[0] < 2 && (int)p[1] >= 0 && (int)p[1] < 20)) return false;
   config["alignment_heads"] = new JArray(Enumerable.Range(0, 20).Select(i => new JArray(1, i)));
   if (!File.Exists(file + ".classic-original")) File.Copy(file, file + ".classic-original");
   var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
   try
   {
    File.WriteAllText(temporary, config.ToString());
    File.Replace(temporary, file, null);
   }
   finally { if (File.Exists(temporary)) File.Delete(temporary); }
   return true;
  }
 }
}
