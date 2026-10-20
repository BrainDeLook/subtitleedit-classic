using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms;
class TestShotMarkerVisibility {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
 [STAThread] static int Main(string[] args){try{
 var root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);foreach(var field in new[]{"DataDirectory","SettingsFileName","WaveformsDirectory","AutoBackupDirectory","DictionariesDirectory"})typeof(Configuration).GetField(field).SetValue(null,field=="SettingsFileName"?Path.Combine(root,"Settings.xml"):root+Path.DirectorySeparatorChar);
 using(var wave=new AudioVisualizer{Size=new Size(500,180),ShowGridLines=false}) {
 var markers=new List<double>{1};var peaks=new List<WavePeak>();for(int i=0;i<1000;i++)peaks.Add(new WavePeak(1000,-1000));wave.WavePeaks=new WavePeakData(100,peaks);wave.ZoomFactor=1;wave.ShotChanges=markers;wave.SetPosition(0,new Subtitle(),1.5,-1,null);
 using(var visible=new Bitmap(500,180))using(var hidden=new Bitmap(500,180))using(var restored=new Bitmap(500,180)){
 wave.DrawToBitmap(visible,wave.ClientRectangle);Check(visible.GetPixel(100,5).ToArgb()==Color.AntiqueWhite.ToArgb(),"shot marker is initially visible");wave.ShowShotChanges=false;wave.DrawToBitmap(hidden,wave.ClientRectangle);Check(hidden.GetPixel(100,5)!=visible.GetPixel(100,5),"toggle removes only marker rendering");Check(ReferenceEquals(markers,wave.ShotChanges)&&wave.ShotChanges.Count==1,"hidden markers stay loaded");Check(hidden.GetPixel(150,5)==visible.GetPixel(150,5),"playhead remains unchanged");wave.ShowShotChanges=true;wave.DrawToBitmap(restored,wave.ClientRectangle);Check(restored.GetPixel(100,5)==visible.GetPixel(100,5),"marker can be shown again without regenerating");}}
 File.WriteAllText(Path.Combine(root,"ClassicWaveformModes.json"),"{\"HideShotChanges\":true}");
 using(var main=new Main()){var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;var menu=(ToolStripMenuItem)typeof(Main).GetField("optionsToolStripMenuItem",flags).GetValue(main);var found=menu.DropDownItems.Find("classicShotChangeVisibility",true);Check(found.Length==1,"one marker toggle is wired in the waveform menu");var toggle=(ToolStripMenuItem)found[0];var wave=(AudioVisualizer)typeof(Main).GetField("audioVisualizer",flags).GetValue(main);Check(!toggle.Checked&&!wave.ShowShotChanges,"hidden preference is restored on startup");toggle.Checked=true;Check(wave.ShowShotChanges,"menu checkbox updates native waveform");}
 return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
