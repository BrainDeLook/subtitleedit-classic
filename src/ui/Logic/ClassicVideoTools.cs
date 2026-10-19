using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicCutSegment { public double Start { get; set; } public double End { get; set; } }
    public sealed class ClassicMappedStream { public string File { get; set; } public ClassicMediaStream Stream { get; set; } public override string ToString() => Path.GetFileName(File) + " | " + Stream; }
    public sealed class ClassicVideoChapter { public double Start { get; set; } public string Title { get; set; } }
    public static class ClassicVideoTools
    {
        private static string N(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
        private static string Q(string value) => ClassicMediaJobs.Q(value);
        private static async Task Run(string input, string output, string arguments, CancellationToken token)
        {
            ClassicMediaJobs.CheckOutput(input, output);
            var stage = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)), ".se-video-" + Guid.NewGuid().ToString("N") + Path.GetExtension(output));
            try { await ClassicMediaJobs.Render(arguments, stage, token); var probe = await ClassicMediaJobs.Probe(stage, token); if (probe.Streams.Count == 0) throw new IOException("The rendered file contains no media streams."); token.ThrowIfCancellationRequested(); ClassicMediaJobs.Commit(stage, output); }
            finally { if (File.Exists(stage)) File.Delete(stage); }
        }
        public static Task Cut(string input, string output, IList<ClassicCutSegment> clips, int audioIndex, double transitionSeconds, string transition, double sourceDuration, CancellationToken token)
        {
            if (clips.Count == 0) throw new ArgumentException("Add at least one segment.");
            foreach (var clip in clips) if (clip.Start < 0 || clip.End <= clip.Start || clip.End > sourceDuration + .05 || (clips.Count > 1 && transitionSeconds > 0 && transitionSeconds * 2 >= clip.End - clip.Start)) throw new ArgumentException("Invalid segment or transition longer than half a segment.");
            if (transitionSeconds < 0 || !new[] { "fade", "fadeblack", "wipeleft", "slideright", "none" }.Contains(transition)) throw new ArgumentException("Invalid transition.");
            if (transition == "none") transitionSeconds = 0;
            var filters = new List<string>();
            for (var i = 0; i < clips.Count; i++)
            {
                var c = clips[i];
                filters.Add("[0:v:0]trim=start=" + N(c.Start) + ":end=" + N(c.End) + ",setpts=PTS-STARTPTS,fps=30,format=yuv420p,settb=AVTB,setsar=1[v" + i + "]");
                if (audioIndex >= 0) filters.Add("[0:" + audioIndex + "]atrim=start=" + N(c.Start) + ":end=" + N(c.End) + ",asetpts=PTS-STARTPTS,aresample=48000,aformat=channel_layouts=stereo,apad,atrim=duration=" + N(c.End - c.Start) + "[a" + i + "]");
            }
            var video = "v0"; var audio = "a0"; var length = clips[0].End - clips[0].Start;
            for (var i = 1; i < clips.Count; i++)
            {
                var v = "jv" + i; var a = "ja" + i;
                if (transitionSeconds > 0)
                {
                    filters.Add("[" + video + "][v" + i + "]xfade=transition=" + transition + ":duration=" + N(transitionSeconds) + ":offset=" + N(length - transitionSeconds) + "[" + v + "]");
                    if (audioIndex >= 0) filters.Add("[" + audio + "][a" + i + "]acrossfade=d=" + N(transitionSeconds) + ":c1=tri:c2=tri[" + a + "]");
                }
                else
                {
                    filters.Add("[" + video + "][v" + i + "]concat=n=2:v=1:a=0[" + v + "]");
                    if (audioIndex >= 0) filters.Add("[" + audio + "][a" + i + "]concat=n=2:v=0:a=1[" + a + "]");
                }
                length += clips[i].End - clips[i].Start - transitionSeconds; video = v; audio = a;
            }
            return Run(input, output, "-i " + Q(input) + " -filter_complex_threads 1 -filter_complex " + Q(string.Join(";", filters)) + " -map " + Q("[" + video + "]") + (audioIndex < 0 ? " -an" : " -map " + Q("[" + audio + "]") + " -c:a aac -b:a 192k -ac 2") + " -c:v libx264 -preset fast -crf 18 -map_chapters -1", token);
        }
        public static Task Reencode(string input, string output, IEnumerable<int> audioIndices, int profile, CancellationToken token)
        {
            if (profile < 0 || profile > 2) throw new ArgumentOutOfRangeException(nameof(profile));
            var codec = profile == 0 ? "-c:v libx264 -preset fast -crf 18 -g 12 -pix_fmt yuv420p" : profile == 1 ? "-c:v libx264 -preset fast -crf 18 -g 1 -pix_fmt yuv420p" : "-c:v prores_ks -profile:v 3 -pix_fmt yuv422p10le";
            return Run(input, output, "-i " + Q(input) + " -map 0:v:0 " + string.Join(" ", audioIndices.Distinct().Select(i => "-map 0:" + i)) + " " + codec + (profile == 2 ? " -c:a pcm_s24le" : " -c:a aac -b:a 192k") + " -map_metadata 0 -map_chapters 0", token);
        }
        public static Task Remux(string input, string output, IList<ClassicMappedStream> streams, bool mix, CancellationToken token)
        {
            if (streams.Count == 0) throw new ArgumentException("Select streams to include.");
            foreach (var stream in streams) ClassicMediaJobs.CheckOutput(stream.File, output);
            var files = streams.Select(s => s.File).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var args = new StringBuilder(string.Join(" ", files.Select(f => "-i " + Q(f))));
            var audio = streams.Where(s => s.Stream.Type == "audio").ToList();
            var subtitleCodec = Path.GetExtension(output).Equals(".mp4", StringComparison.OrdinalIgnoreCase) ? "mov_text" : "copy";
            foreach (var s in streams.Where(s => !mix || s.Stream.Type != "audio")) args.Append(" -map ").Append(files.FindIndex(f => f.Equals(s.File, StringComparison.OrdinalIgnoreCase))).Append(':').Append(s.Stream.Index);
            args.Append(" -c copy -c:s ").Append(subtitleCodec);
            if (mix)
            {
                if (audio.Count < 2) throw new ArgumentException("Select at least two audio streams for mixing.");
                var filter = new StringBuilder();
                for (var i = 0; i < audio.Count; i++) filter.Append('[').Append(files.FindIndex(f => f.Equals(audio[i].File, StringComparison.OrdinalIgnoreCase))).Append(':').Append(audio[i].Stream.Index).Append("]aresample=48000,aformat=channel_layouts=stereo[m").Append(i).Append("];");
                for (var i = 0; i < audio.Count; i++) filter.Append("[m").Append(i).Append(']');
                filter.Append("amix=inputs=").Append(audio.Count).Append(":duration=longest:normalize=1[mixed]");
                args.Append(" -filter_complex ").Append(Q(filter.ToString())).Append(" -map \"[mixed]\" -c:a aac -ac 2 -b:a 192k -metadata:s:a:0 title=\"Stereo mix\"");
            }
            return Run(input, output, args.ToString(), token);
        }
        public static string ChapterMetadata(IEnumerable<ClassicVideoChapter> chapters, double duration)
        {
            var list = chapters.OrderBy(c => c.Start).ToList();
            if (list.Any(c => c.Start < 0 || c.Start >= duration || double.IsNaN(c.Start)) || list.Select(c => c.Start).Distinct().Count() != list.Count) throw new ArgumentException("Chapter starts must be unique and inside the video duration.");
            var text = new StringBuilder(";FFMETADATA1\n");
            for (var i = 0; i < list.Count; i++) text.Append("[CHAPTER]\nTIMEBASE=1/1000\nSTART=").Append((long)Math.Round(list[i].Start * 1000)).Append("\nEND=").Append((long)Math.Round((i + 1 < list.Count ? list[i + 1].Start : duration) * 1000)).Append("\ntitle=").Append(EscapeMetadata(list[i].Title)).Append('\n');
            return text.ToString();
        }
        private static string EscapeMetadata(string value) => (value ?? "").Replace("\\", "\\\\").Replace("=", "\\=").Replace(";", "\\;").Replace("#", "\\#").Replace("\r", "").Replace("\n", "\\\n");
        public static async Task Chapters(string input, string output, IEnumerable<ClassicVideoChapter> chapters, double duration, CancellationToken token)
        {
            var metadata = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)), ".se-chapters-" + Guid.NewGuid().ToString("N") + ".txt");
            try { File.WriteAllText(metadata, ChapterMetadata(chapters, duration), new UTF8Encoding(false)); await Run(input, output, "-i " + Q(input) + " -f ffmetadata -i " + Q(metadata) + " -map 0 -map_metadata 0 -map_chapters 1 -c copy", token); }
            finally { if (File.Exists(metadata)) File.Delete(metadata); }
        }
    }
}
