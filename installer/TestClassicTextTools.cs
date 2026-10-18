using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.CommandLineConvert;
using MainForm=Nikse.SubtitleEdit.Forms.Main;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicTextTools
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object Call(object o,string n,params object[] a){try{return o.GetType().GetMethod(n,F).Invoke(o,a);}catch(TargetInvocationException ex){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();return null;}}
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static string Snapshot(Subtitle s)=>JsonConvert.SerializeObject(s);
 static Subtitle Sub(params string[] lines){var s=new Subtitle {Header="Preserved ASS header",OriginalFormat=new SubRip()};for(int i=0;i<lines.Length;i++)s.Paragraphs.Add(new Paragraph(lines[i],1000+i*1200,2100+i*1200){Number=i+1,Actor="Speaker",Extra="Default",Layer=2,MarginL="13"});return s;}
 static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();}
 [STAThread]static int Main(string[] args)
 {
  var root=Path.Combine(Path.GetFullPath(args[0]),"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));Console.WriteLine("TEST DATA: "+root);
  typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();int exit=0;
  using(var anchor=new Form {ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),Size=new Size(1,1)}){anchor.Shown+=async(s,e)=>{try{await Unit(root);}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{anchor.Close();}};Application.Run(anchor);}return exit;
 }
 static async Task Unit(string root)
 {
  Configuration.Settings.General.SubtitleLineMaximumLength=42;LanguageSettings.Current=new Language();
  var source=Sub("An unfinished phrase", "continues here", "and ends.");var baseline=Snapshot(source);var merges=ClassicTextTools.DetectMerges(source,"en",500,84);
  Check(merges.Count==2,"Consecutive unfinished phrases produce both candidate boundaries");var merged=ClassicTextTools.Merge(source,new HashSet<int>(merges.Select(c=>c.Index)),"en");
  Check(merged.Paragraphs.Count==1 && Utilities.UnbreakLine(merged.Paragraphs[0].Text)=="An unfinished phrase continues here and ends.","Selecting a chain combines every word once in source order");
  Check(merged.Paragraphs[0].StartTime.TotalMilliseconds==1000 && merged.Paragraphs[0].EndTime.TotalMilliseconds==4500,"A merged chain spans the first start and final end");Check(merged.Header==source.Header && merged.Paragraphs[0].Id==source.Paragraphs[0].Id && merged.Paragraphs[0].Layer==2 && merged.Paragraphs[0].MarginL=="13","Merge retains header, identity and ASS metadata");Check(Snapshot(source)==baseline,"Detection and application previews leave the source untouched");
  Check(ClassicTextTools.Merge(source,new HashSet<int>{1},"en").Paragraphs.Count==2,"An unchecked boundary remains separate");
  foreach(var ending in new[]{"Done.","Stop!", "Really?", "She said \"Go.\"", "完了！", "終わり．", "Done｡", "Music♪"})Check(ClassicTextTools.DetectMerges(Sub(ending,"Next"),"en",500,84).Count==0,"Finished sentence/music is not merged: "+ending);
  foreach(var ending in new[]{"Wait...", "Wait…", "123", "unfinished,", ""})Check(ClassicTextTools.DetectMerges(Sub(ending,"continues"),"en",500,84).Count==1,"Ellipsis, digits and unfinished text continue: "+ending);
  foreach(var dash in new[]{"-", "‐", "–", "—"})Check(ClassicTextTools.DetectMerges(Sub("unfinished",dash+" speaker"),"en",500,84).Count==0,"Dialog speaker boundary is protected: "+dash);
  var limit=Sub("unfinished","next");limit.Paragraphs[1].StartTime.TotalMilliseconds=2600;Check(!ClassicTextTools.CanMerge(limit.Paragraphs[0],limit.Paragraphs[1],"en",500,84),"Gap equal to the maximum is excluded");limit.Paragraphs[1].StartTime.TotalMilliseconds=2099;Check(!ClassicTextTools.CanMerge(limit.Paragraphs[0],limit.Paragraphs[1],"en",500,84),"Overlapping cues remain independent");limit=Sub("unfinished","next");Check(!ClassicTextTools.CanMerge(limit.Paragraphs[0],limit.Paragraphs[1],"en",500,14),"Combined visible text at the character limit is excluded");limit.Paragraphs[1].Actor="Other";Check(ClassicTextTools.DetectMerges(limit,"en",500,84).Count==0,"Different named actors are not merged");limit.Paragraphs[1].Actor="Speaker";limit.Paragraphs[1].Extra="Other style";Check(ClassicTextTools.DetectMerges(limit,"en",500,84).Count==0,"Different ASS styles are not merged");
  var settings=new ClassicTextToolSettings {MaxGap=321,MaxCharacters=123,Actors=new ClassicActorOptions {From=0,To=3,OnlyNames=false}};settings.Replacements["漢"]="Kan";settings.Replacements["Ω"]="O";settings.Replacements["Ж"]="Zh";settings.Save();Check(ClassicTextToolSettings.Load().MaxGap==321 && ClassicTextToolSettings.Load().Actors.To==3,"Tool and actor options persist");
  var unicode=Sub("café © Ж😀😀 <font face=\"Ω\">Hello…</font>\u200f", "Ж 😀");var chars=ClassicTextTools.UnicodeCharacters(unicode,settings.Replacements);
  Check(chars.All(c=>c.CodePoint>255) && !chars.Any(c=>c.Character=="é" || c.Character=="©"),"Latin-1 text is excluded from the Unicode replacement list");var emoji=chars.Single(c=>c.Character=="😀");Check(emoji.Count==3 && emoji.Lines.SequenceEqual(new[]{1,2}) && emoji.CodePoint==0x1F600,"An emoji counts as one scalar with distinct cue references");Check(chars.Select(c=>c.CodePoint).SequenceEqual(chars.Select(c=>c.CodePoint).OrderBy(x=>x)),"Unicode entries are sorted by scalar code point");Check(chars.Single(c=>c.Character=="Ж").Replacement=="Zh","Saved replacement appears in the next scan");
  var replaced=ClassicTextTools.ReplaceUnicode(unicode,new Dictionary<string,string>{{"😀",""},{"Ω","O"},{"…","..."},{"\u200f",""}});Check(replaced.Paragraphs[0].Text=="café © Ж <font face=\"O\">Hello...</font>" && replaced.Paragraphs[1].Text=="Ж ","Only selected mappings replace literal characters, including inside tags");Check(replaced.Header==unicode.Header && replaced.Paragraphs[0].Id==unicode.Paragraphs[0].Id,"Unicode replacement retains subtitle metadata and cue identity");
  var cascading=ClassicTextTools.ReplaceUnicode(Sub("ЖΩ"),new Dictionary<string,string>{{"Ж","Ω"},{"Ω","O"}});Check(cascading.Paragraphs[0].Text=="ΩO","Replacement text is not accidentally replaced a second time");Check(ClassicTextTools.ReplaceUnicode(Sub("\uD800x😀"),new Dictionary<string,string>{{"😀","yes"}}).Paragraphs[0].Text=="\uD800xyes","Malformed UTF-16 does not lose neighbouring text or crash");
  var actorSource=Sub("[Alice] Hello"+Environment.NewLine+"[Bob] Goodbye");var actors=ClassicTextTools.ConvertActors(actorSource,new AdvancedSubStationAlpha(),settings.Actors);Check(actors.Paragraphs.Count==2 && actors.Paragraphs[0].Actor=="Alice" && actors.Paragraphs[1].Actor=="Bob" && actors.Paragraphs[0].Text=="Hello" && actors.Paragraphs[1].Text=="Goodbye","Two bracketed speakers split into separate Actor cues");Check(actors.Paragraphs[0].Id==actorSource.Paragraphs[0].Id && actors.Paragraphs[1].Id!=actors.Paragraphs[0].Id && actors.Paragraphs[1].MarginL=="13","Speaker split preserves the first ID and copies ASS metadata to a new second ID");
  settings.Actors.From=2;var colon=ClassicTextTools.ConvertActors(Sub("   Alice: Hello"+Environment.NewLine+"Bob: Goodbye"+Environment.NewLine+"still Bob"),new SubRip(),settings.Actors);Check(colon.Paragraphs.Count==2 && colon.Paragraphs[0].Actor=="Alice" && colon.Paragraphs[1].Actor=="Bob" && colon.Paragraphs[1].Text=="Goodbye"+Environment.NewLine+"still Bob","Colon labels use trimmed indices and keep follow-up text with the right speaker");
  Check(ClassicTextTools.ConvertActors(Sub("Alice: A"+Environment.NewLine+"Bob: B"+Environment.NewLine+"Carol: C"),new SubRip(),settings.Actors).Paragraphs.Count==1,"Three colon speakers are left intact rather than losing a speaker");
  settings.Actors.From=1;var broken=ClassicTextTools.ConvertActors(Sub("(Speaker"+Environment.NewLine+"2) Hello"),new SubRip(),settings.Actors);Check(broken.Paragraphs[0].Actor=="Speaker 2" && broken.Paragraphs[0].Text=="Hello","A bracketed speaker broken across a line is reconstructed");
  settings.Actors.From=0;var malformed=ClassicTextTools.ConvertActors(Sub("] before [broken"+Environment.NewLine+"keep this line"),new SubRip(),settings.Actors);Check(malformed.Paragraphs[0].Text.Contains("keep this line"),"Reversed brackets do not drop following text");
  settings.Actors.From=3;settings.Actors.To=0;settings.Actors.Casing=1;settings.Actors.UseColor=true;settings.Actors.ColorArgb=Color.Red.ToArgb();var moved=ClassicTextTools.ConvertActors(actors,new SubRip(),settings.Actors);Check(moved.Paragraphs[0].Actor=="" && moved.Paragraphs[0].Text.Contains("[ALICE]") && moved.Paragraphs[0].Text.Contains("<font"),"Moving Actor into text applies casing/HTML colour and clears the metadata to avoid duplicates");Check(ClassicTextTools.ConvertActors(actors,new AdvancedSubStationAlpha(),settings.Actors).Paragraphs[0].Text.Contains("{\\c"),"ASS actor colours use ASS overrides");
  settings.Actors=new ClassicActorOptions {From=0,To=3,OnlyNames=true};Check(ClassicTextTools.ConvertActors(Sub("[noise123] Hello"),new SubRip(),settings.Actors).Paragraphs[0].Text=="[noise123] Hello","Only-names batch mode leaves non-name labels intact");
  foreach(var lang in new[]{"en-US","ru-RU"})
  {
   Configuration.Settings.General.Language=lang;LanguageSettings.Current=lang=="en-US"?new Language():Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages",lang+".xml"));settings.Save();
   foreach(bool isUnicode in new[]{false,true})using(var form=new ClassicTextToolsForm(isUnicode?unicode:source,isUnicode))
   {
    Show(form);form.Size=form.MinimumSize;await Task.Delay(20);var grid=(DataGridView)Field(form,"_grid");var preview=(DataGridView)Field(form,"_preview");var footer=form.Controls.OfType<TableLayoutPanel>().Single().GetControlFromPosition(0,4);Check(footer.Bottom<=form.ClientSize.Height && footer.Controls.Cast<Control>().All(c=>c.Right<=footer.ClientSize.Width),"Tool footer fits the minimum window: "+lang+" "+isUnicode);
    if(isUnicode)
    {
     Call(form,"Toggle",true);Check(form.Result.Paragraphs[0].Text==unicode.Paragraphs[0].Text,"Invert selection deselects every character without changing source text: "+lang);
     int row=chars.FindIndex(c=>c.Character=="😀");grid.Rows[row].Cells[0].Value=true;grid.Rows[row].Cells[4].Value="emoji";Check(form.Result.Paragraphs[0].Text.Contains("emojiemoji"),"Unicode edit updates the exact result preview: "+lang);
     Call(form,"Apply");var saved=ClassicTextToolSettings.Load();Check(saved.Replacements["😀"]=="emoji" && saved.Replacements["漢"]=="Kan","OK remembers replacements and keeps mappings absent from the file: "+lang);
    }
    else { grid.Rows[0].Cells[0].Value=false;Check(form.Result.Paragraphs.Count==2 && preview.Rows.Count==2,"Unchecking a candidate updates the complete resulting cue list: "+lang); }
    using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(root,"text-tool-"+lang+"-"+isUnicode+".png"));}
   }
   settings.Actors=new ClassicActorOptions {From=0,To=3,OnlyNames=true};settings.Save();
   using(var form=new ConvertActor())
   {
    form.Initialize(Sub("[noise123] Hello"),new SubRip());Show(form);await Task.Delay(10);var fixes=(ListView)Field(form,"listViewFixes");Check(fixes.Items.Count==1 && !fixes.Items[0].Checked,"Non-name candidate starts unchecked in the real actor dialog: "+lang);Call(form,"ButtonOkClick",null,EventArgs.Empty);Check(form.Subtitle.Paragraphs[0].Text=="[noise123] Hello","Applying without checking the non-name candidate preserves its source text: "+lang);
   }
   settings.Actors=new ClassicActorOptions {From=0,To=3};settings.Save();using(var form=new ConvertActor())
   {
    var sample=Sub("[Alice] Hello"+Environment.NewLine+"[Bob] Goodbye", "[Carol] untouched");form.SetSelectedIndices(new[]{0});form.Initialize(sample,new SubRip());Show(form);await Task.Delay(20);((CheckBox)Field(form,"_onlySelected")).Checked=true;var fixes=(ListView)Field(form,"listViewFixes");Check(fixes.Items.Count==1,"Selected-only actor conversion excludes other source cues: "+lang);fixes.Items[0].Checked=false;Call(form,"ButtonOkClick",null,EventArgs.Empty);Check(form.Subtitle.Paragraphs.Count==2 && form.Subtitle.Paragraphs[0].Text==sample.Paragraphs[0].Text,"Unchecked two-speaker split is atomic and leaves all source words intact: "+lang);
   }
   using(var main=new MainForm())
   {
    var current=new Subtitle(source,false);var original=new Subtitle();Set(main,"_subtitle",current);Set(main,"_subtitleOriginal",original);var grid=(SubtitleListView)Field(main,"SubtitleListview1");grid.GetType().GetMethod("Fill",F,null,new[]{typeof(Subtitle),typeof(Subtitle)},null).Invoke(grid,new object[]{current,original});var handle=grid.Handle;grid.SelectIndexAndEnsureVisible(0);
    Check((bool)Call(main,"ApplyClassicTextTools",new Subtitle(current,false),merged,false),"Real editor applies the complete preview snapshot: "+lang);Check(((Subtitle)Field(main,"_subtitle")).Paragraphs.Count==1,"Merged cues reach the editor: "+lang);Call(main,"UndoToIndex",true);Check(((Subtitle)Field(main,"_subtitle")).Paragraphs.Count==3,"One ordinary Undo restores all merged cues: "+lang);
    var stale=new Subtitle((Subtitle)Field(main,"_subtitle"),false);((Subtitle)Field(main,"_subtitle")).Paragraphs[0].Text="Changed";Check(!(bool)Call(main,"ApplyClassicTextTools",stale,merged,false),"A stale preview cannot overwrite later edits: "+lang);
   }
  }
  Configuration.Settings.General.Language="en-US";LanguageSettings.Current=new Language();settings=new ClassicTextToolSettings {MaxGap=500,MaxCharacters=84,Actors=new ClassicActorOptions {From=0,To=3}};settings.Save();
  var input=Path.Combine(root,"input");var output=Path.Combine(root,"output");Directory.CreateDirectory(input);Directory.CreateDirectory(output);
  using(var batch=new BatchConvert(SystemIcons.Application))
  {
   Show(batch);var list=(ListView)Field(batch,"listViewConvertOptions");var actions=new[]{CommandLineConverter.BatchAction.MergeContinuationLines,CommandLineConverter.BatchAction.ConvertActors,CommandLineConverter.BatchAction.RemoveUnicodeControlChars};foreach(ListViewItem item in list.Items)item.Checked=actions.Contains(((BatchConvert.FixActionItem)item.Tag).Action);
   Check(actions.All(a=>list.Items.Cast<ListViewItem>().Any(i=>((BatchConvert.FixActionItem)i.Tag).Action==a)),"All three independent functions are available in batch conversion");Check(!(bool)Call(batch,"AllowImageToImage"),"Text functions force bitmap inputs through text recognition");
   var sub=Sub("[Alice] An unfinished phrase\u200f", "[Alice] continues here.");for(int i=0;i<2;i++){string file=Path.Combine(input,"sample"+i+".srt");File.WriteAllText(file,sub.ToText(new SubRip()));Call(batch,"AddInputFile",file);}
   ((RadioButton)Field(batch,"radioButtonSaveInOutputFolder")).Checked=true;((TextBox)Field(batch,"textBoxOutputFolder")).Text=output;Call(batch,"buttonConvert_Click",null,EventArgs.Empty);
   var files=Directory.GetFiles(output,"*.srt");Check(files.Length==2,"Real batch Convert writes both queued output files");foreach(var file in files){var result=new Subtitle();result.LoadSubtitle(file,out var encoding,null);Check(result.Paragraphs.Count==1 && Utilities.UnbreakLine(result.Paragraphs[0].Text)=="An unfinished phrase continues here.","Saved batch file contains actor conversion, continuation merge and control removal");}
   Check(ClassicTextToolSettings.Load().BatchMerge && ClassicTextToolSettings.Load().BatchActors && ClassicTextToolSettings.Load().BatchControls,"Enabled batch actions persist independently");
   foreach(var action in actions){var item=list.Items.Cast<ListViewItem>().Single(i=>((BatchConvert.FixActionItem)i.Tag).Action==action);list.SelectedItems.Clear();item.Selected=true;Call(batch,"listViewConvertOptions_SelectedIndexChanged",null,EventArgs.Empty);await Task.Delay(10);var panel=((BatchConvert.FixActionItem)item.Tag).Control;Check(panel.Visible && panel.Right<=panel.Parent.ClientSize.Width && panel.Bottom<=panel.Parent.ClientSize.Height,"Batch function settings fit their containing panel: "+action);}
   using(var image=new Bitmap(batch.Width,batch.Height)){batch.DrawToBitmap(image,new Rectangle(Point.Empty,batch.Size));image.Save(Path.Combine(root,"batch-text-tools.png"));}
  }
  File.WriteAllText(Path.Combine(root,"validation.json"),JsonConvert.SerializeObject(new {success=true,version=typeof(MainForm).Assembly.GetName().Version.ToString(),paidRequests=0,outputFiles=Directory.GetFiles(output)}));
 }
}
