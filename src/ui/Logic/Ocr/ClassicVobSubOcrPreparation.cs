using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Nikse.SubtitleEdit.Core.Common;
using SkiaSharp;

namespace Nikse.SubtitleEdit.Logic.Ocr
{
    public static class ClassicVobSubOcrPreparation
    {
        // The recognizers use different masks: nOCR/BinaryOCR split the alpha plane,
        // while Tesseract and Vision engines expect opaque black text on white.
        public static Bitmap Prepare(Bitmap source, bool alphaMask)
        {
            using (var png = new MemoryStream())
            {
                source.Save(png, ImageFormat.Png);
                using (var decoded = SKBitmap.Decode(png.ToArray()))
                using (var isolated = VobSubColorIsolation.Isolate(decoded))
                {
                    using (var alpha = alphaMask ? new SKBitmap(new SKImageInfo(isolated.Width, isolated.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul)) : null)
                    {
                    if (alphaMask)
                    {
                        for (var y = 0; y < isolated.Height; y++)
                        for (var x = 0; x < isolated.Width; x++)
                            alpha.SetPixel(x, y, isolated.GetPixel(x, y).Red == 0 ? SKColors.White : SKColors.Transparent);
                    }
                    using (var image = SKImage.FromBitmap(alpha ?? isolated))
                    using (var bytes = image.Encode(SKEncodedImageFormat.Png, 100))
                    using (var stream = bytes.AsStream())
                    using (var bitmap = new Bitmap(stream)) return new Bitmap(bitmap);
                    }
                }
            }
        }
    }
}
