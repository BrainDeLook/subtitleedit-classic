using System;
namespace Nikse.SubtitleEdit.Core.ContainerFormats.ProgramStream
{
internal static class ProgramStreamClosedCaptionReader
{
        internal static int GetPesDataStart(ReadOnlySpan<byte> data, out long? pts)
        {
            pts = null;
            var length = data.Length;
            if (length < 3)
            {
                return -1;
            }

            if ((data[0] & 0xC0) == 0x80) // MPEG-2 PES header
            {
                var headerDataLength = data[2];
                if ((data[1] & 0x80) != 0 && length >= 8)
                {
                    pts = ReadPts(data, 3);
                }

                return 3 + headerDataLength;
            }

            // MPEG-1: stuffing, optional STD buffer, then PTS / PTS+DTS / nothing
            var i = 0;
            while (i < length && data[i] == 0xFF && i < 16)
            {
                i++;
            }

            if (i < length && (data[i] & 0xC0) == 0x40)
            {
                i += 2;
            }

            if (i >= length)
            {
                return -1;
            }

            if ((data[i] & 0xF0) == 0x20 && i + 5 <= length)
            {
                pts = ReadPts(data, i);
                return i + 5;
            }

            if ((data[i] & 0xF0) == 0x30 && i + 10 <= length)
            {
                pts = ReadPts(data, i);
                return i + 10;
            }

            return i + 1; // 0x0F = no timestamps
        }

        private static long ReadPts(ReadOnlySpan<byte> data, int index)
        {
            return ((long)(data[index] & 0x0E) << 29) | ((long)data[index + 1] << 22) | ((long)(data[index + 2] & 0xFE) << 14) |
                   ((long)data[index + 3] << 7) | ((long)data[index + 4] >> 1);
        }

}
}
