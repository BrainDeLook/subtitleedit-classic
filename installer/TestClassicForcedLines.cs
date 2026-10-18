using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicForcedLines
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static int checks;
    static object Field(object value,string name)=>value.GetType().GetField(name,F).GetValue(value);
    static void Set(object value,string name,object data)=>value.GetType().GetField(name,F).SetValue(value,data);
    static object Call(object value,string name,params object[] args)
    {
        try { return value.GetType().GetMethods(F).Single(m=>m.Name==name && m.GetParameters().Length==args.Length && m.GetParameters().Select((p,i)=>args[i]==null || p.ParameterType.IsInstanceOfType(args[i])).All(v=>v)).Invoke(value,args); }
        catch(TargetInvocationException ex) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();return null; }
    }
    static void Check(bool value,string label) { if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label); }
    static Subtitle Sub()
    {
        var s=new Subtitle();for(int i=0;i<3;i++)s.Paragraphs.Add(new Paragraph("<i>Реплика "+i+"</i>",1000+i*3000,3000+i*3000){Number=i+1,Actor="Alice",Extra="Style",Layer=2});return s;
    }
    static Subtitle Clean(Subtitle s) { var result=new Subtitle(s,false);foreach(var p in result.Paragraphs){p.Forced=false;p.Bookmark=null;}return result; }
    static void Select(SubtitleListView grid,params int[] indices) { foreach(ListViewItem item in grid.Items)item.Selected=false;foreach(var index in indices)grid.Items[index].Selected=true; }
    [STAThread]static int Main(string[] args)
    {
        var root=Path.Combine(Path.GetFullPath(args[0]),"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
        typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);
        typeof(Configuration).GetField("SettingsFileName",BindingFlags.Public|BindingFlags.Static).SetValue(null,Path.Combine(root,"Settings.xml"));
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();var exit=0;
        using(var anchor=new Form {ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)})
        {anchor.Shown+=async(s,e)=>{try{await Run(root);}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{anchor.Close();}};Application.Run(anchor);}
        Console.WriteLine("FEATURE CHECKS: "+checks);Console.WriteLine("ARTIFACTS: "+root);return exit;
    }
    static async Task Run(string root)
    {
        LanguageSettings.Current=new Language();Configuration.Settings.General.Language="en-US";
        var source=Sub();source.Paragraphs[0].Forced=true;source.Paragraphs[1].Bookmark="Quoted \"bookmark\" [array] \\ Unicode 日本語";
        var path=Path.Combine(root,"marks.srt");var marks=new ClassicSubtitleMarksPersistence(source,path);
        Check(marks.Save(),"Forced and bookmark marks save together");var content=File.ReadAllText(path+".SE.bookmarks");Check(content.Contains("\"bookmarks\"") && content.Contains("\"forced\"") && content.Contains("\"ms\""),"Sidecar retains the stable 5.3 bookmark and forced schema");
        var loaded=Clean(source);Check(new ClassicSubtitleMarksPersistence(loaded,path).Load() && loaded.Paragraphs[0].Forced && loaded.Paragraphs[1].Bookmark==source.Paragraphs[1].Bookmark,"Forced marks and escaped Unicode bookmarks round-trip");
        var shifted=Clean(source);shifted.Paragraphs.Insert(0,new Paragraph("Inserted before",100,500));new ClassicSubtitleMarksPersistence(shifted,path).Load();Check(!shifted.Paragraphs[0].Forced && shifted.Paragraphs[1].Forced && shifted.Paragraphs[2].Bookmark!=null,"Time keys keep marks on their cue after a row is inserted above");
        var rounded=Clean(source);rounded.Paragraphs[0].StartTime.TotalMilliseconds+=42;new ClassicSubtitleMarksPersistence(rounded,path).Load();Check(rounded.Paragraphs[0].Forced,"Frame-rounded times resolve within the stable tolerance");
        var sameTime=Clean(source);sameTime.Paragraphs[1].StartTime.TotalMilliseconds=1000;sameTime.Paragraphs[0].Forced=true;sameTime.Paragraphs[1].Forced=true;var duplicatePath=Path.Combine(root,"duplicates.srt");new ClassicSubtitleMarksPersistence(sameTime,duplicatePath).Save();var sameLoaded=Clean(sameTime);new ClassicSubtitleMarksPersistence(sameLoaded,duplicatePath).Load();Check(sameLoaded.Paragraphs.Count(p=>p.Forced)==2,"Separate forced marks survive identical cue start times");
        var legacy=Path.Combine(root,"legacy.srt");File.WriteAllText(legacy+".SE.bookmarks","{\"bookmarks\":[{\"idx\":1,\"txt\":\"Old bookmark\"}]}");loaded=Clean(source);Check(new ClassicSubtitleMarksPersistence(loaded,legacy).Load() && loaded.Paragraphs[1].Bookmark=="Old bookmark" && !loaded.Paragraphs.Any(p=>p.Forced),"Classic index-only bookmark files remain readable");
        Check(!new ClassicSubtitleMarksPersistence(source,"").Save() && !File.Exists(Path.Combine(root,".SE.bookmarks")),"Untitled documents never write a relative sidecar");
        var none=Clean(source);Check(new ClassicSubtitleMarksPersistence(none,path).Save() && !File.Exists(path+".SE.bookmarks"),"Clearing all marks removes only that document's sidecar");
        source.Paragraphs[0].Forced=true;source.Paragraphs[1].Bookmark=null;new ClassicSubtitleMarksPersistence(source,path).Save();Check(new ClassicSubtitleMarksPersistence(Clean(source),Path.Combine(root,"missing.srt")).Load(),"Missing sidecars leave unmarked cues alone");
        var impossible=Path.Combine(root,"missing-parent","file.srt");Check(!new ClassicSubtitleMarksPersistence(source,impossible).Save(),"An unavailable sidecar directory reports failure");
        var reparsed=Clean(source);reparsed.Paragraphs[0].Text="Edited source text";new ClassicSubtitleMarksPersistence(reparsed,null).RestoreForcedFrom(source);Check(reparsed.Paragraphs[0].Forced,"Reparsing edited source text retains its Forced mark by time");
        reparsed=Clean(source);reparsed.Paragraphs.RemoveAt(0);new ClassicSubtitleMarksPersistence(reparsed,null).RestoreForcedFrom(source);Check(!reparsed.Paragraphs[0].Forced,"Deleting a marked source cue does not mark the next unrelated row");
        reparsed=Clean(source);foreach(var p in reparsed.Paragraphs)p.StartTime.TotalMilliseconds+=1500;new ClassicSubtitleMarksPersistence(reparsed,null).RestoreForcedFrom(source,1500);Check(reparsed.Paragraphs[0].Forced,"Source parsing with video offset resolves the mark in shifted time coordinates");
        var existing=File.ReadAllText(path+".SE.bookmarks");using(var locked=new FileStream(path+".SE.bookmarks",FileMode.Open,FileAccess.Read,FileShare.Read)){source.Paragraphs[2].Forced=true;Check(!new ClassicSubtitleMarksPersistence(source,path).Save(),"A locked sidecar reports failure without replacement");}Check(File.ReadAllText(path+".SE.bookmarks")==existing && !Directory.GetFiles(root,"*.tmp").Any(),"Failed sidecar replacement preserves the previous content and removes its temporary file");
        foreach(var language in new[]{"en-US","ru-RU"})
        {
            Configuration.Settings.General.Language=language;LanguageSettings.Current=language=="en-US"?new Language():Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages",language+".xml"));
            using(var main=new Nikse.SubtitleEdit.Forms.Main())
            {
                main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-20000,-20000);main.ShowInTaskbar=false;main.Show();await Task.Delay(30);
                var current=Sub();var original=Clean(current);Set(main,"_subtitle",current);Set(main,"_subtitleOriginal",original);var file=Path.Combine(root,language+".srt");File.WriteAllText(file,new SubRip().ToText(current,file));Set(main,"_fileName",file);
                var grid=(SubtitleListView)Field(main,"SubtitleListview1");Call(grid,"Fill",current,original);Select(grid,0,2);var text=JsonConvert.SerializeObject(current.Paragraphs.Select(p=>new{p.Text,p.Actor,p.Extra,p.Layer,p.StartTime,p.EndTime}));
                var hash=(int)Call(main,"GetFastSubtitleHash");Call(main,"ToggleClassicForcedLines");Check(current.Paragraphs[0].Forced && !current.Paragraphs[1].Forced && current.Paragraphs[2].Forced,"Only selected cues receive Forced marks: "+language);
                Check((int)Call(main,"GetFastSubtitleHash")!=hash,"Forced-only edits affect the unsaved-change hash: "+language);
                Check(grid.ColumnIndexForced>=0 && grid.Items[0].SubItems[grid.ColumnIndexForced].Text=="✓" && grid.Items[1].SubItems[grid.ColumnIndexForced].Text=="","The Forced column appears and displays current marks: "+language);
                Check(grid.SelectedIndices.Cast<int>().SequenceEqual(new[]{0,2}),"Toggling marks retains the multiselection: "+language);
                Check(JsonConvert.SerializeObject(current.Paragraphs.Select(p=>new{p.Text,p.Actor,p.Extra,p.Layer,p.StartTime,p.EndTime}))==text && !original.Paragraphs.Any(p=>p.Forced),"Marking leaves text, times, actor/style metadata and original cues intact: "+language);
                Check(ClassicGridAppearance.Load(Path.Combine(root,"ClassicGridAppearance.json")).ShowForcedColumn,"Forced-column visibility is persisted: "+language);
                Call(main,"SetClassicGridAppearance",ClassicGridFormattingMode.ShowFormatting);Check(ClassicGridAppearance.Load(Path.Combine(root,"ClassicGridAppearance.json")).ShowForcedColumn,"Changing formatting does not reset Forced-column visibility: "+language);
                Call(main,"UndoToIndex",true);current=(Subtitle)Field(main,"_subtitle");Check(!current.Paragraphs.Any(p=>p.Forced) && (int)Call(main,"GetFastSubtitleHash")==hash,"One Undo restores Forced flags and the clean hash: "+language);var fromDisk=Clean(current);new ClassicSubtitleMarksPersistence(fromDisk,file).Load();Check(!fromDisk.Paragraphs.Any(p=>p.Forced),"Undo also updates persisted Forced marks: "+language);
                Call(main,"UndoToIndex",false);current=(Subtitle)Field(main,"_subtitle");Check(current.Paragraphs[0].Forced && current.Paragraphs[2].Forced,"Redo restores the complete Forced selection: "+language);
                Select(grid,0,1);Call(main,"ToggleClassicForcedLines");current=(Subtitle)Field(main,"_subtitle");Check(current.Paragraphs.All(p=>p.Forced),"Mixed marked/unmarked selection is marked uniformly: "+language);
                Call(main,"ToggleClassicForcedLines");Check(!current.Paragraphs[0].Forced && !current.Paragraphs[1].Forced && current.Paragraphs[2].Forced,"An entirely marked selection is unmarked uniformly: "+language);
                Select(grid,2);Call(main,"ToggleBookmarks",false,main);fromDisk=Clean(current);new ClassicSubtitleMarksPersistence(fromDisk,file).Load();Check(fromDisk.Paragraphs[2].Forced && fromDisk.Paragraphs[2].Bookmark!=null,"Ordinary bookmark editing retains Forced marks on disk: "+language);
                Call(main,"ClearBookmarks");fromDisk=Clean(current);new ClassicSubtitleMarksPersistence(fromDisk,file).Load();Check(fromDisk.Paragraphs[2].Forced && fromDisk.Paragraphs.All(p=>p.Bookmark==null),"Clearing bookmarks preserves Forced marks: "+language);
                Check((DialogResult)Call(main,"SaveSubtitle",new SubRip(),false,true)==DialogResult.OK,"Ordinary File Save accepts a subtitle with Forced marks: "+language);
                var saved=new Subtitle();new SubRip().LoadSubtitle(saved,File.ReadAllLines(file).ToList(),file);new ClassicSubtitleMarksPersistence(saved,file).Load();Check(saved.Paragraphs.Count==3 && saved.Paragraphs[2].Forced && !saved.Paragraphs[0].Forced,"Ordinary File Save writes text and Forced marks for reopening: "+language);
                var before=JsonConvert.SerializeObject(current.Paragraphs);var history=current.HistoryItems.Count;
                foreach(SubtitleFormat format in new SubtitleFormat[]{new SubRip(),new AdvancedSubStationAlpha()})
                {
                    var output=Path.Combine(root,"forced-"+language+format.Extension);Check((int)Call(main,"SaveClassicForcedExport",output,format)==1,"Forced-only export writes exactly the marked cue: "+language+format.Extension);
                    var read=new Subtitle();format.LoadSubtitle(read,File.ReadAllLines(output).ToList(),output);Check(read.Paragraphs.Count==1 && HtmlUtil.RemoveHtmlTags(read.Paragraphs[0].Text,true)==HtmlUtil.RemoveHtmlTags(current.Paragraphs[2].Text,true) && read.Paragraphs[0].StartTime.TotalMilliseconds==current.Paragraphs[2].StartTime.TotalMilliseconds,"Exported subtitle retains the selected text and timing: "+language+format.Extension);
                    new ClassicSubtitleMarksPersistence(read,output).Load();Check(read.Paragraphs[0].Forced,"Exported Forced flags survive reopening via the sidecar: "+language+format.Extension);
                }
                Check(JsonConvert.SerializeObject(current.Paragraphs)==before && current.HistoryItems.Count==history && (string)Field(main,"_fileName")==file,"Export leaves the open subtitle and its history/name intact: "+language);
                var originalFile=Path.Combine(root,"reference-"+language+".srt");File.WriteAllText(originalFile,"Reference sentinel");Set(main,"_subtitleOriginalFileName",originalFile);
                foreach(var protectedPath in new[]{file,originalFile}) {var bytes=File.ReadAllBytes(protectedPath);try{Call(main,"SaveClassicForcedExport",protectedPath,new SubRip());throw new Exception("Overwrite was accepted");}catch(InvalidOperationException){Check(File.ReadAllBytes(protectedPath).SequenceEqual(bytes),"Forced-only export refuses to overwrite an open subtitle: "+Path.GetFileName(protectedPath));}}
                Call(main,"SetClassicForcedColumn",false,true);Check(grid.ColumnIndexForced<0 && grid.Items.Cast<ListViewItem>().All(i=>i.SubItems.Count==grid.Columns.Count),"Hiding the Forced column retains aligned text subitems: "+language);Call(main,"SetClassicForcedColumn",true,true);Check(grid.Items.Cast<ListViewItem>().All(i=>i.SubItems.Count==grid.Columns.Count) && grid.Items[2].SubItems[grid.ColumnIndexForced].Text=="✓","Showing the column again restores marks without duplicating subitems: "+language);
                Call(main,"InitializeClassicForcedLines");Check(((ToolStripMenuItem)Field(main,"_classicForcedToggle")).Text.Contains(language=="ru-RU"?"Переключить":"Toggle"),"Forced commands refresh their localized labels: "+language);
                using(var image=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(image,new Rectangle(Point.Empty,main.Size));image.Save(Path.Combine(root,"forced-main-"+language+".png"));}
                Select(grid);Call(main,"ToggleClassicForcedLines");Check(current.Paragraphs[2].Forced,"An empty selection does not change any marks: "+language);
                current.Paragraphs[2].Forced=false;var emptyPath=Path.Combine(root,"empty-"+language+".srt");Check((int)Call(main,"SaveClassicForcedExport",emptyPath,new SubRip())==0 && !File.Exists(emptyPath),"No Forced cues produces no empty output file: "+language);
            }
            using(var reopened=new Nikse.SubtitleEdit.Forms.Main())
            {
                Check(((SubtitleListView)Field(reopened,"SubtitleListview1")).ColumnIndexForced>=0,"New main windows restore Forced-column visibility: "+language);
                Call(reopened,"OpenSubtitle",Path.Combine(root,language+".srt"),Encoding.UTF8);
                var read=(Subtitle)Field(reopened,"_subtitle");Check(read.Paragraphs.Count==3 && read.Paragraphs[2].Forced && !read.Paragraphs[0].Forced,"Normal File Open restores Forced marks with the subtitle: "+language);
                var column=((SubtitleListView)Field(reopened,"SubtitleListview1")).ColumnIndexForced;Check(((SubtitleListView)Field(reopened,"SubtitleListview1")).Items[2].SubItems[column].Text=="✓","Normal File Open populates the Forced column from restored marks: "+language);
                foreach(var reload in new[]{"ReloadFromSourceInSourceView","ReloadFromSourceView"})
                {
                    ((Control)Field(reopened,"textBoxSource")).Text=new SubRip().ToText((Subtitle)Field(reopened,"_subtitle"),"source.srt").Replace("Реплика 2","Updated source text "+reload);
                    Set(reopened,"_sourceViewChange",true);Call(reopened,reload);read=(Subtitle)Field(reopened,"_subtitle");Check(read.Paragraphs[2].Forced && read.Paragraphs[2].Text.Contains("Updated source text"),"Source edits retain Forced flags: "+language+" "+reload);
                }
            }
        }
    }
}
