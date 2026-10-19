using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicCuePicker : Form
    {
        private readonly bool _styles;
        private readonly List<ClassicCueChoice> _items;
        private readonly List<string> _positionShortcuts;
        private readonly TextBox _filter=new TextBox{Dock=DockStyle.Fill};
        private readonly ListView _list=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false,MultiSelect=false,ShowItemToolTips=true};
        private readonly TextBox _details=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
        private readonly Panel _preview=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(35,35,35)};
        private readonly List<Font> _fonts=new List<Font>();
        public string ResultName {get;private set;}
        public bool ResultIsNew {get;private set;}
        public bool OpenManager {get;private set;}
        public List<string> NamesInOrder => _items.Select(i=>i.Name).ToList();
        private ClassicCueChoice Selected => _list.SelectedItems.Count==0?null:_list.SelectedItems[0].Tag as ClassicCueChoice;
        private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
        public ClassicCuePicker(List<ClassicCueChoice> items,bool styles,int selectedCount,Action shortcuts)
        {
            _items=items.ToList();_positionShortcuts=items.Select(i=>i.Shortcut).ToList();_styles=styles;Text=styles?L("Выбрать стиль","Set style"):L("Выбрать актёра","Set actor");
            ClientSize=new Size(860,530);MinimumSize=new Size(760,490);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;KeyPreview=true;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10),ColumnCount=1,RowCount=5};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,28));root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));root.RowStyles.Add(new RowStyle(SizeType.Absolute,72));
            root.Controls.Add(new Label{Dock=DockStyle.Fill,Text=L("Выбрано реплик: ","Selected lines: ")+selectedCount},0,0);root.Controls.Add(_filter,0,1);
            _filter.AccessibleName=L("Поиск или новое имя","Search or new name");
            var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
            _list.Columns.Add("#",28);_list.Columns.Add(styles?L("Стиль","Style"):L("Актёр","Actor"),230);_list.Columns.Add(L("Реплик","Lines"),55);_list.Columns.Add(L("Клавиши","Shortcut"),110);body.Controls.Add(_list,0,0);
            var right=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};right.RowStyles.Add(new RowStyle(SizeType.Absolute,105));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));right.Controls.Add(_preview,0,0);right.Controls.Add(_details,0,1);body.Controls.Add(right,1,0);root.Controls.Add(body,0,2);
            root.Controls.Add(new Label{Dock=DockStyle.Fill,Text=styles?L("1–9 / 0 — выбрать; Enter — применить; Esc — отменить. Предпросмотр шрифта и цветов.","1–9 / 0 to pick; Enter to apply; Esc to cancel. Font and color preview."):L("1–9 / 0 — выбрать; Alt+↑/↓ — порядок; Delete — убрать актёра. Поиск отключает цифровой выбор.","1–9 / 0 to pick; Alt+Up/Down to reorder; Delete to remove actor. Filtering disables number keys.")},0,3);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,WrapContents=true};
            AddButton(footer,L("Применить","Apply"),()=>Pick(Selected));
            var cancel=AddButton(footer,LanguageSettings.Current.General.Cancel,()=>{DialogResult=DialogResult.Cancel;Close();});CancelButton=cancel;
            if(styles)AddButton(footer,L("Управление стилями…","Manage styles…"),()=>{OpenManager=true;DialogResult=DialogResult.Cancel;Close();});
            else{AddButton(footer,"↑",()=>Move(-1));AddButton(footer,"↓",()=>Move(1));AddButton(footer,L("Убрать актёра","Remove actor"),()=>{ResultName=string.Empty;DialogResult=DialogResult.OK;Close();});}
            AddButton(footer,L("Горячие клавиши…","Shortcuts…"),()=>{shortcuts?.Invoke();});root.Controls.Add(footer,0,4);Controls.Add(root);
            _filter.TextChanged+=(s,e)=>Reload();_list.SelectedIndexChanged+=(s,e)=>ShowDetails();_list.DoubleClick+=(s,e)=>Pick(Selected);_preview.Paint+=(s,e)=>PaintPreview(e.Graphics);
            Shown+=(s,e)=>_filter.Focus();FormClosed+=(s,e)=>{foreach(var font in _fonts)font.Dispose();};
            UiUtil.FixFonts(this);Reload();var current=_list.Items.Cast<ListViewItem>().FirstOrDefault(i=>((ClassicCueChoice)i.Tag).Current);if(current!=null)current.Selected=true;
        }
        private static Button AddButton(FlowLayoutPanel parent,string text,Action action)
        {var b=new Button{Text=text,AutoSize=true,MinimumSize=new Size(45,28)};b.Click+=(s,e)=>action();parent.Controls.Add(b);return b;}
        private Font PreviewFont(ClassicCueChoice item,float size)
        {
            var style=item.Style;var flags=FontStyle.Regular;if(style.Bold)flags|=FontStyle.Bold;if(style.Italic)flags|=FontStyle.Italic;if(style.Underline)flags|=FontStyle.Underline;if(style.Strikeout)flags|=FontStyle.Strikeout;
            try{return new Font(style.FontName,size,flags);}catch(ArgumentException){return new Font(Font.FontFamily,size,FontStyle.Regular);}
        }
        private void Reload(string keep=null)
        {
            _list.BeginUpdate();_list.Items.Clear();foreach(var font in _fonts)font.Dispose();_fonts.Clear();
            foreach(var entry in ClassicCuePickerData.Filter(_items,_filter.Text,_styles))
            {
                var item=new ListViewItem(!entry.IsNew && entry.Position<10 && _filter.Text.Length==0?((entry.Position+1)%10).ToString():""){Tag=entry,UseItemStyleForSubItems=false,ToolTipText=entry.Name};
                item.SubItems.Add((entry.IsNew?L("Создать: ","New: "):"")+entry.Name+(entry.Current?" ✓":""));item.SubItems.Add(entry.Count.ToString());item.SubItems.Add(entry.Shortcut??"");
                if(entry.Style!=null){var font=PreviewFont(entry,12);_fonts.Add(font);item.SubItems[1].Font=font;}
                _list.Items.Add(item);
            }
            _list.EndUpdate();var selection=_list.Items.Cast<ListViewItem>().FirstOrDefault(i=>((ClassicCueChoice)i.Tag).Name==keep)??_list.Items.Cast<ListViewItem>().FirstOrDefault();if(selection!=null){selection.Selected=true;selection.EnsureVisible();}ShowDetails();
        }
        private void ShowDetails()
        {
            var item=Selected;if(item==null){_details.Text="";_preview.Invalidate();return;}
            var s=item.Style;_details.Text=item.Name+Environment.NewLine+L("Реплик: ","Lines: ")+item.Count;
            if(s!=null)_details.Text+=Environment.NewLine+string.Join(Environment.NewLine,new[]{L("Шрифт: ","Font: ")+s.FontName+" / "+s.FontSize,L("Позиция: ","Alignment: ")+s.Alignment,L("Рамка: ","Border: ")+s.BorderStyle,L("Контур / тень: ","Outline / shadow: ")+s.OutlineWidth+" / "+s.ShadowWidth,L("Поля L/R/V: ","Margins L/R/V: ")+s.MarginLeft+" / "+s.MarginRight+" / "+s.MarginVertical,"Primary: "+s.Primary,"Secondary: "+s.Secondary,"Outline: "+s.Outline,"Background: "+s.Background});
            _preview.Invalidate();
        }
        private void PaintPreview(Graphics graphics)
        {
            var item=Selected;if(item==null)return;if(item.Style==null){TextRenderer.DrawText(graphics,item.Name,Font,_preview.ClientRectangle,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);return;}
            using(var font=PreviewFont(item,20))
            {
                var bounds=Rectangle.Inflate(_preview.ClientRectangle,-10,-10);var flags=TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix;
                if(item.Style.BorderStyle=="3" || item.Style.BorderStyle=="4")graphics.Clear(Color.FromArgb(255,item.Style.Background));
                else{var shadow=bounds;shadow.Offset(2,2);TextRenderer.DrawText(graphics,item.Name,font,shadow,Color.FromArgb(255,item.Style.Background),flags);for(var x=-1;x<=1;x++)for(var y=-1;y<=1;y++){var outline=bounds;outline.Offset(x,y);TextRenderer.DrawText(graphics,item.Name,font,outline,Color.FromArgb(255,item.Style.Outline),flags);}}
                TextRenderer.DrawText(graphics,item.Name,font,bounds,Color.FromArgb(255,item.Style.Primary),flags);
            }
        }
        private void Pick(ClassicCueChoice item){if(item==null)return;ResultName=item.Name;ResultIsNew=item.IsNew;DialogResult=DialogResult.OK;Close();}
        private void Move(int delta)
        {
            var item=Selected;if(_styles || item==null || item.IsNew)return;var index=_items.IndexOf(item);var next=index+delta;if(next<0 || next>=_items.Count)return;
            _items.RemoveAt(index);_items.Insert(next,item);for(var i=0;i<_items.Count;i++){_items[i].Position=i;_items[i].Shortcut=_positionShortcuts[i];}Reload(item.Name);
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
        {
            if(keyData==Keys.Escape){DialogResult=DialogResult.Cancel;Close();return true;}
            if(keyData==Keys.F1){System.Diagnostics.Process.Start("https://github.com/SubtitleEdit/subtitleedit/blob/v5.3.0/docs/features/subtitle-grid.md");return true;}
            if(keyData==(Keys.Alt|Keys.Up) || keyData==(Keys.Alt|Keys.Down)){Move(keyData==(Keys.Alt|Keys.Up)?-1:1);return true;}
            if(keyData==Keys.Enter){Pick(Selected);return true;}
            if(keyData==Keys.Up || keyData==Keys.Down){var i=_list.SelectedIndices.Count>0?_list.SelectedIndices[0]:0;i=Math.Max(0,Math.Min(_list.Items.Count-1,i+(keyData==Keys.Up?-1:1)));if(_list.Items.Count>0){_list.Items[i].Selected=true;_list.Items[i].EnsureVisible();}return true;}
            if(_filter.Text.Length==0)
            {
                if(!_styles && keyData==Keys.Delete){ResultName="";DialogResult=DialogResult.OK;Close();return true;}
                var digit=keyData>=Keys.D0 && keyData<=Keys.D9?(int)(keyData-Keys.D0):keyData>=Keys.NumPad0 && keyData<=Keys.NumPad9?(int)(keyData-Keys.NumPad0):-1;
                if(digit>=0){var index=digit==0?9:digit-1;if(index<_items.Count)Pick(_items[index]);return true;}
            }
            return base.ProcessCmdKey(ref msg,keyData);
        }
    }
}
