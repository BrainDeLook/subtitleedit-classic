using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Controls
{
    public sealed class ClassicTrackTimeline : Control
    {
        private Subtitle _subtitle;
        private double _position, _secondsVisible = 45, _origin;
        private int _selectedIndex = -1, _signature;
        private string[] _tracks = new string[0];
        private int[] _cueTracks = new int[0];
        private readonly VScrollBar _scroll = new VScrollBar { Dock = DockStyle.Right, Visible = false, SmallChange = 1 };
        private const int RowHeight = 28, LabelWidth = 150;
        private int _drag = -1, _edge;
        private double _anchor, _start, _end;
        public Action<int> SelectCue { get; set; }
        public Action<int, double, double> CommitTime { get; set; }
        public Action<double> Seek { get; set; }
        public Subtitle Subtitle { get => _subtitle; set { if (ReferenceEquals(_subtitle, value)) return; _subtitle = value; _signature = int.MinValue; RefreshContent(); } }
        public double Position
        {
            get => _position;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) return;
                value = Math.Max(0, value);
                if (Math.Abs(value - _position) < .00001) return;
                int oldHead = XAt(_position); _position = value;
                // Keep cue boxes and labels still while the playhead moves. Freeze the
                // viewport during a drag so playback cannot change mouse-to-time mapping.
                if (_drag < 0 && (value < _origin || value > _origin + SecondsVisible * .9))
                { _origin = Math.Max(0, value - SecondsVisible * .15); Invalidate(); }
                else { InvalidateHead(oldHead); InvalidateHead(XAt(value)); }
            }
        }
        public double SecondsVisible
        {
            get => _secondsVisible;
            set { if (double.IsNaN(value) || double.IsInfinity(value)) return; _secondsVisible = Math.Max(5, Math.Min(600, value)); _origin = Math.Max(0, _position - _secondsVisible / 3); Invalidate(); }
        }
        public int SelectedIndex
        {
            get => _selectedIndex;
            set { if (value == _selectedIndex) return; InvalidateCue(_selectedIndex); _selectedIndex = value; InvalidateCue(value); }
        }
        public double ViewStart => _origin;
        private int PlotWidth => Math.Max(1, Width - LabelWidth - (_scroll.Visible ? _scroll.Width : 0));
        private int XAt(double time) => LabelWidth + (int)Math.Round((time - _origin) / SecondsVisible * PlotWidth);
        private double TimeAt(int x) => Math.Max(0, _origin + (x - LabelWidth) * SecondsVisible / PlotWidth);
        private static string Track(Paragraph p) => "L" + p.Layer + " · " + (string.IsNullOrWhiteSpace(p.Actor) ? "—" : p.Actor);
        public ClassicTrackTimeline()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(24, 26, 29); ForeColor = Color.White; Height = 190; TabStop = true;
            Controls.Add(_scroll); _scroll.ValueChanged += (s,e) => Invalidate();
        }
        private void InvalidateHead(int x) { if (x >= LabelWidth - 2 && x <= LabelWidth + PlotWidth + 2) Invalidate(new Rectangle(x - 2, 20, 5, Math.Max(1, Height - 20))); }
        private Rectangle CueBounds(int index, double start, double end)
        {
            int x = XAt(start), right = XAt(end), y = 28 + (_cueTracks[index] - _scroll.Value) * RowHeight;
            return new Rectangle(Math.Max(LabelWidth, x), y, Math.Max(3, right - Math.Max(LabelWidth, x)), RowHeight - 4);
        }
        private void InvalidateCue(int index)
        {
            if (_subtitle == null || index < 0 || index >= _subtitle.Paragraphs.Count || index >= _cueTracks.Length) return;
            var cue = _subtitle.Paragraphs[index]; var bounds = CueBounds(index, cue.StartTime.TotalSeconds, cue.EndTime.TotalSeconds); bounds.Inflate(2,2); Invalidate(bounds);
        }
        public void RefreshContent()
        {
            unchecked
            {
                int signature = 17;
                if (_subtitle != null) foreach (var p in _subtitle.Paragraphs)
                { signature = signature * 31 + p.Layer; signature = signature * 31 + (p.Actor?.GetHashCode() ?? 0); signature = signature * 31 + (p.Text?.GetHashCode() ?? 0); signature = signature * 31 + p.StartTime.TotalMilliseconds.GetHashCode(); signature = signature * 31 + p.EndTime.TotalMilliseconds.GetHashCode(); }
                if (signature == _signature && _cueTracks.Length == (_subtitle?.Paragraphs.Count ?? 0)) return;
                _signature = signature;
            }
            var names = new List<string>(); var rows = new Dictionary<string,int>();
            _cueTracks = new int[_subtitle?.Paragraphs.Count ?? 0];
            for (int i = 0; i < _cueTracks.Length; i++) { var name = Track(_subtitle.Paragraphs[i]); if (!rows.TryGetValue(name, out var row)) { row = names.Count; rows[name] = row; names.Add(name); } _cueTracks[i] = row; }
            _tracks = names.ToArray(); UpdateScroll(); Invalidate();
        }
        private void UpdateScroll()
        {
            int visible = Math.Max(1, (Height - 26) / RowHeight);
            _scroll.Visible = _tracks.Length > visible; _scroll.Maximum = Math.Max(0, _tracks.Length - 1); _scroll.LargeChange = visible;
            _scroll.Value = Math.Min(_scroll.Value, Math.Max(0, _tracks.Length - visible));
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); if (_scroll != null) UpdateScroll(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); if (_subtitle == null) return;
            if (_cueTracks.Length != _subtitle.Paragraphs.Count) RefreshContent();
            var g = e.Graphics;
            using (var grid = new Pen(Color.FromArgb(65,70,78))) using (var line = new Pen(Color.OrangeRed,2))
            {
                for (int r = _scroll.Value; r < _tracks.Length; r++) { int y = 26 + (r - _scroll.Value) * RowHeight; if (y >= Height) break; TextRenderer.DrawText(g,_tracks[r],Font,new Rectangle(3,y,LabelWidth-6,RowHeight),ForeColor,TextFormatFlags.EndEllipsis); g.DrawLine(grid,0,y,Width,y); }
                for (double t = Math.Ceiling(_origin / 5) * 5; t < _origin + SecondsVisible; t += 5) { int x = XAt(t); g.DrawLine(grid,x,20,x,Height); TextRenderer.DrawText(g,TimeSpan.FromSeconds(t).ToString(@"hh\:mm\:ss"),Font,new Point(x,1),ForeColor); }
                var saved = g.Save(); g.SetClip(new Rectangle(LabelWidth,26,PlotWidth,Math.Max(1,Height-26)),System.Drawing.Drawing2D.CombineMode.Intersect);
                using (var normal = new SolidBrush(Color.FromArgb(50,73,91))) using (var selected = new SolidBrush(Color.FromArgb(38,123,190)))
                for (int i = 0; i < _subtitle.Paragraphs.Count; i++)
                {
                    var cue = _subtitle.Paragraphs[i]; double start = i == _drag ? _start : cue.StartTime.TotalSeconds, end = i == _drag ? _end : cue.EndTime.TotalSeconds;
                    if (end < _origin || start > _origin + SecondsVisible) continue;
                    var box = CueBounds(i,start,end); if (box.Bottom < 26 || box.Y >= Height || !box.IntersectsWith(e.ClipRectangle)) continue;
                    g.FillRectangle(i == _selectedIndex ? selected : normal,box);
                    TextRenderer.DrawText(g,HtmlUtil.RemoveHtmlTags(cue.Text,true),Font,box,Color.White,TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsClipping);
                }
                g.Restore(saved); int head = XAt(_position); if (head >= LabelWidth && head <= LabelWidth+PlotWidth) g.DrawLine(line,head,21,head,Height);
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); Focus(); if (_subtitle == null || e.X < LabelWidth || e.Button != MouseButtons.Left) return;
            double time = TimeAt(e.X); if (e.Y < 25) { Seek?.Invoke(time); return; }
            int track = _scroll.Value + (e.Y - 26) / RowHeight;
            for (int i = _subtitle.Paragraphs.Count-1; i>=0; i--) { var cue = _subtitle.Paragraphs[i]; if (i < _cueTracks.Length && _cueTracks[i]==track && time>=cue.StartTime.TotalSeconds && time<=cue.EndTime.TotalSeconds) { _drag=i; _start=cue.StartTime.TotalSeconds; _end=cue.EndTime.TotalSeconds; _anchor=time; double edge=SecondsVisible*7/PlotWidth; _edge=time-_start<edge?-1:_end-time<edge?1:0; Capture=true; SelectCue?.Invoke(i); break; } }
            Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e); if (_drag<0 || _subtitle==null || _drag>=_subtitle.Paragraphs.Count) return;
            var cue=_subtitle.Paragraphs[_drag]; double delta=TimeAt(e.X)-_anchor;
            if(_edge==-1)_start=Math.Max(0,Math.Min(cue.EndTime.TotalSeconds-.01,cue.StartTime.TotalSeconds+delta));
            else if(_edge==1)_end=Math.Max(_start+.01,cue.EndTime.TotalSeconds+delta);
            else {_start=Math.Max(0,cue.StartTime.TotalSeconds+delta);_end=_start+cue.DurationTotalMilliseconds/1000;}
            Invalidate();
        }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if(!Capture && _drag>=0){_drag=-1;Invalidate();} }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if(_drag<0)return;int i=_drag;_drag=-1;Capture=false;CommitTime?.Invoke(i,_start,_end);RefreshContent();Invalidate(); }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); if(_drag>=0)return;if(_scroll.Visible && (ModifierKeys & Keys.Control)==0){_scroll.Value=Math.Max(0,Math.Min(Math.Max(0,_tracks.Length-_scroll.LargeChange),_scroll.Value+(e.Delta>0?-1:1)));return;}SecondsVisible*=e.Delta>0?.8:1.25; }
    }
}
