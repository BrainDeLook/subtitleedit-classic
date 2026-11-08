using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicPowerRequest : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] private struct ReasonContext { public uint Version, Flags; public IntPtr Reason; }
        [DllImport("kernel32.dll", SetLastError=true)] private static extern IntPtr PowerCreateRequest(ref ReasonContext context);
        [DllImport("kernel32.dll", SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool PowerSetRequest(SafeFileHandle handle,int type);
        [DllImport("kernel32.dll", SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool PowerClearRequest(SafeFileHandle handle,int type);
        private SafeFileHandle _handle; private IntPtr _reason;
        private ClassicPowerRequest(SafeFileHandle handle,IntPtr reason){_handle=handle;_reason=reason;}
        public static ClassicPowerRequest Acquire(string reason)
        {
            if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return null;
            var text=Marshal.StringToHGlobalUni("Subtitle Edit Classic: "+reason);
            var context=new ReasonContext{Flags=1,Reason=text};var handle=new SafeFileHandle(PowerCreateRequest(ref context),true);
            if(handle.IsInvalid || !PowerSetRequest(handle,1)){SeLogger.Error("Unable to prevent sleep: "+Marshal.GetLastWin32Error());handle.Dispose();Marshal.FreeHGlobal(text);return null;}
            return new ClassicPowerRequest(handle,text);
        }
        public void Dispose(){var handle=Interlocked.Exchange(ref _handle,null);if(handle==null)return;PowerClearRequest(handle,1);handle.Dispose();Marshal.FreeHGlobal(_reason);_reason=IntPtr.Zero;}
    }
}
