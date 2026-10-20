using System.Linq;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Logic.ClassicMusic
{
    internal static class MusicMath
    {
        public static int Clamp(int v, int min, int max) => Math.Max(min, Math.Min(max, v));
        public static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(max, v));
        public static float Clamp(float v, float min, float max) => Math.Max(min, Math.Min(max, v));
    }
    public static class ClassicMusicGenerator
    {
        public const string ModelFile = "ace-step-1.5-turbo-q8_0.gguf";
        public const long ModelBytes = 6185460032L;
        public const string ModelHash = "cd7bf272588f548d4a253f57483132ef0f8f54a549f26471b09caa72c654270b";
        public static string Model => Path.Combine(Configuration.DataDirectory, "audio.cpp", "models", "ACE-Step1.5-GGUF", ModelFile);
        public static string BuildArguments(string prompt, int bpm, int seconds, long seed, string backend, string model, string output, bool floats)
        {
            if (string.IsNullOrWhiteSpace(prompt) || bpm < 40 || bpm > 240 || seconds < 20 || seconds > 240 || !new[] { "cpu", "cuda", "vulkan" }.Contains(backend)) throw new ArgumentException("Invalid music generation settings.");
            return "--task gen --family ace_step --model " + ClassicMediaJobs.Q(model) + " --backend " + backend + " --task-route text2music --text " + ClassicMediaJobs.Q(prompt) + " --lyrics " + ClassicMediaJobs.Q("[Instrumental]") + " --duration-seconds " + seconds + " --seed " + seed.ToString(CultureInfo.InvariantCulture) + " --request-option bpm=" + bpm + " --request-option timesignature=4 --request-option " + ClassicMediaJobs.Q("negative_prompt=vocals, singing, choir, rap, aggressive, distorted guitar, heavy drums, dark, horror") + " --log --out " + ClassicMediaJobs.Q(output) + (floats ? " --out-format float32" : "");
        }
        public static async Task<string> Generate(string prompt, int bpm, int seconds, long seed, string backend, double targetSeconds, string output, CancellationToken token, IProgress<SpeechProgress> progress)
        {
            if (targetSeconds <= 0 || targetSeconds > 10800 || double.IsNaN(targetSeconds)) throw new ArgumentException("Choose an output length between 0 and 10800 seconds.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var job = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)), ".se-music-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(job);
            try
            {
                await ClassicAudioCpp.EnsureAsync(backend, token, progress);
                await ClassicVoiceAssets.EnsureFileAsync("https://huggingface.co/audio-cpp/audio.cpp-gguf/resolve/main/ACE-Step1.5-GGUF/turbo/" + ModelFile, Model, ModelBytes, ModelHash, true, token, progress);
                var cli = Path.Combine(ClassicAudioCpp.Root(backend), "audiocpp_cli.exe");
                var help = await ClassicMediaJobs.Capture(cli, "--help", token);
                var clip = Path.Combine(job, "clip.wav");
                await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(cli, BuildArguments(prompt, bpm, seconds, seed, backend, Model, clip, help.Contains("--out-format"))) { WorkingDirectory = Path.GetDirectoryName(cli) }, token, progress);
                var rendered = Path.Combine(job, "music.wav");
                await Task.Run(() => { token.ThrowIfCancellationRequested(); var audio = MusicAudio.ReadWav(clip); var loop = MusicLooper.FindLoop(audio, bpm); token.ThrowIfCancellationRequested(); MusicStreamRenderer.Write(audio, loop, targetSeconds, rendered, token); token.ThrowIfCancellationRequested(); }, token);
                ClassicMediaJobs.Commit(rendered, output); return output;
            }
            finally { Directory.Delete(job, true); }
        }
        public static async Task Mix(string source, string music, string output, double musicVolume, double originalVolume, CancellationToken token, int audioTrack = 0)
        {
            ClassicMediaJobs.CheckOutput(source, output); ClassicMediaJobs.CheckOutput(music, output);
            if (musicVolume < 0 || musicVolume > 2 || originalVolume < 0 || originalVolume > 2) throw new ArgumentException("Invalid volume.");
            var info = await ClassicMediaJobs.Probe(source, token); bool video = info.Streams.Exists(s => s.Type == "video"), audio = info.Streams.Exists(s => s.Type == "audio");
            if (audioTrack < 0 || audioTrack >= Math.Max(1, info.Streams.Count(s => s.Type == "audio"))) throw new ArgumentOutOfRangeException(nameof(audioTrack));
            video = video && !new[] { ".wav", ".mp3", ".m4a", ".aac", ".flac" }.Contains(Path.GetExtension(output).ToLowerInvariant());
            string N(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
            var filters = audio ? "[0:a:" + audioTrack + "]volume=" + N(originalVolume) + "[original];[1:a:0]volume=" + N(musicVolume) + "[music];[original][music]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.891:level=0:latency=1[out]" : "[1:a:0]volume=" + N(musicVolume) + "[out]";
            var stage = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)), ".se-music-mix-" + Guid.NewGuid().ToString("N") + Path.GetExtension(output));
            try { await ClassicMediaJobs.Render("-i " + ClassicMediaJobs.Q(source) + " -i " + ClassicMediaJobs.Q(music) + " -filter_complex " + ClassicMediaJobs.Q(filters) + (video ? " -map 0:v:0 -c:v copy" : "") + " -map \"[out]\" -c:a " + (Path.GetExtension(output).Equals(".wav", StringComparison.OrdinalIgnoreCase) ? "pcm_s16le" : "aac -b:a 192k") + " -t " + N(info.Seconds), stage, token); token.ThrowIfCancellationRequested(); ClassicMediaJobs.Commit(stage, output); }
            finally { if (File.Exists(stage)) File.Delete(stage); }
        }
    }
}
