using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic.VideoOcr
{
    public sealed class ClassicVideoOcrOptions
    {
        // Defaults copied from stable v5.2.0 SeVideoOcr.
        public string Language { get; set; }="en";
        public string Engine { get; set; }="Paddle OCR";
        public string CrispBackend { get; set; }="GLM-OCR";
        public string CrispModel { get; set; }="glm-ocr-q8_0.gguf";
        public string CrispVariant { get; set; }="vulkan";
        public string Variant { get; set; }="cpu";
        public string ModelMode { get; set; }="mobile";
        public int FramesPerSecond { get; set; }=5;
        public int ImageSimilarityPercent { get; set; }=92;
        public int TextSimilarityPercent { get; set; }=80;
        public int MaxGapMs { get; set; }=250;
        public int MinDurationMs { get; set; }=250;
        public int BrightnessMinimum { get; set; }=190;
        public int MaxImageWidth { get; set; }=720;
        public bool AddAssaPositionTag { get; set; }
        public bool FixOcrErrors { get; set; }=true;
        public string DictionaryFileName { get; set; }="";
        public double CropXPercent { get; set; }
        public double CropYPercent { get; set; }=200.0/3;
        public double CropWidthPercent { get; set; }=100;
        public double CropHeightPercent { get; set; }=100.0/3;
        public static ClassicVideoOcrOptions Load()
        {
            var file=Path.Combine(ClassicPaddleOcr.Root,"settings.json");
            try { return File.Exists(file) ? JsonConvert.DeserializeObject<ClassicVideoOcrOptions>(File.ReadAllText(file)) ?? new ClassicVideoOcrOptions() : new ClassicVideoOcrOptions(); }
            catch(IOException) { return new ClassicVideoOcrOptions(); }
            catch(JsonException) { return new ClassicVideoOcrOptions(); }
        }
        public void Save()
        {
            Directory.CreateDirectory(ClassicPaddleOcr.Root);
            var file=Path.Combine(ClassicPaddleOcr.Root,"settings.json"); var temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            File.WriteAllText(temp,JsonConvert.SerializeObject(this,Formatting.Indented));
            if(File.Exists(file)) File.Replace(temp,file,null); else File.Move(temp,file);
        }
        public string CropFilter(VideoInfo info)
        {
            var x=(int)Math.Floor(VideoOcrCompat.Clamp(CropXPercent,0,99.9)*info.Width/100);
            var y=(int)Math.Floor(VideoOcrCompat.Clamp(CropYPercent,0,99.9)*info.Height/100);
            var width=Math.Max(1,Math.Min(info.Width-x,(int)Math.Round(CropWidthPercent*info.Width/100)));
            var height=Math.Max(1,Math.Min(info.Height-y,(int)Math.Round(CropHeightPercent*info.Height/100)));
            return "crop="+width+":"+height+":"+x+":"+y+(width>MaxImageWidth ? ",scale="+MaxImageWidth+":-1" : "");
        }
    }
    public static class ClassicVideoOcrPipeline
    {
        public static async Task<Subtitle> ScanAsync(string video,VideoInfo info,ClassicVideoOcrOptions options,CancellationToken cancel,
            IProgress<SpeechProgress> progress=null,Action<int,VideoOcrFrameGroup> observation=null,Func<int,VideoOcrFrameGroup,string> fixText=null)
        {
            if(info.Width<=0 || info.Height<=0 || info.TotalSeconds<=0) throw new ArgumentException("No valid video information");
            if(options.FramesPerSecond<1 || options.FramesPerSecond>25) throw new ArgumentOutOfRangeException("FramesPerSecond");
            await EnsureInstalledAsync(options,cancel,progress).ConfigureAwait(false);
            var job=Path.Combine(ClassicPaddleOcr.Root,"Jobs",Guid.NewGuid().ToString("N"));
            var frames=Path.Combine(job,"frames"); Directory.CreateDirectory(frames);
            try
            {
                progress?.Report(new SpeechProgress{Message="Extracting video frames"});
                var filter=options.CropFilter(info);
                var args="-nostdin -y -i "+VideoOcrProcess.Quote(video)+" -an -vf "+VideoOcrProcess.Quote("fps="+options.FramesPerSecond+","+filter)+
                    " -q:v 2 -start_number 0 -progress pipe:1 "+VideoOcrProcess.Quote(Path.Combine(frames,"img%06d.jpg"));
                await VideoOcrProcess.RunFfmpegAsync(args,job,cancel,line=>
                {
                    if(line.StartsWith("out_time_us=",StringComparison.Ordinal) && long.TryParse(line.Substring(12),out var time))
                        progress?.Report(new SpeechProgress{Message="Extracting video frames",Percent=Math.Min(100,(int)(time/10000.0/info.TotalSeconds))});
                }).ConfigureAwait(false);
                var files=Directory.GetFiles(frames,"img*.jpg").OrderBy(f=>f,StringComparer.Ordinal).ToList();
                if(files.Count==0) throw new InvalidDataException("FFmpeg did not produce video frames");
                var groups=await Task.Run(()=>VideoOcrFrameGrouper.Group(files,options.BrightnessMinimum,options.ImageSimilarityPercent,
                    (n,total)=>{if(n%50==0 || n==total) progress?.Report(new SpeechProgress{Message="Grouping frames",Percent=n*100/total});},cancel),cancel).ConfigureAwait(false);
                var candidates=groups.Where(g=>!g.IsBlank).ToList();
                progress?.Report(new SpeechProgress{Message="OCR: "+candidates.Count+" frame groups"});
                await RecognizeAsync(candidates,job,options,cancel,progress,(index,group)=>
                {
                    if(fixText!=null) group.Text=fixText(index,group);
                    observation?.Invoke(index,group);
                }).ConfigureAwait(false);
                var lines=VideoOcrLineBuilder.Build(groups,options.FramesPerSecond,options.TextSimilarityPercent,options.MaxGapMs,options.MinDurationMs);
                progress?.Report(new SpeechProgress{Message="Refining subtitle timings"});
                await VideoOcrTimingRefiner.RefineAsync(lines,new VideoOcrTimingRefiner.Context
                {
                    VideoFileName=video,FramesFolder=frames,CoarseFps=options.FramesPerSecond,BrightnessMinimum=options.BrightnessMinimum,
                    ImageSimilarityPercent=options.ImageSimilarityPercent,CropAndScaleFilter=filter
                },(n,total)=>progress?.Report(new SpeechProgress{Message="Refining subtitle timings",Percent=n*100/total}),cancel).ConfigureAwait(false);
                var result=new Subtitle();
                var tag=options.AddAssaPositionTag ? VideoOcrLineBuilder.GetAssaAlignmentTag((options.CropXPercent+options.CropWidthPercent/2)/100,(options.CropYPercent+options.CropHeightPercent/2)/100) : "";
                foreach(var line in lines)
                {
                    var end=Math.Min(info.TotalMilliseconds,line.EndMs);
                    if(end>line.StartMs) result.Paragraphs.Add(new Paragraph(tag+line.Text,line.StartMs,end));
                }
                result.Renumber(); return result;
            }
            finally
            {
                // job is a GUID directory constructed under our own private Jobs root.
                if(Directory.Exists(job)) Directory.Delete(job,true);
            }
        }
        public static Task EnsureInstalledAsync(ClassicVideoOcrOptions options,CancellationToken cancel,IProgress<SpeechProgress> progress=null)
            => options.Engine=="CrispEmbed" ? ClassicCrispVideoOcr.EnsureInstalledAsync(options,cancel,progress) : ClassicPaddleOcr.EnsureInstalledAsync(options.Variant,cancel,progress);
        public static Task RecognizeAsync(IReadOnlyList<VideoOcrFrameGroup> groups,string job,ClassicVideoOcrOptions options,CancellationToken cancel,
            IProgress<SpeechProgress> progress=null,Action<int,VideoOcrFrameGroup> resultAvailable=null)
            => options.Engine=="CrispEmbed" ? ClassicCrispVideoOcr.RecognizeAsync(groups,job,options,cancel,progress,resultAvailable)
                : ClassicPaddleOcr.RecognizeAsync(groups,job,options.Language,options.ModelMode,options.Variant,options.BrightnessMinimum,cancel,progress,resultAvailable);
    }
}
