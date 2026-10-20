using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private int ClassicSelectedAudioIndex()
        {
            if(mediaPlayer.VideoPlayer is Nikse.SubtitleEdit.Logic.VideoPlayers.LibVlcDynamic vlc)
            {
                var tracks=vlc.GetAudioTracks().Where(t=>t.TrackNumber>=0).ToList();return Math.Max(0,tracks.FindIndex(t=>t.TrackNumber==VideoAudioTrackNumber));
            }
            return Math.Max(0,VideoAudioTrackNumber);
        }
        private ToolStripMenuItem _classicImproveTimeCodesMenu;
        private void InitializeClassicTimingMenu()
        {
            if(_classicImproveTimeCodesMenu==null)
            {
                _classicImproveTimeCodesMenu=new ToolStripMenuItem { Name="classicImproveTimeCodes" };toolsToolStripMenuItem.DropDownItems.Add(_classicImproveTimeCodesMenu);
                _classicImproveTimeCodesMenu.Click+=(s,e)=>
                {
                    if(_subtitle.Paragraphs.Count==0 || _videoInfo==null || !File.Exists(_videoFileName)){MessageBox.Show(Configuration.Settings.General.Language.StartsWith("ru") ? "Открой субтитры и локальное видео или аудио." : "Open subtitles and a local video/audio file.");return;}
                    if(!RequireFfmpegOk())return;
                    var previewSerial=0;
                    using(var form=new ClassicImproveTimeCodes(_subtitle,_videoFileName,_videoInfo.TotalSeconds,CurrentFrameRate,at=>
                    {
                        var serial=++previewSerial;mediaPlayer.Pause();mediaPlayer.CurrentPosition=at;mediaPlayer.Play();
                        Nikse.SubtitleEdit.Logic.TaskDelayHelper.RunDelayed(TimeSpan.FromSeconds(5),()=>{if(serial==previewSerial)mediaPlayer.Pause();});
                    },ClassicSelectedAudioIndex()))
                    {
                        form.FormClosed+=(sender,args)=>{previewSerial++;mediaPlayer.Pause();};
                        if(form.ShowDialog(this)!=DialogResult.OK)return;
                        var index=FirstSelectedIndex;MakeHistoryForUndo("Before improving time codes");_subtitle=form.EditedSubtitle;
                        SubtitleListview1.Fill(_subtitle,_subtitleOriginal);_subtitleListViewIndex=-1;SubtitleListview1.SelectIndexAndEnsureVisibleFaster(index);audioVisualizer.Invalidate();
                    }
                };
            }
            _classicImproveTimeCodesMenu.Text=Configuration.Settings.General.Language.StartsWith("ru",StringComparison.OrdinalIgnoreCase) ? "Улучшить тайминги по речи…" : "Improve time codes…";
        }
    }
}
