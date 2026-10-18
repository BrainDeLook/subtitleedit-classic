using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestAiReviewPasses
{
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static object Call(object o,string n,params object[] a){try{return o.GetType().GetMethod(n,F).Invoke(o,a);}catch(TargetInvocationException e){throw e.InnerException;}}
 static void Check(bool ok,string m){if(!ok)throw new Exception(m);Console.WriteLine("PASS: "+m);}
 static IEnumerable<Control> Desc(Control o){foreach(Control c in o.Controls){yield return c;foreach(var d in Desc(c))yield return d;}}
 static T Control<T>(Control f,string name) where T:Control=>Desc(f).OfType<T>().Single(c=>c.Name==name);
 static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();}
 sealed class Handler:HttpMessageHandler
 {
  public Func<JObject,CancellationToken,Task<HttpResponseMessage>> Run;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c)=>await Run(JObject.Parse(await r.Content.ReadAsStringAsync()),c);
  protected override void Dispose(bool disposing){}
 }
 static HttpResponseMessage Reply(JObject obj)=>new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(new JObject{["choices"]=new JArray(new JObject{["message"]=new JObject{["content"]=obj.ToString()}})}.ToString(),Encoding.UTF8,"application/json")};
 static Subtitle Sample(int count=3){var s=new Subtitle();for(var i=0;i<count;i++)s.Paragraphs.Add(new Paragraph(i==0?"I has a apple.":i==1?"hello World.":"Original meaning.",1000+i*3000,3000+i*3000){Actor="A"+i,Extra="Default"});return s;}
 static JObject Change(int n,string orig,string text,string category)=>new JObject{["n"]=n,["orig"]=orig,["text"]=text,["reason"]="Test",["category"]=category};
 [STAThread]static int Main(string[] args){try{
  var root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);Configuration.Settings.General.Language="ru-RU";LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();
  var result=0;using(var anchor=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),Size=new Size(1,1)}){anchor.Shown+=async(s,e)=>{try{if(args[0]=="native")await Native(root,args[2]);else await Unit(root);}catch(Exception ex){Console.Error.WriteLine(ex);result=1;}finally{ClassicLlamaServer.Stop();anchor.Close();}};Application.Run(anchor);}return result;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static async Task Unit(string root)
 {
  Check(ClassicAiReview.SimilarityPercent("<i>Hello world</i>"," HELLO WORLD ")==100,"Similarity ignores tags, whitespace and casing");
  var source=Sample();var parsed=ClassicAiReview.Parse(new JObject{["changes"]=new JArray(Change(1,source.Paragraphs[0].Text,"I have an apple.","grammar"),Change(2,source.Paragraphs[1].Text,"Hello World.","casing"),Change(3,source.Paragraphs[2].Text,"A completely unrelated rewritten paragraph about something else.","unexpected"))}.ToString(),source.Paragraphs.Select((p,i)=>new{p,i}).ToDictionary(x=>x.i+1,x=>x.p.Text));
  Check(parsed[0].Category==ClassicAiCategory.Grammar && parsed[1].Category==ClassicAiCategory.Casing && parsed[2].Category==ClassicAiCategory.Other,"Review retains categories and safely maps unknown categories");
  Check(parsed[0].Selected && !parsed[2].Selected && parsed[2].IsWarning,"Large rewrites are flagged and unchecked by default");
  var handler=new Handler{Run=(body,c)=>Task.FromResult(Reply(new JObject{["changes"]=new JArray(parsed.Select(p=>Change(p.Number,p.Original,p.Text,p.Category.ToString())))}))};
  using(var main=new Nikse.SubtitleEdit.Forms.Main())
  {
   main.GetType().GetField("_subtitle",F).SetValue(main,new Subtitle(source));
   var list=(Nikse.SubtitleEdit.Controls.SubtitleListView)Field(main,"SubtitleListview1");list.GetType().GetMethod("Fill",F,null,new[]{typeof(Subtitle),typeof(Subtitle)},null).Invoke(list,new object[]{(Subtitle)Field(main,"_subtitle"),new Subtitle()});list.SelectIndexAndEnsureVisibleFaster(0);
   var handle=list.Handle;foreach(ListViewItem item in list.Items)item.Selected=false;list.Items[1].Selected=true;
   Check(((int[])Call(main,"ClassicAiReviewIndices",false,true)).SequenceEqual(new[]{1}) && ((int[])Call(main,"ClassicAiReviewIndices",false,false)).Length==3,"Selected-line entry scopes AI review to the chosen cues");
   Call(main,"AddClassicAiSelectedMenu");Check(((ToolStripMenuItem)Field(main,"toolStripMenuItemSelectedLines")).DropDownItems.ContainsKey("classicAiReviewSelected"),"Selected-lines menu exposes AI review");
   int played=-1,stopped=0;
   using(var form=new ClassicAiReviewForm(source,new[]{0,1,2},false,changes=>(bool)Call(main,"ApplyClassicAiPass",changes),i=>played=i,()=>stopped++,handler))
   {
    Show(form);await (Task)Call(form,"Work",false);var grid=Control<DataGridView>(form,"aiReviewSuggestions");
    Check(grid.Rows.Count==3 && !(bool)grid.Rows.Cast<DataGridViewRow>().Single(r=>((ClassicAiChange)r.Tag).IsWarning).Cells[0].Value,"Warning rows remain visible without automatic approval");
    Control<Button>(form,"aiReviewSelectNone").PerformClick();Check(grid.Rows.Cast<DataGridViewRow>().All(r=>!(bool)r.Cells[0].Value),"Select none clears every suggestion");
    var category=Control<ComboBox>(form,"aiReviewCategory");category.SelectedIndex=2;Check(grid.Rows.Count==1 && ((ClassicAiChange)grid.Rows[0].Tag).Category==ClassicAiCategory.Grammar,"Category filter shows only grammar changes");
    grid.Rows[0].Cells[0].Value=true;Control<Button>(form,"aiReviewApply").PerformClick();
    var live=(Subtitle)Field(main,"_subtitle");Check(form.Visible && live.Paragraphs[0].Text=="I have an apple." && live.HistoryItems.Count==1,"First pass updates main subtitle and leaves review open with one undo step");
    category.SelectedIndex=0;Check(grid.Rows.Count==2,"Applied suggestions disappear permanently across filter changes");
    var casing=grid.Rows.Cast<DataGridViewRow>().Single(r=>((ClassicAiChange)r.Tag).Category==ClassicAiCategory.Casing);grid.CurrentCell=casing.Cells["Suggestion"];Control<Button>(form,"aiReviewPlay").PerformClick();Check(played==1,"Play current follows selected suggestion identity");
    casing.Cells["Suggestion"].Value="Hello world.";casing.Cells[0].Value=true;grid.Sort(grid.Columns[1],System.ComponentModel.ListSortDirection.Descending);Control<Button>(form,"aiReviewApply").PerformClick();
    Check(live.Paragraphs[1].Text=="Hello world." && live.HistoryItems.Count==2 && form.Visible,"Edited suggestion applies after sorting as a separate second pass");
    Check(live.Paragraphs[0].Actor=="A0" && live.Paragraphs[0].Extra=="Default" && live.Paragraphs[0].StartTime.TotalMilliseconds==1000 && live.Paragraphs[2].Text==source.Paragraphs[2].Text,"Passes preserve timing, actors, styles and unchecked lines");
    Control<CheckBox>(form,"aiReviewWarnings").Checked=true;Check(grid.Rows.Count==1 && ((ClassicAiChange)grid.Rows[0].Tag).IsWarning,"Warnings-only filter isolates the large rewrite");
    foreach(var size in new[]{new Size(1050,760),new Size(800,640)}){form.Size=size;Application.DoEvents();foreach(var button in Desc(form).OfType<Button>().Where(b=>b.Name=="aiReviewApply" || b.Name=="aiReviewPlay" || b.Name=="aiReviewRun"))Check(button.Parent.ClientRectangle.Contains(button.Bounds),"Footer action fits at "+size);using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(root,"review-"+size.Width+".png"));}}
    bool updatedInput=false;handler.Run=(body,c)=>{var lines=JObject.Parse((string)body["messages"][1]["content"])["lines"];updatedInput=(string)lines[0]["text"]=="I have an apple." && (string)lines[1]["text"]=="Hello world.";return Task.FromResult(Reply(new JObject{["changes"]=new JArray()}));};
    await (Task)Call(form,"Work",false);Check(updatedInput && grid.Rows.Count==0,"A new review uses applied text and clears old suggestions");
    form.Close();Check(stopped>0,"Closing review stops its playback callback");
   }
   Call(main,"UndoToIndex",true);Check(((Subtitle)Field(main,"_subtitle")).Paragraphs[1].Text==source.Paragraphs[1].Text && ((Subtitle)Field(main,"_subtitle")).Paragraphs[0].Text=="I have an apple.","Main undo reverses the second pass only");
   Call(main,"UndoToIndex",true);Check(((Subtitle)Field(main,"_subtitle")).Paragraphs[0].Text==source.Paragraphs[0].Text,"A second undo restores the first pass");
   var stale=new ClassicAiChange{Number=1,Original="Old source",Text="New text"};var history=((Subtitle)Field(main,"_subtitle")).HistoryItems.Count;Check(!(bool)Call(main,"ApplyClassicAiPass",(object)new[]{stale}) && ((Subtitle)Field(main,"_subtitle")).HistoryItems.Count==history,"Stale source is rejected before creating an undo step");
  }
  var many=Sample(16);var times=new List<long>();var watch=Stopwatch.StartNew();handler=new Handler{Run=(body,c)=>{times.Add(watch.ElapsedMilliseconds);var lines=JObject.Parse((string)body["messages"][1]["content"])["lines"];var line=lines.First;return Task.FromResult(Reply(new JObject{["changes"]=new JArray(Change((int)line["n"],(string)line["text"],"I have an apple.","grammar"))}));}};
  using(var form=new ClassicAiReviewForm(many,Enumerable.Range(0,16).ToArray(),handler:handler)){Show(form);Control<NumericUpDown>(form,"aiReviewDelay").Value=1;await (Task)Call(form,"Work",false);Check(times.Count==2 && times[1]-times[0]>=900,"Configured request delay separates review batches");form.Close();}
  times.Clear();using(var form=new ClassicAiReviewForm(many,Enumerable.Range(0,16).ToArray(),handler:handler)){Show(form);Control<NumericUpDown>(form,"aiReviewDelay").Value=5;var task=(Task)Call(form,"Work",false);using(var timer=new System.Windows.Forms.Timer{Interval=100}){timer.Tick+=(s,e)=>{if(times.Count==0)return;timer.Stop();form.Close();};timer.Start();await task;}Check(times.Count==1 && !form.Visible,"Close cancels an active request delay and completes cleanup without another request");}
  times.Clear();using(var form=new ClassicAiReviewForm(many,Enumerable.Range(0,16).ToArray(),handler:handler)){Show(form);Control<NumericUpDown>(form,"aiReviewDelay").Value=5;var task=(Task)Call(form,"Work",false);using(var timer=new System.Windows.Forms.Timer{Interval=100}){timer.Tick+=(s,e)=>{if(times.Count==0)return;timer.Stop();Control<Button>(form,"aiReviewStop").PerformClick();};timer.Start();await task;}Check(times.Count==1 && form.Visible && Control<DataGridView>(form,"aiReviewSuggestions").Rows.Count==1,"Stop retains completed suggestions and leaves the review usable");form.Close();}
  var tagged=Sample(1);tagged.Paragraphs[0].Text="<i>I has a apple.</i>";handler=new Handler{Run=(body,c)=>Task.FromResult(Reply(new JObject{["changes"]=new JArray(Change(1,tagged.Paragraphs[0].Text,"<i>I have an apple.</i>","grammar"))}))};
  using(var form=new ClassicAiReviewForm(tagged,new[]{0},handler:handler)){Show(form);await (Task)Call(form,"Work",false);var row=Control<DataGridView>(form,"aiReviewSuggestions").Rows[0];row.Cells["Suggestion"].Value="I have an apple.";row.Cells[0].Value=true;Control<Button>(form,"aiReviewApply").PerformClick();Check(form.EditedSubtitle==null && form.Visible,"Manually edited suggestions cannot remove formatting tags");form.Close();}
 }
 static async Task Native(string root,string models)
 {
  var config=ClassicLlamaSettings.Load();config.Variant="cuda13";config.Folder=models;config.Save();var settings=ClassicAiSettings.Load();settings.UseLocalLlama=true;settings.RequestDelaySeconds=0;settings.Save();var source=Sample(1);
  using(var form=new ClassicAiReviewForm(source,new[]{0})){Show(form);await (Task)Call(form,"Work",false);var grid=Control<DataGridView>(form,"aiReviewSuggestions");Check(grid.Rows.Count>0,"Native managed Qwen produces a source-matched review suggestion");foreach(DataGridViewRow row in grid.Rows)Console.WriteLine(((ClassicAiChange)row.Tag).Text);Check(grid.Rows.Cast<DataGridViewRow>().Any(r=>((ClassicAiChange)r.Tag).Text.IndexOf("I have an apple",StringComparison.OrdinalIgnoreCase)>=0),"Native review fixes known English grammar");Check(ClassicLlamaServer.IsRunning,"Managed server remains controllable after review");await (Task)Call(form,"ToggleServer");Check(!ClassicLlamaServer.IsRunning,"Review can release the managed server without closing");await (Task)Call(form,"ToggleServer");Check(ClassicLlamaServer.IsRunning,"Review restarts the configured local model");form.Close();Check(!ClassicLlamaServer.IsRunning,"Closing review releases its managed model");}
 }
}
