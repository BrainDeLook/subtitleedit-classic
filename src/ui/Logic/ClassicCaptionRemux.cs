using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Cea608;
using Nikse.SubtitleEdit.Core.ContainerFormats.ProgramStream;
using Nikse.SubtitleEdit.Core.ContainerFormats.Mp4;

namespace Nikse.SubtitleEdit.Logic
{
    public static class ClassicCaptionRemux
    {
        public static bool IsMpeg(string file) => new[] { ".mpg", ".mpeg", ".vob" }.Contains(Path.GetExtension(file).ToLowerInvariant());
        public static async Task Write(string input, string output, IList<ClassicMappedStream> streams, bool mix, CancellationToken token, double captionOffsetMs = 0)
        {
            if (double.IsNaN(captionOffsetMs) || double.IsInfinity(captionOffsetMs) || Math.Abs(captionOffsetMs) > 86400000) throw new ArgumentOutOfRangeException(nameof(captionOffsetMs));
            var mp4 = Path.GetExtension(output).Equals(".mp4", StringComparison.OrdinalIgnoreCase);
            var captions = streams.Where(s => s.Stream.Type == "subtitle").ToList();
            if (captions.Count > 2) throw new ArgumentException("Caption authoring supports at most two tracks (CC1 and CC3).");
            if (!streams.Any(s => s.Stream.Type == "video")) throw new ArgumentException("Select a video stream for Caption authoring.");
            var folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)), ".se-captions-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try
            {
                var paths = new List<string>();
                foreach (var track in captions)
                {
                    token.ThrowIfCancellationRequested(); ClassicMediaJobs.CheckOutput(track.File, output);
                    if (new[] { ".scc", ".mcc", ".srt", ".ass", ".vtt", ".ttml" }.Contains(Path.GetExtension(track.File).ToLowerInvariant())) paths.Add(track.File);
                    else
                    {
                        var extracted = Path.Combine(folder, "caption-" + paths.Count + ".srt");
                        await ClassicMediaJobs.Render("-i " + ClassicMediaJobs.Q(track.File) + " -map 0:" + track.Stream.Index + " -c:s srt", extracted, token); paths.Add(extracted);
                    }
                }
                var bytes = paths.Count == 0 ? new ClosedCaptionBytes() : ClosedCaptionBytes.FromFile(paths[0]);
                bytes.Shift(captionOffsetMs);
                if (paths.Count == 2)
                {
                    var second = ClosedCaptionBytes.FromFile(paths[1]); second.Shift(captionOffsetMs);
                    if (bytes.Field2.Count > 0 || second.Field2.Count > 0 || second.HasCea708) throw new ArgumentException("Use one MCC file for multi-service captions, or two SCC/text files for CC1 and CC3.");
                    bytes.Field2 = second.Field1;
                }
                if (paths.Count > 0 && bytes.Field1.Count + bytes.Field2.Count + bytes.Dtvcc.Count == 0) throw new InvalidDataException("Selected subtitles produced no caption bytes.");
                var mpeg = Path.Combine(folder, mp4 ? "source.mp4" : "source.mpg");
                await ClassicVideoTools.RemuxStreams(input, mpeg, streams.Where(s => s.Stream.Type != "subtitle").ToList(), mix, token, !mp4);
                var stage = Path.Combine(folder, mp4 ? "captions.mp4" : "captions.mpg");
                await Task.Run(() =>
                {
                    if (mp4) Mp4ClosedCaptionWriter.Write(mpeg, stage, bytes, progress => token.ThrowIfCancellationRequested());
                    else ProgramStreamClosedCaptionWriter.Write(mpeg, stage, bytes, progress => token.ThrowIfCancellationRequested());
                }, token);
                token.ThrowIfCancellationRequested(); ClassicMediaJobs.Commit(stage, output);
            }
            finally { Directory.Delete(folder, true); }
        }
    }
}
