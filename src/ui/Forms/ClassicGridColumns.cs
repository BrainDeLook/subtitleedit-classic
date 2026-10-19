using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using C = Nikse.SubtitleEdit.Controls.SubtitleListView.SubtitleColumn;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicGridColumns : Form
    {
        private readonly DataGridView _grid;
        private readonly Button _up, _down;
        private readonly Func<C,string> _label;
        private readonly Func<C,string> _note;
        private readonly Func<List<ClassicColumnPreference>> _defaults;
        private List<ClassicColumnPreference> _draft;
        public List<ClassicColumnPreference> Result { get; private set; }
        private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
        public ClassicGridColumns(IEnumerable<ClassicColumnPreference> preferences,Func<C,string> label,Func<C,string> note,Func<List<ClassicColumnPreference>> defaults)
        {
            _draft=preferences.Select(p=>p.Copy()).ToList();_label=label;_note=note;_defaults=defaults;
            Text=L("Столбцы таблицы", "Subtitle grid columns");StartPosition=FormStartPosition.CenterParent;MinimumSize=new Size(600,460);ClientSize=new Size(720,580);ShowInTaskbar=false;
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(10),ColumnCount=2,RowCount=3};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);
            var hint=new Label {AutoSize=true,MaximumSize=new Size(620,0),Text=L("Отметьте нужные столбцы и задайте порядок. Alt+↑ / Alt+↓ перемещают выбранный столбец. Отмена оставляет таблицу без изменений.","Choose column visibility and order. Alt+Up / Alt+Down moves the selected column. Cancel leaves the grid unchanged."),Margin=new Padding(0,0,0,10)};
            layout.Controls.Add(hint,0,0);layout.SetColumnSpan(hint,2);
            _grid=new DataGridView {Dock=DockStyle.Fill,AutoGenerateColumns=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=SystemColors.Window,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,AccessibleName=Text};
            _grid.Columns.Add(new DataGridViewCheckBoxColumn {Name="Visible",HeaderText=L("Видимость","Visible"),Width=85});
            _grid.Columns.Add(new DataGridViewTextBoxColumn {Name="Column",HeaderText=L("Столбец","Column"),ReadOnly=true,Width=170});
            _grid.Columns.Add(new DataGridViewTextBoxColumn {Name="Details",HeaderText=L("Примечание","Note"),ReadOnly=true,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,DefaultCellStyle=new DataGridViewCellStyle {WrapMode=DataGridViewTriState.True}});
            _grid.CurrentCellDirtyStateChanged+=(s,e)=>{if(_grid.IsCurrentCellDirty)_grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
            _grid.CellValueChanged+=(s,e)=>{if(e.RowIndex>=0 && e.ColumnIndex==0 && e.RowIndex<_draft.Count){var item=_draft[e.RowIndex];if(ClassicColumnPreference.TryColumn(item.Key,out var c) && ClassicColumnPreference.CanToggle(c))item.Visible=Convert.ToBoolean(_grid.Rows[e.RowIndex].Cells[0].Value);}};
            _grid.SelectionChanged+=(s,e)=>RefreshButtons();
            _grid.KeyDown+=(s,e)=>{if(e.Alt && (e.KeyCode==Keys.Up || e.KeyCode==Keys.Down)){MoveSelected(e.KeyCode==Keys.Up?-1:1);e.Handled=true;e.SuppressKeyPress=true;}};
            layout.Controls.Add(_grid,0,1);
            var moves=new FlowLayoutPanel {AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(5,0,0,0)};
            _up=new Button {Text=L("Вверх","Up"),AutoSize=true};_down=new Button {Text=L("Вниз","Down"),AutoSize=true};_up.Click+=(s,e)=>MoveSelected(-1);_down.Click+=(s,e)=>MoveSelected(1);moves.Controls.AddRange(new Control[]{_up,_down});layout.Controls.Add(moves,1,1);
            var footer=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,WrapContents=true};
            var cancel=new Button {Text=L("Отмена","Cancel"),DialogResult=DialogResult.Cancel,AutoSize=true};
            var ok=new Button {Text="OK",AutoSize=true};ok.Click+=(s,e)=>{_grid.EndEdit();Result=_draft.Select(p=>p.Copy()).ToList();DialogResult=DialogResult.OK;};
            var reset=new Button {Text=L("По умолчанию","Reset to defaults"),AutoSize=true};reset.Click+=(s,e)=>{_draft=_defaults().Select(p=>p.Copy()).ToList();Fill(0);};
            footer.Controls.AddRange(new Control[]{cancel,ok,reset});layout.Controls.Add(footer,0,2);layout.SetColumnSpan(footer,2);AcceptButton=ok;CancelButton=cancel;Fill(0);UiUtil.FixFonts(this);
        }
        private void Fill(int selected)
        {
            _grid.Rows.Clear();
            foreach(var preference in _draft)
            {
                var column=(C)Enum.Parse(typeof(C),preference.Key);var row=_grid.Rows[_grid.Rows.Add(preference.Visible,_label(column),_note(column))];
                if(!ClassicColumnPreference.CanToggle(column)){row.Cells[0].ReadOnly=true;row.Cells[0].Style.BackColor=SystemColors.Control;}
            }
            if(_grid.Rows.Count>0){_grid.CurrentCell=_grid.Rows[Math.Max(0,Math.Min(selected,_grid.Rows.Count-1))].Cells[1];}
            RefreshButtons();
        }
        private void MoveSelected(int direction)
        {
            _grid.EndEdit();var from=_grid.CurrentCell?.RowIndex??-1;var to=from+direction;
            if(from<0 || to<0 || to>=_draft.Count)return;
            var item=_draft[from];_draft.RemoveAt(from);_draft.Insert(to,item);Fill(to);_grid.Focus();
        }
        private void RefreshButtons()
        {
            if(_up==null)return;var index=_grid.CurrentCell?.RowIndex??-1;_up.Enabled=index>0;_down.Enabled=index>=0 && index<_draft.Count-1;
        }
    }
}
