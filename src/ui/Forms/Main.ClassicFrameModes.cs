using System;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private void InitializeClassicFrameModes()
        {
            if (FindClassicMenu(menuStrip1.Items, "classicTimeDisplay") != null) return;
            var menu = new ToolStripMenuItem(ClassicAssistedText.L("Отображение времени", "Time code display")) { Name = "classicTimeDisplay" };
            var names = new[] { ClassicAssistedText.L("Миллисекунды", "Milliseconds"), "HH:MM:SS:FF", ClassicAssistedText.L("Номера кадров", "Frame numbers") };
            for (var i = 0; i < names.Length; i++)
            {
                int mode = i; var item = new ToolStripMenuItem(names[i]);
                item.Click += (s, e) => { Configuration.Settings.General.UseAbsoluteFrameNumbers = mode == 2; Configuration.Settings.General.UseTimeFormatHHMMSSFF = mode == 1; RefreshTimeCodeMode(); SubtitleListview1.Fill(_subtitle, _subtitleOriginal); RefreshSelectedParagraph(); };
                menu.DropDownItems.Add(item);
            }
            var exact = new ToolStripMenuItem(ClassicAssistedText.L("Хранить время на кадрах видео", "Store times on video frames")) { CheckOnClick = true, Checked = Configuration.Settings.General.StoreTimesOnVideoFrames };
            exact.CheckedChanged += (s, e) => { Configuration.Settings.General.StoreTimesOnVideoFrames = exact.Checked; RefreshTimeCodeMode(); SubtitleListview1.Fill(_subtitle, _subtitleOriginal); RefreshSelectedParagraph(); };
            menu.DropDownItems.Add(new ToolStripSeparator()); menu.DropDownItems.Add(exact);
            menu.DropDownOpening += (s, e) => { var mode = Configuration.Settings.General.UseAbsoluteFrameNumbers ? 2 : Configuration.Settings.General.UseTimeFormatHHMMSSFF ? 1 : 0; for (var i = 0; i < 3; i++) ((ToolStripMenuItem)menu.DropDownItems[i]).Checked = i == mode; };
            optionsToolStripMenuItem.DropDownItems.Add(menu);
        }
    }
}
