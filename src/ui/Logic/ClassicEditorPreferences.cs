using System;
using System.IO;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic
{
 public enum ClassicExternalChangeMode { ReloadWhenUnmodified, AlwaysAsk, Never }
 public sealed class ClassicEditorPreferences
 {
    public ClassicExternalChangeMode ExternalChanges {get;set;}
    public bool SplitAtVideoKeepTextAtEnd {get;set;}
    public bool WaveformRuleProfile {get;set;}
    public bool ColorGapRange {get;set;}
    public bool GapRangeFrames {get;set;}
    public int GapRangeFrom {get;set;}
    public int GapRangeTo {get;set;}=100;
    public bool ExtraPlayerControls {get;set;}
    public string IgnoreCustomTagsPattern {get;set;}="";
    public ClassicCookieBrowser VideoCookiesBrowser {get;set;}
    public bool OcrAssaPosition {get;set;}
    public bool VobSubIsolateColors {get;set;}=true;
    public string SecondarySubtitleFont {get;set;}="Tahoma";
    public string MoveFollowingFrameBack {get;set;}="";
    public string MoveFollowingFrameForward {get;set;}="";
    public string MoveAllFrameBack {get;set;}="";
    public string MoveAllFrameForward {get;set;}="";
    public bool Microphone {get;set;}
    public string[] HiddenQuickToolbarCommands {get;set;}=Array.Empty<string>();
    public bool QuickToolbarBurnInMigrated {get;set;}
    public string MoveLineFrameBack {get;set;}="";
    public string MoveLineFrameForward {get;set;}="";
    public string PushStartBack {get;set;}="";
    public string PushEndForward {get;set;}="";
    public int MoveBoundaryMilliseconds {get;set;}=100;
    private static string FileName=>Path.Combine(Configuration.DataDirectory,"ClassicEditorPreferences.json");
    public static ClassicEditorPreferences Load()
    {
        try
        {
            var preferences=JsonConvert.DeserializeObject<ClassicEditorPreferences>(File.ReadAllText(FileName)) ?? new ClassicEditorPreferences();
            if(!Enum.IsDefined(typeof(ClassicCookieBrowser),preferences.VideoCookiesBrowser) || !Enum.IsDefined(typeof(ClassicExternalChangeMode),preferences.ExternalChanges) || preferences.MoveBoundaryMilliseconds<1 || preferences.MoveBoundaryMilliseconds>10000 || preferences.GapRangeFrom<0 || preferences.GapRangeTo<preferences.GapRangeFrom || preferences.GapRangeTo>99999)return new ClassicEditorPreferences();
            preferences.IgnoreCustomTagsPattern=preferences.IgnoreCustomTagsPattern ?? "";
            preferences.MoveLineFrameBack=preferences.MoveLineFrameBack ?? "";preferences.MoveLineFrameForward=preferences.MoveLineFrameForward ?? "";
            preferences.MoveFollowingFrameBack=preferences.MoveFollowingFrameBack ?? "";preferences.MoveFollowingFrameForward=preferences.MoveFollowingFrameForward ?? "";
            preferences.MoveAllFrameBack=preferences.MoveAllFrameBack ?? "";preferences.MoveAllFrameForward=preferences.MoveAllFrameForward ?? "";
            preferences.SecondarySubtitleFont=preferences.SecondarySubtitleFont ?? "Tahoma";
            preferences.HiddenQuickToolbarCommands=preferences.HiddenQuickToolbarCommands ?? Array.Empty<string>();
            preferences.PushStartBack=preferences.PushStartBack ?? "";preferences.PushEndForward=preferences.PushEndForward ?? "";
            return preferences;
        }
        catch(IOException){return new ClassicEditorPreferences();}catch(JsonException){return new ClassicEditorPreferences();}
    }
    public void Save()
    {
        if(!Enum.IsDefined(typeof(ClassicCookieBrowser),VideoCookiesBrowser) || !Enum.IsDefined(typeof(ClassicExternalChangeMode),ExternalChanges) || MoveBoundaryMilliseconds<1 || MoveBoundaryMilliseconds>10000 || GapRangeFrom<0 || GapRangeTo<GapRangeFrom || GapRangeTo>99999)throw new ArgumentException("Invalid editor preferences.");
        Directory.CreateDirectory(Path.GetDirectoryName(FileName));var temp=FileName+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,JsonConvert.SerializeObject(this,Formatting.Indented));if(File.Exists(FileName))File.Replace(temp,FileName,null);else File.Move(temp,FileName);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
 }
}
