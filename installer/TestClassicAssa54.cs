using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms.Assa;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicDraw;
using Nikse.SubtitleEdit.Logic.VideoPlayers;

internal static class TestClassicAssa54
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int _checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); _checks++; }
    private static object Get(object instance, string name) => instance.GetType().GetField(name, Fields).GetValue(instance);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Fields).SetValue(instance, value);
    private static object Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, Fields).Invoke(instance, args);
    private static DrawShape Rect(float x, float y, float width, float height, int group = 0)
    {
        var s = new DrawShape { GroupId = group };
        s.AddPoint(DrawCoordinateType.Move, x, y, Color.Red);
        s.AddPoint(DrawCoordinateType.Line, x + width, y, Color.Red);
        s.AddPoint(DrawCoordinateType.Line, x + width, y + height, Color.Red);
        s.AddPoint(DrawCoordinateType.Line, x, y + height, Color.Red);
        return s;
    }
    private static Subtitle Source()
    {
        var sub = new Subtitle { Header = AdvancedSubStationAlpha.DefaultHeader.Replace("PlayResX: 384", "PlayResX: 320").Replace("PlayResY: 288", "PlayResY: 180") };
        sub.Header = AdvancedSubStationAlpha.AddTagToHeader("PlayResX", "PlayResX: 320", "[Script Info]", sub.Header);
        sub.Header = AdvancedSubStationAlpha.AddTagToHeader("PlayResY", "PlayResY: 180", "[Script Info]", sub.Header);
        sub.Paragraphs.Add(new Paragraph("{\\an7\\pos(40,60)}Preview text", 0, 2000) { Extra = "Default" });
        return sub;
    }
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            if (!Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dictionaries"))) throw new Exception("Run only in an isolated fixture containing Dictionaries.");
            Configuration.Settings.General.FFmpegLocation = args[0];
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            Check(BackgroundBoxStyles.Keys.Length == 24 && BackgroundBoxStyles.Names.Length == 24, "Background catalogue");
            Check(ProgressBarStyles.Names.Length == 18, "Bar catalogue");
            var backgrounds = new HashSet<string>();
            for (var i = 0; i < 24; i++)
            {
                var drawing = BackgroundBoxStyles.GenerateBackgroundBox(i, 12, 30, 630, 40, 200);
                Check(drawing.StartsWith("{\\p1}m ") && drawing.EndsWith("{\\p0}") && !drawing.Contains(","), "Background ASS geometry " + i);
                backgrounds.Add(drawing);
            }
            Check(backgrounds.Count == 24, "All background styles differ");
            var bars = new HashSet<string>();
            for (var i = 0; i < 18; i++)
            {
                var drawing = ProgressBarStyles.Drawing(i, 1000, 40);
                Check(drawing.StartsWith("m ") && !drawing.Contains(","), "Bar ASS geometry " + i);
                bars.Add(drawing);
            }
            Check(bars.Count == 18, "All bar styles differ");
            Check(ProgressBarStyles.GetEndCapInset(3, 40).Right == 20, "Arrow bar text avoids tip");
            var info = new VideoInfo { Width = 320, Height = 180, TotalMilliseconds = 2000 };
            var sub = Source();
            using (var background = new AssSetBackground(sub, new[] { 0 }, "", info, 0))
                Check(((Nikse.SubtitleEdit.Controls.NikseComboBox)Get(background, "comboBoxBoxStyle")).Items.Count == 24, "Background GUI reaches every style");
            using (var bar = new AssaProgressBar(sub, "", info))
            {
                var combo = (Nikse.SubtitleEdit.Controls.NikseComboBox)Get(bar, "comboBoxProgressBarEdge");
                Check(combo.Items.Count == 18, "Bar GUI reaches every style");
                combo.SelectedIndex = 17;
                var drawing = (string)Call(bar, "GenerateProgressBar", info, 40, 200);
                Check(drawing == "{\\K200\\p1}" + ProgressBarStyles.Drawing(17, 320, 40) + "{\\p0}", "Selected star bar reaches actual ASS output");
            }
            using (var draw = new ClassicAssaDraw(sub.Paragraphs[0], 320, 180, sub))
            {
                var canvas = (Control)Get(draw, "_canvas");
                canvas.Dock = DockStyle.None;
                canvas.Size = new Size(320, 180);
                var a = Rect(30, 30, 30, 30, 7); var b = Rect(90, 30, 30, 30, 7); var c = Rect(200, 100, 25, 25);
                var shapes = new List<DrawShape> { a, b, c };
                Set(canvas, "Shapes", shapes); Set(canvas, "Active", null);
                var selected = (HashSet<DrawShape>)Get(canvas, "Selected");
                Call(canvas, "SelectGroup", a, true);
                Check(selected.SetEquals(new[] { a, b }), "Group selection includes all group shapes");
                Call(canvas, "OnKeyDown", new KeyEventArgs(Keys.Right));
                Check(a.Points[0].X == 31 && b.Points[0].X == 91 && c.Points[0].X == 200, "Arrow nudges selected group only");
                Call(canvas, "OnKeyDown", new KeyEventArgs(Keys.Shift | Keys.Down));
                Check(a.Points[0].Y == 40 && b.Points[0].Y == 40, "Shift arrow nudges by ten");
                selected.Clear(); Set(canvas, "Active", null);
                Call(canvas, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
                Call(canvas, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 70, 80, 0));
                Call(canvas, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 70, 80, 0));
                Check(selected.SetEquals(new[] { a, b }), "Rubber-band expands intersected shape to its group");
                selected.Clear(); selected.Add(c); Set(canvas, "Active", c); Set(canvas, "ActivePoint", c.Points[2]);
                Call(canvas, "OnKeyDown", new KeyEventArgs(Keys.Delete));
                Check(c.Points.Count == 3 && shapes.Contains(c), "Delete active point retains its shape");
                Set(canvas, "ActivePoint", null);
                Call(canvas, "OnKeyDown", new KeyEventArgs(Keys.Control | Keys.A));
                Check(selected.Count == 3, "Ctrl A selects visible shapes");
                Call(canvas, "OnKeyDown", new KeyEventArgs(Keys.Control | Keys.G));
                Check(shapes.Select(s => s.GroupId).Distinct().Count() == 1, "Ctrl G groups selection");
                Call(canvas, "OnKeyDown", new KeyEventArgs(Keys.Control | Keys.Shift | Keys.G));
                Check(shapes.All(s => s.GroupId == 0), "Ctrl Shift G ungroups selection");
            }
            using (var position = new SetPosition(Source(), new[] { 0 }, "", info, 0))
            {
                Set(position, "_loading", false); Set(position, "_playResScaleOn", true);
                Set(position, "_originalPlayResX", 640); Set(position, "_originalPlayResY", 360);
                Set(position, "_x", 20); Set(position, "_y", 30); Set(position, "_xScaled", 40); Set(position, "_yScaled", 60);
                Set(position, "_tempXScaled", 999); Set(position, "_tempYScaled", 999);
                Call(position, "ApplyCustomStyles_KeyDown", null, new KeyEventArgs(Keys.Right));
                Check((int)Get(position, "_xScaled") == 41, "Arrow uses committed coordinates, not last mouse hover");
                Call(position, "ApplyOverrideTags", Source());
                Check(position.UpdatedSubtitle.Paragraphs[0].Text.Contains("\\pos(41,60)"), "Keyboard coordinates reach applied ASS");
                var anchor = (ComboBox)Get(position, "_alignment"); anchor.SelectedIndex = 8;
                Call(position, "ApplyOverrideTags", Source());
                Check(position.UpdatedSubtitle.Paragraphs[0].Text.Contains("\\an9") && !position.UpdatedSubtitle.Paragraphs[0].Text.Contains("\\an7"), "Anchor choice replaces old anchor");
            }
            using (var attachments = new Attachments(""))
            {
                Check(attachments.AllowDrop && ((ListView)Get(attachments, "listViewAttachments")).AllowDrop, "Attachments accepts file drops");
                var png = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "attachment54.png");
                using (var image = new Bitmap(16, 16)) image.Save(png);
                Call(attachments, "AttachFiles", new object[] { new[] { png } });
                Check(((ListView)Get(attachments, "listViewAttachments")).Items.Count == 1, "Drop route uses shared attachment import");
                File.Delete(png);
            }
            Preview(Source()).GetAwaiter().GetResult();
            Console.WriteLine("PASS " + _checks + " ASS checks");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static async Task Preview(Subtitle subtitle)
    {
        var source = new Bitmap(320, 180);
        using (var g = Graphics.FromImage(source)) g.Clear(Color.Navy);
        var preview = new SetPositionPreview(subtitle, "", 0, 320, 180, source);
        var directory = (string)Get(preview, "_directory");
        using (var rendered = await preview.Render(subtitle, CancellationToken.None).ConfigureAwait(false))
        {
            Check(rendered.Size == new Size(320, 180), "Native libass preview size");
            Check(rendered.GetPixel(0, 0).B > 100 && rendered.GetPixel(0, 0).R < 8, "Paused frame is reused as background");
            var changed = false; for (var y = 45; y < 175 && !changed; y++) for (var x = 35; x < 310; x++) if (rendered.GetPixel(x, y).R > 100) { changed = true; break; }
            Check(changed, "Native libass renders positioned subtitle over paused frame");
        }
        using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel(); var stopped = false;
            try { using (await preview.Render(subtitle, cancel.Token).ConfigureAwait(false)) { } } catch (OperationCanceledException) { stopped = true; }
            Check(stopped, "Cancelled preview does not render");
        }
        preview.Dispose();
        for (var i = 0; i < 100 && Directory.Exists(directory); i++) await Task.Delay(20).ConfigureAwait(false);
        Check(!Directory.Exists(directory), "Preview cleans temporary files after disposal");
    }
}
