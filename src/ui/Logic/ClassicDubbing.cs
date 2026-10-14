using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicDubbingSettings
    {
        public bool RemoveSpeech { get; set; }
        public bool RemoveAllSpeech { get; set; } // Missing setting preserves actor-scoped removal.
        public bool Fast { get; set; } = true;
        public string Variant { get; set; } = "cpu";
        public static string Root=>Path.Combine(Configuration.DataDirectory,"TextToSpeech","ClassicDubbing");
        public static ClassicDubbingSettings Load()
        {
            try{return JsonConvert.DeserializeObject<ClassicDubbingSettings>(File.ReadAllText(Path.Combine(Root,"settings.json"))) ?? new ClassicDubbingSettings();}
            catch(IOException){return new ClassicDubbingSettings();}catch(JsonException){return new ClassicDubbingSettings();}
        }
        public void Save(){Directory.CreateDirectory(Root);File.WriteAllText(Path.Combine(Root,"settings.json"),JsonConvert.SerializeObject(this,Formatting.Indented));}
    }
    // Stable v5.3.0 RemoveOriginalSpeech: separate original audio, retain music/effects, mix new speech.
    public static class ClassicDubbing
    {
        private static readonly SpeechEngine Engine=new SpeechEngine{Family="crisp"};
        public static async Task<string> BackgroundAsync(string video,string job,ClassicDubbingSettings settings,CancellationToken cancel,IProgress<SpeechProgress> progress,int audioTrack=0)
        {
            await ClassicSpeechDownload.EnsureRuntimeAsync(Engine,settings.Variant,cancel,progress).ConfigureAwait(false);
            var file=settings.Fast ? "htdemucs-f16.gguf" : "mel-band-roformer-vocals-f16.gguf";
            var repository=settings.Fast ? "htdemucs-GGUF" : "mel-band-roformer-vocals-GGUF";
            var bytes=settings.Fast ? 84329376L : 457014016L;
            var hash=settings.Fast ? "4e7c4dcb8ac6315115848ff642ff8465f56b96d0a8ac7450be82acfc3534f006" : "fe94b114bce12653dae4e94968d28cbab36dcdd0c783fc052c3b656218233f24";
            var models=Path.Combine(ClassicDubbingSettings.Root,"models");
            await ClassicQwenTts.EnsureModel(file,repository,bytes,hash,cancel,progress,models).ConfigureAwait(false);
            Directory.CreateDirectory(job);
            var original=Path.Combine(job,"original.wav");
            await ExtractOriginalAsync(video,original,audioTrack,cancel,progress).ConfigureAwait(false);
            var stems=settings.Fast ? new[]{"drums","bass","other"} : new[]{"other"};
            var executable=ClassicSpeechCatalog.Executable(Engine,settings.Variant);
            await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(executable,"-m "+Quote(Path.Combine(models,file))+" -f "+Quote(original)+" --separate --stems "+string.Join(",",stems)+" --sep-output-dir "+Quote(job)){WorkingDirectory=Path.GetDirectoryName(executable)},cancel,progress).ConfigureAwait(false);
            var paths=stems.Select(s=>Path.Combine(job,"original_"+s+".wav")).ToArray();
            if(paths.Any(p=>!File.Exists(p) || new FileInfo(p).Length<44))throw new IOException("Original speech separation did not produce valid stems");
            if(paths.Length==1)return paths[0];
            var output=Path.Combine(job,"background.wav");
            await RunFfmpegAsync("-nostdin -y "+string.Join(" ",paths.Select(p=>"-i "+Quote(p)))+" -filter_complex amix=inputs="+paths.Length+":normalize=0 -ar 44100 -ac 2 -c:a pcm_s16le "+Quote(output),cancel,progress).ConfigureAwait(false);
            return output;
        }
        public sealed class SpeechRange
        {
            public double Start { get; }
            public double End { get; }
            public SpeechRange(double start, double end) { Start = Math.Max(0, start); End = end; }
        }

        public static List<SpeechRange> ExcludeProtectedRanges(IEnumerable<SpeechRange> selected, IEnumerable<SpeechRange> protectedRanges)
        {
            var pieces = selected.Where(r => r.End > r.Start).OrderBy(r => r.Start).ToList();
            foreach (var keep in protectedRanges.Where(r => r.End > r.Start))
            {
                var next = new List<SpeechRange>();
                foreach (var r in pieces)
                {
                    if (keep.End <= r.Start || keep.Start >= r.End) next.Add(r);
                    else
                    {
                        if (keep.Start > r.Start) next.Add(new SpeechRange(r.Start, keep.Start));
                        if (keep.End < r.End) next.Add(new SpeechRange(keep.End, r.End));
                    }
                }
                pieces = next;
            }
            var merged = new List<SpeechRange>();
            foreach (var r in pieces.OrderBy(r => r.Start))
            {
                if (merged.Count == 0 || merged[merged.Count - 1].End < r.Start) merged.Add(r);
                else { var last = merged[merged.Count - 1]; merged[merged.Count - 1] = new SpeechRange(last.Start, Math.Max(last.End, r.End)); }
            }
            return merged;
        }

        public static Task ExtractOriginalAsync(string video, string output, int audioTrack, CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            return RunFfmpegAsync("-nostdin -y -i " + Quote(video) + " -map 0:a:" + Math.Max(0, audioTrack) +
                " -vn -af aresample=async=1:first_pts=0 -ar 44100 -ac 2 -c:a pcm_s16le " + Quote(output), cancel, progress);
        }

        public static Task<string> ApplyRemovalScopeAsync(string original, string background, string job,
            ClassicDubbingSettings settings, IEnumerable<SpeechRange> ranges, CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            cancel.ThrowIfCancellationRequested();
            if (settings.RemoveAllSpeech) return Task.FromResult(background);
            return RestoreOutsideAsync(original, background, job, ranges, cancel, progress);
        }

        // Preserve the original samples outside dubbed cues. Fade only inside each selected interval.
        public static async Task<string> RestoreOutsideAsync(string original, string background, string job,
            IEnumerable<SpeechRange> ranges, CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            var intervals = ExcludeProtectedRanges(ranges, new SpeechRange[0]);
            if (intervals.Count == 0) return original;
            var n = new Func<double,string>(v => v.ToString("0.######", CultureInfo.InvariantCulture));
            var mask = BuildRangeMask(intervals, 0, intervals.Count, n);
            var graph = "[0:a][1:a]join=inputs=2:channel_layout=quad:map=0.0-FL|0.1-FR|1.0-BL|1.1-BR[j];"+
                "[j]aeval=exprs='val(0)*(1-("+mask+"))+val(2)*("+mask+")|val(1)*(1-("+mask+"))+val(3)*("+mask+")':channel_layout=stereo[out]";
            Directory.CreateDirectory(job);
            var script = Path.Combine(job, "restore-original.ffgraph");
            File.WriteAllText(script, graph, new System.Text.UTF8Encoding(false));
            var output = Path.Combine(job, "selected-background.wav");
            var inputs = "-nostdin -y -i "+Quote(original)+" -i "+Quote(background);
            var outputs = " -map " + Quote("[out]") + " -ar 44100 -ac 2 -c:a pcm_s16le " + Quote(output);
            try
            {
                // FFmpeg 9 removed filter_complex_script; modern builds load arguments with -/option.
                await RunFfmpegAsync(inputs+" -/filter_complex "+Quote(script)+outputs,cancel,progress).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (ex.Message.IndexOf("Unrecognized option '/filter_complex'", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Retain compatibility with older user-provided FFmpeg builds.
                cancel.ThrowIfCancellationRequested();
                await RunFfmpegAsync(inputs+" -filter_complex_script "+Quote(script)+outputs,cancel,progress).ConfigureAwait(false);
            }
            return output;
        }

        private static string BuildRangeMask(List<SpeechRange> ranges, int start, int count, Func<double,string> n)
        {
            if (count == 0) return "0";
            if (count == 1)
            {
                var r = ranges[start];
                var ramp = Math.Min(0.02, (r.End-r.Start)/2);
                return "max(0,min(1,min((t-"+n(r.Start)+")/"+n(ramp)+",("+n(r.End)+"-t)/"+n(ramp)+")))";
            }
            // FFmpeg evaluates only the chosen branch, so long films remain O(log cue count) per sample.
            var left = count / 2;
            return "if(lt(t,"+n(ranges[start+left].Start)+"),"+BuildRangeMask(ranges,start,left,n)+","+BuildRangeMask(ranges,start+left,count-left,n)+")";
        }

        public static Task MuxAsync(string video,string speech,string background,string output,string encoding,CancellationToken cancel,IProgress<SpeechProgress> progress)
        {
            if(string.IsNullOrWhiteSpace(encoding) || encoding=="copy")encoding=Path.GetExtension(output).Equals(".mp4",StringComparison.OrdinalIgnoreCase) ? "aac" : "pcm_s16le";
            return RunFfmpegAsync("-nostdin -y -i "+Quote(video)+" -i "+Quote(speech)+" -i "+Quote(background)+" -filter_complex \"[1:a][2:a]amix=inputs=2:duration=longest:normalize=0[mixed]\" -map 0:v:0 -map \"[mixed]\" -c:v copy -c:a "+encoding+" -map_metadata 0 "+ClassicBurnIn.GetContainerArguments(Path.GetExtension(output).ToLowerInvariant())+" "+Quote(output),cancel,progress);
        }
        private static string Quote(string value)=>ClassicSpeechRunner.Quote(value);
        private static Task RunFfmpegAsync(string args,CancellationToken cancel,IProgress<SpeechProgress> progress)=>ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,args),cancel,progress);
    }
}
