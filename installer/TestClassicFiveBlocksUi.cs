using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Forms.Assa;
using Nikse.SubtitleEdit.Logic.ClassicDraw;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicFiveBlocksUi
{
    const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;static int checks;
    static void Check(bool value,string text){if(!value)throw new Exception(text);checks++;Console.WriteLine("PASS: "+text);}
    static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethods(F).Single(m=>m.Name==n && m.GetParameters().Length==a.Length && m.GetParameters().Select((p,i)=>a[i]==null || p.ParameterType.IsInstanceOfType(a[i])).All(v=>v)).Invoke(o,a);
    static void Show(Form form){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();}
    static System.Collections.Generic.IEnumerable<Control> Controls(Control control)
    {if(control is DataGridView || control is PropertyGrid || control is ListView || control is ComboBox || control is NumericUpDown)return Enumerable.Empty<Control>();return control.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(Controls(c)));}
    static async Task Run(string root)
    {
        var subtitle=new Subtitle{Header=AdvancedSubStationAlpha.DefaultHeader};subtitle.Paragraphs.Add(new Paragraph("Hello Привет",1000,3000){Extra="Default",Actor="Alice",Layer=2});subtitle.Renumber();
        foreach(var language in new[]{"ru-RU","en-US"})
        {
            Configuration.Settings.General.Language=language;
            var forms=new Form[]{new ClassicSpreadsheetImport(),new ClassicTableExportForm(subtitle),new ClassicCaptionImportForm(null),new ClassicDvdImport(null),new ClassicImageExportForm(subtitle),new ClassicVideoToolsForm(null),new ClassicAssaDraw(subtitle.Paragraphs[0],1280,720),new ClassicAdvancedEffects(subtitle,null,null),new ClassicAssaFilter(subtitle,new int[0]),new ClassicFontPicker("Arial"),new ClassicFontCollector(subtitle)};
            foreach(var form in forms)using(form)
            {
                Show(form);await Task.Delay(100);form.Size=form.MinimumSize;form.PerformLayout();await Task.Delay(50);
                foreach(var control in Controls(form).Where(c=>c.Visible && (c is Button || c is ComboBox || c is NumericUpDown || c is TextBox || c is CheckBox)))
                {
                    // A scrolled viewport may intentionally clip its contents; fixed action panels may not.
                    if(control.Parent is ScrollableControl sc && sc.AutoScroll)continue;
                    var bounds=control.Bounds;var parent=control.Parent.ClientRectangle;
                    Check(bounds.Left>=-1 && bounds.Top>=-1 && bounds.Right<=parent.Right+2 && bounds.Bottom<=parent.Bottom+2,"Visible control fits: "+form.GetType().Name+" / "+control.Text+" / "+language+" "+bounds+" parent "+parent);
                }
                using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(root,form.GetType().Name+"-"+language+".png"));}
                form.DialogResult=DialogResult.Cancel;form.Close();
            }
        }
        using(var empty=new ClassicAssaDraw(new Paragraph("Plain text",1000,3000),1280,720))Check(!((Button)Field(empty,"_apply")).Enabled,"An empty drawing cannot delete the selected subtitle through Apply");
        var shape=new DrawShape{IsTextBox=true,Text="Long text wraps inside this rectangle",FontSize=100,FontBold=true,ShrinkToFit=true,TextColor=Color.Red,TextFrame=new DrawTextFrame(200,200,200,80,15)};
        shape.AddPoint(DrawCoordinateType.Move,100,160,Color.Red);shape.AddPoint(DrawCoordinateType.Line,300,160,Color.Red);shape.AddPoint(DrawCoordinateType.Line,300,240,Color.Red);shape.AddPoint(DrawCoordinateType.Line,100,240,Color.Red);
        var text=AssaDrawTextLine.Format(shape);Check(text.Contains("\\frz-15") && text.Contains("\\b1") && text.Contains("\\c&H0000FF"),"Draw text retains rotation, bold and color");
        using(var draw=new ClassicAssaDraw(new Paragraph(text,1000,3000),1280,720)){Check(draw.BuildResult().Count==1 && draw.BuildResult()[0].Text.Contains("Long"),"Draw text can be reopened without losing its layout");}
        using(var main=new Nikse.SubtitleEdit.Forms.Main())
        {
            Show(main);await Task.Delay(180);Call(main,"SetCurrentFormat",new AdvancedSubStationAlpha().FriendlyName);Set(main,"_subtitle",subtitle);Set(main,"_undoIndex",-1);Call(Field(main,"SubtitleListview1"),"Fill",subtitle,null);
            Check(Field(main,"_classicSpreadsheet")!=null && Field(main,"_classicImageExport")!=null && Field(main,"_classicVideoTools")!=null,"Main window registers every new workflow menu");
            var result=new System.Collections.Generic.List<Paragraph>{new Paragraph("{\\p1}m 0 0 l 10 0 10 10{\\p0}",1000,3000){Actor="Alice",Layer=2,Extra="ClassicDraw"}};
            var header=(string)Call(main,"CreateClassicDrawHeader");Call(main,"ApplyClassicAssEvents",new[]{0},result,"Draw test",header);Check(subtitle.Paragraphs[0].Actor=="Alice" && subtitle.Header.Contains("ClassicDrawText"),"Draw applies metadata and neutral styles in one editor transaction");
            var undo=main.GetType().GetMethods(F).First(m=>m.Name=="ToolStripMenuItemUndoClick");undo.Invoke(main,new object[]{null,EventArgs.Empty});Check(((Subtitle)Field(main,"_subtitle")).Paragraphs[0].Text=="Hello Привет","Editor Undo restores source after drawing");
            Call(main,"ResetSubtitle");main.Close();
        }
    }
    [STAThread]static int Main(string[] args)
    {
        var root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);typeof(Configuration).GetField("DataDirectory").SetValue(null,root+Path.DirectorySeparatorChar);typeof(Configuration).GetField("SettingsFileName").SetValue(null,Path.Combine(root,"Settings.xml"));Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);var exit=0;
        using(var anchor=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)}){anchor.Shown+=async(s,e)=>{try{await Run(root);}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{anchor.Close();}};Application.Run(anchor);}Console.WriteLine("UI CHECKS: "+checks);return exit;
    }
}
