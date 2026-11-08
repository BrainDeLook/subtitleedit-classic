using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic
{
 public static class ClassicSyntaxGap
 {
    public static bool Enabled {get;private set;}
    private static bool _frames;private static int _from,_to;
    public static void Configure(ClassicEditorPreferences preferences){Enabled=preferences.ColorGapRange;_frames=preferences.GapRangeFrames;_from=preferences.GapRangeFrom;_to=preferences.GapRangeTo;}
    public static bool Contains(double milliseconds)
    {var value=_frames?milliseconds*Configuration.Settings.General.CurrentFrameRate/1000d:milliseconds;return Enabled && value>=_from-.0001 && value<=_to+.0001;}
 }
}
