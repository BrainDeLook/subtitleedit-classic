using Nikse.SubtitleEdit.Core.Common;
using System;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private ToolStripMenuItem _classicVideoOcrMenu;
        private void InitializeClassicVideoOcrMenu()
        {
            if(_classicVideoOcrMenu==null)
            {
                _classicVideoOcrMenu=new ToolStripMenuItem { Name="classicVideoOcr" };
                _classicVideoOcrMenu.Click+=ToolStripMenuItemImportOcrHardSubClick;
                toolStripMenuItemVideo.DropDownItems.Add(_classicVideoOcrMenu);
            }
            _classicVideoOcrMenu.Text=Configuration.Settings.General.Language.StartsWith("ru",StringComparison.OrdinalIgnoreCase)
                ? "OCR вшитых субтитров…" : "OCR burned-in subtitles…";
        }
    }
}
