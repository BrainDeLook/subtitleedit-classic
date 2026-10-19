using System.Drawing;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms.Styles
{
    public sealed class ClassicStyleConflictDialog : Form
    {
        private readonly CheckBox _all=new CheckBox{Dock=DockStyle.Fill};
        public ClassicStyleConflict Choice {get;private set;}
        public bool ForAll=>_all.Checked;
        public ClassicStyleConflictDialog(string name,bool multiple)
        {
            Text=ClassicAssistedText.L("Совпадение имён стилей","Style name conflict");ClientSize=new Size(610,180);MinimumSize=new Size(560,215);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;MaximizeBox=false;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=3};root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            root.Controls.Add(new Label{Dock=DockStyle.Fill,AutoEllipsis=true,Text=ClassicAssistedText.L("Стиль уже существует: ","A style already exists: ")+name+"\n"+ClassicAssistedText.L("Заменить его оформление или добавить стиль под новым именем?","Replace its formatting or add a style with a new name?")},0,0);
            _all.Text=ClassicAssistedText.L("Применить ко всем конфликтам этой операции","Use this answer for all conflicts in this operation");_all.Visible=multiple;root.Controls.Add(_all,0,1);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};
            var cancel=new Button{Text=ClassicAssistedText.L("Отмена","Cancel"),AutoSize=true,DialogResult=DialogResult.Cancel};
            var both=new Button{Text=ClassicAssistedText.L("Сохранить оба","Keep both"),AutoSize=true};var overwrite=new Button{Text=ClassicAssistedText.L("Перезаписать","Overwrite"),AutoSize=true};
            both.Click+=(s,e)=>{Choice=ClassicStyleConflict.KeepBoth;DialogResult=DialogResult.OK;};overwrite.Click+=(s,e)=>{Choice=ClassicStyleConflict.Overwrite;DialogResult=DialogResult.OK;};
            footer.Controls.Add(cancel);footer.Controls.Add(both);footer.Controls.Add(overwrite);root.Controls.Add(footer,0,2);Controls.Add(root);CancelButton=cancel;AcceptButton=cancel;UiUtil.FixFonts(this);
        }
    }
}
