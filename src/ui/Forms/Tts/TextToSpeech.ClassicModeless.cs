using System;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
namespace Nikse.SubtitleEdit.Forms.Tts
{
    public sealed partial class TextToSpeech
    {
        public Func<Subtitle> MainSubtitleSource {get;set;}
        public Func<SubtitleFormat> MainSubtitleFormat {get;set;}
        public Func<int> MainSelectedIndex {get;set;}
        public Func<bool> SourceVideoIsCurrent {get;set;}
        public Func<Subtitle,Subtitle,bool> ApplyModelessSubtitle {get;set;}
        private Subtitle _modelessBaseline;
        private Timer _modelessTimer;
        private bool _closeWhenIdle;
        public bool SpeechWorkIsActive => _converting || _playbackCancellation!=null || _qwenCancellation!=null || _localCancellation!=null || _castCancellation!=null || _compatibleCancellation!=null || _dubbingCancellation!=null;
        public static string SpeechDocumentSignature(Subtitle subtitle)
        {
            if(subtitle==null)return "";
            return JsonConvert.SerializeObject(new {subtitle.FileName,subtitle.Header,subtitle.Footer,Paragraphs=subtitle.Paragraphs.Select(p=>new {p.Id,p.Text,Start=p.StartTime.TotalMilliseconds,End=p.EndTime.TotalMilliseconds,p.Actor,p.Extra,p.Layer,p.Forced,p.IsComment,p.Region,p.MarginL,p.MarginR,p.MarginV,p.Effect,p.Language,p.Style,p.NewSection,p.Bookmark})});
        }
        public void InitializeModelessSession()
        {
            _modelessBaseline=new Subtitle(_subtitle,false);
            _modelessTimer=new Timer{Interval=150};
            _modelessTimer.Tick+=(s,e)=>{
                if(_closeWhenIdle && !SpeechWorkIsActive){_closeWhenIdle=false;DialogResult=DialogResult.Cancel;Close();return;}
                if (SourceVideoIsCurrent != null && !SourceVideoIsCurrent() && SpeechWorkIsActive)
                {
                    CancelSpeechAndCloseWhenIdle();
                    return;
                }
                RefreshMainSelection();
            };
            Activated+=(s,e)=>SynchronizeMainSubtitle();
            FormClosed+=(s,e)=>{_modelessTimer.Stop();_modelessTimer.Dispose();_modelessTimer=null;};
            _modelessTimer.Start();RefreshMainSelection();
        }
        private void RefreshMainSelection()
        {
            if(MainSelectedIndex==null || SpeechWorkIsActive || IsDisposed)return;
            var index=MainSelectedIndex();
            if(index<0)return;
            PlaybackFromIndex=index;
            _playSpeech.Text=CL("Озвучить со строки ","Speak from line ")+(index+1)+"…";
        }
        private void SynchronizeMainSubtitle()
        {
            if(MainSubtitleSource==null || SpeechWorkIsActive || _modelessBaseline==null)return;
            RefreshMainSelection();
            var source=MainSubtitleSource();if(source==null)return;
            if(SpeechDocumentSignature(_subtitle)!=SpeechDocumentSignature(_modelessBaseline))return;
            if(SpeechDocumentSignature(source)==SpeechDocumentSignature(_modelessBaseline))return;
            var copy=new Subtitle(source,false);
            _subtitle.Paragraphs.Clear();_subtitle.Paragraphs.AddRange(copy.Paragraphs);
            _subtitle.Header=copy.Header;_subtitle.Footer=copy.Footer;_subtitle.FileName=copy.FileName;
            _modelessBaseline=new Subtitle(copy,false);
            if(MainSubtitleFormat!=null)_subtitleFormat=MainSubtitleFormat();
            var actors=_subtitle.Paragraphs.Select(p=>p.Actor ?? "").Distinct().ToList();
            if (actors.All(string.IsNullOrEmpty)) actors.Clear();
            if (!actors.SequenceEqual(_actors.Select(a => a ?? "")))
            {
                PersistClassicCast();
                RefreshClassicActors();
            }
        }
        private int ResolvePlaybackIndex()
        {
            RefreshMainSelection();
            if(MainSubtitleSource==null)return PlaybackFromIndex;
            var source=MainSubtitleSource();var index=MainSelectedIndex?.Invoke() ?? PlaybackFromIndex;
            if(source==null || index<0 || index>=source.Paragraphs.Count)return PlaybackFromIndex;
            var selected=source.Paragraphs[index];
            var match=_subtitle.Paragraphs.FindIndex(p=>p.Id==selected.Id);
            if(match>=0)return match;
            match=_subtitle.Paragraphs.FindIndex(p=>p.StartTime.TotalMilliseconds<=selected.StartTime.TotalMilliseconds && p.EndTime.TotalMilliseconds>selected.StartTime.TotalMilliseconds);
            return match>=0 ? match : Math.Min(index,Math.Max(0,_subtitle.Paragraphs.Count-1));
        }
        private void CancelSpeechAndCloseWhenIdle()
        {
            _abort=true;_playbackCancellation?.Cancel();_castCancellation?.Cancel();_qwenCancellation?.Cancel();_localCancellation?.Cancel();_dubbingCancellation?.Cancel();_compatibleCancellation?.Cancel();
            if(_modelessTimer!=null)_closeWhenIdle=true;
        }
        private bool EnsureCurrentSpeechVideo()
        {
            if (SourceVideoIsCurrent == null || SourceVideoIsCurrent()) return true;
            MessageBox.Show(this, CL("Видео изменилось. Открой окно озвучки заново для текущего видео.", "The video changed. Reopen speech settings for the current video."));
            return false;
        }
        private bool CanApplyModelessChanges()
        {
            if(ApplyModelessSubtitle==null || _modelessBaseline==null)return true;
            return ApplyModelessSubtitle(_modelessBaseline,_subtitle);
        }
    }
}
