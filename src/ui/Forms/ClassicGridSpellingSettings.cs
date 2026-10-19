using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicGridSpellingSettings : Form
    {
        private readonly CheckBox _enabled;
        private readonly ComboBox _dictionary;
        public bool EnabledResult=>_enabled.Checked;
        public string DictionaryResult=>_dictionary.SelectedIndex<=0?null:(string)_dictionary.SelectedItem;
        public event Action DownloadRequested;
        public ClassicGridSpellingSettings(bool enabled,string dictionary)
        {
            Text=ClassicAssistedText.L("Орфография в таблице","Grid spell checking");MinimumSize=new Size(520,300);Size=new Size(610,330);StartPosition=FormStartPosition.CenterParent;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=5};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,35));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,26));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));Controls.Add(layout);
            _enabled=new CheckBox{Text=ClassicAssistedText.L("Проверять орфографию в таблице субтитров","Check spelling in the subtitle grid"),Checked=enabled,Dock=DockStyle.Fill};layout.Controls.Add(_enabled);
            layout.Controls.Add(new Label{Text=ClassicAssistedText.L("Словарь","Dictionary"),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft});
            _dictionary=new ComboBox{Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList};layout.Controls.Add(_dictionary);
            layout.Controls.Add(new Label{Dock=DockStyle.Fill,Text=ClassicAssistedText.L("Исправления: правая кнопка на подчёркнутом слове. «Пропустить все» действует до смены словаря; слова и имена сохраняются в общих словарях Classic. Проверка в поле текста настраивается отдельно.","Right-click an underlined word for corrections. Ignore all lasts until the dictionary changes; added words and names use Classic's shared lists. Edit-box spell checking has its own setting.")});
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};layout.Controls.Add(footer);
            var cancel=new Button{Text=LanguageSettings.Current.General.Cancel,DialogResult=DialogResult.Cancel,AutoSize=true};var ok=new Button{Text=LanguageSettings.Current.General.Ok,DialogResult=DialogResult.OK,AutoSize=true};var download=new Button{Text=ClassicAssistedText.L("Скачать словари…","Get dictionaries…"),AutoSize=true};footer.Controls.Add(cancel);footer.Controls.Add(ok);footer.Controls.Add(download);AcceptButton=ok;CancelButton=cancel;
            Action refresh=()=>{var selected=DictionaryResult??dictionary;_dictionary.Items.Clear();_dictionary.Items.Add(ClassicAssistedText.L("Автоматически по субтитрам","Detect from subtitles"));if(Directory.Exists(Configuration.DictionariesDirectory))foreach(var file in Directory.GetFiles(Configuration.DictionariesDirectory,"*.dic").OrderBy(p=>p))if(File.Exists(Path.ChangeExtension(file,".aff")))_dictionary.Items.Add(Path.GetFileNameWithoutExtension(file));_dictionary.SelectedIndex=selected!=null && _dictionary.Items.Contains(selected)?_dictionary.Items.IndexOf(selected):0;};
            download.Click+=(s,e)=>{DownloadRequested?.Invoke();refresh();};refresh();
            UiUtil.FixFonts(this);
        }
    }
}
