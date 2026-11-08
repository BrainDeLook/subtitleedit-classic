using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
namespace Nikse.SubtitleEdit.Forms
{
 public sealed partial class Main
 {
    private readonly object _classicMicWriteGate=new object();
    private bool _classicMicRecognizing;
    private bool _classicMicCloseRequested;
    private IOException _classicMicWriteError;
    private WaveInEvent _classicMic;
    private WaveFileWriter _classicMicWriter;
    private CancellationTokenSource _classicMicCancellation;
    private System.Windows.Forms.Timer _classicMicLimit;
    private string _classicMicFolder,_classicMicInitial;
    private Subtitle _classicMicDocument;
    private Paragraph _classicMicCue;
    private int _classicMicCaret;
    private SpeechEngine _classicMicEngine;
    private SpeechModel _classicMicModel;
    private ClassicSpeechOptions _classicMicOptions;
    private string _classicMicAligner;
    private Task ToggleClassicMicrophone()
    {
        if(_classicMic!=null){_classicMic.StopRecording();return Task.CompletedTask;}
        if(_classicMicCancellation!=null){_classicMicCancellation.Cancel();return Task.CompletedTask;}
        var index=FirstSelectedIndex;if(index<0 || index>=_subtitle.Paragraphs.Count)return Task.CompletedTask;
        try
        {
            var options=ClassicSpeechOptions.Load();var engine=ClassicSpeechCatalog.Get(options.Engine,options.Backend);
            if(engine==null || !(engine.Family=="crisp" || engine.Family=="qwen" || engine.Family=="cpp"))throw new InvalidOperationException(ClassicAssistedText.L("Выберите локальный Crisp ASR, Qwen3 или Whisper CPP в окне «Аудио в текст».","Select local Crisp ASR, Qwen3 or Whisper CPP in Audio to text."));
            var model=engine.Models.FirstOrDefault(m=>m.Name==options.Model && !m.Aligner) ?? engine.Models.FirstOrDefault(m=>!m.Aligner);
            var custom=engine.Family=="qwen" && !string.IsNullOrEmpty(options.CustomQwenModel);
            var path=custom?options.CustomQwenModel:model==null?"":ClassicSpeechCatalog.ModelPath(engine,model);
            if(!File.Exists(ClassicSpeechCatalog.Executable(engine,options.Variant)) || !File.Exists(path))throw new FileNotFoundException(ClassicAssistedText.L("Сначала установите движок и модель через «Аудио в текст». Запись не запускает скачивание моделей.","Install the engine and model in Audio to text first. Recording does not download models."));
            if(custom){var reason=Qwen3AsrCppModelValidator.GetIncompatibilityReason(path);if(reason!=null)throw new InvalidDataException(reason);}
            var aligner=engine.Models.FirstOrDefault(m=>m.Aligner && m.Name==options.Aligner) ?? (engine.NativeTimings?null:engine.Models.FirstOrDefault(m=>m.Aligner));
            var alignerPath=aligner==null?null:ClassicSpeechCatalog.ModelPath(engine,aligner);
            if(!engine.NativeTimings && (alignerPath==null || !File.Exists(alignerPath)))throw new FileNotFoundException(ClassicAssistedText.L("Сначала установите модель выравнивания в окне «Аудио в текст».","Install the aligner in Audio to text first."));
            options.IsolateSpeech=false;options.DetectSpeakers=false;options.UseVad=false;options.WhisperXAlignWords=false;
            if(ClassicSpeechExtras.UseVad(engine,options) && !File.Exists(ClassicSpeechExtras.VadPath(engine,options)))throw new FileNotFoundException("Install the selected engine's VAD in Audio to text first.");
            _classicMicEngine=engine;_classicMicModel=model;_classicMicOptions=options;_classicMicAligner=alignerPath;
            _classicMicDocument=_subtitle;_classicMicCue=_subtitle.Paragraphs[index];_classicMicInitial=_classicMicCue.Text;_classicMicCaret=textBoxListViewText.SelectionStart;
            var root=Path.GetFullPath(Path.Combine(ClassicSpeechCatalog.Root,"MicrophoneJobs"));
            _classicMicFolder=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_classicMicFolder);
            _classicMicWriteError=null;_classicMicCancellation=new CancellationTokenSource();
            _classicMic=new WaveInEvent{WaveFormat=new WaveFormat(16000,16,1)};
            _classicMicWriter=new WaveFileWriter(Path.Combine(_classicMicFolder,"input.wav"),_classicMic.WaveFormat);
            var folder=_classicMicFolder;
            _classicMic.DataAvailable+=(sender,e)=>
            {
                lock(_classicMicWriteGate){try{_classicMicWriter?.Write(e.Buffer,0,e.BytesRecorded);}catch(IOException error){_classicMicWriteError=error;}}
                if(_classicMicWriteError!=null)_classicMic?.StopRecording();
            };
            _classicMic.RecordingStopped+=(sender,e)=>
            {
                if(IsHandleCreated && !IsDisposed)BeginInvoke(new Action(async()=>{if(_classicMicFolder==folder)await FinishClassicMicrophone(e.Exception ?? _classicMicWriteError);}));
                else if(_classicMicFolder==folder)ReleaseClassicMicrophone();
            };
            _classicMicLimit=new System.Windows.Forms.Timer{Interval=60000};_classicMicLimit.Tick+=(sender,e)=>{_classicMicLimit.Stop();_classicMic?.StopRecording();};
            _classicMic.StartRecording();_classicMicLimit.Start();_classicMicrophoneButton.Text="■";_classicMicrophoneButton.ToolTipText=ClassicAssistedText.L("Остановить запись (до 60 секунд)","Stop recording (up to 60 seconds)");
            ShowStatus(ClassicAssistedText.L("Идёт запись с микрофона…","Recording microphone…"));
        }
        catch(Exception ex)when(ex is IOException || ex is InvalidOperationException || ex is ArgumentException || ex is NAudio.MmException || ex is System.ComponentModel.Win32Exception)
        {ReleaseClassicMicrophone();MessageBox.Show(this,ex.Message,Title);}
        return Task.CompletedTask;
    }
    private async Task FinishClassicMicrophone(Exception recordingError)
    {
        _classicMicRecognizing=true;_classicMicLimit?.Stop();
        _classicMicrophoneButton.Text="…";_classicMicrophoneButton.ToolTipText=ClassicAssistedText.L("Отменить распознавание","Cancel recognition");
        try
        {
            var device=_classicMic;_classicMic=null;device?.Dispose();
            lock(_classicMicWriteGate){var writer=_classicMicWriter;_classicMicWriter=null;writer?.Dispose();}
            if(recordingError!=null)throw new IOException(recordingError.Message,recordingError);
            var cancel=_classicMicCancellation.Token;cancel.ThrowIfCancellationRequested();
            var wave=Path.Combine(_classicMicFolder,"input.wav");if(new FileInfo(wave).Length<=44)return;
            var recognized=await ClassicSpeechRunner.TranscribeAsync(_classicMicEngine,_classicMicModel,_classicMicOptions,wave,_classicMicFolder,_classicMicAligner,cancel,null);
            var text=string.Join(" ",recognized.Paragraphs.Select(p=>HtmlUtil.RemoveHtmlTags(p.Text,true))).Trim();
            if(text.Length==0){ShowStatus(ClassicAssistedText.L("Речь не распознана.","No speech recognized."));return;}
            if(!ReferenceEquals(_classicMicDocument,_subtitle) || !_subtitle.Paragraphs.Contains(_classicMicCue) || _classicMicCue.Text!=_classicMicInitial || !ReferenceEquals(_subtitle.GetParagraphOrDefault(FirstSelectedIndex),_classicMicCue))
            {MessageBox.Show(this,ClassicAssistedText.L("Реплика изменилась во время распознавания. Полученный текст:\n\n","The cue changed during recognition. Recognized text:\n\n")+text,Title);return;}
            MakeHistoryForUndo("Before microphone transcription");
            var visible=textBoxListViewText.Text;
            var caret=Math.Min(_classicMicCaret,visible.Length);
            if(caret>0 && char.IsLetterOrDigit(visible[caret-1]) && char.IsLetterOrDigit(text[0]))text=" "+text;
            if(caret<visible.Length && char.IsLetterOrDigit(visible[caret]) && char.IsLetterOrDigit(text[text.Length-1]))text+=" ";
            textBoxListViewText.SelectionStart=caret;textBoxListViewText.SelectionLength=0;textBoxListViewText.SelectedText=text;

        }
        catch(OperationCanceledException){ShowStatus(ClassicAssistedText.L("Распознавание отменено.","Recognition cancelled."));}
        catch(Exception ex)when(ex is IOException || ex is InvalidOperationException || ex is ArgumentException || ex is Newtonsoft.Json.JsonException || ex is TimeoutException || ex is System.ComponentModel.Win32Exception){MessageBox.Show(this,ex.Message,Title);}
        finally{_classicMicRecognizing=false;ReleaseClassicMicrophone();}
    }
    private void CancelClassicMicrophone()
    {
        _classicMicCancellation?.Cancel();
        if(_classicMic!=null)_classicMic.StopRecording();
        else if(!_classicMicRecognizing)ReleaseClassicMicrophone();
    }
    private void ReleaseClassicMicrophone()
    {
        _classicMicLimit?.Dispose();_classicMicLimit=null;
        var device=_classicMic;_classicMic=null;
        try{device?.Dispose();}catch(NAudio.MmException ex){System.Diagnostics.Trace.WriteLine(ex);}
        lock(_classicMicWriteGate)
        {
            var writer=_classicMicWriter;_classicMicWriter=null;
            try{writer?.Dispose();}catch(IOException ex){System.Diagnostics.Trace.WriteLine(ex);}
        }
        _classicMicCancellation?.Dispose();_classicMicCancellation=null;
        var folder=_classicMicFolder;_classicMicFolder=null;if(folder!=null && Directory.Exists(folder))
        {
            try{Directory.Delete(folder,true);}catch(IOException ex){System.Diagnostics.Trace.WriteLine(ex);}catch(UnauthorizedAccessException ex){System.Diagnostics.Trace.WriteLine(ex);}
        }
        if(!IsDisposed && !Disposing && _classicMicrophoneButton!=null){_classicMicrophoneButton.Text="🎙";RefreshClassicEditor54();}
        if(_classicMicCloseRequested && !IsDisposed && IsHandleCreated){_classicMicCloseRequested=false;BeginInvoke(new Action(Close));}
    }
 }
}
