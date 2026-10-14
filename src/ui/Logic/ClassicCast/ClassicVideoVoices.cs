using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace Nikse.SubtitleEdit.Logic.ClassicCast
{
 public static class ClassicVideoVoices
 {
  private static string Q(string s)=>ClassicSpeechRunner.Quote(s);
  public static List<Paragraph> PickReferences(IEnumerable<Paragraph> lines)
  {
   var candidates=lines.Where(p=>!p.IsComment && !SpeechToTextQualityReport.IsNonSpeechLine(p.Text) && p.DurationTotalMilliseconds>=1000 && p.DurationTotalMilliseconds<=30000 && !string.IsNullOrWhiteSpace(p.Text)).OrderByDescending(p=>p.DurationTotalMilliseconds).ToList();var result=new List<Paragraph>();double seconds=0;
   foreach(var p in candidates){if(seconds+p.Duration.TotalSeconds>30)continue;result.Add(p);seconds+=p.Duration.TotalSeconds;if(seconds>=15 || result.Count>=8)break;}return result.OrderBy(p=>p.StartTime.TotalMilliseconds).ToList();
  }
  public static string Transcript(IEnumerable<Paragraph> parts)=>string.Join(" ",parts.Select(p=>Utilities.UnbreakLine(System.Text.RegularExpressions.Regex.Replace(HtmlUtil.RemoveHtmlTags(p.Text),@"\{[^}]*\}",""))).Where(t=>!string.IsNullOrWhiteSpace(t)));
  public static async Task<string> BuildReferenceAsync(string video,List<Paragraph> parts,int track,string job,CancellationToken cancel,IProgress<SpeechProgress> progress)
  {
   Directory.CreateDirectory(job);var clips=new List<string>();for(int i=0;i<parts.Count;i++){cancel.ThrowIfCancellationRequested();var p=parts[i];var file=Path.Combine(job,"part"+i+".wav");await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,"-nostdin -y -ss "+p.StartTime.TotalSeconds.ToString(CultureInfo.InvariantCulture)+" -i "+Q(video)+" -map 0:a:"+Math.Max(0,track)+" -t "+p.Duration.TotalSeconds.ToString(CultureInfo.InvariantCulture)+" -vn -ar 24000 -ac 1 -c:a pcm_s16le "+Q(file)),cancel,progress).ConfigureAwait(false);clips.Add(file);}
   if(clips.Count==0)throw new InvalidDataException("No usable reference lines");var list=Path.Combine(job,"parts.txt");File.WriteAllLines(list,clips.Select(f=>"file '"+f.Replace("\\","/").Replace("'","'\\''")+"'"));var output=Path.Combine(job,"reference.wav");await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,"-nostdin -y -f concat -safe 0 -i "+Q(list)+" -c:a pcm_s16le "+Q(output)),cancel,progress).ConfigureAwait(false);return output;
  }
  public static async Task<Subtitle> FindAsync(string video,int track,string language,string variant,string job,CancellationToken cancel,IProgress<SpeechProgress> progress)
  {
   var engine=ClassicSpeechCatalog.Engines.First(e=>e.Family=="crisp" && e.Backend==(language=="ru" ? "gigaam" : "parakeet"));var model=engine.Models.First(m=>!m.Aligner && (language!="ru" || m.Name.Contains("v3")));
   await ClassicSpeechDownload.EnsureRuntimeAsync(engine,variant,cancel,progress).ConfigureAwait(false);await ClassicSpeechDownload.EnsureModelAsync(engine,model,cancel,progress).ConfigureAwait(false);
   var diarization=Path.Combine(ClassicSpeechCatalog.Root,"Models","crisp","Nemotron-3-Diarization.q8_0.gguf");await ClassicVoiceAssets.EnsureFileAsync("https://huggingface.co/nvidia/Nemotron-3-Diarization/resolve/main/Nemotron-3-Diarization.q8_0.gguf",diarization,107012128L,"08456d9e22cd9a323c0364d98375f3746d6e68507ebb705cd46438c534c7a3a1",true,cancel,progress).ConfigureAwait(false);
   Directory.CreateDirectory(job);var audio=Path.Combine(job,"source.wav");await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,"-nostdin -y -i "+Q(video)+" -map 0:a:"+Math.Max(0,track)+" -vn -af aresample=async=1:first_pts=0 -ar 16000 -ac 1 -c:a pcm_s16le "+Q(audio)),cancel,progress).ConfigureAwait(false);
   var options=new ClassicSpeechOptions{Language=language,Variant=variant,UseVad=true};options.ExtraArguments[engine.Name]="--diarize --diarize-method sortformer --diarize-model "+Q(diarization);
   var result=await ClassicSpeechRunner.TranscribeAsync(engine,model,options,audio,Path.Combine(job,"transcript"),null,cancel,progress).ConfigureAwait(false);if(SpeakerLabelParser.MoveLabelsToActors(result)==0)throw new InvalidDataException("The engine returned no speaker labels");return result;
  }
 }
}
