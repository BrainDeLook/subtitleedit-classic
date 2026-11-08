using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using Nikse.SubtitleEdit.Logic.SpellCheck;
namespace Nikse.SubtitleEdit.Forms.Options
{
 public sealed class ClassicRemovedWordsForm:Form
 {
    public ClassicRemovedWordsForm(string language)
    {
        Text=L("Исключённые слова — ","Removed dictionary words — ")+language;ClientSize=new Size(590,440);MinimumSize=new Size(450,330);StartPosition=FormStartPosition.CenterParent;
        var list=new ListBox{Dock=DockStyle.Fill,SelectionMode=SelectionMode.MultiExtended};list.Items.AddRange(ClassicRemovedDictionaryWords.UserRemoved(language).OrderBy(x=>x).Cast<object>().ToArray());
        var field=new TextBox{Width=280};var add=new Button{AutoSize=true,Text=L("Добавить","Add")};var remove=new Button{AutoSize=true,Text=L("Убрать из списка","Remove from list")};
        var edit=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink};edit.Controls.AddRange(new Control[]{field,add,remove});
        var note=new Label{Dock=DockStyle.Top,Height=56,Text=L("Эти слова считаются ошибками, даже если они есть в основном словаре. Исходный словарь не изменяется. Чтобы вернуть слово, уберите его из этого списка.","These words are rejected even when the main dictionary accepts them. The original dictionary is unchanged. Remove a word from this list to accept it again.")};
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=40,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{AutoSize=true,Text=L("Отмена","Cancel"),DialogResult=DialogResult.Cancel};var save=new Button{AutoSize=true,Text=L("Сохранить","Save")};footer.Controls.AddRange(new Control[]{cancel,save});
        add.Click+=(sender,e)=>{var word=field.Text.Trim();if(word.Length>0 && !list.Items.Contains(word)){list.Items.Add(word);field.Clear();}};
        remove.Click+=(sender,e)=>{foreach(var word in list.SelectedItems.Cast<object>().ToArray())list.Items.Remove(word);};
        save.Click+=(sender,e)=>{try{ClassicRemovedDictionaryWords.Save(language,list.Items.Cast<string>());DialogResult=DialogResult.OK;}catch(Exception error)when(error is IOException || error is UnauthorizedAccessException || error is System.Xml.XmlException){MessageBox.Show(this,error.Message,Text);}};
        Controls.Add(list);Controls.Add(edit);Controls.Add(footer);Controls.Add(note);CancelButton=cancel;UiUtil.FixFonts(this);
    }
    private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
 }
}
