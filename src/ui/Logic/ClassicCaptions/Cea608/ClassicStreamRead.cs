using System.IO;
namespace Nikse.SubtitleEdit.Logic.ClassicCaptions.Cea608
{
    internal static class ClassicStreamRead
    {
        public static int ReadFully(this Stream stream,byte[] buffer,int offset,int count)
        {var total=0;while(total<count){var read=stream.Read(buffer,offset+total,count-total);if(read==0)break;total+=read;}return total;}
    }
}
