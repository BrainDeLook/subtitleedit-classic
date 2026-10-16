using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicCast;
namespace Nikse.SubtitleEdit.Forms.Tts
{
    public sealed partial class TextToSpeech
    {
        public int PlaybackFromIndex {get;set;}
        public IClassicSpeechVideo PlaybackVideo {get;set;}
        public Action<double> SeekPlaybackVideo {get;set;}
        private CancellationTokenSource _playbackCancellation;
        private Button _playSpeech;
        private void InitializeClassicPlayback()
        {
            var oldSize=ClientSize;var existing=Controls.Cast<Control>().ToArray();var bounds=existing.Select(c=>c.Bounds).ToArray();var anchors=existing.Select(c=>c.Anchor).ToArray();
            SuspendLayout();foreach(var c in existing)c.Anchor=AnchorStyles.Top|AnchorStyles.Left;Controls.Clear();
            ClientSize=new Size(oldSize.Width,oldSize.Height+72);MinimumSize=new Size(MinimumSize.Width,MinimumSize.Height+72);
            var content=new Panel{Name="classicTtsContent",Bounds=new Rectangle(Point.Empty,oldSize),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right};Controls.Add(content);content.SuspendLayout();content.Controls.AddRange(existing);
            for(int i=0;i<existing.Length;i++){existing[i].Bounds=bounds[i];existing[i].Anchor=anchors[i];}content.ResumeLayout(false);
            var bar=new FlowLayoutPanel{Name="classicPlayback",Location=new Point(0,oldSize.Height),Size=new Size(oldSize.Width,72),Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,Padding=new Padding(8,3,8,3),WrapContents=true};
            _playSpeech=new Button{AutoSize=true,Text=CL("Озвучка с текущей строки…","Play with speech from current line…")};
            _playSpeech.Click+=async(s,e)=>await PlayClassicSpeechAsync();
            var pause=new Button{AutoSize=true,Text=CL("Пауза / продолжить","Pause / resume")};pause.Click+=(s,e)=>{if(_playbackCancellation==null || PlaybackVideo==null)return;if(PlaybackVideo.IsPlaying)PlaybackVideo.Pause();else PlaybackVideo.Play();};
            _importSpeechSession=new Button{AutoSize=true,Text=CL("Импорт сессии…","Import session…"),Name="buttonImportSpeechSession"};_importSpeechSession.Click+=async(s,e)=>await ChooseSpeechSessionAsync();
            bar.Controls.AddRange(new Control[]{_playSpeech,pause,_importSpeechSession});Controls.Add(bar);FormClosed+=(s,e)=>CleanupSessionReferences();ResumeLayout(false);Shown+=(s,e)=>_playSpeech.Text=CL("Озвучить со строки ","Speak from line ")+(PlaybackFromIndex+1)+"…";
        }
        private async Task PlayClassicSpeechAsync()
        {
            if(_playbackCancellation!=null){_playbackCancellation.Cancel();_castCancellation?.Cancel();_localCancellation?.Cancel();_qwenCancellation?.Cancel();_compatibleCancellation?.Cancel();return;}
            if(_converting || _qwenCancellation!=null || _localCancellation!=null || _castCancellation!=null)return;
            if(PlaybackVideo==null || !PlaybackVideo.HasVideo){MessageBox.Show(this,CL("Сначала открой видео в главном окне.","Open a video in the main window first."));return;}
            if (!EnsureCurrentSpeechVideo()) return;
            SynchronizeMainSubtitle();var playbackIndex=ResolvePlaybackIndex();
            if(!CaptureSpeechActors())return;SaveConfiguration();PersistClassicCast();
            var lines=SpeakingSubtitle(_subtitle,true).Paragraphs.Select((p,i)=>new ClassicSpeechLine{Paragraph=p,Index=i}).Where(l=>l.Index>=playbackIndex && !string.IsNullOrWhiteSpace(l.Text)).OrderBy(l=>l.Start).ToList();
            if(lines.Count==0)return;
            var local=new Dictionary<string,ClassicLocalTts>();var qwen=new Dictionary<string,ClassicQwenTts>();
            var definition=_engines.First(x=>x.Index==nikseComboBoxEngine.SelectedIndex);
            var global=IsLocalCrispTts(definition.Id) ? (definition.Id==TextToSpeechEngineId.SupertonicCrisp ? "Supertonic" : "Qwen3") : CompatibleSelected ? "API" : definition.Name;
            CaptureFinalOnline();
            if(SelectedLocalId!=null)CaptureLocal(false);else if(IsLocalCrispTts(definition.Id))CaptureQwen(false);
            var previousFolder=_waveFolder;var folder=Path.Combine(Path.GetTempPath(),"ClassicSpeechPlayback",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);_waveFolder=folder;
            _playbackCancellation=new CancellationTokenSource();var cancel=_playbackCancellation.Token;_converting=true;SetLocalBusy(true);buttonGenerateTTS.Enabled=false;_playSpeech.Text=CL("Остановить озвучку","Stop speech");
            try {
                async Task<string> Generate(ClassicSpeechLine line,string outputFolder,CancellationToken token) {
                    token.ThrowIfCancellationRequested();var row=SpeechCastRow(line.Paragraph) ?? new ClassicCastRow();var choice=string.IsNullOrWhiteSpace(row.Engine)?global:row.Engine;var output=Path.Combine(outputFolder,Guid.NewGuid().ToString("N")+".wav");var text=Utilities.UnbreakLine(HtmlUtil.RemoveHtmlTags(line.Text));
                    var d=ClassicLocalTts.Catalog.FirstOrDefault(x=>x.Name==choice);
                    if(d!=null) {
                        var config=ClassicLocalTtsSettings.Load(d.Id);if(!string.IsNullOrEmpty(row.Model))config.Model=row.Model;if(!string.IsNullOrEmpty(row.Language))config.Language=row.Language;if(!string.IsNullOrEmpty(row.Instructions))config.Instructions=row.Instructions;
                        if(!AcceptAudioLicense(d.Id,config))throw new OperationCanceledException();
                        var key=d.Id+"|"+config.Model+"|"+config.Language+"|"+config.Instructions;
                        if(!local.TryGetValue(key,out var engine)){engine=new ClassicLocalTts(d.Id,config);local.Add(key,engine);}
                        var voice=string.IsNullOrWhiteSpace(row.Voice)?config.Voice:ClassicLocalTts.ResolveVoice(row.Voice);if(_perLineOriginal!=null && d.SupportsCloning)voice=await PerLineReferenceAsync(line.Paragraph,token,d.Id) ?? voice;
                        await engine.SpeakAsync(text,voice,output,token);return output;
                    }
                    if(choice=="Qwen3" || choice=="Supertonic") {
                        var config=ClassicQwenTtsSettings.Load(choice=="Supertonic");if(!string.IsNullOrEmpty(row.Model)){config.Clone=row.Model=="Clone";config.Design=row.Model=="VoiceDesign";}if(!string.IsNullOrEmpty(row.Language))config.Language=row.Language;if(!string.IsNullOrEmpty(row.Instructions))config.Instructions=row.Instructions;
                        if(_perLineOriginal!=null && choice=="Qwen3"){config.Clone=true;config.Design=false;}
                        var key=choice+"|"+config.Clone+"|"+config.Design+"|"+config.Language+"|"+config.Instructions;
                        if(!qwen.TryGetValue(key,out var engine)){engine=new ClassicQwenTts(config);qwen.Add(key,engine);await engine.StartAsync(token,null);}
                        var voice=string.IsNullOrWhiteSpace(row.Voice)?config.Voice:row.Voice;if(config.Clone && _perLineOriginal!=null)voice=await PerLineReferenceAsync(line.Paragraph,token) ?? voice;if(config.Clone)voice=ClassicLocalTts.ResolveVoice(voice);
                        await engine.SpeakAsync(text,voice,output,token,config.Instructions);return output;
                    }
                    var onlineId=_engines.Where(e=>e.Name==choice).Select(e=>FinalOnlineId(e.Id)).FirstOrDefault();
                    if(onlineId!=null) {
                        var config=ClassicOnlineTtsSettings.Load(onlineId);if(!string.IsNullOrWhiteSpace(row.Model))config.Model=row.Model;
                        using(var engine=new ClassicOnlineTts(onlineId,config))await engine.SpeakAsync(text,ClassicOnlineTts.ResolveVoice(onlineId,row.Voice,config.Voice),output,token);return output;
                    }
                    if(choice=="API") {
                        var config=ClassicCompatibleTtsSettings.Load().Selected();using(var engine=new ClassicCompatibleTts(config))await engine.SpeakAsync(text,string.IsNullOrWhiteSpace(row.Voice)?config.Voice:row.Voice,output,token,row.Instructions);return output;
                    }
                    var single=new Subtitle();single.Paragraphs.Add(new Paragraph(line.Paragraph));if(!await GenerateParagraphAudioAsync(single,false,Path.GetFileName(output)))throw new OperationCanceledException();return output;
                }
                SeekPlaybackVideo?.Invoke(lines[0].Start);
                var reader=new ClassicSpeechReader(Generate);
                await reader.RunAsync(lines,PlaybackVideo,i=>labelProgress.Text=CL("Озвучка строки ","Speaking line ")+(lines[i].Index+1),i=>labelProgress.Text=CL("Подготовка строки ","Preparing line ")+(lines[i].Index+1),cancel);
            }catch(OperationCanceledException){}catch(Exception ex){HandleError(ex);}
            finally {
                foreach(var e in local.Values)e.Dispose();foreach(var e in qwen.Values)e.Dispose();CleanupPerLineReferences();_waveFolder=previousFolder;
                if(Directory.Exists(folder))Directory.Delete(folder,true);_playbackCancellation.Dispose();_playbackCancellation=null;_converting=false;SetLocalBusy(false);buttonGenerateTTS.Enabled=true;_playSpeech.Text=CL("Озвучка с текущей строки…","Play with speech from current line…");labelProgress.Text="";
            }
        }
    }
}
