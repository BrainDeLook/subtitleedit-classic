using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.BluRaySup;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Forms;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Core.VobSub;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.VideoPlayers;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
class TestClassicSynchronization
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static object Call(object o,string n,params object[] a){try{return o.GetType().GetMethod(n,F).Invoke(o,a);}catch(TargetInvocationException e){throw e.InnerException;}}
 static Type Internal(string n)=>typeof(Main).Assembly.GetType("Nikse.SubtitleEdit."+n,true);
 static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS: "+text);}
 static void Show(Form f){f.ShowInTaskbar=false;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();}
 static Subtitle Sample(){var s=new Subtitle { OriginalFormat=new AdvancedSubStationAlpha(),Header="[Script Info]\nPlayResX: 1920\nPlayResY: 1080"};for(int i=0;i<5;i++)s.Paragraphs.Add(new Paragraph("Cue "+i,1000+i*5000,2000+i*5000){Number=i+1,Actor="Actor",Extra="Default"});return s;}
 static SortedDictionary<int,(TimeSpan Time,int OtherIndex)> Points(params double[] times){var p=new SortedDictionary<int,(TimeSpan,int)>();for(int i=0;i<times.Length;i++)p[i*2]=(TimeSpan.FromMilliseconds(times[i]),i*2);return p;}
 static Subtitle Apply(Subtitle s,SortedDictionary<int,(TimeSpan Time,int OtherIndex)> p)=>(Subtitle)Internal("Logic.ClassicPointSync").GetMethod("Apply").Invoke(null,new object[]{s,p});
 static void Select(SubtitleListView v,int index){v.SelectedItems.Clear();v.SelectIndexAndEnsureVisible(index);}
 static void Run(string exe,string args){using(var p=Process.Start(new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true})){var error=p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception(error);}}
 static async Task Until(Func<bool> done){for(int i=0;i<1000;i++){if(done())return;await Task.Delay(20);}throw new TimeoutException("Native sync player did not become ready");}
 static async Task<object> Reference(string path,int? track=null,CancellationToken token=default(CancellationToken))
 {
  var task=(Task)Internal("Logic.ClassicSyncReference").GetMethod("LoadAsync").Invoke(null,new object[]{null,path,token,track});await task;return task.GetType().GetProperty("Result").GetValue(task);
 }
 static Subtitle RefSub(object r)=>(Subtitle)r.GetType().GetField("Subtitle").GetValue(r);
 static List<Func<Bitmap>> RefImages(object r)=>(List<Func<Bitmap>>)r.GetType().GetField("Images").GetValue(r);
 static byte[] Join(params byte[][] parts)=>parts.SelectMany(p=>p).ToArray();
 static byte[] Ebml(uint id,params byte[][] parts){var data=Join(parts);var bytes=BitConverter.GetBytes(id).AsEnumerable().Reverse().SkipWhile(b=>b==0).ToArray();var size=data.Length<127?new[]{(byte)(128+data.Length)}:new[]{(byte)(64+(data.Length>>8)),(byte)data.Length};return Join(bytes,size,data);}
 static byte[] DvbSegment(byte type,byte[] data)=>Join(new byte[]{15,type,0,1,(byte)(data.Length>>8),(byte)data.Length},data);
 static void CreateDvbReference(string file)
 {
  // A minimal standards-shaped Matroska track with one DVB display set, not a mock decoder.
  var line=Join(new byte[]{18},Enumerable.Repeat((byte)1,100).ToArray(),new byte[]{0,0,240});var field=Join(Enumerable.Repeat(line,10).ToArray());
  var objects=Join(new byte[]{0,1,0,(byte)(field.Length>>8),(byte)field.Length,(byte)(field.Length>>8),(byte)field.Length},field,field);
  var payload=Join(new byte[]{32,0},DvbSegment(16,new byte[]{5,4,1,0,0,10,0,10}),DvbSegment(17,new byte[]{1,8,0,100,0,20,44,1,0,0,0,1,0,0,0,0}),DvbSegment(18,new byte[]{1,0,1,33,235,128,128,0}),DvbSegment(19,objects),DvbSegment(128,new byte[0]),new byte[]{255});
  var header=Ebml(0x1A45DFA3,Ebml(0x4286,new byte[]{1}),Ebml(0x42F7,new byte[]{1}),Ebml(0x42F2,new byte[]{4}),Ebml(0x42F3,new byte[]{8}),Ebml(0x4282,Encoding.ASCII.GetBytes("matroska")),Ebml(0x4287,new byte[]{4}),Ebml(0x4285,new byte[]{2}));
  var info=Ebml(0x1549A966,Ebml(0x2AD7B1,new byte[]{15,66,64}),Ebml(0x4489,BitConverter.GetBytes(3000.0).AsEnumerable().Reverse().ToArray()));
  var tracks=Ebml(0x1654AE6B,Ebml(0xAE,Ebml(0xD7,new byte[]{1}),Ebml(0x73C5,new byte[]{1}),Ebml(0x83,new byte[]{17}),Ebml(0x86,Encoding.ASCII.GetBytes("S_DVBSUB")),Ebml(0x63A2,new byte[]{0,1,0,1})));
  var cluster=Ebml(0x1F43B675,Ebml(0xE7,new byte[]{0}),Ebml(0xA0,Ebml(0xA1,Join(new byte[]{129,3,232,0},payload)),Ebml(0x9B,new byte[]{3,232})));
  File.WriteAllBytes(file,Join(header,Ebml(0x18538067,info,tracks,cluster)));
 }
 [STAThread]static int Main(string[] args)
 {
  try{
   var root=Path.Combine(Path.GetFullPath(args[0]),"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Console.WriteLine("TEST DATA: "+root);
   typeof(Configuration).GetField("DataDirectory",BindingFlags.Public|BindingFlags.Static).SetValue(null,root+Path.DirectorySeparatorChar);
   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();int exit=0;
   using(var anchor=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),Size=new Size(1,1)}){anchor.Shown+=async(s,e)=>{try{await Unit(root,args[1]);}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{anchor.Close();}};Application.Run(anchor);}return exit;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
 static async Task Unit(string root,string ffmpeg)
 {
  Configuration.Settings.General.FFmpegLocation=ffmpeg;
  var source=Sample();var snapshot=source.Paragraphs[0].Id;
  var one=Apply(source,Points(1700));Check(one.Paragraphs[0].StartTime.TotalMilliseconds==1700 && one.Paragraphs[4].StartTime.TotalMilliseconds==21700,"Single point offsets the whole subtitle from its frozen source");
  Check(source.Paragraphs[0].StartTime.TotalMilliseconds==1000 && one.Paragraphs[0].Id==snapshot && one.Header==source.Header && one.Paragraphs[0].Actor=="Actor","Synchronization preserves source, IDs, actor and ASS metadata");
  var multi=Apply(source,Points(1700,12700,24700));Check(multi.Paragraphs[0].StartTime.TotalMilliseconds==1700 && multi.Paragraphs[2].StartTime.TotalMilliseconds==12700 && multi.Paragraphs[4].StartTime.TotalMilliseconds==24700,"Piecewise synchronization lands all three anchors exactly");
  Check(Apply(source,Points(1700,1000))==null && Apply(source,Points(-100))==null,"Reversed anchors and negative extrapolated times are rejected atomically");
  var overlap=Sample();overlap.Paragraphs[0].EndTime.TotalMilliseconds=7000;Check(Apply(overlap,Points(1700)).Paragraphs[0].EndTime.TotalMilliseconds>Apply(overlap,Points(1700)).Paragraphs[1].StartTime.TotalMilliseconds,"Point synchronization retains intentional cue overlaps");
  var seek=Internal("Logic.ClassicSyncKeys").GetMethod("SeekSeconds");Configuration.Settings.Shortcuts.MainAdjustStartXMsBack="";Configuration.Settings.Shortcuts.MainAdjustEndXMsBack="";Configuration.Settings.Shortcuts.MainAdjustStartXMsForward="";Configuration.Settings.Shortcuts.MainAdjustEndXMsForward="";
  Check((double)seek.Invoke(null,new object[]{Keys.Control|Keys.Left})==-.1 && (double)seek.Invoke(null,new object[]{Keys.Alt|Keys.Right})==.5 && (double)seek.Invoke(null,new object[]{Keys.Control|Keys.Shift|Keys.Left})==-1,"Fine seek shortcuts use the correct direction and increments");
  Configuration.Settings.Shortcuts.MainAdjustStartXMsBack="Control+Left";Configuration.Settings.Tools.MoveStartEndMs=275;Check((double)seek.Invoke(null,new object[]{Keys.Control|Keys.Left})==-.275,"Configured timing shortcut takes precedence over default fine seeking");
  var peaks=new WavePeakData(10,Enumerable.Range(0,400).Select(i=>new WavePeak((short)500,(short)-500)).ToArray());
  foreach(var lang in new[]{"en-US","ru-RU"})
  {
   Configuration.Settings.General.Language=lang;LanguageSettings.Current=lang=="en-US"?new Language():Language.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Languages","ru-RU.xml"));
   using(var point=new SetSyncPoint { SharedWavePeaks=peaks,SharedWaveVideo=null })
   {
    point.Initialize(source,null,1,null,-1);Show(point);var input=(NikseTimeUpDown)Field(point,"timeUpDownLine");Check(input.TimeCode.TotalMilliseconds==6000,"No-video point window starts at the selected cue: "+lang);
    Call(point,"buttonStartThreeSecondsAhead_Click",null,EventArgs.Empty);Check(input.TimeCode.TotalMilliseconds==7000 && source.Paragraphs[1].StartTime.TotalMilliseconds==6000,"One-second point nudge works without video and cannot alter source timing: "+lang);
    Call(point,"SeekClassicPoint",-5.0);Check(input.TimeCode.TotalMilliseconds==0,"No-video seeking clamps at zero: "+lang);
    var waveObject=Field(point,"_classicWave");var wave=(AudioVisualizer)waveObject.GetType().GetField("Wave").GetValue(waveObject);Check(wave.WavePeaks==peaks && wave.Locked && wave.Visible,"Point window reuses actual cached peaks read-only: "+lang);
    point.Size=new Size(1000,750);Check(wave.Bottom<=((Label)Field(point,"labelSubtitle")).Top && wave.Top>=((VideoPlayerContainer)Field(point,"videoPlayerContainer1")).Bottom,"Resizing keeps waveform below the video and above controls: "+lang);
    point.SharedWaveVideo="different.mkv";Call(point,"UpdateClassicPointWave");Check(!wave.Visible,"Cached peaks are hidden for a different video: "+lang);
   }
   using(var visual=new VisualSync(Application.OpenForms[0]) { SharedWavePeaks=peaks,SharedWaveVideo=null })
   {
    Call(visual,"Initialize",null,source,null,null,"Visual sync",24.0,false);Show(visual);
    Check((bool)Call(visual,"ApplyClassicManual",1.1,.5),"Manual speed and offset apply: "+lang);Check(visual.Paragraphs[0].StartTime.TotalMilliseconds==1600 && visual.Paragraphs[0].Id==snapshot,"Manual transform preserves IDs and computes speed before offset: "+lang);
    Call(visual,"ApplyClassicManual",1.1,.5);Check(visual.Paragraphs[0].StartTime.TotalMilliseconds==1600,"Repeated manual Apply is idempotent: "+lang);Check(!(bool)Call(visual,"ApplyClassicManual",1.0,-10.0) && visual.Paragraphs[0].StartTime.TotalMilliseconds==1600,"Invalid manual transform leaves the displayed result intact: "+lang);
    var left=Field(visual,"_classicStartWave");var right=Field(visual,"_classicEndWave");Call(left,"SetHeight",130);Check((int)left.GetType().GetProperty("RequestedHeight").GetValue(left)==130,"Waveform separator height is adjustable: "+lang);
    var preview=(Subtitle)Call(visual,"ClassicVisualPreview");Check(preview.Header==source.Header && preview.OriginalFormat is AdvancedSubStationAlpha,"Visual playback retains ASS style context: "+lang);
    // Restore baseline to close the offscreen test without a user confirmation.
    Call(visual,"ApplyClassicManual",1.0,0.0);visual.Close();
   }
   using(var form=new SyncPointsSync())
   {
    form.Initialize(source,null,null,-1,"reference",one);Show(form);var reference=Activator.CreateInstance(Internal("Logic.ClassicSyncReference"));RefSub(reference).Paragraphs.AddRange(one.Paragraphs);Call(form,"SetClassicReference",reference,"reference.srt");
    Check(!((Button)Field(form,"buttonOK")).Enabled,"Point synchronization cannot apply an empty point list: "+lang);
    var l=(SubtitleListView)Field(form,"subtitleListView1");var r=(SubtitleListView)Field(form,"subtitleListView2");Select(l,0);Select(r,0);Call(form,"SetSyncPointViaOtherSubtitle");Select(l,4);Select(r,4);Call(form,"SetSyncPointViaOtherSubtitle");
    var points=(SortedDictionary<int,(TimeSpan Time,int OtherIndex)>)Field(form,"_synchronizationPoints");Check(points.Keys.SequenceEqual(new[]{0,4}),"Points are sorted by source cue: "+lang);
    Select(l,0);Select(r,1);Call(form,"SetSyncPointViaOtherSubtitle");Check(points.Count==2 && points[0].OtherIndex==1,"Repointing a cue replaces its existing anchor: "+lang);Select(r,0);Call(form,"SetSyncPointViaOtherSubtitle");
    Call(form,"buttonSync_Click",null,EventArgs.Empty);Call(form,"buttonSync_Click",null,EventArgs.Empty);Check(form.FixedSubtitle.Paragraphs[0].StartTime.TotalMilliseconds==1700 && points.Count==2,"Repeated Apply keeps anchors and avoids accumulating offsets: "+lang);
    Check(l.ColumnIndexGap>=0 && r.ColumnIndexGap>=0 && l.Items[0].SubItems[l.ColumnIndexGap].BackColor!=l.BackColor,"Both reference grids display local green long-gap badges: "+lang);
    var list=(NikseListBox)Field(form,"listBoxSyncPoints");list.SelectedIndex=1;Call(form,"RemoveClassicPoint");Check(points.Count==1 && points.ContainsKey(0),"Removing a selected list point removes that point: "+lang);list.SelectedIndex=0;Call(form,"RemoveClassicPoint");Check(!((Button)Field(form,"buttonOK")).Enabled,"Removing the last point disables Apply and OK: "+lang);
    var browse=(Button)Field(form,"_classicBrowse");var viaVideo=(Button)Field(form,"_classicViaVideo");Check(browse.Visible && viaVideo.Visible && browse.Bottom<viaVideo.Top && viaVideo.Bottom<((Button)Field(form,"buttonSetSyncPoint")).Top,"Reference and video-point controls fit between the grids: "+lang);
    using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(root,"point-sync-"+lang+".png"));}
   }
  }
  var a=Path.Combine(root,"first.srt");var b=Path.Combine(root,"second.srt");File.WriteAllText(a,"1\n00:00:01,000 --> 00:00:02,000\nFirst track\n");File.WriteAllText(b,"1\n00:00:03,000 --> 00:00:04,000\nSecond track\n");
  Check(RefSub(await Reference(b)).Paragraphs[0].Text=="Second track","Plain subtitle reference loads with its timing and text");
  foreach(var extension in new[]{"mkv","mp4"})
  {
   var file=Path.Combine(root,"two-tracks."+extension);Run(ffmpeg,"-hide_banner -loglevel error -y -f lavfi -i color=s=160x90:d=5 -i \""+a+"\" -i \""+b+"\" -map 0:v -map 1:s -map 2:s -c:v libx264 -c:s "+(extension=="mp4"?"mov_text":"srt")+" \""+file+"\"");
   var reference=await Reference(file,1);Check(RefSub(reference).Paragraphs[0].Text=="Second track" && RefSub(reference).Paragraphs[0].StartTime.TotalMilliseconds==3000,"Selected second "+extension+" track is loaded rather than the first");
  }
  var sup=Path.Combine(root,"images.sup");using(var stream=File.Create(sup))using(var bitmap=new Bitmap(320,50)){using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);g.DrawString("Reference image",SystemFonts.DefaultFont,Brushes.White,5,5);}for(int i=0;i<2;i++){var bytes=BluRaySupPicture.CreateSupFrame(new BluRaySupPicture{Width=1920,Height=1080,StartTime=1000+5000*i,EndTime=2000+5000*i,CompositionNumber=i*2},bitmap,24,30,0,ContentAlignment.BottomCenter);stream.Write(bytes,0,bytes.Length);}}
  var images=await Reference(sup);Check(RefImages(images).Count==2 && RefSub(images).Paragraphs[1].StartTime.TotalMilliseconds==6000,"SUP image references load cue times without OCR");using(var bmp=RefImages(images)[0]())Check(bmp!=null && bmp.Width>1,"SUP reference image decodes for thumbnail and full preview");
  using(var form=new SyncPointsSync()){form.Initialize(source,null,null,-1,sup,RefSub(images));Show(form);Call(form,"SetClassicReference",images,sup);Check(!((Button)Field(form,"buttonFindTextOther")).Enabled && ((SubtitleListView)Field(form,"subtitleListView2")).SmallImageList.ImageSize.Height==64,"Image references display thumbnail rows and disable text search");using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(Point.Empty,form.Size));bmp.Save(Path.Combine(root,"point-sync-images.png"));}}
  var cancelled=new CancellationTokenSource();cancelled.Cancel();try{await Reference(a,null,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(true,"Reference loading honors cancellation before touching the file");}
  var pgsMkv=Path.Combine(root,"pgs.mkv");Run(ffmpeg,"-hide_banner -loglevel error -y -f lavfi -i color=s=160x90:d=8 -i \""+sup+"\" -map 0:v -map 1:s -c:v libx264 -c:s copy \""+pgsMkv+"\"");using(varRef reference=new varRef(await Reference(pgsMkv))){Check(RefImages(reference.Value).Count==2,"Matroska PGS image track loads without OCR");using(var bmp=RefImages(reference.Value)[1]())Check(bmp.Width>1,"Matroska PGS image reference decodes correctly");}
  var video=Path.Combine(root,"two-audio.mkv");Run(ffmpeg,"-hide_banner -loglevel error -y -f lavfi -i color=s=160x90:d=8 -f lavfi -i sine=frequency=440:duration=8 -f lavfi -i sine=frequency=880:duration=8 -map 0:v -map 1:a -map 2:a -c:v libx264 -c:a aac -metadata:s:a:0 language=tur -metadata:s:a:1 language=eng \""+video+"\"");
  var vob=Path.Combine(root,"vob.sub");using(var writer=new VobSubWriter(vob,720,480,20,10,32,Color.White,Color.Black,false,new DvdSubtitleLanguage("en","English","English")))using(var bitmap=new Bitmap(300,45)){using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);g.DrawString("Bitmap reference",SystemFonts.DefaultFont,Brushes.White,3,3);}writer.WriteParagraph(new Paragraph("",1000,2000),bitmap,ContentAlignment.BottomCenter);writer.WriteIdxFile();}
  foreach(var extension in new[]{"mkv","mp4"}){var file=Path.Combine(root,"vob."+extension);Run(ffmpeg,"-hide_banner -loglevel error -y -i \""+video+"\" -i \""+Path.ChangeExtension(vob,"idx")+"\" -map 0:v -map 1:s -c copy \""+file+"\"");using(varRef reference=new varRef(await Reference(file))){Check(RefImages(reference.Value).Count==1,"VobSub "+extension+" reference is recognized as an image track");using(var bmp=RefImages(reference.Value)[0]())Check(bmp.Width>1,"VobSub "+extension+" reference decodes without a missing-palette failure");}}
  var dvb=Path.Combine(root,"dvb.mkv");CreateDvbReference(dvb);using(varRef reference=new varRef(await Reference(dvb))){Check(RefImages(reference.Value).Count==1 && RefSub(reference.Value).Paragraphs[0].StartTime.TotalMilliseconds==1000,"Matroska DVB reference loads image and block timing without OCR");using(var bmp=RefImages(reference.Value)[0]())Check(bmp.GetPixel(10,10).A>0,"DVB object and palette produce a visible reference image");}
  var native=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"libmpv-2.dll");if(!File.Exists(native))throw new FileNotFoundException("MPV runtime required for native synchronization checks",native);File.Copy(native,Path.Combine(root,"libmpv-2.dll"),true);
  Configuration.Settings.General.VideoPlayer="MPV";Configuration.Settings.General.FFmpegLocation=ffmpeg;Configuration.Settings.General.MpvExtraOptions="--ao=null --vo=null";Configuration.Settings.General.VideoPlayerDefaultVolume=0;
  using(var point=new SetSyncPoint {SharedWavePeaks=peaks,SharedWaveVideo=video})
  {
   point.Initialize(source,null,1,video,1);Show(point);var player=(VideoPlayerContainer)Field(point,"videoPlayerContainer1");await Until(()=>player.VideoPlayer is LibMpvDynamic mpv && mpv.AudioTracks.Count==2 && mpv.AudioTrackNumber==1);
   Check(((LibMpvDynamic)player.VideoPlayer).AudioTrackNumber==1,"Real point player retains the selected second audio track");
   var input=(NikseTimeUpDown)Field(point,"timeUpDownLine");input.Focus();input.TimeCode=TimeCode.FromSeconds(7.123);Call(point,"SeekClassicPoint",2.0);input.TimeCode=TimeCode.FromSeconds(7.123);await Task.Delay(150);Call(point,"timer1_Tick",null,EventArgs.Empty);Check(input.TimeCode.TotalMilliseconds==7123,"Playing-video timer does not overwrite a focused manual sync time");
   using(var bmp=new Bitmap(point.Width,point.Height)){point.DrawToBitmap(bmp,new Rectangle(Point.Empty,point.Size));bmp.Save(Path.Combine(root,"set-point-waveform.png"));}
  }
  using(var visual=new VisualSync(Application.OpenForms[0]){VideoFileName=video,AudioTrackNumber=1,SharedWavePeaks=peaks,SharedWaveVideo=video})
  {
   Call(visual,"Initialize",null,source,null,null,"Visual sync",24.0,false);Show(visual);var left=(VideoPlayerContainer)Field(visual,"MediaPlayerStart");var right=(VideoPlayerContainer)Field(visual,"MediaPlayerEnd");await Until(()=>left.VideoPlayer is LibMpvDynamic l && right.VideoPlayer is LibMpvDynamic r && l.AudioTracks.Count==2 && r.AudioTracks.Count==2 && l.AudioTrackNumber==1 && r.AudioTrackNumber==1);
   Check(true,"Both real visual-sync players use the selected second audio track");var leftWave=Field(visual,"_classicStartWave");var rightWave=Field(visual,"_classicEndWave");var aWave=(AudioVisualizer)leftWave.GetType().GetField("Wave").GetValue(leftWave);var bWave=(AudioVisualizer)rightWave.GetType().GetField("Wave").GetValue(rightWave);Call(visual,"UpdateClassicWaves");Check(aWave.WavePeaks==peaks && bWave.WavePeaks==peaks && aWave.Visible && bWave.Visible,"Both real visual-sync panes share the cached waveform");
   using(var bmp=new Bitmap(visual.Width,visual.Height)){visual.DrawToBitmap(bmp,new Rectangle(Point.Empty,visual.Size));bmp.Save(Path.Combine(root,"visual-sync-waveform.png"));}visual.Close();
  }
 }
 sealed class varRef:IDisposable { public readonly object Value;public varRef(object value){Value=value;}public void Dispose(){((IDisposable)Value).Dispose();} }
}
