using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using System;
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
        private ClassicCompatibleTtsSettings _compatibleSettings;
        private CancellationTokenSource _compatibleCancellation;
        private Button _compatibleSettingsButton;
        private bool _compatibleLoading;
        private bool CompatibleSelected=>_engines.First(p=>p.Index==nikseComboBoxEngine.SelectedIndex).Id==TextToSpeechEngineId.OpenAiCompatible;
        private void FillCompatibleControls(bool reload=true)
        {
            _compatibleLoading=true;
            try
            {
                if(reload || _compatibleSettings==null)_compatibleSettings=ClassicCompatibleTtsSettings.Load();
                var settings=_compatibleSettings.Selected();
                labelApiKey.Visible=nikseTextBoxApiKey.Visible=true;labelApiKey.Text="API key";nikseTextBoxApiKey.UseSystemPasswordChar=true;nikseTextBoxApiKey.Text=settings.ApiKey;
                labelRegion.Visible=nikseComboBoxRegion.Visible=true;labelRegion.Text=QwenRussian ? "Провайдер" : "Provider";
                nikseComboBoxRegion.Items.Clear();nikseComboBoxRegion.Items.AddRange(new object[]{"OpenAI","OpenRouter","Custom"});nikseComboBoxRegion.SelectedItem=_compatibleSettings.Provider;
                labelLanguage.Visible=nikseComboBoxLanguage.Visible=true;labelLanguage.Text=QwenRussian ? "Модель" : "Model";nikseComboBoxLanguage.Items.Clear();
                var models=ClassicCompatibleTts.Models(_compatibleSettings.Provider).ToList();if(!models.Contains(settings.Model))models.Add(settings.Model);nikseComboBoxLanguage.Items.AddRange(models.Cast<object>().ToArray());nikseComboBoxLanguage.Text=settings.Model;
                nikseComboBoxVoice.Items.Clear();nikseComboBoxVoice.DropDownStyle=ComboBoxStyle.DropDown;
                var voices=ClassicCompatibleTts.Voices(_compatibleSettings.Provider,settings.Model).ToList();if(!voices.Contains(settings.Voice))voices.Add(settings.Voice);nikseComboBoxVoice.Items.AddRange(voices.Cast<object>().ToArray());nikseComboBoxVoice.Text=settings.Voice;
                if(_compatibleSettingsButton==null)
                {
                    _compatibleSettingsButton=new Button{Name="buttonCompatibleTtsSettings",Text=QwenRussian ? "Параметры API…" : "API settings…",Location=new Point(buttonTestVoice.Right+8,buttonTestVoice.Top),Size=new Size(145,buttonTestVoice.Height)};
                    _compatibleSettingsButton.Click+=(s,e)=>EditCompatibleSettings();groupBoxSettings.Controls.Add(_compatibleSettingsButton);
                    nikseComboBoxLanguage.SelectedIndexChanged+=(s,e)=>ChangeCompatibleModel();
                }
                _compatibleSettingsButton.Visible=true;
            }
            finally{_compatibleLoading=false;}
        }
        private void CaptureCompatible()
        {
            if(_compatibleLoading || _compatibleSettings==null || !CompatibleSelected)return;
            var settings=_compatibleSettings.Selected();settings.ApiKey=nikseTextBoxApiKey.Text;settings.Voice=nikseComboBoxVoice.Text;_compatibleSettings.Save();
        }
        private void ChangeCompatibleProvider()
        {
            if(_compatibleLoading || _compatibleSettings==null || !CompatibleSelected || nikseComboBoxRegion.SelectedIndex<0)return;
            CaptureCompatible();_compatibleSettings.Provider=nikseComboBoxRegion.Text;FillCompatibleControls(false);_compatibleSettings.Save();
        }
        private void ChangeCompatibleModel()
        {
            if(_compatibleLoading || _compatibleSettings==null || !CompatibleSelected || nikseComboBoxLanguage.SelectedIndex<0)return;
            CaptureCompatible();var settings=_compatibleSettings.Selected();settings.Model=nikseComboBoxLanguage.Text;var voices=ClassicCompatibleTts.Voices(_compatibleSettings.Provider,settings.Model);if(voices.Length>0 && !voices.Contains(settings.Voice))settings.Voice=voices[0];_compatibleSettings.Save();FillCompatibleControls(false);
        }
        private void EditCompatibleSettings()
        {
            CaptureCompatible();var settings=_compatibleSettings.Selected();
            using(var dialog=new Form{Text=QwenRussian ? "Совместимая озвучка API" : "Compatible speech API",ClientSize=new Size(650,325),MinimumSize=new Size(580,350),StartPosition=FormStartPosition.CenterParent})
            {
                var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=7,Padding=new Padding(12)};root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
                var url=new TextBox{Dock=DockStyle.Fill,Text=settings.Url};var model=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDown,Text=settings.Model};model.Items.AddRange(ClassicCompatibleTts.Models(_compatibleSettings.Provider));model.Text=settings.Model;
                var voice=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDown};voice.Items.AddRange(ClassicCompatibleTts.Voices(_compatibleSettings.Provider,settings.Model));voice.Text=settings.Voice;
                var format=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};format.Items.AddRange(new object[]{"auto","mp3","pcm"});format.SelectedItem=settings.Format;if(format.SelectedIndex<0)format.SelectedIndex=0;
                var speed=new NumericUpDown{Dock=DockStyle.Fill,Minimum=0.25m,Maximum=4,DecimalPlaces=2,Increment=0.05m,Value=(decimal)Math.Max(0.25,Math.Min(4,settings.Speed))};var instructions=new TextBox{Dock=DockStyle.Fill,Multiline=true,Height=60,Text=settings.Instructions};
                var labels=new[]{"URL (POST)",QwenRussian ? "Модель" : "Model",QwenRussian ? "Голос" : "Voice",QwenRussian ? "Формат ответа" : "Response format",QwenRussian ? "Скорость" : "Speed",QwenRussian ? "Стиль голоса" : "Voice instructions"};var controls=new Control[]{url,model,voice,format,speed,instructions};
                for(int i=0;i<controls.Length;i++){root.Controls.Add(new Label{Text=labels[i],AutoSize=true,Padding=new Padding(0,4,8,0)},0,i);root.Controls.Add(controls[i],1,i);root.RowStyles.Add(new RowStyle(SizeType.AutoSize));}
                model.TextChanged+=(s,e)=>{var previous=voice.Text;voice.Items.Clear();voice.Items.AddRange(ClassicCompatibleTts.Voices(_compatibleSettings.Provider,model.Text));voice.Text=voice.Items.Contains(previous) ? previous : voice.Items.Count>0 ? Convert.ToString(voice.Items[0]) : previous;};
                var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=QwenRussian ? "Отмена" : "Cancel",DialogResult=DialogResult.Cancel};var ok=new Button{Text="OK",DialogResult=DialogResult.OK};buttons.Controls.AddRange(new Control[]{cancel,ok});root.Controls.Add(buttons,0,6);root.SetColumnSpan(buttons,2);root.RowStyles.Add(new RowStyle(SizeType.Percent,100));dialog.Controls.Add(root);dialog.AcceptButton=ok;dialog.CancelButton=cancel;
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                settings.Url=url.Text.Trim();settings.Model=model.Text.Trim();settings.Voice=voice.Text.Trim();settings.Format=format.Text;settings.Speed=(double)speed.Value;settings.Instructions=instructions.Text;_compatibleSettings.Save();FillCompatibleControls(false);
            }
        }
        private async Task<bool> GenerateCompatibleAudioAsync(Subtitle subtitle,bool showProgress,string overrideFileName)
        {
            if(_compatibleCancellation!=null)throw new InvalidOperationException("Speech API is already running");
            CaptureCompatible();var settings=_compatibleSettings.Selected();_compatibleCancellation=new CancellationTokenSource();
            nikseComboBoxEngine.Enabled=nikseComboBoxVoice.Enabled=nikseComboBoxRegion.Enabled=nikseComboBoxLanguage.Enabled=nikseTextBoxApiKey.Enabled=_compatibleSettingsButton.Enabled=buttonTestVoice.Enabled=false;
            progressBar1.Maximum=Math.Max(1,subtitle.Paragraphs.Count);progressBar1.Value=0;progressBar1.Visible=showProgress;
            try
            {
                using(var engine=new ClassicCompatibleTts(settings))
                {
                    for(int i=0;i<subtitle.Paragraphs.Count;i++)
                    {
                        _compatibleCancellation.Token.ThrowIfCancellationRequested();var p=subtitle.Paragraphs[i];var text=Utilities.UnbreakLine(HtmlUtil.RemoveHtmlTags(p.Text));if(string.IsNullOrWhiteSpace(text))continue;
                        var actor=_actorAndVoices.FirstOrDefault(a=>a.Actor==p.Actor);var allowed=ClassicCompatibleTts.Voices(_compatibleSettings.Provider,settings.Model);var voice=string.IsNullOrWhiteSpace(actor?.Voice) || allowed.Length>0 && !allowed.Contains(actor.Voice) && actor.Voice!=settings.Voice ? settings.Voice : actor.Voice;
                        labelProgress.Text=(QwenRussian ? "Озвучка " : "Generating ")+(i+1)+" / "+subtitle.Paragraphs.Count;
                        await engine.SpeakAsync(text,voice,Path.Combine(_waveFolder,string.IsNullOrEmpty(overrideFileName) ? i+".wav" : overrideFileName),_compatibleCancellation.Token);progressBar1.Value=i+1;
                    }
                }
                return true;
            }
            catch(OperationCanceledException){_abort=true;return false;}
            finally{_compatibleCancellation.Dispose();_compatibleCancellation=null;nikseComboBoxEngine.Enabled=nikseComboBoxVoice.Enabled=nikseComboBoxRegion.Enabled=nikseComboBoxLanguage.Enabled=nikseTextBoxApiKey.Enabled=_compatibleSettingsButton.Enabled=buttonTestVoice.Enabled=true;progressBar1.Visible=false;labelProgress.Text="";}
        }
    }
}
