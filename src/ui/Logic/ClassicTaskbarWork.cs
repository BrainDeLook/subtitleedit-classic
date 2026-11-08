using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace Nikse.SubtitleEdit.Logic
{
 public sealed class ClassicTaskbarWork:IDisposable
 {
    private static readonly Dictionary<IntPtr,int> Active=new Dictionary<IntPtr,int>();
    private IntPtr _handle;
    public ClassicTaskbarWork(Form form)
    {
        while(form.Owner!=null)form=form.Owner;
        if(!form.IsHandleCreated || !TaskbarList.Supported)return;
        _handle=form.Handle;
        lock(Active)
        {
            Active.TryGetValue(_handle,out var count);Active[_handle]=count+1;
            if(count==0)Set(TaskbarButtonProgressFlags.Indeterminate);
        }
    }
    private void Set(TaskbarButtonProgressFlags state)
    {try{TaskbarList.SetProgressState(_handle,state);}catch(COMException error){Nikse.SubtitleEdit.Core.Common.SeLogger.Error(error,"Update taskbar progress");}}
    public void Dispose()
    {
        lock(Active)
        {
            if(_handle==IntPtr.Zero)return;
            if(Active[_handle]>1)Active[_handle]--;else{Active.Remove(_handle);Set(TaskbarButtonProgressFlags.NoProgress);}
            _handle=IntPtr.Zero;
        }
    }
 }
}
