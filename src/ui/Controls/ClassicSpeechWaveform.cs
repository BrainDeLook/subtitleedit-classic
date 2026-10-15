using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Controls
{
    // Selected generated clip on the subtitle timeline. Peaks are kept in clip-relative time.
    public sealed class ClassicSpeechWaveform : Control
    {
        public WavePeakData Peaks { get; set; }
        public double AudioSeconds { get; set; }
        public double CueStart { get; private set; }
        public double CueEnd { get; private set; }
        public double PlaybackSeconds { get; set; } = -1;
        public event Action<double, double> TimingEdited;
        public event Action<double> SeekRequested;
        private double _viewStart, _viewSeconds = 5, _dragStart, _dragEnd;
        private int _drag;

        public ClassicSpeechWaveform()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(24, 24, 24);
            ForeColor = Color.White;
            TabStop = true;
            AccessibleName = "Generated speech waveform";
        }

        public void SetCue(double start, double end, bool fitView)
        {
            CueStart = start; CueEnd = end;
            if (fitView)
            {
                _viewStart = Math.Max(0, start - .5);
                _viewSeconds = Math.Max(2, Math.Max(end - start, AudioSeconds) + 1);
            }
            Invalidate();
        }

        public void Zoom(double factor)
        {
            _viewSeconds = Math.Max(.5, Math.Min(3600, _viewSeconds * factor));
            Invalidate();
        }

        public int TimeToX(double seconds) => (int)Math.Max(-10000, Math.Min(10000, (seconds - _viewStart) * Math.Max(1, Width - 1) / _viewSeconds));
        private double XToTime(int x) => Math.Max(0, _viewStart + x * _viewSeconds / Math.Max(1, Width - 1));

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var start = _drag == 0 ? CueStart : _dragStart;
            var end = _drag == 0 ? CueEnd : _dragEnd;
            var top = 23; var bottom = Math.Max(top + 1, Height - 22); var middle = (top + bottom) / 2;
            using (var shade = new SolidBrush(Color.FromArgb(45, 75, 55)))
                g.FillRectangle(shade, TimeToX(start), top, Math.Max(1, TimeToX(end) - TimeToX(start)), bottom - top);
            using (var grid = new Pen(Color.FromArgb(65, 65, 65)))
            {
                g.DrawLine(grid, 0, middle, Width, middle);
                var step = Math.Pow(10, Math.Floor(Math.Log10(_viewSeconds / 6)));
                if (_viewSeconds / step > 12) step *= 5;
                else if (_viewSeconds / step > 6) step *= 2;
                for (var at = Math.Ceiling(_viewStart / step) * step; at <= _viewStart + _viewSeconds; at += step)
                {
                    var x = TimeToX(at); g.DrawLine(grid, x, top, x, bottom);
                    var label = TimeSpan.FromSeconds(at).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
                    TextRenderer.DrawText(g, label, Font, new Point(Math.Min(Math.Max(0, Width - 90), x), Height - 19), ForeColor);
                }
            }
            if (Peaks != null && Peaks.Peaks.Count > 0)
            {
                using (var normal = new Pen(Color.LimeGreen))
                using (var overrun = new Pen(Color.OrangeRed))
                {
                    // Aggregate every peak covered by a pixel, preserving short transients when zoomed out.
                    for (var x = 0; x < Width; x++)
                    {
                        var from = (_viewStart + x * _viewSeconds / Math.Max(1, Width - 1) - start) * Peaks.SampleRate;
                        var to = from + _viewSeconds * Peaks.SampleRate / Math.Max(1, Width - 1);
                        if (to < 0 || from >= Peaks.Peaks.Count) continue;
                        var first = Math.Max(0, (int)from); var last = Math.Min(Peaks.Peaks.Count - 1, Math.Max(first, (int)Math.Ceiling(to)));
                        int high = 0, low = 0;
                        for (var i = first; i <= last; i++) { high = Math.Max(high, Peaks.Peaks[i].Max); low = Math.Min(low, Peaks.Peaks[i].Min); }
                        var scale = (bottom - top) * .46 / Math.Max(1, Peaks.HighestPeak);
                        g.DrawLine(XToTime(x) > end ? overrun : normal, x, middle - (float)(high * scale), x, middle - (float)(low * scale));
                    }
                }
            }
            using (var edge = new Pen(Color.Gold, 2))
            {
                g.DrawLine(edge, TimeToX(start), top, TimeToX(start), bottom);
                g.DrawLine(edge, TimeToX(end), top, TimeToX(end), bottom);
            }
            if (PlaybackSeconds >= 0)
                using (var cursor = new Pen(Color.Cyan)) g.DrawLine(cursor, TimeToX(start + PlaybackSeconds), top, TimeToX(start + PlaybackSeconds), bottom);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(4, 3, Math.Max(1, Width - 8), 20), ForeColor, TextFormatFlags.EndEllipsis);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;
            Focus();
            var nearStart = Math.Abs(e.X - TimeToX(CueStart)); var nearEnd = Math.Abs(e.X - TimeToX(CueEnd));
            _drag = Math.Min(nearStart, nearEnd) <= 8 ? (nearStart < nearEnd ? 1 : 2) : 0;
            _dragStart = CueStart; _dragEnd = CueEnd;
            if (_drag != 0) Capture = true;
            else if (Peaks != null) SeekRequested?.Invoke(Math.Max(0, Math.Min(AudioSeconds, XToTime(e.X) - CueStart)));
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_drag == 1) { var duration = CueEnd - CueStart; _dragStart = XToTime(e.X); _dragEnd = _dragStart + duration; }
            else if (_drag == 2) _dragEnd = Math.Max(_dragStart + .01, XToTime(e.X));
            Cursor = _drag != 0 || Math.Min(Math.Abs(e.X - TimeToX(CueStart)), Math.Abs(e.X - TimeToX(CueEnd))) <= 8 ? Cursors.SizeWE : Cursors.Hand;
            if (_drag != 0) Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_drag == 0) return;
            _drag = 0; Capture = false; TimingEdited?.Invoke(_dragStart, _dragEnd); Invalidate();
        }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) { _drag = 0; Invalidate(); } }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if ((ModifierKeys & Keys.Control) != 0) Zoom(e.Delta > 0 ? .8 : 1.25);
            else { _viewStart = Math.Max(0, _viewStart - Math.Sign(e.Delta) * _viewSeconds / 5); Invalidate(); }
        }
    }
}
