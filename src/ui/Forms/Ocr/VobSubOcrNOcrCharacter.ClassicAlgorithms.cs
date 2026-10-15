using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic.Ocr;
namespace Nikse.SubtitleEdit.Forms.Ocr
{
    public sealed partial class VobSubOcrNOcrCharacter
    {
        private void InitializeClassicAlgorithms()
        {
            var ru=(Nikse.SubtitleEdit.Core.Common.Configuration.Settings.General.Language ?? "").StartsWith("ru",StringComparison.OrdinalIgnoreCase);
            var oldSize=ClientSize;var existing=Controls.Cast<Control>().ToArray();var bounds=existing.Select(c=>c.Bounds).ToArray();var anchors=existing.Select(c=>c.Anchor).ToArray();
            SuspendLayout();foreach(var c in existing)c.Anchor=AnchorStyles.Top|AnchorStyles.Left;Controls.Clear();ClientSize=new Size(oldSize.Width,oldSize.Height+40);MinimumSize=new Size(MinimumSize.Width,MinimumSize.Height+40);
            var content=new Panel{Bounds=new Rectangle(Point.Empty,oldSize),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right};Controls.Add(content);content.SuspendLayout();content.Controls.AddRange(existing);for(int i=0;i<existing.Length;i++){existing[i].Bounds=bounds[i];existing[i].Anchor=anchors[i];}content.ResumeLayout(false);
            var bar=new FlowLayoutPanel{Location=new Point(0,oldSize.Height),Size=new Size(oldSize.Width,40),Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,Padding=new Padding(8,4,8,4),WrapContents=false};
            bar.Controls.Add(new Label{Text=ru?"Алгоритм линий":"Line algorithm",AutoSize=true,Padding=new Padding(0,5,0,0)});
            var combo=new ComboBox{Width=180,DropDownStyle=ComboBoxStyle.DropDownList};combo.Items.AddRange(Enum.GetNames(typeof(NOcrLineAlgorithm)));combo.SelectedIndex=(int)ClassicNOcrPreferences.LoadAlgorithm();combo.SelectedIndexChanged+=(s,e)=>ClassicNOcrPreferences.SaveAlgorithm((NOcrLineAlgorithm)combo.SelectedIndex);bar.Controls.Add(combo);Controls.Add(bar);ResumeLayout(false);
        }
    }
}
