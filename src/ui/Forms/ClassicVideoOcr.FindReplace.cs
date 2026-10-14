using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic.VideoOcr;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class ClassicVideoOcr
    {
        private void ShowOcrFindReplace()
        {
            if(_operation!=null || _lines.Rows.Count==0)return;_lines.EndEdit();
            using(var dialog=new Form{Text=L("Найти и заменить — OCR","Find and replace — OCR"),StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(590,235),MinimumSize=new Size(560,270)})
            {
                var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(10),RowCount=5};root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
                var find=new TextBox{Dock=DockStyle.Fill};var replacement=new TextBox{Dock=DockStyle.Fill};
                root.Controls.Add(new Label{Text=L("Найти","Find"),AutoSize=true},0,0);root.Controls.Add(find,1,0);root.Controls.Add(new Label{Text=L("Заменить на","Replace with"),AutoSize=true},0,1);root.Controls.Add(replacement,1,1);
                var regular=new CheckBox{Text=L("Регулярное выражение","Regular expression"),AutoSize=true};var sensitive=new CheckBox{Text=L("Учитывать регистр","Case sensitive"),AutoSize=true};var words=new CheckBox{Text=L("Целые слова","Whole words"),AutoSize=true};
                var options=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};options.Controls.AddRange(new Control[]{regular,sensitive,words});root.Controls.Add(options,0,2);root.SetColumnSpan(options,2);
                var status=new Label{AutoSize=true,Dock=DockStyle.Fill};root.Controls.Add(status,0,3);root.SetColumnSpan(status,2);
                var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var next=new Button{Text=L("Найти далее","Find next"),AutoSize=true};var all=new Button{Text=L("Заменить все","Replace all"),AutoSize=true};var undo=new Button{Text=L("Отменить замену","Undo replace"),AutoSize=true,Enabled=false};var close=new Button{Text=L("Закрыть","Close"),AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.AddRange(new Control[]{next,all,undo,close});root.Controls.Add(buttons,0,4);root.SetColumnSpan(buttons,2);dialog.Controls.Add(root);dialog.AcceptButton=next;dialog.CancelButton=close;
                var snapshot=new List<Tuple<DataGridViewRow,string>>();var last=-1;
                next.Click+=(s,e)=>
                {
                    try
                    {
                        var pattern=ClassicOcrSearchReplace.Pattern(find.Text,regular.Checked,sensitive.Checked,words.Checked);
                        for(int n=0;n<_lines.Rows.Count;n++){var index=(last+1+n)%_lines.Rows.Count;var row=_lines.Rows[index];if(!pattern.IsMatch(Convert.ToString(row.Cells[3].Value)))continue;_lines.ClearSelection();row.Selected=true;_lines.CurrentCell=row.Cells[3];_lines.FirstDisplayedScrollingRowIndex=index;last=index;status.Text=L("Найдено в строке ","Found in row ")+(index+1);return;}
                        status.Text=L("Совпадений нет","No matches");
                    }
                    catch(Exception ex) when(ex is ArgumentException || ex is System.Text.RegularExpressions.RegexMatchTimeoutException){status.Text=ex.Message;}
                };
                all.Click+=(s,e)=>
                {
                    try
                    {
                        var pattern=ClassicOcrSearchReplace.Pattern(find.Text,regular.Checked,sensitive.Checked,words.Checked);var changes=new List<Tuple<DataGridViewRow,string,string>>();var count=0;
                        foreach(DataGridViewRow row in _lines.Rows){var original=Convert.ToString(row.Cells[3].Value);var updated=ClassicOcrSearchReplace.Replace(original,pattern,replacement.Text,regular.Checked,out var matches);count+=matches;if(updated!=original)changes.Add(Tuple.Create(row,original,updated));}
                        if(changes.Count>0){snapshot=changes.Select(c=>Tuple.Create(c.Item1,c.Item2)).ToList();foreach(var c in changes){c.Item1.Cells[3].Value=c.Item3;MarkSpelling(c.Item1);}undo.Enabled=true;}
                        status.Text=L("Заменено: ","Replaced: ")+count;
                    }
                    catch(Exception ex) when(ex is ArgumentException || ex is System.Text.RegularExpressions.RegexMatchTimeoutException){status.Text=ex.Message;}
                };
                undo.Click+=(s,e)=>{foreach(var original in snapshot){original.Item1.Cells[3].Value=original.Item2;MarkSpelling(original.Item1);}undo.Enabled=false;status.Text=L("Замена отменена","Replacement undone");};
                dialog.ShowDialog(this);
            }
        }
    }
}
