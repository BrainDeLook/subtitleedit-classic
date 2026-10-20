using System;
using System.Collections.Generic;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
namespace Nikse.SubtitleEdit.Logic.ClassicFormats
{
    // Media/frame clock conversion is inherited from the core stable 5.3 adapters.
    public abstract class ClassicSubtitleFormat : SubtitleFormat
    {
        public virtual List<string> AlternateNames=>new List<string>();
        public virtual bool HasPositionSupport=>false;
        public virtual bool StoresFrameNumbers=>IsFrameBased;
        protected static string JoinLines(List<string> lines)=>string.Join(Environment.NewLine,lines);
        protected static string JoinLinesTrimmed(List<string> lines)=>JoinLines(lines).Trim();
        protected static string GetJoinedText(List<string> lines)=>string.Join(Environment.NewLine,lines);
    }
}




