using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.AudioToText;
using Nikse.SubtitleEdit.Core.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nikse.SubtitleEdit.Logic.SpeechToText
{
    public sealed class SpeechModel
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public string[] Urls { get; set; }
        public bool Aligner { get; set; }
        public ClassicLocalTtsFile[] Assets { get; set; }
        public override string ToString() => Name + (string.IsNullOrEmpty(Size) ? "" : "  (" + Size + ")");
    }

    public sealed class SpeechLanguage
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public override string ToString() => Name;
    }

    public sealed class SpeechEngine
    {
        public string Name { get; set; }
        public string Family { get; set; }
        public string Backend { get; set; }
        public bool NativeTimings { get; set; }
        public List<SpeechLanguage> Languages { get; set; }
        public List<SpeechModel> Models { get; set; }
        public override string ToString() => Name.Replace("Crisp ASR ", "");
    }

    public static class ClassicSpeechCatalog
    {
        public const string Crisp = "Crisp ASR";
        public const string Qwen = "Qwen3 ASR CPP";
        public const string WhisperX = "WhisperX";
        public const string CppVulkan = "Whisper CPP Vulkan";
        public const string CompatibleApi = "OpenAI-compatible / vLLM";
        public static readonly string[] CloudNames={"OpenRouter ASR","DashScope Qwen3 ASR","Google Cloud ASR v2"};
        public static string CloudId(string name)=>name==CloudNames[0]?"openrouter":name==CloudNames[1]?"dashscope":"google";
        public static readonly List<SpeechEngine> Engines = Load();
        public static string Root => Path.Combine(Configuration.DataDirectory, "ClassicSpeechToText");

        private static List<SpeechEngine> Load()
        {
            using (var stream = typeof(ClassicSpeechCatalog).Assembly.GetManifestResourceStream("ClassicSpeechModels.json"))
            {
                if (stream == null) throw new InvalidOperationException("Speech-to-text model catalog is missing.");
                using (var reader = new StreamReader(stream))
                {
                    return JsonConvert.DeserializeObject<List<SpeechEngine>>(reader.ReadToEnd());
                }
            }
        }

        public static SpeechEngine Get(string selection, string backend)
        {
            if(CloudNames.Contains(selection))return new SpeechEngine{Name=selection,Family="compatible-api",Backend=CloudId(selection),NativeTimings=true,Languages=new[]{new SpeechLanguage{Code="auto",Name="Auto detect"}}.Concat(WhisperLanguage.Languages.Select(l=>new SpeechLanguage{Code=l.Code,Name=l.Name})).ToList(),Models=new List<SpeechModel>{new SpeechModel{Name=ClassicCloudAsrSettings.Load(CloudId(selection)).Api.Model}}};
            if(selection==CompatibleApi)return new SpeechEngine{Name=CompatibleApi,Family="compatible-api",Backend="api",NativeTimings=true,Languages=new[]{new SpeechLanguage{Code="auto",Name="Auto detect"}}.Concat(WhisperLanguage.Languages.Select(l=>new SpeechLanguage{Code=l.Code,Name=l.Name})).ToList(),Models=new List<SpeechModel>{new SpeechModel{Name=ClassicCompatibleAsrSettings.Load().Model}}};
            if (selection == Crisp) return Engines.First(e => e.Family == "crisp" && (e.Backend == backend || e.Name == backend));
            if (selection == Qwen) return Engines.First(e => e.Family == "qwen");
            if (selection == WhisperX || selection == CppVulkan)
            {
                var cpp = selection == CppVulkan;
                var sourceModels = cpp ? new WhisperCppModel().Models : new WhisperPurfviewFasterWhisperModel().Models.Concat(new[]{ClassicLegacyAsr.AnimeModel()}).ToArray();
                return new SpeechEngine
                {
                    Name = selection, Family = cpp ? "cpp" : "whisperx", Backend = "", NativeTimings = true,
                    Languages = new[] { new SpeechLanguage { Code = "auto", Name = "Auto detect" } }.Concat(
                        WhisperLanguage.Languages.Select(l => new SpeechLanguage { Code = l.Code, Name = l.Name })).ToList(),
                    Models = sourceModels.Where(m => !m.Name.EndsWith(".nb", StringComparison.Ordinal)).Select(m => new SpeechModel
                    {
                        Name = cpp ? "ggml-" + m.Name + ".bin" : m.Name,
                        Size = m.Size, Urls = m.Urls
                    }).ToList()
                };
            }
            return null;
        }

        public static string ModelPath(SpeechEngine engine, SpeechModel model) =>
            Path.Combine(Root, "Models", engine.Family == "crisp" ? "crisp" : engine.Family, Path.GetFileName(model.Name));

        public static string WhisperXRussianAligner => Path.Combine(Root, "Models", "whisperx-russian-aligner", "2329100508896c6d9b157019803ab5601e6f3406");

        public static string EngineDirectory(SpeechEngine engine, string variant) =>
            Path.Combine(Root, "Engines", engine.Family, PinnedVersion(engine), variant);

        public static string PinnedVersion(SpeechEngine engine) => engine.Family == "crisp" ? "0.8.43" :
            engine.Family == "qwen" ? "0.1.8" : engine.Family == "cpp" ? "1.9.3" : "1.0.1";

        public static string RuntimeUrl(SpeechEngine engine, string variant)
        {
            if (engine.Family == "crisp")
            {
                if (!new[] { "cpu", "cpu-legacy", "vulkan", "cuda", "cuda13" }.Contains(variant)) throw new ArgumentException("Unsupported Crisp ASR build");
                return "https://github.com/CrispStrobe/CrispASR/releases/download/v" + PinnedVersion(engine) + "/crispasr-windows-x86_64-" + variant + ".zip";
            }
            if (engine.Family == "qwen")
                return "https://github.com/niksedk/qwen3-asr.cpp/releases/download/v0.1.8/qwen3-asr-cpp-win64" + (variant == "vulkan" ? "-vulkan" : "") + ".zip";
            if (engine.Family == "cpp") return "https://github.com/SubtitleEdit/support-files/releases/download/whispercpp-193/whisper-vulkan-x64.zip";
            if (engine.Family == "whisperx") return "https://github.com/SubtitleEdit/support-files/releases/download/whisperx-standalone-101/whisperx-standalone-windows-x64.7z";
            throw new ArgumentException("Unknown speech engine");
        }

        public static string Executable(SpeechEngine engine, string variant)
        {
            var directory = EngineDirectory(engine, variant);
            var names = engine.Family == "crisp" ? new[] { "crispasr.exe" } : engine.Family == "qwen" ?
                new[] { "qwen3-asr-cli.exe", "qwen3-asr.exe", "main.exe" } : engine.Family == "cpp" ?
                new[] { "whisper-cli.exe", "main.exe" } : new[] { "whisperx.exe", "whisperx-standalone.exe" };
            if (Directory.Exists(directory))
                foreach (var name in names)
                {
                    var found = Directory.GetFiles(directory, name, SearchOption.AllDirectories).FirstOrDefault();
                    if (found != null) return found;
                }
            return Path.Combine(directory, names[0]);
        }
    }

    public sealed class ClassicSpeechOptions
    {
        public string Engine { get; set; } = ClassicSpeechCatalog.Crisp;
        public string Backend { get; set; } = "parakeet";
        public string Variant { get; set; } = "vulkan";
        public string Language { get; set; } = "auto";
        public string Model { get; set; } = "";
        public string Aligner { get; set; } = "";
        public bool UseVad { get; set; }
        public string Vad { get; set; } = "auto";
        public bool IsolateSpeech { get; set; }
        public bool DetectSpeakers { get; set; }
        public bool WhisperXAlignWords { get; set; } = true;
        public bool TranslateToEnglish { get; set; }
        public bool RetryWithoutVad { get; set; } = true;
        public bool RemoveNonSpeech { get; set; }
        public bool RemoveRepeats { get; set; }
        public Dictionary<string, string> ExtraArguments { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> LastModels { get; set; } = new Dictionary<string, string>();

        public static ClassicSpeechOptions Load()
        {
            var path = Path.Combine(ClassicSpeechCatalog.Root, "settings.json");
            try
            {
                var options = File.Exists(path) ? JsonConvert.DeserializeObject<ClassicSpeechOptions>(File.ReadAllText(path)) ?? new ClassicSpeechOptions() : new ClassicSpeechOptions();
                options.ExtraArguments = options.ExtraArguments ?? new Dictionary<string, string>();
                options.LastModels = options.LastModels ?? new Dictionary<string, string>();
                return options;
            }
            catch (IOException) { return new ClassicSpeechOptions(); }
            catch (JsonException) { return new ClassicSpeechOptions(); }
        }

        public void Save()
        {
            Directory.CreateDirectory(ClassicSpeechCatalog.Root);
            var path = Path.Combine(ClassicSpeechCatalog.Root, "settings.json");
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(this, Formatting.Indented));
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
    }
}
