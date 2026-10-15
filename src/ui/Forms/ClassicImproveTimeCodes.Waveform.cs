using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.ClassicTiming;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class ClassicImproveTimeCodes
    {
        private readonly AudioVisualizer _waveOriginal = NewVisualizer(true);
        private readonly AudioVisualizer _waveAligned = NewVisualizer(false);
        private Subtitle _waveBefore, _waveProposals;
        private CancellationTokenSource _waveOperation;
        private bool _syncWave, _waveCloseRequested;
        private readonly Label _waveStatus = new Label {AutoSize = true};
        private readonly Button _loadWave = new Button {AutoSize = true};
        private static AudioVisualizer NewVisualizer(bool original)
        {
            return new AudioVisualizer {Dock = DockStyle.Fill, Locked = original, AllowNewSelection = false, ShowWaveform = true, ShowGridLines = true, BackgroundColor = Color.FromArgb(24,24,24), Color = Color.LimeGreen, SelectedColor = Color.YellowGreen, ParagraphColor = original ? Color.SteelBlue : Color.DarkOrange, TextColor = Color.White, CursorColor = Color.OrangeRed, GridColor = Color.FromArgb(60,60,60), TextSize = 9};
        }
        private Control BuildWaveformArea()
        {
            var area = new TableLayoutPanel {Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5};
            foreach (var style in new[] {new RowStyle(SizeType.AutoSize), new RowStyle(SizeType.Absolute,18), new RowStyle(SizeType.Percent,50), new RowStyle(SizeType.Absolute,18), new RowStyle(SizeType.Percent,50)}) area.RowStyles.Add(style);
            var bar = new FlowLayoutPanel {Dock = DockStyle.Fill, AutoSize = true, WrapContents = false};
            _loadWave.Text = L("Осциллограмма", "Waveform"); var zoomIn = new Button {Text = "+", Width = 35}; var zoomOut = new Button {Text = "−", Width = 35}; var reset = new Button {Text = L("Вернуть исходное", "Reset selected"), AutoSize = true}; var listen = new Button {Text = L("Прослушать", "Listen"), AutoSize = true};
            bar.Controls.AddRange(new Control[] {_loadWave,zoomIn,zoomOut,reset,listen,_waveStatus}); area.Controls.Add(bar,0,0);
            area.Controls.Add(new Label {Text = L("Исходные тайминги", "Original time codes"), Dock = DockStyle.Fill},0,1); area.Controls.Add(_waveOriginal,0,2);
            area.Controls.Add(new Label {Text = L("Новые тайминги — перетаскивай начало, конец или всю реплику", "Proposed time codes — drag start, end or the whole cue"), Dock = DockStyle.Fill},0,3); area.Controls.Add(_waveAligned,0,4);
            _loadWave.Click += async(s,e) => await LoadWaveformAsync(); zoomIn.Click += (s,e) => _waveAligned.ZoomIn(); zoomOut.Click += (s,e) => _waveAligned.ZoomOut();
            reset.Click += (s,e) => {if (_operation != null || _grid.CurrentRow == null) return; var i = _grid.CurrentRow.Index; var p = _source.Paragraphs[_indices[i]]; ApplyWaveformEdit(i,p.StartTime.TotalSeconds,p.EndTime.TotalSeconds);};
            listen.Click += (s,e) => {if (_grid.CurrentRow != null && TrySeconds(Convert.ToString(_grid.CurrentRow.Cells[3].Value),out var at)) _preview?.Invoke(at);};
            _waveAligned.OnTimeChanged += (s,e) => {if (_operation == null && _waveProposals != null) {var index = _waveProposals.Paragraphs.IndexOf(e.Paragraph); if (index >= 0) ApplyWaveformEdit(index,e.Paragraph.StartTime.TotalSeconds,e.Paragraph.EndTime.TotalSeconds);}};
            _waveAligned.OnStartTimeChanged += (s,e) => {if (_operation == null && _waveProposals != null) {var index = _waveProposals.Paragraphs.IndexOf(e.Paragraph); if (index >= 0) ApplyWaveformEdit(index,e.Paragraph.StartTime.TotalSeconds,e.Paragraph.EndTime.TotalSeconds);}};
            _waveOriginal.OnZoomedChanged += (s,e) => MirrorWave(_waveOriginal,_waveAligned); _waveAligned.OnZoomedChanged += (s,e) => MirrorWave(_waveAligned,_waveOriginal);
            _waveOriginal.OnPositionSelected += (s,e) => {_waveOriginal.StartPositionSeconds = Math.Max(0,e.Seconds); MirrorWave(_waveOriginal,_waveAligned); RefreshWaveform();}; _waveAligned.OnPositionSelected += (s,e) => {MirrorWave(_waveAligned,_waveOriginal); RefreshWaveform();};
            _waveAligned.MouseWheel += (s,e) => {MirrorWave(_waveAligned,_waveOriginal); RefreshWaveform();};
            _waveOriginal.OnSingleClick += (s,e) => _preview?.Invoke(e.Seconds); _waveAligned.OnSingleClick += (s,e) => _preview?.Invoke(e.Seconds);
            return area;
        }
        private async Task LoadWaveformAsync()
        {
            if (_waveOperation != null || _waveOriginal.WavePeaks != null || !File.Exists(_video)) return;
            _waveOperation = new CancellationTokenSource(); var cancel = _waveOperation.Token; _loadWave.Enabled = false; _waveStatus.Text = L("Загрузка…", "Loading…");
            var folder = Path.Combine(Path.GetTempPath(),"se-timing-wave-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try
            {
                var wave = Path.Combine(folder,"source.wav");
                await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation,"-nostdin -y -i "+ClassicSpeechRunner.Quote(_video)+" -map 0:a:"+Math.Max(0,_audioTrack)+" -vn -af aresample=async=1:first_pts=0 -ar 16000 -ac 1 -c:a pcm_s16le "+ClassicSpeechRunner.Quote(wave)),cancel,null);
                var peaks = await Task.Run(() => {cancel.ThrowIfCancellationRequested(); using (var generator = new WavePeakGenerator(wave)) return generator.GeneratePeaks(0,Path.Combine(folder,"peaks.dat"));},cancel);
                cancel.ThrowIfCancellationRequested(); _waveOriginal.WavePeaks = peaks; _waveAligned.WavePeaks = peaks; RefreshWaveform(); FocusWaveformRow(); _waveStatus.Text = L("Границы можно двигать мышью", "Drag cue boundaries");
            }
            catch (OperationCanceledException) {_waveStatus.Text = L("Отменено", "Cancelled");}
            catch (Exception ex) {_waveStatus.Text = ex.Message;}
            finally {if (Directory.Exists(folder)) Directory.Delete(folder,true); _waveOperation.Dispose(); _waveOperation = null; _loadWave.Enabled = true; if (_waveCloseRequested && _operation == null) Close();}
        }
        private void MarkManual(int row)
        {
            if (_results == null) _results = _indices.Select(i => new SubtitleRetimer.LineResult(_source.Paragraphs[i].StartTime.TotalSeconds,_source.Paragraphs[i].EndTime.TotalSeconds,SubtitleRetimer.LineStatus.NoSpeech)).ToArray();
            _results[row] = _results[row] with {Status = SubtitleRetimer.LineStatus.AdjustedByHand}; _grid.Rows[row].Cells[0].Value = true; _grid.Rows[row].Cells[5].Value = L("Изменено вручную", "Adjusted by hand"); _apply.Enabled = true;
        }
        public bool ApplyWaveformEdit(int row, double start, double end)
        {
            if (_operation != null || row < 0 || row >= _grid.Rows.Count || double.IsNaN(start) || double.IsNaN(end) || start < 0 || end <= start || end > _seconds) {RefreshWaveform(); return false;}
            _syncWave = true;
            try {_grid.Rows[row].Cells[3].Value = Format(start); _grid.Rows[row].Cells[4].Value = Format(end); MarkManual(row); _grid.CurrentCell = _grid.Rows[row].Cells[3];}
            finally {_syncWave = false;}
            RefreshWaveform(); return true;
        }
        private void RefreshWaveform()
        {
            if (_waveOriginal.WavePeaks == null || _syncWave) return;
            if (_waveBefore == null) {_waveBefore = new Subtitle(); foreach (var index in _indices) _waveBefore.Paragraphs.Add(new Paragraph(_source.Paragraphs[index],false));}
            if (_waveProposals == null) {_waveProposals = new Subtitle(); foreach (var index in _indices) _waveProposals.Paragraphs.Add(new Paragraph(_source.Paragraphs[index],false));}
            for (var i = 0; i < _indices.Length; i++)
            {
                var p = _waveProposals.Paragraphs[i];
                if (TrySeconds(Convert.ToString(_grid.Rows[i].Cells[3].Value),out var start) && TrySeconds(Convert.ToString(_grid.Rows[i].Cells[4].Value),out var end) && start >= 0 && end > start) {p.StartTime.TotalMilliseconds = start*1000; p.EndTime.TotalMilliseconds = end*1000;}
            }
            var row = _grid.CurrentRow?.Index ?? 0;
            _waveOriginal.SetPosition(_waveOriginal.StartPositionSeconds,_waveBefore,0,row,null); _waveAligned.SetPosition(_waveAligned.StartPositionSeconds,_waveProposals,0,row,null);
        }
        private void FocusWaveformRow()
        {
            if (_waveAligned.WavePeaks == null || _grid.CurrentRow == null || _syncWave) return;
            RefreshWaveform(); var i = _grid.CurrentRow.Index; var p = _waveProposals.Paragraphs[i]; var widthSeconds = Math.Max(6,p.Duration.TotalSeconds+4);
            _waveAligned.ZoomFactor = _waveAligned.Width/(_waveAligned.WavePeaks.SampleRate*widthSeconds); _waveAligned.StartPositionSeconds = Math.Max(0,p.StartTime.TotalSeconds-2); MirrorWave(_waveAligned,_waveOriginal); RefreshWaveform();
        }
        private void MirrorWave(AudioVisualizer from, AudioVisualizer to)
        {
            if (_syncWave) return; _syncWave = true;
            try {to.ZoomFactor = from.ZoomFactor; to.StartPositionSeconds = from.StartPositionSeconds; to.Invalidate();}
            finally {_syncWave = false;}
        }
    }
}
