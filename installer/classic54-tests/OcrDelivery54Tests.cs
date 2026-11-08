using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic.Ocr;
using Nikse.SubtitleEdit.Controls;

namespace Classic54Tests
{
    public class OcrDelivery54Tests
    {
        [Theory][InlineData("Latin", 2287)][InlineData("Cyrillic", 1631)]
        public void StandardDatabaseAddsBundledGlyphsWithoutOverwritingTrainedData(string name, int bundledCount)
        {
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nocr-delivery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var path = Path.Combine(folder, name + ".nocr");
                var db = new NOcrDb(Path.Combine(folder, "empty.nocr"));
                var custom = new NOcrChar("trained-user-character") { Width = 10, Height = 20 };
                db.Add(custom);
                var copy = new NOcrDb(db, path); copy.Save();
                var original = File.ReadAllBytes(path);
                var upgraded = new NOcrDb(path);
                Assert.Equal(bundledCount + 1, upgraded.TotalCharacterCount);
                Assert.Equal("trained-user-character", upgraded.OcrCharactersCombined.First().Text);
                Assert.True(original.SequenceEqual(File.ReadAllBytes(path)));
                upgraded.Save();
                Assert.Equal(bundledCount + 1, new NOcrDb(path).TotalCharacterCount);
            }
            finally { Directory.Delete(folder, true); }
        }
        [Fact]
        public void ACustomDatabaseDoesNotAcquireStandardGlyphs()
        {
            var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "trained-" + Guid.NewGuid().ToString("N") + ".nocr");
            try
            {
                var db = new NOcrDb(file); db.Add(new NOcrChar("user") { Width = 10, Height = 20 }); db.Save();
                Assert.Equal(1, new NOcrDb(file).TotalCharacterCount);
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }
        private static Bitmap PaletteImage()
        {
            var image = new Bitmap(40, 30);
            using (var g = Graphics.FromImage(image))
            {
                g.Clear(Color.Transparent);
                g.FillRectangle(Brushes.Black, 4, 4, 32, 22); // outline larger than fill
                g.FillRectangle(Brushes.Yellow, 10, 9, 20, 12);
            }
            return image;
        }
        [Theory][InlineData(true)][InlineData(false)]
        public void VobSubPreparationSeparatesFillFromLargerOutline(bool alpha)
        {
            using (var source = PaletteImage())
            using (var result = ClassicVobSubOcrPreparation.Prepare(source, alpha))
            {
                Assert.Equal(40, result.Width); Assert.Equal(30, result.Height);
                Assert.Equal(alpha ? Color.White.ToArgb() : Color.Black.ToArgb(), result.GetPixel(12, 12).ToArgb());
                Assert.Equal(alpha ? 0 : 255, result.GetPixel(5, 5).A);
                Assert.Equal(alpha ? 0 : 255, result.GetPixel(0, 0).A);
                Assert.Equal(Color.Yellow.ToArgb(), source.GetPixel(12, 12).ToArgb());
            }
        }
        [Fact]
        public void ActualVobSubOcrFormPreparesPaletteBeforeUnattendedRecognition()
        {
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vob-palette-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var savedLanguage = Configuration.Settings.General.Language;
            Configuration.Settings.General.Language = "en";
            try
            {
                var file = Path.Combine(folder, "palette.sub");
                using (var source = PaletteImage())
                using (var writer = new Nikse.SubtitleEdit.Core.VobSub.VobSubWriter(file, 720, 576, 20, 20, 32, Color.Yellow, Color.Black, true, Nikse.SubtitleEdit.Core.Common.DvdSubtitleLanguage.CompliantLanguages.First()))
                { writer.WriteParagraph(new Paragraph("", 1000, 2000), source, ContentAlignment.BottomCenter); writer.WriteIdxFile(); }
                var parser = new Nikse.SubtitleEdit.Core.VobSub.VobSubParser(true);
                parser.OpenSubIdx(file, Path.ChangeExtension(file, ".idx"));
                using (var ocr = new Nikse.SubtitleEdit.Forms.Ocr.VobSubOcr())
                {
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var type = ocr.GetType();
                    type.GetField("_vobSubMergedPackList", flags).SetValue(ocr, parser.MergeVobSubPacks());
                    type.GetField("_palette", flags).SetValue(ocr, parser.IdxPalette);
                    type.GetField("_ocrMethodIndex", flags).SetValue(ocr, type.GetField("_ocrMethodNocr", flags).GetValue(ocr));
                    ((CheckBox)type.GetField("checkBoxCustomFourColors", flags).GetValue(ocr)).Checked = false;
                    type.GetField("_classicIsolateVobSubColors", flags).SetValue(ocr, false);
                    using (var raw = ocr.GetSubtitleBitmap(0))
                    {
                        type.GetField("_classicIsolateVobSubColors", flags).SetValue(ocr, true);
                        using (var prepared = ocr.GetSubtitleBitmap(0))
                        {
                            var rawInk = 0; var preparedInk = 0;
                            for (var y = 0; y < raw.Height; y++) for (var x = 0; x < raw.Width; x++) if (raw.GetPixel(x, y).A > 100) rawInk++;
                            for (var y = 0; y < prepared.Height; y++) for (var x = 0; x < prepared.Width; x++)
                            {
                                var color = prepared.GetPixel(x, y);
                                if (color.A > 100) { preparedInk++; Assert.Equal(Color.White.ToArgb(), color.ToArgb()); }
                            }
                            Assert.True(preparedInk > 0 && preparedInk < rawInk);
                        }
                    }
                }
            }
            finally { Configuration.Settings.General.Language = savedLanguage; Directory.Delete(folder, true); }
        }

