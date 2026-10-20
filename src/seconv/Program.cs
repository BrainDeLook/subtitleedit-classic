using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.AutoTranslate;
using Nikse.SubtitleEdit.Core.BluRaySup;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Interfaces;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Core.VobSub;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicFormats;
using Nikse.SubtitleEdit.Logic.ClassicImageExport;
using Nikse.SubtitleEdit.Logic.Ocr;
using Nikse.SubtitleEdit.Logic.VideoOcr;
using Nikse.SubtitleEdit.Logic.Ocr.Tesseract;

namespace Nikse.SubtitleEdit.ClassicConsole
{
    public static class Program
    {
        public static readonly string[] Options = { "json", "help-json", "help", "output-folder", "output-filename", "output-filename-append", "overwrite", "encoding", "fps", "translate", "translate-from", "translate-to", "translate-prompt", "api-url", "api-key", "model", "ocr-engine", "ocr-language", "ocr-variant", "ocr-mode", "ocr-prompt", "ocr-db", "nocr-optimize", "resolution", "full-frame", "override-position", "settings", "legacy" };
        [STAThread] public static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.InputEncoding = new UTF8Encoding(false);
            using (var cancel = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (s,e) => { e.Cancel = true; cancel.Cancel(); };
                try { return Run(args, cancel.Token).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { Console.Error.WriteLine("Cancelled."); return 130; }
                catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
            }
        }
        public static async Task<int> Run(string[] args, CancellationToken token)
        {
            var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); var positional = new List<string>();
            foreach (var arg in args)
            {
                if (!arg.StartsWith("--", StringComparison.Ordinal)) { positional.Add(arg); continue; }
                int colon = arg.IndexOf(':'); string key = colon < 0 ? arg.Substring(2) : arg.Substring(2, colon - 2);
                if (!Options.Contains(key, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Unknown option --" + key);
                flags[key] = colon < 0 ? "true" : arg.Substring(colon + 1);
            }
            string Get(string key, string fallback = null) => flags.TryGetValue(key, out var value) ? value : fallback;
            bool Has(string key) => flags.ContainsKey(key) && !Get(key).Equals("false", StringComparison.OrdinalIgnoreCase);
            ClassicFormatRegistry.Register();
            if (args.Length == 0 || Has("help") || Has("help-json"))
            {
                if (Has("help-json")) Console.WriteLine(JsonConvert.SerializeObject(new { tool = "seconv-classic", options = Options, formats = SubtitleFormat.AllSubtitleFormats.Select(f => f.Name).Concat(new[] { "imsc-image", "dvd-sup", "bdn-image", "bdn-8bit" }) }));
                else Console.WriteLine("seconv <file-or-pattern> <format> [--output-folder:path] [--json]\nOptions use --name:value. --translate:compatible --translate-to:ru,en --translate-prompt:text; --ocr-engine:tesseract|nocr|compatible|crispembed|mistral|lens|paddle|ollama|llama|lmstudio; --nocr-optimize:db; --help-json");
                return 0;
            }
            if (Has("legacy"))
            {
                // Legacy SE4 operations stay available, rather than being silently dropped.
                var method = typeof(Nikse.SubtitleEdit.Logic.CommandLineConvert.CommandLineConverter).GetMethod("Convert", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                return (int)method.Invoke(null, new object[] { new[] { "seconv", "/convert" }.Concat(positional).ToArray() });
            }
            if (Has("fps")) Configuration.Settings.General.CurrentFrameRate = double.Parse(Get("fps"), CultureInfo.InvariantCulture);
            if (Has("nocr-optimize"))
            {
                var file = Path.GetFullPath(Get("nocr-optimize")); var db = new NOcrDb(file); var stage = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { var candidate = new NOcrDb(db, stage); var report = await Task.Run(() => NOcrDbOptimizer.Optimize(candidate, new NOcrDbOptimizerSettings(), () => token.IsCancellationRequested), token); token.ThrowIfCancellationRequested(); candidate.Save(); File.Copy(file, NOcrDbOptimizer.GetFreeBackupFileName(file)); File.Replace(stage, file, null); Console.WriteLine(JsonConvert.SerializeObject(new { file, optimized = report != null })); return 0; }
                finally { if (File.Exists(stage)) File.Delete(stage); }
            }
            if (positional.Count != 2) throw new ArgumentException("Specify an input file/pattern and output format.");
            var input = positional[0]; var targetName = positional[1];
            var inputs = input.IndexOfAny(new[] { '*', '?' }) < 0 ? new[] { Path.GetFullPath(input) } : Directory.GetFiles(Path.GetDirectoryName(Path.GetFullPath(input)), Path.GetFileName(input));
            if (inputs.Length == 0) throw new FileNotFoundException("No input files matched.");
            if (Has("output-filename") && inputs.Length != 1) throw new ArgumentException("--output-filename requires one input.");
            var results = new List<object>(); int failures = 0;
            foreach (var file in inputs)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var subtitle = await Read(file, flags, token);
                    var languages = Get("translate-to", "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (!Has("translate")) languages = new[] { "" };
                    if (languages.Length > 1 && Has("output-filename")) throw new ArgumentException("Use --output-folder for multiple translation targets; each target needs its own filename.");
                    if (languages.Any(code => code.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))) throw new ArgumentException("Invalid target language code.");
                    if (languages.Length == 0) throw new ArgumentException("--translate-to is required with --translate.");
                    foreach (var language in languages)
                    {
                        var working = new Subtitle(subtitle, false);
                        if (Has("translate")) await Translate(working, Get("translate"), Get("translate-from", "en"), language, flags, token);
                        var format = SubtitleFormat.AllSubtitleFormats.Concat(ClassicFormatRegistry.Other()).FirstOrDefault(f => string.Equals(f.Name.Replace(" ", ""), targetName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) || string.Equals(f.Extension.TrimStart('.'), targetName, StringComparison.OrdinalIgnoreCase));
                        int image = Array.IndexOf(new[] { "imsc-image", "bdn-image", "bdn-8bit", "dvd-sup" }, targetName.ToLowerInvariant());
                        if (format == null && image < 0) throw new ArgumentException("Unknown output format: " + targetName);
                        var stem = Path.GetFileNameWithoutExtension(file) + Get("output-filename-append", "") + (string.IsNullOrEmpty(language) ? "" : "." + language);
                        if (stem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || language.Contains("/") || language.Contains("\\")) throw new ArgumentException("Invalid output name/language.");
                        var folder = Path.GetFullPath(Get("output-folder", Path.GetDirectoryName(file))); Directory.CreateDirectory(folder);
                        var output = Path.GetFullPath(Get("output-filename", Path.Combine(folder, stem + (image >= 0 ? "_images" : format.Extension))));
                        ClassicMediaJobs.CheckOutput(file, output);
                        if (!Has("overwrite") && (File.Exists(output) || Directory.Exists(output))) throw new IOException("Output exists: " + output);
                        if (image >= 0)
                        {
                            var settings = Has("settings") ? JsonConvert.DeserializeObject<ClassicImageOptions>(File.ReadAllText(Get("settings"))) : new ClassicImageOptions();
                            if (Has("resolution")) { var dimensions = Get("resolution").Split('x'); settings.Width = int.Parse(dimensions[0]); settings.Height = int.Parse(dimensions[1]); }
                            if (image == 3 && !Has("resolution")) { settings.Width = 720; settings.Height = 576; settings.FrameRate = 25; }
                            if (flags.ContainsKey("full-frame")) settings.FullFrame = Has("full-frame");
                            if (Has("override-position")) { int position = int.Parse(Get("override-position"), CultureInfo.InvariantCulture); if (position < 1 || position > 9) throw new ArgumentException("Position must be between 1 and 9."); foreach (var p in working.Paragraphs) p.Text = "{\\an" + position + "}" + p.Text; }
                            await ExportImages(working, settings, image, file, output, Has("overwrite"), token);
                        }
                        else
                        {
                            if (!format.IsTextBased) throw new ArgumentException("Use an explicit image output name or --legacy for this binary format.");
                            var stage = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
                            try { File.WriteAllText(stage, format.ToText(working, Path.GetFileNameWithoutExtension(output)), Has("encoding") ? Encoding.GetEncoding(Get("encoding")) : new UTF8Encoding(false)); token.ThrowIfCancellationRequested(); ClassicMediaJobs.Commit(stage, output); }
                            finally { if (File.Exists(stage)) File.Delete(stage); }
                        }
                        results.Add(new { input = file, output, cues = working.Paragraphs.Count, language, success = true });
                        if (!Has("json")) Console.WriteLine(output);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { failures++; results.Add(new { input = file, success = false, error = ex.Message }); if (!Has("json")) Console.Error.WriteLine(file + ": " + ex.Message); }
            }
            if (Has("json")) Console.WriteLine(JsonConvert.SerializeObject(new { results, failures })); return failures == 0 ? 0 : 1;
        }
        private static async Task ExportImages(Subtitle subtitle, ClassicImageOptions options, int format, string input, string output, bool overwrite, CancellationToken token)
        {
            if (!Directory.Exists(output)) { await ClassicImageJob.Export(subtitle, options, format, output, token, null); return; }
            if (!overwrite) throw new IOException("Output folder exists.");
            if (Path.GetFullPath(input).StartsWith(output.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("An image export cannot replace a folder containing its input.");
            // Replace only a recognizable previous image export, never an arbitrary directory.
            if (!File.Exists(Path.Combine(output, "subtitles.ttml")) && !File.Exists(Path.Combine(output, "subtitles.sup")) && !File.Exists(Path.Combine(output, "index.xml"))) throw new IOException("The output folder is not a recognized image export. Choose a new folder.");
            var stage = output + ".seconv-" + Guid.NewGuid().ToString("N"); var backup = output + ".backup-" + Guid.NewGuid().ToString("N");
            try
            {
                await ClassicImageJob.Export(subtitle, options, format, stage, token, null); token.ThrowIfCancellationRequested();
                Directory.Move(output, backup);
                try { Directory.Move(stage, output); } catch { Directory.Move(backup, output); throw; }
                Directory.Delete(backup, true);
            }
            finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        }
        private static async Task Translate(Subtitle subtitle, string engine, string source, string target, Dictionary<string,string> flags, CancellationToken token)
        {
            string Get(string k, string fallback = null) => flags.TryGetValue(k, out var v) ? v : fallback;
            if (engine.Equals("deepl", StringComparison.OrdinalIgnoreCase))
            { using (var deepl = new DeepLTranslate()) { deepl.Initialize(); foreach (var p in subtitle.Paragraphs) { token.ThrowIfCancellationRequested(); p.Text = await deepl.Translate(p.Text, source, target, token); if (string.IsNullOrWhiteSpace(p.Text)) throw new InvalidDataException("DeepL returned an empty cue."); } } return; }
            if (engine.Equals("ollama", StringComparison.OrdinalIgnoreCase)) engine = "ollama-advanced";
            if (engine.Equals("lmstudio", StringComparison.OrdinalIgnoreCase)) engine = "lmstudio-advanced";
            if (!ClassicNewTranslator.Ids.Contains(engine)) throw new ArgumentException("Unsupported translation engine: " + engine);
            using (var translator = new ClassicNewTranslator(engine))
            {
                var settings = ClassicTranslationSettings.Load(engine); settings.Prompt = Get("translate-prompt", settings.Prompt); settings.Api.Url = Get("api-url", settings.Api.Url); settings.Api.ApiKey = Get("api-key", settings.Api.ApiKey); settings.Api.Model = Get("model", settings.Api.Model); if (flags.ContainsKey("api-url")) settings.UseLocalLlama = false; translator.Initialize(settings);
                if (translator.IsAdvanced)
                {
                    for (int offset = 0; offset < subtitle.Paragraphs.Count; offset += translator.AdvancedBatchSize)
                    {
                        var batch = subtitle.Paragraphs.Skip(offset).Take(translator.AdvancedBatchSize).ToList();
                        var translated = await translator.TranslateBatchAsync(batch.Select(p => p.Text).ToList(), source, target, token);
                        for (int i = 0; i < batch.Count; i++) batch[i].Text = translated[i];
                    }
                    return;
                }
                foreach (var p in subtitle.Paragraphs) { token.ThrowIfCancellationRequested(); var text = await translator.Translate(p.Text, source, target, token); if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Translation returned an empty cue."); p.Text = text; }
            }
        }
        private static async Task<Subtitle> Read(string file, Dictionary<string,string> flags, CancellationToken token)
        {
            if (!File.Exists(file)) throw new FileNotFoundException("Input not found", file);
            string Get(string k, string fallback = null) => flags.TryGetValue(k, out var v) ? v : fallback;
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (!new[] { ".sup", ".sub", ".png", ".jpg", ".bmp" }.Contains(ext) || ext == ".sub" && !File.Exists(Path.ChangeExtension(file, ".idx"))) return Subtitle.Parse(file) ?? throw new InvalidDataException("Unknown subtitle format.");
            var result = new Subtitle(); string engine = Get("ocr-engine", "tesseract"), language = Get("ocr-language", "eng");
            var images = new List<IBinaryParagraphWithPosition>();
            if (ext == ".sup") images.AddRange(BluRaySupParser.ParseBluRaySup(file, new StringBuilder()).Cast<IBinaryParagraphWithPosition>());
            else if (ext == ".sub") { var parser = new VobSubParser(true); parser.OpenSubIdx(file, Path.ChangeExtension(file, ".idx")); foreach (var pack in parser.MergeVobSubPacks()) { pack.Palette = parser.IdxPalette; images.Add(pack); } }
            if (engine == "paddle")
            {
                string variant = Get("ocr-variant", "cpu");
                if (!File.Exists(ClassicPaddleOcr.Executable(variant))) throw new FileNotFoundException("Install Paddle OCR from Video OCR first, or use --ocr-variant:cuda12 for an existing GPU installation.");
                var job = Path.Combine(Path.GetTempPath(), "seconv-paddle-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(job);
                try
                {
                    var groups = new List<VideoOcrFrameGroup>();
                    for (int i = 0; i < Math.Max(1, images.Count); i++)
                    {
                        var imageFile = Path.Combine(job, "source" + i + ".png");
                        using (var bitmap = images.Count > 0 ? images[i].GetBitmap() : new Bitmap(file)) bitmap.Save(imageFile, System.Drawing.Imaging.ImageFormat.Png);
                        groups.Add(new VideoOcrFrameGroup { RepresentativeFileName = imageFile });
                    }
                    await ClassicPaddleOcr.RecognizeAsync(groups, job, language, Get("ocr-mode", "server"), variant, 0, token, null);
                    for (int i = 0; i < groups.Count; i++) result.Paragraphs.Add(new Paragraph(groups[i].Text, images.Count > 0 ? images[i].StartTimeCode.TotalMilliseconds : 0, images.Count > 0 ? images[i].EndTimeCode.TotalMilliseconds : 2000));
                    result.Renumber(); return result;
                }
                finally { Directory.Delete(job, true); }
            }
            if (engine == "ollama" || engine == "llama" || engine == "lmstudio")
            {
                if (!flags.ContainsKey("api-url")) flags["api-url"] = engine == "ollama" ? "http://localhost:11434/v1/chat/completions" : engine == "lmstudio" ? "http://localhost:1234/v1/chat/completions" : "http://localhost:8080/v1/chat/completions";
                engine = "compatible";
            }
            async Task<string> Ocr(Bitmap bitmap)
            {
                token.ThrowIfCancellationRequested();
                if (engine == "nocr")
                { var db = new NOcrDb(Get("ocr-db") ?? throw new ArgumentException("--ocr-db is required for nOCR.")); var items = ClassicNOcrBitmap.Split(new NikseBitmap(bitmap), 8, false, false, 5, true); return string.Concat(items.Select(i => i.NikseBitmap == null ? i.SpecialCharacter : db.GetMatch(i.NikseBitmap, i.Top, true, 10)?.Text ?? "*")); }
                if (engine == "tesseract")
                {
                    var temp = Path.Combine(Path.GetTempPath(), "seconv-" + Guid.NewGuid().ToString("N") + ".png");
                    try { bitmap.Save(temp); var runner = new TesseractRunner(); var text = await Task.Run(() => runner.Run(language, "6", "1", temp), token); token.ThrowIfCancellationRequested(); if (!string.IsNullOrEmpty(runner.LastError)) throw new InvalidOperationException(runner.LastError); return text; } finally { if (File.Exists(temp)) File.Delete(temp); }
                }
                if (!new[] { "compatible", "crispembed", "mistral", "lens" }.Contains(engine)) throw new ArgumentException("Unsupported OCR engine.");
                var settings = ClassicBitmapSettings.Load(engine); if (flags.ContainsKey("api-url")) settings.Api.Url = Get("api-url"); if (flags.ContainsKey("api-key")) settings.Api.ApiKey = Get("api-key");
                using (var ocr = new ClassicBitmapOcr(engine, settings)) return await ocr.Ocr(bitmap, Get("model", settings.Api.Model), language, token, Get("ocr-prompt"));
            }
            if (images.Count > 0) foreach (var image in images) { using (var bitmap = image.GetBitmap()) result.Paragraphs.Add(new Paragraph(await Ocr(bitmap), image.StartTimeCode.TotalMilliseconds, image.EndTimeCode.TotalMilliseconds)); }
            else if (ext != ".sup" && ext != ".sub") using (var bitmap = new Bitmap(file)) result.Paragraphs.Add(new Paragraph(await Ocr(bitmap), 0, 2000));
            if (result.Paragraphs.Count == 0) throw new InvalidDataException("No subtitle images were read."); result.Renumber(); return result;
        }
    }
}
