using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Translate;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
namespace Nikse.SubtitleEdit.Forms.Translate
{
 public sealed partial class AutoTranslate
 {
  private readonly int[] _classicInitialSelection;
  private ListView _classicSelectedTranslationView;
  private string ClassicSelectedTranslateButtonText => ClassicAssistedText.L("Перевести выбранные", "Translate selected");
  private void InitializeClassicTranslation54()
  {
   subtitleListViewSource.MultiSelect=subtitleListViewTarget.MultiSelect=true;
   subtitleListViewSource.ContextMenuStrip=contextMenuStrip1;
   _classicSelectedTranslationView=subtitleListViewSource;
   subtitleListViewSource.Enter+=(sender,e)=>_classicSelectedTranslationView=subtitleListViewSource;
   subtitleListViewTarget.Enter+=(sender,e)=>_classicSelectedTranslationView=subtitleListViewTarget;
   var search=new ToolStripMenuItem(ClassicAssistedText.L("Найти / заменить в переводе…", "Find / replace in translation…"));search.Click+=(s,e)=>SearchClassicTranslation();contextMenuStrip2.Items.Insert(0,search);

   foreach(var pair in new[]{new{Menu=contextMenuStrip1,View=subtitleListViewSource},new{Menu=contextMenuStrip2,View=subtitleListViewTarget}})
   {
    var selected=new ToolStripMenuItem(ClassicAssistedText.L("Перевести только выбранные строки","Translate only selected rows"));selected.Click+=async(s,e)=>await TranslateClassicSelection(pair.View);pair.Menu.Items.Insert(0,selected);pair.Menu.Items.Insert(1,new ToolStripSeparator());
   }
  }
  private void SearchClassicTranslation()
  {
   if(_translationInProgress)return;
   using(var form=new ClassicTranslationSearchForm(TranslatedSubtitle,Math.Max(0,subtitleListViewTarget.SelectedIndex),index=>{subtitleListViewTarget.SelectIndexAndEnsureVisible(index);subtitleListViewSource.SelectIndexAndEnsureVisible(index);}))
    if(form.ShowDialog(this)==DialogResult.OK){for(int i=0;i<TranslatedSubtitle.Paragraphs.Count;i++)TranslatedSubtitle.Paragraphs[i].Text=form.Result.Paragraphs[i].Text;subtitleListViewTarget.Fill(TranslatedSubtitle,null);}
  }
  protected override bool ProcessCmdKey(ref Message message,Keys keys)
  {
   if((keys==(Keys.Control|Keys.F) || keys==(Keys.Control|Keys.H)) && !_translationInProgress){SearchClassicTranslation();return true;}return base.ProcessCmdKey(ref message,keys);
  }
  private async Task TranslateClassicSelection(ListView view)
  {
   if(_translationInProgress)return;var indices=view.SelectedIndices.Cast<int>().OrderBy(i=>i).ToArray();if(indices.Length==0)return;
   if(!(comboBoxSource.SelectedItem is TranslationPair source) || !(comboBoxTarget.SelectedItem is TranslationPair target))return;
   if(!EnsureClassicTranslatorReady())return;
   _autoTranslator=GetCurrentEngine();
   try{SaveSettings(_autoTranslator.GetType());_autoTranslator.Initialize();SetFormality(target);}
   catch(Exception ex){MessageBox.Show(this,ex.Message,Text);return;}
   _translationInProgress=true;_breakTranslation=false;_cancellationTokenSource=new CancellationTokenSource();buttonStrategy.Enabled=buttonOK.Enabled=buttonCancel.Enabled=nikseComboBoxEngine.Enabled=comboBoxSource.Enabled=comboBoxTarget.Enabled=false;buttonTranslate.Text=LanguageSettings.Current.General.Cancel;progressBar1.Minimum=0;progressBar1.Maximum=indices.Length;progressBar1.Value=0;progressBar1.Visible=labelPleaseWait.Visible=true;
   try
   {
    var translated=await ClassicSelectedTranslation.Translate(_subtitle,TranslatedSubtitle,indices,_autoTranslator,source.Code,target.Code,_cancellationTokenSource.Token,new Progress<int>(value=>{if(_translationInProgress)progressBar1.Value=Math.Min(progressBar1.Maximum,value);}));
    for(int i=0;i<translated.Paragraphs.Count;i++)TranslatedSubtitle.Paragraphs[i].Text=translated.Paragraphs[i].Text;
    TwoLetterIsoSource=source.TwoLetterIsoLanguageName;TwoLetterIsoTarget=target.TwoLetterIsoLanguageName;
    subtitleListViewTarget.Fill(TranslatedSubtitle,null);foreach(var index in indices)subtitleListViewTarget.Items[index].Selected=true;
   }
   catch(OperationCanceledException){}
   catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}
   finally
   {
    _translationInProgress=false;_breakTranslation=false;_cancellationTokenSource.Dispose();_cancellationTokenSource=null;
    progressBar1.Visible=labelPleaseWait.Visible=false;buttonTranslate.Enabled=buttonStrategy.Enabled=buttonOK.Enabled=buttonCancel.Enabled=nikseComboBoxEngine.Enabled=comboBoxSource.Enabled=comboBoxTarget.Enabled=true;buttonTranslate.Text=_classicInitialSelection == null ? LanguageSettings.Current.GoogleTranslate.Translate : ClassicSelectedTranslateButtonText;
   }
  }
 }
}
