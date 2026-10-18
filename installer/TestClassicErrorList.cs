using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Forms;
using MainForm=Nikse.SubtitleEdit.Forms.Main;
using Nikse.SubtitleEdit.Logic;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicErrorList
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object Call(object o,string n,params object[] a){try{return o.GetType().GetMethod(n,F).Invoke(o,a);}catch(TargetInvocationException ex){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();return null;}}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 static Subtitle Sub(params string[] text){var s=new Subtitle {Header="Preserved header",FileName="example.srt",OriginalFormat=new SubRip()};for(int i=0;i<text.Length;i++)s.Paragraphs.Add(new Paragraph(text[i],1000+i*3000,3000+i*3000){Number=i+1,Actor="Alice",Extra="Style",Layer=2,MarginL="17"});return s;}
 static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();}
 static void Defaults()
 {
  var t=Configuration.Settings.Tools;var g=Configuration.Settings.General;t.ListViewSyntaxColorDurationSmall=true;t.ListViewSyntaxColorDurationBig=true;t.ListViewSyntaxColorOverlap=true;t.ListViewSyntaxColorGap=true;t.ListViewSyntaxColorLongLines=true;t.ListViewSyntaxColorWideLines=false;t.ListViewSyntaxMoreThanXLines=true;
  g.SubtitleMinimumDisplayMilliseconds=1000;g.SubtitleMaximumDisplayMilliseconds=8000;g.SubtitleMaximumCharactersPerSeconds=25;g.SubtitleLineMaximumLength=43;g.MaxNumberOfLines=2;g.MinimumMillisecondsBetweenLines=83;
 }
 [STAThread]static int Main(string[] args)
 {
  var root=Path.Combine(Path.GetFullPath(args[0]),"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));Console.WriteLine("TEST DATA: "+root);typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();int exit=0;using(var anchor=new Form {ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),Size=new Size(1,1)}){anchor.Shown+=async(s,e)=>{try{await Unit(root);}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{anchor.Close();}};Application.Run(anchor);}return exit;
 }
 static async Task Unit(string root)
 {
  Defaults();LanguageSettings.Current=new Language();var sample=Sub(new string('x',50)+Environment.NewLine+new string('y',48)+Environment.NewLine+"third line", "second", "third");sample.Paragraphs[0].EndTime.TotalMilliseconds=1500;sample.Paragraphs[1].StartTime.TotalMilliseconds=1400;sample.Paragraphs[1].EndTime.TotalMilliseconds=9500;sample.Paragraphs[2].StartTime.TotalMilliseconds=9540;sample.Paragraphs[2].EndTime.TotalMilliseconds=11540;
  var before=JsonConvert.SerializeObject(sample);var errors=ClassicErrorReport.Analyze(sample,new SubRip(),"a.srt");
  foreach(var type in new[]{ClassicLineErrorType.TooManyLines,ClassicLineErrorType.ReadingSpeed,ClassicLineErrorType.TooShort,ClassicLineErrorType.TooLong,ClassicLineErrorType.LineTooLong,ClassicLineErrorType.Overlap,ClassicLineErrorType.GapTooShort})Check(errors.Any(e=>e.Type==type),"Report detects "+type);
  Check(errors.Count(e=>e.Number==1 && e.Type==ClassicLineErrorType.LineTooLong)==2,"Every overlong physical line gets its own report row");Check(errors.Count(e=>e.Type==ClassicLineErrorType.Overlap)==2 && errors.Any(e=>e.Detail.Contains("previous")) && errors.Any(e=>e.Detail.Contains("next")),"An overlap is reported on both neighbouring cues with direction");Check(errors.Count(e=>e.Type==ClassicLineErrorType.GapTooShort)==2,"A short positive gap is reported on both sides");Check(JsonConvert.SerializeObject(sample)==before,"Analysis does not change text, bookmarks, IDs or metadata");Check(errors[0].Cue.Id==sample.Paragraphs[0].Id && errors[0].Cue.Layer==2,"Report snapshots retain source identity and ASS metadata");sample.Paragraphs[0].Text="edited";Check(errors[0].Text.Contains("third line"),"Report text remains frozen after a source edit");
  var untimed=Sub("Untimed");untimed.Paragraphs[0].StartTime.TotalMilliseconds=TimeCode.MaxTimeTotalMilliseconds;Check(ClassicErrorReport.Analyze(untimed,new SubRip()).Any(e=>e.Type==ClassicLineErrorType.MissingTimeCodes),"Classic missing-time-code diagnosis is preserved");
  var wordy=Sub(string.Join(" ",Enumerable.Repeat("a",20)));wordy.Paragraphs[0].EndTime.TotalMilliseconds=1100;Check(ClassicErrorReport.Analyze(wordy,new SubRip()).Any(e=>e.Type==ClassicLineErrorType.WordsPerMinute),"Classic words-per-minute diagnosis is preserved");
  var boundary=Sub(new string('a',25));boundary.Paragraphs[0].EndTime.TotalMilliseconds=2000;Check(ClassicErrorReport.Analyze(boundary,new SubRip()).Count==0,"Values exactly at minimum duration and CPS limits are accepted");boundary.Paragraphs[0].Text="short";boundary.Paragraphs[0].EndTime.TotalMilliseconds=9000;Check(!ClassicErrorReport.Analyze(boundary,new SubRip()).Any(e=>e.Type==ClassicLineErrorType.TooLong),"Duration equal to maximum is accepted");
  var pair=Sub("A", "B");pair.Paragraphs[1].StartTime.TotalMilliseconds=3083;Check(!ClassicErrorReport.Analyze(pair,new SubRip()).Any(e=>e.Type==ClassicLineErrorType.GapTooShort),"Gap equal to configured minimum is accepted");
  var tools=Configuration.Settings.Tools;foreach(var property in tools.GetType().GetProperties().Where(p=>new[]{"ListViewSyntaxColorDurationSmall","ListViewSyntaxColorDurationBig","ListViewSyntaxColorOverlap","ListViewSyntaxColorGap","ListViewSyntaxColorLongLines","ListViewSyntaxColorWideLines","ListViewSyntaxMoreThanXLines"}.Contains(p.Name)))property.SetValue(tools,false);Check(ClassicErrorReport.Analyze(Sub(new string('x',100)+"\nB\nC"),new SubRip()).Count==0,"Disabled syntax-colouring checks do not appear in the report");
  tools.ListViewSyntaxColorWideLines=true;Configuration.Settings.General.SubtitleLineMaximumPixelWidth=20;Check(ClassicErrorReport.Analyze(Sub("A very wide subtitle"),new SubRip()).Any(e=>e.Type==ClassicLineErrorType.LineTooWide),"Pixel-width check uses classic font measurement and configured limit");Defaults();
  tools.ListViewSyntaxColorLongLines=false;var tele=Sub(new string('t',38));tele.Header=new Ebu.EbuGeneralSubtitleInformation {DisplayStandardCode="1"}.ToString();Check(ClassicErrorReport.Analyze(tele,new Ebu()).Any(e=>e.Detail.StartsWith("Teletext")),"Teletext EBU width applies independently of the general long-line switch");tele.Header=new Ebu.EbuGeneralSubtitleInformation {DisplayStandardCode="0"}.ToString();Check(!ClassicErrorReport.Analyze(tele,new Ebu()).Any(e=>e.Type==ClassicLineErrorType.LineTooLong),"Open-subtitling EBU is not incorrectly constrained to teletext width");Defaults();
  var noErrors=ClassicErrorReport.Analyze(Sub("A short line."),new SubRip());Check(noErrors.Count==0 && ClassicErrorReport.Summary(noErrors,1).Contains("No errors"),"Clean subtitles get an explicit no-errors result");Check(ClassicErrorReport.Analyze(new Subtitle(),new SubRip()).Count==0,"Empty subtitle produces an empty report");
  var injected=errors.Take(3).ToList();injected[0].Cue.Text="=SUM(1,2) <script>alert(1)</script> & Ж 😀\tquote\"\nnext\u0001";var tsv=ClassicErrorReport.Tsv(injected,false);Check(tsv.Split(new[]{Environment.NewLine},StringSplitOptions.None).All(line=>line.Split('\t').Length==6),"Clipboard TSV has exactly six columns even with tabs/newlines in text");var html=ClassicErrorReport.Html(injected,"summary <test>","a & b.srt",false);Check(!html.Contains("<script>") && html.Contains("&lt;script&gt;") && html.Contains("&amp;"),"HTML export escapes subtitle text, filenames and summary");Check(html.Contains("input type=radio") && html.Contains("checked~table"),"Standalone HTML preserves category filtering without script or remote resources");
  var xlsx=ClassicErrorReport.Xlsx(injected,false);File.WriteAllBytes(Path.Combine(root,"errors.xlsx"),xlsx);using(var zip=new ZipArchive(new MemoryStream(xlsx),ZipArchiveMode.Read)) {Check(zip.Entries.Count==6,"Excel export contains a real six-part OOXML workbook");foreach(var entry in zip.Entries)using(var stream=entry.Open())XDocument.Load(stream);using(var stream=zip.GetEntry("xl/worksheets/sheet1.xml").Open()){var xml=XDocument.Load(stream);XNamespace n="http://schemas.openxmlformats.org/spreadsheetml/2006/main";Check(xml.Descendants(n+"row").Count()==4,"Excel contains header plus exactly the visible error rows");Check(!xml.Descendants(n+"f").Any() && xml.Descendants(n+"t").Any(e=>e.Value.Contains("=SUM")),"Subtitle text stays literal and is never interpreted as an Excel formula");Check(xml.Descendants(n+"v").First().Value==injected[0].Number.ToString(),"Cue numbers are exported as numeric cells");}}
  var csv=ClassicErrorReport.Csv(injected);Check(csv.StartsWith("FileName,LineNumber,Text,Error") && csv.Contains("\\nnext") && csv.Contains("quote\"\""),"Batch CSV quotes fields and preserves embedded quotes and physical line breaks");
  foreach(var lang in new[]{"en-US","ru-RU"})
  {
   Configuration.Settings.General.Language=lang;LanguageSettings.Current=lang=="en-US"?new Language():Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages",lang+".xml"));
   foreach(bool batch in new[]{false,true})using(var form=new ClassicErrorList(errors,3,"a.srt",batch))
   {
    Show(form);form.Size=form.MinimumSize;await Task.Delay(25);var layout=form.Controls.OfType<TableLayoutPanel>().Single();var footer=layout.GetControlFromPosition(0,3);var cards=(FlowLayoutPanel)Field(form,"_cards");Check(footer.Bottom<=form.ClientSize.Height && footer.Controls.Cast<Control>().All(c=>c.Right<=footer.ClientSize.Width),"Error-list footer fits minimum size: "+lang+" "+batch);Check(cards.Controls.Cast<Control>().All(c=>c.Right<=cards.ClientSize.Width && c.Bottom<=cards.ClientSize.Height),"Category cards wrap without clipping: "+lang+" "+batch);
    form.Filter(ClassicLineErrorType.Overlap);Check(form.VisibleErrors.Count==2 && form.VisibleErrors.All(r=>r.Type==ClassicLineErrorType.Overlap),"Category filter retains exactly its own rows: "+lang+" "+batch);var file=Path.Combine(root,"filtered-"+lang+"-"+batch+".xlsx");form.SaveReport(file,"xlsx");using(var zip=ZipFile.OpenRead(file))using(var stream=zip.GetEntry("xl/worksheets/sheet1.xml").Open()){XNamespace n="http://schemas.openxmlformats.org/spreadsheetml/2006/main";Check(XDocument.Load(stream).Descendants(n+"row").Count()==3,"Export honours the active category filter: "+lang+" "+batch);}
    form.Filter(ClassicLineErrorType.LineTooWide);Check(form.VisibleErrors.Count==0 && !((Button)Field(form,"_export")).Enabled,"Empty filter disables export: "+lang+" "+batch);form.Filter(null);Check(form.VisibleErrors.Count==errors.Count,"All restores the complete report: "+lang+" "+batch);
    using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(root,"errors-"+lang+"-"+batch+".png"));}
    if(!batch){Call(form,"GoToSelected");Check(form.SelectedError!=null && form.DialogResult==DialogResult.OK,"Initial selected row supports immediate Go to: "+lang);}else Check(!((Button)Field(form,"_go")).Visible,"Batch report does not navigate into an unrelated open document: "+lang);
   }
   using(var main=new MainForm())
   {
    var current=Sub("first", "second", "third");Set(main,"_subtitle",current);Set(main,"_subtitleOriginal",new Subtitle());var grid=(SubtitleListView)Field(main,"SubtitleListview1");grid.GetType().GetMethod("Fill",F,null,new[]{typeof(Subtitle),typeof(Subtitle)},null).Invoke(grid,new object[]{current,new Subtitle()});var handle=grid.Handle;grid.SelectIndexAndEnsureVisible(0);var row=new ClassicLineError {Number=2,Cue=new Paragraph(current.Paragraphs[1],false),Type=ClassicLineErrorType.TooShort};Check((bool)Call(main,"NavigateClassicError",row) && grid.SelectedIndex==1,"Go to uses the exact source cue in the real editor: "+lang);current.Paragraphs[1].Text="changed";Check(!(bool)Call(main,"NavigateClassicError",row),"Stale report row cannot jump to a changed cue: "+lang);
    Call(main,"SetShortcuts");Check(((ToolStripMenuItem)Field(main,"listErrorsToolStripMenuItem")).ShortcutKeys==(Keys.Control|Keys.F8),"The existing configurable Ctrl+F8 command is retained: "+lang);
   }
  }
  Configuration.Settings.General.Language="en-US";LanguageSettings.Current=new Language();Defaults();var input=Path.Combine(root,"input");var output=Path.Combine(root,"output");Directory.CreateDirectory(input);Directory.CreateDirectory(output);
  using(var batch=new BatchConvert(SystemIcons.Application))
  {
   Show(batch);await Task.Delay(20);foreach(ListViewItem item in ((ListView)Field(batch,"listViewConvertOptions")).Items)item.Checked=false;((RadioButton)Field(batch,"radioButtonSaveInOutputFolder")).Checked=true;((TextBox)Field(batch,"textBoxOutputFolder")).Text=output;
   for(int i=0;i<2;i++){string file=Path.Combine(input,"sample"+i+".srt");var sub=Sub("Too fast and very short");sub.Paragraphs[0].EndTime.TotalMilliseconds=1200;File.WriteAllText(file,sub.ToText(new SubRip()));Call(batch,"AddInputFile",file);}Call(batch,"buttonConvert_Click",null,EventArgs.Empty);
   var rows=(List<ClassicLineError>)Field(batch,"_classicBatchErrors");Check(Directory.GetFiles(output,"*.srt").Length==2 && rows.Count>=4,"Real batch conversion records errors for both saved output files");Check(rows.Select(r=>r.FileName).Distinct().Count()==2 && rows.All(r=>!string.IsNullOrEmpty(r.FileName)),"Batch report retains independent file names for each output");Check((int)Field(batch,"_classicBatchErrorLines")==2 && ((Button)Field(batch,"_classicListErrorsButton")).Enabled,"Batch report counts every converted cue and is enabled after completion");var reportButton=(Button)Field(batch,"_classicListErrorsButton");var convert=(Button)Field(batch,"buttonConvert");var progress=(ProgressBar)Field(batch,"progressBar1");Check(reportButton.Right<convert.Left && progress.Right<reportButton.Left,"Batch error button does not overlap Convert or the progress bar");Call(batch,"BeginClassicBatchErrors");Check(rows.Count==0 && (int)Field(batch,"_classicBatchErrorLines")==0 && !reportButton.Enabled,"Starting another run clears old report data and disables the report while converting");
  }
  File.WriteAllText(Path.Combine(root,"validation.json"),JsonConvert.SerializeObject(new {success=true,version=typeof(MainForm).Assembly.GetName().Version.ToString(),paidRequests=0}));
 }
}
