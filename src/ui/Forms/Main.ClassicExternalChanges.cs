using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using C=Nikse.SubtitleEdit.Controls.SubtitleListView.SubtitleColumn;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private ClassicSubtitleFileStamp _classicExternalStamp;
        private bool _checkingClassicExternalChange,_classicExternalInitialized;
        // Parser instances contain mutable error counters; those are not document edits.
        private static string ClassicExternalContent(Subtitle subtitle)=>JsonConvert.SerializeObject(new{subtitle.Header,subtitle.Footer,subtitle.Paragraphs});
        private void InitializeClassicExternalChanges()
        {
            if(_classicExternalInitialized)return;_classicExternalInitialized=true;
            Deactivate+=(s,e)=>EnsureClassicExternalStamp();
            Activated+=(s,e)=>
            {
                if(IsHandleCreated)BeginInvoke(new Action(async()=>
                {
                    if(IsDisposed || Disposing || Form.ActiveForm!=this || OwnedForms.Any(f=>f.Visible && f.Modal))return;
                    await CheckClassicExternalChange(null);
                }));
            };
        }
        private string ClassicWatchedSubtitleFile()=>!_converted && _currentSubtitleFormat?.IsTextBased==true && !_currentSubtitleFormat.IsVobSubIndexFile && !string.IsNullOrWhiteSpace(_fileName)?_fileName:null;
        private void RememberClassicExternalStamp()
        {var file=ClassicWatchedSubtitleFile();_classicExternalStamp=file==null?null:ClassicSubtitleFileStamp.Read(file);}
        private void EnsureClassicExternalStamp()
        {
            var file=ClassicWatchedSubtitleFile();
            if(file==null)_classicExternalStamp=null;
            else if(_classicExternalStamp==null || !_classicExternalStamp.SameFile(file))_classicExternalStamp=ClassicSubtitleFileStamp.Read(file);
        }
        private async Task<bool> CheckClassicExternalChange(Func<string,bool> confirm)
        {
            if(_checkingClassicExternalChange || _loading || _openSaveCounter!=0 || _networkSession!=null || IsDisposed || Disposing || !Enabled || OwnedForms.Any(f=>f.Visible && f.Modal))return false;
            var file=ClassicWatchedSubtitleFile();var previous=_classicExternalStamp;
            if(file==null || previous==null || !previous.SameFile(file))return false;
            var current=ClassicSubtitleFileStamp.Read(file);
            if(current==null || current.Length==0 || current.Length>Subtitle.MaxFileSize || previous.SameContent(current))return false;
            _checkingClassicExternalChange=true;
            try
            {
                var document=_subtitle;var snapshot=ClassicExternalContent(document);var source=textBoxSource.Text;var sourceChanged=_sourceViewChange;var currentFormat=_currentSubtitleFormat;
                if(sourceChanged || _changeSubtitleHash!=GetFastSubtitleHash())
                {
                    var question=ClassicAssistedText.L("Файл изменён другой программой:\n","The file was changed outside Subtitle Edit:\n")+file+"\n\n"+ClassicAssistedText.L("Перечитать его и заменить несохранённые изменения?","Reload it and discard your unsaved changes?");
                    var accept=confirm!=null?confirm(question):MessageBox.Show(this,question,Title,MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)==DialogResult.Yes;
                    if(!accept){_classicExternalStamp=current;return false;}
                }
                var encoding=GetCurrentEncoding();
                // Parse a separate document: interrupted, unreadable or invalid writes never clear the editor.
                var loaded=await Task.Run(()=>Subtitle.Parse(file,encoding));
                if(IsDisposed || Disposing || !ReferenceEquals(document,_subtitle) || ClassicWatchedSubtitleFile()!=file || _currentSubtitleFormat!=currentFormat || ClassicExternalContent(document)!=snapshot || _sourceViewChange!=sourceChanged || textBoxSource.Text!=source)return false;
                var after=ClassicSubtitleFileStamp.Read(file);
                if(!current.SameContent(after))return false;
                if(loaded==null || loaded.Paragraphs.Count==0 || loaded.OriginalFormat?.IsTextBased!=true || loaded.OriginalFormat.IsVobSubIndexFile)
                {ShowStatus(ClassicAssistedText.L("Изменённый файл пока не удалось прочитать; текущие субтитры сохранены в редакторе.","The changed file could not be read; current subtitles remain in the editor."));return false;}
                ApplyClassicExternalSubtitle(file,loaded,current);
                return true;
            }
            catch(Exception ex)when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is FormatException || ex is InvalidOperationException || ex is System.Xml.XmlException)
            {if(!IsDisposed && !Disposing)ShowStatus(ClassicAssistedText.L("Не удалось перечитать субтитры: ","Unable to reload subtitle: ")+ex.Message);return false;}
            finally{_checkingClassicExternalChange=false;}
        }
        private void ApplyClassicExternalSubtitle(string file,Subtitle loaded,ClassicSubtitleFileStamp stamp)
        {
            var selected=SubtitleListview1.GetSelectedIndices();var top=FirstVisibleIndex;var format=loaded.OriginalFormat;
            var player=mediaPlayer.VideoPlayer;var videoPosition=player?.CurrentPosition??0;var playing=player?.IsPlaying??false;
            new ClassicSubtitleMarksPersistence(loaded,file).Load();
            loaded.AddTimeToAllParagraphs(TimeSpan.FromMilliseconds(-Configuration.Settings.General.CurrentVideoOffsetInMs));
            _subtitle=loaded;_subtitle.FileName=file;_subtitleListViewIndex=-1;_sourceViewChange=false;
            // Display an already-parsed document without converting its markup or the original reference.
            comboBoxSubtitleFormats.SelectedIndexChanged-=ComboBoxSubtitleFormatsSelectedIndexChanged;
            try{_currentSubtitleFormat=format;_oldSubtitleFormat=format;_lastChangedToFormat=format.FriendlyName;UiUtil.InitializeSubtitleFormatComboBox(comboBoxSubtitleFormats.ComboBox,format.FriendlyName);}
            finally{comboBoxSubtitleFormats.SelectedIndexChanged+=ComboBoxSubtitleFormatsSelectedIndexChanged;}
            _converted=false;_formatManuallyChanged=false;_fileDateTime=stamp.LastWriteUtc.ToLocalTime();
            if(_classicColumns!=null)ApplyClassicColumns(_classicColumns,false);
            else
            {
                ShowHideTextBasedFeatures(format);
                if(format.HasStyleSupport)SubtitleListview1.ShowExtraColumn(_languageGeneral.Style);else SubtitleListview1.HideColumn(C.Extra);
                SubtitleListview1.Fill(_subtitle,_subtitleOriginal);
            }
            _selectedIndices=selected.Where(i=>i<_subtitle.Paragraphs.Count).ToList();RestoreSubtitleListviewIndices();
            if(SubtitleListview1.SelectedIndices.Count==0)SubtitleListview1.SelectIndexAndEnsureVisible(0);
            if(top>=0 && top<SubtitleListview1.Items.Count)SubtitleListview1.TopItem=SubtitleListview1.Items[top];
            ResetHistory();UpdateSourceView();RefreshSelectedParagraph();SetListViewStateImages();
            _changeSubtitleHash=GetFastSubtitleHash();_classicExternalStamp=stamp;
            UpdateToolbarButtonsToCurrentFormat(format);RefreshClassicCuePickerAvailability();RefreshClassicForcedAvailability();
            audioVisualizer.Invalidate();mediaPlayer.LastParagraph=null;UiUtil.ShowSubtitle(_subtitle,mediaPlayer,format);SetTitle();
            if(player!=null && ReferenceEquals(player,mediaPlayer.VideoPlayer))
            {
                if(Math.Abs(player.CurrentPosition-videoPosition)>.05)player.CurrentPosition=videoPosition;
                if(playing && !player.IsPlaying)player.Play();else if(!playing && player.IsPlaying)player.Pause();
            }
            ShowStatus(ClassicAssistedText.L("Субтитры перечитаны после изменения другой программой: ","Subtitle reloaded (changed outside Subtitle Edit): ")+Path.GetFileName(file));
        }
    }
}
