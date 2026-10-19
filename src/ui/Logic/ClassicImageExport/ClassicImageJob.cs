using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using SkiaSharp;

namespace Nikse.SubtitleEdit.Logic.ClassicImageExport
{
    public sealed class ClassicImageOptions
    {
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public float FontSize { get; set; } = 48;
        public string FontName { get; set; } = "Arial";
        public double FrameRate { get; set; } = 23.976;
        public int Margin { get; set; } = 30;
        public bool FullFrame { get; set; }
        public bool UseAssStyles { get; set; } = true;
        public TextEffectPreset Effect { get; set; }
        public Export3DMode Stereo { get; set; }
        public int Depth { get; set; }
        internal System.Collections.Generic.List<ClassicFontFile> Fonts;
    }

    public static class ClassicImageJob
    {
        public static ImageParameter Render(Subtitle subtitle, Paragraph cue, ClassicImageOptions options)
        {
            options.Fonts = options.Fonts ?? ClassicFontCollection.Scan(subtitle, CancellationToken.None);
            using (FontFaces.UseFonts(options.Fonts))
            {
            var ip = new ImageParameter { Text = ExportTextTags.ToRenderableText(cue.Text), StartTime = cue.StartTime.TimeSpan,
                EndTime = cue.EndTime.TimeSpan, ScreenWidth = options.Width, ScreenHeight = options.Height,
                FontName = options.FontName, FontSize = options.FontSize, FontColor = SKColors.White,
                OutlineColor = SKColors.Black, OutlineWidth = 2, ShadowColor = SKColors.Black, ShadowWidth = 1,
                BackgroundColor = SKColors.Transparent, LineSpacingPercent = 100, Alignment = ExportAlignment.BottomCenter,
                ContentAlignment = ExportContentAlignment.FromAlignment, BottomTopMargin = options.Margin,
                LeftRightMargin = options.Margin, FramesPerSecond = options.FrameRate,
                Mode3D = options.Stereo, Depth3D = options.Depth, IsForced = cue.Forced };
            var resolution = ExportTextTags.GetScriptResolution(subtitle.Header);
            if (options.UseAssStyles && !string.IsNullOrWhiteSpace(subtitle.Header) && subtitle.Header.Contains("[V4+ Styles]"))
            {
                var style = AdvancedSubStationAlpha.GetSsaStyle(cue.Extra, subtitle.Header);
                if (style != null)
                {
                    var scale = resolution.Height > 0 ? options.Height / (float)resolution.Height : 1;
                    ip.FontName = style.FontName; ip.FontSize = (float)style.FontSize * scale; ip.IsBold = style.Bold;
                    ip.FontColor = Color(style.Primary); ip.OutlineColor = Color(style.Outline); ip.ShadowColor = Color(style.Background);
                    ip.OutlineWidth = (double)style.OutlineWidth * scale; ip.ShadowWidth = (double)style.ShadowWidth * scale;
                    ip.Alignment = ExportTextTags.GetAlignment("{\\an" + style.Alignment + "}", ip.Alignment);
                    ip.BottomTopMargin=(int)(style.MarginVertical*scale);
                    ip.LeftRightMargin=(int)((style.Alignment=="3" || style.Alignment=="6" || style.Alignment=="9"?style.MarginRight:style.MarginLeft)*(resolution.Width>0?options.Width/(float)resolution.Width:1));
                    if(style.BorderStyle=="3"){ip.BackgroundColor=Color(style.Outline);ip.BoxType=ExportBoxType.OneBox;}
                    ip.Text = (style.Italic ? "<i>" : "") + ip.Text + (style.Italic ? "</i>" : "");
                }
            }
            ip.Alignment = ExportTextTags.GetAlignment(cue.Text, ip.Alignment);
            ExportTextTags.ApplyStyleOverrideTags(ip, cue.Text, resolution.Height);
            ExportTextTags.ApplyTransparencyTags(ip, cue.Text);
            ip.TextEffects = TextEffectPresetFactory.Create(options.Effect, ip.FontSize, ip.FontColor, ip.OutlineColor, ip.ShadowColor, new TextEffectAdjustments());
            ip.Bitmap.Dispose(); ip.Bitmap = ImageRenderer.GenerateBitmap(ip);
            ExportTextTags.ApplyPositionTag(ip, cue.Text, resolution.Width, resolution.Height);
            if (options.Stereo != Export3DMode.None) { Stereo3DImage.Apply(ip); }
            if (options.FullFrame) { var rendered = FullFrameImage.Create(ip); ip.Bitmap.Dispose(); ip.Bitmap = rendered; ip.OverridePosition = new SKPointI(0, 0); ip.BottomTopMargin = 0; }
            return ip;
            }
        }
        private static SKColor Color(System.Drawing.Color c) => new SKColor(c.R, c.G, c.B, c.A);

        // Export to a fresh directory: the complete set becomes visible through one directory rename.
        public static Task Export(Subtitle subtitle, ClassicImageOptions options, int format, string destination, CancellationToken token, IProgress<int> progress)
        {
            return Task.Run(() =>
            {
                if (subtitle.Paragraphs.Count == 0) throw new InvalidOperationException("No subtitles to export.");
                destination = Path.GetFullPath(destination);
                if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Choose a new output folder.");
                var parent = Path.GetDirectoryName(destination); Directory.CreateDirectory(parent);
                var stage = Path.Combine(parent, ".se-images-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
                try
                {
                    if (format == 3 && (options.Width != 720 || (options.Height != 576 && options.Height != 480)))
                        throw new InvalidOperationException("DVD SUP requires 720 x 576 (PAL) or 720 x 480 (NTSC).");
                    IExportHandler handler = format == 0 ? (IExportHandler)new ExportHandlerImscImage() : new ExportHandlerBdnXml(format == 2);
                    using (var dvd = format == 3 ? new ClassicExport.DvdSupWriter(Path.Combine(stage, "subtitles.sup"), options.Width, options.Height, options.Margin, options.Margin, System.Drawing.Color.White, System.Drawing.Color.Black, true) : null)
                    {
                        if (dvd == null) handler.WriteHeader(format == 0 ? Path.Combine(stage, "subtitles.ttml") : stage, new ImageParameter { ScreenWidth = options.Width, ScreenHeight = options.Height, FramesPerSecond = options.FrameRate });
                        var count = 0;
                        foreach (var cue in subtitle.Paragraphs)
                        {
                            token.ThrowIfCancellationRequested();
                            if (cue.EndTime.TotalMilliseconds <= cue.StartTime.TotalMilliseconds) throw new InvalidOperationException("Invalid subtitle duration: " + cue.Number);
                            var ip = Render(subtitle, cue, options);
                            using (ip.Bitmap)
                            {
                                if (dvd == null) handler.WriteParagraph(ip);
                                else using (var stream = new MemoryStream(ip.Bitmap.ToPngArray())) using (var bitmap = new System.Drawing.Bitmap(stream))
                                {
                                    var position = FullFrameImage.GetPosition(ip, bitmap.Width, bitmap.Height);
                                    dvd.WriteParagraph(cue, bitmap, System.Drawing.ContentAlignment.TopLeft, new System.Drawing.Point(position.X, position.Y));
                                }
                            }
                            progress?.Report(++count * 100 / subtitle.Paragraphs.Count);
                        }
                        if (dvd == null) handler.WriteFooter();
                    }
                    token.ThrowIfCancellationRequested(); Directory.Move(stage, destination);
                }
                finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
            }, token);
        }
    }
}
