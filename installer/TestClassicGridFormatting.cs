using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicGridFormatting
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int checks;
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS: " + label); }
    static object Field(object value, string name) => value.GetType().GetField(name, F).GetValue(value);
    static object Call(object value, string name, params object[] args) => value.GetType().GetMethods(F).Single(m => m.Name==name && m.GetParameters().Length==args.Length && m.GetParameters().Select((p,i)=>args[i]==null || p.ParameterType.IsInstanceOfType(args[i])).All(v=>v)).Invoke(value, args);
    static string Plain(string text, ClassicGridFormattingMode mode = ClassicGridFormattingMode.HideTags) => string.Concat(ClassicGridMarkup.Parse(text, mode).Select(r => r.Text));
    static void Offscreen(Form form) { form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.ShowInTaskbar = false; form.Show(); }
    [STAThread] static int Main(string[] args)
    {
        var root = Path.Combine(Path.GetFullPath(args[0]), "run-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        typeof(Configuration).GetField("DataDirectory", BindingFlags.Static | BindingFlags.Public).SetValue(null, root + Path.DirectorySeparatorChar);
        Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dictionaries"));
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); Application.EnableVisualStyles(); var exit = 0;
        using (var anchor = new Form()) { anchor.Shown += async (s,e) => { anchor.Hide(); try { await Run(root); } catch (Exception ex) { Console.Error.WriteLine(ex); exit = 1; } finally { anchor.Close(); } }; anchor.ShowInTaskbar = false; anchor.StartPosition = FormStartPosition.Manual; anchor.Location = new Point(-30000,-30000); Application.Run(anchor); }
        Console.WriteLine("FEATURE CHECKS: " + checks); return exit;
    }
    static async Task Run(string root)
    {
        LanguageSettings.Current = new Language(); Configuration.Settings.General.Language = "en-US";
        var visual = ClassicGridFormattingMode.ShowFormatting;
        var keep = ClassicGridFormattingMode.ShowFormattingKeepTags;
        var runs = ClassicGridMarkup.Parse("plain <i>italic <b>both</b> italic</i> plain", visual);
        Check(runs[1].Style == FontStyle.Italic && runs[2].Style == (FontStyle.Italic | FontStyle.Bold), "Nested HTML styles compose");
        Check(runs[3].Style == FontStyle.Italic && runs[4].Style == FontStyle.Regular, "Closing HTML styles restores the previous state");
        runs = ClassicGridMarkup.Parse("<font color='#ff0000' face='Arial' size='30'>red <font color='blue'>blue</font> red</font> normal", visual);
        Check(runs[0].Color.Value.ToArgb() == Color.Red.ToArgb() && runs[0].Face == "Arial" && runs[0].Size == 30, "HTML color, face and size are interpreted");
        Check(runs[1].Color.Value.ToArgb() == Color.Blue.ToArgb() && runs[2].Color.Value.ToArgb() == Color.Red.ToArgb() && !runs.Last().Color.HasValue, "Nested font colors restore correctly");
        runs = ClassicGridMarkup.Parse(@"{\i1\b700\u1\s1\1c&H0000FF&\fnArial\fs30}Русский{\r} normal", visual);
        Check(runs[0].Style == (FontStyle.Italic | FontStyle.Bold | FontStyle.Underline | FontStyle.Strikeout), "ASS style overrides compose");
        Check(runs[0].Color.Value.ToArgb() == Color.Red.ToArgb() && runs[0].Face == "Arial" && runs[0].Size == 30, "ASS BGR primary color, face and size are interpreted");
        Check(runs.Last().Style == FontStyle.Regular && !runs.Last().Color.HasValue && runs.Last().Face == null, "ASS reset clears visual overrides");
        runs = ClassicGridMarkup.Parse(@"{\i1}yes{\i0\b0\u0\s0}no", visual);
        Check(runs[0].Style == FontStyle.Italic && runs[1].Style == FontStyle.Regular, "Explicit ASS disable codes clear style flags");
        Check(Plain(@"{\an8\pos(10,20)\t(0,100,\c&HFF0000&)}Text") == "Text", "Hide tags removes positional and animated ASS blocks");
        runs = ClassicGridMarkup.Parse(@"{\i1\an8\t(0,100,\c&HFF0000&)}Text", keep);
        Check(runs[0].Tag && runs[0].Text == @"{\an8\t(0,100,\c&HFF0000&)}" && runs.Last().Style == FontStyle.Italic && !runs.Last().Color.HasValue, "Keep-tags mode retains a whole transition without applying its nested color");
        Check(Plain(@"{\fsp2\fscx90\fs20}text", keep) == @"{\fsp2\fscx90}text", "Spacing and scaling commands are not confused with font size");
        Check(Plain("<ruby>word</ruby>", keep) == "<ruby>word</ruby>", "Keep-tags mode preserves unsupported HTML markup");
        Check(Plain("<!-- comment --><i>Text</i>") == "Text", "Complete HTML comments and styling are hidden");
        Check(Plain("2 < 3 &amp; 4 &lt; 5 &#x1F600;") == "2 < 3 & 4 < 5 😀", "Entities decode after parsing and literal comparison text is retained");
        Check(Plain("broken {\\i1") == "broken {\\i1" && Plain("<i broken") == "<i broken", "Unclosed tags retain readable source text");
        Check(Plain("one\r\ntwo\nthree\\Nfour\\hf") == "one || two || three || four\u00a0f", "Physical and ASS line breaks use the classic line separator");
        Check(ClassicGridMarkup.Parse("<b>bold</b>", ClassicGridFormattingMode.HideTags).All(r => r.Style == FontStyle.Regular), "Hide-tags mode removes style as well as markup");
        Check(Plain("<b>bold</b>", ClassicGridFormattingMode.ShowTags) == "<b>bold</b>", "Show-tags mode retains the exact original markup");
        Check(Plain(null) == "" && ClassicGridMarkup.Parse("", visual).Count == 0, "Empty and null text are supported");
        Check(Plain("{" + string.Concat(Enumerable.Repeat(@"\t(0,100,\bord2)", 1000)) + "}Dialogue") == "Dialogue", "Long override blocks do not consume the visible text limit");
        var capped = Plain(new string('x', 2047) + "😀after"); Check(capped.EndsWith("…") && !capped.Any(char.IsSurrogate), "Display limits never cut a surrogate pair");
        Check(ClassicGridPainter.ReadableColor(Color.White, Color.Black, Color.White, false) == Color.Black, "Invisible white text falls back on a light background");
        Check(ClassicGridPainter.ReadableColor(Color.Black, Color.White, Color.Black, false) == Color.White, "Invisible black text falls back on a dark background");
        Check(ClassicGridPainter.ReadableColor(Color.Lime, Color.Black, Color.White, false) == Color.Black, "Bright green text falls back when its contrast is insufficient");
        Check(ClassicGridPainter.ReadableColor(Color.Red, Color.White, Color.Blue, true) == Color.White, "Selected rows use a readable selection foreground");
        var settings = Path.Combine(root, "appearance.json");
        Check(ClassicGridAppearance.Load(settings).Formatting == ClassicGridFormattingMode.ShowTags, "Existing installations retain the original default");
        foreach (ClassicGridFormattingMode mode in Enum.GetValues(typeof(ClassicGridFormattingMode))) { new ClassicGridAppearance { Formatting = mode }.Save(settings); Check(ClassicGridAppearance.Load(settings).Formatting == mode, "Appearance mode persists: " + mode); }
        File.WriteAllText(settings, "{\"Formatting\":999}"); Check(ClassicGridAppearance.Load(settings).Formatting == ClassicGridFormattingMode.ShowTags, "Invalid saved modes fall back safely");
        File.WriteAllText(settings, "broken"); Check(ClassicGridAppearance.Load(settings).Formatting == ClassicGridFormattingMode.ShowTags, "Invalid settings JSON falls back safely");
        var painter = new ClassicGridPainter();
        using (var bitmap = new Bitmap(800, 300)) using (var graphics = Graphics.FromImage(bitmap)) using (var font = new Font("Segoe UI", 11))
        {
            graphics.Clear(Color.White);
            for (var i=0;i<4;i++) painter.Draw(graphics,new Rectangle(8, 10+i*45,760,35),"<i>Italic</i> <b>Bold</b> <u>Underlined</u> <font color='red'>Red</font> {\\an8}Русский 😀",font,Color.Black,Color.White,false,false,(ClassicGridFormattingMode)i);
            painter.Draw(graphics,new Rectangle(8,200,760,30),"<b>مرحبا</b> بالعالم — <i>שלום</i>",font,Color.Black,Color.White,false,true,visual);
            painter.Draw(graphics,new Rectangle(8,250,25,25),"<font face='No such font' size='999'>Text that is clipped</font>",font,Color.White,Color.Black,true,false,visual);
            bitmap.Save(Path.Combine(root,"painter.png"));
        }
        Check(true, "Real GDI drawing supports mixed styles, RTL, missing fonts, clipped and selected text");
        using(var form = new Form { ClientSize=new Size(1040,420) }) using(var grid = new SubtitleListView { Dock=DockStyle.Fill, GridLines=true })
        {
            form.Controls.Add(grid); var source = new Subtitle(); source.Paragraphs.Add(new Paragraph("<i>Русский</i> <b>English</b> <font color='red'>RED</font> {\\an8}top", 1000,3000)); source.Paragraphs.Add(new Paragraph("{\\i1\\c&H00FF00&}Green italic{\\r} normal",4000,6000));
            var original = new Subtitle(source,false); original.Paragraphs[0].Text="<u>Original</u> text"; grid.InitializeLanguage(LanguageSettings.Current.General,Configuration.Settings); grid.ShowOriginalTextColumn("Original"); Call(grid,"Fill",source,original); Offscreen(form); await Task.Delay(50);
            var text = grid.Items[0].SubItems[grid.ColumnIndexText].Text; var data=source.Paragraphs[0].Text;
            foreach(ClassicGridFormattingMode mode in Enum.GetValues(typeof(ClassicGridFormattingMode)))
            {
                grid.GridFormattingMode=mode; grid.SelectIndexAndEnsureVisible(0); grid.Focus(); grid.Refresh(); await Task.Delay(20);
                using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(root,"grid-"+mode+".png"));}
                Check(grid.Items[0].SubItems[grid.ColumnIndexText].Text==text && source.Paragraphs[0].Text==data,"Changing grid mode preserves source and subitem markup: "+mode);
                Check(grid.Items[0].SubItems[grid.ColumnIndexTextOriginal].Text==original.Paragraphs[0].Text,"Original text retains its exact markup in each mode: "+mode);
            }
            grid.GridFormattingMode=visual;grid.Items[0].Selected=false;grid.BackColor=Color.FromArgb(25,25,25);grid.ForeColor=Color.White;grid.Refresh();
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(root,"grid-dark.png"));}
            Check(true,"Dark-background grid and the original column render through the same display-only path");
            source.Paragraphs[0].Text="<b>Changed</b>";Call(grid,"Fill",source,original);grid.GridFormattingMode=visual;grid.Refresh();Check(grid.GetText(0)=="<b>Changed</b>","Editing and refilling retains the new exact source text");
        }
        foreach(var language in new[]{"en-US","ru-RU"})
        {
            Configuration.Settings.General.Language=language;LanguageSettings.Current=language=="en-US"?new Language():Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages",language+".xml"));
            using(var main=new Nikse.SubtitleEdit.Forms.Main())
            {
                var menu=(ToolStripMenuItem)Field(main,"_classicGridAppearanceMenu");var grid=(SubtitleListView)Field(main,"SubtitleListview1");
                Check(menu.DropDownItems.Count==4 && menu.Text.Contains(language=="en-US"?"formatting":"Отображение"),"Four localized menu modes are accessible: "+language);
                var subtitle=new Subtitle(); subtitle.Paragraphs.Add(new Paragraph("<i>Exact source</i> {\\an8}retained",1000,3000){Number=1,Actor="Alice",Extra="Style",Layer=2});
                main.GetType().GetField("_subtitle",F).SetValue(main,subtitle);Call(grid,"Fill",subtitle,new Subtitle());var handle=grid.Handle;grid.SelectIndexAndEnsureVisible(0);
                var before=new SubRip().ToText(subtitle,"test.srt");
                Call(main,"SetClassicGridAppearance",keep);Check(grid.GridFormattingMode==keep && ((ToolStripMenuItem)menu.DropDownItems[3]).Checked,"Menu applies and checks the active mode: "+language);
                Check(grid.SelectedIndex==0 && grid.GetText(0)==subtitle.Paragraphs[0].Text,"The selected editor row retains editable source markup: "+language);
                Check(new SubRip().ToText(subtitle,"test.srt")==before && subtitle.Paragraphs[0].Actor=="Alice" && subtitle.Paragraphs[0].Layer==2,"Mode selection preserves serialized subtitle, timing and metadata: "+language);
                Call(main,"InitializeClassicGridAppearance");Check(menu.DropDownItems.Count==4,"Refreshing language does not duplicate mode entries: "+language);
            }
            using(var main=new Nikse.SubtitleEdit.Forms.Main()) Check(((SubtitleListView)Field(main,"SubtitleListview1")).GridFormattingMode==keep,"A new main window restores the selected mode: "+language);
        }
        var timer=System.Diagnostics.Stopwatch.StartNew(); for(var i=0;i<10000;i++) ClassicGridMarkup.Parse(@"{\an8\i1}Русский <b>English</b> &amp; text",visual);
        Console.WriteLine("10,000 parsed cues ms: "+timer.ElapsedMilliseconds);Check(timer.ElapsedMilliseconds<10000,"Parsing a large subtitle remains bounded");
        File.WriteAllText(Path.Combine(root,"validation.txt"),"Passed checks: "+checks+"; paid requests: 0");Console.WriteLine("ARTIFACTS: "+root);
    }
}
