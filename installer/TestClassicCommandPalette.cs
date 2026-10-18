using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Controls;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicCommandPalette
{
 static BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static object Call(object o,string n,params object[] args){try{return o.GetType().GetMethod(n,F).Invoke(o,args);}catch(TargetInvocationException ex){throw ex.InnerException;}}
 static void Check(bool b,string text){if(!b)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();}
 static ClassicPaletteCommand Item(string id,string name,string group="Tools",string shortcut="")=>new ClassicPaletteCommand{Id=id,Name=name,Group=group,Shortcut=shortcut,Execute=()=>true};
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
  var items=new[]{Item("fce","Fix common errors"),Item("bc","Batch convert"),Item("merge","Merge short lines"),Item("save","Save","File","Ctrl+S"),Item("other","Other")};
  Check(ClassicCommandPalette.Filter(items,"short merge").Single().Id=="merge","Search requires all words and accepts reversed order");
  Check(ClassicCommandPalette.Filter(items,"fce").Single().Id=="fce" && ClassicCommandPalette.Filter(items,"bc").Single().Id=="bc","Initials find commands");
  Check(ClassicCommandPalette.Filter(items,"ctrl+s").Single().Id=="save" && ClassicCommandPalette.Filter(items,"File").Single().Id=="save","Search includes shortcut and group");
  Check(ClassicCommandPalette.Filter(items,"unknown xyz").Count==0,"No matches yields an empty result");
  Check(ClassicCommandPalette.Clean("&Save && close…")=="Save & close","Menu mnemonics normalize without losing literal ampersands");
  Check(ClassicCommandPalette.Run(items[4]) && ClassicCommandPalette.Filter(items,"").First().Id=="other","Successful execution moves command to session history");
  var disabled=Item("disabled","Disabled");disabled.Enabled=false;int ran=0;disabled.Execute=()=>{ran++;return true;};Check(!ClassicCommandPalette.Run(disabled) && ran==0,"Disabled commands cannot run");
  for(int i=0;i<12;i++)ClassicCommandPalette.Run(Item("recent"+i,"Recent "+i));var hist=items.Concat(Enumerable.Range(0,12).Select(i=>Item("recent"+i,"Recent "+i))).ToList();Check(ClassicCommandPalette.Filter(hist,"").Take(10).Select(c=>c.Id).SequenceEqual(Enumerable.Range(2,10).Reverse().Select(i=>"recent"+i)),"Recent history retains the last ten actions in order");
  foreach(var culture in new[]{"en-US","ru-RU"})
  {
   Configuration.Settings.General.Language=culture;LanguageSettings.Current=culture=="en-US"?new Language():Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages",culture+".xml"));
   Configuration.Settings.Shortcuts.GeneralGoToNextSubtitle="";Configuration.Settings.Shortcuts.GeneralGoToPrevSubtitle="Control+Alt+F10";
   using(var main=new Nikse.SubtitleEdit.Forms.Main())
   {
    Call(main,"SetShortcuts");
    var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("One",1000,2000){Actor="A"});sub.Paragraphs.Add(new Paragraph("Two",2300,3500){Actor="B"});sub.Paragraphs.Add(new Paragraph("Three",4000,5000));
    main.GetType().GetField("_subtitle",F).SetValue(main,sub);var list=(SubtitleListView)Field(main,"SubtitleListview1");list.GetType().GetMethod("Fill",F,null,new[]{typeof(Subtitle),typeof(Subtitle)},null).Invoke(list,new object[]{sub,new Subtitle()});var handle=list.Handle;list.SelectIndexAndEnsureVisibleFaster(0);
    var commands=(List<ClassicPaletteCommand>)Call(main,"BuildClassicPaletteCommands");Console.WriteLine("CATALOG: "+culture+" "+commands.Count);
    Check(commands.Select(c=>c.Id).Distinct().Count()==commands.Count,"Dynamic menu actions retain distinct IDs for recent history");
    Check(commands.Count>250 && !commands.Any(c=>c.Id=="menu:classicCommandPalette" || c.Id.StartsWith("MainTextBox")),"Main catalog includes menu and shortcut-only commands, excluding itself and text-box-only actions");
    Check(commands.Where(c=>c.Id.StartsWith("MainVideo") || c.Id.StartsWith("Waveform")).All(c=>!c.Enabled),"Media-only named commands are unavailable without loaded media");
    var next=commands.Single(c=>c.Id==nameof(MainShortcuts.MainGeneralGoToNextSubtitle));Check(string.IsNullOrEmpty(next.Shortcut) && ClassicCommandPalette.Run(next) && list.SelectedIndices[0]==1,"Unassigned next-line command dispatches by ID without assigning a key");
    Check(Configuration.Settings.Shortcuts.GeneralGoToNextSubtitle=="" && Field(main,"_paletteAction")==null,"Named execution leaves shortcut configuration and dispatcher state intact");
    var shortcuts=(MainShortcuts)Field(main,"_shortcuts");var keys=shortcuts.MainGeneralGoToPrevSubtitle;Call(main,"MainKeyDown",main,new KeyEventArgs(keys));Check(list.SelectedIndices[0]==0,"Original assigned shortcut continues to execute through MainKeyDown");
    foreach(var pair in new[]{Tuple.Create(Keys.A,Keys.A),Tuple.Create(Keys.A,Keys.B),Tuple.Create(Keys.Control|Keys.S,Keys.Control|Keys.S),Tuple.Create(Keys.None,Keys.None)})Check((bool)Call(main,"ClassicShortcutMatches",pair.Item1,pair.Item2,"action")== (pair.Item1==pair.Item2),"Ordinary key matching remains identical");
    // Verify that selection-wide changes still use the classic undo path.
    foreach(ListViewItem item in list.Items)item.Selected=false;list.Items[0].Selected=true;list.Items[1].Selected=true;
    var merge=commands.Single(c=>c.Id==nameof(MainShortcuts.MainGeneralMergeSelectedLinesAndUnbreak));Check(ClassicCommandPalette.Run(merge) && sub.Paragraphs.Count==2,"Named merge executes existing classic action");
    Call(main,"UndoToIndex",true);var restored=(Subtitle)Field(main,"_subtitle");Check(restored.Paragraphs.Count==3 && restored.Paragraphs[0].Text=="One" && restored.Paragraphs[1].Actor=="B","Classic undo restores cues after palette merge");
    var paletteItem=(ToolStripMenuItem)Field(main,"_paletteMenuItem");Check(paletteItem!=null && paletteItem.ShortcutKeys==(Keys.Control|Keys.Shift|Keys.K),"Edit menu exposes the default palette shortcut");
    Configuration.Settings.Shortcuts.GeneralGoToNextSubtitle="Control+Shift+K";Call(main,"RefreshClassicPaletteShortcut");Check(paletteItem.ShortcutKeys==Keys.None,"User shortcut conflict is preserved instead of being stolen");Configuration.Settings.Shortcuts.GeneralGoToNextSubtitle="";Call(main,"RefreshClassicPaletteShortcut");
    Call(main,"SaveClassicPaletteShortcut",Keys.Control|Keys.Alt|Keys.Shift|Keys.F24);Check(paletteItem.ShortcutKeys==(Keys.Control|Keys.Alt|Keys.Shift|Keys.F24) && (Keys)Call(main,"LoadClassicPaletteShortcut")==paletteItem.ShortcutKeys,"Palette shortcut persists and reloads independently of SE4 settings schema");
    try{Call(main,"SaveClassicPaletteShortcut",Keys.A);throw new Exception("Plain typing key accepted");}catch(ArgumentException){Console.WriteLine("PASS: Plain typing key is rejected for palette shortcut");}
    Configuration.Settings.Shortcuts.GeneralGoToNextSubtitle="Control+Alt+Shift+F24";try{Call(main,"SaveClassicPaletteShortcut",Keys.Control|Keys.Alt|Keys.Shift|Keys.F24);throw new Exception("Conflicting key accepted");}catch(InvalidOperationException){Console.WriteLine("PASS: Saving a conflicting palette shortcut is rejected");}Configuration.Settings.Shortcuts.GeneralGoToNextSubtitle="";
    Call(main,"SaveClassicPaletteShortcut",Keys.None);Check(paletteItem.ShortcutKeys==Keys.None,"Palette can be used from the menu without a keyboard shortcut");Call(main,"SaveClassicPaletteShortcut",Keys.Control|Keys.Shift|Keys.K);
    using(var form=new ClassicCommandPaletteForm(commands))
    {
     Show(form);await Task.Delay(60);var search=(TextBox)Field(form,"_search");var grid=(DataGridView)Field(form,"_list");
     search.Text=next.Name;Check(grid.Rows.Count>0 && form.SelectedCommand!=null,"Localized command search selects a result");
     search.Text="zzzzzzzz-unmatched";Check(grid.Rows.Count==0 && !((Button)Field(form,"_ok")).Enabled,"No-match state disables Run");search.Text="";
     Call(form,"ProcessCmdKey",Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),Keys.PageDown);Check(grid.CurrentRow.Index==10,"PageDown advances ten commands from the search box");
     Call(form,"ProcessCmdKey",Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),Keys.Up);Check(grid.CurrentRow.Index==9,"Up moves selection without changing search");
     using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(0,0,form.Width,form.Height));bmp.Save(Path.Combine(root,"command-palette-"+culture+".png"));}
     form.Size=form.MinimumSize;await Task.Delay(40);Check(form.ClientRectangle.Contains(((Control)Field(form,"_list")).Parent.Bounds),"Palette layout remains inside minimum-size window");
     Call(form,"ProcessCmdKey",Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),Keys.Escape);Check(form.DialogResult==DialogResult.Cancel,"Escape closes without running a command");
    }
   }
  }
 }
}
