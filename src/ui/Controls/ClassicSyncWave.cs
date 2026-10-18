using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Controls
{
    internal sealed class ClassicSyncWave : IDisposable
    {
        public readonly AudioVisualizer Wave;
        private readonly Panel _handle;
        private readonly Func<Rectangle> _bounds;
        private readonly Action<int> _resizeVideo;
        private readonly string _settings;
        public int RequestedHeight { get; private set; } = 95;
        public event Action<int> HeightChanged;
        public ClassicSyncWave(Control parent, Func<Rectangle> bounds, Action<int> resizeVideo, string settings, Action<double> seek)
        {
            _bounds = bounds; _resizeVideo = resizeVideo; _settings = Path.Combine(Configuration.DataDirectory, settings);
            try { if (int.TryParse(File.ReadAllText(_settings), out var h)) RequestedHeight = Math.Max(45, Math.Min(220, h)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            Wave = new AudioVisualizer { Visible = false, ReadOnlyView = true, TabStop = true, ShowWaveform = true };
            _handle = new Panel { Visible = false, Cursor = Cursors.HSplit, BackColor = SystemColors.ControlDark, Height = 4 };
            parent.Controls.Add(Wave); parent.Controls.Add(_handle);
            Wave.OnPositionSelected += (s, e) => seek(Math.Max(0, e.Seconds));
            _handle.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _handle.Capture = true; };
            _handle.MouseMove += (s, e) =>
            {
                if (!_handle.Capture) return;
                var y = parent.PointToClient(_handle.PointToScreen(e.Location)).Y;
                SetHeight(_bounds().Bottom - y); HeightChanged?.Invoke(RequestedHeight);
            };
            _handle.MouseUp += (s, e) => { _handle.Capture = false; SaveHeight(); };
        }
        public void SetHeight(int height) { RequestedHeight = Math.Max(45, Math.Min(220, height)); Layout(); }
        public void Layout()
        {
            var r = _bounds(); var visible = Wave.WavePeaks != null;
            Wave.Visible = _handle.Visible = visible;
            if (!visible) { _resizeVideo(Math.Max(40, r.Height)); return; }
            var h = Math.Max(30, Math.Min(RequestedHeight, r.Height - 85));
            _resizeVideo(Math.Max(50, r.Height - h - 4));
            _handle.SetBounds(r.Left, r.Bottom - h - 4, r.Width, 4); Wave.SetBounds(r.Left, r.Bottom - h, r.Width, h);
            Wave.BringToFront(); _handle.BringToFront();
        }
        public void Update(Subtitle source, double position, int selected)
        {
            if (Wave.WavePeaks == null || source == null) return;
            var start = Wave.StartPositionSeconds;
            if (position < start || position > Wave.EndPositionSeconds - 0.2) start = Math.Max(0, position - 1);
            Wave.SetPosition(start, source, position, selected, null);
        }
        private void SaveHeight() { try { File.WriteAllText(_settings, RequestedHeight.ToString()); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        public void Dispose() { SaveHeight(); Wave.Dispose(); _handle.Dispose(); }
    }
}
