using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Forms.Tts;
using Nikse.SubtitleEdit.Logic;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicActors
{
 static BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
 static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);Console.WriteLine("PASS: "+msg);}
 [STAThread] static void Main(string[] args)
 {
  var folder=Path.GetFullPath(args[0]);Directory.CreateDirectory(folder);Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));
  typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,folder+Path.DirectorySeparatorChar);
  Configuration.Settings.General.FFmpegLocation=args[1];Configuration.Settings.General.Language="ru-RU";
  LanguageSettings.Current=Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages/ru-RU.xml"));
  var sub=new Subtitle();sub.Paragraphs.Add(new Paragraph("Alice one",1000,2000){Actor="Alice"});sub.Paragraphs.Add(new Paragraph("Bob",2200,2900){Actor="Bob"});sub.Paragraphs.Add(new Paragraph("Alice two",3000,4000){Actor="Alice"});sub.Paragraphs.Add(new Paragraph("Unnamed",4100,4500));
  using(var form=new TextToSpeech(sub,new AdvancedSubStationAlpha(),null,null))
  {
   form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
   var actors=(ListView)Get(form,"listViewActors");Check(actors.CheckBoxes && actors.Items.Count==3,"actor checkboxes include unnamed actor");
   foreach(ListViewItem i in actors.Items)i.Checked=i.Text=="Alice";
   Check((bool)Call(form,"CaptureSpeechActors"),"single actor accepted");
   var filtered=(Subtitle)Call(form,"SpeakingSubtitle",sub,true);
   Check(filtered.Paragraphs.Count==4 && filtered.Paragraphs[0].Text=="Alice one" && filtered.Paragraphs[2].Text=="Alice two" && filtered.Paragraphs[1].Text=="" && filtered.Paragraphs[3].Text=="","only selected actor reaches every speech engine; indices preserved");
   Check(sub.Paragraphs[1].Text=="Bob","source subtitle preserved");
   actors.Items.Cast<ListViewItem>().First(i=>i.Text=="Bob").Checked=true;Call(form,"CaptureSpeechActors");
   filtered=(Subtitle)Call(form,"SpeakingSubtitle",sub,true);Check(filtered.Paragraphs[1].Text=="Bob" && filtered.Paragraphs[3].Text=="","multiple actor selection");
   Call(form,"FillActorListView");Check(actors.CheckedItems.Count==2,"checkbox selection survives list rebuild");
   var engine=(Control)Get(form,"nikseComboBoxEngine");
   engine.Text="Qwen3 TTS (CrispASR / local)";Application.DoEvents();
   Check(actors.CheckedItems.Count==2,"actor selection survives Qwen switch");
   Call(form,"CaptureSpeechActors");filtered=(Subtitle)Call(form,"SpeakingSubtitle",sub,true);
   Check(filtered.Paragraphs[1].Text=="Bob" && filtered.Paragraphs[3].Text=="","Qwen receives only checked actors");
   using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(Path.Combine(folder,"actors.png"));}
   var settings=ClassicDubbingSettings.Load();Check(!settings.RemoveAllSpeech,"existing settings default to selective removal");
   ScopeDialog(form,folder,true,DialogResult.OK);
   Check(ClassicDubbingSettings.Load().RemoveAllSpeech,"all-speech option persists after OK");
   ScopeDialog(form,folder,false,DialogResult.Cancel);
   Check(ClassicDubbingSettings.Load().RemoveAllSpeech,"cancel leaves removal scope unchanged");
   ScopeDialog(form,folder,false,DialogResult.OK);
   Check(!ClassicDubbingSettings.Load().RemoveAllSpeech,"selected-actor option persists after OK");
   form.Close();
  }
  var video=Path.Combine(folder,"fixture.mp4");Run(args[1],"-nostdin -y -f lavfi -i color=c=blue:s=640x360:r=24:d=5 -f lavfi -i sine=frequency=220:duration=5 -c:v libx264 -preset ultrafast -c:a aac -shortest \""+video+"\"");
  using(var form=new GenerateVideoWithHardSubs(sub,new SubRip(),video,new VideoInfo{Width=640,Height=360,TotalSeconds=5,TotalMilliseconds=5000,FramesPerSecond=24,TotalFrames=120,Success=true},30,false))
  {
   form.GetType().GetField("_mpvOn",F).SetValue(form,false);form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
   foreach(var scale in new[]{1f,1.25f,1.5f})
   {
    if(scale!=1)form.Scale(new SizeF(scale,scale));Call(form,"LayoutClassicPixelFormat");
    if(scale==1)using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(folder,"burn-in.png"));}
    var group=(Control)Get(form,"groupBoxVideo");var pixel=(Control)Get(form,"nikseComboBoxPixelFormat");var label=(Control)Get(form,"labelPixelFormat");var encoder=(Control)Get(form,"comboBoxVideoEncoding");
    Check(pixel.Right<=group.ClientSize.Width-8 && label.Left>=encoder.Right+4,"pixel dropdown and Russian label fit video column at scale "+scale);
   }
   form.Close();
  }
  var original=Path.Combine(folder,"original.wav");var bg=Path.Combine(folder,"background.wav");WriteWav(original,220);WriteWav(bg,880);
  var ranges=ClassicDubbing.ExcludeProtectedRanges(new[]{new ClassicDubbing.SpeechRange(1,2),new ClassicDubbing.SpeechRange(3,4)},new[]{new ClassicDubbing.SpeechRange(3.4,3.6)});
  Check(ranges.Count==3,"unselected overlapping actor protected");
  var output=ClassicDubbing.ApplyRemovalScopeAsync(original,bg,folder,new ClassicDubbingSettings{RemoveAllSpeech=false},ranges,CancellationToken.None,null).GetAwaiter().GetResult();
  var a=ReadWav(original);var b2=ReadWav(bg);var c=ReadWav(output);
  foreach(var time in new[]{0.5,2.5,3.5,4.5})Check(Error(a,c,time)<0.0001,"original audio samples preserved at "+time);
  foreach(var time in new[]{1.5,3.2,3.8})Check(Error(b2,c,time)<0.0001,"separated background only inside selected cues at "+time);
  var allBackground=ClassicDubbing.ApplyRemovalScopeAsync(original,bg,folder,new ClassicDubbingSettings{RemoveAllSpeech=true},ranges,CancellationToken.None,null).GetAwaiter().GetResult();
  foreach(var time in new[]{0.5,2.5,3.5,4.5})Check(Error(b2,ReadWav(allBackground),time)<0.0001,"all-speech mode keeps separated background across video at "+time);
  var silent = Path.Combine(folder,"silence.wav");Run(args[1],"-nostdin -y -f lavfi -i anullsrc=r=44100:cl=stereo -t 5 -c:a pcm_s16le "+Q(silent));
  var muxed = Path.Combine(folder,"selective-dub.mkv");ClassicDubbing.MuxAsync(video,silent,output,muxed,null,CancellationToken.None,null).GetAwaiter().GetResult();
  var decoded = Path.Combine(folder,"muxed.wav");Run(args[1],"-nostdin -y -i "+Q(muxed)+" -map 0:a:0 -c:a pcm_s16le "+Q(decoded));
  Check(Error(ReadWav(decoded),c,2.5)<0.0001,"exported video retains original outside dubbing intervals");
  var multi=Path.Combine(folder,"multi.mkv");Run(args[1],"-nostdin -y -i "+Q(video)+" -i "+Q(original)+" -i "+Q(bg)+" -map 0:v -map 1:a -map 2:a -c copy "+Q(multi));
  var track=Path.Combine(folder,"track.wav");ClassicDubbing.ExtractOriginalAsync(multi,track,1,CancellationToken.None,null).GetAwaiter().GetResult();
  Check(Error(ReadWav(track),b2,1.5)<0.0001,"selected source audio track retained");
  var many=Enumerable.Range(0,1000).Select(i=>new ClassicDubbing.SpeechRange(i*.005,i*.005+.002)).ToArray();
  var watch=System.Diagnostics.Stopwatch.StartNew();ClassicDubbing.RestoreOutsideAsync(original,bg,Path.Combine(folder,"many"),many,CancellationToken.None,null).GetAwaiter().GetResult();Check(watch.Elapsed.TotalSeconds<30,"1000-cue graph avoids command-line/depth limits");
  using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{ClassicDubbing.RestoreOutsideAsync(original,bg,Path.Combine(folder,"cancel"),ranges,cancel.Token,null).GetAwaiter().GetResult();throw new Exception("cancel ignored");}catch(OperationCanceledException){Console.WriteLine("PASS: cancellation");}}
 }
 static void ScopeDialog(Form owner,string folder,bool all,DialogResult result)
 {
  bool found=false;using(var timer=new System.Windows.Forms.Timer{Interval=200})
  {
   timer.Tick+=(s,e)=>
   {
    var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f!=owner);if(dialog==null)return;
    timer.Stop();found=true;dialog.ShowInTaskbar=false;dialog.Location=new Point(-20000,-20000);
    var selected=(RadioButton)dialog.Controls.Find("radioRemoveSelectedActors",true).Single();
    var entire=(RadioButton)dialog.Controls.Find("radioRemoveAllSpeech",true).Single();
    if(all)entire.Checked=true;else selected.Checked=true;
    Check(entire.Checked!=selected.Checked,"removal modes are mutually exclusive");
    Check(selected.Right<selected.Parent.ClientSize.Width && entire.Right<entire.Parent.ClientSize.Width && entire.Bottom<entire.Parent.ClientSize.Height,"scope options fully visible");
    foreach(var button in dialog.Controls.OfType<Button>())Check(button.Bottom<=dialog.ClientSize.Height,"settings footer buttons fully visible");
    using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(folder,"scope-"+(all ? "all" : "selected")+".png"));}
    dialog.DialogResult=result;dialog.Close();
   };
   timer.Start();Call(owner,"EditDubbingSettings");
  }
  Check(found,"speech-removal settings dialog opened");
 }
 static string Q(string path)=>Nikse.SubtitleEdit.Logic.SpeechToText.ClassicSpeechRunner.Quote(path);
 static void WriteWav(string path,int frequency){using(var w=new BinaryWriter(File.Create(path))){int samples=44100*5;w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+samples*4);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)2);w.Write(44100);w.Write(176400);w.Write((short)4);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(samples*4);for(int i=0;i<samples;i++){var s=(short)(9000*Math.Sin(i*2*Math.PI*frequency/44100));w.Write(s);w.Write(s);}}}
 static short[] ReadWav(string p){using(var r=new BinaryReader(File.OpenRead(p))){r.ReadBytes(12);while(r.BaseStream.Position<r.BaseStream.Length){var name=System.Text.Encoding.ASCII.GetString(r.ReadBytes(4));int size=r.ReadInt32();if(name=="data"){var a=new short[size/2];for(int i=0;i<a.Length;i++)a[i]=r.ReadInt16();return a;}r.BaseStream.Position+=size+(size%2);}throw new Exception("no wave data");}}
 static double Error(short[] a,short[] b,double t){int start=(int)(t*44100)*2;double error=0;for(int i=start;i<start+4000;i++)error+=Math.Abs(a[i]-b[i])/32768.0;return error/4000;}
 static void Run(string exe,string args){using(var p=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe,"-hide_banner -loglevel error "+args){UseShellExecute=false,CreateNoWindow=true})){if(!p.WaitForExit(30000)||p.ExitCode!=0)throw new Exception("ffmpeg failed");}}
}
