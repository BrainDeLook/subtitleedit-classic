using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Nikse.SubtitleEdit.Logic
{
    // Ported from the stable v5.2.0 burn-in model, using the classic WinForms controls.
    public static class ClassicBurnIn
    {
        public static string[] GetContainers(string codec)
        {
            if (codec == "prores_ks") return new[] { ".mov", ".mkv" };
            if (codec == "libvpx-vp9") return new[] { ".mkv", ".webm", ".mp4" };
            return new[] { ".mp4", ".mkv", ".mov", ".ts" };
        }

        public static string[] GetAudioEncoders(string container)
        {
            switch ((container ?? string.Empty).ToLowerInvariant())
            {
                case ".webm": return new[] { "libopus", "libvorbis" };
                case ".ts": return new[] { "copy", "aac", "ac3", "mp3", "libopus" };
                case ".mov": return new[] { "copy", "aac", "ac3", "mp3", "libvorbis" };
                default: return new[] { "copy", "aac", "ac3", "mp3", "libopus", "libvorbis" };
            }
        }

        public static string GetAudioEncoder(string container, string wanted)
        {
            if (wanted == "opus") wanted = "libopus";
            if (wanted == "vorbis") wanted = "libvorbis";
            var encoders = GetAudioEncoders(container);
            return encoders.Contains(wanted) ? wanted : (encoders.Contains("aac") ? "aac" : encoders[0]);
        }

        public static string GetMuxer(string extension)
        {
            switch ((extension ?? string.Empty).ToLowerInvariant())
            {
                case ".mkv": return "matroska";
                case ".ts": return "mpegts";
                default: return extension.TrimStart('.');
            }
        }

        public static string GetContainerArguments(string extension)
        {
            return extension == ".mp4" || extension == ".mov" || extension == ".m4v"
                ? " -use_editlist 1 -movflags +faststart" : string.Empty;
        }

        public static string GetEffectTags(string effects, int width, int height, int fontSize, int duration)
        {
            var result = string.Empty;
            foreach (var effect in (effects ?? string.Empty).Split(','))
            {
                switch (effect)
                {
                    case "FadeInOut": result += "{\\fad(250,250)}"; break;
                    case "SlowFontSizeChange": result += "{\\t(\\fs" + (int)(fontSize * 1.2) + ")}"; break;
                    case "IncreaseFontKerning": result += "{\\t(\\fsp4)}"; break;
                    case "ScrollUp": result += "{\\move(" + width / 2 + "," + (height - 60) + "," + width / 2 + "," + (height - 80) + ")}"; break;
                    case "ScrollDown": result += "{\\move(" + width / 2 + "," + (height - 80) + "," + width / 2 + "," + (height - 60) + ")}"; break;
                    case "RotateIn": result += "{\\frx-30\\t(0," + duration / 2 + ",\\frx0)}"; break;
                    case "TiltBounce": result += "{\\frz-10\\t(0,250,\\frz10)\\t(250,500,\\frz-6)\\t(500,750,\\frz4)\\t(750,1000,\\frz-2)\\t(1000,1250,\\frz1)\\t(1250," + duration + ",\\frz0)}"; break;
                    case "FontSizeBounceIn": result += "{\\fs" + (int)(fontSize * .5) + "\\t(0,250,\\fs" + (int)(fontSize * 1.2) + ")\\t(250,500,\\fs" + (int)(fontSize * .9) + ")\\t(500,750,\\fs" + (int)(fontSize * 1.05) + ")\\t(750," + duration + ",\\fs" + fontSize + ")}"; break;
                }
            }
            return result;
        }

        public static void ApplyEffects(Subtitle subtitle, ClassicBurnInOptions options, int width, int height, int fontSize)
        {
            // ASS position/move tags use the script canvas, not output video pixels.
            // Keeping output dimensions here moves low-resolution ASS off screen.
            var scriptWidth = ReadScriptDimension(subtitle.Header, "PlayResX");
            var scriptHeight = ReadScriptDimension(subtitle.Header, "PlayResY");
            if (scriptWidth > 0 && scriptHeight > 0)
            {
                width = scriptWidth;
                height = scriptHeight;
            }
            foreach (var paragraph in subtitle.Paragraphs)
            {
                var size = fontSize;
                if (!string.IsNullOrEmpty(subtitle.Header))
                {
                    var style = AdvancedSubStationAlpha.GetSsaStyle(string.IsNullOrEmpty(paragraph.Extra) ? "Default" : paragraph.Extra, subtitle.Header);
                    if (style != null && style.FontSize > 0) size = (int)style.FontSize;
                }
                paragraph.Text = GetEffectTags(options.Effects, width, height, size, (int)paragraph.DurationTotalMilliseconds) + paragraph.Text;
            }
        }

        private static int ReadScriptDimension(string header, string key)
        {
            if (string.IsNullOrEmpty(header)) return 0;
            var inScriptInfo = false;
            foreach (var raw in header.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    inScriptInfo = line.Equals("[Script Info]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                var separator = line.IndexOf(':');
                if (inScriptInfo && separator > 0 && line.Substring(0, separator).Trim().Equals(key, StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(line.Substring(separator + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
                    return value;
            }
            return 0;
        }

        public static void ApplyTextStyle(SsaStyle style, ClassicBurnInOptions options)
        {
            if (!options.UseTextStyle) return;
            style.Alignment = options.Alignment.ToString(CultureInfo.InvariantCulture);
            style.MarginLeft = style.MarginRight = options.MarginHorizontal;
            style.MarginVertical = options.MarginVertical;
            style.ShadowWidth = options.ShadowWidth;
            if (options.BoxType == 0) style.Background = System.Drawing.Color.FromArgb(options.ShadowColorArgb);
            style.Spacing = options.Spacing;
            style.BorderStyle = options.BoxType == 1 ? "3" : options.BoxType == 2 ? "4" : "1";
        }

        public static string BuildFilter(string subtitleFileName, int width, int height, ClassicBurnInOptions options, string cutStart, out string extraInputs, out string mappings, out string endArguments)
        {
            extraInputs = string.Empty;
            mappings = string.Empty;
            endArguments = string.Empty;
            var inputCount = 1;
            var source = "[0:v]";
            if (options != null && options.InputIsAudioOnly)
            {
                extraInputs += " -f lavfi -i color=c=black:s=" + width + "x" + height + ":r=25";
                source = "[" + inputCount++ + ":v]";
                endArguments = " -shortest";
            }
            var image = options != null && !string.IsNullOrWhiteSpace(options.ImageSubtitleFileName);
            var logo = options != null && !string.IsNullOrWhiteSpace(options.LogoFileName);
            if (!image && !logo && (options == null || !options.InputIsAudioOnly))
                return "-vf \"scale=" + width + ":" + height + ",ass=" + Path.GetFileName(subtitleFileName) + "\"";

            var filter = source + "scale=" + width + ":" + height;
            if (image)
            {
                var seek = (cutStart ?? string.Empty).Trim();
                if (seek.StartsWith("-ss ", StringComparison.Ordinal)) extraInputs += " -itsoffset -" + seek.Substring(4).Trim();
                extraInputs += " -i \"" + options.ImageSubtitleFileName + "\"";
                filter += "[scaledvideo];[" + inputCount++ + ":s]scale=" + width + ":" + height + "[subtitles];[scaledvideo][subtitles]overlay";
            }
            else filter += ",ass=" + Path.GetFileName(subtitleFileName);
            if (logo)
            {
                extraInputs += " -i \"" + options.LogoFileName + "\"";
                var size = Math.Max(1, options.LogoSize).ToString(CultureInfo.InvariantCulture);
                var opacity = (Math.Max(0, Math.Min(100, options.LogoOpacity)) / 100.0).ToString(CultureInfo.InvariantCulture);
                filter += "[withsubtitles];[" + inputCount + ":v]scale=iw*" + size + "/100:ih*" + size + "/100,format=rgba,colorchannelmixer=aa=" + opacity + "[logo];[withsubtitles][logo]overlay=" + options.LogoX + ":" + options.LogoY;
            }
            mappings = " -map \"[burnout]\"";
            return "-filter_complex \"" + filter + "[burnout]\"";
        }
    }
}
