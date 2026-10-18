using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
class TestClassicCompareSync
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
 static void Check(bool b,string m){if(!b)throw new Exception(m);Console.WriteLine("PASS: "+m);}
 static string Json(object o)=>JsonConvert.SerializeObject(o);
 static Subtitle Lines(params (string Text,double Start)[] lines){var sub=new Subtitle();foreach(var p in lines)sub.Paragraphs.Add(new Paragraph(p.Text,p.Start,p.Start+1000){Number=sub.Paragraphs.Count+1,Actor="Actor",Extra="Style"});return sub;}
 static List<(int Left,int Right)> Rows(CompareForm f)=>((IEnumerable)Field(f,"_classicRows")).Cast<object>().Select(p=>((int)Field(p,"Left"),(int)Field(p,"Right"))).ToList();
 static int Points(CompareForm f)=>((IEnumerable)Call(f,"ValidClassicSyncIndexes")).Cast<object>().Count();
 static Subtitle Working(CompareForm f)=>(Subtitle)Field(f,"_subtitle1");
 static bool Pin(CompareForm f,int left,int right)=>(bool)Call(f,"AddClassicSyncPoint",Working(f).Paragraphs[left].Id,((Subtitle)Field(f,"_subtitle2")).Paragraphs[right].Id);
 static void Select(CompareForm f,int index,bool right=false){var rows=Rows(f);var row=rows.FindIndex(p=>right?p.Right==index:p.Left==index);var list=(SubtitleListView)Field(f,right?"subtitleListView2":"subtitleListView1");list.SelectedItems.Clear();list.Items[row].Selected=true;Call(f,"SelectClassicPair",list);}
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
 static List<(int Left,int Right)> Align(Subtitle left,Subtitle right,(int Left,int Right)[] points=null)
 {
  var t=typeof(CompareForm).Assembly.GetType("Nikse.SubtitleEdit.Logic.ClassicCompareAligner");var line=t.GetNestedType("Line",BindingFlags.NonPublic|BindingFlags.Public);var lt=typeof(List<>).MakeGenericType(line);
  var a=(IList)Activator.CreateInstance(lt);var b=(IList)Activator.CreateInstance(lt);
  foreach(var p in left.Paragraphs)a.Add(Activator.CreateInstance(line,new object[]{p.Text,p.StartTime.TimeSpan,p.EndTime.TimeSpan}));foreach(var p in right.Paragraphs)b.Add(Activator.CreateInstance(line,new object[]{p.Text,p.StartTime.TimeSpan,p.EndTime.TimeSpan}));
  var result=(IEnumerable)t.GetMethod("Align",BindingFlags.Static|BindingFlags.Public).Invoke(null,new object[]{a,b,new Func<TimeSpan,TimeSpan,bool>((x,y)=>Math.Abs((x-y).TotalMilliseconds)<.1),points});
  return result.Cast<object>().Select(p=>((int)p.GetType().GetProperty("Left").GetValue(p),(int)p.GetType().GetProperty("Right").GetValue(p))).ToList();
 }
 static async Task Unit(string root)
 {
  Configuration.Settings.Compare.ShowOnlyDifferences=false;Configuration.Settings.Compare.OnlyLookForDifferenceInText=false;
  var left=Lines(("A",0),("B",2000),("C",4000),("D",6000),("E",8000),("F",10000));var right=Lines(("A",700),("B",2700),("D",6700),("E",8700),("F",10700));
  Check(Align(left,right).SequenceEqual(new[]{(0,0),(1,1),(2,-1),(3,2),(4,3),(5,4)}),"Stable aligner restores text alignment after a missing cue in a retimed file");
  Check(Align(Lines(("Yes.",0),("I told you so.",2000),("Yes.",4000),("No.",6000)),Lines(("Yes.",0),("Wait!",1000),("I told ya so.",2000),("Yes.",4000),("No.",6000))).SequenceEqual(new[]{(0,0),(-1,1),(1,2),(2,3),(3,4)}),"Fuzzy text and timing distinguish repeated lines and an insertion");
  var largeLeft=new Subtitle();var largeRight=Lines(("Extra",0));for(int i=0;i<3000;i++){largeLeft.Paragraphs.Add(new Paragraph("Original "+i,2000+i*2000,3000+i*2000));largeRight.Paragraphs.Add(new Paragraph("Translation "+i,2000+i*2000,3000+i*2000));}
  var watch=Stopwatch.StartNew();var aligned=Align(largeLeft,largeRight);watch.Stop();Check(aligned.SequenceEqual(new[]{(-1,0)}.Concat(Enumerable.Range(0,3000).Select(i=>(i,i+1)))) && watch.ElapsedMilliseconds<5000,"Large bilingual file aligns by timing without a quadratic full-file matrix");Console.WriteLine("ALIGN 3000: "+watch.ElapsedMilliseconds+" ms");
  foreach(var lang in new[]{"ru-RU","en-US"})
  {
   Configuration.Settings.General.Language=lang;LanguageSettings.Current=lang=="en-US"?new Language():Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages",lang+".xml"));
   var source=Lines(("A",1000),("B",3000),("C",5000),("D",7000),("E",9000),("F",11000));var reference=Lines(("A",1700),("B",3700),("C",5700),("D",8200),("E",10200),("F",12200));var original=Json(source);var originalRef=Json(reference);
   using(var f=new CompareForm())
   {
    f.Initialize(source,"current.srt",reference,"reference.srt");Show(f);await Task.Delay(30);
    Check(Pin(f,3,3) && Working(f).Paragraphs[5].StartTime.TotalMilliseconds==12200,"A timing point shifts the tail, preserving cue duration: "+lang);
    Check(Pin(f,1,1) && Points(f)==2,"An earlier timing point keeps a later noncrossing anchor");
    var work=Working(f);Check(work.Paragraphs.Select(p=>p.StartTime.TotalMilliseconds).SequenceEqual(new double[]{1000,3700,5700,8200,10200,12200}),"Earlier point shifts only up to the next anchor and leaves preceding cues alone");
    Check(work.Paragraphs.All(p=>p.DurationTotalMilliseconds==1000 && p.Actor=="Actor" && p.Extra=="Style") && Json(source)==original && Json(reference)==originalRef,"Sync preserves duration, actors, styles and both source inputs");
    var before=Json(work);((CheckBox)Field(f,"_classicShiftSync")).Checked=false;Check(Pin(f,4,2) && Points(f)==2 && Rows(f).Contains((4,2)),"A crossing pair replaces conflicting anchors and is forced into one row");Check(Json(Working(f))==before,"Pair-only mode leaves every timing unchanged");
    Call(f,"UndoClassicMerge");Check(Points(f)==2 && Rows(f).Contains((3,3)) && !Rows(f).Contains((4,2)),"Undo restores the previous anchor set and content together");
    Select(f,3);Call(f,"DeleteClassicCurrent");Check(Points(f)==1,"Deleting an anchored cue removes its active sync marker");Call(f,"UndoClassicMerge");Check(Points(f)==2,"Undo deletion restores the cue and its sync marker");
    Select(f,1);Call(f,"RemoveClassicSyncPoint");Check(Points(f)==1 && Json(Working(f))==before,"Removing a point does not reverse already pending timing edits");Call(f,"UndoClassicMerge");
    Call(f,"ClearClassicSyncPoints");Check(Points(f)==0 && Json(Working(f))==before,"Clear points keeps pending timing edits");Call(f,"UndoClassicMerge");Check(Points(f)==2,"Clear points is undoable");
    Select(f,0);Call(f,"PickClassicSyncHalf",true);Check(((Button)Field(f,"_classicCancelSync")).Enabled,"Half-picked sync has an explicit cancel action");Call(f,"ProcessCmdKey",Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),Keys.Escape);Check(!((Button)Field(f,"_classicCancelSync")).Enabled && Points(f)==2,"Escape cancels a half-picked pair without changing existing points");
    Select(f,0);Call(f,"PickClassicSyncHalf",true);Select(f,2,true);Call(f,"PickClassicSyncHalf",false);Check(Rows(f).Contains((0,2)),"Two-step toolbar selection pairs the originally picked cue with a different reference row");
    Call(f,"UndoClassicMerge");Select(f,1);var list=(SubtitleListView)Field(f,"subtitleListView1");Check(list.SelectedItems[0].SubItems[0].Text.Contains("↔"),"Pinned pairs have a visible link marker");
    var applyCalls=0;f.ApplyComparedSubtitle=(baseline,result)=>{applyCalls++;return Json(baseline)==original && result.Paragraphs[1].StartTime.TotalMilliseconds==3700;};Call(f,"ApplyClassicMerge");Check(applyCalls==1 && !((Button)Field(f,"_classicApply")).Enabled && Points(f)==2,"Apply returns retimed content and keeps view anchors without changing the reference");
    f.Size=f.MinimumSize;await Task.Delay(30);foreach(var name in new[]{"_classicPickLeft","_classicPickRight","_classicCancelSync","_classicClearSync"}){var button=(Button)Field(f,name);var rectangle=button.RectangleToScreen(button.ClientRectangle);Check(rectangle.Right<=f.RectangleToScreen(f.ClientRectangle).Right && rectangle.Bottom<=button.Parent.RectangleToScreen(button.Parent.ClientRectangle).Bottom,"Sync control fits at minimum size: "+name);}
    using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,f.Width,f.Height));bmp.Save(Path.Combine(root,"compare-sync-"+lang+".png"));}
    Call(f,"ResetClassicReferenceSync");Check(Points(f)==0 && !((Button)Field(f,"_classicCancelSync")).Enabled,"Loading another reference clears anchors and half-picked state");
    Call(f,"DisableClassicMerge");var unchanged=Json(Working(f));Check(Pin(f,0,2) && Json(Working(f))==unchanged && !((Button)Field(f,"_classicApply")).Enabled,"Read-only comparison supports pairing without retiming or editor Apply");Call(f,"UndoClassicMerge");Check(Points(f)==0,"Read-only pairing can be undone");
   }
   Check(Json(source)==original,"Closing a sync comparison leaves the original source intact");
  }
  var replacement=Path.Combine(root,"replacement.srt");File.WriteAllText(replacement,"1\n00:00:02,000 --> 00:00:03,000\nNew reference\n");
  using(var f=new CompareForm())
  {
   var initial=Lines(("First",1000),("Second",4000));f.Initialize(initial,"current",Lines(("First",2000),("Second",5000)),"reference");Show(f);Pin(f,0,0);Call(f,"LoadAndCompare",2,replacement);
   Check(Points(f)==0 && ((Subtitle)Field(f,"_subtitle2")).Paragraphs[0].Text=="New reference","Actual reference-file loading clears anchors while using the new file");Call(f,"UndoClassicMerge");Check(Points(f)==0 && Working(f).Paragraphs[0].StartTime.TotalMilliseconds==1000,"Undo after replacing the reference restores current timing without resurrecting old reference anchors");
  }
  var negative=Lines(("First",1000),("Overlapping earlier start",500));var negativeRef=Lines(("First",0),("Second",500));using(var f=new CompareForm()){f.Initialize(negative,"current",negativeRef,"reference");Show(f);var before=Json(Working(f));Check(!Pin(f,0,0) && Points(f)==0 && Json(Working(f))==before,"A shift producing negative times is rejected atomically");}
 }
}
