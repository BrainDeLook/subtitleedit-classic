using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using Nikse.SubtitleEdit.Logic.ClassicTiming;
namespace Nikse.SubtitleEdit.Forms
{
 public sealed partial class Main
 {
    private ClassicEditorPreferences _classicEditorPreferences=new ClassicEditorPreferences();
    private FlowLayoutPanel _classicExtraPlayerControls;
    private ToolStripButton _classicMicrophoneButton;
    private ToolStripButton _classicWaveRuleProfile;
    private double? _classicRelativeSeekTarget;
    private Timer _classicSeekSample;
    private double _classicSeekSampleTarget;
    private void InitializeClassicEditor54()
    {
        _classicEditorPreferences=ClassicEditorPreferences.Load();
        NavigationAction(optionsToolStripMenuItem.DropDownItems,"classicEditorPreferences", "Дополнительные параметры редактора…","Additional editor settings…",()=>{using(var form=new ClassicEditorPreferencesForm())if(form.ShowDialog(this)==DialogResult.OK){_classicEditorPreferences=form.Preferences;RefreshClassicEditor54();UpdateListSyntaxColoring();}});
        if(_classicMicrophoneButton==null)
        {
            mediaPlayer.PositionOrPlaybackChanged += (sender, args) => { _classicRelativeSeekTarget = null; _classicSeekSample?.Stop(); };
            FormClosed += (sender, args) => _classicSeekSample?.Dispose();
            _classicWaveRuleProfile=new ToolStripButton{Text=ClassicAssistedText.L("Профиль правил…","Rule profile…"),DisplayStyle=ToolStripItemDisplayStyle.Text,Name="classicWaveRuleProfile"};_classicWaveRuleProfile.Click+=(sender,e)=>ChooseProfile();toolStripWaveControls.Items.Add(_classicWaveRuleProfile);
            _classicMicrophoneButton=new ToolStripButton{Text="🎙",DisplayStyle=ToolStripItemDisplayStyle.Text,Name="classicMicrophone"};toolStrip1.Items.Add(_classicMicrophoneButton);
            _classicMicrophoneButton.Click+=async(sender,e)=>await ToggleClassicMicrophone();
            _classicExtraPlayerControls=new FlowLayoutPanel{Name="classicExtraPlayerControls",AutoSize=true,WrapContents=false,BackColor=Color.FromArgb(240,240,240),Padding=new Padding(1)};
            var speed=new ComboBox{Name="classicPlaybackSpeed",Width=64,DropDownStyle=ComboBoxStyle.DropDownList};speed.Items.AddRange(new object[]{"0.5×","0.75×","1×","1.25×","1.5×","2×"});speed.SelectedIndex=2;
            speed.SelectedIndexChanged+=(sender,e)=>{if(mediaPlayer.VideoPlayer!=null)mediaPlayer.VideoPlayer.PlayRate=new[]{.5,.75,1,1.25,1.5,2}[speed.SelectedIndex];};
            var previous=new Button{Text="◀|",AutoSize=true,Name="classicPreviousCue"};previous.Click+=(sender,e)=>GoToPreviousSubtitle(mediaPlayer.CurrentPosition*1000);
            var next=new Button{Text="|▶",AutoSize=true,Name="classicNextCue"};next.Click+=(sender,e)=>GoToNextSubtitle(mediaPlayer.CurrentPosition*1000);
            var subtitles=new CheckBox{Text="CC",AutoSize=true,Checked=true,Name="classicSubtitleVisibility"};subtitles.CheckedChanged+=(sender,e)=>{mediaPlayer.PreviewSubtitlesVisible=subtitles.Checked;mediaPlayer.LastParagraph=null;UiUtil.ShowSubtitle(_subtitle,mediaPlayer,GetCurrentSubtitleFormat());};
            _classicExtraPlayerControls.Controls.AddRange(new Control[]{speed,previous,next,subtitles});mediaPlayer.Controls.Add(_classicExtraPlayerControls);
            mediaPlayer.ClassicFullscreenChanged+=(sender,e)=>_classicExtraPlayerControls.Visible=_classicEditorPreferences.ExtraPlayerControls && !mediaPlayer.ClassicFullscreenActive;
            mediaPlayer.Resize+=(sender,e)=>{_classicExtraPlayerControls.Location=new Point(8,Math.Max(0,mediaPlayer.Height-mediaPlayer.ControlsHeight-_classicExtraPlayerControls.Height-2));_classicExtraPlayerControls.BringToFront();};
            FormClosing+=(sender,e)=>{if(e.Cancel || _classicMicCancellation==null)return;e.Cancel=true;_classicMicCloseRequested=true;CancelClassicMicrophone();};
        }
        RefreshClassicEditor54();
    }
    private void RefreshClassicEditor54()
    {
        Nikse.SubtitleEdit.Core.Common.TextLengthCalculator.CalcFactory.SetIgnoreTagsPattern(_classicEditorPreferences.IgnoreCustomTagsPattern);
        ClassicSyntaxGap.Configure(_classicEditorPreferences);_classicWaveRuleProfile.Visible=_classicEditorPreferences.WaveformRuleProfile;
        _classicMicrophoneButton.Visible=_classicEditorPreferences.Microphone || _classicMicCancellation!=null;
        if(_classicMicCancellation==null)_classicMicrophoneButton.ToolTipText=ClassicAssistedText.L("Записать речь в текущую реплику (локальный ASR)","Record speech into the current cue (local ASR)");
        _classicExtraPlayerControls.Visible=_classicEditorPreferences.ExtraPlayerControls && !mediaPlayer.ClassicFullscreenActive;
        _classicExtraPlayerControls.Location=new Point(8,Math.Max(0,mediaPlayer.Height-mediaPlayer.ControlsHeight-_classicExtraPlayerControls.Height-2));_classicExtraPlayerControls.BringToFront();
    }
    private bool TryClassicEditor54Shortcut(Keys key)
    {
        if(Form.ActiveForm!=this)return false;
        var prefs=_classicEditorPreferences;
        var bindings=new[]{prefs.MoveLineFrameBack,prefs.MoveLineFrameForward,prefs.MoveFollowingFrameBack,prefs.MoveFollowingFrameForward,prefs.MoveAllFrameBack,prefs.MoveAllFrameForward,prefs.PushStartBack,prefs.PushEndForward};
        var action=Array.FindIndex(bindings,x=>!string.IsNullOrEmpty(x) && UiUtil.GetKeys(x)==key);if(action<0)return false;
        ApplyClassicTimingShortcut(action);
        return true;
    }
    private void ApplyClassicTimingShortcut(int action)
    {
        var prefs=_classicEditorPreferences;
        var indices=SubtitleListview1.GetSelectedIndices();if(indices.Length==0)return;
        var edited=new Subtitle(_subtitle,false);bool changed;
        if(action<6)
        {
            var moved=action<2?indices:action<4?Enumerable.Range(indices.Min(),edited.Paragraphs.Count-indices.Min()).ToArray():Enumerable.Range(0,edited.Paragraphs.Count).ToArray();
            changed=EditorTiming54.MoveLines(edited,moved,(action%2==0?-1:1)*1000/Math.Max(1,CurrentFrameRate));
        }
        else changed=EditorTiming54.PushBoundary(edited,indices[0],action==6,prefs.MoveBoundaryMilliseconds,(int)MinGapBetweenLines,Configuration.Settings.General.SubtitleMinimumDisplayMilliseconds,Configuration.Settings.General.SubtitleMaximumDisplayMilliseconds);
        if(changed){MakeHistoryForUndo("Before timing shortcut");SaveSubtitleListviewIndices();_subtitle=edited;SubtitleListview1.Fill(_subtitle,_subtitleOriginal);RestoreSubtitleListviewIndices();RefreshSelectedParagraph();audioVisualizer.Invalidate();UpdateSourceView();}
    }
    private void ApplyClassicSameTimeStack(Paragraph before,Paragraph moved)
    {
        if(before==null || !(GetCurrentSubtitleFormat() is AdvancedSubStationAlpha || GetCurrentSubtitleFormat() is SubStationAlpha))return;
        foreach(var i in EditorTiming54.MoveSameTimeStack(_subtitle,before,moved))SubtitleListview1.SetStartTimeAndDurationBackground(i,_subtitle.Paragraphs[i],_subtitle.GetParagraphOrDefault(i+1),_subtitle.GetParagraphOrDefault(i-1));
    }
 }
}