        [Fact]
        public async Task VobSubCliIsolationAndOptOutReachRealNOcrRecognition()
        {
            var cli = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "seconv.exe");
            Assert.True(File.Exists(cli), "CLI fixture must contain the built seconv executable");
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vob-cli-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var file = Path.Combine(folder, "palette.sub");
                using (var source = new Bitmap(50, 50))
                {
                    using (var g = Graphics.FromImage(source))
                    { g.Clear(Color.Transparent); g.FillRectangle(Brushes.Black, 2, 2, 44, 44); g.FillRectangle(Brushes.Yellow, 12, 8, 8, 30); g.FillRectangle(Brushes.Yellow, 12, 30, 24, 8); }
                    using (var writer = new Nikse.SubtitleEdit.Core.VobSub.VobSubWriter(file, 720, 576, 20, 20, 32, Color.Yellow, Color.Black, true, Nikse.SubtitleEdit.Core.Common.DvdSubtitleLanguage.CompliantLanguages.First()))
                    { writer.WriteParagraph(new Paragraph("", 1000, 2000), source, ContentAlignment.BottomCenter); writer.WriteIdxFile(); }
                }
                var parser = new Nikse.SubtitleEdit.Core.VobSub.VobSubParser(true);
                parser.OpenSubIdx(file, Path.ChangeExtension(file, ".idx"));
                var pack = parser.MergeVobSubPacks().Single(); pack.Palette = parser.IdxPalette;
                var dbFile = Path.Combine(folder, "known.nocr"); var db = new NOcrDb(dbFile);
                using (var raw = pack.GetBitmap())
                using (var prepared = ClassicVobSubOcrPreparation.Prepare(raw, true))
                {
                    var item = ClassicNOcrBitmap.Split(new NikseBitmap(prepared), 8, false, false, 5, true).Single(i => i.NikseBitmap != null);
                    var glyph = new NOcrChar("R") { Width = item.NikseBitmap.Width, Height = item.NikseBitmap.Height, MarginTop = item.Top };
                    NOcrChar.GenerateLineSegments(100, true, glyph, item.NikseBitmap, NOcrLineAlgorithm.Random, new Random(42)); db.Add(glyph); db.Save();
                }
                foreach (var isolate in new[] { true, false })
                {
                    var output = Path.Combine(folder, isolate ? "isolated" : "raw");
                    var args = "\"" + file + "\" subrip --ocr-engine:nocr --ocr-db:\"" + dbFile + "\" --output-folder:\"" + output + "\"" + (isolate ? "" : " --no-vobsub-isolate-colors");
                    using (var process = Process.Start(new ProcessStartInfo(cli, args) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true }))
                    {
                        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                        var completed = await Task.Run(() => process.WaitForExit(30000));
                        if (!completed) { process.Kill(); throw new Exception("CLI OCR timed out"); }
                        await Task.WhenAll(stdout, stderr); Assert.Equal(0, process.ExitCode);
                    }
                    var text = Subtitle.Parse(Path.Combine(output, "palette.srt")).Paragraphs.Single().Text.Trim();
                    if (isolate) Assert.Equal("R", text); else Assert.NotEqual("R", text);
                }
            }
            finally { Directory.Delete(folder, true); }
        }

        [Fact]
        public void OcrThumbnailZoomLeavesSourceImageAndResets()
        {
            using (var bitmap = PaletteImage())
            using (var box = new ClassicZoomPictureBox { Image = bitmap, Size = new Size(300, 100) })
            {
                box.ChangeZoom(240); Assert.True(box.ZoomFactor > 1);
                using (var view = new Bitmap(300, 100)) box.DrawToBitmap(view, new Rectangle(0, 0, 300, 100));
                box.ResetZoom(); Assert.Equal(1.0, box.ZoomFactor);
                Assert.Equal(Color.Yellow.ToArgb(), bitmap.GetPixel(12, 12).ToArgb());
            }
        }
        [Fact]
        public void ImageViewerMovesBothDirectionsWithoutReplacingItsNativeWindow()
        {
            var calls = 0; var changed = -1;
            using (var viewer = new ExportPngXmlPreview(index =>
            {
                calls++; var bitmap = new Bitmap(100 + index, 40);
                using (var g = Graphics.FromImage(bitmap)) g.Clear(index == 0 ? Color.Red : Color.Blue);
                return bitmap;
            }, 0, 2, index => changed = index))
            {
                var handle = viewer.Handle;
                Assert.True(viewer.ClientSize.Width >= 360 && viewer.ClientSize.Height >= 200, "Small glyph previews retain usable navigation space: " + viewer.ClientSize + " minimum " + viewer.MinimumSize);
                Assert.False(viewer.MoveImage(-1)); Assert.True(viewer.MoveImage(1));
                Assert.Equal(1, changed); Assert.Equal(1, viewer.ImageIndex);
                Assert.Equal(handle, viewer.Handle); Assert.False(viewer.MoveImage(1));
                Assert.True(viewer.MoveImage(-1)); Assert.Equal(0, viewer.ImageIndex);
                Assert.Equal(handle, viewer.Handle); Assert.Equal(3, calls);
            }
        }
        [Fact]
        public void OrdinaryGuiExportUsesAssStylesAndNewOverrideRenderer()
        {
            var source = new Subtitle { Header = "[Script Info]\nPlayResX: 640\nPlayResY: 360\n\n[V4+ Styles]\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\nStyle: Default,Arial,30,&H0000FFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,0,8,10,10,12,1\n" };
            var cue = new Paragraph(@"{\u1\fsp4\frz12\pos(320,40)}ASS style", 1000, 2000) { Extra = "Default" };
            source.Paragraphs.Add(cue);
            using (var export = new ExportPngXml())
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(ExportPngXml).GetMethod("Initialize", flags).Invoke(export, new object[] { source, new Nikse.SubtitleEdit.Core.SubtitleFormats.AdvancedSubStationAlpha(), "BDNXML", "sample.ass", null, null });
                var guiParameter = typeof(ExportPngXml).GetMethod("MakeMakeBitmapParameter", flags).Invoke(export, new object[] { 0, 640, 360 });
                var parameterType = guiParameter.GetType();
                Assert.Same(source.Header, ((Subtitle)parameterType.GetProperty("SourceSubtitle").GetValue(guiParameter)).Header);
                ExportPngXml.DoWork(guiParameter);
                using (var rendered = (Bitmap)parameterType.GetProperty("Bitmap").GetValue(guiParameter))
                {
                    var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gui-ass-export-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(folder);
                    try
                    {
                        var xml = new System.Text.StringBuilder();
                        typeof(ExportPngXml).GetMethod("WriteBdnXmlParagraph", flags).Invoke(export, new object[] { 640, xml, 30, 360, 0, guiParameter, 0, folder });
                        var position = (Point)parameterType.GetProperty("OverridePosition").GetValue(guiParameter);
                        Assert.Contains("X=\"" + position.X + "\"", xml.ToString());
                        Assert.Contains("Y=\"" + position.Y + "\"", xml.ToString());
                        Assert.Equal(1, Directory.GetFiles(folder, "*.png").Length);
                    }
                    finally { Directory.Delete(folder, true); }
                }
            }
            var type = typeof(ExportPngXml).GetNestedType("MakeBitmapParameter", BindingFlags.NonPublic);
            var parameter = Activator.CreateInstance(type);
            Action<string, object> set = (name, value) => type.GetProperty(name).SetValue(parameter, value);
            set("SourceSubtitle", source); set("P", cue); set("ScreenWidth", 640); set("ScreenHeight", 360);
            set("SubtitleFontName", "Arial"); set("SubtitleFontSize", 20f); set("SubtitleColor", Color.Red);
            set("FramesPerSeconds", 24d); set("Alignment", ContentAlignment.BottomCenter);
            var method = typeof(ExportPngXml).GetMethod("GenerateImageFromTextWithStyle", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { type }, null);
            using (var bitmap = (Bitmap)method.Invoke(null, new[] { parameter }))
            {
                var yellow = 0;
                for (var y = 0; y < bitmap.Height; y++) for (var x = 0; x < bitmap.Width; x++)
                { var color = bitmap.GetPixel(x, y); if (color.A > 100 && color.R > 150 && color.G > 150 && color.B < 80) yellow++; }
                Assert.True(yellow > 20); // Style yellow beats red from the generic export settings.
                Assert.Equal(ContentAlignment.TopCenter, type.GetProperty("Alignment").GetValue(parameter));
                Assert.True(type.GetProperty("OverridePosition").GetValue(parameter) is Point);
                Assert.Equal(@"{\u1\fsp4\frz12\pos(320,40)}ASS style", cue.Text);
            }
            cue.Text = "First" + Environment.NewLine + "Second";
            var advances = new System.Collections.Generic.Dictionary<string, int> { { "Default", 35 } };
            set("LineHeight", advances);
            using (var closeLines = (Bitmap)method.Invoke(null, new[] { parameter }))
            {
                advances["Default"] = 80;
                using (var spacedLines = (Bitmap)method.Invoke(null, new[] { parameter }))
                    Assert.True(spacedLines.Height > closeLines.Height + 35, "explicit line advance: close=" + closeLines.Height + " spaced=" + spacedLines.Height);
            }
        }
    }
}




