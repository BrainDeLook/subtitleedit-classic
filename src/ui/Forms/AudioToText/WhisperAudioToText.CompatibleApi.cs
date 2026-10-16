using System;
using System.Drawing;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Forms.AudioToText
{
 public sealed partial class WhisperAudioToText
 {
  private void EditCompatibleAsrSettings(){using(var dialog=new ClassicCompatibleAsrForm())if(dialog.ShowDialog(this)==DialogResult.OK)RefreshModernSpeech();}
 }
 public sealed class ClassicCompatibleAsrForm : Form
 {
  public ClassicCompatibleAsrForm()
  {
   Text=ClassicSpeechSettings.L("Распознавание: OpenAI-compatible / vLLM","Speech recognition: OpenAI-compatible / vLLM");ClientSize=new Size(680,430);MinimumSize=new Size(650,440);StartPosition=FormStartPosition.CenterParent;
   var settings=ClassicCompatibleAsrSettings.Load();var table=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=2,RowCount=8};table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   for(int i=0;i<5;i++)table.RowStyles.Add(new RowStyle(SizeType.AutoSize));table.RowStyles.Add(new RowStyle(SizeType.Percent,100));table.RowStyles.Add(new RowStyle(SizeType.Absolute,65));table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   var url=new TextBox{Name="asrApiUrl",Dock=DockStyle.Fill,Text=settings.Url};var model=new TextBox{Name="asrApiModel",Dock=DockStyle.Fill,Text=settings.Model};var key=new TextBox{Name="asrApiKey",Dock=DockStyle.Fill,UseSystemPasswordChar=true,Text=settings.ApiKey};var chunks=new NumericUpDown{Name="asrApiChunks",Minimum=5,Maximum=600,Value=Math.Max(5,Math.Min(600,settings.ChunkSeconds)),Dock=DockStyle.Fill};var timeout=new NumericUpDown{Name="asrApiTimeout",Minimum=10,Maximum=3600,Value=Math.Max(10,Math.Min(3600,settings.TimeoutSeconds)),Dock=DockStyle.Fill};var prompt=new TextBox{Name="asrApiPrompt",Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Vertical,Text=settings.Prompt};
   var labels=new[]{ClassicSpeechSettings.L("URL сервера (/v1)","Server URL (/v1)"),ClassicSpeechSettings.L("Модель","Model"),ClassicSpeechSettings.L("API-ключ (необязательно)","API key (optional)"),ClassicSpeechSettings.L("Длина части, секунд","Chunk length, seconds"),ClassicSpeechSettings.L("Таймаут, секунд","Timeout, seconds"),ClassicSpeechSettings.L("Подсказка распознаванию","Transcription prompt")};var fields=new Control[]{url,model,key,chunks,timeout,prompt};
   for(int i=0;i<fields.Length;i++){table.Controls.Add(new Label{Text=labels[i],AutoSize=true,Margin=new Padding(3,7,3,8)},0,i);table.Controls.Add(fields[i],1,i);}
   var notice=new Label{Name="asrApiNotice",Dock=DockStyle.Fill,Text=ClassicSpeechSettings.L("Аудио отправляется указанному серверу. Для локального vLLM ключ обычно не нужен. Если сервер не возвращает тайминги, они оцениваются по тексту.","Audio is sent to this server. Local vLLM usually needs no key. If the server returns no timestamps, cue times are estimated from text.")};table.Controls.Add(notice,0,6);table.SetColumnSpan(notice,2);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=ClassicSpeechSettings.L("Отмена","Cancel"),DialogResult=DialogResult.Cancel,AutoSize=true};var save=new Button{Name="saveAsrApiSettings",Text=ClassicSpeechSettings.L("Сохранить","Save"),AutoSize=true};buttons.Controls.AddRange(new Control[]{cancel,save});table.Controls.Add(buttons,0,7);table.SetColumnSpan(buttons,2);Controls.Add(table);CancelButton=cancel;AcceptButton=save;
   save.Click+=(s,e)=>{try{settings.Url=url.Text.Trim();settings.Model=model.Text.Trim();settings.ApiKey=key.Text.Trim();settings.Prompt=prompt.Text;settings.ChunkSeconds=(int)chunks.Value;settings.TimeoutSeconds=(int)timeout.Value;settings.Save();DialogResult=DialogResult.OK;}catch(Exception error){MessageBox.Show(this,error.Message,Text);}};
  }
 }
}
