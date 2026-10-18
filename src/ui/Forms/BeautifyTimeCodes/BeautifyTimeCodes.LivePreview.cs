using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Forms;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Forms.BeautifyTimeCodes
{
    public sealed partial class BeautifyTimeCodes
    {
        private AudioVisualizer _beforeWave, _afterWave;
        private TableLayoutPanel _previewOptions;
        private Label _previewStats, _previewDetail, _previewReason, _previewPosition, _audioStatus;
        private Button _loadPreviewAudio, _previousChange, _nextChange;
        private System.Windows.Forms.Timer _previewTimer;
        private CancellationTokenSource _previewOperation, _waveOperation;
        private List<int> _previewChanges = new List<int>();
        private Subtitle _displayAfter;
        private int _changeAt = -1, _previewAudioTrack;
        private bool _previewReady, _previewDirty, _syncPreviewWave, _previewCloseRequested, _extractingExact;
        private bool PreviewRu => (Configuration.Settings.General.Language ?? "").StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        private string P(string ru, string en) => PreviewRu ? ru : en;
        private static AudioVisualizer PreviewWave(Color color) => new AudioVisualizer { ReadOnlyView = true, Dock = DockStyle.Fill, Locked = true, AllowNewSelection = false, ShowGridLines = true, ShowWaveform = true, BackgroundColor = Color.FromArgb(24,24,24), Color = Color.LimeGreen, SelectedColor = Color.YellowGreen, ParagraphColor = color, TextColor = Color.White, CursorColor = Color.OrangeRed, GridColor = Color.FromArgb(60,60,60), TextSize = 9 };

        private void InitializeLivePreview(WavePeakData peaks, int audioTrack)
        {
            _previewAudioTrack = Math.Max(0, audioTrack);
            buttonImportShotChanges.AutoSize = true;buttonExtractTimeCodes.AutoSize = true;buttonCancelTimeCodes.AutoSize = true;
            SuspendLayout();
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 9 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (var row in new[] { new RowStyle(SizeType.Absolute,180),new RowStyle(SizeType.Absolute,30),new RowStyle(SizeType.Absolute,20),new RowStyle(SizeType.Percent,50),new RowStyle(SizeType.Absolute,20),new RowStyle(SizeType.Percent,50),new RowStyle(SizeType.Absolute,92),new RowStyle(SizeType.AutoSize),new RowStyle(SizeType.AutoSize) }) root.RowStyles.Add(row);
            _previewOptions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            _previewOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); _previewOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            int col = 0;
            foreach (var group in new[] { groupBoxTimeCodes, groupBoxShotChanges })
            {
                var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true }; group.Dock = DockStyle.None; group.Anchor = AnchorStyles.Top | AnchorStyles.Left; group.Location = Point.Empty; group.Width = 558;
                scroll.Controls.Add(group); _previewOptions.Controls.Add(scroll,col++,0);
            }
            _previewStats = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            _beforeWave = PreviewWave(Color.SteelBlue); _afterWave = PreviewWave(Color.DarkOrange);
            var detail = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            detail.RowStyles.Add(new RowStyle(SizeType.Absolute,22)); detail.RowStyles.Add(new RowStyle(SizeType.Percent,50)); detail.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            _previewPosition = new Label { Dock = DockStyle.Fill, AutoEllipsis = true }; _previewDetail = new Label { Dock = DockStyle.Fill }; _previewReason = new Label { Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText };
            detail.Controls.Add(_previewPosition,0,0);detail.Controls.Add(_previewDetail,0,1);detail.Controls.Add(_previewReason,0,2);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            var first = PreviewButton("|◀",()=>NavigateChange(0)); _previousChange = PreviewButton("◀",()=>NavigateChange(_changeAt-1)); _nextChange = PreviewButton("▶",()=>NavigateChange(_changeAt+1)); var last = PreviewButton("▶|",()=>NavigateChange(_previewChanges.Count-1));
            toolbar.Controls.AddRange(new Control[] { first,_previousChange,_nextChange,last,PreviewButton("−",()=>_afterWave.ZoomOut()),PreviewButton("+",()=>_afterWave.ZoomIn()) });
            _loadPreviewAudio = PreviewButton(P("Загрузить осциллограмму", "Load waveform"),async()=>await LoadPreviewAudioAsync());
            _loadPreviewAudio.Enabled = File.Exists(_videoFileName) && File.Exists(Configuration.Settings.General.FFmpegLocation);
            _audioStatus = new Label { AutoSize = true, Padding = new Padding(5,7,0,0) }; toolbar.Controls.Add(_loadPreviewAudio);toolbar.Controls.Add(_audioStatus);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = true };
            buttonOK.Text = P("Применить", "Apply");buttonOK.AutoSize = true;buttonCancel.AutoSize=true;buttonEditProfile.AutoSize=true;
            footer.Controls.Add(buttonCancel);footer.Controls.Add(buttonOK);footer.Controls.Add(buttonEditProfile);
            root.Controls.Add(_previewOptions,0,0);root.Controls.Add(_previewStats,0,1);root.Controls.Add(new Label {Text=P("До", "Original"),Dock=DockStyle.Fill},0,2);root.Controls.Add(_beforeWave,0,3);root.Controls.Add(new Label {Text=P("После", "Beautified"),Dock=DockStyle.Fill},0,4);root.Controls.Add(_afterWave,0,5);root.Controls.Add(detail,0,6);root.Controls.Add(toolbar,0,7);root.Controls.Add(footer,0,8);
            foreach(Control old in Controls.Cast<Control>().ToArray()) old.Visible=false;
            // Reparented controls are no longer in the old top-level collection.
            progressBar.Visible=false;Controls.Add(root);root.BringToFront();
            FormBorderStyle=FormBorderStyle.Sizable;MinimumSize=new Size(900,650);ClientSize=new Size(1180,800);
            _previewTimer=new System.Windows.Forms.Timer { Interval=140 };_previewTimer.Tick+=async(s,e)=>{_previewTimer.Stop();await RecalculatePreviewAsync();};
            _beforeWave.OnZoomedChanged+=(s,e)=>MirrorPreview(_beforeWave,_afterWave);_afterWave.OnZoomedChanged+=(s,e)=>MirrorPreview(_afterWave,_beforeWave);
            _beforeWave.OnPositionSelected+=(s,e)=>{_beforeWave.StartPositionSeconds=Math.Max(0,e.Seconds);MirrorPreview(_beforeWave,_afterWave);};
            _afterWave.OnPositionSelected+=(s,e)=>{_afterWave.StartPositionSeconds=Math.Max(0,e.Seconds);MirrorPreview(_afterWave,_beforeWave);};
            _beforeWave.MouseWheel+=(s,e)=>MirrorPreview(_beforeWave,_afterWave);_afterWave.MouseWheel+=(s,e)=>MirrorPreview(_afterWave,_beforeWave);
            FormClosing+=LivePreviewClosing;FormClosed+=(s,e)=>{_previewTimer.Stop();_previewTimer.Dispose();};
            SetPreviewPeaks(peaks);_previewReady=true;Shown+=(s,e)=>QueueLivePreview();ResumeLayout(true);
        }
        private Button PreviewButton(string text, Action action) { var button=new Button {Text=text,AutoSize=true};button.Click+=(s,e)=>action();return button; }
        private void SetPreviewPeaks(WavePeakData peaks)
        {
            bool actual=peaks!=null;
            if(peaks==null)
            {
                var duration=Math.Max(10,Math.Max(_duration,_subtitle.Paragraphs.Count==0?0:_subtitle.Paragraphs.Max(p=>p.EndTime.TotalSeconds)+5));
                peaks=new WavePeakData(100,new WavePeak[Math.Min(8640000,Math.Max(1000,(int)Math.Ceiling(duration*100)))]);
            }
            _beforeWave.WavePeaks=peaks;_afterWave.WavePeaks=peaks;_beforeWave.ShowWaveform=actual;_afterWave.ShowWaveform=actual;
            _audioStatus.Text=actual?P("Звук загружен", "Audio loaded"):P("Только шкала реплик — звук не загружен", "Cue timeline only — audio not loaded");
            _loadPreviewAudio.Visible=!actual;RenderPreview();
        }
        private void QueueLivePreview()
        {
            if(!_previewReady || IsDisposed || _previewCloseRequested)return;
            FixedSubtitle=null;buttonOK.Enabled=false;_previewDirty=true;NavigateChange(-1);
            _previewStats.Text=P("Обновление предпросмотра…", "Updating preview…");
            if(_previewOperation!=null){_previewOperation.Cancel();return;}
            if(!_extractingExact)_previewTimer.Start();
        }
        private async Task RecalculatePreviewAsync()
        {
            if(_previewOperation!=null || _extractingExact || IsDisposed || _previewCloseRequested)return;
            _previewDirty=false;
            if(checkBoxExtractExactTimeCodes.Checked && _timeCodes.Count==0 || checkBoxSnapToShotChanges.Checked && _shotChanges.Count==0)
            { _previewStats.Text=P("Загрузите выбранные тайм-коды/смены планов или отключите соответствующий пункт.", "Load the selected time codes/shot changes or turn that option off.");return; }
            var op=new CancellationTokenSource();_previewOperation=op;var cancel=op.Token;
            _previewOptions.Enabled=false;buttonEditProfile.Enabled=false;buttonOK.Enabled=false;
            var exact=checkBoxExtractExactTimeCodes.Checked?new List<double>(_timeCodes):new List<double>();var shots=checkBoxSnapToShotChanges.Checked?new List<double>(_shotChanges):new List<double>();
            try
            {
                var result=await Task.Run(()=>
                {
                    var copy=new Subtitle(_subtitle,false);
                    var beautifier=new TimeCodesBeautifier(copy,_frameRate,exact,shots);
                    beautifier.ProgressChanged=p=>cancel.ThrowIfCancellationRequested();beautifier.Beautify();cancel.ThrowIfCancellationRequested();return copy;
                },cancel);
                if(cancel.IsCancellationRequested || IsDisposed)return;
                _displayAfter=result;FixedSubtitle=result;CheckUnfixableParagraphs();
                _previewChanges=Enumerable.Range(0,result.Paragraphs.Count).Where(i=>Math.Abs(result.Paragraphs[i].StartTime.TotalMilliseconds-_subtitle.Paragraphs[i].StartTime.TotalMilliseconds)>.5 || Math.Abs(result.Paragraphs[i].EndTime.TotalMilliseconds-_subtitle.Paragraphs[i].EndTime.TotalMilliseconds)>.5).ToList();
                _previewStats.Text=P("Реплик: ","Subtitles: ")+result.Paragraphs.Count+P(" · Изменено: "," · Changed: ")+_previewChanges.Count+" · FPS: "+_frameRate.ToString("0.###")+(exact.Count>0?P(" (точные тайм-коды)"," (exact time codes)"):"")+P(" · Смен планов: "," · Shot changes: ")+shots.Count;
                buttonOK.Enabled=_waveOperation==null;NavigateChange(_previewChanges.Count==0?-1:Math.Max(0,Math.Min(_changeAt,_previewChanges.Count-1)));RenderPreview();
            }
            catch(OperationCanceledException) { }
            catch(Exception ex){FixedSubtitle=null;_previewStats.Text=ex.Message;buttonOK.Enabled=false;}
            finally
            {
                op.Dispose();_previewOperation=null;
                if(!IsDisposed){_previewOptions.Enabled=true;buttonEditProfile.Enabled=true;if(_previewCloseRequested && _waveOperation==null && !_extractingExact)Close();else if(_previewDirty)_previewTimer.Start();}
            }
        }
        private void NavigateChange(int at)
        {
            if(FixedSubtitle==null){_changeAt=-1;_previewPosition.Text=P("Предпросмотр ещё не рассчитан", "Preview has not been calculated");_previewDetail.Text="";_previewReason.Text="";_previousChange.Enabled=false;_nextChange.Enabled=false;return;}
            if(_previewChanges.Count==0 || _displayAfter==null){_changeAt=-1;_previewPosition.Text=P("Изменений нет", "No changes");_previewDetail.Text="";_previewReason.Text="";_previousChange.Enabled=false;_nextChange.Enabled=false;return;}
            _changeAt=Math.Max(0,Math.Min(_previewChanges.Count-1,at));var i=_previewChanges[_changeAt];var before=_subtitle.Paragraphs[i];var after=_displayAfter.Paragraphs[i];
            _previewPosition.Text=P("Изменение ","Change ")+(_changeAt+1)+" / "+_previewChanges.Count+" · #"+(before.Number>0?before.Number:i+1)+" · "+HtmlUtil.RemoveHtmlTags(before.Text).Replace(Environment.NewLine," ");
            _previewDetail.Text=P("Начало: ","Start: ")+TimeShift(before.StartTime,after.StartTime)+"    "+P("Конец: ","End: ")+TimeShift(before.EndTime,after.EndTime);
            _previewReason.Text=P("Вероятная причина: ","Likely reason: ")+DescribePreviewChange(i);
            _previousChange.Enabled=_changeAt>0;_nextChange.Enabled=_changeAt<_previewChanges.Count-1;
            var seconds=Math.Max(6,Math.Max(before.EndTime.TotalSeconds,after.EndTime.TotalSeconds)-Math.Min(before.StartTime.TotalSeconds,after.StartTime.TotalSeconds)+4);
            _afterWave.ZoomFactor=Math.Max(.00001,_afterWave.Width/(_afterWave.WavePeaks.SampleRate*seconds));_afterWave.StartPositionSeconds=Math.Max(0,Math.Min(before.StartTime.TotalSeconds,after.StartTime.TotalSeconds)-2);MirrorPreview(_afterWave,_beforeWave);RenderPreview();
        }
        private string TimeShift(TimeCode before,TimeCode after)
        {var delta=after.TotalMilliseconds-before.TotalMilliseconds;return before.ToDisplayString()+" → "+after.ToDisplayString()+" ("+delta.ToString("+0.###;-0.###;0")+" ms / "+(delta*_frameRate/1000).ToString("+0.###;-0.###;0")+P(" к.)", " f)");}
        private string DescribePreviewChange(int index)
        {
            var old=_subtitle.Paragraphs[index];var item=_displayAfter.Paragraphs[index];var reasons=new List<string>();
            foreach(var start in new[]{true,false})
            {
                var was=start?old.StartTime.TotalMilliseconds:old.EndTime.TotalMilliseconds;var now=start?item.StartTime.TotalMilliseconds:item.EndTime.TotalMilliseconds;if(Math.Abs(now-was)<.5)continue;
                string reason=null;var profile=Configuration.Settings.BeautifyTimeCodes.Profile;var gap=(start?profile.InCuesGap:-profile.OutCuesGap)*1000.0/_frameRate;
                if(checkBoxSnapToShotChanges.Checked && _shotChanges.Any(sc=>Math.Abs(now-(sc*1000+gap))<=.6))reason=P("привязка к смене плана", "snapped to shot change");
                var neighbor=index+(start?-1:1);var minGap=profile.Gap*1000.0/_frameRate;
                if(reason==null && neighbor>=0 && neighbor<_displayAfter.Paragraphs.Count){var other=_displayAfter.Paragraphs[neighbor];var expected=start?other.EndTime.TotalMilliseconds+minGap:other.StartTime.TotalMilliseconds-minGap;if(Math.Abs(now-expected)<=.6)reason=P("интервал между репликами", "inter-cue gap");}
                if(reason==null && checkBoxAlignTimeCodes.Checked)
                {
                    if(checkBoxExtractExactTimeCodes.Checked){if(_timeCodes.Any(t=>Math.Abs(now-t*1000)<=.6))reason=P("привязка к точному тайм-коду кадра", "snapped to exact frame time");}
                    else if(Math.Abs(now- Math.Round(now*_frameRate/1000)*1000/_frameRate)<=.6)reason=P("привязка к кадру", "snapped to frame");
                }
                reasons.Add((start?P("начало: ","start: "):P("конец: ","end: "))+(reason??P("связывание или другое правило профиля", "chaining or another profile rule")));
            }
            var duration=item.Duration.TotalMilliseconds;var general=Configuration.Settings.General;
            if(Math.Abs(duration-old.Duration.TotalMilliseconds)>.5 && (Math.Abs(duration-general.SubtitleMinimumDisplayMilliseconds)<=.6 || Math.Abs(duration-general.SubtitleMaximumDisplayMilliseconds)<=.6))reasons.Add(P("ограничение длительности", "duration limit"));
            return string.Join("; ",reasons);
        }
        private void RenderPreview()
        {
            if(_beforeWave?.WavePeaks==null)return;var index=_changeAt>=0 && _changeAt<_previewChanges.Count?_previewChanges[_changeAt]:-1;
            _beforeWave.ShotChanges=new List<double>(_shotChanges);_afterWave.ShotChanges=new List<double>(_shotChanges);
            _beforeWave.SetPosition(_beforeWave.StartPositionSeconds,_subtitle,0,index,null);_afterWave.SetPosition(_afterWave.StartPositionSeconds,_displayAfter??_subtitle,0,index,null);
        }
        private void MirrorPreview(AudioVisualizer from,AudioVisualizer to)
        {
            if(_syncPreviewWave)return;_syncPreviewWave=true;try{to.ZoomFactor=from.ZoomFactor;to.StartPositionSeconds=from.StartPositionSeconds;RenderPreview();}finally{_syncPreviewWave=false;}
        }
        private async Task LoadPreviewAudioAsync()
        {
            if(_waveOperation!=null || !File.Exists(_videoFileName))return;
            var op=new CancellationTokenSource();_waveOperation=op;buttonOK.Enabled=false;_loadPreviewAudio.Enabled=false;_audioStatus.Text=P("Загрузка звука…","Loading audio…");
            var folder=Path.Combine(Path.GetTempPath(),"se-beautify-wave-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                var wave=Path.Combine(folder,"source.wav");
                await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,"-nostdin -y -i "+ClassicSpeechRunner.Quote(_videoFileName)+" -map 0:a:"+_previewAudioTrack+" -vn -af aresample=async=1:first_pts=0 -ar 16000 -ac 1 -c:a pcm_s16le "+ClassicSpeechRunner.Quote(wave)),op.Token,null);
                var peaks=await Task.Run(()=>{using(var generator=new WavePeakGenerator(wave))return generator.GeneratePeaks(0,Path.Combine(folder,"peaks.dat"));},op.Token);
                op.Token.ThrowIfCancellationRequested();if(!IsDisposed){SetPreviewPeaks(peaks);NavigateChange(_changeAt);}
            }
            catch(OperationCanceledException) { }
            catch(Exception ex){if(!IsDisposed)_audioStatus.Text=ex.Message;}
            finally{if(Directory.Exists(folder))Directory.Delete(folder,true);op.Dispose();_waveOperation=null;if(!IsDisposed){_loadPreviewAudio.Enabled=true;buttonOK.Enabled=FixedSubtitle!=null && _previewOperation==null && !_previewCloseRequested;if(_previewCloseRequested && _previewOperation==null && !_extractingExact)Close();}}
        }
        private void LivePreviewClosing(object sender,FormClosingEventArgs e)
        {
            _previewTimer.Stop();
            if(_previewOperation!=null || _waveOperation!=null || _extractingExact){_previewCloseRequested=true;_previewOperation?.Cancel();_waveOperation?.Cancel();_abortTimeCodes=true;e.Cancel=true;DialogResult=DialogResult.None;}
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
        {
            if(keyData==Keys.Down || keyData==Keys.Right || keyData==Keys.PageDown){NavigateChange(_changeAt+1);return true;}
            if(keyData==Keys.Up || keyData==Keys.Left || keyData==Keys.PageUp){NavigateChange(_changeAt-1);return true;}
            if(keyData==Keys.Home){NavigateChange(0);return true;}if(keyData==Keys.End){NavigateChange(_previewChanges.Count-1);return true;}
            return base.ProcessCmdKey(ref msg,keyData);
        }
    }
}
