using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;

namespace System.Runtime.CompilerServices { internal static class IsExternalInit {} }
namespace Nikse.SubtitleEdit.Logic.ClassicTiming
{
    public sealed class ForcedAlignerException : Exception
    {
        public string Details { get; }
        public ForcedAlignerException(string message,string details=null):base(message){Details=details;}
    }
    public static class ClassicAlignerContracts
    {
        public interface IRunner {Task<string> AlignAsync(string audioFileName,string textFileName,CancellationToken cancellationToken);}
        public interface IAudioSource
        {
            double TotalSeconds {get;}
            Task<string> ExtractWindowAsync(double startSeconds,double durationSeconds,CancellationToken cancellationToken);
        }
        public readonly record struct Cue(double StartSeconds,double EndSeconds);
        public static IReadOnlyList<Cue> ParseCues(string text)
        {
            var subtitle=new Subtitle();new SubRip().LoadSubtitle(subtitle,text.Replace("\r\n","\n").Split('\n').ToList(),string.Empty);
            return subtitle.Paragraphs.Select(p=>new Cue(p.StartTime.TotalSeconds,p.EndTime.TotalSeconds)).ToList();
        }
    }
}
