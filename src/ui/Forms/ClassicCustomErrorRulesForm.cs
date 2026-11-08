using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicCustomErrorRulesForm : Form
    {
        public ClassicCustomErrorRulesForm()
        {
            UiUtil.PreInitialize(this); Text = ClassicAssistedText.L("Пользовательские правила ошибок", "Custom error rules");
            Size = new Size(880, 520); MinimumSize = new Size(650, 360); StartPosition = FormStartPosition.CenterParent;
            var rules = new BindingList<ClassicCustomErrorRule>(ClassicCustomErrorRules.Definitions());
            var grid = new DataGridView { Dock = DockStyle.Fill, DataSource = rules, AutoGenerateColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Enabled", HeaderText = ClassicAssistedText.L("Вкл.", "On"), FillWeight = 15 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = ClassicAssistedText.L("Название", "Name"), FillWeight = 35 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Pattern", HeaderText = "Regex", FillWeight = 100 });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IgnoreCase", HeaderText = ClassicAssistedText.L("Без регистра", "Ignore case"), FillWeight = 25 });
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8), FlowDirection = FlowDirection.RightToLeft };
            var save = new Button { Text = ClassicAssistedText.L("Сохранить", "Save"), AutoSize = true }; var cancel = new Button { Text = ClassicAssistedText.L("Отмена", "Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
            var status = new Label { Text = ClassicCustomErrorRules.LoadError ?? ClassicAssistedText.L("До 32 правил. Совпадения подсвечиваются в списке и попадают в отчёт ошибок.", "Up to 32 rules. Matches are highlighted in the grid and included in error reports."), AutoSize = true, MaximumSize = new Size(570, 0) };
            save.Click += (s, e) =>
            {
                grid.EndEdit(); BindingContext[rules].EndCurrentEdit();
                try { ClassicCustomErrorRules.Save(rules); DialogResult = DialogResult.OK; }
                catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException) { MessageBox.Show(this, ex.Message, Text); }
            };
            footer.Controls.AddRange(new Control[] { save, cancel, status }); Controls.Add(grid); Controls.Add(footer); CancelButton = cancel; UiUtil.FixFonts(this);
        }
    }
}
