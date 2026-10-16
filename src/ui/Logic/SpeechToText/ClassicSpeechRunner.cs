using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic.SpeechToText
{
    public sealed class SpeechProgress
    {
        public string Message { get; set; }
        public int Percent { get; set; } = -1;
    }

    public static class ClassicSpeechRunner
    {
        private static readonly Regex Percent = new Regex(@"(?:progress\s*=\s*|^\s*)(\d+(?:\.\d+)?)\s*%", RegexOptions.Compiled);
        public static string Quote(string value)
        {
            // Windows argv quoting; do not invoke a shell for engine arguments.
            var result = new StringBuilder("\"");
            var slashes = 0;
            foreach (var ch in value ?? "")
            {
                if (ch == '\\') { slashes++; continue; }
                result.Append('\\', ch == '"' ? slashes * 2 + 1 : slashes);
                result.Append(ch); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        public static async Task RunProcessAsync(ProcessStartInfo start, CancellationToken cancel, IProgress<SpeechProgress> progress, string standardInput = null)
        {
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;
            if (standardInput != null)
            {
                start.RedirectStandardInput = true;
            }
            var errors = new StringBuilder();
            using (var process = new Process { StartInfo = start })
            {
                cancel.ThrowIfCancellationRequested();
                if (!process.Start()) throw new IOException("Unable to start speech engine.");
                using (cancel.Register(() => KillTree(process)))
                {
                    var stdout = ReadLines(process.StandardOutput, errors, progress);
                    var stderr = ReadLines(process.StandardError, errors, progress);
                    if (standardInput != null)
                    {
                        var inputBytes = new UTF8Encoding(false).GetBytes(standardInput);
                        await process.StandardInput.BaseStream.WriteAsync(inputBytes, 0, inputBytes.Length, cancel).ConfigureAwait(false);
                        process.StandardInput.BaseStream.Close();
                    }
                    await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);
                    await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
                    cancel.ThrowIfCancellationRequested();
                    if (process.ExitCode != 0) throw new InvalidOperationException(Path.GetFileName(start.FileName) + " exited with code " + process.ExitCode + Environment.NewLine + errors);
                }
            }
        }

        private static async Task ReadLines(StreamReader reader, StringBuilder tail, IProgress<SpeechProgress> progress)
        {
            string line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                lock (tail)
                {
                    tail.AppendLine(line);
                    if (tail.Length > 16000) tail.Remove(0, tail.Length - 12000);
                }
                var match = Percent.Match(line);
                var percent = match.Success ? Math.Min(100, (int)double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)) : -1;
                progress?.Report(new SpeechProgress { Message = line, Percent = percent });
            }
        }

        private static void KillTree(Process process)
        {
            try
            {
                if (process.HasExited) return;
                using (var kill = Process.Start(new ProcessStartInfo("taskkill.exe", "/PID " + process.Id.ToString(CultureInfo.InvariantCulture) + " /T /F")
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true }))
                {
                    kill?.WaitForExit(5000);
                }
                if (!process.HasExited) process.Kill();
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }

        public static List<string> Arguments(SpeechEngine engine, SpeechModel model, ClassicSpeechOptions options,
            string inputWave, string outputPrefix, string alignerPath, bool useVad)
        {
            var args = new List<string>();
            var modelPath = ClassicSpeechCatalog.ModelPath(engine, model);
            if (engine.Family == "crisp")
            {
                args.AddRange(new[] { "--backend", engine.Backend, engine.Backend == "index-echo" ? "--target-lang" : "-l", options.Language, "-m", modelPath,
                    "-f", inputWave, "--output-srt", "--output-file", outputPrefix, "--print-progress" });
                // Native ASR chunks can span a minute of uninterrupted speech.
                // Split while the engine still has word timestamps, before SRT
                // discards that information; later text splitting cannot recover it.
                var maxChars = Configuration.Settings.Tools.AudioToTextLineMaxChars;
                if (maxChars < 20 || maxChars > 120) maxChars = 86;
                // Index-Echo emits original + translation in one cue. Splitting that
                // bilingual text here breaks it into separate cues and loses their pairing.
                if (engine.Backend != "index-echo") args.AddRange(new[] { "--max-len", maxChars.ToString(CultureInfo.InvariantCulture), "--split-on-word", "--split-on-punct" });
                if (!string.IsNullOrEmpty(alignerPath)) args.AddRange(new[] { "-am", alignerPath });
                if (!engine.NativeTimings && string.IsNullOrEmpty(alignerPath)) throw new InvalidOperationException("This backend requires a forced aligner for subtitle timings.");
                if (useVad) args.AddRange(new[] { "--vad", "--vad-model", ClassicSpeechExtras.VadPath(engine, options) });
                if (options.DetectSpeakers && ClassicSpeechExtras.CanDiarize(engine))
                    args.AddRange(new[] {"--diarize", "--diarize-method", "sortformer", "--diarize-model", ClassicSpeechExtras.DiarizationPath});
            }
            else if (engine.Family == "qwen")
            {
                if (string.IsNullOrEmpty(alignerPath)) throw new InvalidOperationException("Qwen3 requires the forced-aligner model.");
                args.AddRange(new[] { "-m", modelPath, "--aligner-model", alignerPath, "-f", inputWave,
                    "--transcribe-align", "-o", outputPrefix + ".json" });
                if (options.Language != "auto") args.AddRange(new[] { "-l", options.Language });
            }
            else if (engine.Family == "cpp")
            {
                args.AddRange(new[] { "-m", modelPath, "-l", options.Language, "-f", inputWave, "-osrt", "-of", outputPrefix });
                if (options.TranslateToEnglish) args.Add("-tr");
            }
            else if (engine.Family == "whisperx")
            {
                if (options.Language != "auto") args.AddRange(new[] { "--language", options.Language });
                args.AddRange(new[] { "--model", model.Name==ClassicLegacyAsr.AnimeName ? ClassicLegacyAsr.AnimeFolder : model.Name, "--output_format", "srt", "--output_dir", Path.GetDirectoryName(outputPrefix),
                    "--model_dir", Path.Combine(ClassicSpeechCatalog.Root, "Models", "whisperx"), "--device", "cpu", "--compute_type", "int8", "--verbose", "False", inputWave });
                if (options.TranslateToEnglish && model.Name!=ClassicLegacyAsr.AnimeName) args.AddRange(new[] { "--task", "translate" });
                else if (!options.WhisperXAlignWords) args.Add("--no_align");
                else if (options.Language == "ru") args.AddRange(new[] { "--align_model", ClassicSpeechCatalog.WhisperXRussianAligner });
            }
            else throw new ArgumentException("Unknown speech-to-text engine.");
            return args;
        }

        public static async Task<Subtitle> TranscribeAsync(SpeechEngine engine, SpeechModel model, ClassicSpeechOptions options,
            string waveFile, string jobFolder, string alignerPath, CancellationToken cancel, IProgress<SpeechProgress> progress,
            string executableOverride = null)
        {
            jobFolder = Path.GetFullPath(jobFolder);
            Directory.CreateDirectory(jobFolder);
            if(engine.Family=="compatible-api")using(var api=new ClassicCompatibleAsr(ClassicCompatibleAsrSettings.Load()))return await api.TranscribeAsync(waveFile,jobFolder,options.Language,cancel,progress).ConfigureAwait(false);
            if (executableOverride == null && engine.Family == "crisp")
            {
                await ClassicSpeechExtras.EnsureAsync(engine, options, cancel, progress).ConfigureAwait(false);
                if (options.IsolateSpeech) waveFile = await ClassicSpeechExtras.IsolateAsync(waveFile, jobFolder, options.Variant, cancel, progress).ConfigureAwait(false);
            }
            if (engine.Family == "whisperx" && options.Language == "ru" && options.WhisperXAlignWords && !options.TranslateToEnglish)
                await ClassicSpeechDownload.EnsureWhisperXRussianAlignerAsync(cancel, progress).ConfigureAwait(false);
            var output = Path.Combine(jobFolder, "transcript");
            var useVad = engine.Family == "crisp" && (options.UseVad || ClassicSpeechExtras.NeedsVad(engine));
            for (var attempt = 0; attempt < 2; attempt++)
            {
                cancel.ThrowIfCancellationRequested();
                var arguments = Arguments(engine, model, options, waveFile, output, alignerPath, useVad);
                var extra = options.ExtraArguments.TryGetValue(engine.Name, out var value) ? value : "";
                var start = new ProcessStartInfo(executableOverride ?? ClassicSpeechCatalog.Executable(engine, options.Variant),
                    string.Join(" ", arguments.Select(Quote)) + (string.IsNullOrWhiteSpace(extra) ? "" : " " + extra)) { WorkingDirectory = jobFolder };
                start.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                start.EnvironmentVariables["PYTHONUTF8"] = "1";
                start.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
                start.EnvironmentVariables["MPLCONFIGDIR"] = Path.Combine(ClassicSpeechCatalog.Root, "WhisperXFontCache");
                start.EnvironmentVariables["HF_HOME"] = Path.Combine(ClassicSpeechCatalog.Root, "Models", "hf-cache");
                start.EnvironmentVariables["TORCH_HOME"] = Path.Combine(ClassicSpeechCatalog.Root, "Models", "torch-cache");
                var ffmpeg = Configuration.Settings.General.FFmpegLocation;
                if (File.Exists(ffmpeg)) start.EnvironmentVariables["PATH"] = Path.GetDirectoryName(ffmpeg) + ";" + start.EnvironmentVariables["PATH"];
                await RunProcessAsync(start, cancel, progress).ConfigureAwait(false);
                var file = engine.Family == "qwen" ? output + ".json" : output + ".srt";
                if (engine.Family == "whisperx") file = Path.Combine(jobFolder, Path.GetFileNameWithoutExtension(waveFile) + ".srt");
                if (!File.Exists(file))
                {
                    if (attempt == 0 && useVad && options.RetryWithoutVad)
                    {
                        useVad = false;
                        progress?.Report(new SpeechProgress { Message = "No transcript with VAD; retrying without VAD." });
                        continue;
                    }
                    throw new InvalidDataException("The engine finished without producing a transcript. Check its log and output parameters.");
                }
                var subtitle = engine.Family == "qwen" ? ParseWordJson(File.ReadAllText(file)) : ParseSrt(File.ReadAllText(file));
                if (engine.Backend == "index-echo") foreach (var p in subtitle.Paragraphs) p.Text = IndexEchoTranslation(p.Text);
                if (engine.Family == "crisp" && (options.DetectSpeakers || engine.Backend == "moss-diarize"))
                    Nikse.SubtitleEdit.Logic.ClassicCast.SpeakerLabelParser.MoveLabelsToActors(subtitle);
                if (subtitle.Paragraphs.Count > 0 || !useVad || !options.RetryWithoutVad) return subtitle;
                useVad = false;
                File.Delete(file);
                progress?.Report(new SpeechProgress { Message = "No speech found with VAD; retrying without VAD." });
            }
            return new Subtitle();
        }

        public static Subtitle ParseSrt(string text)
        {
            var result = new Subtitle();
            new SubRip().LoadSubtitle(result, text.TrimStart('\uFEFF').Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList(), null);
            Validate(result);
            return result;
        }

        public static string IndexEchoTranslation(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n'); if (lines.Length < 2) return text;
            var result = string.Join(Environment.NewLine, lines.Skip(1)).Trim();
            if (result.StartsWith("「") && result.EndsWith("」") && result.IndexOf("「", 1, StringComparison.Ordinal) < 0) result = result.Substring(1, result.Length - 2).Trim();
            if (Nikse.SubtitleEdit.Logic.ClassicCast.SpeakerLabelParser.TrySplit(lines[0], out var speaker, out _) && !Nikse.SubtitleEdit.Logic.ClassicCast.SpeakerLabelParser.TrySplit(result, out _, out _)) result = "(" + speaker + ") " + result;
            return result;
        }

        public static Subtitle ParseWordJson(string text)
        {
            var root = JObject.Parse(text);
            var words = root["words"] as JArray;
            if (words == null) throw new InvalidDataException("No timed words in Qwen3 JSON.");
            var result = new Subtitle();
            var phrase = new StringBuilder();
            double start = 0, end = 0;
            foreach (var word in words)
            {
                var value = ((string)word["word"] ?? "").Trim();
                var from = (double?)word["start"] ?? double.NaN;
                var to = (double?)word["end"] ?? double.NaN;
                if (!Finite(from) || !Finite(to) || from < 0 || to < from) throw new InvalidDataException("Invalid word timestamp in Qwen3 JSON.");
                if (value.Length == 0) continue;
                if (phrase.Length > 0 && (from - end > .5 || phrase.Length + value.Length > 80 || to - start > 6))
                {
                    result.Paragraphs.Add(new Paragraph(phrase.ToString(), start * 1000, end * 1000)); phrase.Clear();
                }
                if (phrase.Length == 0) start = from;
                else if (!IsCjk(phrase[phrase.Length - 1]) && !IsCjk(value[0])) phrase.Append(' ');
                phrase.Append(value); end = to;
                if (Regex.IsMatch(value, @"[.!?。！？]$"))
                {
                    result.Paragraphs.Add(new Paragraph(phrase.ToString(), start * 1000, end * 1000)); phrase.Clear();
                }
            }
            if (phrase.Length > 0) result.Paragraphs.Add(new Paragraph(phrase.ToString(), start * 1000, end * 1000));
            Validate(result);
            return result;
        }

        private static bool IsCjk(char ch) => ch >= '\u3000' && ch <= '\u9fff';
        private static bool Finite(double value) => !double.IsInfinity(value) && !double.IsNaN(value);
        public static void Validate(Subtitle subtitle)
        {
            foreach (var p in subtitle.Paragraphs)
                if (!Finite(p.StartTime.TotalMilliseconds) || !Finite(p.EndTime.TotalMilliseconds) || p.StartTime.TotalMilliseconds < 0 ||
                    p.EndTime.TotalMilliseconds < p.StartTime.TotalMilliseconds) throw new InvalidDataException("The engine returned invalid subtitle timestamps.");
            subtitle.Renumber();
        }
    }
}
