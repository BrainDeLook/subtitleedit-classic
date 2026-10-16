using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Logic
{
 public static class ClassicPerLineReference
 {
  public static Tuple<double,double> Range(Paragraph cue,IEnumerable<Paragraph> timeline)
  {
   var start=Math.Max(0,cue.StartTime.TotalSeconds);var end=cue.EndTime.TotalSeconds;
   if(end<=start || end-start>30)throw new ArgumentException("Voice reference must have a positive duration of at most 30 seconds.");
   var others=timeline.Where(p=>p.Id!=cue.Id).ToList();
   // Never enlarge a cue that already overlaps another speaker.
   if(!others.Any(p=>p.StartTime.TotalSeconds<end && p.EndTime.TotalSeconds>start)){
    var earliest=others.Where(p=>p.EndTime.TotalSeconds<=start).Select(p=>p.EndTime.TotalSeconds).DefaultIfEmpty(0).Max();
    var latest=others.Where(p=>p.StartTime.TotalSeconds>=end).Select(p=>p.StartTime.TotalSeconds).DefaultIfEmpty(end+3).Min();
    var missing=Math.Max(0,3-(end-start));start=Math.Max(earliest,start-missing/2);end=Math.Min(latest,end+missing/2);
    missing=Math.Max(0,3-(end-start));start=Math.Max(earliest,start-missing);end=Math.Min(latest,end+Math.Max(0,3-(end-start)));
   }
   return Tuple.Create(start,end-start);
  }
  public static async Task<string> ExtractAsync(string video,Paragraph cue,IEnumerable<Paragraph> timeline,int track,string folder,CancellationToken cancel)
  {
   var range=Range(cue,timeline);Directory.CreateDirectory(folder);var file=Path.Combine(folder,"reference.wav");
   string N(double n)=>n.ToString("0.######",CultureInfo.InvariantCulture);
   var args="-nostdin -y -ss "+N(range.Item1)+" -t "+N(range.Item2)+" -i "+ClassicSpeechRunner.Quote(video)+" -map 0:a:"+Math.Max(0,track)+" -vn -ar 24000 -ac 1 -af apad=whole_dur=1 -t "+N(Math.Max(1,range.Item2))+" -c:a pcm_s16le "+ClassicSpeechRunner.Quote(file);
   try{await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,args),cancel,null).ConfigureAwait(false);using(var stream=File.OpenRead(file))if(new WaveHeader(stream).LengthInSeconds<.99)throw new InvalidDataException("No usable voice reference audio.");return file;}
   catch{if(File.Exists(file))File.Delete(file);throw;}
  }
 }
}
