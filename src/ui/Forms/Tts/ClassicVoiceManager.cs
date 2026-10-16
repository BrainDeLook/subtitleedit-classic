using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.ClassicCast;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace Nikse.SubtitleEdit.Forms.Tts
{
 public sealed partial class ClassicVoiceManager : Form
 {
  private readonly ListBox _voices=new ListBox{Dock=DockStyle.Left,Width=235,SelectionMode=SelectionMode.MultiExtended};
  private readonly TextBox _name=new TextBox{Dock=DockStyle.Top};
  private readonly TextBox _transcript=new TextBox{Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Vertical};
  private readonly Label _progress=new Label{Dock=DockStyle.Bottom,Height=25,AutoEllipsis=true};
  private readonly FlowLayoutPanel _toolbar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=65,WrapContents=true,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink};
  private readonly Button _cancel=new Button{Text="Отмена",Visible=false};
  private readonly System.Media.SoundPlayer _player=new System.Media.SoundPlayer();
  private CancellationTokenSource _operation;private bool _closeRequested;
  private static bool Ru=>(Nikse.SubtitleEdit.Core.Common.Configuration.Settings.General.Language ?? "").StartsWith("ru",StringComparison.OrdinalIgnoreCase);
  private static string L(string ru,string en)=>Ru ? ru : en;
  public ClassicVoiceManager()
  {
   Text=L("Менеджер голосов","Voice manager");Size=new Size(820,640);MinimumSize=new Size(780,580);StartPosition=FormStartPosition.CenterParent;Padding=new Padding(10);
   var editor=new Panel{Dock=DockStyle.Fill,Padding=new Padding(10,0,0,0)};editor.Controls.Add(_transcript);editor.Controls.Add(new Label{Text=L("Точный текст записи","Exact reference transcript"),Dock=DockStyle.Top,Height=24});editor.Controls.Add(_name);editor.Controls.Add(new Label{Text=L("Имя голоса","Voice name"),Dock=DockStyle.Top,Height=24});
   var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=35,FlowDirection=FlowDirection.RightToLeft};var close=new Button{Text=L("Закрыть","Close")};close.Click+=(s,e)=>Close();bottom.Controls.Add(close);bottom.Controls.Add(_cancel);_cancel.Text=L("Отмена","Cancel");_cancel.Click+=(s,e)=>_operation?.Cancel();
   var content=new Panel{Dock=DockStyle.Fill};content.Controls.Add(editor);content.Controls.Add(_voices);Controls.Add(content);Controls.Add(_toolbar);Controls.Add(_progress);Controls.Add(bottom);
   Add(L("Прослушать","Listen"),()=>PlayReference(0));
   Add(L("Сохранить","Save"),()=>{var v=Selected();if(v==null)return;ClassicVoiceLibrary.Update(v,_name.Text,_transcript.Text);Reload(v.Id);});
   Add(L("Импорт…","Import…"),()=>{using(var f=new ClassicQwenReferenceImport(false))if(f.ShowDialog(this)==DialogResult.OK)Reload(f.Reference.Id);});
   Add(L("Обрезать образец…","Trim reference…"),()=>{var v=Selected();if(v==null)return;using(var f=new ClassicQwenReferenceImport(v, false))if(f.ShowDialog(this)==DialogResult.OK)Reload(f.Reference.Id);});
   Add(L("Наборы голосов…","Voice packs…"),()=>PickPack());
   Add(L("Экспорт ZIP…","Export ZIP…"),()=>{var v=Selected();if(v==null)return;using(var f=new SaveFileDialog{Filter="Voice pack|*.zip",FileName="voices.zip"})if(f.ShowDialog(this)==DialogResult.OK)ClassicVoiceLibrary.Export(_voices.SelectedItems.Cast<ClassicQwenReference>(),f.FileName);});
   Add(L("Импорт ZIP…","Import ZIP…"),()=>{using(var f=new OpenFileDialog{Filter="Voice pack|*.zip"})if(f.ShowDialog(this)==DialogResult.OK && Permission())Run(async token=>{var voices=await ClassicVoiceLibrary.ImportPackAsync(f.FileName,true,token);Reload(voices.FirstOrDefault()?.Id);});});
   Add(L("Копировать образец…","Copy reference…"),()=>{var v=Selected();if(v==null)return;using(var f=new FolderBrowserDialog{Description=L("Папка образцов другого движка","Another engine's reference folder")})if(f.ShowDialog(this)==DialogResult.OK){var target=Path.Combine(f.SelectedPath,v.Id);if(File.Exists(target+".wav")||File.Exists(target+".txt"))throw new IOException("Reference already exists");foreach(var ext in new[]{".wav",".txt",".json"})File.Copy(Path.Combine(ClassicQwenReference.Folder,v.Id+ext),target+ext);}});
   Add(L("Очистить Sidon…","Clean with Sidon…"),()=>{var v=Selected();if(v==null)return;Run(async token=>{var job=NewJob();try{var variant=ClassicQwenTtsSettings.Load().Variant;var path=await ClassicSidon.CleanAsync(Path.Combine(ClassicQwenReference.Folder,v.Id+".wav"),job,variant=="cuda13" ? "cuda" : variant,token,Progress());var seconds=ClassicVoiceAssets.WaveSeconds(path);var reference=await ClassicQwenReference.ImportAsync(path,v.Name+" (Sidon)",File.ReadAllText(Path.Combine(ClassicQwenReference.Folder,v.Id+".txt")),0,Math.Min(30,seconds),v.Consent,token,false);Reload(reference.Id);}finally{Directory.Delete(job,true);}});});
   Add(L("VoiceDesign…","VoiceDesign…"),()=>Design());
   Add(L("Удалить","Delete"),()=>{var v=Selected();if(v!=null && MessageBox.Show(this,L("Удалить выбранный образец голоса?","Delete this voice reference?"),Text,MessageBoxButtons.YesNo)==DialogResult.Yes){StopReferencePreview();ClassicVoiceLibrary.Delete(v);Reload();}});
   _voices.SelectedIndexChanged+=(s,e)=>{StopReferencePreview();var v=Selected();_name.Text=v?.Name ?? "";_transcript.Text=v==null ? "" : File.ReadAllText(Path.Combine(ClassicQwenReference.Folder,v.Id+".txt"));RefreshReferencePreview();};
   FormClosing+=(s,e)=>{StopReferencePreview();if(_operation!=null){_closeRequested=true;_operation.Cancel();e.Cancel=true;}};FormClosed+=(s,e)=>_player.Dispose();InitializeReferencePreview(editor);Reload();
  }
  private void Add(string text,Action action){var b=new Button{Text=text,AutoSize=true};b.Click+=(s,e)=>{try{action();}catch(Exception error){MessageBox.Show(this,error.Message,Text);}};_toolbar.Controls.Add(b);}
  private ClassicQwenReference Selected()=>_voices.SelectedItem as ClassicQwenReference;
  private void Reload(string id=null){_voices.Items.Clear();_voices.Items.AddRange(ClassicQwenReference.List(false).Cast<object>().ToArray());_voices.SelectedItem=_voices.Items.Cast<ClassicQwenReference>().FirstOrDefault(v=>v.Id==id) ?? _voices.Items.Cast<ClassicQwenReference>().FirstOrDefault();}
  private bool Permission()=>MessageBox.Show(this,L("У меня есть разрешение на использование голосов из этого набора.","I have permission to use the voices in this pack."),Text,MessageBoxButtons.OKCancel)==DialogResult.OK;
  private IProgress<SpeechProgress> Progress()=>new Progress<SpeechProgress>(p=>_progress.Text=p.Message);
  private static string NewJob(){var job=Path.Combine(Path.GetTempPath(),"se-voice-manager-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(job);return job;}
  private async void Run(Func<CancellationToken,Task> action)
  {
   if(_operation!=null)return;_operation=new CancellationTokenSource();_toolbar.Enabled=_voices.Enabled=_name.Enabled=_transcript.Enabled=false;_cancel.Visible=true;
   try{await action(_operation.Token);_progress.Text=L("Готово","Done");}catch(OperationCanceledException){_progress.Text=L("Отменено","Cancelled");}catch(Exception error){MessageBox.Show(this,error.Message,Text);}finally{_operation.Dispose();_operation=null;_toolbar.Enabled=_voices.Enabled=_name.Enabled=_transcript.Enabled=true;_cancel.Visible=false;if(_closeRequested)Close();}
  }
  private void PickPack()
  {
   using(var f=new Form{Text=L("Наборы голосов","Voice packs"),Size=new Size(650,350),StartPosition=FormStartPosition.CenterParent})
   {var list=new ListBox{Dock=DockStyle.Fill};list.Items.AddRange(VoicePackCatalog.All.Cast<object>().ToArray());var ok=new Button{Text=L("Скачать","Download"),Dock=DockStyle.Bottom,DialogResult=DialogResult.OK};f.Controls.AddRange(new Control[]{list,ok});if(f.ShowDialog(this)!=DialogResult.OK || !(list.SelectedItem is ClassicVoicePack pack) || !Permission())return;Run(async token=>{var job=NewJob();try{var path=Path.Combine(job,"voices.zip");await ClassicVoiceAssets.EnsureFileAsync(pack.Url,path,pack.Bytes,pack.Hash,false,token,Progress());var added=await ClassicVoiceLibrary.ImportPackAsync(path,true,token);foreach(var voice in added)if(string.IsNullOrWhiteSpace(voice.Attribution)){voice.Attribution=pack.Name+"\n"+pack.License+"\n"+pack.Url;File.WriteAllText(Path.Combine(ClassicQwenReference.Folder,voice.Id+".json"),Newtonsoft.Json.JsonConvert.SerializeObject(voice));}Reload(added.FirstOrDefault()?.Id);}finally{Directory.Delete(job,true);}});}
  }
  private void Design()
  {
   using(var f=new Form{Text=L("Создать голос по описанию","Design a voice"),ClientSize=new Size(520,260),StartPosition=FormStartPosition.CenterParent})
   {
    var name=new TextBox{Dock=DockStyle.Top,Text=L("Новый голос","New voice")};var description=new TextBox{Dock=DockStyle.Fill,Multiline=true};var text=new TextBox{Dock=DockStyle.Bottom,Text=L("Привет! Сегодня мы проверяем новый голос. Он должен звучать ясно и естественно.","Hello! Today we are testing a new voice. It should sound clear and natural.")};var ok=new Button{Text=L("Создать","Create"),Dock=DockStyle.Bottom,DialogResult=DialogResult.OK};f.Controls.Add(description);f.Controls.Add(new Label{Text=L("Описание голоса","Voice description"),Dock=DockStyle.Top,Height=23});f.Controls.Add(name);f.Controls.Add(text);f.Controls.Add(ok);if(f.ShowDialog(this)!=DialogResult.OK || string.IsNullOrWhiteSpace(description.Text))return;
    var voiceName=name.Text;var instruction=description.Text;var transcript=text.Text;Run(async token=>{var job=NewJob();try{var settings=ClassicQwenTtsSettings.Load();settings.Clone=false;settings.Design=true;settings.Instructions=instruction;var output=Path.Combine(job,"design.wav");using(var engine=new ClassicQwenTts(settings)){await engine.StartAsync(token,Progress());await engine.SpeakAsync(transcript,"vivian",output,token);}var seconds=ClassicVoiceAssets.WaveSeconds(output);if(seconds>30)throw new InvalidDataException("Use a shorter reference sentence");var voice=await ClassicQwenReference.ImportAsync(output,voiceName,transcript,0,seconds,true,token);Reload(voice.Id);}finally{Directory.Delete(job,true);}});
   }
  }
 }
}
