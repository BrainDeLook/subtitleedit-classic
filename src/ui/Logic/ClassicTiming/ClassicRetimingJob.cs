using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Logic.ClassicTiming
{
    public sealed class ClassicRetimingJob : ClassicAlignerContracts.IRunner, ClassicAlignerContracts.IAudioSource
    {
        private static readonly SpeechEngine Engine=new SpeechEngine{Family="crisp"};
        private string _executable,_model,_audio,_folder;
        private int _window;
        public double TotalSeconds { get; private set; }
        private IProgress<SpeechProgress> _progress;
        public async Task<SubtitleRetimer.LineResult[]> RunAsync(Subtitle subtitle,string video,double seconds,string variant,bool qwen,bool checkSpeech,string language,SubtitleRetimer.Options options,CancellationToken cancel,IProgress<SpeechProgress> progress,int audioTrack=0)
        {
            if(seconds<=0 || !File.Exists(video))throw new ArgumentException("Open a local video or audio file first");
            _folder=Path.Combine(Path.GetTempPath(),"se-retime-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_folder);
            _progress=progress;TotalSeconds=seconds;
            try
            {
                await ClassicSpeechDownload.EnsureRuntimeAsync(Engine,variant,cancel,progress).ConfigureAwait(false);
                _executable=ClassicSpeechCatalog.Executable(Engine,variant);
                var name=qwen ? "qwen3-forced-aligner-0.6b-q8_0.gguf" : "canary-ctc-aligner-q8_0.gguf";
                var models=Path.Combine(ClassicSpeechCatalog.Root,"Models","crisp");
                await ClassicQwenTts.EnsureModel(name,qwen ? "qwen3-forced-aligner-0.6b-GGUF" : "canary-ctc-aligner-GGUF",qwen ? 985594624L : 666894464L,qwen ? "539df5dd0fe1721e378ac13bfac9a26b1260dafb62d892c518c1f21244762636" : "f72d2beb52348e6cbc80f6993e6a10fda63629cbcf6ba14244c0c1211f91e8bd",cancel,progress,models).ConfigureAwait(false);
                _model=Path.Combine(models,name);_audio=Path.Combine(_folder,"source.wav");
                await Ffmpeg("-nostdin -y -i "+Q(video)+" -map 0:a:"+Math.Max(0,audioTrack)+" -vn -af aresample=async=1:first_pts=0 -ar 16000 -ac 1 -c:a pcm_s16le "+Q(_audio),cancel).ConfigureAwait(false);
                List<SpeechToTextCheck.HeardWord> heard=null;
                if(checkSpeech)heard=await HearAsync(variant,language,cancel).ConfigureAwait(false);
                var lines=subtitle.Paragraphs.Select(p=>new SubtitleRetimer.Line(p.IsComment ? string.Empty : p.Text,p.StartTime.TotalSeconds,p.EndTime.TotalSeconds)).ToList();
                var synced=lines;RoughSync.Result sync=null;
                if(heard!=null)
                {
                    sync=RoughSync.Measure(lines,heard);
                    if(sync!=null && sync.MaxAbsOffset>options.MaxShiftSeconds)synced=RoughSync.Apply(lines,sync);else sync=null;
                }
                var alignmentProgress=new Progress<SubtitleRetimer.Progress>(p=>progress?.Report(new SpeechProgress{Message="Aligning batch "+p.BatchIndex+" / "+p.BatchCount,Percent=(int)p.Percent}));
                var result=(await new SubtitleRetimer(this,this,options).RetimeAsync(synced,alignmentProgress,cancel,heard).ConfigureAwait(false)).ToArray();
                if(sync!=null)RoughSync.Merge(lines,synced,result);
                return result;
            }
            finally{if(Directory.Exists(_folder))Directory.Delete(_folder,true);}
        }
        private async Task<List<SpeechToTextCheck.HeardWord>> HearAsync(string variant,string language,CancellationToken cancel)
        {
            var engine=ClassicSpeechCatalog.Get(ClassicSpeechCatalog.Crisp,"parakeet");
            var model=engine.Models.First(m=>m.Name=="parakeet-tdt-0.6b-v3-q4_k.gguf");
            await ClassicSpeechDownload.EnsureModelAsync(engine,model,cancel,_progress).ConfigureAwait(false);
            var output=Path.Combine(_folder,"heard");
            var args="--backend parakeet -l "+Q(language)+" -m "+Q(ClassicSpeechCatalog.ModelPath(engine,model))+" -f "+Q(_audio)+" --output-srt -of "+Q(output)+" -ml 1 -sow --print-progress";
            await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(_executable,args){WorkingDirectory=Path.GetDirectoryName(_executable)},cancel,_progress).ConfigureAwait(false);
            return ParseHeardWords(File.ReadAllText(output+".srt"));
        }
        public static List<SpeechToTextCheck.HeardWord> ParseHeardWords(string srt)
        {
            var result=new List<SpeechToTextCheck.HeardWord>();
            foreach(var paragraph in ClassicSpeechRunner.ParseSrt(srt).Paragraphs)
            {
                var parts=HtmlUtil.RemoveHtmlTags(paragraph.Text,true).Split(new[]{' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries);
                var chars=parts.Sum(p=>p.Length);var start=paragraph.StartTime.TotalSeconds;
                foreach(var part in parts){var length=chars>0 ? paragraph.Duration.TotalSeconds*part.Length/chars : 0;result.Add(new SpeechToTextCheck.HeardWord(part,start,start+length));start+=length;}
            }
            return result;
        }
        public async Task<string> AlignAsync(string audioFileName,string textFileName,CancellationToken cancellationToken)
        {
            var output=Path.ChangeExtension(audioFileName,".aligned.srt");
            var args="--align-only -am "+Q(_model)+" -f "+Q(audioFileName)+" --text-file "+Q(textFileName)+" --align-granularity segment --align-output "+Q(output)+" --no-prints";
            await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(_executable,args){WorkingDirectory=Path.GetDirectoryName(_executable)},cancellationToken,_progress).ConfigureAwait(false);
            return File.ReadAllText(output);
        }
        public async Task<string> ExtractWindowAsync(double startSeconds,double durationSeconds,CancellationToken cancellationToken)
        {
            var output=Path.Combine(_folder,"window-"+(++_window)+".wav");
            await Ffmpeg("-nostdin -y -ss "+startSeconds.ToString(CultureInfo.InvariantCulture)+" -i "+Q(_audio)+" -t "+durationSeconds.ToString(CultureInfo.InvariantCulture)+" -ar 16000 -ac 1 -c:a pcm_s16le "+Q(output),cancellationToken).ConfigureAwait(false);
            return output;
        }
        private Task Ffmpeg(string args,CancellationToken cancel)=>ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,args),cancel,_progress);
        private static string Q(string value)=>ClassicSpeechRunner.Quote(value);
    }
}
