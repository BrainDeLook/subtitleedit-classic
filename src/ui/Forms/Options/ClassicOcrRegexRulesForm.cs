using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Dictionaries;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
namespace Nikse.SubtitleEdit.Forms.Options
{
 public sealed class ClassicOcrRegexRulesForm:Form
 {
  private readonly OcrFixReplaceList _list;
  private readonly DataGridView _grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
  private readonly TextBox _search=new TextBox{Width=240},_find=new TextBox{Dock=DockStyle.Fill},_replace=new TextBox{Dock=DockStyle.Fill};
  private readonly ComboBox _filter=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=190};
  private readonly RichTextBox _input=new RichTextBox{Dock=DockStyle.Fill},_output=new RichTextBox{Dock=DockStyle.Fill,ReadOnly=true};
  private readonly CheckBox _all=new CheckBox{AutoSize=true};
  private readonly Label _status=new Label{AutoSize=true};
  private readonly Button _update=new Button{AutoSize=true},_delete=new Button{AutoSize=true};
  private bool _loading;
  private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
  public ClassicOcrRegexRulesForm(OcrFixReplaceList list)
  {
   UiUtil.PreInitialize(this);_list=list;Text=L("OCR: регулярные выражения","OCR regular expressions");Size=new Size(1050,760);MinimumSize=new Size(800,620);StartPosition=FormStartPosition.CenterParent;
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10),RowCount=5};root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,55));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,45));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};_filter.Items.AddRange(new object[]{L("Все правила","All rules"),L("Включённые","Enabled"),L("Отключённые","Disabled"),L("Мои правила","My rules")});_filter.SelectedIndex=0;
   var enable=new Button{Text=L("Включить выбранные","Enable selected"),AutoSize=true};var disable=new Button{Text=L("Отключить выбранные","Disable selected"),AutoSize=true};bar.Controls.AddRange(new Control[]{new Label{Text=L("Поиск","Search"),AutoSize=true},_search,_filter,enable,disable});root.Controls.Add(bar,0,0);
   _grid.Columns.Add(new DataGridViewCheckBoxColumn{HeaderText=L("Вкл.","On"),FillWeight=12});_grid.Columns.Add("find",L("Найти (regex)","Find (regex)"));_grid.Columns.Add("replace",L("Заменить на","Replace with"));_grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=L("Источник","Source"),FillWeight=25});root.Controls.Add(_grid,0,1);
   var editor=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,AutoSize=true};editor.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));editor.Controls.Add(new Label{Text=L("Найти","Find"),AutoSize=true},0,0);editor.Controls.Add(_find,1,0);editor.Controls.Add(new Label{Text=L("Заменить","Replace"),AutoSize=true},0,1);editor.Controls.Add(_replace,1,1);
   var editButtons=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var add=new Button{Text=L("Добавить правило","Add rule"),AutoSize=true};_update.Text=L("Обновить моё правило","Update my rule");_delete.Text=L("Удалить моё правило","Delete my rule");editButtons.Controls.AddRange(new Control[]{add,_update,_delete});editor.Controls.Add(editButtons,0,2);editor.SetColumnSpan(editButtons,2);root.Controls.Add(editor,0,2);
   var preview=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2};preview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));preview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));preview.RowStyles.Add(new RowStyle(SizeType.AutoSize));preview.RowStyles.Add(new RowStyle(SizeType.Percent,100));preview.Controls.Add(new Label{Text=L("Исходный текст","Input"),AutoSize=true},0,0);preview.Controls.Add(new Label{Text=L("Результат (не меняет субтитры)","Result (does not change subtitles)"),AutoSize=true},1,0);preview.Controls.Add(_input,0,1);preview.Controls.Add(_output,1,1);root.Controls.Add(preview,0,3);
   var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var test=new Button{Text=L("Проверить","Test"),AutoSize=true};_all.Text=L("Все включённые правила","All enabled rules");var close=new Button{Text=L("Закрыть","Close"),AutoSize=true,DialogResult=DialogResult.Cancel};footer.Controls.AddRange(new Control[]{test,_all,_status,close});root.Controls.Add(footer,0,4);Controls.Add(root);CancelButton=close;
   _search.TextChanged+=(s,e)=>Reload();_filter.SelectedIndexChanged+=(s,e)=>Reload();_grid.SelectionChanged+=(s,e)=>SelectRule();enable.Click+=(s,e)=>SetEnabled(true);disable.Click+=(s,e)=>SetEnabled(false);
   _grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0){var rule=(OcrFixRegexRule)_grid.Rows[e.RowIndex].Tag;Save(()=>_list.SetRegexRuleEnabled(rule.Find,rule.ReplaceWith,!rule.IsEnabled));}};
   add.Click+=(s,e)=>Save(()=>_list.AddRegexRule(_find.Text,_replace.Text));
   _update.Click+=(s,e)=>{if(_grid.CurrentRow!=null){var rule=(OcrFixRegexRule)_grid.CurrentRow.Tag;Save(()=>_list.UpdateUserRegexRule(rule.Find,_find.Text,_replace.Text));}};
   _delete.Click+=(s,e)=>{if(_grid.CurrentRow!=null){var rule=(OcrFixRegexRule)_grid.CurrentRow.Tag;Save(()=>_list.RemoveUserRegexRule(rule.Find));}};
   test.Click+=(s,e)=>Test();Reload();UiUtil.FixFonts(this);
  }
  private void SelectRule()
  {
   if(_loading)return;var rule=_grid.CurrentRow==null?null:(OcrFixRegexRule)_grid.CurrentRow.Tag;_update.Enabled=_delete.Enabled=rule?.IsUserRule==true;
   if(rule!=null){_find.Text=rule.Find;_replace.Text=rule.ReplaceWith;}
  }
  private void Reload()
  {
   var selected=_grid.CurrentRow==null?null:((OcrFixRegexRule)_grid.CurrentRow.Tag).Find;_loading=true;_grid.Rows.Clear();
   foreach(var rule in _list.GetRegexRules().Where(r=>(_filter.SelectedIndex==0 || _filter.SelectedIndex==1 && r.IsEnabled || _filter.SelectedIndex==2 && !r.IsEnabled || _filter.SelectedIndex==3 && r.IsUserRule) && (r.Find.IndexOf(_search.Text,StringComparison.OrdinalIgnoreCase)>=0 || r.ReplaceWith.IndexOf(_search.Text,StringComparison.OrdinalIgnoreCase)>=0)))
   {var i=_grid.Rows.Add(rule.IsEnabled,rule.Find,rule.ReplaceWith,rule.IsUserRule?L("Моё","User"):L("Базовое","Built-in"));_grid.Rows[i].Tag=rule;if(rule.Find==selected)_grid.CurrentCell=_grid.Rows[i].Cells[1];}
   _loading=false;SelectRule();_status.Text=L("Правил: ","Rules: ")+_grid.Rows.Count;
  }
  private void Save(Func<bool> operation)
  {
   if(operation()){Reload();return;}
   MessageBox.Show(this,string.IsNullOrEmpty(_list.ErrorMessage)?L("Правило некорректно или уже существует. Базовые правила можно отключить, а редактировать и удалять — только свои.","The rule is invalid or already exists. Built-in rules can be disabled; only user rules can be edited or deleted."):_list.ErrorMessage,Text);
  }
  private void SetEnabled(bool enabled)
  {
   var rules=_grid.SelectedRows.Cast<DataGridViewRow>().Select(row=>(OcrFixRegexRule)row.Tag).Select(r=>new System.Collections.Generic.KeyValuePair<string,string>(r.Find,r.ReplaceWith)).ToArray();if(rules.Length>0)Save(()=>_list.SetRegexRulesEnabled(rules,enabled));
  }
  private void Test()
  {
   try
   {
    var text=_input.Text;var result=text;var count=0;
    _input.SelectAll();_input.SelectionBackColor=_input.BackColor;
    if(_all.Checked)
    {
     foreach(var rule in _list.GetRegexRules().Where(r=>r.IsEnabled)){var regex=new Regex(rule.Find,RegexOptions.Multiline,TimeSpan.FromMilliseconds(100));count+=regex.Matches(result).Count;result=regex.Replace(result,rule.ReplaceWith);}
    }
    else
    {
     var regex=new Regex(_find.Text,RegexOptions.Multiline,TimeSpan.FromMilliseconds(100));var matches=regex.Matches(text);count=matches.Count;
     foreach(Match match in matches){_input.Select(match.Index,match.Length);_input.SelectionBackColor=Color.LightGoldenrodYellow;}
     result=regex.Replace(text,_replace.Text);
    }
    _input.Select(0,0);_output.Text=result;_status.Text=L("Совпадений: ","Matches: ")+count;
   }
   catch(Exception ex)when(ex is ArgumentException || ex is RegexMatchTimeoutException){_status.Text=ex.Message;}
  }
 }
}
