using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Logic
{
    public static class ClassicReferenceAudio
    {
        public static async Task<string> ExtractAsync(string source, double start, double seconds, string job, CancellationToken cancel, int audioTrack = 0)
        {
            if (!File.Exists(source) || double.IsNaN(start) || double.IsInfinity(start) || start < 0 || double.IsNaN(seconds) || seconds < 1 || seconds > 30) throw new ArgumentException("Select a recording and a 1–30 second range.");
            Directory.CreateDirectory(job); var output = Path.Combine(job, "reference-" + Guid.NewGuid().ToString("N") + ".wav");
            try
            {
                await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation, "-nostdin -y -ss " + start.ToString(CultureInfo.InvariantCulture) + " -i " + ClassicSpeechRunner.Quote(source) + " -t " + seconds.ToString(CultureInfo.InvariantCulture) + " -map 0:a:" + Math.Max(0,audioTrack) + " -vn -ac 1 -ar 16000 -c:a pcm_s16le " + ClassicSpeechRunner.Quote(output)), cancel, null).ConfigureAwait(false);
                using (var stream = File.OpenRead(output)) if (new WaveHeader(stream).LengthInSeconds < .5) throw new InvalidDataException("The selected range contains no usable audio.");
                return output;
            }
            catch { if (File.Exists(output)) File.Delete(output); throw; }
        }
        public static string Transcript(Subtitle subtitle) => string.Join(" ", (subtitle ?? new Subtitle()).Paragraphs.Select(p => (p.Text ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim()).Where(t => t.Length > 0));
    }
}
