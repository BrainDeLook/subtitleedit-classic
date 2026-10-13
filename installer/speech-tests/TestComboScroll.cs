using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;

class TestComboScroll
{
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wp,IntPtr lp);
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Call(object control,string method) { control.GetType().GetMethod(method,Flags).Invoke(control,null); }
    [STAThread] static int Main()
    {
        Application.EnableVisualStyles();
        using(var form=new Form{ClientSize=new Size(600,450),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})
        using(var combo=new NikseComboBox{Left=20,Top=20,Width=300,DropDownHeight=150,DropDownStyle=ComboBoxStyle.DropDownList})
        {
            form.Controls.Add(combo); form.Show();
            for(int cycle=0;cycle<2;cycle++)
            {
                combo.Items.Clear(); combo.Items.Add("small"); combo.SelectedIndex=0;
                Call(combo,"ShowListView"); Call(combo,"HideDropDown");
                combo.Items.Clear(); for(int i=0;i<60;i++) combo.Items.Add("Model "+i); combo.SelectedIndex=0;
                Call(combo,"ShowListView"); Application.DoEvents();
                var list=(ListView)combo.DropDownControl;
                if(!list.Scrollable){Console.WriteLine("FAIL: expanding from a short model list leaves scrolling disabled"); return 1;}
                int before=list.TopItem.Index;
                SendMessage(list.Handle,0x020A,new IntPtr(-120<<16),IntPtr.Zero); Application.DoEvents();
                if(list.TopItem.Index<=before){Console.WriteLine("FAIL: mouse wheel did not move the expanded list");return 2;}
                list.Items[59].EnsureVisible();
                if(list.TopItem.Index==0){Console.WriteLine("FAIL: last model is unreachable");return 3;}
                Call(combo,"HideDropDown");
            }
            combo.SelectedIndex=58;
            typeof(Control).GetMethod("OnMouseWheel",Flags).Invoke(combo,new object[]{new MouseEventArgs(MouseButtons.None,0,5,5,-120)});
            if(combo.SelectedIndex!=59){Console.WriteLine("FAIL: closed picker skips its last model");return 4;}
            Console.WriteLine("PASS: short/long model lists reopen with scrolling, native mouse wheel moves the list and the last model is reachable"); return 0;
        }
    }
}
