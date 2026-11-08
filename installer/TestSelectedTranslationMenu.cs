using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.AutoTranslate;
using Nikse.SubtitleEdit.Core.Translate;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Forms.Translate;
class TestSelectedTranslationMenu
{
 const BindingFlags B=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static int checks;
 static object Get(object o,string name)=>o.GetType().GetField(name,B).GetValue(o);
 static void Set(object o,string name,object value)=>o.GetType().GetField(name,B).SetValue(o,value);
 static object Call(object o,string name,params object[] args)=>o.GetType().GetMethods(B).First(m=>m.Name==name && m.GetParameters().Length==args.Length).Invoke(o,args);
 static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 sealed class FakeTranslator:IAutoTranslator
 {
  public int Calls;public List<string> Inputs=new List<string>();public string Name=>"Offline UI fixture";public string Url=>"https://invalid.example";public string Error{get;set;}public int MaxCharacters=>1000;public void Initialize(){}
  public List<TranslationPair> GetSupportedSourceLanguages()=>new List<TranslationPair>{new TranslationPair("English","en")};
  public List<TranslationPair> GetSupportedTargetLanguages()=>new List<TranslationPair>{new TranslationPair("Russian","ru")};
  public Task<string> Translate(string text,string from,string to,CancellationToken token){Calls++;Inputs.Add(text);return Task.FromResult("Translated "+text);}
  public override string ToString()=>Name;
 }
 [STAThread]static int Main()
 {
  try{return Run();}catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
 static int Run()
 {
  var folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data");Directory.CreateDirectory(folder);
  foreach(var name in new[]{"DataDirectory","DictionariesDirectory","WaveformsDirectory","AutoBackupDirectory","SettingsFileName"})typeof(Configuration).GetField(name).SetValue(null,name=="SettingsFileName"?Path.Combine(folder,"Settings.xml"):folder+Path.DirectorySeparatorChar);
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
  Configuration.Settings.General.CheckForUpdates=false;Configuration.Settings.General.AutoSave=false;Configuration.Settings.General.Language="ru-RU";Application.EnableVisualStyles();
  using(var main=new Nikse.SubtitleEdit.Forms.Main())
  {
   main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-30000,-30000);main.Show();Application.DoEvents();main.Location=new Point(-30000,-30000);
   foreach(var mode in new[]{"apply","cancel","original"})
   {
    var sub=new Subtitle();for(int i=0;i<5;i++)sub.Paragraphs.Add(new Paragraph("Cue "+i,i*2000,i*2000+1000){Actor="Actor "+i,Extra="Style "+i,Number=i+1});
    Set(main,"_subtitle",sub);Set(main,"_currentSubtitleFormat",new SubRip());sub.OriginalFormat=new SubRip();var grid=(SubtitleListView)Get(main,"SubtitleListview1");
    if(mode=="original"){var original=new Subtitle(sub,false);foreach(var cue in sub.Paragraphs)cue.Text="Old "+cue.Number;Set(main,"_subtitleOriginal",original);grid.ShowOriginalTextColumn("Original");}
    var originals=(Subtitle)Get(main,"_subtitleOriginal");Call(grid,"Fill",sub,originals);grid.SelectedIndices.Clear();grid.Items[1].Selected=true;grid.Items[3].Selected=true;Call(main,"ShowSource");Call(main,"ResetHistory");Call(main,"RefreshClassicNavigation");
    var menu=(ContextMenuStrip)Get(main,"contextMenuStripListView");var group=(ToolStripMenuItem)Get(main,"toolStripMenuItemTranslateSelected");
    var item=(ToolStripMenuItem)group.DropDownItems["classicTranslateSelectedTogether"];
    Check(item!=null && item.Enabled,"selected-row command inside existing translation submenu: "+mode);
    Check(menu.Items["classicTranslateSelectedTogether"]==null,"no duplicate command at context-menu root: "+mode);
    Check(item.Text.Contains("заменой на месте") && item.ToolTipText.Contains("остальные сохраняются"),"specific in-place label and preservation tooltip: "+mode);
    var fake=new FakeTranslator();Exception error=null;var phase=0;var deadline=DateTime.UtcNow.AddSeconds(15);
    using(var timer=new System.Windows.Forms.Timer{Interval=50})
    {
     timer.Tick+=(sender,e)=>
     {
      var form=Application.OpenForms.OfType<AutoTranslate>().FirstOrDefault();
      if(form==null)return;
      try
      {
       if(DateTime.UtcNow>deadline)throw new Exception("Translation dialog timeout");
       if(phase==0)
       {
        var source=(SubtitleListView)Get(form,"subtitleListViewSource");var target=(SubtitleListView)Get(form,"subtitleListViewTarget");
        Check(source.SelectedIndices.Cast<int>().SequenceEqual(new[]{1,3}),"source selection retained: "+mode);Check(target.SelectedIndices.Cast<int>().SequenceEqual(new[]{1,3}),"target selection retained: "+mode);
        Check(form.TranslatedSubtitle.Paragraphs[0].Text==sub.Paragraphs[0].Text,"unselected target seeded: "+mode);
        Check(source.ContextMenuStrip!=null && source.ContextMenuStrip.Items[0].Text.Contains("выбранные"),"source has explicit selected-row menu");
        if(mode=="cancel"){timer.Stop();form.DialogResult=DialogResult.Cancel;return;}
        form.Location=new Point(-30000,-30000);form.ShowInTaskbar=false;
        var engine=(NikseComboBox)Get(form,"nikseComboBoxEngine");
        var engineChanged=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),form,typeof(AutoTranslate).GetMethod("nikseComboBoxEngine_SelectedIndexChanged",B));
        engine.SelectedIndexChanged-=engineChanged;
        engine.Items.Add(fake);engine.SelectedItem=fake;Set(form,"_autoTranslator",fake);
        var from=(NikseComboBox)Get(form,"comboBoxSource");from.Items.Clear();from.Items.Add(new TranslationPair("English","en"));from.SelectedIndex=0;
        var to=(NikseComboBox)Get(form,"comboBoxTarget");to.Items.Clear();to.Items.Add(new TranslationPair("Russian","ru"));to.SelectedIndex=0;
        phase=1;((Button)Get(form,"buttonTranslate")).PerformClick();
       }
       else if(!(bool)Get(form,"_translationInProgress"))
       {
        Check(fake.Calls==2,"button translated exactly two selected rows: "+mode);Check(fake.Inputs.SequenceEqual(new[]{"Cue 1","Cue 3"}),"uses correct source/original rows: "+mode);
        timer.Stop();form.DialogResult=DialogResult.OK;
       }
      }
      catch(Exception ex){error=ex;timer.Stop();form.DialogResult=DialogResult.Cancel;}
     };
     timer.Start();item.PerformClick();timer.Stop();if(error!=null)throw error;
    }
    var after=(Subtitle)Get(main,"_subtitle");
    foreach(var i in new[]{0,2,4})Check(after.Paragraphs[i].Text==(mode=="original"?"Old "+(i+1):"Cue "+i),"unselected row unchanged: "+mode+" "+i);
    foreach(var i in new[]{1,3})Check(after.Paragraphs[i].Text==(mode=="cancel"?"Cue "+i:"Translated Cue "+i),"selected result: "+mode+" "+i);
    Check(after.Paragraphs[3].Actor=="Actor 3" && after.Paragraphs[3].StartTime.TotalMilliseconds==6000,"metadata/timing preserved: "+mode);
    if(mode!="cancel"){Call(main,"UndoToIndex",true);var undone=(Subtitle)Get(main,"_subtitle");Check(undone.Paragraphs[1].Text==(mode=="original"?"Old 2":"Cue 1"),"single Undo restores text: "+mode);}
   }
   Call(main,"InitializeLanguage");var context=(ContextMenuStrip)Get(main,"contextMenuStripListView");var translatedGroup=(ToolStripMenuItem)Get(main,"toolStripMenuItemTranslateSelected");
   Check(translatedGroup.DropDownItems.Cast<ToolStripItem>().Count(i=>i.Name=="classicTranslateSelectedTogether")==1,"language refresh retains one nested context command");
   Check(translatedGroup.DropDownItems["genericTranslateToolStripMenuItem"]!=null,"original Classic translation command remains available");
   var finalGrid=(SubtitleListView)Get(main,"SubtitleListview1");finalGrid.SelectedIndices.Clear();Call(main,"RefreshClassicNavigation");
   Check(!translatedGroup.DropDownItems["classicTranslateSelectedTogether"].Enabled,"nested command disabled without selected cues");
   Configuration.Settings.General.Language="en-US";Call(main,"InitializeLanguage");
   var englishItem=translatedGroup.DropDownItems["classicTranslateSelectedTogether"];
   Check(englishItem.Text=="Translate and replace in place…","nested command follows English UI language");
   Check(englishItem.ToolTipText.Contains("other cues are retained"),"tooltip follows English UI language");
  }
  Console.WriteLine("SELECTED MENU CHECKS: "+checks);return 0;
 }
}
