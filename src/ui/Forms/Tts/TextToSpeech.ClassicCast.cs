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
  private ClassicCastSettings _castSettings=ClassicCastSettings.Load();
  private CancellationTokenSource _castCancellation;
  private string _castRegenerationVoice;
  private readonly HashSet<string> _castSpeakerNames=new HashSet<string>(StringComparer.Ordinal);
  private static string CL(string ru,string en)=>QwenRussian ? ru : en;
  private void InitializeClassicCast()
  {
   _videoOriginalFile=_castSettings.OriginalSubtitleFile ?? "";
   if(_castSettings.CloneConsent && string.Equals(_castSettings.PerLineVideoFile,_videoFileName,StringComparison.OrdinalIgnoreCase) && File.Exists(_videoOriginalFile))
   {var original=new Subtitle();if(original.LoadSubtitle(_videoOriginalFile,out _,null)!=null)_perLineOriginal=original;}
   ClientSize=new Size(ClientSize.Width,ClientSize.Height+36);MinimumSize=new Size(MinimumSize.Width,MinimumSize.Height+36);
   groupBoxSettings.Height-=36;listViewActors.Height-=36;
   var toolbar=new FlowLayoutPanel{Name="panelClassicCast",Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,Location=new Point(groupBoxSettings.Left,groupBoxSettings.Bottom+5),Size=new Size(ClientSize.Width-24,30),WrapContents=false};
   foreach(var pair in new[]{new[]{CL("Состав голосов…","Cast…"),"cast"},new[]{CL("Менеджер голосов…","Voice manager…"),"voices"},new[]{CL("Голоса из видео…","Voices from video…"),"video"}})
   {
    var button=new Button{Text=pair[0],AutoSize=true,Height=26};var action=pair[1];button.Click+=(s,e)=>{if(_converting || _castCancellation!=null || _qwenCancellation!=null || _localCancellation!=null || _compatibleCancellation!=null)return;if(action=="cast")EditClassicCast();else if(action=="voices"){using(var f=new ClassicVoiceManager())f.ShowDialog(this);nikseComboBoxEngine_SelectedIndexChanged(null,null);}else EditVideoVoices();};toolbar.Controls.Add(button);
   }
   Controls.Add(toolbar);
  }
  private void RefreshClassicActors()
  {
   _actors.Clear();_actors.AddRange(_subtitle.Paragraphs.Select(p=>p.Actor ?? "").Distinct());
   if(_actors.All(string.IsNullOrEmpty))_actors.Clear();
   if(_actors.Count>0 && ClientSize.Width<800){ClientSize=new Size(850,ClientSize.Height);groupBoxSettings.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left;groupBoxSettings.Width=listViewActors.Left-groupBoxSettings.Left-10;}
   if(_actors.Count>0)MinimumSize=new Size(Math.Max(800,MinimumSize.Width),MinimumSize.Height);
   nikseComboBoxEngine_SelectedIndexChanged(null,null);
  }
  private void PersistClassicCast()
  {
   foreach(ListViewItem item in listViewActors.Items)
   {var actor=(ActorAndVoice)item.Tag;var row=_castSettings.Get(actor.Actor);if(row==null){row=new ClassicCastRow{Actor=actor.Actor ?? ""};_castSettings.Rows.Add(row);}row.Selected=item.Checked;if(!string.IsNullOrEmpty(actor.Voice))row.Voice=actor.Voice;}
   _castSettings.Save();
  }
  private void EditClassicCast()
  {
   var working=_castSettings.Rows.Select(r=>r.Copy()).ToList();
   foreach(var actor in _actors)if(!working.Any(r=>r.Actor==actor))working.Add(new ClassicCastRow{Actor=actor});
   using(var f=new Form{Text=CL("Состав голосов","Cast"),Size=new Size(1000,520),MinimumSize=new Size(800,420),StartPosition=FormStartPosition.CenterParent})
   {
    var grid=new DataGridView{Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
    grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="selected",HeaderText=CL("Озвучить","Speak"),Width=65,AutoSizeMode=DataGridViewAutoSizeColumnMode.None});
    grid.Columns.Add(new DataGridViewTextBoxColumn{Name="actor",HeaderText=CL("Актёр","Actor"),ReadOnly=true});
    var engines=new DataGridViewComboBoxColumn{Name="engine",HeaderText=CL("Движок","Engine")};engines.Items.AddRange(new object[]{"Global","Qwen3","Supertonic","API"});engines.Items.AddRange(_engines.Where(e=>!IsLocalCrispTts(e.Id) && e.Id!=TextToSpeechEngineId.OpenAiCompatible).Select(e=>(object)e.Name).ToArray());grid.Columns.Add(engines);
    grid.Columns.Add("model",CL("Модель","Model"));grid.Columns.Add("voice",CL("Голос / ID","Voice / ID"));grid.Columns.Add("language",CL("Язык (ru/en)","Language (ru/en)"));grid.Columns.Add("instructions",CL("Инструкции","Instructions"));
    foreach(var row in working.Where(r=>_actors.Contains(r.Actor))){var i=grid.Rows.Add(row.Selected,string.IsNullOrEmpty(row.Actor) ? CL("(без актёра)","(no actor)") : row.Actor,string.IsNullOrEmpty(row.Engine) ? "Global" : row.Engine,row.Model,row.Voice,row.Language,row.Instructions);grid.Rows[i].Tag=row;}
    grid.CurrentCellDirtyStateChanged+=(s,e)=>{if(grid.IsCurrentCellDirty && grid.CurrentCell?.ColumnIndex==2)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
    grid.CellValueChanged+=(s,e)=>{if(e.RowIndex>=0 && e.ColumnIndex==2){var item=grid.Rows[e.RowIndex];item.Cells["model"].Value="";item.Cells["voice"].Value="";}};
    var note=new Label{Dock=DockStyle.Top,Height=48,Text=CL("Global — текущий движок. Qwen3: CustomVoice, Clone или VoiceDesign. Supertonic: M1–M5 / F1–F5. API использует ключ и адрес из настроек провайдера. Для Clone выбери импортированный голос.","Global uses the current engine. Qwen3 models: CustomVoice, Clone or VoiceDesign. Supertonic: M1–M5 / F1–F5. API uses your provider settings. Clone requires an imported reference.")};
    var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=40,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=CL("Отмена","Cancel"),DialogResult=DialogResult.Cancel};var ok=new Button{Text="OK",DialogResult=DialogResult.OK};var detect=new Button{Text=CL("Имена из текста…","Detect names…"),AutoSize=true};var choose=new Button{Text=CL("Выбрать голос…","Pick voice…"),AutoSize=true};bar.Controls.AddRange(new Control[]{cancel,ok,choose,detect});
    choose.Click+=(s,e)=>{if(grid.CurrentRow==null)return;var row=grid.CurrentRow;var isClone=Convert.ToString(row.Cells["model"].Value).IndexOf("Clone",StringComparison.OrdinalIgnoreCase)>=0;var engineName=Convert.ToString(row.Cells["engine"].Value);
     var choices=CastVoiceChoices(engineName,Convert.ToString(row.Cells["model"].Value));using(var pick=new Form{Text=CL("Голос","Voice"),ClientSize=new Size(380,85),StartPosition=FormStartPosition.CenterParent}){var combo=new ComboBox{Location=new Point(12,12),Width=356,DropDownStyle=ComboBoxStyle.DropDownList};combo.Items.AddRange(choices);if(combo.Items.Count>0)combo.SelectedIndex=0;var accept=new Button{Text="OK",Location=new Point(280,48),DialogResult=DialogResult.OK};pick.Controls.AddRange(new Control[]{combo,accept});pick.AcceptButton=accept;if(pick.ShowDialog(f)==DialogResult.OK && combo.SelectedItem!=null)row.Cells["voice"].Value=(combo.SelectedItem as ClassicQwenReference)?.Id ?? combo.Text;}};
    detect.Click+=(s,e)=>{if(DetectClassicSpeakerNames(f)){f.DialogResult=DialogResult.Retry;f.Close();}};
    f.Controls.Add(grid);f.Controls.Add(note);f.Controls.Add(bar);f.AcceptButton=ok;f.CancelButton=cancel;
    var result=f.ShowDialog(this);if(result==DialogResult.Retry){RefreshClassicActors();EditClassicCast();return;}if(result!=DialogResult.OK)return;grid.EndEdit();
    foreach(DataGridViewRow item in grid.Rows){var row=(ClassicCastRow)item.Tag;row.Selected=(bool?)item.Cells["selected"].Value==true;row.Engine=Convert.ToString(item.Cells["engine"].Value);if(row.Engine=="Global")row.Engine="";row.Model=Convert.ToString(item.Cells["model"].Value).Trim();row.Voice=Convert.ToString(item.Cells["voice"].Value).Trim();row.Language=Convert.ToString(item.Cells["language"].Value).Trim();row.Instructions=Convert.ToString(item.Cells["instructions"].Value);}
    _castSettings.Rows=working;_castSettings.Save();_checkedSpeechActors.Clear();foreach(var r in working.Where(r=>r.Selected))_checkedSpeechActors.Add(r.Actor);RefreshClassicActors();
   }
  }
  private bool DetectClassicSpeakerNames(IWin32Window owner)
  {
   var candidates=TextSpeakerDetector.Detect(_subtitle);if(candidates.Count==0){MessageBox.Show(owner,CL("Имена говорящих в тексте не найдены.","No speaker names found in text."));return false;}
   using(var f=new Form{Text=CL("Имена говорящих","Speaker names"),Size=new Size(520,420),StartPosition=FormStartPosition.CenterParent})
   {
    var list=new CheckedListBox{Dock=DockStyle.Fill};var names=candidates.Select(c=>c.Speaker).Distinct().ToArray();foreach(var name in names)list.Items.Add(name,TextSpeakerDetector.IsConfidentSpeakerName(name));
    var sticky=new CheckBox{Text=CL("Без имени — продолжение предыдущего актёра","Unnamed lines continue the previous speaker"),Dock=DockStyle.Top,Height=35,Checked=true};var ok=new Button{Text="OK",Dock=DockStyle.Bottom,DialogResult=DialogResult.OK};f.Controls.AddRange(new Control[]{list,sticky,ok});f.AcceptButton=ok;if(f.ShowDialog(owner)!=DialogResult.OK)return false;
    var confirmed=new HashSet<string>(list.CheckedItems.Cast<string>());var speech=new Subtitle(_subtitle);TextSpeakerDetector.Apply(speech,TextSpeakerDetector.Detect(speech).Where(c=>confirmed.Contains(c.Speaker)).ToList(),sticky.Checked);for(int i=0;i<_subtitle.Paragraphs.Count;i++)_subtitle.Paragraphs[i].Actor=speech.Paragraphs[i].Actor;_castSpeakerNames.UnionWith(confirmed);return confirmed.Count>0;
   }
  }
  public TextToSpeechEngine GetParagraphEngine(Paragraph p)
  {
   var row=_castSettings.Get(p.Actor);var engine=row?.Engine;
   var id=engine=="Qwen3" ? TextToSpeechEngineId.Qwen3Crisp : engine=="Supertonic" ? TextToSpeechEngineId.SupertonicCrisp : engine=="API" ? TextToSpeechEngineId.OpenAiCompatible : (TextToSpeechEngineId?)null;
   return id.HasValue ? _engines.First(e=>e.Id==id.Value) : _engines.FirstOrDefault(e=>e.Name==engine) ?? _engines.First(e=>e.Index==nikseComboBoxEngine.SelectedIndex);
  }
  public void SetCurrentVoices(Nikse.SubtitleEdit.Controls.NikseComboBox combo,Paragraph p)
  {
   var row=_castSettings.Get(p.Actor);if(row==null){SetCurrentVoices(combo);return;}
   var engine=GetParagraphEngine(p);var name=engine.Id==TextToSpeechEngineId.Qwen3Crisp ? "Qwen3" : engine.Id==TextToSpeechEngineId.SupertonicCrisp ? "Supertonic" : engine.Id==TextToSpeechEngineId.OpenAiCompatible ? "API" : engine.Name;
   combo.Items.Clear();combo.Items.AddRange(CastVoiceChoices(name,row.Model));combo.Text=GetParagraphAudio(p);
  }
  private object[] CastVoiceChoices(string engineName,string model)
  {
   if(engineName=="Supertonic")return ClassicQwenTts.SupertonicVoices.Cast<object>().ToArray();
   if(engineName=="Qwen3")return (model.IndexOf("clone",StringComparison.OrdinalIgnoreCase)>=0 || (string.IsNullOrEmpty(model) && ClassicQwenTtsSettings.Load().Clone)) ? ClassicQwenReference.List().Cast<object>().ToArray() : ClassicQwenTts.Voices.Cast<object>().ToArray();
   if(engineName=="API"){var settings=ClassicCompatibleTtsSettings.Load();return ClassicCompatibleTts.Voices(settings.Provider,string.IsNullOrEmpty(model) ? settings.Selected().Model : model).Cast<object>().ToArray();}
   if(engineName=="Global")return nikseComboBoxVoice.Items.Cast<object>().ToArray();
   var localDefinition=ClassicLocalTts.Catalog.FirstOrDefault(d=>d.Name==engineName);if(localDefinition!=null)return ClassicLocalTts.Voices(localDefinition.Id);
   var selected=_engines.FirstOrDefault(e=>e.Name==engineName);if(selected==null)return new object[0];
   var before=nikseComboBoxEngine.SelectedIndex;var voice=nikseComboBoxVoice.Text;var region=nikseComboBoxRegion.Text;var language=nikseComboBoxLanguage.Text;
   try{nikseComboBoxEngine.SelectedIndex=selected.Index;if(nikseComboBoxRegion.Items.Cast<object>().Any(v=>v.ToString()==model))nikseComboBoxRegion.Text=model;return nikseComboBoxVoice.Items.Cast<object>().ToArray();}
   finally{nikseComboBoxEngine.SelectedIndex=before;nikseComboBoxVoice.Text=voice;nikseComboBoxRegion.Text=region;nikseComboBoxLanguage.Text=language;}
  }
  private int CastVoiceIndex(string voice)
  {
   if(string.IsNullOrEmpty(voice))return -1;
   for(int i=0;i<nikseComboBoxVoice.Items.Count;i++)
   {
    var value=nikseComboBoxVoice.Items[i];if(value.ToString()==voice)return i;
    foreach(var name in new[]{"Voice","Name","DisplayName","Id"})if(Convert.ToString(value.GetType().GetProperty(name)?.GetValue(value,null))==voice)return i;
   }
   return -1;
  }
  private async Task<bool> GenerateLegacyCastAsync(Subtitle subtitle,List<int> indices,string engineName,CancellationToken cancel,string outputOverride=null)
  {
   var target=_engines.FirstOrDefault(e=>e.Name==engineName);if(target==null)throw new ArgumentException("Unknown cast engine: "+engineName);
   var beforeEngine=nikseComboBoxEngine.SelectedIndex;var beforeVoice=nikseComboBoxVoice.Text;var beforeRegion=nikseComboBoxRegion.Text;var beforeLanguage=nikseComboBoxLanguage.Text;
   try
   {
    nikseComboBoxEngine.SelectedIndex=target.Index;
    foreach(var i in indices)
    {
     cancel.ThrowIfCancellationRequested();var p=subtitle.Paragraphs[i];var row=_castSettings.Get(p.Actor).Copy();if(_castRegenerationVoice!=null)row.Voice=_castRegenerationVoice;
     if(!string.IsNullOrEmpty(row.Model))
     {
      if(nikseComboBoxRegion.Items.Cast<object>().Any(v=>v.ToString()==row.Model))nikseComboBoxRegion.Text=row.Model;
      else if(nikseComboBoxLanguage.Items.Cast<object>().Any(v=>v.ToString()==row.Model))nikseComboBoxLanguage.Text=row.Model;
      else throw new ArgumentException("This engine does not expose the selected model: "+row.Model);
     }
     if(!string.IsNullOrWhiteSpace(row.Voice)){var index=CastVoiceIndex(row.Voice);if(index<0)throw new ArgumentException("Cast voice is not available: "+row.Voice);nikseComboBoxVoice.SelectedIndex=index;var actor=_actorAndVoices.FirstOrDefault(a=>a.Actor==p.Actor);if(actor!=null){actor.Voice=row.Voice;actor.VoiceIndex=index;}}
     var single=new Subtitle();single.Paragraphs.Add(p);if(!await GenerateDefaultAudioAsync(single,false,outputOverride ?? i+".wav"))return false;
    }
    return true;
   }
   finally{nikseComboBoxEngine.SelectedIndex=beforeEngine;nikseComboBoxVoice.Text=beforeVoice;nikseComboBoxRegion.Text=beforeRegion;nikseComboBoxLanguage.Text=beforeLanguage;SetQwenBusy(true);}
  }
  private bool HasClassicCast(Subtitle subtitle)=>subtitle.Paragraphs.Any(p=>{var row=_castSettings.Get(p.Actor);return row!=null && (!string.IsNullOrEmpty(row.Engine) || !string.IsNullOrEmpty(row.Model) || !string.IsNullOrEmpty(row.Instructions) || !string.IsNullOrEmpty(row.Language));});
  private async Task<bool> GenerateClassicCastAsync(Subtitle subtitle,string outputOverride=null)
  {
   _castCancellation=new CancellationTokenSource();var cancel=_castCancellation.Token;SetQwenBusy(true);
   try
   {
    var groups=Enumerable.Range(0,subtitle.Paragraphs.Count).Where(i=>!string.IsNullOrWhiteSpace(subtitle.Paragraphs[i].Text)).GroupBy(i=>{var row=_castSettings.Get(subtitle.Paragraphs[i].Actor);return (row?.Engine ?? "")+"|"+(row?.Model ?? "")+"|"+(row?.Language ?? "");});
    foreach(var group in groups)
    {
     cancel.ThrowIfCancellationRequested();var row=_castSettings.Get(subtitle.Paragraphs[group.First()].Actor) ?? new ClassicCastRow();var choice=row?.Engine ?? "";
     if(string.IsNullOrEmpty(choice)){var id=_engines.First(e=>e.Index==nikseComboBoxEngine.SelectedIndex).Id;choice=id==TextToSpeechEngineId.Qwen3Crisp ? "Qwen3" : id==TextToSpeechEngineId.SupertonicCrisp ? "Supertonic" : id==TextToSpeechEngineId.OpenAiCompatible ? "API" : LocalId(id)!=null ? _engines.First(e=>e.Id==id).Name : "";}
     if(string.IsNullOrEmpty(choice))
     {var filtered=new Subtitle(subtitle);for(var i=0;i<filtered.Paragraphs.Count;i++)if(!group.Contains(i))filtered.Paragraphs[i].Text="";if(!await GenerateDefaultAudioAsync(filtered,true,outputOverride))return false;continue;}
     var localDefinition=ClassicLocalTts.Catalog.FirstOrDefault(d=>d.Name==choice);
     if(localDefinition!=null)
     {
      var config=ClassicLocalTtsSettings.Load(localDefinition.Id);if(!string.IsNullOrWhiteSpace(row.Model))config.Model=row.Model;if(!string.IsNullOrWhiteSpace(row.Language))config.Language=row.Language;if(!string.IsNullOrWhiteSpace(row.Instructions))config.Instructions=row.Instructions;if(!AcceptAudioLicense(localDefinition.Id,config))return false;
      using(var engine=new ClassicLocalTts(localDefinition.Id,config))foreach(var i in group){cancel.ThrowIfCancellationRequested();var p=subtitle.Paragraphs[i];var actor=_castSettings.Get(p.Actor) ?? new ClassicCastRow();var voice=_castRegenerationVoice ?? (string.IsNullOrWhiteSpace(actor.Voice) ? config.Voice : actor.Voice);if(_perLineOriginal!=null && localDefinition.SupportsCloning)voice=await PerLineReferenceAsync(p,cancel) ?? voice;labelProgress.Text=CL("Озвучка актёра: ","Speaking actor: ")+p.Actor;await engine.SpeakAsync(Utilities.UnbreakLine(HtmlUtil.RemoveHtmlTags(p.Text)),voice,Path.Combine(_waveFolder,outputOverride ?? i+".wav"),cancel,new Progress<SpeechProgress>(x=>labelProgress.Text=x.Message));}continue;
     }
     var onlineId=_engines.Where(e=>e.Name==choice).Select(e=>FinalOnlineId(e.Id)).FirstOrDefault();
     if(onlineId!=null) {
      var config=ClassicOnlineTtsSettings.Load(onlineId);if(!string.IsNullOrWhiteSpace(row.Model))config.Model=row.Model;
      using(var engine=new ClassicOnlineTts(onlineId,config))foreach(var i in group){cancel.ThrowIfCancellationRequested();var p=subtitle.Paragraphs[i];var actor=_castSettings.Get(p.Actor);var voice=ClassicOnlineTts.ResolveVoice(onlineId,_castRegenerationVoice ?? actor?.Voice,config.Voice);await engine.SpeakAsync(Utilities.UnbreakLine(HtmlUtil.RemoveHtmlTags(p.Text)),voice,Path.Combine(_waveFolder,outputOverride ?? i+".wav"),cancel);}continue;
     }
     if(choice!="Qwen3" && choice!="Supertonic" && choice!="API")
     {
      if(!await GenerateLegacyCastAsync(subtitle,group.ToList(),choice,cancel,outputOverride))return false;
      continue;
     }
     if(choice=="API")
     {
      var all=ClassicCompatibleTtsSettings.Load();var config=Newtonsoft.Json.JsonConvert.DeserializeObject<ClassicCompatibleProvider>(Newtonsoft.Json.JsonConvert.SerializeObject(all.Selected()));if(!string.IsNullOrEmpty(row.Model))config.Model=row.Model;
      using(var engine=new ClassicCompatibleTts(config))foreach(var i in group){cancel.ThrowIfCancellationRequested();var p=subtitle.Paragraphs[i];var actor=_castSettings.Get(p.Actor) ?? new ClassicCastRow();await engine.SpeakAsync(Utilities.UnbreakLine(HtmlUtil.RemoveHtmlTags(p.Text)),_castRegenerationVoice ?? (string.IsNullOrEmpty(actor.Voice) ? config.Voice : actor.Voice),Path.Combine(_waveFolder,outputOverride ?? i+".wav"),cancel,string.IsNullOrWhiteSpace(actor.Instructions) ? null : actor.Instructions);}
     }
     else
     {
      var config=ClassicQwenTtsSettings.Load(choice=="Supertonic");if(choice=="Qwen3" && !string.IsNullOrWhiteSpace(row.Model)){
       if(!new[]{"CustomVoice","Clone","VoiceDesign"}.Contains(row.Model,StringComparer.OrdinalIgnoreCase))throw new ArgumentException("Qwen3 model must be CustomVoice, Clone or VoiceDesign");
       config.Design=row.Model.Equals("VoiceDesign",StringComparison.OrdinalIgnoreCase);config.Clone=row.Model.Equals("Clone",StringComparison.OrdinalIgnoreCase);
      }if(!string.IsNullOrEmpty(row.Language))config.Language=row.Language;if(!string.IsNullOrWhiteSpace(row.Instructions))config.Instructions=row.Instructions;if(_perLineOriginal!=null && choice=="Qwen3"){config.Clone=true;config.Design=false;}
      using(var engine=new ClassicQwenTts(config)){await engine.StartAsync(cancel,new Progress<SpeechProgress>(p=>labelProgress.Text=p.Message));foreach(var i in group){cancel.ThrowIfCancellationRequested();var p=subtitle.Paragraphs[i];var actor=_castSettings.Get(p.Actor) ?? new ClassicCastRow();var voice=_castRegenerationVoice ?? (string.IsNullOrEmpty(actor.Voice) ? config.Voice : actor.Voice);if(config.Clone && _perLineOriginal!=null)voice=await PerLineReferenceAsync(p,cancel) ?? voice;if(config.Clone)voice=ClassicQwenReference.List().FirstOrDefault(v=>v.Id==voice || v.Name==voice)?.Id ?? voice;labelProgress.Text=CL("Озвучка актёра: ","Speaking actor: ")+p.Actor;await engine.SpeakAsync(Utilities.UnbreakLine(HtmlUtil.RemoveHtmlTags(p.Text)),voice,Path.Combine(_waveFolder,outputOverride ?? i+".wav"),cancel,string.IsNullOrWhiteSpace(actor.Instructions) ? null : actor.Instructions);}}
     }
    }
    return true;
   }
   catch(OperationCanceledException){_abort=true;return false;}
   finally{CleanupPerLineReferences();_castCancellation.Dispose();_castCancellation=null;SetQwenBusy(false);}
  }
 }
}
