using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
class TestClassicTimelinePlayback {
 static int count;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static void Mouse(Control control,string method,int x,int y){typeof(ClassicTrackTimeline).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(control,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,y,0)});}
 [STAThread] static int Main(string[] args){try{
 using(var host=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),ClientSize=new Size(1200,220)})
 using(var timeline=new ClassicTrackTimeline{Dock=DockStyle.Fill}) {
 host.Controls.Add(timeline);host.Show();Application.DoEvents();
 var sub=new Subtitle();for(int i=0;i<583;i++)sub.Paragraphs.Add(new Paragraph("Cue "+i,i*2000,i*2000+1600){Actor="Actor "+(i%12)});timeline.Subtitle=sub;
 timeline.Position=10;var origin=timeline.ViewStart;int fullInvalidations=0;timeline.Invalidated+=(s,e)=>{if(e.InvalidRect.Width>=timeline.Width)fullInvalidations++;};
 using(var before=new Bitmap(1200,220)) using(var after=new Bitmap(1200,220)){
 timeline.DrawToBitmap(before,timeline.ClientRectangle);for(int i=1;i<=60;i++)timeline.Position=10+i/60.0;Check(timeline.ViewStart==origin,"viewport stays stationary throughout playback");Check(fullInvalidations==0,"playback invalidates cursor strips rather than the whole panel");
 timeline.DrawToBitmap(after,timeline.ClientRectangle);int outside=0;for(int y=0;y<220;y++)for(int x=0;x<1200;x++)if((x<376||x>410)&&before.GetPixel(x,y)!=after.GetPixel(x,y))outside++;Check(outside==0,"cue boxes and text do not move during playback");}
 timeline.Position=42;Check(timeline.ViewStart>0 && timeline.ViewStart<42,"viewport advances when cursor reaches the edge");timeline.Position=2;Check(timeline.ViewStart<=2,"backward seek brings position into view");
 timeline.Position=10;timeline.SelectedIndex=1;int committed=-1;double start=0,end=0;timeline.CommitTime=(i,a,b)=>{committed=i;start=a;end=b;};
 // First cue is on track 0, at [0,1.6], and its body is away from either edge.
 Mouse(timeline,"OnMouseDown",170,36);var dragOrigin=timeline.ViewStart;timeline.Position=100;Check(timeline.ViewStart==dragOrigin,"playback cannot shift the viewport during a drag");Mouse(timeline,"OnMouseMove",194,36);Mouse(timeline,"OnMouseUp",194,36);Check(committed==0 && start>0 && Math.Abs(end-start-1.6)<.001,"drag commits the selected cue with duration preserved");
 sub.Paragraphs[0].Actor="New actor";timeline.RefreshContent();using(var shot=new Bitmap(1200,220)){timeline.DrawToBitmap(shot,timeline.ClientRectangle);shot.Save(Path.Combine(args[0],"timeline-playback.png"));}Check(timeline.Controls.Count==1,"track rebuild preserves a single scrollbar");
 host.Close();}
 Console.WriteLine("PASS TOTAL "+count);return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
