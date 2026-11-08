using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
namespace Nikse.SubtitleEdit.Forms.Translate
{
 public sealed class ClassicTranslationSearchForm:Form
 {
  private Subtitle _working;private int _cursor;private readonly Action<int> _select;
  private readonly TextBox _find=new TextBox{Dock=DockStyle.Fill},_replacement=new TextBox{Dock=DockStyle.Fill},_text=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
  private readonly CheckBox _case=new CheckBox{AutoSize=true},_regex=new CheckBox{Text="Regex",AutoSize=true};private readonly Label _status=new Label{AutoSize=true};
  public Subtitle Result=>_working;
  private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
  public ClassicTranslationSearchForm(Subtitle translations,int start,Action<int> select)
  {
   UiUtil.PreInitialize(this);_working=new Subtitle(translations,false);_cursor=start-1;_select=select;Text=L("Поиск и замена в переводе","Find and replace in translation");Size=new Size(790,430);MinimumSize=new Size(650,380);StartPosition=FormStartPosition.CenterParent;
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=2,RowCount=6};root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));for(int i=0;i<4;i++)root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   root.Controls.Add(new Label{Text=L("Найти","Find"),AutoSize=true},0,0);root.Controls.Add(_find,1,0);root.Controls.Add(new Label{Text=L("Заменить","Replace"),AutoSize=true},0,1);root.Controls.Add(_replacement,1,1);_case.Text=L("Учитывать регистр","Match case");
   var options=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};options.Controls.AddRange(new Control[]{_case,_regex});root.Controls.Add(options,0,2);root.SetColumnSpan(options,2);
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var next=new Button{Text=L("Найти далее","Find next"),AutoSize=true};var replace=new Button{Text=L("Заменить одно","Replace one"),AutoSize=true};var all=new Button{Text=L("Заменить все","Replace all"),AutoSize=true};actions.Controls.AddRange(new Control[]{next,replace,all});root.Controls.Add(actions,0,3);root.SetColumnSpan(actions,2);root.Controls.Add(_text,0,4);root.SetColumnSpan(_text,2);
   var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var ok=new Button{Text=L("Применить","Apply"),AutoSize=true,DialogResult=DialogResult.OK};var cancel=new Button{Text=L("Отмена","Cancel"),AutoSize=true,DialogResult=DialogResult.Cancel};footer.Controls.AddRange(new Control[]{ok,cancel,_status});root.Controls.Add(footer,0,5);root.SetColumnSpan(footer,2);Controls.Add(root);CancelButton=cancel;
   next.Click+=(s,e)=>Run(false,false);replace.Click+=(s,e)=>Run(true,false);all.Click+=(s,e)=>Run(true,true);UiUtil.FixFonts(this);
  }
  private void Run(bool replace,bool all)
  {
   try
   {
    var query=new ClassicTranslationSearch(_find.Text,_replacement.Text,_case.Checked,_regex.Checked);
    if(!replace){_cursor=query.FindNext(_working,_cursor+1);if(_cursor>=0)_select(_cursor);}
    else
    {
     if(!all && _cursor<0)_cursor=query.FindNext(_working,0);
     if(!all && _cursor<0){_status.Text=L("Совпадений нет","No matches");return;}
     _working=query.Replace(_working,all?(int?)null:_cursor,!all,out var count);_status.Text=L("Замен: ","Replacements: ")+count;
    }
    if(_cursor>=0 && _cursor<_working.Paragraphs.Count){_text.Text=_working.Paragraphs[_cursor].Text;if(!replace)_status.Text=L("Строка ","Row ")+(_cursor+1);}
    else{_text.Clear();if(!replace)_status.Text=L("Совпадений нет","No matches");}
   }
   catch(Exception ex)when(ex is ArgumentException || ex is RegexMatchTimeoutException){_status.Text=ex.Message;}
  }
 }
}
