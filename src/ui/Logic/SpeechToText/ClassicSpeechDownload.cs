using SevenZipExtractor;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Nikse.SubtitleEdit.Logic.SpeechToText
{
    public static class ClassicSpeechDownload
    {
        private static readonly Dictionary<string, string> RuntimeHashes = new Dictionary<string, string>
        {
            ["crispasr-windows-x86_64-cpu.zip"] = "ac8b6caf4dd448d00c5050907275bce4d154747110c37943aa4f69ee7fac9541",
            ["crispasr-windows-x86_64-cpu-legacy.zip"] = "ba4e23fb8dfcc99b8a76af034954576a75f88193e3dbf62fc774287bcbd1114b",
            ["crispasr-windows-x86_64-vulkan.zip"] = "112a33912d464346ba1c2a75f975864a7ed0a3c1bd1ad0c3cf8806b6919efd7d",
            ["crispasr-windows-x86_64-cuda13.zip"] = "e2183d839d13a2eeea175f167acfafbd66ba5cc072bd21500777c2a43e1aa8a4",
            ["crispasr-windows-x86_64-cuda.zip"] = "9108d2be9b61415cf2c6d758d09a6fbfda369c2cda2d98f1f3d61e1326792d01",
            ["qwen3-asr-cpp-win64.zip"] = "ad9c196aa52ceb42a3f46946644e4145fc7e297c84fa54e4cdf7fd9c22556300",
            ["qwen3-asr-cpp-win64-vulkan.zip"] = "a9971a24ba08765568c8a83a547924e42f15bd576b2cf594f6757fff66baf244",
            ["whisper-vulkan-x64.zip"] = "2b3829c69c64b3bbd117ef3fa3ae1de2574cab36a4a572199bf3c291404ff1cc",
            ["whisperx-standalone-windows-x64.7z"] = "439776243a3040693e9a2767a3efb4b8dd7549244bb6695ce0ef7209e5456bf3"
        };
        public static async Task DownloadFileAsync(string url, string destination, bool isGguf,
            CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".part";
            try
            {
                using (var client = new HttpClient(new HttpClientHandler { SslProtocols = System.Security.Authentication.SslProtocols.Tls12 }) { Timeout = TimeSpan.FromMinutes(60) })
                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    var total = response.Content.Headers.ContentLength;
                    using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        var buffer = new byte[81920];
                        var reportWatch = Stopwatch.StartNew();
                        long read = 0;
                        int length;
                        while ((length = await input.ReadAsync(buffer, 0, buffer.Length, cancel).ConfigureAwait(false)) > 0)
                        {
                            await output.WriteAsync(buffer, 0, length, cancel).ConfigureAwait(false);
                            read += length;
                            if (reportWatch.ElapsedMilliseconds >= 100 || total.HasValue && read == total.Value)
                            {
                                progress?.Report(new SpeechProgress { Message = Path.GetFileName(destination) + "  " + (read / 1048576) + " MB",
                                    Percent = total > 0 ? (int)(read * 100 / total.Value) : -1 });
                                reportWatch.Restart();
                            }
                        }
                        if (read == 0 || total.HasValue && read != total.Value) throw new InvalidDataException("Incomplete download: " + Path.GetFileName(destination));
                    }
                }
                if (isGguf)
                {
                    using (var file = File.OpenRead(temporary))
                    {
                        var magic = new byte[4];
                        if (file.Read(magic, 0, 4) != 4 || System.Text.Encoding.ASCII.GetString(magic) != "GGUF")
                            throw new InvalidDataException("Downloaded file is not a GGUF model.");
                    }
                }
                cancel.ThrowIfCancellationRequested();
                if (File.Exists(destination)) File.Replace(temporary, destination, null); else File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public static string SafeArchivePath(string directory, string name)
        {
            var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive entry is outside its installation folder.");
            return path;
        }

        public static void Extract(string archive, string directory, CancellationToken cancel)
        {
            Directory.CreateDirectory(directory);
            if (Path.GetExtension(archive).Equals(".7z", StringComparison.OrdinalIgnoreCase))
            {
                using (var file = new ArchiveFile(archive))
                    file.Extract(entry =>
                    {
                        cancel.ThrowIfCancellationRequested();
                        var path = SafeArchivePath(directory, entry.FileName);
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        return path;
                    });
            }
            else
            {
                using (var file = ZipFile.OpenRead(archive))
                    foreach (var entry in file.Entries)
                    {
                        cancel.ThrowIfCancellationRequested();
                        var path = SafeArchivePath(directory, entry.FullName);
                        if (entry.FullName.EndsWith("/")) { Directory.CreateDirectory(path); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        entry.ExtractToFile(path, true);
                    }
            }
        }

        public static async Task EnsureRuntimeAsync(SpeechEngine engine, string variant, CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            if (File.Exists(ClassicSpeechCatalog.Executable(engine, variant))) return;
            var target = ClassicSpeechCatalog.EngineDirectory(engine, variant);
            var staging = target + "." + Guid.NewGuid().ToString("N") + ".staging";
            var url = ClassicSpeechCatalog.RuntimeUrl(engine, variant);
            var archive = Path.Combine(ClassicSpeechCatalog.Root, "Downloads", engine.Family + "-" + variant + Path.GetExtension(new Uri(url).AbsolutePath));
            try
            {
                await DownloadFileAsync(url, archive, false, cancel, progress).ConfigureAwait(false);
                VerifyRuntimeArchive(url, archive);
                progress?.Report(new SpeechProgress { Message = "Extracting " + engine.Name + "…" });
                await Task.Run(() => Extract(archive, staging, cancel), cancel).ConfigureAwait(false);
                cancel.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (Directory.Exists(target))
                {
                    foreach (var file in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
                    {
                        var destination = SafeArchivePath(target, file.Substring(staging.Length + 1));
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        File.Copy(file, destination, true);
                    }
                }
                else Directory.Move(staging, target);
                if (!File.Exists(ClassicSpeechCatalog.Executable(engine, variant))) throw new InvalidDataException("Downloaded engine executable was not found.");
            }
            finally
            {
                // These paths are generated under our private engine/download root.
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                if (File.Exists(archive)) File.Delete(archive);
            }
        }

        public static void VerifyRuntimeArchive(string url, string archive)
        {
            if (!RuntimeHashes.TryGetValue(Path.GetFileName(new Uri(url).AbsolutePath), out var expected))
                throw new InvalidDataException("No checksum for the pinned engine archive.");
            using (var input = File.OpenRead(archive))
            using (var sha = SHA256.Create())
            {
                var actual = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                if (actual != expected) throw new InvalidDataException("Engine archive checksum mismatch.");
            }
        }

        public static async Task EnsureModelAsync(SpeechEngine engine, SpeechModel model, CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            if (engine.Family == "whisperx") return; // WhisperX downloads to its private HF/model cache on first inference.
            var path = ClassicSpeechCatalog.ModelPath(engine, model);
            if (File.Exists(path) && new FileInfo(path).Length > 1000000) return;
            if (model.Urls == null || model.Urls.Length == 0) throw new InvalidDataException("No model download URL.");
            await DownloadFileAsync(model.Urls[0], path, engine.Family != "cpp", cancel, progress).ConfigureAwait(false);
        }

        public static async Task EnsureWhisperXRussianAlignerAsync(CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            // Download the complete processor explicitly: the bundled HF client can
            // fail to resolve vocab.json and leave an unusable partial cache.
            const string source = "https://huggingface.co/jonatasgrosman/wav2vec2-large-xlsr-53-russian/resolve/2329100508896c6d9b157019803ab5601e6f3406/";
            foreach (var name in new[] { "config.json", "preprocessor_config.json", "special_tokens_map.json", "vocab.json", "pytorch_model.bin" })
            {
                var destination = Path.Combine(ClassicSpeechCatalog.WhisperXRussianAligner, name);
                if (!File.Exists(destination) || new FileInfo(destination).Length == 0)
                    await DownloadFileAsync(source + name, destination, false, cancel, progress).ConfigureAwait(false);
            }
        }
    }
}
