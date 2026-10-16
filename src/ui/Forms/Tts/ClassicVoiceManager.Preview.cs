using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms.AudioToText;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.VideoPlayers;

namespace Nikse.SubtitleEdit.Forms.Tts
{
 public sealed partial class ClassicVoiceManager
 {
  private ClassicSpeechWaveform _referenceWave;
  private LibMpvDynamic _referenceMpv;
  private CancellationTokenSource _previewCancellation;
  private System.Windows.Forms.Timer _previewTimer;
  private int _playGeneration;
  private Button _transcribeReference;
  public Task ReferencePreviewTask {get;private set;}=Task.CompletedTask;
  private void InitializeReferencePreview(Control editor)
  {
   var area=new TableLayoutPanel{Name="referenceWaveArea",Dock=DockStyle.Bottom,Height=180,ColumnCount=1,RowCount=2};area.RowStyles.Add(new RowStyle(SizeType.AutoSize));area.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};var stop=new Button{Text=L("Остановить","Stop"),AutoSize=true};var plus=new Button{Text="+",Width=32};var minus=new Button{Text="−",Width=32};
   bar.Controls.AddRange(new Control[]{stop,plus,minus});_referenceWave=new ClassicSpeechWaveform{Name="voiceReferenceWaveform",Dock=DockStyle.Fill,AllowTimingEdit=false};area.Controls.Add(bar,0,0);area.Controls.Add(_referenceWave,0,1);editor.Controls.Add(area);
   stop.Click+=(s,e)=>StopReferencePreview();plus.Click+=(s,e)=>_referenceWave.Zoom(.8);minus.Click+=(s,e)=>_referenceWave.Zoom(1.25);_referenceWave.SeekRequested+=PlayReference;
   _transcribeReference=new Button{Name="buttonTranscribeVoiceReference",Text=L("Распознать текст…","Transcribe reference…"),AutoSize=true};_transcribeReference.Click+=(s,e)=>TranscribeReference();_toolbar.Controls.Add(_transcribeReference);
   if(LibMpvDynamic.IsInstalled)_referenceMpv=new LibMpvDynamic();
   _previewTimer=new System.Windows.Forms.Timer{Interval=70};_previewTimer.Tick+=(s,e)=>{_referenceWave.PlaybackSeconds=_referenceMpv!=null && !_referenceMpv.IsPaused ? _referenceMpv.CurrentPosition : -1;_referenceWave.Invalidate();};_previewTimer.Start();
   Shown+=(s,e)=>RefreshReferencePreview();
   FormClosed+=(s,e)=>{_previewCancellation?.Cancel();_previewTimer.Stop();_previewTimer.Dispose();_referenceMpv?.Dispose();};
  }
  private void StopReferencePreview()
  {
   _playGeneration++;_player.Stop();_referenceMpv?.Stop();if(_referenceWave!=null){_referenceWave.PlaybackSeconds=-1;_referenceWave.Invalidate();}
  }
  private void PlayReference(double seconds)
  {
   if(_operation!=null)return;var voice=Selected();if(voice==null)return;StopReferencePreview();var generation=_playGeneration;
   var path=Path.Combine(ClassicQwenReference.Folder,voice.Id+".wav");
   if(_referenceMpv==null){_player.SoundLocation=path;_player.Play();_progress.Text=L("Перемотка требует MPV; воспроизведение с начала","Seeking requires MPV; playing from start");return;}
   _referenceMpv.Initialize(null,path,(s,e)=>{if(IsDisposed || Disposing || generation!=_playGeneration)return;_referenceMpv.PlayRate=1;_referenceMpv.CurrentPosition=Math.Max(0,Math.Min(seconds,_referenceMpv.Duration));_referenceMpv.Play();},null);
  }
  private void RefreshReferencePreview()
  {
   if(_referenceWave==null)return;StopReferencePreview();_previewCancellation?.Cancel();_referenceWave.Peaks=null;_referenceWave.AudioSeconds=0;var voice=Selected();_transcribeReference.Enabled=voice!=null;
   if(voice==null){_referenceWave.Text="";_referenceWave.Invalidate();return;}
   if(!IsHandleCreated)return;
   _referenceWave.Text=L("Загрузка образца…","Loading reference…");_referenceWave.Invalidate();
   var cancellation=new CancellationTokenSource();_previewCancellation=cancellation;ReferencePreviewTask=LoadReferencePreview(voice.Id,cancellation);
  }
  private async Task LoadReferencePreview(string id,CancellationTokenSource cancellation)
  {
   var job=NewJob();var token=cancellation.Token;
   try{
    var result=await Task.Run(()=>{token.ThrowIfCancellationRequested();using(var file=new FileStream(Path.Combine(ClassicQwenReference.Folder,id+".wav"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var memory=new MemoryStream()){file.CopyTo(memory);memory.Position=0;using(var generator=new WavePeakGenerator(memory))return Tuple.Create(generator.GeneratePeaks(0,Path.Combine(job,"peaks.dat")),generator.Header.LengthInSeconds);}},token);
    token.ThrowIfCancellationRequested();if(IsDisposed || Disposing || _previewCancellation!=cancellation || Selected()?.Id!=id)return;
    _referenceWave.Peaks=result.Item1;_referenceWave.AudioSeconds=result.Item2;_referenceWave.SetCue(0,result.Item2,true);_referenceWave.Text=result.Item2.ToString("0.000")+L(" с · щелчок — слушать отсюда; Ctrl+колесо — масштаб"," s · click to play from here; Ctrl+wheel to zoom");
   }catch(OperationCanceledException){}catch(Exception error){if(!IsDisposed && !Disposing && _previewCancellation==cancellation){_referenceWave.Text=error.Message;_referenceWave.Invalidate();}}
   finally{try{Directory.Delete(job,true);}catch(IOException){}catch(UnauthorizedAccessException){}if(_previewCancellation==cancellation)_previewCancellation=null;cancellation.Dispose();}
  }
  public bool ApplyReferenceTranscript(string text)
  {
   if(Selected()==null || string.IsNullOrWhiteSpace(text))return false;_transcript.Text=text.Trim();return true;
  }
  private void TranscribeReference()
  {
   var voice=Selected();if(voice==null || _operation!=null)return;StopReferencePreview();
   using(var form=new WhisperAudioToText(Path.Combine(ClassicQwenReference.Folder,voice.Id+".wav"),new Subtitle(),0,this,null))
    if(form.ShowDialog(this)==DialogResult.OK && Selected()?.Id==voice.Id)ApplyReferenceTranscript(ClassicReferenceAudio.Transcript(form.TranscribedSubtitle));
  }
 }
}
