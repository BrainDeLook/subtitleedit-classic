using System;
using System.Collections.Generic;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
namespace Nikse.SubtitleEdit.Logic.ClassicFormats
{
    // Media/frame clock conversion is inherited from the core stable 5.3 adapters.
    public abstract class ClassicSubtitleFormat : SubtitleFormat
    {
        public override List<string> AlternateNames=>new List<string>();
        public override bool HasPositionSupport=>false;
        public virtual bool StoresFrameNumbers=>IsFrameBased;
        protected static string GetJoinedText(List<string> lines)=>string.Join(Environment.NewLine,lines);
    }
}




