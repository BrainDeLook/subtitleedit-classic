using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Enums;
using Nikse.SubtitleEdit.Core.Forms;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using MainForm=Nikse.SubtitleEdit.Forms.Main;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicAssistedEditing
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object Call(object o,string n,params object[] a){try{return o.GetType().GetMethod(n,F).Invoke(o,a);}catch(TargetInvocationException ex){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();return null;}}
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static string Snapshot(Subtitle sub)=>JsonConvert.SerializeObject(sub);
 static Subtitle Sub(params string[] text){var s=new Subtitle {Header="Preserved header"};for(int i=0;i<text.Length;i++)s.Paragraphs.Add(new Paragraph(text[i],1000+i*5000,5900+i*5000){Number=i+1,Actor="Speaker",Extra="Style",Layer=2,MarginL="15"});return s;}
 static string Words(string text)=>Utilities.UnbreakLine(HtmlUtil.RemoveHtmlTags(text,true)).Replace(" ","");
 static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();}
 [STAThread] static int Main(string[] args)
 {
  var root=Path.Combine(Path.GetFullPath(args[0]),"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));Console.WriteLine("TEST DATA: "+root);
  typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();int exit=0;
  using(var anchor=new Form {ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),Size=new Size(1,1)}){anchor.Shown+=async(s,e)=>{try{await Unit(root);}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{anchor.Close();}};Application.Run(anchor);}return exit;
 }
 static async Task Unit(string root)
 {
  Configuration.Settings.General.ContinuationStyle=ContinuationStyle.None;Configuration.Settings.General.SubtitleLineMaximumLength=42;Configuration.Settings.General.MinimumMillisecondsBetweenLines=40;
  var sample=Sub("She said \"Stop.\" He left, then returned to the room.");var before=Snapshot(sample);var splits=ClassicAssistedWorkflow.Split(sample,null,false,0);
  Check(splits.Count>1 && splits.Count<=5,"Split offers up to five punctuation, comma and midpoint alternatives");Check(splits[0].Preview[0].Text.EndsWith("Stop.\""),"A sentence-ending period before a closing quote is ranked first");
  Check(Snapshot(sample)==before,"Split previews cannot change the source subtitle");
  foreach(var c in splits){Check(c.Result.Paragraphs.Count==2 && c.Result.Paragraphs[0].Id==sample.Paragraphs[0].Id && c.Result.Paragraphs[1].Id!=sample.Paragraphs[0].Id,"Split retains first cue identity and allocates a new second cue");Check(Words(c.Result.Paragraphs[0].Text+c.Result.Paragraphs[1].Text)==Words(sample.Paragraphs[0].Text),"Split preview retains every visible source word");Check(c.Result.Paragraphs[0].Actor=="Speaker" && c.Result.Paragraphs[1].Extra=="Style" && c.Result.Header==sample.Header && c.SelectedIndex==1,"Split retains actor, style, header and second-half selection");Check(c.Result.Paragraphs[1].StartTime.TotalMilliseconds-c.Result.Paragraphs[0].EndTime.TotalMilliseconds==40,"Automatic split uses the configured full gap");}
  var dialog=Sub("- First speaker?"+Environment.NewLine+"- Second speaker answers.");var candidates=ClassicAssistedWorkflow.Split(dialog,null,false,0);Check(candidates[0].Title==ClassicAssistedText.Get("SplitAtDialogDash") && !candidates[0].Preview.Any(p=>p.Text.TrimStart().StartsWith("-")),"Dialog split ranks first and removes the per-speaker dashes");
  var decimals=Sub("Version 1.5 on nikse.dk works with this long sentence");var ranked=AssistedSplitCandidateGenerator.Generate(new ClassicAssistedLine(decimals.Paragraphs[0]),"en",new ClassicAssistedSplitManager());Check(!ranked.Any(c=>c.Title==ClassicAssistedText.Get("SplitAtSentenceEnd")),"Decimals and host names are not sentence boundaries");
  var tagged=Sub("<font color=\"red\"><i>First sentence. Second sentence remains.</i></font>");var tags=ClassicAssistedWorkflow.Split(tagged,null,false,0);Check(tags.Count>0 && tags.All(c=>c.Preview.All(p=>p.Text.Contains("<i>") && p.Text.Contains("</i>") && p.Text.Contains("<font") && p.Text.Contains("</font>"))),"HTML font and italic state survive on both split halves");
  var ass=Sub("{\\i1\\c&H00FF00&}First sentence. Second sentence continues.");var assChoice=ClassicAssistedWorkflow.Split(ass,null,false,0)[0];Check(assChoice.Preview[1].Text.Contains("\\i1") && assChoice.Preview[1].Text.Contains("\\c&H00FF00&"),"Combined ASS override blocks propagate to the new half");
  foreach(var text in new[]{"<i><font color=\"red\">First sentence. Second sentence remains.</font></i>","<b><i>First sentence. Second sentence remains.</i></b>"})foreach(var choice in ClassicAssistedWorkflow.Split(Sub(text),null,false,0))foreach(var p in choice.Preview){XDocument.Parse("<root>"+p.Text+"</root>");Check(true,"Nested HTML remains balanced on each split half");}
  Check(ClassicAssistedWorkflow.Split(Sub("Hi"),null,false,0).Count==0,"Very short text produces no split dialog");var tiny=Sub("First sentence. Second sentence.");tiny.Paragraphs[0].EndTime.TotalMilliseconds=1001;Check(ClassicAssistedWorkflow.Split(tiny,null,false,0).Count==0,"Insufficient timing for two positive halves produces no candidate");
  var orig=Sub("Первая фраза."+Environment.NewLine+"Вторая фраза.");var origBefore=Snapshot(orig);var translated=Sub("First sentence. Second sentence.");var bilingual=ClassicAssistedWorkflow.Split(translated,orig,true,0)[0];Check(bilingual.OriginalResult.Paragraphs.Count==2 && Words(string.Join("",bilingual.OriginalResult.Paragraphs.Select(p=>p.Text)))==Words(orig.Paragraphs[0].Text),"Editable original splits at its own language boundary without duplicating text");Check(Snapshot(orig)==origBefore && bilingual.OriginalResult.Paragraphs[0].Id==orig.Paragraphs[0].Id,"Original preview preserves its source and first cue identity");Check(ClassicAssistedWorkflow.Split(translated,orig,false,0)[0].OriginalResult==null,"Read-only original reference is left untouched");
  var moves=Sub("A short unfinished phrase with several", "words continuing before it finishes.");var moveBefore=Snapshot(moves);var choices=ClassicAssistedWorkflow.Move(moves,null,false,0);Check(choices.Count>0 && choices.Count<=6,"Continuation boundary produces a bounded list of move suggestions");
  foreach(var c in choices){var first=c.Result.Paragraphs[0];var second=c.Result.Paragraphs[1];Check(Words(first.Text+second.Text)==Words(moves.Paragraphs[0].Text+moves.Paragraphs[1].Text),"Move preview retains every source word in order");Check(first.StartTime.TotalMilliseconds==1000 && second.EndTime.TotalMilliseconds==10900 && second.StartTime.TotalMilliseconds-first.EndTime.TotalMilliseconds==100,"Cross-cue move preserves outer bounds and the original gap");Check(first.Id==moves.Paragraphs[0].Id && second.Id==moves.Paragraphs[1].Id && first.Layer==2 && second.MarginL=="15","Move retains cue identity and ASS metadata");}
  Check(Snapshot(moves)==moveBefore,"Move previews cannot change source timing or text");Check(ClassicAssistedWorkflow.Move(Sub("Done.","A different sentence."),null,false,0).Count==0,"Completed sentence has no cross-boundary move");Check(ClassicAssistedWorkflow.Move(Sub("unfinished words", "- Another speaker begins."),null,false,0).Count==0,"Words are not moved into a new dialog speaker");Check(ClassicAssistedWorkflow.Move(Sub("An unfinished phrase…", "continues in lower case"),null,false,0).Count>0,"Lowercase continuation after ellipsis offers moves");Check(ClassicAssistedWorkflow.Move(Sub("An unfinished phrase…", "Another sentence begins"),null,false,0).Count==0,"Uppercase sentence after ellipsis does not offer moves");
  var within=Sub("Several words on the first line"+Environment.NewLine+"and more words on the second line");var local=ClassicAssistedWorkflow.Move(within,null,false,0);Check(local.Count==2 && local.All(c=>c.Preview.Count==1 && c.Result.Paragraphs[0].StartTime.TotalMilliseconds==1000 && c.Result.Paragraphs[0].EndTime.TotalMilliseconds==5900),"Both within-cue moves preserve timing");
  var backwards=ClassicAssistedWorkflow.Move(moves,null,false,1);Check(backwards.Count>0 && backwards.All(c=>c.SelectedIndex==1),"Previous-cue alternatives retain the current cue selection");
  var assMove=new ClassicAssistedWordMove("{\\i1\\c&H00FF00&}Several styled words", "{\\i0}continue in the next cue");var assWords=Words(assMove.S1+assMove.S2);for(int i=0;i<5;i++){assMove.MoveWordDown();assMove.MoveWordUp();}Check(Words(assMove.S1+assMove.S2)==assWords,"Repeated ASS word moves retain all words in order");Check(assMove.S1.Length+assMove.S2.Length<500,"ASS override state does not accumulate unbounded duplicate blocks");
  foreach(var lang in new[]{"en-US","ru-RU"})
  {
   Configuration.Settings.General.Language=lang;LanguageSettings.Current=lang=="en-US"?new Language():Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages",lang+".xml"));
   var localized=ClassicAssistedWorkflow.Split(sample,null,false,0);
   Check(lang!="ru-RU" || localized[0].Title.Contains("предложения"),"Suggestion titles follow the current UI language: "+lang);
   using(var form=new ClassicAssistedEditing(sample.Paragraphs[0],localized,true,null))
   {
    Show(form);foreach(var size in new[]{form.Size,form.MinimumSize,new Size(1000,850)}){form.Size=size;await Task.Delay(20);var cards=(Panel)Field(form,"_cards");Check(cards.Controls.Cast<Control>().All(c=>c.Right<=cards.ClientSize.Width),"Suggestion cards fit the scroll viewport: "+lang+" "+size);var footer=form.Controls.OfType<TableLayoutPanel>().Single().GetControlFromPosition(0,2);Check(footer.Bottom<=form.ClientSize.Height && footer.Controls.Cast<Control>().All(c=>c.Right<=footer.ClientSize.Width),"Footer remains visible while cards scroll: "+lang+" "+size);}
    using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(root,"assisted-"+lang+".png"));}
    var msg=Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero);Call(form,"ProcessCmdKey",msg,Keys.D2);Check(form.SelectedChoice==localized[1] && form.DialogResult==DialogResult.OK,"Number key picks the exact preview snapshot: "+lang);
   }
   var crowded=Sub("An earlier phrase that continues", "several words on this line"+Environment.NewLine+"then more words continue", "into a following phrase that finishes.");var crowdedChoices=ClassicAssistedWorkflow.Move(crowded,null,false,1);
   using(var form=new ClassicAssistedEditing(crowded.Paragraphs[1],crowdedChoices,false,null))
   {
    Show(form);form.Size=form.MinimumSize;await Task.Delay(20);var cards=(Panel)Field(form,"_cards");Check(cards.AutoScroll && crowdedChoices.Count>=4 && cards.VerticalScroll.Visible,"Long move lists scroll at the minimum window size: "+lang);Check(!cards.HorizontalScroll.Visible,"Move cards do not require horizontal scrolling: "+lang);cards.AutoScrollPosition=new Point(0,cards.DisplayRectangle.Height);await Task.Delay(20);Check(cards.Controls[cards.Controls.Count-1].Bottom<=cards.ClientSize.Height,"The last move card can be fully scrolled into view: "+lang);using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(root,"assisted-move-min-"+lang+".png"));}var msg=Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero);Call(form,"ProcessCmdKey",msg,Keys.NumPad1);Check(form.SelectedChoice==crowdedChoices[0],"Numpad choice applies the exact move snapshot: "+lang);
   }
   using(var main=new MainForm())
   {
    var current=Sub("First sentence. Second sentence.","Third sentence remains.");var original=new Subtitle();Set(main,"_subtitle",current);Set(main,"_subtitleOriginal",original);var grid=(SubtitleListView)Field(main,"SubtitleListview1");grid.GetType().GetMethod("Fill",F,null,new[]{typeof(Subtitle),typeof(Subtitle)},null).Invoke(grid,new object[]{current,original});var handle=grid.Handle;grid.SelectIndexAndEnsureVisible(0);
    var prepared=ClassicAssistedWorkflow.Split(current,original,false,0)[0];var baseline=new Subtitle(current,false);
    Check((bool)Call(main,"ApplyClassicAssisted",baseline,new Subtitle(original,false),prepared,true),"Real editor accepts a source-matched split snapshot: "+lang);Check(((Subtitle)Field(main,"_subtitle")).Paragraphs[1].Text==prepared.Preview[1].Text && grid.SelectedIndex==1,"Applied text and selection match the preview: "+lang);
    Call(main,"UndoToIndex",true);Check(((Subtitle)Field(main,"_subtitle")).Paragraphs.Count==2 && ((Subtitle)Field(main,"_subtitle")).Paragraphs[0].Text==baseline.Paragraphs[0].Text,"Ordinary editor undo restores the original unsplit cue: "+lang);
    var stale=new Subtitle((Subtitle)Field(main,"_subtitle"),false);((Subtitle)Field(main,"_subtitle")).Paragraphs[0].Text="Changed while dialog was open";Check(!(bool)Call(main,"ApplyClassicAssisted",stale,new Subtitle(original,false),prepared,true),"Stale suggestions are rejected without changing the editor: "+lang);
    Call(main,"SaveClassicAssistedKeys",Keys.Control|Keys.Alt|Keys.F11,Keys.Control|Keys.Alt|Keys.F12);Call(main,"RefreshClassicAssistedKeys");Check(((ToolStripMenuItem)Field(main,"_assistedSplitMenu")).ShortcutKeys==(Keys.Control|Keys.Alt|Keys.F11),"Custom assisted shortcut persists and binds to the real menu: "+lang);
    try{Call(main,"SaveClassicAssistedKeys",Keys.Control|Keys.Alt|Keys.F11,Keys.Control|Keys.Alt|Keys.F11);throw new Exception("Duplicate shortcut accepted");}catch(ArgumentException){Check(true,"Duplicate shortcuts are rejected: "+lang);}
    try{Call(main,"SaveClassicAssistedKeys",(Keys)typeof(MainForm).Assembly.GetType("Nikse.SubtitleEdit.Logic.UiUtil").GetMethod("GetKeys").Invoke(null,new object[]{Configuration.Settings.Shortcuts.GeneralHelp}),Keys.None);throw new Exception("Occupied shortcut accepted");}catch(ArgumentException){Check(true,"Existing shortcuts are protected: "+lang);}
    Call(main,"SaveClassicAssistedKeys",Keys.None,Keys.None);
    var moving=Sub("Several unfinished words leading into", "the following phrase before it ends.");Set(main,"_subtitle",moving);grid.GetType().GetMethod("Fill",F,null,new[]{typeof(Subtitle),typeof(Subtitle)},null).Invoke(grid,new object[]{moving,original});grid.SelectIndexAndEnsureVisible(0);var movingBefore=new Subtitle(moving,false);var moveChoice=ClassicAssistedWorkflow.Move(moving,null,false,0)[0];Check((bool)Call(main,"ApplyClassicAssisted",movingBefore,new Subtitle(original,false),moveChoice,false),"Real editor applies both sides of a move together: "+lang);Check(((Subtitle)Field(main,"_subtitle")).Paragraphs.Select(p=>p.Text).SequenceEqual(moveChoice.Result.Paragraphs.Select(p=>p.Text)),"Applied move text equals its preview snapshot: "+lang);Call(main,"UndoToIndex",true);Check(((Subtitle)Field(main,"_subtitle")).Paragraphs[0].Text==movingBefore.Paragraphs[0].Text && ((Subtitle)Field(main,"_subtitle")).Paragraphs[1].StartTime.TotalMilliseconds==movingBefore.Paragraphs[1].StartTime.TotalMilliseconds,"One ordinary Undo restores both moved text and timing: "+lang);
   }
   using(var main=new MainForm())
   {
    var working=Sub("First sentence. Second sentence.");var editableOriginal=Sub("Первая фраза."+Environment.NewLine+"Вторая фраза.");Set(main,"_subtitle",working);Set(main,"_subtitleOriginal",editableOriginal);Set(main,"_isOriginalActive",true);Configuration.Settings.General.AllowEditOfOriginalSubtitle=true;var grid=(SubtitleListView)Field(main,"SubtitleListview1");grid.GetType().GetMethod("Fill",F,null,new[]{typeof(Subtitle),typeof(Subtitle)},null).Invoke(grid,new object[]{working,editableOriginal});var handle=grid.Handle;grid.SelectIndexAndEnsureVisible(0);var choice=ClassicAssistedWorkflow.Split(working,editableOriginal,true,0)[0];Check((bool)Call(main,"ApplyClassicAssisted",new Subtitle(working,false),new Subtitle(editableOriginal,false),choice,true) && ((Subtitle)Field(main,"_subtitleOriginal")).Paragraphs.Count==2,"Real editor commits editable original together with the split: "+lang);Call(main,"UndoToIndex",true);Check(((Subtitle)Field(main,"_subtitle")).Paragraphs.Count==1 && ((Subtitle)Field(main,"_subtitleOriginal")).Paragraphs.Count==1 && ((Subtitle)Field(main,"_subtitleOriginal")).Paragraphs[0].Text=="Первая фраза."+Environment.NewLine+"Вторая фраза.","One Undo restores both translation and editable original: "+lang);
   }
  }
 }
}
