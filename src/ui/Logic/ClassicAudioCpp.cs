using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.SpeechToText;
namespace Nikse.SubtitleEdit.Logic
{
    // Runtime and protocol pinned to stable Subtitle Edit v5.3.0.
    public static class ClassicAudioCpp
    {
        private const string Release = "audiocpp-indextts25-2026-10-13";
        private static readonly Dictionary<string,string> Hashes = new Dictionary<string,string> {
            {"cpu","2d5d3ffdb9e95c816f214ecdfdb40697ccce8d24dfd742befc05ac79fde93daf"},
            {"cuda","bcb0cc492fedb356398ca1a10708ca92ec4e5f8cbda439b6eee40c5ad820b0a4"},
            {"vulkan","d79517505418d4cbdf0d1d266a99cb983515920ec81b45891c50abe6a8202a48"},
        };
        private static readonly Dictionary<string,long> Sizes=new Dictionary<string,long>{{"cpu",6179099L},{"cuda",837110935L},{"vulkan",33150010L}};
        public static string Root(string variant) => Path.Combine(Configuration.DataDirectory, "audio.cpp", Release, variant);
        public static string Executable(string variant) => Path.Combine(Root(variant), "audiocpp_server.exe");
        private static readonly SemaphoreSlim InstallLock = new SemaphoreSlim(1,1);
        public static async Task EnsureAsync(string variant, CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            if (!Hashes.ContainsKey(variant)) throw new ArgumentException("audio.cpp supports CPU, Vulkan or CUDA (not CUDA13).");
            await InstallLock.WaitAsync(cancel).ConfigureAwait(false);
            try {
                if (File.Exists(Executable(variant))) return;
                var root = Root(variant); var staging = root + "." + Guid.NewGuid().ToString("N") + ".staging";
                var archive = staging + ".zip";
                try {
                    var name = "audiocpp-indextts25-windows-x86_64-" + variant + ".zip";
                    await ClassicVoiceAssets.EnsureFileAsync("https://github.com/SubtitleEdit/support-files/releases/download/" + Release + "/" + name, archive, Sizes[variant], Hashes[variant], false, cancel, progress).ConfigureAwait(false);
                    await Task.Run(() => ClassicSpeechDownload.Extract(archive, staging, cancel), cancel).ConfigureAwait(false);
                    var exe = Directory.GetFiles(staging,"audiocpp_server.exe",SearchOption.AllDirectories).SingleOrDefault();
                    if (exe == null) throw new InvalidDataException("audio.cpp server was not found in the archive.");
                    cancel.ThrowIfCancellationRequested(); Directory.CreateDirectory(Path.GetDirectoryName(root));
                    var source = Path.GetDirectoryName(exe);
                    if (!Directory.Exists(root)) Directory.Move(source,root);
                    else foreach(var file in Directory.GetFiles(source,"*",SearchOption.AllDirectories)) {
                        var destination=ClassicSpeechDownload.SafeArchivePath(root,file.Substring(source.Length+1));
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(file,destination,true);
                    }
                } finally {if(File.Exists(archive))File.Delete(archive);if(Directory.Exists(staging))Directory.Delete(staging,true);}
            } finally {InstallLock.Release();}
        }
        public static string[] License(string id)
        {
            if(id=="fish")return new[]{"fish-audio-research-license-2026-09","Fish Audio S2 Pro weights: Fish Audio Research License. Research and non-commercial use; commercial use requires a separate written license. Preserve the license notice when redistributing derivatives. Read the full license before accepting.","https://huggingface.co/fishaudio/s2-pro/blob/main/LICENSE.md"};
            if(id=="higgs")return new[]{"boson-higgs-tts3-nc-license-2026-09","Higgs Audio v3 weights: Boson AI Research and Non-Commercial License. Production deployment, hosted APIs, embedding in a product or reselling models/fine-tunes requires a separate commercial license. Read the full license before accepting.","https://huggingface.co/bosonai/higgs-tts-3-4b/blob/main/LICENSE"};
            if(id=="breeze")return new[]{"breezeblue-research-non-commercial-1.1","Breeze TTS 2: BreezeBlue Research and Non-Commercial License. The non-commercial restriction applies to the weights and generated audio. Read the full license before accepting.","https://huggingface.co/BreezeBlue/Breeze-TTS-2/blob/main/LICENSE"};
            if(id=="index25")return new[]{"bilibili-model-use-license-2026-08","IndexTTS 2.5 weights use the bilibili Model Use License. These terms are separate from the audio.cpp runtime license and include usage and redistribution conditions. Read the full license before accepting.","https://huggingface.co/IndexTeam/IndexTTS-2.5/blob/main/LICENSE"};
            return null;
        }
        public static string FireRedLanguage(string language)
        {
            var codes="en|English,ar|Arabic,yue|Cantonese,zh|Chinese,cs|Czech,nl|Dutch,fi|Finnish,fr|French,de|German,el|Greek,hi|Hindi,id|Indonesian,it|Italian,ja|Japanese,ko|Korean,pl|Polish,pt|Portuguese,ro|Romanian,ru|Russian,es|Spanish,th|Thai,tr|Turkish,uk|Ukrainian,vi|Vietnamese";
            return codes.Split(',').Select(x=>x.Split('|')).FirstOrDefault(x=>x[0]==language || x[1]==language)?[1] ?? language;
        }
        public static object Config(ClassicLocalTtsDefinition definition, ClassicLocalTtsSettings settings, int port) => new {
            host="127.0.0.1", port, backend=settings.Variant, threads=Math.Max(1,Math.Min(8,Environment.ProcessorCount/2)), lazy_load=true,
            models=new[]{new {id=definition.Id, family=definition.Backend, path=Path.Combine(ClassicLocalTtsSettings.Root(definition.Id),"models",definition.Model(settings.Model).Files[0].File), task=definition.Id=="firered" ? "clon" : "tts", mode="offline"}}
        };
        public static Dictionary<string,object> Payload(ClassicLocalTtsDefinition definition, ClassicLocalTtsSettings settings, string text, string voice, string transcript, string referenceFolder)
        {
            var options = new Dictionary<string,object>();
            var body = new Dictionary<string,object>{{"model",definition.Id},{"input",text},{"response_format","wav"}};
            if(definition.Id=="kugel") {
                if(!definition.Presets.Contains(voice))throw new ArgumentException("KugelAudio supports preset voices only.");
                options["voice_id"]=voice;
            } else if(!definition.Presets.Contains(voice)) {
                if(definition.Id=="firered" && string.IsNullOrWhiteSpace(transcript))throw new ArgumentException("FireRedTTS3 requires the exact reference transcript.");
                body["voice_ref"]=new Dictionary<string,object>{{"type","path"},{"path",Path.Combine(referenceFolder,voice+".wav")}};
                if(!string.IsNullOrWhiteSpace(transcript))options["reference_text"]=transcript;
                else if(definition.Id=="fish" || definition.Id=="breeze")options["reference_text"]=" ";
            }
            if(definition.Id=="firered") {
                if(!definition.Languages.Any(x=>x.Split('|')[0]==FireRedLanguage(settings.Language)))throw new ArgumentException("Select a supported FireRedTTS3 language.");
                options["language"]=FireRedLanguage(settings.Language);
            }
            if(definition.Id=="index25" && !definition.Languages.Any(x=>x.Split('|')[0]==settings.Language))throw new ArgumentException("IndexTTS 2.5 supports Auto, Chinese, English, Japanese, Spanish or Arabic. Use Fish/Higgs for Russian.");
            if(definition.Id=="index25" && settings.Language!="auto")options["language"]=settings.Language;
            if(definition.Id=="index25" && Math.Abs(settings.Speed-1)>0.001)options["duration_factor"]=Math.Max(0.5,Math.Min(2,1/settings.Speed));
            if(definition.Id=="index25" && settings.Emotion!="neutral") {
                var emotions=new[]{"happy","angry","sad","afraid","disgusted","melancholic","surprised","calm"};var index=Array.IndexOf(emotions,settings.Emotion);if(index>=0){var vector=new double[8];vector[index]=1;options["emotion_vector"]=vector;options["emotion_alpha"]=1.0;}
            }
            if(definition.Id=="breeze" && !string.IsNullOrWhiteSpace(settings.Instructions))options["instruction"]=settings.Instructions;
            if(options.Count>0)body["options"]=options;
            return body;
        }
    }
}
