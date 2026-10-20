using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms.Ocr;
using Nikse.SubtitleEdit.Logic.Ocr;
using Nikse.SubtitleEdit.Logic;
using Updates = Nikse.SubtitleEdit.Logic.CheckForUpdatesHelper;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicUpdatesAndTraining
{
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static void Check(bool ok,string m){if(!ok)throw new Exception(m);Console.WriteLine("PASS: "+m);}
 static string FutureStable {get{var v=typeof(Updates).Assembly.GetName().Version;return new Version(v.Major,v.Minor,v.Build+1).ToString();}}
 static string Release(string tag=null,bool prerelease=false,bool draft=false)=>new JObject{["tag_name"]=tag??FutureStable,["prerelease"]=prerelease,["draft"]=draft,["body"]="Classic release notes",["html_url"]="https://github.com/SubtitleEdit/subtitleedit/releases/tag/5.3.0"}.ToString();
 sealed class Handler:HttpMessageHandler
 {
  public string Body=Release();public HttpStatusCode Status=HttpStatusCode.OK;public int Calls; public string Redirect = Updates.RepositoryUrl+"/releases/tag/4.0.25";
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c){Calls++;if(r.RequestUri.AbsoluteUri==Updates.LatestStableUrl)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect){Headers={Location=new Uri(Redirect)}});Check(r.RequestUri.AbsoluteUri==Updates.StableReleaseApi,"Only the Classic stable release endpoint is requested");Check(r.Headers.UserAgent.ToString().Contains("SubtitleEdit-Classic"),"GitHub User-Agent is set");return Task.FromResult(new HttpResponseMessage(Status){Content=new StringContent(Body)});}
 }
 [STAThread] static int Main(string[] args)
 {
  try
  {
   var root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);
   Configuration.Settings.General.Language="ru-RU";LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));
   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();int result=0;
   using(var anchor=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),Size=new Size(1,1)})
   {
    anchor.Shown+=async(s,e)=>{try{if(args[0]=="live"){var real=new Updates();await real.CheckForUpdates(true);Check(real.Done && real.Error==null,"Live Classic stable API succeeds: "+real.Error);Check(real.ReleasePageUrl.StartsWith(Updates.RepositoryUrl+"/releases/tag/"),"Live download link stays in the fork");Console.WriteLine("Latest stable: "+real.LatestVersionNumber+"; local: "+typeof(Updates).Assembly.GetName().Version+"; update: "+real.IsNewSubtitleEditAvailable());}else await Unit(root);}catch(Exception ex){Console.Error.WriteLine(ex);result=1;}finally{anchor.Close();}};
    Application.Run(anchor);
   }
   return result;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
 static async Task Unit(string root)
 {
  Check(!Updates.IsNewer("4.0.25","4.0.25.26") && !Updates.IsNewer("4.0.25.26","4.0.25.26") && Updates.IsNewer("v4.0.26","4.0.25.26"),"Stable/local version comparison prevents downgrade and detects next stable");
  foreach(var tag in new[]{"5.3.0-beta1","4.0.26-rc1","invalid","4.0"}){try{Updates.ParseStableVersion(tag);throw new Exception("Accepted "+tag);}catch(FormatException){Console.WriteLine("PASS: Reject unstable/invalid tag "+tag);}}
  var handler=new Handler();var update=new Updates(handler);await update.CheckForUpdates(true);
  Check(update.Done && update.Error==null && update.ManualCheck && update.IsNewSubtitleEditAvailable(),"Manual stable update completes");
  Check(update.ReleasePageUrl==Updates.RepositoryUrl+"/releases/tag/"+FutureStable && update.LatestChangeLog.Contains("Classic release notes"),"Release notes and download URL come from the fork");
  var current=typeof(Updates).Assembly.GetName().Version;
  handler.Body=Release(new Version(current.Major,current.Minor,current.Build).ToString());await update.CheckForUpdates(false);Check(!update.IsNewSubtitleEditAvailable(),"The currently installed stable version is not offered as a newer release");
  handler.Body=Release("4.0.25");await update.CheckForUpdates(false);Check(!update.ManualCheck && !update.IsNewSubtitleEditAvailable(),"Startup checker uses the same stable channel and skips older releases");
  foreach(var body in new[]{Release("4.0.26",true),Release("4.0.26",false,true),Release("4.0.26-rc1"),"{}","not json"}){handler.Body=body;await update.CheckForUpdates(true);Check(update.Done && update.Error!=null && !update.IsUpdateAvailable() && update.ReleasePageUrl==Updates.LatestStableUrl,"Reject invalid/draft/prerelease payload without a fallback");}
  foreach(var status in new[]{HttpStatusCode.NotFound,HttpStatusCode.InternalServerError,HttpStatusCode.Redirect}){handler.Status=status;var calls=handler.Calls;await update.CheckForUpdates(false);Check(update.Done && update.Error!=null && handler.Calls==calls+1,"HTTP "+(int)status+" fails once without querying upstream");}
  handler.Status=HttpStatusCode.Forbidden;await update.CheckForUpdates(true);Check(update.Error==null && !update.IsNewSubtitleEditAvailable(),"API rate limiting falls back only to the same fork stable redirect");
  handler.Redirect="https://github.com/SubtitleEdit/subtitleedit/releases/tag/5.3.0";await update.CheckForUpdates(true);Check(update.Error!=null && !update.IsUpdateAvailable(),"Redirect to upstream is rejected");
  handler.Redirect=Updates.RepositoryUrl+"/releases/tag/4.0.26-rc1";await update.CheckForUpdates(true);Check(update.Error!=null,"Unstable fallback tag is rejected");
  Check(VobSubNOcrTrain.ParseFontSizes("30, 40;30 50").SequenceEqual(new[]{30,40,50}),"Multiple font sizes normalize and remove duplicates");
  foreach(var sizes in new[]{"","9","201","30,bad","30.5"}){try{VobSubNOcrTrain.ParseFontSizes(sizes);throw new Exception("Accepted invalid sizes");}catch(FormatException){Console.WriteLine("PASS: Reject invalid font sizes "+sizes);}}
  var settings=new NOcrTrainerSettings{FontNames=new System.Collections.Generic.List<string>{"Arial"},CharactersToTrain="ЖA",NumberOfLineSegments=20,LineSegmentAlgorithm=NOcrLineAlgorithm.SkeletonDistance};
  var db=await Task.Run(()=>VobSubNOcrTrain.TrainSizes(settings,new[]{30,40},null,()=>false));
  Check(db.OcrCharactersCombined.Any(c=>c.Text=="Ж") && db.OcrCharactersCombined.Any(c=>c.Text=="A"),"Real Latin/Cyrillic training learns characters");
  Check(db.OcrCharactersCombined.Select(c=>c.Height).Distinct().Count()>1,"Multiple sizes produce distinct glyph geometry");
  var basePath=Path.Combine(root,"base.nocr");db.FileName=basePath;db.Save();var original=File.ReadAllBytes(basePath);var count=db.TotalCharacterCount;
  settings.CharactersToTrain="БB";var extended=await Task.Run(()=>VobSubNOcrTrain.TrainSizes(settings,new[]{30},basePath,()=>false));
  Check(extended.TotalCharacterCount>count && extended.OcrCharactersCombined.Any(c=>c.Text=="Ж") && extended.OcrCharactersCombined.Any(c=>c.Text=="Б"),"Incremental training retains existing database and adds new glyphs");
  Check(original.SequenceEqual(File.ReadAllBytes(basePath)),"Training never writes to the source database");
  var cancelled=await Task.Run(()=>VobSubNOcrTrain.TrainSizes(settings,new[]{30,40},basePath,()=>true));Check(cancelled.TotalCharacterCount==count && original.SequenceEqual(File.ReadAllBytes(basePath)),"Cancellation preserves source and stops additional training");
  extended.FileName=basePath;extended.Save();Check(original.SequenceEqual(File.ReadAllBytes(basePath+".bak")) && new NOcrDb(basePath).TotalCharacterCount==extended.TotalCharacterCount,"Explicit save is reloadable and backs up the previous database");
  using(var form=new VobSubNOcrTrain())
  {
   form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();await Task.Delay(100);
   var preview=(PictureBox)Field(form,"_fontPreview");var fontList=(ListView)Field(form,"listViewFonts");fontList.Items[0].Selected=true;
   using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(root,"nocr-training-ru.png"));}
   using(var bmp=new Bitmap(preview.Width,preview.Height)){preview.DrawToBitmap(bmp,preview.ClientRectangle);int bright=0;for(int x=0;x<bmp.Width;x++)for(int y=0;y<bmp.Height;y++)if(bmp.GetPixel(x,y).R>100)bright++;Check(bright>100,"Selected-font preview actually renders text");}
   form.Size=form.MinimumSize;await Task.Delay(100);var body=(TableLayoutPanel)Field(form,"_trainingBody");var scroll=(Panel)body.Parent;scroll.AutoScrollPosition=new Point(0,2000);await Task.Delay(50);
   var chars=(TextBox)Field(form,"_trainingCharacters");Check(scroll.RectangleToScreen(scroll.ClientRectangle).Contains(chars.RectangleToScreen(chars.ClientRectangle)),"Bottom training inputs remain reachable at minimum window size");
   var start=(Button)Field(form,"buttonTrain");Check(scroll.RectangleToScreen(scroll.ClientRectangle).Bottom <= start.Parent.RectangleToScreen(start.Parent.ClientRectangle).Top && start.Visible,"Training and close buttons stay outside scrolling content");
   foreach(ListViewItem item in fontList.Items)item.Checked=false;fontList.Items[0].Checked=true;
   chars.Text=NOcrTrainer.DefaultTrainingCharacters;((Control)Field(form,"comboBoxSubtitleFontSize")).Text="30,40";
   typeof(VobSubNOcrTrain).GetMethod("buttonTrain_Click",F).Invoke(form,new object[]{start,EventArgs.Empty});Check((bool)Field(form,"_training"),"Training runs asynchronously");
   form.Close();for(int i=0;i<150 && !form.IsDisposed;i++)await Task.Delay(50);Check(form.IsDisposed,"Closing cancels training and exits without a save dialog");
  }
 }
}

