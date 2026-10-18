using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using CompareForm=Nikse.SubtitleEdit.Forms.Compare;
using MainForm=Nikse.SubtitleEdit.Forms.Main;
class TestClassicCompareComplete
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Field(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Flags).Invoke(o,a);
 static void Check(bool b,string m){if(!b)throw new Exception(m);Console.WriteLine("PASS: "+m);}
 static string Json(object o)=>JsonConvert.SerializeObject(o);
 static Subtitle Sample(){var s=new Subtitle{Header="[Script Info]"};s.Paragraphs.Add(new Paragraph("First",1000,2000){Number=1,Actor="A",Extra="StyleA"});s.Paragraphs.Add(new Paragraph("Second",3000,4000){Number=2,Actor="B",Extra="StyleB"});s.Paragraphs.Add(new Paragraph("Third",5000,6000){Number=3});return s;}
 static void Select(CompareForm f,int row,bool right=false){var list=(SubtitleListView)Field(f,right?"subtitleListView2":"subtitleListView1");foreach(ListViewItem i in list.Items)i.Selected=false;list.Items[row].Selected=true;Call(f,"SelectClassicPair",list);}
 static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();}
 [STAThread]static int Main(string[] args)
 {
  try{
   var root=Path.Combine(Path.GetFullPath(args[0]),"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));Console.WriteLine("TEST DATA: "+root);
   typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);
   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();int result=0;
   using(var anchor=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),Size=new Size(1,1)}){anchor.Shown+=async(s,e)=>{try{await Unit(root);}catch(Exception ex){Console.Error.WriteLine(ex);result=1;}finally{anchor.Close();}};Application.Run(anchor);}return result;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
 static async Task Unit(string root)
 {
  Configuration.Settings.Compare.ShowOnlyDifferences=false;Configuration.Settings.Compare.OnlyLookForDifferenceInText=false;
  foreach(var language in new[]{"ru-RU","en-US"})
  {
   Configuration.Settings.General.Language=language;LanguageSettings.Current=language=="en-US"?new Language():Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages",language+".xml"));
   var source=Sample();var reference=new Subtitle(source,false);foreach(var p in reference.Paragraphs){p.StartTime.TotalMilliseconds+=700;p.EndTime.TotalMilliseconds+=700;}var original=Json(source);var originalRef=Json(reference);
   using(var f=new CompareForm())
   {
    f.Initialize(source,"current.ass",reference,"reference.ass");Show(f);await Task.Delay(30);Select(f,1);
    Check(!((Label)Field(f,"labelStatus")).Text.Contains(LanguageSettings.Current.CompareSubtitles.SubtitlesNotAlike) && (int)Field(f,"_classicDifferenceCount")==3 && (int)Field(f,"_classicTextCount")==0,"Timing-only differences get accurate counts instead of a no-similarity warning: "+language);
    var view=(ComboBox)Field(f,"_classicView");view.SelectedIndex=2;Check(((SubtitleListView)Field(f,"subtitleListView1")).Items.Count==0 && view.Items[2].ToString().Contains("(0)"),"Text-only view excludes timing changes and displays its count");view.SelectedIndex=0;Select(f,1);
    Call(f,"ProcessCmdKey",Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),Keys.F2);var text=(TextBox)Field(f,"_classicInlineText");var start=(NikseTimeUpDown)Field(f,"_classicInlineStart");var end=(NikseTimeUpDown)Field(f,"_classicInlineEnd");
    Check(((RichTextBox)Field(f,"richTextBox2")).Text=="Second","Reference preview retains the selected text");
    Check(((Panel)Field(f,"_classicInlinePanel")).Visible && text.Text=="Second" && !((Button)Field(f,"_classicApply")).Enabled,"F2 opens an inline working draft and defers global Apply");
    text.Text="Edited second"+Environment.NewLine+"Another line";start.TimeCode=new TimeCode(3200);end.TimeCode=new TimeCode(4100);
    Check(Json(Field(f,"_subtitle1"))==original && Json(source)==original,"Typing a draft leaves working subtitle and main input unchanged");
    Call(f,"ProcessCmdKey",Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),Keys.Control|Keys.Enter);
    var work=(Subtitle)Field(f,"_subtitle1");Check(work.Paragraphs[1].Text.Contains("Another line") && work.Paragraphs[1].StartTime.TotalMilliseconds==3200 && work.Paragraphs[1].EndTime.TotalMilliseconds==4100,"Ctrl+Enter commits multiline text and timing to the correct cue");
    Check(work.Paragraphs[1].Actor=="B" && work.Paragraphs[1].Extra=="StyleB" && work.Paragraphs[1].Id==source.Paragraphs[1].Id && Json(reference)==originalRef,"Inline commit preserves actor, style, identity and read-only reference");
    var list=(SubtitleListView)Field(f,"subtitleListView1");var editedColor=(Color)f.GetType().GetProperty("ClassicEditedColor",Flags).GetValue(f);
    Check(list.Items[1].SubItems[list.ColumnIndexText].BackColor==editedColor && ((System.Collections.IEnumerable)Field(Field(f,"_classicOverview"),"Marks")).Cast<Color>().Contains(editedColor),"Edited cue is violet in both list and whole-file overview");
    var html=(string)Call(f,"BuildClassicComparisonHtml");Check(System.Net.WebUtility.HtmlDecode(html).Replace((char)160, ' ').Contains("Edited second") && System.Net.WebUtility.HtmlDecode(html).Replace((char)160, ' ').Contains("Another line") && html.Contains("<br>") && html.Contains("<td"),"HTML export retains aligned rows and multiline edited text");File.WriteAllText(Path.Combine(root,"compare-"+language+".html"),html);
    Call(f,"UndoClassicMerge");Check(Json(Field(f,"_subtitle1"))==original,"Inline text and timing undo together");
    Select(f,1);Call(f,"BeginClassicInlineEdit");text.Text="Discard this";Call(f,"ProcessCmdKey",Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),Keys.Escape);Check(!((Panel)Field(f,"_classicInlinePanel")).Visible && Json(Field(f,"_subtitle1"))==original && !f.IsDisposed,"Escape discards only the inline draft and keeps Compare open");
    Select(f,1);Call(f,"BeginClassicInlineEdit");start.TimeCode=new TimeCode(5000);end.TimeCode=new TimeCode(4000);Call(f,"SaveClassicInlineEdit");Check(((Panel)Field(f,"_classicInlinePanel")).Visible && Json(Field(f,"_subtitle1"))==original,"Reversed timing is rejected without changing the working file");Call(f,"CancelClassicInlineEdit");
    Select(f,1);Call(f,"BeginClassicInlineEdit");Call(f,"TakeClassicInlineReference","text");Check(start.TimeCode.TotalMilliseconds==3000,"Taking reference text into the draft leaves its timing alone");Call(f,"TakeClassicInlineReference","timing");Check(start.TimeCode.TotalMilliseconds==3700 && end.TimeCode.TotalMilliseconds==4700,"Taking reference timing into the draft leaves text independent");
    f.Size=f.MinimumSize;await Task.Delay(30);var panel=(Panel)Field(f,"_classicInlinePanel");Check(panel.Bottom<((Button)Field(f,"buttonOK")).Top && panel.Top>=list.Top && list.Bottom<=panel.Top,"Inline panel stays clear of lists and footer at minimum size");
    var status=(Label)Field(f,"labelStatus");foreach(var name in new[]{"checkBoxIgnoreLineBreaks","checkBoxIgnoreWhitespace","checkBoxIgnoreFormatting"}){var control=(Control)Field(f,name);Check(control.Bottom<status.Top && control.Right<=f.ClientSize.Width,"Footer option remains fully visible and clear of status: "+name);}
    using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,f.Width, f.Height));bmp.Save(Path.Combine(root,"compare-complete-"+language+".png"));}
    Call(f,"CancelClassicInlineEdit");Call(f,"JumpClassicOverview",.99);Check(list.SelectedItems[0].Index==2,"Overview click navigates to a whole-file position");
    Call(f,"DisableClassicMerge");Select(f,1);Call(f,"BeginClassicInlineEdit");Check(!panel.Visible && !((Button)Field(f,"_classicEdit")).Enabled,"An externally loaded left file cannot be inline-edited");
   }
  }
  var numbers=Sample();var other=new Subtitle(numbers,false);other.Paragraphs[1].Number=20;
  using(var f=new CompareForm()){f.Initialize(numbers,"current",other,"reference");Show(f);var ignore=(CheckBox)Field(f,"_classicIgnoreNumbers");ignore.Checked=false;Check((int)Field(f,"_classicDifferenceCount")==1,"Number-only changes are counted");ignore.Checked=true;Check((int)Field(f,"_classicDifferenceCount")==0,"Ignore numbering suppresses number-only differences");}
  var formatting=Sample();formatting.Paragraphs[0].Text="{\\an8}<i>Hello world</i>";formatting.Paragraphs[1].Text="Hello world";formatting.Paragraphs[2].Text="Hello"+Environment.NewLine+"world";var formatted=new Subtitle(formatting,false);formatted.Paragraphs[0].Text="<b>Hello world</b>";formatted.Paragraphs[1].Text="Hello  world";formatted.Paragraphs[2].Text="Hello world";
  using(var f=new CompareForm())
  {
   f.Initialize(formatting,"current",formatted,"reference");Show(f);((CheckBox)Field(f,"checkBoxIgnoreFormatting")).Checked=false;((CheckBox)Field(f,"checkBoxIgnoreWhitespace")).Checked=false;((CheckBox)Field(f,"checkBoxIgnoreLineBreaks")).Checked=false;
   Check((int)Field(f,"_classicTextCount")==3,"Text view counts formatting, whitespace and line-break changes before ignoring them");((CheckBox)Field(f,"checkBoxIgnoreFormatting")).Checked=true;Check((int)Field(f,"_classicTextCount")==2,"Ignore formatting handles both ASS and HTML tags");Select(f,0);Check(!((RichTextBox)Field(f,"richTextBox1")).Text.Contains("an8"),"Preview removes ignored ASS formatting rather than highlighting it as text");((CheckBox)Field(f,"checkBoxIgnoreWhitespace")).Checked=true;Check((int)Field(f,"_classicTextCount")==0,"Ignore whitespace excludes spaces and line breaks consistently");
  }
  using(var f=new CompareForm()){f.Initialize(new Subtitle(),"","Current");Show(f);Check((int)Field(f,"_classicAllCount")==0 && !((Button)Field(f,"_classicEdit")).Enabled,"An empty comparison reports zero rows and disables editing");}
  var saved=Path.Combine(root,"saved.srt");File.WriteAllText(saved,"1\n00:00:01,000 --> 00:00:02,000\nSaved disk text\n");var current=new Subtitle();current.Paragraphs.Add(new Paragraph("Unsaved editor text",1000,2000));
  using(var f=new CompareForm())
  {
   f.Initialize(current,saved,"Current");Show(f);Check(((Button)Field(f,"_classicLoadSaved")).Enabled,"Saved-file action is available when the current subtitle has a disk file");Call(f,"LoadClassicSavedReference");Check(((Subtitle)Field(f,"_subtitle2")).Paragraphs[0].Text=="Saved disk text" && ((Subtitle)Field(f,"_subtitle1")).Paragraphs[0].Text=="Unsaved editor text","Load saved file compares disk content without replacing unsaved current content");
   Select(f,0);Call(f,"BeginClassicInlineEdit");((TextBox)Field(f,"_classicInlineText")).Text="Applied inline result";Call(f,"SaveClassicInlineEdit");var applied=false;f.ApplyComparedSubtitle=(baseline,result)=>{applied=result.Paragraphs[0].Text=="Applied inline result" && baseline.Paragraphs[0].Text=="Unsaved editor text";return true;};Call(f,"ApplyClassicMerge");Check(applied && current.Paragraphs[0].Text=="Unsaved editor text","Apply passes the committed working result and untouched baseline to the existing main guard");
  }
 }
}
