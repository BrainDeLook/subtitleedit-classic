using System;
using System.IO;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private ToolStripMenuItem _classicGridAppearanceMenu;
        private string ClassicGridAppearancePath => Path.Combine(Configuration.DataDirectory, "ClassicGridAppearance.json");
        private void InitializeClassicGridAppearance()
        {
            var russian = Nikse.SubtitleEdit.Logic.AssistedEditing.ClassicAssistedText.Russian;
            if (_classicGridAppearanceMenu == null)
            {
                _classicGridAppearanceMenu = new ToolStripMenuItem { Name = "classicGridAppearance" };
                optionsToolStripMenuItem.DropDownItems.Add(_classicGridAppearanceMenu);
                for (var i = 0; i < 4; i++)
                {
                    var mode = (ClassicGridFormattingMode)i;
                    var item = new ToolStripMenuItem { Name = "classicGridFormatting" + i, Tag = mode };
                    item.Click += (s, e) => SetClassicGridAppearance(mode);
                    _classicGridAppearanceMenu.DropDownItems.Add(item);
                }
                _classicGridAppearanceMenu.DropDownOpening += (s, e) => RefreshClassicGridAppearanceChecks();
                SubtitleListview1.GridFormattingMode = ClassicGridAppearance.Load(ClassicGridAppearancePath).Formatting;
            }
            _classicGridAppearanceMenu.Text = russian ? "Отображение текста в таблице" : "Subtitle grid formatting";
            var labels = russian
                ? new[] { "Показывать теги", "Показывать форматирование", "Скрывать теги", "Форматирование и остальные теги" }
                : new[] { "Show tags", "Show formatting", "Hide tags", "Show formatting and remaining tags" };
            for (var i = 0; i < labels.Length; i++) _classicGridAppearanceMenu.DropDownItems[i].Text = labels[i];
            RefreshClassicGridAppearanceChecks();
        }
        private void SetClassicGridAppearance(ClassicGridFormattingMode mode)
        {
            try { var settings = ClassicGridAppearance.Load(ClassicGridAppearancePath); settings.Formatting = mode; settings.Save(ClassicGridAppearancePath); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(this, ex.Message, _classicGridAppearanceMenu.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SubtitleListview1.GridFormattingMode = mode;
            RefreshClassicGridAppearanceChecks();
        }
        private void RefreshClassicGridAppearanceChecks()
        {
            foreach (ToolStripMenuItem item in _classicGridAppearanceMenu.DropDownItems)
                item.Checked = (ClassicGridFormattingMode)item.Tag == SubtitleListview1.GridFormattingMode;
        }
    }
}
