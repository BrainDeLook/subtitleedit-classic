using SkiaSharp;
namespace Nikse.SubtitleEdit.Logic.ClassicDraw
{
    internal static class TextMeasurer
    {
        public static SKSize MeasureString(string text, string family, float size, SKFontStyleWeight weight)
        {
            using (var face = Nikse.SubtitleEdit.Logic.ClassicImageExport.FontFaces.CreateTypeface(family, weight >= SKFontStyleWeight.Bold, false))
            using (var font = new SKFont(face, size))
                return new SKSize(font.MeasureText(text), font.Metrics.Descent - font.Metrics.Ascent);
        }
    }
    internal static class DrawBuilderCompatibility
    {
        public static System.Text.StringBuilder Append(this System.Text.StringBuilder builder, System.IFormatProvider provider, string text) => builder.Append(text);
    }
}
