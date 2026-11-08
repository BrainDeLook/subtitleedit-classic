using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
namespace Nikse.SubtitleEdit.Logic {
public static class ClassicBurnInRunner
{
    /// <summary>The UI's burn-in defaults (libx264, CRF 23, audio copied).</summary>
    public const string DefaultFfmpegArgs = "-c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p -c:a copy";

    /// <summary>
    /// Default for .webm output, which cannot hold H.264 (or AAC, so audio is re-encoded): VP9 + Opus,
    /// the WebM codecs the UI's burn-in offers.
    /// </summary>
    public const string DefaultWebmFfmpegArgs = "-c:v libvpx-vp9 -crf 32 -b:v 0 -pix_fmt yuv420p -c:a libopus";

    /// <summary>"out/movie.ass" + "in/movie.mp4" -> "out/movie_burn-in.mp4".</summary>
    public static string GetOutputFileName(string assaFileName, string videoFileName)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(assaFileName)) ?? string.Empty;
        return Path.Combine(folder, Path.GetFileNameWithoutExtension(assaFileName) + "_burn-in" + Path.GetExtension(videoFileName));
    }

    /// <summary>Names accepted by <c>--burn-in-effects</c>; one per effect in the UI's Burn in &gt; Effects.</summary>
    public static readonly string[] EffectNames =
    [
        "fade", "font-kerning", "font-size-change", "scroll-up", "scroll-down",
        "rotate-in", "tilt-bounce", "font-size-bounce-in", "fix-rtl",
    ];

    /// <summary>
    /// "fade:500, font-kerning" -> ["fade:500", "font-kerning"]. A bare "fade" is 250 ms in and
    /// out, like the UI's "Fade in/out".
    /// </summary>
    public static List<string> ParseEffects(string value)
    {
        var effects = new List<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return effects;
        }

        foreach (var item in value.Split(new[]{','}, StringSplitOptions.RemoveEmptyEntries))
        {
            var effect = item.Trim().ToLowerInvariant();
            var name = effect.Split(':')[0];
            if (!EffectNames.Contains(name))
            {
                throw new FormatException($"Unknown --burn-in-effects value '{item}'. Valid: {string.Join(", ", EffectNames)} (fade:<ms> sets the fade length).");
            }

            if (name != "fade" && effect.Contains(':'))
            {
                throw new FormatException($"--burn-in-effects: only fade takes a value (fade:<ms>), got '{item}'.");
            }

            if (name == "fade" && effect.Contains(':') &&
                !(int.TryParse(effect.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out var ms) && ms > 0))
            {
                throw new FormatException($"--burn-in-effects: fade length must be a positive number of milliseconds (got '{item}').");
            }

            effects.Add(effect);
        }

        return effects;
    }

    /// <summary>
    /// Applies the effects to every line, the way the UI's burn-in does (BurnInEffectItem.ApplyEffect
    /// in the UI project): each effect in turn, tags prepended. Call after the ASSA styles and
    /// PlayResX/Y are final - the size effects scale from the line's style font size and the
    /// scroll effects position against the script resolution.
    /// </summary>
    public static void ApplyEffects(Subtitle subtitle, IReadOnlyList<string> effects)
    {
        var header = subtitle.Header ?? string.Empty;
        var width = ParsePlayRes("PlayResX", header, 384);
        var height = ParsePlayRes("PlayResY", header, 288);

        foreach (var p in subtitle.Paragraphs)
        {
            var style = AdvancedSubStationAlpha.GetSsaStyle(string.IsNullOrEmpty(p.Extra) ? "Default" : p.Extra, header);
            var fontSize = (int)style.FontSize;
            var durationMs = (int)p.Duration.TotalMilliseconds;
            foreach (var effect in effects)
            {
                p.Text = ApplyEffect(effect, p.Text, width, height, fontSize, durationMs);
            }
        }
    }

    private static int ParsePlayRes(string tag, string header, int fallback)
    {
        var value = AdvancedSubStationAlpha.GetTagValueFromHeader(tag, "[Script Info]", header);
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : fallback;
    }

    internal static string ApplyEffect(string effect, string text, int width, int height, int fontSize, int durationMs)
    {
        const int step = 250;
        if (effect.StartsWith("fade", StringComparison.Ordinal))
        {
            var ms = effect.Contains(':') ? effect.Substring(5) : "250";
            return $"{{\\fad({ms},{ms})}}{text}";
        }

        return effect switch
        {
            "font-kerning" => $"{{\\t(\\fsp4)}}{text}",
            "font-size-change" => $"{{\\t(\\fs{(int)(fontSize * 1.2)})}}{text}",
            "scroll-up" => $"{{\\move({width / 2},{height - 60},{width / 2},{height - 80})}}{text}",
            "scroll-down" => $"{{\\move({width / 2},{height - 80},{width / 2},{height - 60})}}{text}",
            "rotate-in" => $"{{\\frx-30\\t(0,{durationMs / 2},\\frx0)}}{text}",
            "tilt-bounce" => $"{{\\frz-10\\t(0,{step},\\frz10)\\t({step},{step * 2},\\frz-6)\\t({step * 2},{step * 3},\\frz4)" +
                             $"\\t({step * 3},{step * 4},\\frz-2)\\t({step * 4},{step * 5},\\frz1)\\t({step * 5},{durationMs},\\frz0)}}{text}",
            "font-size-bounce-in" => $"{{\\fs{(int)(fontSize * 0.5)}\\t(0,{step},\\fs{(int)(fontSize * 1.2)})" +
                                     $"\\t({step},{step * 2},\\fs{(int)(fontSize * 0.9)})\\t({step * 2},{step * 3},\\fs{(int)(fontSize * 1.05)})" +
                                     $"\\t({step * 3},{durationMs},\\fs{fontSize})}}{text}",
            "fix-rtl" => Utilities.FixRtlViaUnicodeChars(text),
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, "unknown burn-in effect"),
        };
    }

    public static string BuildArguments(string videoFileName, string outputFileName, string ffmpegArgs)
    {
        // The subtitle is referenced by a fixed relative name from the working folder: an
        // absolute path inside the filter graph would need ffmpeg's filter escaping for ':',
        // '\', ',' and quotes (Windows drive letters alone break it).
        var isWebm = Path.GetExtension(outputFileName).Equals(".webm", StringComparison.OrdinalIgnoreCase);
        var args = string.IsNullOrWhiteSpace(ffmpegArgs)
            ? isWebm ? DefaultWebmFfmpegArgs : DefaultFfmpegArgs
            : ffmpegArgs.Trim();

        // "+faststart" only means something to the MP4/QuickTime muxer (as in the UI's FfmpegGenerator).
        var movFlags = IsMp4Family(Path.GetExtension(outputFileName)) ? " -movflags +faststart" : string.Empty;
        return $"-hide_banner -nostdin -y -i \"{videoFileName}\" -vf ass=subtitle.ass {args}{movFlags} \"{outputFileName}\"";
    }

    /// <summary>True for the MP4/QuickTime family, the only containers where "-movflags" means anything.</summary>
    public static bool IsMp4Family(string extension)
    {
        var ext = (extension ?? string.Empty).TrimStart('.').ToLowerInvariant();
        return ext is "mp4" or "m4v" or "mov";
    }

    /// <summary>
    /// The ffmpeg to run: <c>FFmpegLocation</c> when it points at a file, else the first
    /// <c>ffmpeg</c> on PATH (full path, so <c>FfmpegMediaInfo</c> - which on Windows only
    /// trusts <c>FFmpegLocation</c> - can be pointed at it too). Null when there is none.
    /// </summary>
    public static string FindFfmpeg()
    {
        var configured = Configuration.Settings.General.FFmpegLocation;
        if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
        {
            return configured;
        }

        var exeName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "ffmpeg.exe" : "ffmpeg";
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var folder in path.Split(new[]{Path.PathSeparator}, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(folder.Trim('"'), exeName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // malformed PATH entry
            }
        }

        return null;
    }

    /// <summary>
    /// Throws when burning <paramref name="assaFileName"/> into <paramref name="videoFileName"/>
    /// would replace the input video, or an existing output without <c>--overwrite</c>. Called
    /// before the .ass is written too, so a refused burn-in leaves nothing half done.
    /// </summary>
    public static void EnsureOutputWritable(string assaFileName, string videoFileName, bool overwrite)
    {
        var outputFileName = GetOutputFileName(assaFileName, videoFileName);
        if (string.Equals(Path.GetFullPath(outputFileName), Path.GetFullPath(videoFileName), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Burn-in output would replace the input video: {outputFileName}");
        }

        if (File.Exists(outputFileName) && !overwrite)
        {
            throw new InvalidOperationException($"Output file already exists: {outputFileName}. Pass --overwrite to replace it.");
        }
    }

    public static async Task<string> RunAsync(string assaFileName,string videoFileName,string ffmpegArgs,bool overwrite,CancellationToken token)
    {
        EnsureOutputWritable(assaFileName,videoFileName,overwrite);
        var ffmpeg=FindFfmpeg() ?? throw new FileNotFoundException("FFmpeg was not found.");
        var output=GetOutputFileName(assaFileName,videoFileName);
        var folder=Path.Combine(Path.GetTempPath(),"seconv-burn-in-"+Guid.NewGuid().ToString("N"));
        var stage=Path.Combine(Path.GetDirectoryName(output),"."+Guid.NewGuid().ToString("N")+Path.GetExtension(output));
        Directory.CreateDirectory(folder);
        try
        {
            File.Copy(assaFileName,Path.Combine(folder,"subtitle.ass"));
            var start=new System.Diagnostics.ProcessStartInfo(ffmpeg,BuildArguments(Path.GetFullPath(videoFileName),stage,ffmpegArgs)){WorkingDirectory=folder};
            await SpeechToText.ClassicSpeechRunner.RunProcessAsync(start,token,null).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();ClassicMediaJobs.Commit(stage,output);return output;
        }
        finally{if(File.Exists(stage))File.Delete(stage);Directory.Delete(folder,true);}
    }
}}
