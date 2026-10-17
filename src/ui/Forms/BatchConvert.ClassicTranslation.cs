using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Translate;
using Nikse.SubtitleEdit.Forms.Translate;
using Nikse.SubtitleEdit.Forms.AudioToText;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class BatchConvert
    {
        private Button _classicBatchTranslateSettings;
        private void InitializeClassicBatchTranslation()
        {
            _classicBatchTranslateSettings=new Button{Name="classicBatchTranslateSettings",Text=ClassicSpeechSettings.L("Настройки перевода…","Translation settings…"),Height=Math.Max(25,Font.Height+8),Visible=false,Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom};
            _classicBatchTranslateSettings.SetBounds(11,groupBoxAutoTranslate.ClientSize.Height-_classicBatchTranslateSettings.Height-4,groupBoxAutoTranslate.ClientSize.Width-22,_classicBatchTranslateSettings.Height);
            groupBoxAutoTranslate.Controls.Add(_classicBatchTranslateSettings);
            _classicBatchTranslateSettings.Click+=(s,e)=>{if(GetCurrentEngine() is ClassicNewTranslator translator)using(var form=new ClassicTranslationForm(translator.Id))form.ShowDialog(this);};
            FormClosed+=(s,e)=>{foreach(var engine in _autoTranslatorEngines.OfType<IDisposable>())engine.Dispose();};
        }
        private bool PrepareClassicBatchTranslation()
        {
            if(!(GetCurrentEngine() is ClassicNewTranslator translator))return true;
            try
            {
                var settings=ClassicTranslationSettings.Load(translator.Id);
                if((translator.Id=="llama" || translator.Id=="llama-advanced") && settings.UseLocalLlama && !ClassicLlamaServer.Installed(ClassicLlamaSettings.Load()))
                {using(var form=new ClassicLlamaForm())form.ShowDialog(this);if(!ClassicLlamaServer.Installed(ClassicLlamaSettings.Load()))return false;}
                if(translator.Id=="madlad" && !ClassicNewTranslator.MadladReady(settings.Variant))
                {using(var form=new ClassicTranslationForm(translator.Id))form.ShowDialog(this);if(!ClassicNewTranslator.MadladReady(ClassicTranslationSettings.Load(translator.Id).Variant))return false;}
                return true;
            }
            catch(Exception error){MessageBox.Show(this,error.Message,Text);return false;}
        }
        private Subtitle RunClassicBatchTranslation(Subtitle source,ClassicNewTranslator translator,string sourceLanguage,string targetLanguage)
        {
            var cancel=_cancellationTokenSource?.Token??CancellationToken.None;
            // Initialize per file: translation context from another queued file must not leak in.
            translator.Initialize();
            var task=Task.Run(async()=>
            {
                var result=new Subtitle(source);
                for(var at=0;at<source.Paragraphs.Count;)
                {
                    cancel.ThrowIfCancellationRequested();
                    if(translator.IsAdvanced)
                    {
                        var count=Math.Min(translator.AdvancedBatchSize,source.Paragraphs.Count-at);
                        var output=await translator.TranslateBatchAsync(source.Paragraphs.Skip(at).Take(count).Select(p=>p.Text).ToList(),sourceLanguage,targetLanguage,cancel).ConfigureAwait(false);
                        cancel.ThrowIfCancellationRequested();
                        for(var i=0;i<count;i++)result.Paragraphs[at+i].Text=output[i];
                        at+=count;
                    }
                    else
                    {
                        var formatting=new Formatting();var input=formatting.SetTagsAndReturnTrimmed(source.Paragraphs[at].Text,sourceLanguage);
                        var output=await translator.Translate(input,sourceLanguage,targetLanguage,cancel).ConfigureAwait(false);
                        cancel.ThrowIfCancellationRequested();
                        var restored=formatting.ReAddFormatting(output.Replace("<br />",Environment.NewLine).Replace("<br/>",Environment.NewLine));
                        result.Paragraphs[at++].Text=translator.KeepLineBreaks?restored:Utilities.AutoBreakLine(restored);
                    }
                }
                return result;
            },cancel);
            // The classic conversion loop is synchronous; pump while its owned task runs so
            // Cancel can reach the request. Inputs remain disabled by the existing job state.
            while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(15);}
            return task.GetAwaiter().GetResult(); // Never return a partial file on error/cancel.
        }
    }
}
