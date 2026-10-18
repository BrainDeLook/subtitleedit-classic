using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicTextToolsForm : PositionAndSizeForm
    {
        private readonly bool _unicode;
        private readonly Subtitle _source;
        private readonly string _language;
        private readonly ClassicTextToolSettings _settings;
        private readonly DataGridView _grid;
        private readonly DataGridView _preview;
        private readonly NumericUpDown _gap, _characters;
        private readonly Label _status;
        private bool _refreshing;
        private List<ClassicUnicodeCharacter> _unicodeItems;
        private List<ClassicMergeCandidate> _merges;
        public Subtitle Result { get; private set; }
        public ClassicTextToolsForm(Subtitle source, bool unicode)
        {
            UiUtil.PreInitialize(this); _source=new Subtitle(source,false); _unicode=unicode; _settings=ClassicTextToolSettings.Load(); _language=LanguageAutoDetect.AutoDetectGoogleLanguage(source);
            Text=unicode?ClassicTextTools.L("Удалить / заменить Unicode-символы", "Remove / replace Unicode characters"):ClassicTextTools.L("Объединить незаконченные фразы", "Merge continuation lines");
            ClientSize=new Size(960,650); MinimumSize=new Size(780,500); StartPosition=FormStartPosition.CenterParent; ShowInTaskbar=false; MinimizeBox=false;
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(10),ColumnCount=1,RowCount=5 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,52)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,48)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(layout);
            var options=new FlowLayoutPanel { AutoSize=true,Dock=DockStyle.Fill,WrapContents=true,Padding=new Padding(0,0,0,6) };
            _gap=new NumericUpDown { Minimum=0,Maximum=10000,Value=_settings.MaxGap,Width=90 }; _characters=new NumericUpDown { Minimum=20,Maximum=1000,Value=_settings.MaxCharacters,Width=90 };
            if (!unicode) { options.Controls.Add(new Label { AutoSize=true,Text=ClassicTextTools.L("Максимальный разрыв, мс", "Maximum gap, ms"),Margin=new Padding(0,5,4,0) }); options.Controls.Add(_gap); options.Controls.Add(new Label { AutoSize=true,Text=ClassicTextTools.L("Максимум символов", "Maximum characters"),Margin=new Padding(12,5,4,0) }); options.Controls.Add(_characters); }
            else options.Controls.Add(new Label { Name="unicodeNote", AutoSize=true,MaximumSize=new Size(900,0),Text=ClassicTextTools.L("Пустая замена удаляет символ. Список включает русские буквы — сними с них галочки, если их нужно сохранить. Изменения показаны снизу.", "An empty replacement removes the character. The list includes non-Latin alphabets: uncheck letters you want to keep. Review the result below.") });
            var all=new Button { Text=ClassicTextTools.L("Выбрать всё", "Select all"),AutoSize=true }; var invert=new Button { Text=ClassicTextTools.L("Обратить выбор", "Invert selection"),AutoSize=true }; options.Controls.Add(all); options.Controls.Add(invert); layout.Controls.Add(options,0,0);
            _grid=MakeGrid(); _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText=LanguageSettings.Current.General.Apply,Width=65 });
            if(unicode)
            {
                AddColumn(_grid,"character",ClassicTextTools.L("Символ", "Character"),75); AddColumn(_grid,"code","Unicode",90); AddColumn(_grid,"count",ClassicTextTools.L("Количество", "Count"),85); AddColumn(_grid,"replace",ClassicTextTools.L("Заменить на", "Replace with"),160,false); AddColumn(_grid,"lines",ClassicTextTools.L("Реплики", "Subtitles"),100); _grid.Columns[5].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;
            }
            else { AddColumn(_grid,"lines",ClassicTextTools.L("Реплики", "Subtitles"),85); AddColumn(_grid,"before",LanguageSettings.Current.General.Before,340); AddColumn(_grid,"after",LanguageSettings.Current.General.After,340); _grid.Columns[2].AutoSizeMode=_grid.Columns[3].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill; }
            layout.Controls.Add(_grid,0,1); _status=new Label { AutoSize=true,Dock=DockStyle.Fill,Padding=new Padding(0,5,0,5) }; layout.Controls.Add(_status,0,2);
            _preview=MakeGrid(); _preview.ReadOnly=true; AddColumn(_preview,"number","№",55); AddColumn(_preview,"time",ClassicTextTools.L("Начало → конец", "Start → end"),220); AddColumn(_preview,"text",ClassicTextTools.L("Результат", "Result"),300); _preview.Columns[2].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill; layout.Controls.Add(_preview,0,3);
            var footer=new FlowLayoutPanel { Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft }; var ok=new Button { Text=LanguageSettings.Current.General.Ok,AutoSize=true }; var cancel=new Button { Text=LanguageSettings.Current.General.Cancel,AutoSize=true,DialogResult=DialogResult.Cancel }; footer.Controls.Add(cancel); footer.Controls.Add(ok); layout.Controls.Add(footer,0,4); AcceptButton=ok; CancelButton=cancel;
            all.Click+=(s,e)=>Toggle(false); invert.Click+=(s,e)=>Toggle(true); _gap.ValueChanged+=(s,e)=>Populate(); _characters.ValueChanged+=(s,e)=>Populate();
            _grid.CurrentCellDirtyStateChanged+=(s,e)=> { if(_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _grid.CellValueChanged+=(s,e)=> { if(!_refreshing)RefreshResult(); };
            _grid.KeyDown+=(s,e)=> { if(e.KeyCode==Keys.Space && !_grid.IsCurrentCellInEditMode) { foreach(DataGridViewRow row in _grid.SelectedRows) row.Cells[0].Value=!Equals(row.Cells[0].Value,true); e.SuppressKeyPress=true; } };
            ok.Click+=(s,e)=>Apply(); UiUtil.FixFonts(this);
            Action fit=()=> { var note=options.Controls.Find("unicodeNote",false).FirstOrDefault(); if(note!=null)note.MaximumSize=new Size(Math.Max(100,ClientSize.Width-50),0); }; Resize+=(s,e)=>fit(); fit(); Populate();
        }
        private static DataGridView MakeGrid() => new DataGridView { Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.DisplayedCells,BackgroundColor=SystemColors.Window };
        private static void AddColumn(DataGridView grid,string name,string title,int width,bool readOnly=true) { int i=grid.Columns.Add(name,title); grid.Columns[i].Width=width; grid.Columns[i].ReadOnly=readOnly; grid.Columns[i].SortMode=DataGridViewColumnSortMode.NotSortable; grid.Columns[i].DefaultCellStyle.WrapMode=DataGridViewTriState.True; }
        private void Toggle(bool invert) { _grid.EndEdit(); _refreshing=true; foreach(DataGridViewRow row in _grid.Rows) row.Cells[0].Value=!invert || !Equals(row.Cells[0].Value,true); _refreshing=false; RefreshResult(); }
        private void Populate()
        {
            _refreshing=true; _grid.Rows.Clear();
            if(_unicode) { _unicodeItems=ClassicTextTools.UnicodeCharacters(_source,_settings.Replacements); foreach(var c in _unicodeItems) _grid.Rows.Add(true,c.Character,"U+"+c.CodePoint.ToString(c.CodePoint<=65535?"X4":"X6"),c.Count,c.Replacement,string.Join(", ",c.Lines)); }
            else { _merges=ClassicTextTools.DetectMerges(_source,_language,(int)_gap.Value,(int)_characters.Value); foreach(var c in _merges) _grid.Rows.Add(true,(c.Index+1)+" + "+(c.Index+2),c.Before,c.After); }
            _refreshing=false; RefreshResult();
        }
        private void RefreshResult()
        {
            if(_refreshing)return;
            if(_unicode)
            {
                var replacements=new Dictionary<string,string>(); for(int i=0;i<_unicodeItems.Count;i++) if(Equals(_grid.Rows[i].Cells[0].Value,true)) replacements[_unicodeItems[i].Character]=Convert.ToString(_grid.Rows[i].Cells[4].Value)??"";
                Result=ClassicTextTools.ReplaceUnicode(_source,replacements);
            }
            else { var selected=new HashSet<int>(); for(int i=0;i<_merges.Count;i++)if(Equals(_grid.Rows[i].Cells[0].Value,true))selected.Add(_merges[i].Index); Result=ClassicTextTools.Merge(_source,selected,_language); }
            _preview.Rows.Clear(); for(int i=0;i<Result.Paragraphs.Count;i++) { var p=Result.Paragraphs[i]; _preview.Rows.Add(i+1,p.StartTime.ToDisplayString()+" → "+p.EndTime.ToDisplayString(),p.Text); }
            _status.Text=ClassicTextTools.L("Предпросмотр результата: ","Result preview: ")+Result.Paragraphs.Count+ClassicTextTools.L(" реплик", " subtitles");
        }
        private void Apply()
        {
            _grid.EndEdit(); RefreshResult(); var settings=ClassicTextToolSettings.Load();
            if(_unicode)for(int i=0;i<_unicodeItems.Count;i++) { var key=_unicodeItems[i].Character; var value=Convert.ToString(_grid.Rows[i].Cells[4].Value); if(string.IsNullOrEmpty(value)) settings.Replacements.Remove(key); else settings.Replacements[key]=value; }
            else { settings.MaxGap=(int)_gap.Value; settings.MaxCharacters=(int)_characters.Value; }
            try { settings.Save(); DialogResult=DialogResult.OK; } catch(Exception ex)when(ex is System.IO.IOException || ex is UnauthorizedAccessException) { MessageBox.Show(this,ex.Message,Text); }
        }
    }
}
