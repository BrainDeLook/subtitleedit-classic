using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using C=Nikse.SubtitleEdit.Controls.SubtitleListView.SubtitleColumn;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicActorColumn
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;
    static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethods(F).Single(m=>m.Name==n && m.GetParameters().Length==args.Length && m.GetParameters().Select((p,i)=>args[i]==null || p.ParameterType.IsInstanceOfType(args[i])).All(v=>v)).Invoke(o,args);
    static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
    static void Show(Form form){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.ShowInTaskbar=false;form.Show();}
    static List<ClassicColumnPreference> Preferences(bool actor)=>ClassicColumnPreference.Normalize(null,c=>c==C.Start || c==C.End || c==C.Duration || c==C.Extra || (c==C.Actor && actor));
    [STAThread]static int Main(string[] args)
    {
        var root=Path.Combine(Path.GetFullPath(args[0]),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        typeof(Configuration).GetField("DataDirectory").SetValue(null,root+Path.DirectorySeparatorChar);typeof(Configuration).GetField("SettingsFileName").SetValue(null,Path.Combine(root,"Settings.xml"));
        Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);var exit=0;
        using(var anchor=new Form{ShowInTaskbar=false,Location=new Point(-30000,-30000),StartPosition=FormStartPosition.Manual}){anchor.Shown+=async(s,e)=>{try{await Run(root);}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{anchor.Close();}};Application.Run(anchor);}Console.WriteLine("FEATURE CHECKS: "+checks);return exit;
    }
    static async Task Run(string root)
    {
        LanguageSettings.Current=new Language();var g=Configuration.Settings.General;g.DisableVideoAutoLoading=true;g.DisableShowingLoadErrors=true;g.DefaultSubtitleFormat=new AdvancedSubStationAlpha().FriendlyName;
        var source=new Subtitle{Header=AdvancedSubStationAlpha.DefaultHeader};source.Paragraphs.Add(new Paragraph("Hello",1000,2500){Actor="Alice",Extra="Default"});source.Paragraphs.Add(new Paragraph("World",3000,4500){Actor="Bob",Extra="Default"});source.Renumber();
        var ass=Path.Combine(root,"actors.ass");File.WriteAllText(ass,new AdvancedSubStationAlpha().ToText(source,"Test"),Encoding.UTF8);
        var ssa=Path.Combine(root,"actors.ssa");var legacy=new Subtitle(source,false){Header=SubStationAlpha.DefaultHeader};File.WriteAllText(ssa,new SubStationAlpha().ToText(legacy,"Test"),Encoding.UTF8);
        var empty=Path.Combine(root,"empty-actors.ass");var noActors=new Subtitle(source,false);foreach(var p in noActors.Paragraphs)p.Actor="";File.WriteAllText(empty,new AdvancedSubStationAlpha().ToText(noActors,"Test"),Encoding.UTF8);
        var srt=Path.Combine(root,"no-actors.srt");File.WriteAllText(srt,new SubRip().ToText(source,"Test"),Encoding.UTF8);
        var vtt=Path.Combine(root,"voices.vtt");File.WriteAllText(vtt,"WEBVTT\n\n00:00:01.000 --> 00:00:02.500\n<v Voice>A voice\n",Encoding.UTF8);
        foreach(var language in new[]{"ru-RU","en-US"})
        {
            g.Language=language;var appearance=new ClassicGridAppearance{Columns=Preferences(true)};appearance.Save(Path.Combine(root,"ClassicGridAppearance.json"));Configuration.Settings.Tools.ListViewShowColumnActor=true;
            using(var main=new Nikse.SubtitleEdit.Forms.Main())
            {
                Show(main);await Task.Delay(140);var grid=(SubtitleListView)Field(main,"SubtitleListview1");
                Call(main,"OpenSubtitle",ass,Encoding.UTF8);
                Check(grid.ColumnIndexActor>=0,"Saved enabled Actor is visible immediately after opening ASS: "+language);
                Check(grid.Columns[grid.ColumnIndexActor].Width>0 && grid.Items[0].SubItems[grid.ColumnIndexActor].Text=="Alice","Actor header has a visible width and actual file values: "+language);
                // Loading the same format does not run MakeFormatChange. Reproduce a missing physical column.
                grid.HideColumn(C.Actor);Call(main,"OpenSubtitle",ass,Encoding.UTF8);
                Check(grid.ColumnIndexActor>=0 && grid.Items[1].SubItems[grid.ColumnIndexActor].Text=="Bob","Same-format ASS reload restores the enabled physical column: "+language);
                Call(main,"OpenSubtitle",empty,Encoding.UTF8);Check(grid.ColumnIndexActor>=0 && grid.Items[0].SubItems[grid.ColumnIndexActor].Text=="","Enabled Actor is shown even when all actor names are empty: "+language);
                Call(main,"OpenSubtitle",srt,Encoding.UTF8);Check(grid.ColumnIndexActor<0 && Configuration.Settings.Tools.ListViewShowColumnActor,"SRT hides the unsupported column while retaining its preference: "+language);
                Call(main,"OpenSubtitle",ass,Encoding.UTF8);Check(grid.ColumnIndexActor>=0,"SRT to ASS opening restores Actor without toggling: "+language);
                Call(main,"OpenSubtitle",vtt,Encoding.UTF8);Check(grid.UseWebVttMetadata && grid.ColumnIndexActor>=0 && grid.Items[0].SubItems[grid.ColumnIndexActor].Text=="Voice","WebVTT uses its voice column: "+language);
                Call(main,"OpenSubtitle",ass,Encoding.UTF8);Check(!grid.UseWebVttMetadata && grid.ColumnIndexActor>=0 && grid.Items[0].SubItems[grid.ColumnIndexActor].Text=="Alice","WebVTT to ASS restores the Actor column and values: "+language);
                Call(main,"OpenSubtitle",ssa,Encoding.UTF8);grid.HideColumn(C.Actor);Call(main,"OpenSubtitle",ssa,Encoding.UTF8);Check(grid.ColumnIndexActor>=0 && grid.Items[1].SubItems[grid.ColumnIndexActor].Text=="Bob","Same-format legacy SSA reopening also restores Actor: "+language);Call(main,"OpenSubtitle",ass,Encoding.UTF8);
                Check(grid.Items.Cast<ListViewItem>().All(i=>i.SubItems.Count==grid.Columns.Count),"Restored column keeps every subitem aligned: "+language);
                var loaded=(Subtitle)Field(main,"_subtitle");Check(loaded.Paragraphs[0].Text=="Hello" && loaded.Paragraphs[0].StartTime.TotalMilliseconds==1000 && loaded.Paragraphs[1].Actor=="Bob" && !loaded.CanUndo,"Opening and restoring visibility preserve loaded data and clean history: "+language);
                Call(main,"ApplyClassicColumns",Preferences(false),true);Call(main,"OpenSubtitle",ass,Encoding.UTF8);Check(grid.ColumnIndexActor<0 && !Configuration.Settings.Tools.ListViewShowColumnActor,"Explicitly hidden Actor stays hidden even when the file has actors: "+language);
                Call(main,"SetClassicActorColumnVisibility",true);Call(main,"OpenSubtitle",ass,Encoding.UTF8);Check(grid.ColumnIndexActor>=0 && ClassicGridAppearance.Load(Path.Combine(root,"ClassicGridAppearance.json")).Columns.Single(p=>p.Key=="Actor").Visible,"Header-toggle path updates both the saved layout and actual visibility: "+language);
                Call(main,"SetClassicActorColumnVisibility",false);Call(main,"OpenSubtitle",ass,Encoding.UTF8);Check(grid.ColumnIndexActor<0 && !Configuration.Settings.Tools.ListViewShowColumnActor,"Header-toggle path can hide Actor persistently: "+language);Call(main,"SetClassicActorColumnVisibility",true);
            }
            new ClassicGridAppearance().Save(Path.Combine(root,"ClassicGridAppearance.json"));Configuration.Settings.Tools.ListViewShowColumnActor=true;
            using(var main=new Nikse.SubtitleEdit.Forms.Main())
            {Show(main);await Task.Delay(140);Call(main,"OpenSubtitle",ass,Encoding.UTF8);Check(((SubtitleListView)Field(main,"SubtitleListview1")).ColumnIndexActor>=0,"Legacy Settings.xml Actor preference works without a custom layout: "+language);}
            using(var main=new Nikse.SubtitleEdit.Forms.Main())
            {
                Show(main);await Task.Delay(140);Call(main,"OpenSubtitle",ass,Encoding.UTF8);var grid=(SubtitleListView)Field(main,"SubtitleListview1");Check(grid.ColumnIndexActor>=0 && grid.Items[0].SubItems[grid.ColumnIndexActor].Text=="Alice","Restart retains enabled Actor and restores it on ASS open: "+language);
            }
        }
    }
}
