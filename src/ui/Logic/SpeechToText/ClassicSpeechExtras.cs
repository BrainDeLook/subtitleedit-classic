using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic.SpeechToText
{
    public sealed class ClassicSpeechAssetOption
    {
        public string Choice { get; set; }
        public string Language { get; set; }
        public string Display { get; set; }
        public SpeechModel Model { get; set; }
        public override string ToString() => Display ?? Choice;
    }
    public sealed class ClassicSpeechExtras
    {
        public List<ClassicSpeechAssetOption> Aligners { get; set; }
        public List<ClassicSpeechAssetOption> Auxiliary { get; set; }
        public static readonly ClassicSpeechExtras Catalog = Load();
        public static readonly string[] VadChoices = {"auto", "silero", "firered", "whisper-vad", "webrtc"};
        public static readonly string[] VadNames = {"Auto (Silero)", "Silero", "FireRedVAD", "Whisper-VAD", "WebRTC"};
        private static readonly SpeechEngine Crisp = new SpeechEngine {Family = "crisp"};
        private static readonly HashSet<string> CanaryLanguages = new HashSet<string>("bg hr cs da nl en et fi fr de el hu it lv lt mt pl pt ro sk sl es sv ru uk".Split(' '));
        private static readonly HashSet<string> QwenLanguages = new HashSet<string>("zh en fr de it ja ko pt ru es".Split(' '));
        private static ClassicSpeechExtras Load()
        {
            using (var stream = typeof(ClassicSpeechExtras).Assembly.GetManifestResourceStream("ClassicSpeechExtras.json"))
            using (var reader = new StreamReader(stream)) return JsonConvert.DeserializeObject<ClassicSpeechExtras>(reader.ReadToEnd());
        }
        public static bool NeedsVad(SpeechEngine engine) => engine.Family == "crisp" && (engine.Backend == "cohere" || engine.Backend == "mega-asr" || engine.Backend == "index-echo");
        public static bool CanDiarize(SpeechEngine engine) => engine.Family == "crisp" && engine.Backend != "moss-diarize";
        public static string EffectiveVad(SpeechEngine engine, ClassicSpeechOptions options)
        {
            if (engine.Backend == "index-echo" || string.IsNullOrEmpty(options.Vad) || options.Vad == "auto") return "silero";
            if (!VadChoices.Contains(options.Vad)) throw new ArgumentException("Unknown VAD: " + options.Vad);
            return options.Vad;
        }
        public static string VadPath(SpeechEngine engine, ClassicSpeechOptions options)
        {
            var choice = EffectiveVad(engine, options);
            return choice == "webrtc" ? "webrtc" : ClassicSpeechCatalog.ModelPath(Crisp, Catalog.Auxiliary.First(a => a.Choice == choice).Model);
        }
        public static string DiarizationPath => ClassicSpeechCatalog.ModelPath(Crisp, Catalog.Auxiliary.First(a => a.Choice == "sortformer").Model);
        public static async Task EnsureAsync(SpeechEngine engine, ClassicSpeechOptions options, CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            if (engine.Family != "crisp") return;
            var required = new List<string>();
            if (options.UseVad || NeedsVad(engine)) {var choice = EffectiveVad(engine, options); if (choice != "webrtc") required.Add(choice);}
            if (options.DetectSpeakers && CanDiarize(engine)) required.Add("sortformer");
            if (options.IsolateSpeech) required.Add("roformer");
            foreach (var choice in required)
            {
                var model = Catalog.Auxiliary.First(a => a.Choice == choice).Model;
                if (choice == "roformer")
                {
                    var previous = Path.Combine(ClassicDubbingSettings.Root, "models", model.Name); var target = ClassicSpeechCatalog.ModelPath(Crisp, model);
                    if (!File.Exists(target) && File.Exists(previous) && new FileInfo(previous).Length == model.Assets[0].Bytes)
                    {
                        await Nikse.SubtitleEdit.Logic.ClassicCast.ClassicVoiceAssets.CheckHashAsync(previous,model.Assets[0].Hash,cancel).ConfigureAwait(false);
                        await Task.Run(() => {cancel.ThrowIfCancellationRequested(); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(previous,target);},cancel).ConfigureAwait(false);
                    }
                }
                await ClassicSpeechDownload.EnsureModelAsync(Crisp, model, cancel, progress).ConfigureAwait(false);
            }
        }
        public static async Task<string> IsolateAsync(string wave, string job, string variant, CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            var model = Catalog.Auxiliary.First(a => a.Choice == "roformer").Model;
            var existing = Path.Combine(ClassicDubbingSettings.Root, "models", model.Name);
            var modelPath = ClassicSpeechCatalog.ModelPath(Crisp, model);
            // Reuse the verified dubbing model rather than downloading a duplicate.
            if (!File.Exists(modelPath) && File.Exists(existing) && new FileInfo(existing).Length == model.Assets[0].Bytes)
            {Directory.CreateDirectory(Path.GetDirectoryName(modelPath)); File.Copy(existing, modelPath);}
            await ClassicSpeechDownload.EnsureRuntimeAsync(Crisp, variant, cancel, progress).ConfigureAwait(false);
            await ClassicSpeechDownload.EnsureModelAsync(Crisp, model, cancel, progress).ConfigureAwait(false);
            var folder = Path.Combine(job, "isolated"); Directory.CreateDirectory(folder);
            var exe = ClassicSpeechCatalog.Executable(Crisp, variant);
            progress?.Report(new SpeechProgress {Message = "Isolating speech…"});
            await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(exe, "-m " + ClassicSpeechRunner.Quote(modelPath) + " -f " + ClassicSpeechRunner.Quote(Path.GetFullPath(wave)) + " --separate --stems vocals --sep-output-dir " + ClassicSpeechRunner.Quote(folder)) {WorkingDirectory = Path.GetDirectoryName(exe)}, cancel, progress).ConfigureAwait(false);
            var stem = Path.Combine(folder, Path.GetFileNameWithoutExtension(wave) + "_vocals.wav");
            if (!File.Exists(stem)) throw new InvalidDataException("Speech isolation produced no vocals stem");
            var output = Path.Combine(job, "speech.wav");
            await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation, "-nostdin -y -i " + ClassicSpeechRunner.Quote(stem) + " -vn -ar 16000 -ac 1 -c:a pcm_s16le " + ClassicSpeechRunner.Quote(output)), cancel, progress).ConfigureAwait(false);
            if (!File.Exists(output) || new FileInfo(output).Length < 44) throw new InvalidDataException("Speech isolation produced empty audio");
            return output;
        }
        public static IEnumerable<ClassicSpeechAssetOption> RankedAligners(string language)
        {
            language = (language ?? "auto").ToLowerInvariant();
            return Catalog.Aligners.OrderBy(a => a.Language == language ? (language == "en" ? 1.5 : 0) : a.Choice == "canary-ctc" ? (CanaryLanguages.Contains(language) || language == "auto" ? 1 : 3) : a.Choice == "qwen3-forced" ? (QwenLanguages.Contains(language) ? 2 : 4) : 5);
        }
        public static bool CanCheckSpeech(string language) => string.IsNullOrEmpty(language) || language == "auto" || CanaryLanguages.Contains(language);
        public static bool SupportsAligner(ClassicSpeechAssetOption aligner, string language) => string.IsNullOrEmpty(language) || language == "auto" || (!string.IsNullOrEmpty(aligner.Language) ? aligner.Language == language : aligner.Choice == "canary-ctc" ? CanaryLanguages.Contains(language) : QwenLanguages.Contains(language));
        public static Subtitle ProcessByActor(Subtitle source, Func<Subtitle, Subtitle> process)
        {
            if (source.Paragraphs.All(p => string.IsNullOrEmpty(p.Actor))) return process(source);
            var result = new Subtitle(source); result.Paragraphs.Clear();
            for (var i = 0; i < source.Paragraphs.Count;)
            {
                var actor = source.Paragraphs[i].Actor ?? ""; var group = new Subtitle(source); group.Paragraphs.Clear();
                do {group.Paragraphs.Add(new Paragraph(source.Paragraphs[i++], false));}
                while (i < source.Paragraphs.Count && (source.Paragraphs[i].Actor ?? "") == actor);
                var processed = process(group); foreach (var p in processed.Paragraphs) p.Actor = actor;
                result.Paragraphs.AddRange(processed.Paragraphs);
            }
            return result;
        }
    }
}
