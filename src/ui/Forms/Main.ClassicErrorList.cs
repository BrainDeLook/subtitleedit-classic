using System;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private void ShowClassicErrorList()
        {
            if(!IsSubtitleLoaded){DisplaySubtitleNotLoadedMessage();return;}ReloadFromSourceView();
            var errors=ClassicErrorReport.Analyze(_subtitle,GetCurrentSubtitleFormat(),_subtitle.FileName);
            using(var form=new ClassicErrorList(errors,_subtitle.Paragraphs.Count,_subtitle.FileName))if(form.ShowDialog(this)==DialogResult.OK)NavigateClassicError(form.SelectedError);
        }
        private bool NavigateClassicError(ClassicLineError error)
        {
            if(error==null)return false;var index=_subtitle.Paragraphs.FindIndex(p=>p.Id==error.Cue.Id && p.Text==error.Cue.Text && p.StartTime.TotalMilliseconds==error.Cue.StartTime.TotalMilliseconds && p.EndTime.TotalMilliseconds==error.Cue.EndTime.TotalMilliseconds);
            if(index<0)return false;_subtitleListViewIndex=-1;SubtitleListview1.SelectIndexAndEnsureVisible(index,true);RefreshSelectedParagraph();if(mediaPlayer.VideoPlayer!=null)mediaPlayer.CurrentPosition=_subtitle.Paragraphs[index].StartTime.TotalSeconds;return true;
        }
    }
}
