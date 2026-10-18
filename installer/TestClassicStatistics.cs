using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicStatistics
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static object Call(object o,string n,params object[] a){try{return o.GetType().GetMethod(n,F).Invoke(o,a);}catch(TargetInvocationException ex){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();return null;}}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 static Subtitle Sub(params string[] lines){var s=new Subtitle {Header="Preserved ASS metadata",FileName="stats.srt",OriginalFormat=new SubRip()};for(int i=0;i<lines.Length;i++)s.Paragraphs.Add(new Paragraph(lines[i],1000+i*3000,3000+i*3000){Number=i+1,Actor="Alice",Extra="Style",Layer=2,MarginL="17"});return s;}
 static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();}
 [STAThread]static int Main(string[] args)
 {
  var root=Path.Combine(Path.GetFullPath(args[0]),"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));Console.WriteLine("TEST DATA: "+root);typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();int exit=0;using(var anchor=new Form {ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),Size=new Size(1,1)}){anchor.Shown+=async(s,e)=>{try{await Unit(root);}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{anchor.Close();}};Application.Run(anchor);}return exit;
 }
 static async Task Unit(string root)
 {
  LanguageSettings.Current=new Language();var g=Configuration.Settings.General;g.SubtitleMaximumCharactersPerSeconds=6;g.SubtitleOptimalCharactersPerSeconds=4;g.SubtitleMinimumDisplayMilliseconds=1500;g.SubtitleMaximumDisplayMilliseconds=3500;g.SubtitleLineMaximumLength=8;g.MinimumMillisecondsBetweenLines=83;g.MaxNumberOfLines=2;
  var source=Sub("aaaaaaaaaa","bbbbbbbbbb","cccccccccccccccccccc");source.Paragraphs[0].StartTime.TotalMilliseconds=1000;source.Paragraphs[0].EndTime.TotalMilliseconds=2000;source.Paragraphs[1].StartTime.TotalMilliseconds=2500;source.Paragraphs[1].EndTime.TotalMilliseconds=4500;source.Paragraphs[2].StartTime.TotalMilliseconds=5000;source.Paragraphs[2].EndTime.TotalMilliseconds=9000;var before=JsonConvert.SerializeObject(source);var data=ClassicStatisticsData.Calculate(source);
  Check(data.SubtitleCount==3 && data.Characters==40 && data.Words==3,"Totals count the known source cues, characters and words");Check(data.DurationMilliseconds==7000 && data.DurationText=="00:00:07,000","Total duration is the sum of cue durations, not the file span");var duration=data.Ranges[1];Check(duration.Minimum==1 && Math.Abs(duration.Average-7.0/3)<0.00001 && duration.Maximum==4,"Duration meter holds correct minimum, mean and maximum");Check(data.Ranges[4].Average==500 && data.Ranges[4].Samples==2,"Gap average divides by the number of boundaries");Check(data.CpsBins.Sum()==3 && data.CpsBins[5]==1 && data.CpsBins[2]==2,"Two-CPS histogram bins count each subtitle exactly once");Check(data.OptimalCps==4 && data.MaximumCps==6,"Histogram markers use the current configured optimal and maximum CPS");Check(data.Checks[0].Count==1 && data.Checks[1].Count==3,"CPS checks separate maximum and optimal thresholds");Check(data.Checks[3].Count==1 && data.Checks[4].Count==1,"Duration checks count short and long cues independently");Check(JsonConvert.SerializeObject(source)==before,"Statistics do not modify cue IDs, metadata, text or history");
  var multi=Sub("123456789\n123456789", "123456789\nshort");var multiData=ClassicStatisticsData.Calculate(multi);var length=multiData.Checks.Single(c=>c.Label.StartsWith("Overlong"));Check(length.Count==3 && length.Total==4 && length.Percent==75,"Physical-line violation percentages use physical lines as the denominator");g.MaxNumberOfLines=1;Check(ClassicStatisticsData.Calculate(multi).Checks.Single(c=>c.Label.StartsWith("Too many")).Count==2,"Line-count checks count affected cues rather than physical lines");g.MaxNumberOfLines=2;
  var overlap=Sub("first","second","third");overlap.Paragraphs[0].EndTime.TotalMilliseconds=5000;overlap.Paragraphs[1].StartTime.TotalMilliseconds=4000;overlap.Paragraphs[1].EndTime.TotalMilliseconds=9000;overlap.Paragraphs[2].StartTime.TotalMilliseconds=8000;var over=ClassicStatisticsData.Calculate(overlap).Ranges.Last();Check(over.Minimum==-1000 && over.Average==-1000 && over.Maximum==-1000,"An all-overlap file retains a negative maximum and average gap");Check(over.AverageFraction==0,"Negative gaps clamp only the visual meter, never the actual values");
  var fast=Sub(new string('x',70));fast.Paragraphs[0].EndTime.TotalMilliseconds=1100;var fastData=ClassicStatisticsData.Calculate(fast);Check(fastData.CpsBins[14]==1 && fastData.CpsSamples==1,"Very fast speech is collected in the final 28-plus bin");var zero=Sub("zero");zero.Paragraphs[0].EndTime.TotalMilliseconds=1000;var zeroData=ClassicStatisticsData.Calculate(zero);Check(zeroData.CpsSamples==0 && zeroData.Checks.Last().Count==1 && zeroData.Ranges.All(r=>!double.IsNaN(r.Average) && !double.IsInfinity(r.Average)),"Zero-duration text produces an explicit invalid-duration check and finite metrics");var missing=Sub("missing");missing.Paragraphs[0].StartTime.TotalMilliseconds=TimeCode.MaxTimeTotalMilliseconds;Check(ClassicStatisticsData.Calculate(missing).CpsSamples==0,"Unset time codes are excluded from speed samples");var empty=ClassicStatisticsData.Calculate(new Subtitle());Check(empty.SubtitleCount==0 && empty.DurationText=="00:00:00,000" && empty.CpsBins.Sum()==0 && empty.Checks.All(c=>c.Count==0 && c.Percent==0),"Empty statistics remain finite and have zero counts");Check(ClassicStatisticsData.Calculate(Sub("single")).Ranges.Count==4,"Single-cue statistics do not invent a gap sample");
  foreach(var lang in new[]{"en-US","ru-RU"})
  {
   g.Language=lang;LanguageSettings.Current=lang=="en-US"?new Language():Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages",lang+".xml"));
   using(var form=new Statistics(source,"stats.srt",new SubRip()))
   {
    Show(form);await Task.Delay(30);var tabs=(TabControl)Field(form,"_statisticsTabs");Check(tabs.TabPages.Count==3,"Dashboard, full details and frequency lists are all available: "+lang);
    var model=(ClassicStatisticsData)Field(form,"_dashboardData");Check(model.Characters==data.Characters && model.DurationMilliseconds==data.DurationMilliseconds,"UI metrics match the validated report model: "+lang);
    foreach(var size in new[]{form.Size,form.MinimumSize,new Size(1220,950)})
    {
     form.Size=size;await Task.Delay(25);Call(form,"FitDashboard");var dashboard=(Panel)Field(form,"_dashboardPanel");Check(dashboard.Controls.Cast<Control>().All(c=>c.Right<=dashboard.ClientSize.Width),"Dashboard sections stay within the scroll viewport: "+lang+" "+size);var layout=form.Controls.OfType<TableLayoutPanel>().Single();var footer=layout.GetControlFromPosition(0,1);Check(footer.Bottom<=form.ClientSize.Height && footer.Controls.Cast<Control>().All(c=>c.Right<=footer.ClientSize.Width),"Statistics footer stays visible: "+lang+" "+size);
    }
    form.Size=form.MinimumSize;await Task.Delay(25);var panel=(Panel)Field(form,"_dashboardPanel");panel.AutoScrollPosition=new Point(0,panel.DisplayRectangle.Height);await Task.Delay(20);Check(((Control)Field(form,"_histogramCard")).Bottom<=panel.ClientSize.Height,"Last histogram section can be fully scrolled into view: "+lang);Check(!panel.HorizontalScroll.Visible,"Dashboard requires no horizontal scroll: "+lang);
    using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(root,"statistics-bottom-"+lang+".png"));}panel.AutoScrollPosition=Point.Empty;using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(root,"statistics-top-"+lang+".png"));}
    var path=Path.Combine(root,"statistics-"+lang+".txt");form.SaveStatisticsReport(path);var text=File.ReadAllText(path);Check(text.Contains("28+") && text.Contains("500") && text.Contains("Most Used Words") && text.Contains("Most Used Lines"),"Full text export contains dashboard, histogram and both legacy frequency sections: "+lang);Check(!text.Contains("NaN") && !text.Contains("Infinity"),"Normal exported statistics contain finite values: "+lang);
    tabs.SelectedIndex=1;await Task.Delay(10);var details=(Control)Field(form,"textBoxGeneral");Check(details.Text.Contains("500") && details.Width<=tabs.SelectedTab.ClientSize.Width,"Complete text details remain readable inside the Details tab: "+lang);tabs.SelectedIndex=2;await Task.Delay(10);var words=(Control)Field(form,"textBoxMostUsedWords");var lines=(Control)Field(form,"textBoxMostUsedLines");Check(words.Visible && lines.Visible,"Both frequency lists remain available on the final tab: "+lang);
   }
   var repeated=Sub("repeat word\nrepeat word.","repeat word\nrepeat word.");using(var form=new Statistics(repeated,"repeated.srt",new SubRip())) {Check(((Control)Field(form,"textBoxMostUsedWords")).Text.Contains("4: repeat"),"Word frequencies split physical newlines correctly: "+lang);Check(((Control)Field(form,"textBoxMostUsedLines")).Text.Contains("2:"),"Repeated-line frequencies are retained: "+lang);Call(form,"CalculateWordStatistics");Check((int)Field(form,"_totalWords")==8,"Recalculating word statistics does not double totals: "+lang);}
   using(var form=new Statistics(new Subtitle(),"empty.srt",new SubRip())) {Check(form.BuildFullStatisticsReport().Contains(LanguageSettings.Current.Statistics.NothingFound),"Empty-file text export states there are no results: "+lang);}
  }
  Check(JsonConvert.SerializeObject(source)==before,"All UI viewing/export operations leave source subtitles unchanged");var large=Sub(Enumerable.Repeat("Some repeated text.",10000).ToArray());var clock=System.Diagnostics.Stopwatch.StartNew();using(var form=new Statistics(large,"large.srt",new SubRip())){Check(((ClassicStatisticsData)Field(form,"_dashboardData")).SubtitleCount==10000,"A large subtitle is fully represented without sampling");}Console.WriteLine("Large-file statistics milliseconds: "+clock.ElapsedMilliseconds);
  File.WriteAllText(Path.Combine(root,"validation.json"),JsonConvert.SerializeObject(new {success=true,version=typeof(Statistics).Assembly.GetName().Version.ToString(),paidRequests=0}));
 }
}
