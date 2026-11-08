using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Controls
{
    // Keeps PictureBox's fit-to-window view as the default; zoom and pan only affect
    // painting, so OCR always receives the unmodified source image.
    public sealed class ClassicZoomPictureBox : PictureBox
    {
        public double ZoomFactor { get; private set; } = 1;
        private Point _pan, _dragStart, _panStart;
        public ClassicZoomPictureBox() { SetStyle(ControlStyles.Selectable, true); TabStop = true; }
        public void ResetZoom() { ZoomFactor = 1; _pan = Point.Empty; Invalidate(); }
        public void ChangeZoom(int delta)
        {
            ZoomFactor = Math.Max(.25, Math.Min(8, ZoomFactor * Math.Pow(1.2, delta / 120.0)));
            if (Math.Abs(ZoomFactor - 1) < .001) ResetZoom(); else Invalidate();
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) != 0) { ChangeZoom(e.Delta); if (e is HandledMouseEventArgs handled) handled.Handled = true; }
            else base.OnMouseWheel(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0)) { ResetZoom(); e.SuppressKeyPress = true; }
            else base.OnKeyDown(e);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { Focus(); _dragStart = e.Location; _panStart = _pan; Capture = true; }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (Capture && e.Button == MouseButtons.Left && ZoomFactor > 1)
            { _pan = new Point(_panStart.X + e.X - _dragStart.X, _panStart.Y + e.Y - _dragStart.Y); Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Image == null || ZoomFactor == 1) { base.OnPaint(e); return; }
            var scale = Math.Min(ClientSize.Width / (double)Image.Width, ClientSize.Height / (double)Image.Height) * ZoomFactor;
            var width = (float)(Image.Width * scale); var height = (float)(Image.Height * scale);
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(Image, (ClientSize.Width - width) / 2 + _pan.X, (ClientSize.Height - height) / 2 + _pan.Y, width, height);
        }
    }
}
