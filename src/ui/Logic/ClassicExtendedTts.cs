using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicTtsRuntimeArchive
    {
        public string Variant { get; set; }
        public string Url { get; set; }
        public long Bytes { get; set; }
        public string Hash { get; set; }
    }

    // Protocol and runtime differences from the stable Subtitle Edit v5.3.0 engines.
    public static class ClassicExtendedTts
    {
        public static bool Handles(string id) => new[] { "f5", "pocket", "vibe", "index", "dots", "confucius", "moss", "omni", "kokoro", "omnicpp", "zonos" }.Contains(id);
        public static bool UsesRequestVoice(string id, string voice)
        {
            if (id == "pocket" || id == "vibe") return true;
            if (id != "moss" && id != "vox" && id != "cosy") return false;
            return IsPerLineVoice(voice);
        }
        public static bool IsPerLineVoice(string voice)
        {
            if (string.IsNullOrEmpty(voice)) return false;
            var file = Path.Combine(ClassicQwenReference.Folder, voice + ".json");
            return File.Exists(file) && Newtonsoft.Json.JsonConvert.DeserializeObject<ClassicQwenReference>(File.ReadAllText(file))?.PerLine == true;
        }
        private static readonly SemaphoreSlim RuntimeGate = new SemaphoreSlim(1, 1);
        private static string Quote(string text) => ClassicSpeechRunner.Quote(text);
        public static string RuntimeRoot(ClassicLocalTtsDefinition definition, string variant) => Path.Combine(ClassicLocalTtsSettings.Root(definition.Id), "runtime", definition.RuntimeVersion, variant);
        public static string Executable(ClassicLocalTtsDefinition definition, string variant) => Path.Combine(RuntimeRoot(definition, variant), definition.RuntimeExecutable);
        public static int OdeSteps(string id, ClassicLocalTtsSettings settings) => settings.OdeSteps <= 0 ? (id == "dots" ? 16 : 20) : Math.Max(id == "dots" ? 8 : 10, Math.Min(id == "dots" ? 32 : 40, settings.OdeSteps));

        public static async Task EnsureRuntimeAsync(ClassicLocalTtsDefinition definition, string variant, CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            var artifact = definition.RuntimeArchives.FirstOrDefault(a => a.Variant == variant) ?? throw new ArgumentException("Unsupported compute variant for " + definition.Name);
            await RuntimeGate.WaitAsync(cancel).ConfigureAwait(false);
            try
            {
                if (File.Exists(Executable(definition, variant))) return;
                var root = RuntimeRoot(definition, variant);
                var staging = root + "." + Guid.NewGuid().ToString("N") + ".staging";
                var archive = staging + ".zip";
                try
                {
                    await ClassicVoiceAssets.EnsureFileAsync(artifact.Url, archive, artifact.Bytes, artifact.Hash, false, cancel, progress).ConfigureAwait(false);
                    await Task.Run(() => ClassicSpeechDownload.Extract(archive, staging, cancel), cancel).ConfigureAwait(false);
                    var exe = Directory.GetFiles(staging, definition.RuntimeExecutable, SearchOption.AllDirectories).SingleOrDefault();
                    if (exe == null) throw new InvalidDataException("The runtime archive is missing " + definition.RuntimeExecutable);
                    cancel.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(Path.GetDirectoryName(root));
                    if (!Directory.Exists(root)) Directory.Move(Path.GetDirectoryName(exe), root);
                    else
                    {
                        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(exe), "*", SearchOption.AllDirectories))
                        {
                            var destination = ClassicSpeechDownload.SafeArchivePath(root, file.Substring(Path.GetDirectoryName(exe).Length + 1));
                            Directory.CreateDirectory(Path.GetDirectoryName(destination));
                            File.Copy(file, destination, true);
                        }
                    }
                }
                finally
                {
                    if (File.Exists(archive)) File.Delete(archive);
                    if (Directory.Exists(staging)) Directory.Delete(staging, true);
                }
            }
            finally { RuntimeGate.Release(); }
        }

        public static Dictionary<string, object> Payload(ClassicLocalTtsDefinition definition, ClassicLocalTtsSettings settings, string text, string voice, bool imported)
        {
            if (definition.Id == "kokoro")
            {
                if (!definition.Presets.Contains(voice)) throw new ArgumentException("Select a Kokoro preset voice.");
                return new Dictionary<string, object> { { "text", text }, { "voice", voice } };
            }
            var payload = new Dictionary<string, object> { { "input", text }, { "response_format", "wav" } };
            if (definition.SupportsSpeed) payload["speed"] = Math.Max(0.25, Math.Min(4, settings.Speed));
            if (UsesRequestVoice(definition.Id, voice)) payload["voice"] = voice;
            if (definition.Id == "confucius" || definition.Id == "omni" || definition.Id == "zonos")
            {
                if (!definition.Languages.Any(l => l.Split('|')[0] == settings.Language)) throw new ArgumentException("Select a supported target language for " + definition.Name);
                if (settings.Language != "auto") payload["language"] = settings.Language;
            }
            if (imported)
            {
                ClassicQwenReference.Validate(voice, definition.RequiresReferenceTranscript);
                payload["consent_attestation"] = "I have the speaker's consent, or it is my own voice.";
                payload["marking_attestation"] = "I will disclose that this audio is AI-generated.";
                payload["spoken_disclaimer"] = false;
            }
            return payload;
        }

        private static string Backend(ClassicLocalTtsDefinition definition, ClassicLocalTtsSettings settings)
        {
            if (definition.Id != "pocket") return definition.Backend;
            if (settings.Model.StartsWith("German")) return "pocket-tts-de";
            if (settings.Model.StartsWith("Spanish")) return "pocket-tts-es";
            if (settings.Model.StartsWith("Italian")) return "pocket-tts-it";
            if (settings.Model.StartsWith("Portuguese")) return "pocket-tts-pt";
            if (settings.Model.StartsWith("French")) return "pocket-tts-fr";
            return "pocket-tts";
        }

        public static string ServerArguments(ClassicLocalTtsDefinition definition, ClassicLocalTtsSettings settings, string voice, string transcript, string job, int port)
        {
            var models = Path.Combine(ClassicLocalTtsSettings.Root(definition.Id), "models");
            var files = definition.Model(settings.Model).Files;
            var model = Path.Combine(models, files[0].File);
            if (definition.Id == "kokoro") return "-m " + Quote(model) + " -V " + Quote(Path.Combine(models, files[1].File)) + " --host 127.0.0.1 --port " + port;
            var args = "--server --backend " + Backend(definition, settings) + " -m " + Quote(model);
            if (definition.Codec) args += " --codec-model " + Quote(Path.Combine(models, files[1].File));
            args += " --host 127.0.0.1 --port " + port + " --voice-dir " + Quote(job);
            var imported = !definition.Presets.Contains(voice);
            if (imported && !UsesRequestVoice(definition.Id,voice)) args += " --voice " + Quote(Path.Combine(job, voice + ".wav"));
            if (imported && definition.ReferenceText)
            {
                if (definition.RequiresReferenceTranscript && string.IsNullOrWhiteSpace(transcript)) throw new ArgumentException("Provide the exact reference transcript for " + definition.Name);
                if (!string.IsNullOrWhiteSpace(transcript)) args += " --ref-text " + Quote(transcript);
            }
            if (definition.Id == "zonos") args += " -l " + Quote(settings.Language);
            if (definition.Id == "confucius") args += " --tts-steps " + OdeSteps(definition.Id, settings);
            if (definition.Id == "moss" && settings.Language != "auto")
            {
                var names = new Dictionary<string, string> { { "he", "hebrew" }, { "hu", "hungarian" }, { "fa", "persian" }, { "cs", "czech" }, { "da", "danish" }, { "sv", "swedish" }, { "el", "greek" } };
                args += " -l " + Quote(names.TryGetValue(settings.Language, out var name) ? name : settings.Language);
            }
            return args;
        }

        public static async Task SpeakStandaloneAsync(ClassicLocalTtsDefinition definition, ClassicLocalTtsSettings settings, string text, string voice, string job, string output, CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            var model = definition.Model(settings.Model);
            var models = Path.Combine(ClassicLocalTtsSettings.Root(definition.Id), "models");
            var args = "--model " + Quote(Path.Combine(models, model.Files[0].File)) + " --codec " + Quote(Path.Combine(models, model.Files[1].File)) + " -o " + Quote(output);
            if (settings.Language != "auto") args += " --lang " + Quote(settings.Language);
            if (!definition.Presets.Contains(voice))
            {
                var transcript = Path.Combine(job, voice + ".txt");
                File.Copy(Path.Combine(ClassicQwenReference.Folder, voice + ".txt"), transcript, true);
                args += " --ref-wav " + Quote(Path.Combine(job, voice + ".wav")) + " --ref-text " + Quote(transcript);
            }
            else if (!string.IsNullOrWhiteSpace(settings.Instructions)) args += " --instruct " + Quote(settings.Instructions.Trim());
            var start = new ProcessStartInfo(Executable(definition, settings.Variant), args) { WorkingDirectory = RuntimeRoot(definition, settings.Variant) };
            await ClassicSpeechRunner.RunProcessAsync(start, cancel, progress, text).ConfigureAwait(false);
        }
    }
}
