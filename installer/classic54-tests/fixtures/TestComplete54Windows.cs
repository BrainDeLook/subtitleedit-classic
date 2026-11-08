using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Reflection;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Forms.Options;
using Nikse.SubtitleEdit.Forms.Assa;
using Nikse.SubtitleEdit.Controls;
class TestComplete54Windows
{
 [STAThread]static int Main()
 {
  Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dictionaries"));Application.EnableVisualStyles();Configuration.Settings.General.Language="ru";
  using(var form=new ClassicEditorPreferencesForm())Shot(form,"editor54-ru.png");
  using(var form=new ClassicRemovedWordsForm("ru_RU"))Shot(form,"removed54-ru.png");
  var s=new Subtitle();s.Paragraphs.Add(new Paragraph("Пример текста",1000,3000));
  using(var form=new ClassicAdvancedEffects(s,"",null))Shot(form,"effects54-ru.png");
  using(var form=new ClassicAssaDraw(new Paragraph("{\\p1}m 20 20 l 200 20 200 90 20 90{\\p0}",0,2000),640,360,s))Shot(form,"draw54-ru.png");
  using(var host=new Form{Size=new Size(900,600),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000),ShowInTaskbar=false})
  using(var player=new VideoPlayerContainer{Dock=DockStyle.Fill})
  {
   host.Controls.Add(player);host.Show();Application.DoEvents();var size=player.PanelPlayer.Size;
   var click=typeof(VideoPlayerContainer).GetMethod("PanelPlayerMouseDown",BindingFlags.Instance|BindingFlags.NonPublic);var count=0;player.OnEmptyPlayerClicked+=(sender,e)=>count++;
   click.Invoke(player,new object[]{player,new MouseEventArgs(MouseButtons.Right,1,0,0,0)});if(count!=0)throw new Exception("Right click opened video");
   click.Invoke(player,new object[]{player,new MouseEventArgs(MouseButtons.Left,1,0,0,0)});if(count!=1)throw new Exception("Left click did not open video");
   player.ShowFullScreenControls();if(!player.ClassicFullscreenActive)throw new Exception("Fullscreen controls missing");player.HideControls(false);player.ShowControls();if(player.PanelPlayer.Size!=size)throw new Exception("Fullscreen controls resize native video");player.ShowNonFullScreenControls();if(player.ClassicFullscreenActive)throw new Exception("Fullscreen controls leaked");
  }
  Console.WriteLine("PASS: 4 Russian windows, mouse buttons and fullscreen lifecycle");return 0;
 }
 static void Shot(Form form,string file)
 {
  form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.ShowInTaskbar=false;form.Show();Application.DoEvents();form.PerformLayout();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,file));}
 }
}
