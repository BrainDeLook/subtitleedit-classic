using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.ContainerFormats.Matroska;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using Nikse.SubtitleEdit.Logic.VideoPlayers;

namespace Nikse.SubtitleEdit.Forms
{
 public sealed partial class Main
 {
  private int? _classicPendingAudioIndex;
  private CancellationTokenSource _classicWaveformCancellation;
  private Task _classicWaveformTask=Task.CompletedTask;
  private bool ChooseClassicOpeningAudio(string file,ref int index)
  {
   try{
    if(!File.Exists(Configuration.Settings.General.FFmpegLocation))return true;
    var tracks=ClassicAudioTrackLabels.Read(file);
    if(tracks.Count<=1){index=tracks.Count==1?0:index;return true;}
    var ru=Configuration.Settings.General.Language.StartsWith("ru",StringComparison.OrdinalIgnoreCase);
    using(var dialog=new ChooseAudioTrack(tracks.Select((t,i)=>t.Display(i,ru)).ToList(),Math.Max(0,Math.Min(index,tracks.Count-1)))){
     if(dialog.ShowDialog(this)!=System.Windows.Forms.DialogResult.OK)return false;index=dialog.SelectedTrack;return true;
    }
   }catch(Exception error){SeLogger.Error(error,"Reading audio tracks before opening video");return true;}
  }
  private void ApplyClassicOpeningAudio()
  {
   if(!_classicPendingAudioIndex.HasValue)return;var index=_classicPendingAudioIndex.Value;_classicPendingAudioIndex=null;
   if(mediaPlayer.VideoPlayer is ClassicFfmpegPlayer ff){ff.AudioTrackNumber=index;_videoAudioTrackNumber=index;}
   else if(mediaPlayer.VideoPlayer is LibMpvDynamic mpv){mpv.AudioTrackNumber=index;_videoAudioTrackNumber=mpv.AudioTrackNumber;}
   else if(mediaPlayer.VideoPlayer is LibVlcDynamic vlc){var tracks=vlc.GetAudioTracks().Where(t=>t.TrackNumber>=0).ToList();if(index>=0 && index<tracks.Count){vlc.AudioTrackNumber=tracks[index].TrackNumber;_videoAudioTrackNumber=tracks[index].TrackNumber;}}
  }
  private void BeginClassicWaveform(string file,int index)
  {
   _classicWaveformCancellation?.Cancel();var cancellation=new CancellationTokenSource();_classicWaveformCancellation=cancellation;
   _classicWaveformTask=GenerateClassicWaveform(file,Math.Max(0,index),cancellation);
  }
  private async Task GenerateClassicWaveform(string file,int index,CancellationTokenSource cancellation)
  {
   var token=cancellation.Token;var job=Path.Combine(Path.GetTempPath(),"se-wave-track-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(job);
   try{
    audioVisualizer.WaveformNotLoadedText=_language.GeneratingWaveformInBackground;audioVisualizer.Invalidate();
    var target=Path.Combine(job,"source.wav");var peakFile=WavePeakGenerator.GetPeakWaveFileName(file,index);var spectrum=WavePeakGenerator.SpectrogramDrawer.GetSpectrogramFolder(file,index);
    using(var process=AddWaveform.GetCommandLineProcess(file,index,target,Configuration.Settings.General.VlcWaveTranscodeSettings,out var encoder))
     await ClassicSpeechRunner.RunProcessAsync(process.StartInfo,token,null);
    var generateSpectrum=Configuration.Settings.VideoControls.GenerateSpectrogram;
    await Task.Run(()=>{
     token.ThrowIfCancellationRequested();var delay=0;
     if(FileUtil.IsMatroskaFile(file))using(var mkv=new MatroskaFile(file)){var tracks=mkv.GetTracks().Where(t=>t.IsAudio).ToList();if(index<tracks.Count)delay=(int)mkv.GetAudioTrackDelayMilliseconds(tracks[index].TrackNumber);}
     using(var wave=new WavePeakGenerator(target)){wave.GeneratePeaks(delay,peakFile);token.ThrowIfCancellationRequested();if(generateSpectrum)wave.GenerateSpectrogram(delay,spectrum);}
    },token);
    token.ThrowIfCancellationRequested();if(IsDisposed || Disposing || _classicWaveformCancellation!=cancellation || !string.Equals(file,_videoFileName,StringComparison.OrdinalIgnoreCase) || ClassicSelectedAudioIndex()!=index)return;
    audioVisualizer.WavePeaks=WavePeakData.FromDisk(peakFile);audioVisualizer.SetSpectrogram(SpectrogramData.FromDisk(spectrum));audioVisualizer.ShotChanges=ShotChangeHelper.FromDisk(file);SetWaveformPosition(0,mediaPlayer.CurrentPosition,FirstSelectedIndex);timerWaveform.Start();if(smpteTimeModedropFrameToolStripMenuItem.Checked)audioVisualizer.UseSmpteDropFrameTime();
   }catch(OperationCanceledException){}catch(Exception error){if(!IsDisposed && !Disposing && _classicWaveformCancellation==cancellation){SeLogger.Error(error,"Generating waveform for selected audio track");SetWaveFormNotLoadedText(LanguageSettings.Current.Waveform);}}
   finally{try{Directory.Delete(job,true);}catch(IOException){}catch(UnauthorizedAccessException){}if(_classicWaveformCancellation==cancellation)_classicWaveformCancellation=null;cancellation.Dispose();}
  }
 }
}
