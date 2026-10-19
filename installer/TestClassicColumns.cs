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
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using C=Nikse.SubtitleEdit.Controls.SubtitleListView.SubtitleColumn;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicColumns
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;
    static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Set(object o,string n,object value)=>o.GetType().GetField(n,F).SetValue(o,value);
    static object Call(object o,string n,params object[] args){try{return o.GetType().GetMethods(F).Single(m=>m.Name==n && m.GetParameters().Length==args.Length && m.GetParameters().Select((p,i)=>args[i]==null || p.ParameterType.IsInstanceOfType(args[i])).All(v=>v)).Invoke(o,args);}catch(TargetInvocationException ex){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();return null;}}
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label);}
    static Subtitle Sub(){var s=new Subtitle {Header=AdvancedSubStationAlpha.DefaultHeader};for(int i=0;i<4;i++)s.Paragraphs.Add(new Paragraph("<i>Cue "+i+"</i>",1000+3000*i,3000+3000*i){Number=i+1,Actor="Alice",Extra="Default",Region="Region",Layer=i,Forced=i==1});return s;}
    static void Show(Form f){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.ShowInTaskbar=false;f.Show();}
    static List<ClassicColumnPreference> Pref(bool visible=true)=>ClassicColumnPreference.Normalize(null,c=>visible);
    static void Visibility(List<ClassicColumnPreference> prefs,C key,bool visible)=>prefs.Single(p=>p.Key==key.ToString()).Visible=visible;
    static void Align(SubtitleListView grid,string label){Check(grid.Items.Cast<ListViewItem>().All(i=>i.SubItems.Count==grid.Columns.Count),"Each row matches the logical columns: "+label);Check(grid.Items.Cast<ListViewItem>().All(i=>i.SubItems[grid.ColumnIndexText].Text==((Paragraph)i.Tag).Text),"Text stays in its logical text subitem: "+label);}
    [STAThread]static int Main(string[] args)
    {
        var root=Path.Combine(Path.GetFullPath(args[0]),"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
        typeof(Configuration).GetField("DataDirectory").SetValue(null,root+Path.DirectorySeparatorChar);typeof(Configuration).GetField("SettingsFileName").SetValue(null,Path.Combine(root,"Settings.xml"));
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();var exit=0;
        using(var anchor=new Form {ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)}){anchor.Shown+=async(s,e)=>{try{await Run(root);}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{anchor.Close();}};Application.Run(anchor);}Console.WriteLine("FEATURE CHECKS: "+checks);Console.WriteLine("ARTIFACTS: "+root);return exit;
    }
    static async Task Run(string root)
    {
        LanguageSettings.Current=new Language();Configuration.Settings.General.Language="en-US";
        var invalid=new[]{null,new ClassicColumnPreference {Key="Unknown"},new ClassicColumnPreference {Key="999"},new ClassicColumnPreference {Key="Text",Visible=false},new ClassicColumnPreference {Key="Text",Visible=true}};
        var clean=ClassicColumnPreference.Normalize(invalid,c=>false);Check(clean.Count==Enum.GetValues(typeof(C)).Length && clean.Select(p=>p.Key).Distinct().Count()==clean.Count,"Normalization removes null, unknown and duplicate keys");Check(clean.Single(p=>p.Key=="Text").Visible && clean.Single(p=>p.Key=="Number").Visible,"Text and numbering cannot be hidden by invalid persisted settings");
        using(var form=new Form())using(var grid=new SubtitleListView {Dock=DockStyle.Fill})
        {
            form.Controls.Add(grid);grid.InitializeLanguage(LanguageSettings.Current.General,Configuration.Settings);Show(form);var source=Sub();Call(grid,"Fill",source,new Subtitle());
            grid.HideColumn(C.Start);grid.HideColumn(C.End);grid.ShowEndColumn("END");Call(grid,"Fill",source,new Subtitle());Check(grid.Columns[grid.ColumnIndexEnd].Text=="END" && grid.ColumnIndexNumber==0,"End can be restored while Start is hidden without swapping headers");
            grid.HideColumn(C.End);grid.HideColumn(C.Duration);grid.ShowDurationColumn("DURATION");Call(grid,"Fill",source,new Subtitle());Check(grid.Columns[grid.ColumnIndexDuration].Text=="DURATION" && grid.ColumnIndexNumber==0,"Duration can be restored without Start/End without displacing numbering");
            grid.ShowExtraColumn("Style");Call(grid,"Fill",source,new Subtitle());grid.HideColumn(C.Extra);Align(grid,"removing the final extra column");
        }
        foreach(var language in new[]{"en-US","ru-RU"})
        {
            Configuration.Settings.General.Language=language;LanguageSettings.Current=language=="en-US"?new Language():Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages",language+".xml"));
            using(var main=new Nikse.SubtitleEdit.Forms.Main())
            {
                Show(main);await Task.Delay(100);var source=Sub();var original=new Subtitle(source,false);original.Paragraphs[0].Text="Original source";Set(main,"_subtitle",source);Set(main,"_subtitleOriginal",original);var grid=(SubtitleListView)Field(main,"SubtitleListview1");grid.ShowOriginalTextColumn("Original");Call(grid,"Fill",source,original);grid.SelectIndexAndEnsureVisible(1);grid.Items[3].Selected=true;var snapshot=JsonConvert.SerializeObject(source);var orig=JsonConvert.SerializeObject(original);var history=source.HistoryItems.Count;
                var prefs=Pref();prefs.Reverse();Call(main,"ApplyClassicColumns",prefs,true);await Task.Delay(80);
                Check(grid.GetColumnDisplayOrder().SequenceEqual(prefs.Where(p=>grid.GetColumnIndex((C)Enum.Parse(typeof(C),p.Key))>=0).Select(p=>(C)Enum.Parse(typeof(C),p.Key))),"Display order follows the full preference order: "+language);Align(grid,"reversed order "+language);
                Check(grid.SelectedIndices.Cast<int>().SequenceEqual(new[]{1,3}),"Applying column layout retains the selected cues: "+language);Check(JsonConvert.SerializeObject(source)==snapshot && JsonConvert.SerializeObject(original)==orig && source.HistoryItems.Count==history,"Column layout leaves source, original, metadata and undo history unchanged: "+language);
                Check(grid.Items[0].SubItems[grid.ColumnIndexTextOriginal].Text=="Original source","The original text column keeps the correct data after reordering: "+language);
                Visibility(prefs,C.Start,false);Visibility(prefs,C.End,false);Visibility(prefs,C.Duration,false);Visibility(prefs,C.Forced,false);Visibility(prefs,C.Extra,false);Call(main,"ApplyClassicColumns",prefs,true);await Task.Delay(60);Check(grid.ColumnIndexStart<0 && grid.ColumnIndexEnd<0 && grid.ColumnIndexDuration<0 && grid.ColumnIndexForced<0,"Optional columns hide together: "+language);Align(grid,"hidden columns "+language);
                grid.ShowExtraColumn("Style");Check(grid.ColumnIndexExtra<0,"Format updates cannot reintroduce a hidden style column: "+language);
                Call(main,"SetClassicForcedColumn",true,true);Check(grid.ColumnIndexForced>=0 && ((List<ClassicColumnPreference>)Field(main,"_classicColumns")).Single(p=>p.Key=="Forced").Visible,"The existing Forced switch updates the custom layout: "+language);
                Call(main,"SetClassicGridAppearance",ClassicGridFormattingMode.HideTags);Check(ClassicGridAppearance.Load(Path.Combine(root,"ClassicGridAppearance.json")).Columns.Any(p=>p.Key=="Forced" && p.Visible),"Formatting changes preserve column preferences: "+language);
                for(int mask=0;mask<8;mask++)
                {
                    var combination=Pref(false);Visibility(combination,C.Start,(mask&1)!=0);Visibility(combination,C.End,(mask&2)!=0);Visibility(combination,C.Duration,(mask&4)!=0);Call(main,"ApplyClassicColumns",combination,false);Align(grid,"time-column combination "+language+" "+mask);Check(grid.ColumnIndexNumber==0 && grid.Columns[0].Text==LanguageSettings.Current.General.NumberSymbol,"Number header stays logically first in time-column combination "+mask+" "+language);
                }
                Call(main,"SetCurrentFormat",new AdvancedSubStationAlpha().FriendlyName);Call(main,"ApplyClassicColumns",Pref(),false);Check(grid.ColumnIndexExtra>=0 && grid.ColumnIndexActor>=0,"ASS exposes its enabled style and actor columns: "+language);Align(grid,"ASS metadata "+language);
                var hidden=Pref();Visibility(hidden,C.Extra,false);Visibility(hidden,C.Actor,false);Call(main,"ApplyClassicColumns",hidden,false);Call(main,"SetCurrentFormat",new SubRip().FriendlyName);Call(main,"SetCurrentFormat",new AdvancedSubStationAlpha().FriendlyName);Check(grid.ColumnIndexExtra<0 && grid.ColumnIndexActor<0,"ASS/SRT switching respects explicitly hidden metadata columns: "+language);Align(grid,"format switch "+language);
                Call(main,"ApplyClassicColumns",Pref(),false);Call(main,"SetCurrentFormat",new SubRip().FriendlyName);grid.HideColumn(C.Extra);grid.ShowExtraColumn("Style");Call(grid,"Fill",source,original);Check(grid.GetColumnDisplayOrder().Last()==C.WordsPerMinute || grid.GetColumnDisplayOrder().Contains(C.Extra),"Reappearing metadata participates in the saved order: "+language);
                var display=grid.GetColumnDisplayOrder();var header=grid.Columns[grid.GetColumnIndex(display[0])];var args=new ColumnReorderedEventArgs(0,display.Length-1,header);Call(main,"SaveClassicColumnDrag",args);await Task.Delay(60);Check(grid.GetColumnDisplayOrder().Last()==display[0],"Dragging a header persists and applies its new display position: "+language);
                var tools=Configuration.Settings.Tools;tools.ListViewShowColumnStartTime=false;tools.ListViewShowColumnEndTime=true;Call(main,"RefreshClassicColumnsFromLegacySettings");Check(grid.ColumnIndexStart<0 && grid.ColumnIndexEnd>=0,"Legacy Tools settings update the custom column layout: "+language);
                using(var image=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(image,new Rectangle(Point.Empty,main.Size));image.Save(Path.Combine(root,"columns-main-"+language+".png"));}
                var draft=Pref();var before=JsonConvert.SerializeObject(draft);
                using(var dialog=new ClassicGridColumns(draft,c=>(string)Call(main,"ColumnLabel",c),c=>(string)Call(main,"ColumnNote",c),()=>Pref(false)))
                {
                    Show(dialog);await Task.Delay(30);var table=(DataGridView)Field(dialog,"_grid");Check(table.Rows.Count==Enum.GetValues(typeof(C)).Length,"Columns dialog lists every classic logical column: "+language);
                    Check(table.Rows.Cast<DataGridViewRow>().Where(r=>new[]{"Number","Text","TextOriginal","Network"}.Contains(draft[r.Index].Key)).All(r=>r.Cells[0].ReadOnly),"Required and document-managed visibility controls are read-only: "+language);
                    table.CurrentCell=table.Rows[1].Cells[1];Call(dialog,"MoveSelected",-1);Check(((List<ClassicColumnPreference>)Field(dialog,"_draft"))[0].Key=="Start","Move up changes only the dialog draft: "+language);
                    dialog.Size=dialog.MinimumSize;await Task.Delay(20);using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,dialog.Size));image.Save(Path.Combine(root,"columns-dialog-"+language+".png"));}
                    Check(table.Bottom<dialog.ClientSize.Height && table.Right<dialog.ClientSize.Width,"Columns list stays inside the minimum-size dialog: "+language);
                    dialog.DialogResult=DialogResult.Cancel;Check(dialog.Result==null && JsonConvert.SerializeObject(draft)==before,"Cancelling columns leaves the input preferences unchanged: "+language);
                }
                using(var dialog=new ClassicGridColumns(draft,c=>c.ToString(),c=>"",()=>Pref(false)))
                {Show(dialog);Call(dialog,"MoveSelected",1);((IButtonControl)dialog.AcceptButton).PerformClick();Check(dialog.Result!=null && dialog.Result[1].Key=="Number","OK returns an independent reordered preference list: "+language);}
                using(var dialog=new ClassicGridColumns(draft,c=>c.ToString(),c=>"",()=>Pref(false)))
                {Show(dialog);var footer=dialog.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<FlowLayoutPanel>().Single(p=>p.FlowDirection==FlowDirection.RightToLeft);footer.Controls.OfType<Button>().Single(b=>b!=dialog.AcceptButton && b!=dialog.CancelButton).PerformClick();((IButtonControl)dialog.AcceptButton).PerformClick();Check(dialog.Result.Where(p=>ClassicColumnPreference.CanToggle((C)Enum.Parse(typeof(C),p.Key))).All(p=>!p.Visible),"Reset modifies the draft and is committed only with OK: "+language);}
                var settingsPath=Path.Combine(root,"ClassicGridAppearance.json");var orderBefore=grid.GetColumnDisplayOrder();using(var locked=new FileStream(settingsPath,FileMode.Open,FileAccess.Read,FileShare.Read))
                {try{Call(main,"ApplyClassicColumns",Pref(false),true);throw new Exception("Locked settings were overwritten");}catch(IOException){Check(grid.GetColumnDisplayOrder().SequenceEqual(orderBefore),"Failed settings save leaves the active column layout unchanged: "+language);}}
                Check(JsonConvert.SerializeObject(source.Paragraphs.Select(p=>new{p.Text,p.Forced,p.Actor,p.Layer,p.StartTime,p.EndTime}))==JsonConvert.SerializeObject(Sub().Paragraphs.Select(p=>new{p.Text,p.Forced,p.Actor,p.Layer,p.StartTime,p.EndTime})),"Layout and dialog operations preserve cue data: "+language);
            }
            using(var reopened=new Nikse.SubtitleEdit.Forms.Main())
            {var prefs=(List<ClassicColumnPreference>)Field(reopened,"_classicColumns");Check(prefs!=null && !prefs.Single(p=>p.Key=="Start").Visible,"Restart restores the saved custom column visibility: "+language);var grid=(SubtitleListView)Field(reopened,"SubtitleListview1");Check(grid.ColumnIndexStart<0,"Restart applies hidden time columns to the real grid: "+language);}
        }
    }
}
