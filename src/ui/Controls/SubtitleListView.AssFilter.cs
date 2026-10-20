using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Controls
{
    public sealed partial class SubtitleListView
    {
        private Predicate<Paragraph> _assRowFilter;
        private Subtitle _assFilterDocument;
        private bool _assFilterUpdating,_assFilterQueued,_assFilterGroupsCreated;
        private const int AssVisibleGroup=170000001,AssHiddenGroup=170000002;
        public bool HasAssRowFilter=>_assRowFilter!=null;
        public event EventHandler AssRowFilterChanged;
        public int AssVisibleRowCount=>Items.Cast<ListViewItem>().Count(i=>AssRowMatches(i));
        private bool AssRowMatches(ListViewItem item)=>_assRowFilter==null || item.Tag is Paragraph p && _assRowFilter(p);

        public void SetAssRowFilter(Predicate<Paragraph> predicate)
        {
            _assRowFilter=predicate;
            RefreshAssRowFilter();
        }
        private void SelectAssFilteredIndex(int index,bool focus)
        {
            if(index<0 || index>=Items.Count || !AssRowMatches(Items[index]))return;
            SelectNone();Items[index].Selected=true;Items[index].EnsureVisible();
            if(focus)Items[index].Focused=true;
        }

        public void RefreshAssRowFilter()
        {
            if(!IsHandleCreated || _assFilterUpdating)return;
            _assFilterUpdating=true;
            try
            {
                if(_assRowFilter==null)
                {
                    AssSend(Handle,0x109d,IntPtr.Zero,IntPtr.Zero); // LVM_ENABLEGROUPVIEW
                    AssSend(Handle,0x10a0,IntPtr.Zero,IntPtr.Zero); // LVM_REMOVEALLGROUPS
                    _assFilterGroupsCreated=false;
                }
                else
                {
                    var visible=new AssGroup{cbSize=(uint)Marshal.SizeOf(typeof(AssGroup)),mask=0x14,iGroupId=AssVisibleGroup,stateMask=0xffffffff,state=4};
                    var hidden=visible;hidden.iGroupId=AssHiddenGroup;hidden.state=1|2|4;
                    if(!_assFilterGroupsCreated)
                    {
                        AssGroupSend(Handle,0x1091,new IntPtr(-1),ref visible);
                        AssGroupSend(Handle,0x1091,new IntPtr(-1),ref hidden);
                        _assFilterGroupsCreated=true;
                    }
                    foreach(ListViewItem item in Items)
                    {
                        bool match=AssRowMatches(item);
                        if(!match && item.Selected)item.Selected=false;
                        var native=new AssItem{mask=0x100,iItem=item.Index,iGroupId=match?AssVisibleGroup:AssHiddenGroup};
                        AssItemSend(Handle,0x104c,IntPtr.Zero,ref native);
                    }
                    AssSend(Handle,0x109d,new IntPtr(1),IntPtr.Zero);
                    AssGroupSend(Handle,0x1093,new IntPtr(AssHiddenGroup),ref hidden);
                }
            }
            finally{_assFilterUpdating=false;}
            Invalidate();AssRowFilterChanged?.Invoke(this,EventArgs.Empty);
        }

        protected override void OnItemSelectionChanged(ListViewItemSelectionChangedEventArgs e)
        {
            if(!_assFilterUpdating && e.IsSelected && !AssRowMatches(e.Item)){e.Item.Selected=false;return;}
            base.OnItemSelectionChanged(e);
        }
        protected override void OnHandleCreated(EventArgs e){_assFilterGroupsCreated=false;base.OnHandleCreated(e);QueueAssFilter();}
        private void QueueAssFilter()
        {
            if(_assRowFilter==null || _assFilterUpdating || _assFilterQueued || !IsHandleCreated || IsDisposed)return;
            _assFilterQueued=true;
            BeginInvoke(new Action(()=>{_assFilterQueued=false;if(!IsDisposed)RefreshAssRowFilter();}));
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if(m.Msg==0x104d || m.Msg==0x1008 || m.Msg==0x1009 || m.Msg==0x1074 || m.Msg==0x104c)QueueAssFilter();
        }
        [StructLayout(LayoutKind.Sequential)]private struct AssGroup
        {
            public uint cbSize,mask;public IntPtr pszHeader;public int cchHeader;public IntPtr pszFooter;public int cchFooter,iGroupId;public uint stateMask,state,uAlign;
            public IntPtr pszSubtitle;public uint cchSubtitle;public IntPtr pszTask;public uint cchTask;public IntPtr pszDescriptionTop;public uint cchDescriptionTop;public IntPtr pszDescriptionBottom;public uint cchDescriptionBottom;
            public int iTitleImage,iExtendedImage,iFirstItem;public uint cItems;public IntPtr pszSubsetTitle;public uint cchSubsetTitle;
        }
        [StructLayout(LayoutKind.Sequential)]private struct AssItem
        {
            public uint mask;public int iItem,iSubItem;public uint state,stateMask;public IntPtr pszText;public int cchTextMax,iImage;public IntPtr lParam;public int iIndent,iGroupId;public uint cColumns;public IntPtr puColumns,piColFmt;public int iGroup;
        }
        [DllImport("user32.dll",EntryPoint="SendMessageW")]private static extern IntPtr AssSend(IntPtr h,int m,IntPtr w,IntPtr l);
        [DllImport("user32.dll",EntryPoint="SendMessageW")]private static extern IntPtr AssGroupSend(IntPtr h,int m,IntPtr w,ref AssGroup l);
        [DllImport("user32.dll",EntryPoint="SendMessageW")]private static extern IntPtr AssItemSend(IntPtr h,int m,IntPtr w,ref AssItem l);
    }
}
