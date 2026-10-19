using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Settings;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Forms.Assa;
using Nikse.SubtitleEdit.Forms.Styles;
using Nikse.SubtitleEdit.Logic;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicStyleTransfer
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;
    static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Set(object o,string n,object value)=>o.GetType().GetField(n,F).SetValue(o,value);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethods(F).Single(m=>m.Name==n && m.GetParameters().Length==args.Length && m.GetParameters().Select((p,i)=>args[i]==null || p.ParameterType.IsInstanceOfType(args[i])).All(v=>v)).Invoke(o,args);
    static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
    static string Data(object o)=>JsonConvert.SerializeObject(o);
    static ClassicStyleAnswer Answer(ClassicStyleConflict choice,bool all=false)=>new ClassicStyleAnswer{Choice=choice,ForAll=all};
    static void Show(Form form){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.ShowInTaskbar=false;form.Show();}
    static List<SsaStyle> Styles(AssaStyles form)=>(List<SsaStyle>)Field(form,"_currentFileStyles");
    static SsaStyle Style(string name)=>new SsaStyle{Name=name,FontName="Arial",FontSize=23,Primary=Color.Aqua,Secondary=Color.Red,Outline=Color.Navy,Tertiary=Color.Navy,Background=Color.Black,OutlineWidth=2,ShadowWidth=3,MarginLeft=21,MarginRight=22,MarginVertical=23,Alignment="8",Spacing=1,ScaleX=95,ScaleY=105,Angle=5,Bold=true,Italic=true,Underline=true,Strikeout=true};
    static Subtitle Fixture(bool ssa,IEnumerable<SsaStyle> styles)
    {
        var header=ssa?SubStationAlpha.DefaultHeader:AdvancedSubStationAlpha.DefaultHeader;var lines=header.SplitToLines();var output=new List<string>();var added=false;var format=lines.First(l=>l.StartsWith("Format: Name,"));
        foreach(var line in lines)
        {
            if(line.StartsWith("Style:")){if(!added){output.AddRange(styles.Select(s=>ssa?s.ToRawSsa(format):s.ToRawAss(format)));added=true;}}else output.Add(line);
        }
        var sub=new Subtitle{Header=string.Join(Environment.NewLine,output)};sub.Paragraphs.Add(new Paragraph("<i>Hello</i>",1000,3000){Extra="A",Actor="Alice",Layer=4,Forced=true});sub.Paragraphs.Add(new Paragraph("World",4000,6000){Extra="B",Actor="Bob"});sub.Renumber();return sub;
    }
    static void Select(ListView list,params int[] indices){foreach(ListViewItem item in list.Items)item.Selected=false;foreach(var index in indices)list.Items[index].Selected=true;}
    [STAThread]static int Main(string[] args)
    {
        var root=Path.Combine(Path.GetFullPath(args[0]),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);typeof(Configuration).GetField("DataDirectory").SetValue(null,root+Path.DirectorySeparatorChar);typeof(Configuration).GetField("SettingsFileName").SetValue(null,Path.Combine(root,"Settings.xml"));
        Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);var exit=0;
        using(var anchor=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)}){anchor.Shown+=async(s,e)=>{try{await Run(root);}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{anchor.Close();}};Application.Run(anchor);}Console.WriteLine("FEATURE CHECKS: "+checks);Console.WriteLine("ARTIFACTS: "+root);return exit;
    }
    static async Task Run(string root)
    {
        LanguageSettings.Current=new Language();Configuration.Settings.General.DisableVideoAutoLoading=true;Configuration.Settings.General.DisableShowingLoadErrors=true;
        var target=new[]{Style("A"),Style("B"),Style("A_2")};var incoming=new[]{Style("a"),Style("B")};incoming[0].FontSize=71;incoming[1].Spacing=9;var before=Data(target);var sourceBefore=Data(incoming);var prompts=0;
        var overwritten=ClassicStyleTransfer.Plan(incoming,target,(n,m)=>{prompts++;Check(m,"Multiple original conflicts expose the apply-all choice");return Answer(ClassicStyleConflict.Overwrite,true);});
        Check(prompts==1 && overwritten.Styles[0].Name=="A" && overwritten.Styles[0].FontSize==71 && overwritten.Styles[1].Spacing==9,"Overwrite-all preserves target name/casing and list positions");
        Check(Data(target)==before && Data(incoming)==sourceBefore,"Planning never mutates the source or target objects");
        var copies=ClassicStyleTransfer.Plan(incoming,target,(n,m)=>Answer(ClassicStyleConflict.KeepBoth,true));Check(copies.Styles.Select(s=>s.Name).SequenceEqual(new[]{"A","B","A_2","a_3","B_2"}),"Keep both resolves case-insensitive suffix collisions and appends in source order");
        var cancelled=ClassicStyleTransfer.Plan(new[]{Style("New"),Style("A")},target,(n,m)=>Answer(ClassicStyleConflict.Cancel));Check(cancelled==null && Data(target)==before,"Cancelling a later conflict discards the entire planned transfer");
        foreach(var alignment in new[]{"1","2","3","4","5","6","7","8","9"}){var style=Style("A");style.Alignment=alignment;Check(ClassicStyleTransfer.Convert(ClassicStyleTransfer.Convert(style,false,true),true,false).Alignment==alignment,"ASS to SSA to ASS retains position "+alignment);}
        var colors=Style("Colors");colors.Outline=Color.Lime;colors.Tertiary=Color.Red;Check(ClassicStyleTransfer.Convert(colors,false,true).Tertiary.ToArgb()==Color.Lime.ToArgb() && ClassicStyleTransfer.Convert(colors,true,false).Outline.ToArgb()==Color.Red.ToArgb(),"Cross-format transfer maps the native outline color fields");
        Check(ClassicStyleTransfer.Order(5,new[]{1,2},"up").SequenceEqual(new[]{1,2,0,3,4}),"A selected block moves up without reversing itself");
        Check(ClassicStyleTransfer.Order(5,new[]{1,2},"down").SequenceEqual(new[]{0,3,1,2,4}),"A selected block moves down without reversing itself");
        Check(ClassicStyleTransfer.Order(5,new[]{1,3},"top").SequenceEqual(new[]{1,3,0,2,4}),"Disjoint selection moves to top in original order");
        Check(ClassicStyleTransfer.Order(5,new[]{1,3},"bottom").SequenceEqual(new[]{0,2,4,1,3}),"Disjoint selection moves to bottom in original order");
        Check(ClassicStyleTransfer.Order(3,new[]{0,1},"up").SequenceEqual(new[]{0,1,2}) && ClassicStyleTransfer.Order(3,new[]{1,2},"down").SequenceEqual(new[]{0,1,2}),"Boundary blocks remain stable");
        foreach(var language in new[]{"ru-RU","en-US"})
        {
            Configuration.Settings.General.Language=language;
            using(var dialog=new ClassicStyleConflictDialog(new string('A',120),true))
            {Show(dialog);dialog.Size=dialog.MinimumSize;Check(((CheckBox)Field(dialog,"_all")).Visible,"Apply-all is visible for a multi-conflict operation: "+language);using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,dialog.Size));image.Save(Path.Combine(root,"conflict-"+language+".png"));}((IButtonControl)dialog.AcceptButton).PerformClick();Check(dialog.DialogResult==DialogResult.Cancel && dialog.Choice==ClassicStyleConflict.Cancel,"Enter defaults to cancellation rather than overwriting: "+language);}
            foreach(var ssa in new[]{false,true})
            {
                var label=language+" SSA="+ssa;var baseStyles=new[]{"A","B","C","D","E"}.Select(Style).ToList();if(ssa)baseStyles=baseStyles.Select(s=>ClassicStyleTransfer.Convert(s,false,true)).ToList();var document=Fixture(ssa,baseStyles);var documentBefore=Data(document);var format=ssa?(SubtitleFormat)new SubStationAlpha():new AdvancedSubStationAlpha();
                Configuration.Settings.SubtitleSettings.AssaStyleStorageCategories=new List<AssaStorageCategory>{new AssaStorageCategory{Name="Default",IsDefault=true,Styles=new List<SsaStyle>{Style("A"),Style("Library")}},new AssaStorageCategory{Name="Other",IsDefault=false,Styles=new List<SsaStyle>{Style("A")}}};var libraryBefore=Data(Configuration.Settings.SubtitleSettings.AssaStyleStorageCategories);
                var importDoc=Fixture(!ssa,new[]{Style("A"),Style("B"),Style("Imported")});importDoc.Paragraphs[0].Extra="A";importDoc.OriginalFormat=ssa?(SubtitleFormat)new AdvancedSubStationAlpha():new SubStationAlpha();
                // Build the source in its own native alignment vocabulary, then parse the actual disk file.
                var rawStyles=new[]{Style("A"),Style("B"),Style("Imported")}.Select(s=>ClassicStyleTransfer.Convert(s,false,!ssa)).ToList();rawStyles[0].FontSize=45;importDoc=Fixture(!ssa,rawStyles);var sourceFormat=ssa?(SubtitleFormat)new AdvancedSubStationAlpha():new SubStationAlpha();
                var file=Path.Combine(root,"import-"+language+(ssa?".ass":".ssa"));File.WriteAllText(file,sourceFormat.ToText(importDoc,"Import"));var parsed=Subtitle.Parse(file);Check(parsed!=null,"Real source style file parses: "+label);
                
                using(var manager=new AssaStyles(document,format,null,"A"))
                {
                    Show(manager);await Task.Delay(40);var list=(ListView)Field(manager,"listViewStyles");var count=0;Func<string,bool,ClassicStyleAnswer> overwrite=(n,m)=>{count++;return Answer(ClassicStyleConflict.Overwrite,true);};
                    Check((bool)Call(manager,"ImportClassicStyleSelection",parsed,new[]{"A","B","Imported"},false,overwrite),"Real-file selected-style import uses the shared conflict workflow: "+label);
                    Check(count==1 && Styles(manager).Count==6 && Styles(manager)[0].Name=="A" && Styles(manager)[0].FontSize==45,"Import overwrites in place and appends only new names: "+label);
                    Check(Styles(manager)[0].Alignment==baseStyles[0].Alignment,"Cross-format import converts SSA/ASS alignment: "+label);
                    Check(manager.RenameActions.Count==0 && document.Paragraphs[0].Extra=="A","Overwriting does not rename cue references: "+label);
                    var expected=Data(Styles(manager));Check(!(bool)Call(manager,"TransferClassicStyles",new[]{Style("New before cancel"),Style("A")},false,new Func<string,bool,ClassicStyleAnswer>((n,m)=>Answer(ClassicStyleConflict.Cancel))) && Data(Styles(manager))==expected,"Cancel rolls back the whole manager transfer: "+label);
                    Check((bool)Call(manager,"TransferClassicStyles",new[]{new SsaStyle(baseStyles[0])},false,new Func<string,bool,ClassicStyleAnswer>((n,m)=>Answer(ClassicStyleConflict.KeepBoth))) && Styles(manager).Last().Name=="A_2","Manager keep-both action allocates a new style name: "+label);
                    var header=manager.Header;var names=AdvancedSubStationAlpha.GetStylesFromHeader(header);Check(names.Count==7 && names.Distinct(StringComparer.OrdinalIgnoreCase).Count()==7,"Regenerated header contains one definition per style: "+label);
                    var round=new Subtitle{Header=header};round.Paragraphs.Add(new Paragraph("Hello",1000,3000){Extra="A"});var decoded=new Subtitle();format.LoadSubtitle(decoded,format.ToText(round,"Round trip").SplitToLines(),"round"+format.Extension);var a=AdvancedSubStationAlpha.GetSsaStyle("A",decoded.Header);
                    Check(a.FontSize==45 && a.Alignment==baseStyles[0].Alignment && a.Primary.ToArgb()==baseStyles[0].Primary.ToArgb(),"Imported formatting survives actual ASS/SSA serialization: "+label);
                    var oldOrder=Styles(manager).Select(s=>s.Name).ToList();Select(list,1,3);Call(manager,"MoveToTop",list);Check(Styles(manager).Take(2).Select(s=>s.Name).SequenceEqual(new[]{oldOrder[1],oldOrder[3]}) && list.SelectedItems.Count==2,"Several selected styles move to top together: "+label);
                    Call(manager,"MoveToBottom",list);Check(Styles(manager).Skip(Styles(manager).Count-2).Select(s=>s.Name).SequenceEqual(new[]{oldOrder[1],oldOrder[3]}),"Several selected styles move to bottom together: "+label);
                    Call(manager,"listViewStyles_KeyDown",list,new KeyEventArgs(Keys.Control|Keys.Up));Check(list.SelectedIndices[0]==Styles(manager).Count-3 && list.SelectedItems.Count==2,"Ctrl+Up moves the selected block and preserves selection: "+label);
                    Call(manager,"listViewStyles_KeyDown",list,new KeyEventArgs(Keys.Control|Keys.Down));Check(list.SelectedIndices[0]==Styles(manager).Count-2,"Ctrl+Down moves the block back: "+label);
                    Check(AdvancedSubStationAlpha.GetStylesFromHeader(manager.Header).SequenceEqual(Styles(manager).Select(s=>s.Name)),"Header order matches the actual reordered style list: "+label);
                    if(!ssa)
                    {
                        var category=(AssaStorageCategory)Field(manager,"_currentCategory");var other=((List<AssaStorageCategory>)Field(manager,"_storageCategories"))[1];var otherBefore=Data(other);
                        var changed=Style("a");changed.FontSize=67;Check((bool)Call(manager,"TransferClassicStyles",new[]{changed},true,overwrite) && category.Styles[0].Name=="A" && category.Styles[0].FontSize==67,"Storage overwrite preserves the target identity: "+label);
                        Check(!(bool)Field(manager,"_fileStyleActive"),"Transfer activates the destination property editor: "+label);var fileSize=Styles(manager)[0].FontSize;((NikseUpDown)Field(manager,"numericUpDownFontSize")).Value=68;Check(category.Styles[0].FontSize==68 && Styles(manager)[0].FontSize==fileSize,"Editing transferred library style leaves file styles intact: "+label);
                        Check(category.Name=="Default" && category.IsDefault && Data(other)==otherBefore,"Storage conflict scope retains category/default metadata and other categories: "+label);
                        var storage=(ListView)Field(manager,"listViewStorage");Select(storage,0,1);Call(manager,"MoveToBottom",storage);Check(storage.SelectedItems.Count==2 && category.Styles.Select(s=>s.Name).SequenceEqual(storage.Items.Cast<ListViewItem>().Select(i=>i.Text)),"Storage block reorder keeps backing styles and visible rows aligned: "+label);
                        Check(Data(Configuration.Settings.SubtitleSettings.AssaStyleStorageCategories)==libraryBefore,"Uncommitted storage changes stay inside the manager draft: "+label);
                    }
                    manager.DialogResult=DialogResult.Cancel;manager.Close();
                }
                Check(Data(document)==documentBefore && Data(Configuration.Settings.SubtitleSettings.AssaStyleStorageCategories)==libraryBefore,"Cancel retains the subtitle and global storage library: "+label);
                using(var main=new Nikse.SubtitleEdit.Forms.Main())
                {
                    Show(main);await Task.Delay(140);Call(main,"SetCurrentFormat",format.FriendlyName);Set(main,"_subtitle",document);Set(main,"_undoIndex",-1);var grid=(SubtitleListView)Field(main,"SubtitleListview1");Call(grid,"Fill",document,null);grid.SelectIndexAndEnsureVisible(0);var originalHeader=document.Header;
                    using(var manager=new AssaStyles(document,format,main,"A"))
                    {
                        Show(manager);var replacement=new SsaStyle(baseStyles[0]){FontSize=54};Call(manager,"TransferClassicStyles",new[]{replacement},false,new Func<string,bool,ClassicStyleAnswer>((n,m)=>Answer(ClassicStyleConflict.Overwrite)));Call(main,"ApplyAssaStyles",manager);
                        Check(AdvancedSubStationAlpha.GetSsaStyle("A",document.Header).FontSize==54 && document.Paragraphs[0].Actor=="Alice" && document.Paragraphs[0].Layer==4 && document.Paragraphs[0].Forced,"Apply updates style formatting while retaining cue metadata: "+label);
                        Call(main,"UndoToIndex",true);Check(document.Header==originalHeader && document.Paragraphs[0].Extra=="A","One ordinary editor Undo restores the applied style header: "+label);
                    }
                }
            }
        }
    }
}
