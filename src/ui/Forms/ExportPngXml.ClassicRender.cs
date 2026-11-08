using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.ClassicImageExport;
using SkiaSharp;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class ExportPngXml
    {
        private static Bitmap RenderClassicImage(MakeBitmapParameter parameter)
        {
            var alignment = parameter.Alignment;
            if (alignment == 0) alignment = ContentAlignment.BottomCenter;
            var exportAlignment = (ExportAlignment)Array.IndexOf(new[] {
                ContentAlignment.TopLeft, ContentAlignment.TopCenter, ContentAlignment.TopRight,
                ContentAlignment.MiddleLeft, ContentAlignment.MiddleCenter, ContentAlignment.MiddleRight,
                ContentAlignment.BottomLeft, ContentAlignment.BottomCenter, ContentAlignment.BottomRight }, alignment);
            var options = new ClassicImageOptions { Width = parameter.ScreenWidth, Height = parameter.ScreenHeight,
                FontName = parameter.SubtitleFontName, FontSize = parameter.SubtitleFontSize,
                FrameRate = parameter.FramesPerSeconds, Margin = parameter.BottomMargin,
                Stereo = (Export3DMode)parameter.Type3D, Depth = parameter.Depth3D, Fonts = parameter.Fonts };
            var defaults = new ImageParameter { Text = parameter.P.Text, StartTime = parameter.P.StartTime.TimeSpan,
                EndTime = parameter.P.EndTime.TimeSpan, ScreenWidth = parameter.ScreenWidth, ScreenHeight = parameter.ScreenHeight,
                FontName = parameter.SubtitleFontName, FontSize = parameter.SubtitleFontSize, FontColor = ToSkia(parameter.SubtitleColor),
                IsBold = parameter.SubtitleFontBold, OutlineColor = ToSkia(parameter.BorderColor), OutlineWidth = parameter.BorderWidth,
                ShadowColor = new SKColor(parameter.ShadowColor.R, parameter.ShadowColor.G, parameter.ShadowColor.B, (byte)parameter.ShadowAlpha),
                ShadowWidth = parameter.ShadowWidth, BackgroundColor = ToSkia(parameter.BackgroundColor),
                BoxType = parameter.BackgroundColor.A == 0 ? ExportBoxType.None : parameter.BoxSingleLine ? ExportBoxType.BoxPerLine : ExportBoxType.OneBox,
                Alignment = exportAlignment, ContentAlignment = parameter.AlignLeft || parameter.JustifyLeft ? ExportContentAlignment.Left :
                    parameter.AlignRight || parameter.JustifyRight ? ExportContentAlignment.Right : ExportContentAlignment.FromAlignment,
                BottomTopMargin = parameter.BottomMargin, LeftRightMargin = parameter.RightMargin > 0 &&
                    (alignment == ContentAlignment.BottomRight || alignment == ContentAlignment.MiddleRight || alignment == ContentAlignment.TopRight) ? parameter.RightMargin : parameter.LeftMargin,
                LineSpacingPercent = 0, FramesPerSecond = parameter.FramesPerSeconds,
                Mode3D = (Export3DMode)parameter.Type3D, Depth3D = parameter.Depth3D, IsForced = parameter.Forced };
            if (parameter.LineHeight != null && parameter.LineHeight.TryGetValue(GetStyleName(parameter.P), out var advance))
            {
                var scriptHeight = ExportTextTags.GetScriptResolution(parameter.SourceSubtitle.Header).Height;
                defaults.LineAdvancePixels = (float)(Math.Max(1, advance) * parameter.ScreenHeight / (double)(scriptHeight > 0 ? scriptHeight : 288));
            }
            var image = ClassicImageJob.Render(parameter.SourceSubtitle, parameter.P, options, defaults);
            using (image.Bitmap)
            using (var bytes = new MemoryStream(image.Bitmap.ToPngArray()))
            using (var decoded = new Bitmap(bytes))
            {
                parameter.OverridePosition = image.OverridePosition.HasValue ? new Point(image.OverridePosition.Value.X, image.OverridePosition.Value.Y) : (Point?)null;
                parameter.Alignment = new[] { ContentAlignment.TopLeft, ContentAlignment.TopCenter, ContentAlignment.TopRight,
                    ContentAlignment.MiddleLeft, ContentAlignment.MiddleCenter, ContentAlignment.MiddleRight,
                    ContentAlignment.BottomLeft, ContentAlignment.BottomCenter, ContentAlignment.BottomRight }[(int)image.Alignment];
                parameter.SubtitleColor = Color.FromArgb(image.FontColor.Alpha, image.FontColor.Red, image.FontColor.Green, image.FontColor.Blue);
                parameter.BottomMargin = image.BottomTopMargin;
                parameter.LeftMargin = parameter.RightMargin = image.LeftRightMargin;
                return new Bitmap(decoded);
            }
        }
        private static SKColor ToSkia(Color color) => new SKColor(color.R, color.G, color.B, color.A);

        private Bitmap CreateClassicExportPreviewFrame(int index)
        {
            GetResolution(out var width, out var height);
            var bitmap = new Bitmap(width, height);
            try
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    FillPreviewBackground(bitmap, graphics, _subtitle.Paragraphs[index]);
                    var parameter = MakeMakeBitmapParameter(index, width, height);
                    using (var text = GenerateImageFromTextWithStyle(parameter))
                    {
                        var x = parameter.Alignment == ContentAlignment.TopLeft || parameter.Alignment == ContentAlignment.MiddleLeft || parameter.Alignment == ContentAlignment.BottomLeft
                            ? parameter.LeftMargin : parameter.Alignment == ContentAlignment.TopRight || parameter.Alignment == ContentAlignment.MiddleRight || parameter.Alignment == ContentAlignment.BottomRight
                            ? width - text.Width - parameter.RightMargin : (width - text.Width) / 2;
                        var y = parameter.Alignment == ContentAlignment.TopLeft || parameter.Alignment == ContentAlignment.TopCenter || parameter.Alignment == ContentAlignment.TopRight
                            ? parameter.BottomMargin : parameter.Alignment == ContentAlignment.MiddleLeft || parameter.Alignment == ContentAlignment.MiddleCenter || parameter.Alignment == ContentAlignment.MiddleRight
                            ? (height - text.Height) / 2 : height - text.Height - parameter.BottomMargin;
                        var point = parameter.OverridePosition ?? new Point(x, y);
                        graphics.DrawImageUnscaled(text, point);
                    }
                }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }
    }
}

