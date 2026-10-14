using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicDubbingSettings
    {
        public bool RemoveSpeech { get; set; }
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
            await RunFfmpegAsync("-nostdin -y -i "+Quote(video)+" -map 0:a:"+Math.Max(0,audioTrack)+" -vn -ar 44100 -ac 2 -c:a pcm_s16le "+Quote(original),cancel,progress).ConfigureAwait(false);
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
        public static Task MuxAsync(string video,string speech,string background,string output,string encoding,CancellationToken cancel,IProgress<SpeechProgress> progress)
        {
            if(string.IsNullOrWhiteSpace(encoding) || encoding=="copy")encoding=Path.GetExtension(output).Equals(".mp4",StringComparison.OrdinalIgnoreCase) ? "aac" : "pcm_s16le";
            return RunFfmpegAsync("-nostdin -y -i "+Quote(video)+" -i "+Quote(speech)+" -i "+Quote(background)+" -filter_complex \"[1:a][2:a]amix=inputs=2:duration=longest:normalize=0[mixed]\" -map 0:v:0 -map \"[mixed]\" -c:v copy -c:a "+encoding+" -map_metadata 0 "+ClassicBurnIn.GetContainerArguments(Path.GetExtension(output).ToLowerInvariant())+" "+Quote(output),cancel,progress);
        }
        private static string Quote(string value)=>ClassicSpeechRunner.Quote(value);
        private static Task RunFfmpegAsync(string args,CancellationToken cancel,IProgress<SpeechProgress> progress)=>ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,args),cancel,progress);
    }
}
