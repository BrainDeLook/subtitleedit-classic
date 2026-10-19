using System;
using System.Collections.Generic;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
namespace Nikse.SubtitleEdit.Logic.ClassicFormats
{
    // Classic uses media milliseconds internally; SE5's alternate SMPTE clock is not enabled here.
    public abstract class ClassicSubtitleFormat : SubtitleFormat
    {
        public virtual List<string> AlternateNames=>new List<string>();
        public virtual bool HasPositionSupport=>false;
        public virtual bool StoresFrameNumbers=>IsFrameBased;
        protected static Subtitle ToMediaTime(Subtitle source)=>source;
        protected static void FromMediaTime(Subtitle source){}
        protected static string JoinLines(List<string> lines)=>string.Join(Environment.NewLine,lines);
        protected static string JoinLinesTrimmed(List<string> lines)=>JoinLines(lines).Trim();
        protected static string GetJoinedText(List<string> lines)=>string.Join(Environment.NewLine,lines);
    }
}




