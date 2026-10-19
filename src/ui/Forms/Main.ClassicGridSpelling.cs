using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Enums;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private ClassicGridSpelling _classicGridSpelling;
        private ToolStripMenuItem _gridSpellToggle,_gridSpellSettings,_gridSpellContext;
        private bool _gridSpellEnabled;
        private DateTime _nextGridSpellingUpdate;
        private string _gridSpellDictionary,_gridSpellFailedPath;
        private void InitializeClassicGridSpelling()
        {
            if(_gridSpellToggle==null)
            {
                var settings=ClassicGridAppearance.Load(ClassicGridAppearancePath);_gridSpellEnabled=settings.GridSpellCheck;_gridSpellDictionary=settings.GridSpellDictionary;
                _classicGridSpelling=new ClassicGridSpelling();
                _gridSpellToggle=new ToolStripMenuItem{Name="classicGridSpellCheck",Checked=_gridSpellEnabled,CheckOnClick=true};
                _gridSpellSettings=new ToolStripMenuItem{Name="classicGridSpellSettings"};_gridSpellContext=new ToolStripMenuItem{Name="classicGridSpellContext",Visible=false};
                toolStripMenuItemSpellCheckMain.DropDownItems.Add(new ToolStripSeparator());toolStripMenuItemSpellCheckMain.DropDownItems.Add(_gridSpellToggle);toolStripMenuItemSpellCheckMain.DropDownItems.Add(_gridSpellSettings);
                contextMenuStripListView.Items.Insert(0,_gridSpellContext);
                _gridSpellToggle.Click+=(s,e)=>SaveGridSpelling(_gridSpellToggle.Checked,_gridSpellDictionary);
                _gridSpellSettings.Click+=(s,e)=>ShowGridSpellingSettings();
                textBoxListViewText.SpellWordChanged+=(word,action)=>
                {
                    if(action==SpellCheckAction.SkipAll)_classicGridSpelling.Ignore(word);
                    else if(action==SpellCheckAction.AddToNames || action==SpellCheckAction.AddToDictionary)RefreshClassicSpellingWords();
                    SubtitleListview1.RefreshGridSpelling();
                };
                contextMenuStripListView.Opening+=(s,e)=>PopulateGridSpellMenu();
                Activated+=(s,e)=>RefreshClassicSpellingAfterActivation();
                Disposed+=(s,e)=>_classicGridSpelling.Dispose();
            }
            _gridSpellToggle.Text=ClassicAssistedText.L("Орфография в таблице","Grid spell checking");
            _gridSpellSettings.Text=ClassicAssistedText.L("Настройки орфографии в таблице…","Grid spell checking settings…");
        }
        private void SaveGridSpelling(bool enabled,string dictionary)
        {
            try
            {
                var settings=ClassicGridAppearance.Load(ClassicGridAppearancePath);settings.GridSpellCheck=enabled;settings.GridSpellDictionary=dictionary;settings.Save(ClassicGridAppearancePath);
                _gridSpellEnabled=enabled;_gridSpellDictionary=dictionary;_gridSpellToggle.Checked=enabled;_gridSpellFailedPath=null;_nextGridSpellingUpdate=DateTime.MinValue;
                UpdateClassicGridSpelling();SubtitleListview1.RefreshGridSpelling();
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException){_gridSpellToggle.Checked=_gridSpellEnabled;MessageBox.Show(this,ex.Message,_gridSpellToggle.Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        private void ShowGridSpellingSettings()
        {
            using(var form=new ClassicGridSpellingSettings(_gridSpellEnabled,_gridSpellDictionary))
            {form.DownloadRequested+=()=>ShowGetDictionaries();if(form.ShowDialog(this)==DialogResult.OK)SaveGridSpelling(form.EnabledResult,form.DictionaryResult);}
        }
        private void UpdateClassicGridSpelling()
        {
            if(_classicGridSpelling==null)return;
            if(!_gridSpellEnabled){SubtitleListview1.GridSpelling=null;return;}
            if(DateTime.UtcNow<_nextGridSpellingUpdate)return;
            _nextGridSpellingUpdate=DateTime.UtcNow.AddSeconds(2);
            var name=_gridSpellDictionary;
            if(string.IsNullOrEmpty(name))name=_subtitle?.Paragraphs.Count>0?LanguageAutoDetect.AutoDetectLanguageName(string.Empty,_subtitle):null;
            if(string.IsNullOrEmpty(name)){SubtitleListview1.GridSpelling=null;return;}
            if(name.IndexOfAny(Path.GetInvalidFileNameChars())>=0){SubtitleListview1.GridSpelling=null;return;}
            // Saved dictionary identities are file names, never arbitrary paths.
            var path=Path.Combine(Configuration.DictionariesDirectory,Path.GetFileName(name));
            if(path!=_gridSpellFailedPath)
            {
                try
                {
                    if(!File.Exists(path+".dic") || !File.Exists(path+".aff"))throw new FileNotFoundException(ClassicAssistedText.L("Словарь не установлен: ","Dictionary not installed: ")+name);
                    var changed=_classicGridSpelling.Dictionary!=path || SubtitleListview1.GridSpelling!=_classicGridSpelling;
                    _classicGridSpelling.Load(path);_gridSpellFailedPath=null;SubtitleListview1.GridSpelling=_classicGridSpelling;
                    if(changed)SubtitleListview1.RefreshGridSpelling();
                }
                catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is DllNotFoundException || ex is BadImageFormatException || ex is EntryPointNotFoundException || ex is InvalidOperationException || ex is System.Runtime.InteropServices.ExternalException || ex is System.Xml.XmlException)
                {_gridSpellFailedPath=path;SubtitleListview1.GridSpelling=null;SubtitleListview1.RefreshGridSpelling();ShowStatus(ex.Message);}
            }
        }
        private void RefreshClassicSpellingAfterActivation()
        {
            _gridSpellFailedPath=null;_nextGridSpellingUpdate=DateTime.MinValue;
            UpdateClassicGridSpelling();RefreshClassicSpellingWords();
        }
        private void RefreshClassicSpellingWords()
        {
            try{_classicGridSpelling.RefreshWords();SubtitleListview1.RefreshGridSpelling();}
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
            {SubtitleListview1.GridSpelling=null;_gridSpellFailedPath=_classicGridSpelling.Dictionary;SubtitleListview1.RefreshGridSpelling();ShowStatus(ex.Message);}
        }
        private void PopulateGridSpellMenu()
        {
            _gridSpellContext.DropDownItems.Clear();_gridSpellContext.Visible=false;
            if(!_gridSpellEnabled || !_classicGridSpelling.Ready || SubtitleListview1.GridSpelling==null)return;
            var hit=SubtitleListview1.SpellingAt(SubtitleListview1.PointToClient(Cursor.Position),out var row,out var original);
            if(SubtitleListview1.SpellingKeyboardMenu){row=FirstSelectedIndex;original=false;hit=SubtitleListview1.FirstSpellingHit(row);}
            BuildGridSpellMenu(hit,row,original);
        }
        private void BuildGridSpellMenu(ClassicGridSpellHit hit,int row,bool original)
        {
            _gridSpellContext.DropDownItems.Clear();_gridSpellContext.Visible=false;
            if(hit==null)return;
            _gridSpellContext.Visible=true;_gridSpellContext.Text=ClassicAssistedText.L("Орфография: ","Spelling: ")+hit.Word;
            if(original){_gridSpellContext.DropDownItems.Add(new ToolStripMenuItem(ClassicAssistedText.L("Оригинал — только чтение","Original is read-only")){Enabled=false});return;}
            var cue=_subtitle.GetParagraphOrDefault(row);
            if(cue==null || cue.Text!=hit.Source)return;
            foreach(var suggestion in _classicGridSpelling.Suggest(hit.Word))
            {var replacement=suggestion;_gridSpellContext.DropDownItems.Add(new ToolStripMenuItem(suggestion,null,(s,e)=>ApplyGridSpelling(cue,hit,replacement)));}
            if(_gridSpellContext.DropDownItems.Count==0)_gridSpellContext.DropDownItems.Add(new ToolStripMenuItem(ClassicAssistedText.L("Нет вариантов","No suggestions")){Enabled=false});
            _gridSpellContext.DropDownItems.Add(new ToolStripSeparator());
            _gridSpellContext.DropDownItems.Add(ClassicAssistedText.L("Пропустить все","Ignore all"),null,(s,e)=>{_classicGridSpelling.Ignore(hit.Word);textBoxListViewText.ApplyExternalSpellingWord(hit.Word,SpellCheckAction.SkipAll);SubtitleListview1.RefreshGridSpelling();});
            _gridSpellContext.DropDownItems.Add(ClassicAssistedText.L("Добавить в словарь","Add to dictionary"),null,(s,e)=>AddGridSpellingWord(hit.Word,false));
            _gridSpellContext.DropDownItems.Add(ClassicAssistedText.L("Добавить в имена","Add to names"),null,(s,e)=>AddGridSpellingWord(hit.Word,true));
        }
        private void ApplyGridSpelling(Paragraph cue,ClassicGridSpellHit hit,string replacement)
        {
            ReloadFromSourceView();var index=_subtitle.Paragraphs.IndexOf(cue);
            if(index<0 || cue.Text!=hit.Source || hit.SourceRanges==null || hit.SourceRanges.Count==0)return;
            var text=hit.Replace(replacement);if(text==cue.Text)return;
            MakeHistoryForUndo(ClassicAssistedText.L("Исправить орфографию","Correct spelling"));cue.Text=text;
            SubtitleListview1.SetTimeAndText(index,cue,_subtitle.GetParagraphOrDefault(index+1));
            if(FirstSelectedIndex==index)RefreshSelectedParagraph();SubtitleListview1.RefreshGridSpelling();
        }
        private void AddGridSpellingWord(string word,bool name)
        {
            try{_classicGridSpelling.AddWord(word,name);textBoxListViewText.ApplyExternalSpellingWord(word,name?SpellCheckAction.AddToNames:SpellCheckAction.AddToDictionary);SubtitleListview1.RefreshGridSpelling();}
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException){MessageBox.Show(this,ex.Message,_gridSpellToggle.Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
    }
}
