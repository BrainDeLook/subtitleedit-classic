using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using Nikse.SubtitleEdit.Logic.ClassicImport;
namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicSpreadsheetImport : Form
    {
        private readonly DataGridView _raw=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};
        private readonly DataGridView _preview=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
        private readonly FlowLayoutPanel _roles=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,AutoSize=false,WrapContents=false};
        private readonly CheckBox _header=new CheckBox{AutoSize=true};private readonly ComboBox _numeric=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=180};private readonly Label _status=new Label{AutoSize=true};
        private readonly List<ComboBox> _columns=new List<ComboBox>();private List<List<string>> _rows=new List<List<string>>();private bool _updating;
        private readonly Button _ok=new Button{AutoSize=true};private readonly string[] _roleKeys={"none","start","end","duration","text","actor"};
        public Subtitle Result {get;private set;}
        private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
        public ClassicSpreadsheetImport()
        {
            Text=L("Импорт CSV/XLSX/ODS: выбор столбцов","Import CSV/XLSX/ODS: custom columns");Size=new Size(1050,720);MinimumSize=new Size(750,530);StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10),RowCount=5};root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Absolute,110));root.RowStyles.Add(new RowStyle(SizeType.Percent,50));root.RowStyles.Add(new RowStyle(SizeType.Percent,50));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            var top=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var open=new Button{Text=L("Открыть файл…","Open file…"),AutoSize=true};_header.Text=L("Первая строка — заголовки","First row is a header");_numeric.Items.AddRange(new object[]{L("Числовое время: авто","Numeric times: automatic"),L("Секунды","Seconds"),L("Миллисекунды","Milliseconds"),L("Номера кадров","Frame numbers"),L("Доли суток Excel","Excel day fractions")});_numeric.SelectedIndex=0;top.Controls.AddRange(new Control[]{open,_header,_numeric,_status});root.Controls.Add(top,0,0);root.Controls.Add(_roles,0,1);root.Controls.Add(_raw,0,2);root.Controls.Add(_preview,0,3);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=L("Отмена","Cancel"),DialogResult=DialogResult.Cancel,AutoSize=true};_ok.Text=L("Импортировать","Import");_ok.Enabled=false;_ok.Click+=(s,e)=>{UpdatePreview();if(Result?.Paragraphs.Count>0)DialogResult=DialogResult.OK;};footer.Controls.Add(cancel);footer.Controls.Add(_ok);root.Controls.Add(footer,0,4);Controls.Add(root);CancelButton=cancel;
            open.Click+=(s,e)=>{using(var dialog=new OpenFileDialog{Filter="Spreadsheets|*.csv;*.tsv;*.txt;*.xlsx;*.ods"})if(dialog.ShowDialog(this)==DialogResult.OK)try{LoadFile(dialog.FileName);}catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}};_header.CheckedChanged+=(s,e)=>UpdatePreview();_numeric.SelectedIndexChanged+=(s,e)=>UpdatePreview();UiUtil.FixFonts(this);
        }
        public void LoadFile(string path)
        {
            _rows=ClassicSpreadsheet.Read(path);if(_rows.Count==0)return;_updating=true;_raw.Columns.Clear();_raw.Rows.Clear();_roles.Controls.Clear();_columns.Clear();var width=_rows.Max(r=>r.Count);if(width>128)throw new InvalidOperationException("At most 128 columns are supported.");
            for(int i=0;i<width;i++)
            {
                var name=i<_rows[0].Count?_rows[0][i]:"Column "+(i+1);_raw.Columns.Add("c"+i,name);var box=new ComboBox{Width=155,DropDownStyle=ComboBoxStyle.DropDownList,Tag=i};box.Items.AddRange(new object[]{L("Не использовать","None"),L("Начало","Start"),L("Конец","End"),L("Длительность","Duration"),L("Текст","Text"),L("Актёр","Actor")});box.SelectedIndex=Guess(name);_columns.Add(box);var panel=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.TopDown};panel.Controls.Add(new Label{Text=(i+1)+": "+name,AutoSize=true,MaximumSize=new Size(155,40)});panel.Controls.Add(box);_roles.Controls.Add(panel);box.SelectedIndexChanged+=(s,e)=>{if(_updating)return;_updating=true;if(box.SelectedIndex>0)foreach(var other in _columns.Where(c=>c!=box && c.SelectedIndex==box.SelectedIndex))other.SelectedIndex=0;_updating=false;UpdatePreview();};
            }
            foreach(var row in _rows.Take(250))_raw.Rows.Add(Enumerable.Range(0,width).Select(i=>(object)(i<row.Count?row[i]:string.Empty)).ToArray());_header.Checked=_columns.Any(c=>c.SelectedIndex>0);if(_rows[0].Any(c=>c.Trim().EndsWith("ms",StringComparison.OrdinalIgnoreCase)))_numeric.SelectedIndex=2;_updating=false;UpdatePreview();
        }
        private static int Guess(string header){var value=header.Trim().ToLowerInvariant().Replace("_","").Replace(" ","");if(new[]{"start","starttime","startms","begin","from","in","show"}.Contains(value))return 1;if(new[]{"end","endtime","endms","stop","out","hide"}.Contains(value))return 2;if(new[]{"duration","durationms","dur"}.Contains(value))return 3;if(new[]{"text","content","caption","dialogue","dialog"}.Contains(value))return 4;if(new[]{"actor","character","speaker","voice","name"}.Contains(value))return 5;return 0;}
        private void UpdatePreview()
        {
            if(_updating)return;try{Result=ClassicSpreadsheet.Convert(_rows,_columns.Select(c=>_roleKeys[Math.Max(0,c.SelectedIndex)]).ToArray(),_header.Checked,_numeric.SelectedIndex);_preview.Columns.Clear();_preview.Columns.Add("start",L("Начало","Start"));_preview.Columns.Add("end",L("Конец","End"));_preview.Columns.Add("actor",L("Актёр","Actor"));_preview.Columns.Add("text",L("Текст","Text"));foreach(var p in Result.Paragraphs.Take(300))_preview.Rows.Add(p.StartTime,p.EndTime,p.Actor,p.Text);_status.Text=Result.Paragraphs.Count.ToString();_ok.Enabled=Result.Paragraphs.Count>0;}catch(Exception ex){Result=null;_ok.Enabled=false;_status.Text=ex.Message;}
        }
    }
}
