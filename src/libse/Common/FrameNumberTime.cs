using System;
using System.Globalization;
using Nikse.SubtitleEdit.Core.SubtitleFormats;

namespace Nikse.SubtitleEdit.Core.Common
{
    public static class FrameNumberTime
    {
        public static int FromMilliseconds(double ms) => SubtitleFormat.MillisecondsToFrames(ms);
        public static double ToMilliseconds(int frame) => SubtitleFormat.FramesToMilliseconds(frame);
        public static double Snap(double ms) => ToMilliseconds(FromMilliseconds(ms));
        public static string FormatClock(double ms)
        {
            var rate = Math.Max(1, (int)Math.Round(Configuration.Settings.General.CurrentFrameRate));
            var f = Math.Abs((long)FromMilliseconds(ms)); var sec = f / rate;
            return (ms < 0 ? "-" : "") + string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}:{3:00}", sec / 3600, sec / 60 % 60, sec % 60, f % rate);
        }
        public static double ParseClock(int hours, int minutes, int seconds, int frame)
        {
            int rate = Math.Max(1, (int)Math.Round(Configuration.Settings.General.CurrentFrameRate));
            if (minutes < 0 || minutes > 59 || seconds < 0 || seconds > 59 || frame < 0 || frame >= rate) throw new FormatException("Invalid frame time code.");
            return ToMilliseconds(checked(((hours * 60 + minutes) * 60 + seconds) * rate + frame));
        }
    }
}
