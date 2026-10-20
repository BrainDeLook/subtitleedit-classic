using System;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Forms.Options
{
    public sealed partial class Settings
    {
        private RadioButton _classicFfmpegRadio;
        private void InitializeClassicFfmpegOption()
        {
            if (_classicFfmpegRadio != null) return;
            int bottom = groupBoxVideoEngine.Bottom;
            foreach (Control control in panelVideoPlayer.Controls) if (control != groupBoxVideoEngine && control.Top >= bottom) control.Top += 30;
            groupBoxVideoEngine.Height += 30;
            _classicFfmpegRadio = new RadioButton { Text = "FFmpeg (experimental)", Name = "classicFfmpegRadio", AutoSize = true, Left = 9, Top = 160, Checked = Configuration.Settings.General.VideoPlayer == "FFmpeg" };
            groupBoxVideoEngine.Controls.Add(_classicFfmpegRadio);
            if (Configuration.Settings.General.VideoPlayer == "FFmpeg") { _classicFfmpegRadio.Checked = false; _classicFfmpegRadio.Checked = true; }
        }
    }
}
