using System;
using System.Drawing;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Forms.AudioToText;

namespace Nikse.SubtitleEdit.Forms.Ocr
{
    public sealed partial class VobSubOcr
    {
        private void InitializeClassicImageNavigation()
        {
            var text = ClassicSpeechSettings.L("Изображения субтитров… (← / →)", "Subtitle images… (← / →)");
            contextMenuStripImage.Items.Add(text, null, (sender, e) => ShowClassicImageViewer());
            contextMenuStripListview.Items.Add(text, null, (sender, e) => ShowClassicImageViewer());
            KeyDown += (sender, e) =>
            {
                if (e.Control && (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0))
                { pictureBoxSubtitleImage.ResetZoom(); e.SuppressKeyPress = true; }
            };
        }
        private void ShowClassicImageViewer()
        {
            if (_selectedIndex < 0 || GetSubtitleCount() == 0) return;
            using (var viewer = new ExportPngXmlPreview(index => GetSubtitleBitmap(index, false), _selectedIndex, GetSubtitleCount(),
                index => subtitleListView1.SelectIndexAndEnsureVisible(index))) viewer.ShowDialog(this);
        }
        private Bitmap CreateClassicPreviewFrame(int index)
        {
            GetSubtitleScreenSize(index, out var width, out var height);
            GetSubtitleTopAndHeight(index, out var left, out var top, out _, out _);
            var bitmap = new Bitmap(width, height);
            try
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    FillPreviewBackground(bitmap, graphics, _subtitle.Paragraphs[index]);
                    var fromMenu = _fromMenuItem;
                    _fromMenuItem = true;
                    try
                    {
                        using (var subtitle = GetSubtitleBitmap(index, false)) graphics.DrawImageUnscaled(subtitle, left, top);
                    }
                    finally { _fromMenuItem = fromMenu; }
                }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }
    }
}
