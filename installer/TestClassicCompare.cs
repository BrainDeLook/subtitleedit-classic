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
class TestClassicCompare
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
  foreach(var language in new[]{"ru-RU","en-US"})
  {
   Configuration.Settings.General.Language=language;LanguageSettings.Current=language=="en-US"?new Language():Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages",language+".xml"));
   var source=Sample();var reference=Sample();reference.Paragraphs[1].Text="Reference second";reference.Paragraphs[1].StartTime.TotalMilliseconds+=100;reference.Paragraphs[1].EndTime.TotalMilliseconds-=40;reference.Paragraphs[1].Actor="Reference actor";
   var original=Json(source);var refOriginal=Json(reference);
   using(var f=new CompareForm())
   {
    f.Initialize(source,"current.ass",reference,"reference.ass");f.ApplyComparedSubtitle=(a,b)=>true;Show(f);await Task.Delay(50);Select(f,0);Call(f,"ProcessCmdKey",Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),Keys.F8);Check(((SubtitleListView)Field(f,"subtitleListView1")).SelectedItems[0].Index==1,"F8 navigates to the actual next difference");Select(f,1);
    Check(((Label)Field(f,"_classicDelta")).Text.Contains("+100 ms") && ((Label)Field(f,"_classicDelta")).Text.Contains("-40 ms"),"Selected row shows signed start and end deltas: "+language);
    Call(f,"TakeClassicReference","text");var work=(Subtitle)Field(f,"_subtitle1");Check(work.Paragraphs[1].Text=="Reference second" && work.Paragraphs[1].StartTime.TotalMilliseconds==3000 && work.Paragraphs[1].Actor=="B" && work.Header==source.Header,"Text-only merge preserves timing, actor, style and header");
    Check(Json(source)==original && Json(reference)==refOriginal,"Working edits never mutate editor or reference");
    Call(f,"ProcessCmdKey",Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),Keys.Control|Keys.Z);Check(Json(Field(f,"_subtitle1"))==original,"Ctrl+Z restores the complete working subtitle");
    Select(f,1,true);Call(f,"TakeClassicReference","timing");work=(Subtitle)Field(f,"_subtitle1");Check(work.Paragraphs[1].Text=="Second" && work.Paragraphs[1].EndTime.TotalMilliseconds==3960,"Timing-only merge follows right-pane selection and preserves text");
    Call(f,"UndoClassicMerge");((CheckBox)Field(f,"checkBoxShowOnlyDifferences")).Checked=true;Select(f,0);Call(f,"ProcessCmdKey",Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),Keys.Alt|Keys.Left);work=(Subtitle)Field(f,"_subtitle1");Check(work.Paragraphs[0].Text=="First" && work.Paragraphs[1].Text=="Reference second" && work.Paragraphs[2].Text=="Third","Filtered display row maps to the correct actual cue");
    Call(f,"UndoClassicMerge");((CheckBox)Field(f,"checkBoxShowOnlyDifferences")).Checked=false;
    var tolerance=(NumericUpDown)Field(f,"_classicTolerance");tolerance.Value=100;Check((bool)Call(f,"IsTimeEqual",new TimeCode(1000),new TimeCode(1100)),"Tolerance includes its exact boundary");Check(!(bool)Call(f,"IsTimeEqual",new TimeCode(1000),new TimeCode(1100.01)),"Tolerance rejects values beyond its boundary");
    tolerance.Value=0;Check(!(bool)Call(f,"IsTimeEqual",new TimeCode(1000),new TimeCode(1001)),"Zero tolerance retains exact-millisecond differences");
    f.Size=f.MinimumSize;await Task.Delay(30);var apply=(Button)Field(f,"_classicApply");Check(apply.RectangleToScreen(apply.ClientRectangle).Right<=f.RectangleToScreen(f.ClientRectangle).Right && apply.Bottom<=apply.Parent.ClientSize.Height,"Merge toolbar fits at minimum window size");
    using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,f.Width,f.Height));bmp.Save(Path.Combine(root,"compare-"+language+".png"));}
    Call(f,"DisableClassicMerge");Check(!((Button)Field(f,"_classicTakeBoth")).Enabled && !apply.Enabled,"Replacing left file makes merging read-only");
   }
   Check(Json(source)==original,"Closing without Apply discards local edits");
  }
  LanguageSettings.Current=new Language();Configuration.Settings.General.Language="en-US";
  var left=Sample();var right=Sample();right.Paragraphs.Insert(1,new Paragraph("Inserted",2300,2700){Number=9,Actor="New"});
  using(var f=new CompareForm())
  {
   f.Initialize(left,"current",right,"reference");Show(f);((CheckBox)Field(f,"checkBoxShowOnlyDifferences")).Checked=true;
   var list=(SubtitleListView)Field(f,"subtitleListView1");var missing=Enumerable.Range(0,list.Items.Count).First(i=>(int)Field(list.Items[i].Tag,"Left")==-1);Select(f,missing,true);Call(f,"TakeClassicReference","both");var work=(Subtitle)Field(f,"_subtitle1");Check(work.Paragraphs.Count==4 && work.Paragraphs[1].Text=="Inserted" && work.Paragraphs[2].Text=="Second" && work.Paragraphs[1].Actor=="New","Reference-only insertion uses full alignment even when following unchanged cues are hidden");
   Call(f,"UndoClassicMerge");Check(Json(Field(f,"_subtitle1"))==Json(left),"Undo removes an inserted cue and restores numbering");
  }
  using(var f=new CompareForm())
  {
   var s=Sample();var r=new Subtitle(s,false);r.Paragraphs[1].StartTime.TotalMilliseconds+=100;r.Paragraphs[1].EndTime.TotalMilliseconds-=100;
   f.Initialize(s,"current",r,"reference");Show(f);((CheckBox)Field(f,"checkBoxOnlyListDifferencesInText")).Checked=false;
   ((NumericUpDown)Field(f,"_classicTolerance")).Value=100;Check(((System.Collections.Generic.List<int>)Field(f,"_differences")).Count==0,"Time tolerance actually removes within-tolerance rows from the difference count");
   ((NumericUpDown)Field(f,"_classicTolerance")).Value=99;Check(((System.Collections.Generic.List<int>)Field(f,"_differences")).Count==1,"Changing tolerance recalculates the actual comparison");
   Select(f,1);Call(f,"DeleteClassicCurrent");Check(((Subtitle)Field(f,"_subtitle1")).Paragraphs.Count==2 && s.Paragraphs.Count==3,"Delete affects only the working current file");Call(f,"UndoClassicMerge");Check(Json(Field(f,"_subtitle1"))==Json(s),"Delete can be undone locally");
  }
  using(var f=new CompareForm())
  {
   var s=Sample();f.Initialize(s,"current",new Subtitle(),"empty reference");Show(f);Select(f,1);Call(f,"DeleteClassicCurrent");Check(((Subtitle)Field(f,"_subtitle1")).Paragraphs.Count==2,"Current-only cues remain actionable with an empty reference");
  }
  using(var main=new MainForm())
  {
   var sub=Sample();main.GetType().GetField("_subtitle",Flags).SetValue(main,sub);var list=(SubtitleListView)Field(main,"SubtitleListview1");list.GetType().GetMethod("Fill",Flags,null,new[]{typeof(Subtitle),typeof(Subtitle)},null).Invoke(list,new object[]{sub,new Subtitle()});var handle=list.Handle;list.SelectIndexAndEnsureVisibleFaster(0);
   var baseline=new Subtitle(sub,false);var result=new Subtitle(sub,false);result.Paragraphs[0].Text="Merged";
   Check((bool)Call(main,"ApplyClassicCompare",baseline,result) && ((Subtitle)Field(main,"_subtitle")).Paragraphs[0].Text=="Merged","Apply updates the real editor through its standard change path");Check((bool)Call(main,"ApplyClassicCompare",result,result),"Repeated Apply tolerates editor undo history and preserves paragraph IDs");Call(main,"UndoToIndex",true);var restored=(Subtitle)Field(main,"_subtitle");Check(restored.Paragraphs.Count==3 && restored.Paragraphs[0].Text=="First" && restored.Paragraphs[1].Actor=="B" && restored.Paragraphs[1].Extra=="StyleB" && restored.Header==baseline.Header,"Main Undo restores text, timing and metadata after Compare Apply");
   ((Subtitle)Field(main,"_subtitle")).Paragraphs[0].Actor="Changed while compare was open";Check(!(bool)Call(main,"ApplyClassicCompare",baseline,result),"Concurrent editor changes are rejected rather than overwritten");
  }
 }
}
