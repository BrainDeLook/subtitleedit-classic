using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic.VideoOcr
{
    // Stable v5.2.0's Paddle 3.7 standalone archive/model selection and result-file
    // protocol. Kept separate from the legacy image OCR engine and its cache.
    public static class ClassicPaddleOcr
    {
        public static string Root => Path.Combine(Configuration.DataDirectory,"ClassicVideoOcr");
        public static string ModelFolder => Path.Combine(Root,"Paddle","3.7.0","models");
        public static string EngineFolder(string variant) => Path.Combine(Root,"Paddle","3.7.0",variant);
        public static string Executable(string variant) => Directory.Exists(EngineFolder(variant))
            ? Directory.GetFiles(EngineFolder(variant),"paddleocr.exe",SearchOption.AllDirectories).FirstOrDefault() ?? Path.Combine(EngineFolder(variant),"paddleocr.exe")
            : Path.Combine(EngineFolder(variant),"paddleocr.exe");

        public static async Task EnsureInstalledAsync(string variant,CancellationToken cancel,IProgress<SpeechProgress> progress)
        {
            if(variant!="cpu" && variant!="cuda12") throw new ArgumentException("Unsupported Paddle variant");
            if(!File.Exists(Executable(variant)) || !File.Exists(Path.Combine(EngineFolder(variant),".complete")))
                await InstallArchiveAsync(variant=="cpu" ? "PaddleOCR-CPU-v3.7.0.7z" : "PaddleOCR-GPU-v3.7.0-CUDA-12.9.7z",
                    variant=="cpu" ? "a1b597f5620d1a86cec606b50908a12fc1b215adf6807be5538b7ca6bddc9d20" : "6a2c1f17f093403c8f2f4c4c7b81148b29abe710604aca8fac403af2be173cab",
                    EngineFolder(variant),cancel,progress).ConfigureAwait(false);
            if(!File.Exists(Path.Combine(ModelFolder,".complete")))
                await InstallArchiveAsync("PaddleOCR.PP-OCRv6.support.files.VideOCR.7z","7f98a187a1d8d9b5291f3be7cd6a6b693b32ddffd75d39c05d333d8f0b3ee145",ModelFolder,cancel,progress).ConfigureAwait(false);
        }

        private static async Task InstallArchiveAsync(string name,string hash,string target,CancellationToken cancel,IProgress<SpeechProgress> progress)
        {
            var stage=Path.Combine(Path.GetTempPath(),"seocr_"+Guid.NewGuid().ToString("N").Substring(0,12));
            var archive=Path.Combine(Root,"Downloads",name);
            try
            {
                if(!ValidArchive(archive,hash))
                    await ClassicSpeechDownload.DownloadFileAsync("https://github.com/timminator/PaddleOCR-Standalone/releases/download/v3.7.0/"+name,archive,false,cancel,progress).ConfigureAwait(false);
                using(var input=File.OpenRead(archive)) using(var sha=SHA256.Create())
                    if(BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant()!=hash) throw new InvalidDataException("Paddle archive checksum mismatch");
                progress?.Report(new SpeechProgress{Message="Extracting "+name});
                await Task.Run(()=>ClassicSpeechDownload.Extract(archive,stage,cancel),cancel).ConfigureAwait(false);
                var folders=Directory.GetDirectories(stage);
                var content=folders.Length==1 && Directory.GetFiles(stage).Length==0 ? folders[0] : stage;
                // Both stable archives wrap their files in one root directory.
                if(target==ModelFolder && (!Directory.Exists(Path.Combine(content,"det")) || !Directory.Exists(Path.Combine(content,"rec"))))
                    throw new InvalidDataException("Paddle model bundle is missing detection/recognition folders");
                if(target!=ModelFolder && Directory.GetFiles(content,"paddleocr.exe",SearchOption.AllDirectories).Length==0)
                    throw new InvalidDataException("Paddle engine executable was not found");
                cancel.ThrowIfCancellationRequested();
                Directory.CreateDirectory(target);
                foreach(var file in Directory.GetFiles(content,"*",SearchOption.AllDirectories))
                {
                    cancel.ThrowIfCancellationRequested();
                    var destination=ClassicSpeechDownload.SafeArchivePath(target,file.Substring(content.Length+1));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(file,destination,true);
                }
                File.WriteAllText(Path.Combine(target,".complete"),hash);
            }
            finally
            {
                if(Directory.Exists(stage)) Directory.Delete(stage,true);
                if(File.Exists(archive)) File.Delete(archive);
            }
        }

        private static bool ValidArchive(string path,string expected)
        {
            if(!File.Exists(path)) return false;
            using(var input=File.OpenRead(path)) using(var sha=SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant()==expected;
        }

        public static string BuildArguments(string inputFolder,string outputFolder,string language,string mode,string variant)
        {
            var det=PaddleOcrModels.GetDetectionName(language,mode);
            var rec=PaddleOcrModels.GetRecName(language,mode);
            var cls=PaddleOcrModels.TextlineOrientationModelName;
            var args=new List<string>{"ocr","-i",inputFolder,"--use_textline_orientation","true","--use_doc_orientation_classify","false","--use_doc_unwarping","false","--lang",language,
                "--text_detection_model_dir",Path.Combine(ModelFolder,"det",det),"--text_detection_model_name",det,
                "--text_recognition_model_dir",Path.Combine(ModelFolder,"rec",rec),"--text_recognition_model_name",rec,
                "--textline_orientation_model_dir",Path.Combine(ModelFolder,"cls",cls),"--textline_orientation_model_name",cls,
                "--save_path",outputFolder,"--device",variant=="cpu" ? "cpu" : "gpu:0"};
            return string.Join(" ",args.Select(VideoOcrProcess.Quote));
        }

        // Newtonsoft adapter for v5.2.0's PaddleOcrResultJson: same text, confidence
        // and polygon semantics, without adding a second JSON runtime to Classic.
        public static List<PaddleOcrTextRegion> ReadRegions(string json)
        {
            var root=JObject.Parse(json); var texts=root["rec_texts"] as JArray;
            var result=new List<PaddleOcrTextRegion>();
            if(texts==null) return result;
            var scores=root["rec_scores"] as JArray; var polys=root["rec_polys"] as JArray;
            for(int i=0;i<texts.Count;i++)
            {
                var box=PaddleOcrBoundingBox.Empty;
                if(polys!=null && i<polys.Count && polys[i] is JArray points && points.Count>=4)
                    box=new PaddleOcrBoundingBox(ReadPoint(points[0]),ReadPoint(points[1]),ReadPoint(points[2]),ReadPoint(points[3]));
                result.Add(new PaddleOcrTextRegion{Text=(string)texts[i] ?? "",Confidence=scores!=null && i<scores.Count ? (double)scores[i] : 0,BoundingBox=box});
            }
            return result;
        }
        private static PaddleOcrPoint ReadPoint(JToken point) => new PaddleOcrPoint((double)point[0],(double)point[1]);

        public static async Task RecognizeAsync(IReadOnlyList<VideoOcrFrameGroup> groups,string job,string language,string mode,string variant,
            int brightness,CancellationToken cancel,IProgress<SpeechProgress> progress,Action<int,VideoOcrFrameGroup> resultAvailable=null)
        {
            var input=Path.Combine(job,"paddle-input"); var output=Path.Combine(job,"paddle-output");
            Directory.CreateDirectory(input); Directory.CreateDirectory(output);
            for(int i=0;i<groups.Count;i++)
            {
                cancel.ThrowIfCancellationRequested();
                var target=Path.Combine(input,i.ToString("D8",CultureInfo.InvariantCulture)+".jpg");
                if(brightness<=0 || !VideoOcrFrameGrouper.WriteMaskedCopy(groups[i].RepresentativeFileName,target,brightness))
                    File.Copy(groups[i].RepresentativeFileName,target);
            }
            if(groups.Count==0) return;
            var completed=new HashSet<int>();
            var run=VideoOcrProcess.RunAsync(Executable(variant),BuildArguments(input,output,language,mode,variant),job,cancel);
            try
            {
                while(!run.IsCompleted)
                {
                    cancel.ThrowIfCancellationRequested();
                    Collect(false);
                    await Task.Delay(100,cancel).ConfigureAwait(false);
                }
                await run.ConfigureAwait(false); Collect(true);
            }
            catch(OperationCanceledException)
            {
                try { await run.ConfigureAwait(false); } catch(OperationCanceledException) {}
                throw;
            }
            if(completed.Count!=groups.Count) throw new InvalidDataException("Paddle OCR did not produce a result for every input image");

            void Collect(bool final)
            {
                for(int i=0;i<groups.Count;i++)
                {
                    if(completed.Contains(i)) continue;
                    var file=Path.Combine(output,i.ToString("D8",CultureInfo.InvariantCulture)+"_res.json");
                    if(!File.Exists(file)) continue;
                    try
                    {
                        var json=File.ReadAllText(file); if(!json.TrimEnd().EndsWith("}",StringComparison.Ordinal)) continue;
                        var regions=ReadRegions(json);
                        groups[i].Text=PaddleOcrTextLayout.BuildText(regions,75,PaddleOcrModels.IsArabicScript(language),out var confidence);
                        groups[i].Confidence=confidence;
                        completed.Add(i); resultAvailable?.Invoke(i,groups[i]);
                        progress?.Report(new SpeechProgress{Message="OCR "+completed.Count+" / "+groups.Count,Percent=completed.Count*100/groups.Count});
                    }
                    catch(IOException) { if(final) throw; }
                    catch(JsonException) { if(final) throw; }
                }
            }
        }
    }
}
