using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Nikse.SubtitleEdit.Logic.ClassicMusic
{
    // Keep memory bounded by the generated clip, even for feature-length output.
    public static class MusicStreamRenderer
    {
        public static void Write(MusicAudio source, MusicLoop loop, double seconds, string output, CancellationToken token)
        {
            if (seconds <= 0 || seconds > 10800 || double.IsNaN(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            int channels = source.Channels, rate = source.SampleRate;
            long frames = (long)Math.Round(seconds * rate), dataBytes = checked(frames * channels * 2);
            if (dataBytes > uint.MaxValue - 36) throw new ArgumentException("The PCM WAV exceeds the RIFF limit. Choose a shorter output.");
            int p = MusicMath.Clamp((int)Math.Round(loop.StartSeconds * rate), 0, source.FrameCount);
            int end = MusicMath.Clamp((int)Math.Round(loop.EndSeconds * rate), 0, source.FrameCount);
            int xf = MusicMath.Clamp((int)Math.Round(loop.CrossfadeSeconds * rate), 0, Math.Min(p, Math.Max(0, end - p)));
            bool repeat = frames > end && end - p - xf > 0; int unit = end - p;
            long fade = Math.Min(frames, 3L * rate);
            float Sample(long frame, int channel)
            {
                float sample;
                if (!repeat || frame < p) sample = frame < source.FrameCount ? source.Samples[(int)frame * channels + channel] : 0;
                else
                {
                    int at = (int)((frame - p) % unit);
                    if (at < unit - xf) sample = source.Samples[(p + at) * channels + channel];
                    else { int i = at - (unit - xf); double t = (i + .5) / xf; double a = loop.EqualGainCrossfade ? 1 - t : Math.Cos(t * Math.PI / 2), b = loop.EqualGainCrossfade ? t : Math.Sin(t * Math.PI / 2); sample = (float)(source.Samples[(end - xf + i) * channels + channel] * a + source.Samples[(p - xf + i) * channels + channel] * b); }
                }
                if (fade > 0 && frame >= frames - fade) sample *= (float)((double)(frames - frame) / fade);
                return sample;
            }
            double peak = 0, sum = 0, blockSum = 0; long count = 0; int block = Math.Max(1, rate / 10), inBlock = 0;
            for (long frame = 0; frame < frames; frame++)
            {
                if ((frame & 8191) == 0) token.ThrowIfCancellationRequested();
                float mono = 0; for (int c = 0; c < channels; c++) { float v = Sample(frame, c); mono += v; peak = Math.Max(peak, Math.Abs(v)); }
                mono /= channels; blockSum += mono * mono;
                if (++inBlock == block) { if (blockSum / block > 1e-6) { sum += blockSum; count += block; } blockSum = 0; inBlock = 0; }
            }
            float gain = count == 0 || peak <= 0 ? 1 : (float)Math.Min(.1 / Math.Sqrt(sum / count), Math.Pow(10, -1.0 / 20) / peak);
            using (var writer = new BinaryWriter(File.Create(output)))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write((uint)(dataBytes + 36)); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)channels); writer.Write(rate); writer.Write(rate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write((uint)dataBytes);
                var buffer = new byte[32768]; int used = 0;
                for (long frame = 0; frame < frames; frame++)
                {
                    if ((frame & 8191) == 0) token.ThrowIfCancellationRequested();
                    for (int c = 0; c < channels; c++) { short v = (short)Math.Round(MusicMath.Clamp(Sample(frame, c) * gain, -1f, 1f) * 32767f); buffer[used++] = (byte)v; buffer[used++] = (byte)(v >> 8); if (used == buffer.Length) { writer.Write(buffer); used = 0; } }
                }
                if (used > 0) writer.Write(buffer, 0, used);
            }
        }
    }
}
