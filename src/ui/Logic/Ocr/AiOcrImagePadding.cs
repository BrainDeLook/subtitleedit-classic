using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Nikse.SubtitleEdit.Logic.Ocr
{
    public enum AiOcrPadding { Square, Border }

    // Model-specific framing from Subtitle Edit v5.4.0, using Classic's GDI bitmap format.
    public static class AiOcrImagePadding
    {
        public static Bitmap Pad(Bitmap source, AiOcrPadding padding)
        {
            int width, height;
            if (padding == AiOcrPadding.Border)
            {
                var border = Math.Max(10, (int)(source.Height * 0.1));
                width = source.Width + 2 * border;
                height = source.Height + 2 * border;
            }
            else
            {
                var longest = Math.Max(source.Width, source.Height);
                width = height = longest + (int)(longest * 0.2);
            }
            var result = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(result))
            {
                graphics.Clear(Color.Black);
                graphics.DrawImageUnscaled(source, (width - source.Width) / 2, (height - source.Height) / 2);
            }
            return result;
        }
    }
}
