using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic.ClassicEffects;
class TestClassicAssEffectApply
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethods(F).Single(m=>m.Name==n && m.GetParameters().Length==a.Length && m.GetParameters().Select((p,i)=>a[i]==null || p.ParameterType.IsInstanceOfType(a[i])).All(v=>v)).Invoke(o,a);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    static async Task Run()
    {
        var sub=new Subtitle{Header=AdvancedSubStationAlpha.DefaultHeader};
        sub.Paragraphs.Add(new Paragraph("Before",1000,2000));
        sub.Paragraphs.Add(new Paragraph("[Актёр] Первая часть эффекта меняется,"+Environment.NewLine+"затем появляется новая строка.",427140,432360){Actor="Speaker",Extra="Default"});
        sub.Paragraphs.Add(new Paragraph("After",440000,441000));sub.Renumber();
        using(var main=new Nikse.SubtitleEdit.Forms.Main())
        {
            main.ShowInTaskbar=false;main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-20000,-20000);main.Show();await Task.Delay(150);
            Call(main,"SetCurrentFormat",new AdvancedSubStationAlpha().FriendlyName);Set(main,"_subtitle",sub);Set(main,"_undoIndex",-1);
            var list=(ListView)Field(main,"SubtitleListview1");Call(list,"Fill",sub,null);list.Items[1].Selected=true;await Task.Delay(50);
            var original=new Paragraph(sub.Paragraphs[1],false);
            var result=new AdvancedEffectWordFlip3D().ApplyEffect(sub.Header,new System.Collections.Generic.List<SubtitleLineViewModel>{new SubtitleLineViewModel(original)},854,480,null).Select(p=>p.ToParagraph()).ToList();
            var expected=result.Select(p=>new Paragraph(p,false)).ToList();
            Call(main,"ApplyClassicAssEvents",new[]{1},result,"Apply advanced ASS effect",null);
            list.Items[2].Selected=true;list.Items[1].Selected=false;await Task.Delay(100);
            for(int i=0;i<expected.Count;i++)Check(sub.Paragraphs[i+1].Text==expected[i].Text && sub.Paragraphs[i+1].StartTime.TotalMilliseconds==expected[i].StartTime.TotalMilliseconds && sub.Paragraphs[i+1].EndTime.TotalMilliseconds==expected[i].EndTime.TotalMilliseconds,"Generated event survives selection change #"+i);
            Check(sub.Paragraphs.Count==expected.Count+2,"Source is replaced once, surrounding events retained");
            Check(sub.Paragraphs.Skip(1).Take(expected.Count).All(p=>p.Actor=="Speaker"),"Actor metadata retained");
            Call(main,"ToolStripMenuItemUndoClick",null,EventArgs.Empty);
            var restored=(Subtitle)Field(main,"_subtitle");Check(restored.Paragraphs.Count==3 && restored.Paragraphs[1].Text==original.Text,"One Undo restores the complete source cue");
            Call(main,"ResetSubtitle");main.Close();
        }
    }
    [STAThread]static int Main(string[] args)
    {
        var root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);typeof(Configuration).GetField("DataDirectory").SetValue(null,root+Path.DirectorySeparatorChar);typeof(Configuration).GetField("SettingsFileName").SetValue(null,Path.Combine(root,"Settings.xml"));Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);int exit=0;
        using(var anchor=new Form{ShowInTaskbar=false,Location=new Point(-30000,-30000),StartPosition=FormStartPosition.Manual}){anchor.Shown+=async(s,e)=>{try{await Run();}catch(Exception ex){Console.Error.WriteLine(ex);exit=1;}finally{anchor.Close();}};Application.Run(anchor);}return exit;
    }
}
