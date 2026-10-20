using System;

namespace Nikse.SubtitleEdit.Logic.ClassicBroadcast.Transport
{
    public static class Helper
    {
        /// <summary>
        /// Reads bytes in big-endian order and returns as unsigned 32-bit integer
        /// </summary>
        /// <param name="buffer">Source byte array</param>
        /// <param name="count">Number of bytes to read (max 4)</param>
        /// <returns>Value as uint</returns>
        public static uint GetEndian(ReadOnlySpan<byte> buffer, int count)
        {
            uint result = 0;
            for (var i = 0; i < count; i++)
            {
                result = (result << 8) | buffer[i];
            }
            return result;
        }

        /// <summary>
        /// Reads bytes in big-endian order and returns as unsigned 32-bit integer
        /// </summary>
        /// <param name="buffer">Source byte array</param>
        /// <param name="index">Start index</param>
        /// <param name="count">Number of bytes to read (max 4)</param>
        /// <returns>Value as uint</returns>
        public static uint GetEndian(byte[] buffer, int index, int count)
        {
            return GetEndian(buffer.AsSpan(index, count), count);
        }

        /// <summary>
        /// Get two bytes word stored in big-endian order
        /// </summary>
        /// <param name="buffer">Byte array</param>
        /// <returns>Word as int</returns>
        public static int GetEndianWord(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length >= 2)
            {
                return (buffer[0] << 8) | buffer[1];
            }
            return 0;
        }

        /// <summary>
        /// Get two bytes word stored in big-endian order
        /// </summary>
        /// <param name="buffer">Byte array</param>
        /// <param name="index">Index in byte array</param>
        /// <returns>Word as int</returns>
        public static int GetEndianWord(byte[] buffer, int index)
        {
            return GetEndianWord(buffer.AsSpan(index));
        }

        /// <summary>
        /// Converts bytes to binary string representation (e.g., "10110101")
        /// </summary>
        /// <param name="buffer">Source byte array</param>
        /// <param name="index">Start index</param>
        /// <param name="count">Number of bytes to convert</param>
        /// <returns>Binary string representation</returns>
        public static string GetBinaryString(byte[] buffer, int index, int count)
        {
            if (count <= 0)
            {
                return string.Empty;
            }

            var chars = new char[count * 8];
            for (var i = 0; i < count; i++)
                for (var bit = 0; bit < 8; bit++)
                    chars[i * 8 + bit] = (buffer[index + i] & (1 << (7 - bit))) != 0 ? '1' : '0';
            return new string(chars);
        }
    }
}
