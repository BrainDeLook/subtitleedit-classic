using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.ComponentModel;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
class TestQuickToolbar58 {
 const BindingFlags B=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static int checks;
 static object Get(object x,string n)=>x.GetType().GetField(n,B).GetValue(x);
 static object Call(object x,string n,params object[] args)=>x.GetType().GetMethods(B).First(m=>m.Name==n && m.GetParameters().Length==args.Length).Invoke(x,args);
 static void Check(bool yes,string text){if(!yes)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 [STAThread]static int Main(){int result=1;try{Run();Console.WriteLine("RESULT "+checks+" passed 0 failed");result=0;}catch(Exception e){Console.Error.WriteLine(e);}Environment.Exit(result);return result;}
 static void Run(){
  var root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  foreach(var f in new[]{"DataDirectory","SettingsFileName","DictionariesDirectory","WaveformsDirectory","AutoBackupDirectory"})typeof(Configuration).GetField(f).SetValue(null,f=="SettingsFileName"?Path.Combine(root,"Settings.xml"):root+Path.DirectorySeparatorChar);
  Configuration.Settings.General.Language="ru-RU";Configuration.Settings.General.CheckForUpdates=false;Configuration.Settings.General.AutoSave=false;
  Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
  using(var main=new Main()){
   main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-30000,-30000);main.Size=new Size(1700,850);main.Show();Application.DoEvents();
   var strip=(ToolStrip)Get(main,"toolStrip1");var buttons=strip.Items.OfType<ToolStripButton>().Where(x=>x.Name.StartsWith("classicQuick_")).ToArray();
   Check(buttons.Length==8,"eight quick functions created once");
   var original=strip.Items.Cast<ToolStripItem>().Where(x=>!x.Name.StartsWith("classicQuick")).ToArray();
   Call(main,"InitializeClassicQuickToolbar");Call(main,"InitializeClassicQuickToolbar");
   Check(strip.Items.OfType<ToolStripButton>().Count(x=>x.Name.StartsWith("classicQuick_"))==8,"reinitialization never duplicates buttons");
   Check(original.SequenceEqual(strip.Items.Cast<ToolStripItem>().Where(x=>!x.Name.StartsWith("classicQuick"))),"all existing toolbar items and ordering retained");
   Check(buttons.All(x=>x.Image!=null && !string.IsNullOrWhiteSpace(x.ToolTipText) && x.AccessibleName==x.Text),"all buttons have icons, tooltips and accessible labels");
   Check(buttons[0].ToolTipText.Contains("OCR") && buttons[0].Enabled,"burned-in OCR available without loaded subtitles");
   Check(!buttons[2].Enabled && !buttons[3].Enabled && !buttons[5].Enabled,"document/ASS actions disabled in empty SRT");
   var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Test",1000,2000));typeof(Main).GetField("_subtitle",B).SetValue(main,sub);
   typeof(Main).GetField("_currentSubtitleFormat",B).SetValue(main,new AdvancedSubStationAlpha());Call(main,"RefreshClassicQuickToolbarState");
   Check(buttons[2].Enabled && buttons[3].Enabled && buttons[5].Enabled,"loaded ASS enables TTS, translation and Draw");
   var media=Path.Combine(root,"fixture.wav");File.WriteAllBytes(media,new byte[1]);typeof(Main).GetField("_videoFileName",B).SetValue(main,media);typeof(Main).GetField("_videoInfo",B).SetValue(main,new VideoInfo());Call(main,"RefreshClassicQuickToolbarState");
   var eventKey=typeof(ToolStripItem).GetField("EventClick",B).GetValue(null);
   var events=typeof(Component).GetProperty("Events",B);
   foreach(var button in buttons.Where(x=>x.Enabled)){
    var command=(ToolStripMenuItem)button.Tag;var handlers=(EventHandlerList)events.GetValue(command);var saved=handlers[eventKey];handlers[eventKey]=null;
    int calls=0;EventHandler capture=(s,e)=>calls++;handlers.AddHandler(eventKey,capture);button.PerformClick();handlers[eventKey]=saved;
    Check(calls==1,"quick button invokes original command: "+command.Name);
   }
   var settings=(ToolStripMenuItem)strip.ContextMenuStrip.Items["classicQuickToolbarVisibility"];var toggle=(ToolStripMenuItem)settings.DropDownItems[0];toggle.PerformClick();
   Check(!buttons[0].Available && ClassicEditorPreferences.Load().HiddenQuickToolbarCommands.Contains(((ToolStripMenuItem)buttons[0].Tag).Name),"right-click visibility toggle is saved");
   toggle.PerformClick();Call(main,"SetLanguage","en-US");Check(buttons[0].ToolTipText.Contains("burned-in") && settings.Text=="Quick function buttons","button labels and visibility menu follow language changes");
   Call(main,"SetLanguage","ru-RU");main.Width=1700;Application.DoEvents();
   using(var image=new Bitmap(strip.Width,strip.Height)){strip.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"toolbar58.png"));}
   strip.Dock=DockStyle.None;strip.Width=400;strip.PerformLayout();Application.DoEvents();Check(buttons.Any(x=>x.IsOnOverflow),"narrow windows move quick buttons into native overflow");
   var native=(ToolStripButton)Get(main,"toolStripButtonBurnIn");native.Available=true;Call(main,"RefreshClassicQuickToolbarState");Check(!buttons[6].Available,"existing hard-sub button suppresses duplicate shortcut");main.Hide();
  }
 }
}
