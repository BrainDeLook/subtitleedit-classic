using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Cea608;
using Nikse.SubtitleEdit.Core.ContainerFormats.ProgramStream;

namespace Nikse.SubtitleEdit.Logic
{
    public static class ClassicCaptionRemux
    {
        public static bool IsMpeg(string file) => new[] { ".mpg", ".mpeg", ".vob" }.Contains(Path.GetExtension(file).ToLowerInvariant());
        public static async Task Write(string input, string output, IList<ClassicMappedStream> streams, bool mix, CancellationToken token)
        {
            var captions = streams.Where(s => s.Stream.Type == "subtitle").ToList();
            if (captions.Count > 2) throw new ArgumentException("MPEG caption authoring supports at most two tracks (CC1 and CC3).");
            if (!streams.Any(s => s.Stream.Type == "video")) throw new ArgumentException("Select a video stream for MPEG caption authoring.");
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
                if (paths.Count == 2) bytes.Field2 = ClosedCaptionBytes.FromFile(paths[1]).Field1;
                if (paths.Count > 0 && bytes.Field1.Count + bytes.Field2.Count + bytes.Dtvcc.Count == 0) throw new InvalidDataException("Selected subtitles produced no caption bytes.");
                var mpeg = Path.Combine(folder, "source.mpg");
                await ClassicVideoTools.RemuxStreams(input, mpeg, streams.Where(s => s.Stream.Type != "subtitle").ToList(), mix, token, true);
                var stage = Path.Combine(folder, "captions.mpg");
                await Task.Run(() => ProgramStreamClosedCaptionWriter.Write(mpeg, stage, bytes, progress => token.ThrowIfCancellationRequested()), token);
                token.ThrowIfCancellationRequested(); ClassicMediaJobs.Commit(stage, output);
            }
            finally { Directory.Delete(folder, true); }
        }
    }
}
