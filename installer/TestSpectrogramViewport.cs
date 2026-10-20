using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;

class TestSpectrogramViewport
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
    static SpectrogramData Spectrum(int seed)
    {
        var tiles = new List<Bitmap>();
        for (int t = 0; t < 10; t++)
        {
            var tile = new Bitmap(128, 128);
            for (int x = 0; x < 128; x++) for (int y = 0; y < 128; y++)
                tile.SetPixel(x, y, Color.FromArgb((x * 3 + seed + t * 17) % 256, (y * 2 + t) % 256, (x + y + seed) % 256));
            tiles.Add(tile);
        }
        return new SpectrogramData(256, 128, .1, tiles);
    }
    static object Cache(AudioVisualizer view) => typeof(AudioVisualizer).GetField("_spectrogramViewport", Flags).GetValue(view);
    static Bitmap Render(AudioVisualizer view, SpectrogramData data, bool original)
    {
        var image = new Bitmap(view.Width, view.Height);
        using (var g = Graphics.FromImage(image))
        {
            g.Clear(Color.FromArgb(31, 47, 63));
            if (!original) typeof(AudioVisualizer).GetMethod("DrawSpectrogram", Flags).Invoke(view, new object[] { g, 90 });
            else
            {
                int columns = (int)Math.Round((view.EndPositionSeconds - view.StartPositionSeconds) / data.SampleDuration);
                int left = (int)Math.Round(view.StartPositionSeconds / data.SampleDuration);
                using (var source = new Bitmap(columns, 128))
                using (var sg = Graphics.FromImage(source))
                {
                    int offset = 0, index = left / data.ImageWidth;
                    while (offset < columns && index < data.Images.Count)
                    {
                        int x = (left + offset) % data.ImageWidth, w = Math.Min(data.ImageWidth - x, columns - offset);
                        sg.DrawImage(data.Images[index++], offset, 0, new Rectangle(x, 0, w, 128), GraphicsUnit.Pixel);
                        offset += w;
                    }
                    int height = view.CombineSpectrogramAndWaveform ? 90 : (view.ShowWaveform ? 128 : view.Height);
                    var destination = new Rectangle(0, view.Height - height, view.Width, height);
                    if (view.CombineSpectrogramAndWaveform)
                    {
                        using (var attributes = new ImageAttributes())
                        {
                            var matrix = new ColorMatrix { Matrix33 = view.SpectrogramAlpha };
                            attributes.SetColorMatrix(matrix);
                            g.DrawImage(source, destination, 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                        }
                    }
                    else g.DrawImage(source, destination);
                }
            }
        }
        return image;
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            using (var view = new AudioVisualizer())
            {
                using (var generator = new WavePeakGenerator(args[0])) view.WavePeaks = generator.GeneratePeaks(0, args[1]);
                var data = Spectrum(7); view.SetSpectrogram(data);
                foreach (bool combined in new[] { false, true }) foreach (bool wave in new[] { false, true }) foreach (float alpha in new[] { 1f, .4f })
                {
                    view.Size = new Size(683, 220); view.ShowWaveform = wave; view.CombineSpectrogramAndWaveform = combined; view.SpectrogramAlpha = alpha;
                    view.SetPosition(11.7, new Subtitle(), 12, -1, null);
                    using (var expected = Render(view, data, true)) using (var actual = Render(view, data, false))
                    {
                        int max = 0;
                        for (int x = 0; x < actual.Width; x++) for (int y = 0; y < actual.Height; y++)
                        {
                            var a = expected.GetPixel(x, y); var b = actual.GetPixel(x, y);
                            max = Math.Max(max, Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B))));
                        }
                        Check(max <= 2, "pixel parity combined=" + combined + " wave=" + wave + " alpha=" + alpha + " max=" + max);
                    }
                    var cached = Cache(view); using (Render(view, data, false)) { }
                    Check(ReferenceEquals(cached, Cache(view)), "stationary viewport reuses cache");
                }
                var prior = Cache(view); view.SetPosition(24.1, new Subtitle(), 25, -1, null); using (Render(view, data, false)) { }
                Check(!ReferenceEquals(prior, Cache(view)), "scroll refreshes cache");
                prior = Cache(view); view.Width += 37; using (Render(view, data, false)) { }
                Check(!ReferenceEquals(prior, Cache(view)), "resize refreshes cache");
                prior = Cache(view); view.ZoomFactor = 2; using (Render(view, data, false)) { }
                Check(!ReferenceEquals(prior, Cache(view)), "zoom refreshes cache");
                view.SetSpectrogram(Spectrum(99)); Check(Cache(view) == null, "new source clears cache");
                using (Render(view, data, false)) { } view.SetSpectrogram(null); Check(Cache(view) == null, "unload clears cache");
                view.SetSpectrogram(Spectrum(33)); using (Render(view, data, false)) { } view.Dispose(); Check(Cache(view) == null, "dispose releases cached bitmap");
            }
            Console.WriteLine("Passed " + checks + " checks"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

