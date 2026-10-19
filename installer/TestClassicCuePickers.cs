using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicCuePickers
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;
    static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Set(object o,string n,object value)=>o.GetType().GetField(n,F).SetValue(o,value);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethods(F).Single(m=>m.Name==n && m.GetParameters().Length==args.Length && m.GetParameters().Select((p,i)=>args[i]==null || (p.ParameterType.IsByRef?p.ParameterType.GetElementType():p.ParameterType).IsInstanceOfType(args[i])).All(v=>v)).Invoke(o,args);
    static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
    static void Show(Form f){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.ShowInTaskbar=false;f.Show();}
    static bool Key(Form form,Keys key){var msg=Message.Create(form.Handle,0x100,IntPtr.Zero,IntPtr.Zero);return (bool)typeof(ClassicCuePicker).GetMethod("ProcessCmdKey",F).Invoke(form,new object[]{msg,key});}
    static Subtitle Fixture(bool ssa=false)
    {
        var s=new Subtitle{Header=ssa?SubStationAlpha.DefaultHeader:AdvancedSubStationAlpha.DefaultHeader};
        s.Header=ClassicCuePickerData.AddStyle(s.Header,"Titles",ssa);
        for(var i=0;i<12;i++)s.Paragraphs.Add(new Paragraph("<i>Cue "+i+"</i>",i*2000+1000,i*2000+2500){Actor="Actor"+i.ToString("00"),Extra=i==1?"Titles":"Default",Layer=i,Forced=i==0});s.Renumber();return s;
    }
    [STAThread]static int Main(string[] args)
    {
        var root=Path.Combine(Path.GetFullPath(args[0]),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        typeof(Configuration).GetField("DataDirectory").SetValue(null,root+Path.DirectorySeparatorChar);typeof(Configuration).GetField("SettingsFileName").SetValue(null,Path.Combine(root,"Settings.xml"));
        Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);var exit=0;
        using(var anchor=new Form{ShowInTaskbar=false,Location=new Point(-30000,-30000),StartPosition=FormStartPosition.Manual})
        {anchor.Shown+=async(s,e)=>{try{await Run(root);}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{anchor.Close();}};Application.Run(anchor);}Console.WriteLine("FEATURE CHECKS: "+checks);return exit;
    }
    static async Task Run(string root)
    {
        LanguageSettings.Current=new Language();
        var sub=Fixture();var order=new ClassicActorOrder();var names=order.Get(sub);Check(names.Count==12 && names[0]=="Actor00","Actors begin in alphabetical order");
        var reordered=names.ToList();reordered.Reverse();order.Set(sub,reordered);Check(order.Get(sub).SequenceEqual(reordered),"Session actor order can be changed independently of cue order");
        sub.Paragraphs[0].Actor="Aaron";Check(order.Get(sub).Last()=="Aaron" && order.Get(sub)[0]=="Actor11","New actor is appended rather than renumbering existing shortcuts");
        Check(order.Get(sub).Contains("Actor00"),"An unused actor retains its shortcut position in the session");
        Check(order.Get(Fixture()).First()=="Actor00","A new document resets alphabetical actor order");
        Check(!ClassicCuePickerData.ValidName("Bad,Name") && !ClassicCuePickerData.ValidName("Bad\nName"),"Names cannot corrupt ASS dialogue/style records");
        var styles=ClassicCuePickerData.Styles(sub,new[]{"Default"},i=>"Key"+i,false);
        Check(styles.Select(i=>i.Name).SequenceEqual(new[]{"Default","Titles"}),"Style numbering follows header order");
        Check(styles[0].Count==11 && styles[1].Count==1 && styles[0].Current,"Style picker counts and current markers come from the complete document");
        Check(!ClassicCuePickerData.Filter(styles,"default",true).Any(i=>i.IsNew),"Style creation rejects case-insensitive duplicates");
        Check(ClassicCuePickerData.Filter(styles,"New style",true).Single().IsNew,"Unmatched valid name creates a new-style row");
        Check(ClassicCuePickerData.Filter(styles,"Tit",true)[0].Name=="Titles","Search offers matching styles before the create row");
        foreach(var ssa in new[]{false,true})
        {
            var f=Fixture(ssa);f.Header=ClassicCuePickerData.AddStyle(f.Header,"Русский стиль",ssa);var format=ssa?(SubtitleFormat)new SubStationAlpha():new AdvancedSubStationAlpha();var text=format.ToText(f,"test");var read=new Subtitle();format.LoadSubtitle(read,text.SplitToLines(),"test");
            Check(AdvancedSubStationAlpha.GetStylesFromHeader(read.Header).Contains("Русский стиль"),"New style survives real format serialization: SSA="+ssa);
            Check(read.Paragraphs.Count==12 && read.Paragraphs[0].Actor=="Actor00","Style creation preserves all cues and actors: SSA="+ssa);
        }
        foreach(var language in new[]{"ru-RU","en-US"})
        {
            Configuration.Settings.General.Language=language;
            using(var main=new Nikse.SubtitleEdit.Forms.Main())
            {
                Show(main);await Task.Delay(140);var grid=(SubtitleListView)Field(main,"SubtitleListview1");var source=Fixture();Set(main,"_subtitle",source);Call(main,"SetCurrentFormat",new AdvancedSubStationAlpha().FriendlyName);source=Fixture();Set(main,"_subtitle",source);Set(main,"_undoIndex",-1);Call(grid,"Fill",source,null);grid.SelectIndexAndEnsureVisible(0);grid.Items[1].Selected=true;
                var original=new Subtitle(source,false);Set(main,"_subtitleOriginal",original);var originalText=(string)Call(main,"AssistedContent",original);
                var snapshot=(string)Call(main,"AssistedContent",source);var header=source.Header;
                Check((bool)Call(main,"ApplyClassicPickerChoice",source,snapshot,header,new[]{0,1},false,"Новый актёр",true),"Actor selection applies to the selected cue set: "+language);
                Check(source.Paragraphs[0].Actor=="Новый актёр" && source.Paragraphs[1].Actor=="Новый актёр" && source.Paragraphs[2].Actor=="Actor02","Unselected actors are retained: "+language);
                Check(source.Paragraphs[0].Text=="<i>Cue 0</i>" && source.Paragraphs[0].Forced && source.Paragraphs[1].Layer==1 && source.Paragraphs[0].StartTime.TotalMilliseconds==1000,"Actor assignment preserves text, timing, layer and forced flags: "+language);
                Check((string)Call(main,"AssistedContent",original)==originalText,"Original reference remains unchanged: "+language);
                Call(main,"UndoToIndex",true);Check(source.Paragraphs[0].Actor=="Actor00" && source.Paragraphs[1].Actor=="Actor01","One editor Undo restores both assigned actors: "+language);
                Call(main,"UndoToIndex",false);Check(source.Paragraphs[0].Actor=="Новый актёр","Actor assignment supports editor Redo: "+language);
                snapshot=(string)Call(main,"AssistedContent",source);header=source.Header;
                Check((bool)Call(main,"ApplyClassicPickerChoice",source,snapshot,header,new[]{0,1},true,"Brand new",true),"Creating and assigning a style is one operation: "+language);
                Check(AdvancedSubStationAlpha.GetStylesFromHeader(source.Header).Contains("Brand new") && source.Paragraphs[0].Extra=="Brand new","New style is stored in the real subtitle header: "+language);
                Call(main,"UndoToIndex",true);Check(source.Paragraphs[0].Extra=="Default" && !AdvancedSubStationAlpha.GetStylesFromHeader(source.Header).Contains("Brand new"),"Undo restores both style assignment and header: "+language);
                snapshot=(string)Call(main,"AssistedContent",source);header=source.Header;source.Paragraphs[0].Text="Changed";
                Check(!(bool)Call(main,"ApplyClassicPickerChoice",source,snapshot,header,new[]{0},false,"Stale",false) && source.Paragraphs[0].Actor!="Stale","Stale picker snapshot cannot overwrite editor changes: "+language);
                grid.SelectIndexAndEnsureVisible(0);grid.Items[1].Selected=true;Call(main,"SetClassicStyleSlot",1);Check(source.Paragraphs[0].Extra=="Titles" && source.Paragraphs[1].Extra=="Titles","Direct style slot sets all selected cues in header order: "+language);
                var session=(ClassicActorOrder)Field(main,"_classicActorOrder");var actorNames=session.Get(source);var reverse=actorNames.AsEnumerable().Reverse().ToList();session.Set(source,reverse);Call(main,"SetActorVoice",9);
                Check(source.Paragraphs[0].Actor==reverse[9],"Legacy actor slot 10 follows the same session order: "+language);
                Call(main,"RemoveActors",main,EventArgs.Empty);Check(source.Paragraphs[0].Actor=="" && source.Paragraphs[1].Actor=="","Remove actor affects only the selection: "+language);Call(main,"UndoToIndex",true);Check(source.Paragraphs[0].Actor==reverse[9],"Remove actor is undoable: "+language);
                Call(main,"PopulateClassicPickerContext");var styleMenu=(ToolStripMenuItem)Field(main,"setStylesForSelectedLinesToolStripMenuItem");var actorMenu=(ToolStripMenuItem)Field(main,"setActorForSelectedLinesToolStripMenuItem");Check(styleMenu.DropDownItems[0].Name=="classicStylePickerContext" && actorMenu.DropDownItems[0].Name=="classicActorPickerContext","Existing ASS context submenus expose both pickers: "+language);
                var keys=new Dictionary<string,int>{{"classicSetStyle2",(int)(Keys.Control|Keys.Alt|Keys.F11)}};Call(main,"SaveClassicCuePickerKeys",keys);var menu=((ToolStripMenuItem[])Field(main,"_styleSlotMenus"))[1];Check(menu.ShortcutKeys==(Keys.Control|Keys.Alt|Keys.F11),"Direct style key assignment is active: "+language);
                foreach(var focusName in new[]{"textBoxListViewText","audioVisualizer"})
                {
                    grid.SelectIndexAndEnsureVisible(2);source.Paragraphs[2].Extra="Default";((Control)Field(main,focusName)).Focus();
                    var message=Message.Create(main.Handle,0x100,IntPtr.Zero,IntPtr.Zero);var handled=(bool)Call(main,"ProcessCmdKeyFromChildForm",message,Keys.Control|Keys.Alt|Keys.F11);
                    Check(handled && source.Paragraphs[2].Extra=="Titles","Native style shortcut works with focus in "+focusName+": "+language);
                }
                Check((bool)Call(main,"RunClassicPaletteShortcut",nameof(MainShortcuts.MainListViewSetActor9)) && source.Paragraphs[2].Actor==reverse[8],"Named actor slot 9 is available through the command dispatcher: "+language);
                Check((bool)Call(main,"RunClassicPaletteShortcut",nameof(MainShortcuts.MainListViewSetActor10)) && source.Paragraphs[2].Actor==reverse[9],"Named actor slot 10 is available through the command dispatcher: "+language);
                var rejected=false;try{Call(main,"SaveClassicCuePickerKeys",new Dictionary<string,int>{{"classicActorPicker",(int)(Keys.Control|Keys.S)}});}catch(TargetInvocationException ex){rejected=ex.InnerException is ArgumentException;}Check(rejected && menu.ShortcutKeys==(Keys.Control|Keys.Alt|Keys.F11),"Conflicting shortcut is rejected without changing saved keys: "+language);
                var palette=(List<ClassicPaletteCommand>)Call(main,"BuildClassicPaletteCommands");Check(palette.Any(c=>c.Id=="menu:classicActorPicker") && palette.Any(c=>c.Id=="menu:classicStylePicker"),"Command palette discovers the new picker commands: "+language);
                Call(main,"SetCurrentFormat",new SubRip().FriendlyName);Call(main,"RefreshClassicCuePickerAvailability");Check(!((ToolStripMenuItem)Field(main,"_actorPickerMenu")).Enabled && !menu.Enabled,"ASS-only commands are disabled for SRT: "+language);
                using(var dialog=new ClassicCuePicker(styles,true,2,null))
                {
                    Show(dialog);dialog.Size=dialog.MinimumSize;var search=(TextBox)Field(dialog,"_filter");var list=(ListView)Field(dialog,"_list");search.Text="Tit";Check(list.Items.Count==2 && ((ClassicCueChoice)list.Items[0].Tag).Name=="Titles","Style search keeps matching entries before create: "+language);
                    using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,dialog.Size));image.Save(Path.Combine(root,"styles-"+language+".png"));}
                    var details=(TextBox)Field(dialog,"_details");Check(details.Text.Contains("Arial") && details.Text.Contains("Primary:"),"Style details expose font and colors: "+language);
                    search.Text="";Key(dialog,Keys.D2);Check(dialog.ResultName=="Titles" && dialog.DialogResult==DialogResult.OK,"Number key selects style 2 with empty search: "+language);
                }
                var actorChoices=ClassicCuePickerData.Actors(source,session.Get(source),null,i=>"Slot "+i);
                using(var dialog=new ClassicCuePicker(actorChoices,false,2,null))
                {
                    Show(dialog);dialog.Size=dialog.MinimumSize;var list=(ListView)Field(dialog,"_list");list.Items[1].Selected=true;var chosen=((ClassicCueChoice)list.Items[1].Tag).Name;Key(dialog,Keys.Alt|Keys.Up);
                    Check(dialog.NamesInOrder[0]==chosen,"Alt+Up reorders actor shortcut positions: "+language);
                    using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,dialog.Size));image.Save(Path.Combine(root,"actors-"+language+".png"));}
                    Key(dialog,Keys.Escape);Check(dialog.ResultName==null && dialog.DialogResult==DialogResult.Cancel,"Escape cancels actor assignment: "+language);
                }
                using(var dialog=new ClassicCuePicker(actorChoices,false,2,null)){Show(dialog);((TextBox)Field(dialog,"_filter")).Text="New actor 7";Check(!Key(dialog,Keys.D7) && dialog.ResultName==null,"Filtered digits remain ordinary typing: "+language);Key(dialog,Keys.Enter);Check(dialog.ResultIsNew && dialog.ResultName=="New actor 7","Enter can create a new actor: "+language);}
                using(var dialog=new ClassicCuePicker(actorChoices,false,2,null)){Show(dialog);Key(dialog,Keys.Delete);Check(dialog.ResultName=="" && dialog.DialogResult==DialogResult.OK,"Delete removes the actor when the filter is empty: "+language);}
            }
            using(var reopened=new Nikse.SubtitleEdit.Forms.Main()){Check(((ToolStripMenuItem[])Field(reopened,"_styleSlotMenus"))[1].ShortcutKeys==(Keys.Control|Keys.Alt|Keys.F11),"Restart restores picker/style shortcut settings: "+language);}
        }
    }
}
