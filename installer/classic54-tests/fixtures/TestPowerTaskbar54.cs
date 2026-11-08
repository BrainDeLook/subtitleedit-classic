using System;
using System.Drawing;
using System.Reflection;
using System.Collections;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;
class TestPowerTaskbar54
{
 [STAThread]static int Main()
 {
  using(var request=ClassicPowerRequest.Acquire("local integration test"))
  {
   if(request==null)throw new Exception("Windows power request failed");
   request.Dispose();request.Dispose();
  }
  using(var form=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})
  {
   form.Show();Application.DoEvents();
   using(var outer=new ClassicTaskbarWork(form))using(var inner=new ClassicTaskbarWork(form)){Application.DoEvents();}
   var active=(IDictionary)typeof(ClassicTaskbarWork).GetField("Active",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
   if(active.Count!=0)throw new Exception("Taskbar work handle leaked");
  }
  Console.WriteLine("PASS: native Windows power acquisition/release, nested taskbar scope cleanup");return 0;
 }
}
