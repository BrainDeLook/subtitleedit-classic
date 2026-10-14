using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace Nikse.SubtitleEdit.Forms.Tts
{
 public sealed partial class TextToSpeech
 {
  private Subtitle _perLineOriginal;
  private readonly List<ClassicQwenReference> _temporaryVideoVoices=new List<ClassicQwenReference>();
  private string _videoOriginalFile="";
  private void EditVideoVoices()
  {
   if(string.IsNullOrEmpty(_videoFileName)){MessageBox.Show(this,CL("Сначала открой видео.","Open a video first."));return;}
   using(var f=new Form{Text=CL("Клонирование голосов из видео","Clone voices from video"),Size=new Size(790,550),MinimumSize=new Size(720,470),StartPosition=FormStartPosition.CenterParent})
   {
    var grid=new DataGridView{Dock=DockStyle.Fill,AllowUserToAddRows=false,RowHeadersVisible=false};grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="enabled",HeaderText=CL("Выбрать","Select"),Width=65});grid.Columns.Add("speaker",CL("Говорящий","Speaker"));grid.Columns[1].ReadOnly=true;grid.Columns.Add("name",CL("Имя актёра","Actor name"));grid.Columns.Add("text",CL("Точный текст образца","Exact reference transcript"));grid.Columns[3].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;grid.Columns[3].DefaultCellStyle.WrapMode=DataGridViewTriState.True;grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.DisplayedCells;
    var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=128,WrapContents=true};var original=new Button{Text=CL("Оригинальные субтитры…","Original subtitles…"),AutoSize=true};var path=new TextBox{Width=460,ReadOnly=true,Text=_videoOriginalFile};var language=new ComboBox{Width=100,DropDownStyle=ComboBoxStyle.DropDownList};language.Items.AddRange(new object[]{"ru","en"});language.SelectedItem=ClassicQwenTtsSettings.Load().Language;if(language.SelectedIndex<0)language.SelectedIndex=0;
    var perLine=new CheckBox{Text=CL("Клонировать голос каждой реплики (Qwen3 / CosyVoice / VoxCPM / Chatterbox)","Clone each line (Qwen3 / CosyVoice / VoxCPM / Chatterbox)"),AutoSize=true,Checked=_perLineOriginal!=null};var consent=new CheckBox{Text=CL("Это мои голоса или есть разрешение владельцев.","These are my voices or I have the speakers' permission."),AutoSize=true,Checked=_castSettings.CloneConsent && string.Equals(_castSettings.PerLineVideoFile,_videoFileName,StringComparison.OrdinalIgnoreCase)};top.Controls.AddRange(new Control[]{original,path,language,perLine,consent});
    Subtitle source=null,detected=null;var references=new Dictionary<string,List<Paragraph>>();
    Action<Subtitle,Subtitle> fillSpeakers=(spoken,segments)=>{var mapped=SpeakerLabelParser.AssignSpeakersByOverlap(spoken.Paragraphs,segments.Paragraphs);references.Clear();grid.Rows.Clear();foreach(var group in mapped.GroupBy(p=>p.Value)){var picked=ClassicVideoVoices.PickReferences(group.Select(p=>p.Key));if(picked.Count==0)continue;references[group.Key]=picked;grid.Rows.Add(true,group.Key,group.Key,ClassicVideoVoices.Transcript(picked));}};
    var existing=new Button{Text=CL("По актёрам субтитров","Use subtitle actors"),AutoSize=true};top.Controls.Add(existing);
    existing.Click+=(s,e)=>{if(source==null && File.Exists(path.Text)){source=new Subtitle();if(source.LoadSubtitle(path.Text,out _,null)==null)source=null;}if(source==null){MessageBox.Show(f,CL("Выбери оригинальные субтитры с произнесённым текстом.","Select original subtitles containing the spoken text."));return;}if(!_subtitle.Paragraphs.Any(p=>!string.IsNullOrWhiteSpace(p.Actor))){MessageBox.Show(f,CL("Сначала назначь актёров репликам в редакторе или через состав голосов.","Assign actors in the editor or cast dialog first."));return;}detected=new Subtitle(_subtitle);fillSpeakers(source,detected);};original.Click+=(s,e)=>{using(var open=new OpenFileDialog{Filter="Subtitles|*.srt;*.ass;*.ssa;*.vtt"})if(open.ShowDialog(f)==DialogResult.OK){source=new Subtitle();var format=source.LoadSubtitle(open.FileName,out _,null);if(format==null){MessageBox.Show(f,CL("Не удалось прочитать субтитры.","Could not read subtitles."));source=null;return;}path.Text=open.FileName;}};
    var status=new Label{Dock=DockStyle.Bottom,Height=34,AutoEllipsis=true,Text=CL("Нужен текст, произнесённый в исходной дорожке, а не перевод. Проверь расшифровку образцов перед импортом.","References need the original spoken words, never their translation. Review reference transcripts before importing.")};
    var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=40,FlowDirection=FlowDirection.RightToLeft};var close=new Button{Text=CL("Закрыть","Close")};var apply=new Button{Text=CL("Применить","Apply"),AutoSize=true};var find=new Button{Text=CL("Найти говорящих","Find speakers"),AutoSize=true};var cancel=new Button{Text=CL("Отмена","Cancel"),Visible=false};bar.Controls.AddRange(new Control[]{close,apply,find,cancel});f.Controls.Add(grid);f.Controls.Add(top);f.Controls.Add(status);f.Controls.Add(bar);
    CancellationTokenSource operation=null;string job=null;bool closeRequested=false;close.Click+=(s,e)=>f.Close();cancel.Click+=(s,e)=>operation?.Cancel();f.FormClosing+=(s,e)=>{if(operation!=null){closeRequested=true;operation.Cancel();e.Cancel=true;}};
    find.Click+=async(s,e)=>
    {
     if(operation!=null)return;operation=new CancellationTokenSource();find.Enabled=apply.Enabled=top.Enabled=false;cancel.Visible=true;job=Path.Combine(Path.GetTempPath(),"se-video-voices-"+Guid.NewGuid().ToString("N"));
     try
     {
      detected=await ClassicVideoVoices.FindAsync(_videoFileName,_dubbingAudioTrack,language.Text,ClassicQwenTtsSettings.Load().Variant,job,operation.Token,new Progress<SpeechProgress>(p=>status.Text=p.Message));
      fillSpeakers(source ?? detected,detected);
      status.Text=CL("Проверь имена и точный текст. Затем нажми «Применить».","Review names and exact transcripts, then Apply.");
     }catch(OperationCanceledException){status.Text=CL("Отменено","Cancelled");}catch(Exception error){MessageBox.Show(f,error.Message,Text);}finally{operation.Dispose();operation=null;find.Enabled=apply.Enabled=top.Enabled=true;cancel.Visible=false;if(job!=null && Directory.Exists(job))Directory.Delete(job,true);job=null;if(closeRequested)f.Close();}
    };
    apply.Click+=async(s,e)=>
    {
     if(operation!=null)return;if(!consent.Checked){MessageBox.Show(f,CL("Подтверди разрешение на использование голосов.","Confirm permission to use the voices."));return;}
     if(source==null && !string.IsNullOrEmpty(path.Text)){source=new Subtitle();if(source.LoadSubtitle(path.Text,out _,null)==null){source=null;return;}}
     if(perLine.Checked && source==null){MessageBox.Show(f,CL("Для клонирования каждой реплики выбери оригинальные субтитры.","Select original subtitles for per-line cloning."));return;}
     grid.EndEdit();operation=new CancellationTokenSource();find.Enabled=apply.Enabled=top.Enabled=grid.Enabled=false;cancel.Visible=true;var added=new List<ClassicQwenReference>();job=Path.Combine(Path.GetTempPath(),"se-clone-speakers-"+Guid.NewGuid().ToString("N"));
     try
     {
      var actorNames=new Dictionary<string,string>();var newRows=new List<ClassicCastRow>();
      foreach(DataGridViewRow row in grid.Rows)
      {
       if((bool?)row.Cells[0].Value!=true)continue;var speaker=Convert.ToString(row.Cells[1].Value);var name=Convert.ToString(row.Cells[2].Value).Trim();var transcript=Convert.ToString(row.Cells[3].Value).Trim();if(string.IsNullOrEmpty(name)||string.IsNullOrEmpty(transcript))throw new ArgumentException("Actor name and exact transcript are required");
       var parts=references[speaker];var reference=await ClassicVideoVoices.BuildReferenceAsync(_videoFileName,parts,_dubbingAudioTrack,Path.Combine(job,"speaker"+added.Count),operation.Token,new Progress<SpeechProgress>(p=>status.Text=p.Message));var seconds=ClassicVoiceAssets.WaveSeconds(reference);var voice=await ClassicQwenReference.ImportAsync(reference,name,transcript,0,Math.Min(30,seconds),true,operation.Token);added.Add(voice);actorNames[speaker]=name;newRows.Add(new ClassicCastRow{Actor=name,Engine="Qwen3",Model="Clone",Voice=voice.Id,Selected=true});
      }
      operation.Token.ThrowIfCancellationRequested();
      if(detected!=null)foreach(var pair in SpeakerLabelParser.AssignSpeakersByOverlap(_subtitle.Paragraphs,detected.Paragraphs))if(actorNames.TryGetValue(pair.Value,out var name))pair.Key.Actor=name;
      foreach(var row in newRows){_castSettings.Rows.RemoveAll(r=>r.Actor==row.Actor);_castSettings.Rows.Add(row);_checkedSpeechActors.Add(row.Actor);}_castSettings.OriginalSubtitleFile=path.Text;_castSettings.PerLineVideoFile=perLine.Checked ? _videoFileName : "";_castSettings.CloneConsent=consent.Checked;_castSettings.Save();_perLineOriginal=perLine.Checked ? source : null;if(perLine.Checked && SelectedLocalId==null)nikseComboBoxEngine.Text="Qwen3 TTS (CrispASR / local)";_videoOriginalFile=path.Text;RefreshClassicActors();status.Text=CL("Голоса добавлены. Проверь их в менеджере голосов.","Voices added. Review them in the voice manager.");
     }catch(OperationCanceledException){foreach(var v in added)ClassicVoiceLibrary.Delete(v);}catch(Exception error){foreach(var v in added)ClassicVoiceLibrary.Delete(v);MessageBox.Show(f,error.Message,Text);}finally{operation.Dispose();operation=null;find.Enabled=apply.Enabled=top.Enabled=grid.Enabled=true;cancel.Visible=false;if(Directory.Exists(job))Directory.Delete(job,true);job=null;if(closeRequested)f.Close();}
    };
    f.ShowDialog(this);
   }
  }
  private async Task<string> PerLineReferenceAsync(Paragraph target,CancellationToken cancel)
  {
   if(_perLineOriginal==null)return null;
   var source=_perLineOriginal.Paragraphs.Where(p=>p.StartTime.TotalSeconds<target.EndTime.TotalSeconds && p.EndTime.TotalSeconds>target.StartTime.TotalSeconds)
    .OrderByDescending(p=>Math.Min(p.EndTime.TotalSeconds,target.EndTime.TotalSeconds)-Math.Max(p.StartTime.TotalSeconds,target.StartTime.TotalSeconds)).FirstOrDefault();
   if(source==null || source.DurationTotalMilliseconds<1000 || source.DurationTotalMilliseconds>30000)return null;
   var job=Path.Combine(_waveFolder,"reference-"+Guid.NewGuid().ToString("N"));try{var recording=await ClassicVideoVoices.BuildReferenceAsync(_videoFileName,new List<Paragraph>{source},_dubbingAudioTrack,job,cancel,null);var voice=await ClassicQwenReference.ImportAsync(recording,"temporary line",ClassicVideoVoices.Transcript(new[]{source}),0,source.Duration.TotalSeconds,true,cancel);_temporaryVideoVoices.Add(voice);return voice.Id;}finally{if(Directory.Exists(job))Directory.Delete(job,true);}
  }
  private void CleanupPerLineReferences(){foreach(var voice in _temporaryVideoVoices)ClassicVoiceLibrary.Delete(voice);_temporaryVideoVoices.Clear();}
 }
}
