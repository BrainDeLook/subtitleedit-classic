using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
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
    public sealed partial class TextToSpeech
    {
        private static bool IsLocalCrispTts(TextToSpeechEngineId id)=>id==TextToSpeechEngineId.Qwen3Crisp || id==TextToSpeechEngineId.SupertonicCrisp;
        private bool SupertonicSelected=>_engines.First(p=>p.Index==nikseComboBoxEngine.SelectedIndex).Id==TextToSpeechEngineId.SupertonicCrisp;
        private CancellationTokenSource _qwenCancellation;
        private Button _qwenDownloadButton;
        private Button _qwenSettingsButton;
        private static bool QwenRussian => (Configuration.Settings.General.Language ?? string.Empty).StartsWith("ru",StringComparison.OrdinalIgnoreCase);
        private void FillQwenControls()
        {
            var settings=ClassicQwenTtsSettings.Load(SupertonicSelected);
            if(settings.Clone) {
                var references=ClassicQwenReference.List();nikseComboBoxVoice.Items.AddRange(references.Cast<object>().ToArray());
                nikseComboBoxVoice.SelectedItem=references.FirstOrDefault(v=>v.Id==settings.Voice) ?? references.FirstOrDefault();
            }
            else {var voices=SupertonicSelected ? ClassicQwenTts.SupertonicVoices : ClassicQwenTts.Voices; nikseComboBoxVoice.Items.AddRange(voices.Cast<object>().ToArray());nikseComboBoxVoice.Text=voices.Contains(settings.Voice) ? settings.Voice : voices[0];}
            labelRegion.Text=QwenRussian ? "Вычисления" : "Compute";
            labelRegion.Visible=nikseComboBoxRegion.Visible=true;
            nikseComboBoxRegion.Items.Clear();nikseComboBoxRegion.Items.AddRange(new object[]{"cpu","cpu-legacy","vulkan","cuda","cuda13"});nikseComboBoxRegion.Text=settings.Variant;
            labelLanguage.Visible=nikseComboBoxLanguage.Visible=true;
            nikseComboBoxLanguage.Items.Clear();
            foreach(var pair in SupertonicSelected ? ClassicSupertonicLanguages.Catalog : new[]{"ru|Russian","en|English","zh|Chinese","de|German","it|Italian","pt|Portuguese","es|Spanish","ja|Japanese","ko|Korean","fr|French"})
            {var parts=pair.Split('|');nikseComboBoxLanguage.Items.Add(new TtsLanguage(parts[1],parts[0]));}
            nikseComboBoxLanguage.SelectedItem=nikseComboBoxLanguage.Items.Cast<TtsLanguage>().FirstOrDefault(l=>l.Code==settings.Language) ?? nikseComboBoxLanguage.Items[0];
            labelApiKey.Text=SupertonicSelected ? (QwenRussian ? "Скорость (0,25–4)" : "Speed (0.25–4)") : settings.Clone ? (QwenRussian ? "Модель" : "Model") : (QwenRussian ? "Стиль голоса" : "Voice style");
            labelApiKey.Visible=nikseTextBoxApiKey.Visible=true;nikseTextBoxApiKey.ReadOnly=settings.Clone;nikseTextBoxApiKey.Text=SupertonicSelected ? settings.Speed.ToString(System.Globalization.CultureInfo.CurrentCulture) : settings.Clone ? "1.7B Voice clone" : settings.Instructions;
            if(_qwenDownloadButton==null)
            {
                var available=nikseComboBoxVoice.Right-buttonTestVoice.Right-10;
                _qwenDownloadButton=new Button{Text=QwenRussian ? "Скачать модель" : "Download model",Name="buttonQwenDownload",Location=new Point(buttonTestVoice.Right+5,buttonTestVoice.Top),Size=new Size(available/2,buttonTestVoice.Height)};
                _qwenDownloadButton.Click+=async(s,e)=>await DownloadQwen();groupBoxSettings.Controls.Add(_qwenDownloadButton);
                _qwenSettingsButton=new Button{Text=QwenRussian ? "Настройки…" : "Settings…",Name="buttonQwenSettings",Location=new Point(_qwenDownloadButton.Right+5,buttonTestVoice.Top),Size=new Size(available-available/2,buttonTestVoice.Height)};
                _qwenSettingsButton.Click+=(s,e)=>{var current=CaptureQwen(false);using(var dialog=new ClassicQwenCloneSettings(current))if(dialog.ShowDialog(this)==DialogResult.OK)nikseComboBoxEngine_SelectedIndexChanged(null,null);};groupBoxSettings.Controls.Add(_qwenSettingsButton);
            }
        }
        private ClassicQwenTtsSettings CaptureQwen(bool requireVoice=true)
        {
            var settings=ClassicQwenTtsSettings.Load(SupertonicSelected);settings.Variant=nikseComboBoxRegion.Text;settings.Voice=(nikseComboBoxVoice.SelectedItem as ClassicQwenReference)?.Id ?? nikseComboBoxVoice.Text;settings.Language=(nikseComboBoxLanguage.SelectedItem as TtsLanguage)?.Code ?? "ru";
            if(SupertonicSelected)
            {
                if(!double.TryParse(nikseTextBoxApiKey.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.CurrentCulture,out var speed) || double.IsNaN(speed) || speed<0.25 || speed>4)
                {if(requireVoice)throw new ArgumentException(QwenRussian ? "Скорость должна быть от 0,25 до 4" : "Speed must be between 0.25 and 4");}
                else settings.Speed=speed;
            }
            else if(!settings.Clone)settings.Instructions=nikseTextBoxApiKey.Text;
            if(requireVoice && settings.Clone)ClassicQwenReference.Validate(settings.Voice);
            settings.Save();return settings;
        }
        private void SetQwenBusy(bool busy)
        {
            nikseComboBoxEngine.Enabled=nikseComboBoxVoice.Enabled=nikseComboBoxRegion.Enabled=nikseComboBoxLanguage.Enabled=nikseTextBoxApiKey.Enabled=!busy;
            if(_qwenDownloadButton!=null)_qwenDownloadButton.Enabled=!busy;
            if(_qwenSettingsButton!=null)_qwenSettingsButton.Enabled=!busy;
            buttonTestVoice.Enabled=!busy;
        }
        private async Task DownloadQwen()
        {
            if(_qwenCancellation!=null)return;
            var settings=CaptureQwen(false);_qwenCancellation=new CancellationTokenSource();SetQwenBusy(true);buttonGenerateTTS.Enabled=false;
            try
            {
                await ClassicQwenTts.EnsureInstalledAsync(settings,_qwenCancellation.Token,new Progress<SpeechProgress>(p=>labelProgress.Text=p.Message));
                labelProgress.Text=QwenRussian ? "Движок и модель готовы" : "Engine and model ready";
            }
            catch(OperationCanceledException){labelProgress.Text=QwenRussian ? "Загрузка отменена" : "Download cancelled";}
            catch(Exception ex){HandleError(ex);}
            finally{_qwenCancellation.Dispose();_qwenCancellation=null;_abort=false;SetQwenBusy(false);buttonGenerateTTS.Enabled=true;}
        }
        private async Task<bool> GenerateQwenAudioAsync(Subtitle subtitle,bool showProgress,string overrideFileName)
        {
            if(_qwenCancellation!=null)throw new InvalidOperationException("Qwen3 TTS is already running");
            var settings=CaptureQwen(_perLineOriginal==null);if(_perLineOriginal!=null && !settings.Supertonic){settings.Clone=true;settings.Design=false;}_qwenCancellation=new CancellationTokenSource();var cancel=_qwenCancellation.Token;SetQwenBusy(true);
            var wasGenerateEnabled=buttonGenerateTTS.Enabled;if(!showProgress)buttonGenerateTTS.Enabled=false;
            progressBar1.Maximum=Math.Max(1,subtitle.Paragraphs.Count);progressBar1.Value=0;progressBar1.Visible=showProgress;
            try
            {
                using(var engine=new ClassicQwenTts(settings))
                {
                    await engine.StartAsync(cancel,new Progress<SpeechProgress>(p=>labelProgress.Text=p.Message));
                    for(int i=0;i<subtitle.Paragraphs.Count;i++)
                    {
                        cancel.ThrowIfCancellationRequested();var p=subtitle.Paragraphs[i];
                        var text=Utilities.UnbreakLine(HtmlUtil.RemoveHtmlTags(p.Text));
                        if(string.IsNullOrWhiteSpace(text))continue;
                        var actor=_actorAndVoices.FirstOrDefault(a=>a.Actor==p.Actor);
                        var voice=settings.Clone ? ClassicQwenReference.List().FirstOrDefault(v=>v.Name==actor?.Voice)?.Id ?? settings.Voice : actor!=null && (SupertonicSelected ? ClassicQwenTts.SupertonicVoices : ClassicQwenTts.Voices).Contains(actor.Voice) ? actor.Voice : settings.Voice;
                        if(_perLineOriginal!=null && settings.Clone)voice=await PerLineReferenceAsync(p,cancel) ?? voice;
                        labelProgress.Text=(QwenRussian ? "Озвучка " : "Generating ")+(i+1)+" / "+subtitle.Paragraphs.Count;
                        await engine.SpeakAsync(text,voice,Path.Combine(_waveFolder,string.IsNullOrEmpty(overrideFileName) ? i+".wav" : overrideFileName),cancel);
                        progressBar1.Value=i+1;
                    }
                }
                return true;
            }
            catch(OperationCanceledException){_abort=true;return false;}
            finally{CleanupPerLineReferences();_qwenCancellation.Dispose();_qwenCancellation=null;SetQwenBusy(false);if(!showProgress)buttonGenerateTTS.Enabled=wasGenerateEnabled;progressBar1.Visible=false;labelProgress.Text="";}
        }
    }
}
