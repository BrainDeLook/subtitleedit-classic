using Nikse.SubtitleEdit.Logic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
namespace Nikse.SubtitleEdit.Forms.Assa
{
    public sealed class ClassicAssaFilter : Form
    {
        private readonly Subtitle _subtitle;
        private readonly TextBox _text=new TextBox{Dock=DockStyle.Fill};
        private readonly ComboBox _style=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill},_actor=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        private readonly CheckedListBox _layers=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true};
        private readonly ListView _list=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=true,HideSelection=false};
        private readonly CheckBox _filterGrid=new CheckBox{AutoSize=true,Checked=true};
        public Predicate<Paragraph> RowFilter { get { var text=_text.Text;var style=_style.SelectedIndex>0?(string)_style.SelectedItem:null;var actor=_actor.SelectedIndex>0?(string)_actor.SelectedItem:null;return !_filterGrid.Checked || text.Length==0 && style==null && actor==null ? null : new Predicate<Paragraph>(p=>(style==null || (p.Extra??string.Empty)==style) && (actor==null || (p.Actor??string.Empty)==actor) && (p.Text??string.Empty).IndexOf(text,StringComparison.OrdinalIgnoreCase)>=0); } }
        public int[] SelectedIndices=>_list.SelectedItems.Cast<ListViewItem>().Select(i=>(int)i.Tag).ToArray();
        public List<int> HiddenLayers=>_layers.CheckedItems.Cast<int>().ToList();
        private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
        public ClassicAssaFilter(Subtitle subtitle,IEnumerable<int> hidden)
        {
            _subtitle=subtitle;Text=L("Фильтр ASS и скрытые слои предпросмотра","ASS filter and hidden preview layers");Size=new Size(1000,600);MinimumSize=new Size(750,450);StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10),ColumnCount=2,RowCount=5};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,80));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
            _text.PlaceholderTextCompat(L("Текст или ASS-теги","Text or ASS tags"));root.Controls.Add(_text,0,0);root.Controls.Add(_style,0,1);root.Controls.Add(_actor,0,2);root.Controls.Add(_list,0,3);
            var note=new Label{Dock=DockStyle.Fill,Text=L("Отмеченные слои скрыты только в видео. Данные и экспорт сохраняются.","Checked layers are hidden in video only. Data and export are retained.")};root.Controls.Add(note,1,0);root.SetRowSpan(note,3);root.Controls.Add(_layers,1,3);
            foreach(var layer in subtitle.Paragraphs.Select(p=>p.Layer).Distinct().OrderBy(n=>n))_layers.Items.Add(layer,hidden.Contains(layer));
            _style.Items.Add(L("Все стили","All styles"));foreach(var name in subtitle.Paragraphs.Select(p=>p.Extra??string.Empty).Distinct().OrderBy(s=>s))_style.Items.Add(name);
            _actor.Items.Add(L("Все актёры","All actors"));foreach(var name in subtitle.Paragraphs.Select(p=>p.Actor??string.Empty).Distinct().OrderBy(s=>s))_actor.Items.Add(name);
            _list.Columns.Add("#",50);_list.Columns.Add(L("Начало","Start"),110);_list.Columns.Add(L("Слой","Layer"),50);_list.Columns.Add(L("Стиль / актёр","Style / actor"),180);_list.Columns.Add(L("Текст","Text"),450);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=L("Отмена","Cancel"),DialogResult=DialogResult.Cancel,AutoSize=true};var ok=new Button{Text=L("Применить / перейти","Apply / go to"),DialogResult=DialogResult.OK,AutoSize=true};_filterGrid.Text=L("Фильтровать основную таблицу","Filter main subtitle grid");footer.Controls.Add(cancel);footer.Controls.Add(ok);footer.Controls.Add(_filterGrid);root.Controls.Add(footer,0,4);root.SetColumnSpan(footer,2);Controls.Add(root);CancelButton=cancel;AcceptButton=ok;_list.DoubleClick+=(s,e)=>{if(_list.SelectedItems.Count>0)DialogResult=DialogResult.OK;};
            _style.SelectedIndex=0;_actor.SelectedIndex=0;_style.SelectedIndexChanged+=(s,e)=>RefreshRows();_actor.SelectedIndexChanged+=(s,e)=>RefreshRows();_text.TextChanged+=(s,e)=>RefreshRows();RefreshRows();UiUtil.FixFonts(this);
        }
        private void RefreshRows(){_list.BeginUpdate();_list.Items.Clear();for(int i=0;i<_subtitle.Paragraphs.Count;i++){var p=_subtitle.Paragraphs[i];if(_style.SelectedIndex>0 && p.Extra!=(string)_style.SelectedItem || _actor.SelectedIndex>0 && p.Actor!=(string)_actor.SelectedItem || p.Text.IndexOf(_text.Text,StringComparison.OrdinalIgnoreCase)<0)continue;var row=new ListViewItem((i+1).ToString()){Tag=i};row.SubItems.Add(p.StartTime.ToString());row.SubItems.Add(p.Layer.ToString());row.SubItems.Add(p.Extra+" / "+p.Actor);row.SubItems.Add(p.Text);_list.Items.Add(row);}_list.EndUpdate();}
    }
    internal static class ClassicPlaceholder
    {public static void PlaceholderTextCompat(this TextBox box,string description){box.AccessibleName=description;var tip=new ToolTip();tip.SetToolTip(box,description);box.Disposed+=(s,e)=>tip.Dispose();}}
}

