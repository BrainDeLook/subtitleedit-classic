using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicErrorList : PositionAndSizeForm
    {
        private readonly List<ClassicLineError> _all;
        private readonly bool _batch;
        private readonly string _fileName, _summary;
        private readonly DataGridView _grid;
        private readonly FlowLayoutPanel _cards;
        private readonly Button _go, _export;
        private readonly Label _status;
        private readonly Timer _statusTimer;
        private readonly ContextMenuStrip _exportMenu;
        public ClassicLineError SelectedError { get; private set; }
        public List<ClassicLineError> VisibleErrors { get; private set; }
        public ClassicErrorList(IEnumerable<ClassicLineError> rows,int totalLines,string fileName,bool batch=false)
        {
            UiUtil.PreInitialize(this); _all=rows.ToList(); _batch=batch; _fileName=fileName??""; _summary=ClassicErrorReport.Summary(_all,totalLines);
            Name=batch?"ClassicBatchErrorList":"ClassicErrorList";Text=ClassicErrorReport.Title; ClientSize=new Size(1080,680); MinimumSize=new Size(780,500); StartPosition=FormStartPosition.CenterParent; ShowInTaskbar=false; MinimizeBox=false;
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(10),ColumnCount=1,RowCount=4 }; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);
            var summary=new Label { Dock=DockStyle.Fill,AutoSize=true,Padding=new Padding(0,0,0,8),Text=_summary }; layout.Controls.Add(summary,0,0);
            _cards=new FlowLayoutPanel { Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Padding=new Padding(0,0,0,8) }; layout.Controls.Add(_cards,0,1);
            var all=new Button { AutoSize=true,Text=ClassicErrorReport.L("Все", "All")+" ("+_all.Count+")",Tag=null };all.Click+=(s,e)=>Filter(null);_cards.Controls.Add(all);
            foreach(ClassicLineErrorType type in Enum.GetValues(typeof(ClassicLineErrorType)))
            {
                var button=new Button {AutoSize=true,Text=ClassicErrorReport.Label(type)+" ("+_all.Count(r=>r.Type==type)+")",Tag=type,FlatStyle=FlatStyle.Flat};button.FlatAppearance.BorderColor=ColorTranslator.FromHtml(ClassicErrorReport.ColorHex(type));button.Click+=(s,e)=>Filter(type);_cards.Controls.Add(button);
            }
            _grid=new DataGridView { Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.DisplayedCells,BackgroundColor=SystemColors.Window };
            var headers=ClassicErrorReport.Headers(batch);for(int i=0;i<headers.Length;i++) {int index=_grid.Columns.Add("c"+i,headers[i]);_grid.Columns[index].SortMode=DataGridViewColumnSortMode.NotSortable;_grid.Columns[index].Width=(batch && i==0)?160:(i==(batch?1:0)?50:130);}_grid.Columns[_grid.Columns.Count-1].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;_grid.Columns[_grid.Columns.Count-1].MinimumWidth=180;_grid.Columns[_grid.Columns.Count-1].DefaultCellStyle.WrapMode=DataGridViewTriState.True;layout.Controls.Add(_grid,0,2);
            var categoryColumn=_batch?2:1;_grid.Columns[categoryColumn].DefaultCellStyle.Padding=new Padding(17,0,0,0);
            _grid.CellPainting+=(s,e)=> { if(e.RowIndex<0 || e.ColumnIndex!=categoryColumn || !(_grid.Rows[e.RowIndex].Tag is ClassicLineError row))return; e.Paint(e.ClipBounds,DataGridViewPaintParts.All);using(var brush=new SolidBrush(ColorTranslator.FromHtml(ClassicErrorReport.ColorHex(row.Type))))e.Graphics.FillEllipse(brush,e.CellBounds.Left+4,e.CellBounds.Top+Math.Max(3,(e.CellBounds.Height-8)/2),8,8);e.Handled=true; };
            var footer=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,8,0,0)};var close=new Button {AutoSize=true,Text=LanguageSettings.Current.General.Close,DialogResult=DialogResult.Cancel};_go=new Button {AutoSize=true,Text=ClassicErrorReport.L("Перейти", "Go to"),Visible=!batch};_export=new Button {AutoSize=true,Text=ClassicErrorReport.L("Экспорт…", "Export…")};_status=new Label {AutoSize=true,Padding=new Padding(3,6,3,0)};footer.Controls.Add(close);footer.Controls.Add(_go);footer.Controls.Add(_export);footer.Controls.Add(_status);layout.Controls.Add(footer,0,3);CancelButton=close;
            _exportMenu=new ContextMenuStrip();AddExport(ClassicErrorReport.L("Копировать в буфер", "Copy to clipboard"),"clipboard");AddExport(ClassicErrorReport.L("Текстовый файл…", "Text file…"),"txt");AddExport(ClassicErrorReport.L("Файл Excel…", "Excel file…"),"xlsx");AddExport(ClassicErrorReport.L("Веб-страница…", "Web page…"),"html");if(batch)AddExport("CSV…","csv");
            _export.Click+=(s,e)=>_exportMenu.Show(_export,new Point(0,_export.Height));_go.Click+=(s,e)=>GoToSelected();_grid.CellDoubleClick+=(s,e)=> {if(e.RowIndex>=0)GoToSelected();};_grid.SelectionChanged+=(s,e)=>_go.Enabled=!batch && _grid.SelectedRows.Count==1;_grid.KeyDown+=(s,e)=> {if(e.KeyCode==Keys.Enter){GoToSelected();e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Home || e.KeyCode==Keys.End){if(_grid.Rows.Count>0)_grid.CurrentCell=_grid.Rows[e.KeyCode==Keys.Home?0:_grid.Rows.Count-1].Cells[0];e.SuppressKeyPress=true;}};
            _statusTimer=new Timer {Interval=3000};_statusTimer.Tick+=(s,e)=> {_status.Text="";_statusTimer.Stop();};FormClosed+=(s,e)=> {_statusTimer.Dispose();_exportMenu.Dispose();};
            KeyPreview=true;KeyDown+=(s,e)=> {if(e.KeyData==UiUtil.HelpKeys){UiUtil.ShowHelp("#list-errors");e.SuppressKeyPress=true;}};
            UiUtil.FixFonts(this);Resize+=(s,e)=>_cards.MaximumSize=new Size(Math.Max(200,ClientSize.Width-25),0);_cards.MaximumSize=new Size(ClientSize.Width-25,0);Filter(null);
        }
        private void AddExport(string label,string kind) {var item=_exportMenu.Items.Add(label);item.Click+=(s,e)=>Export(kind);}
        public void Filter(ClassicLineErrorType? type)
        {
            VisibleErrors=type.HasValue?_all.Where(r=>r.Type==type.Value).ToList():_all.ToList();_grid.Rows.Clear();
            foreach(var row in VisibleErrors){int i=_grid.Rows.Add(ClassicErrorReport.Cells(row,_batch));_grid.Rows[i].Tag=row;}
            foreach(Button card in _cards.Controls)card.BackColor=(type.HasValue?Equals(card.Tag,type.Value):card.Tag==null)?SystemColors.ActiveCaption:SystemColors.Control;
            _export.Enabled=VisibleErrors.Count>0;_go.Enabled=!_batch && VisibleErrors.Count>0;if(_grid.Rows.Count>0)_grid.CurrentCell=_grid.Rows[0].Cells[0];
        }
        private void GoToSelected() {if(_batch || _grid.SelectedRows.Count!=1)return;SelectedError=_grid.SelectedRows[0].Tag as ClassicLineError;if(SelectedError!=null)DialogResult=DialogResult.OK;}
        public void SaveReport(string path,string kind)
        {
            if(kind=="xlsx")File.WriteAllBytes(path,ClassicErrorReport.Xlsx(VisibleErrors,_batch));
            else File.WriteAllText(path,kind=="html"?ClassicErrorReport.Html(VisibleErrors,_summary,_fileName,_batch):kind=="csv"?ClassicErrorReport.Csv(VisibleErrors):ClassicErrorReport.PlainText(VisibleErrors,_summary,_fileName,_batch),new UTF8Encoding(true));
        }
        private void Export(string kind)
        {
            try
            {
                if(kind=="clipboard") {Clipboard.SetText(ClassicErrorReport.Tsv(VisibleErrors,_batch));_status.Text=ClassicErrorReport.L("Скопировано в буфер", "Copied to clipboard");_statusTimer.Start();return;}
                using(var save=new SaveFileDialog {Filter=kind.ToUpperInvariant()+"|*."+kind,DefaultExt=kind,AddExtension=true,FileName=(_batch?"Subtitle-files":Path.GetFileNameWithoutExtension(_fileName))+"-errors."+kind})
                    if(save.ShowDialog(this)==DialogResult.OK){SaveReport(save.FileName,kind);_status.Text=ClassicErrorReport.L("Файл сохранён", "File saved");_statusTimer.Start();}
            }
            catch(Exception ex)when(ex is IOException || ex is UnauthorizedAccessException || ex is ExternalException || ex is ArgumentException){MessageBox.Show(this,ex.Message,Text);}
        }
    }
}
