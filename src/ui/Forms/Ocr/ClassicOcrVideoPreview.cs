using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms.AudioToText;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.VideoPlayers;

namespace Nikse.SubtitleEdit.Forms.Ocr
{
    public sealed class ClassicOcrVideoPreview : Form
    {
        private readonly VideoPlayerContainer _player = new VideoPlayerContainer { Dock = DockStyle.Fill, Name = "ocrVideoPlayer" };
        private readonly Button _play = new Button { AutoSize = true, Enabled = false, Name = "playOcrCue" };
        private readonly Label _cueText = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, Padding = new Padding(6), Name = "ocrPreviewCue" };
        private readonly Label _file = new Label { AutoSize = true, AutoEllipsis = true, MaximumSize = new Size(500, 40) };
        private readonly Timer _timer = new Timer { Interval = 100 };
        private Paragraph _cue;
        private bool _playingCue;
        public string VideoFileName { get; private set; }
        public int AudioTrackNumber { get; set; } = -1;
        public VideoPlayerContainer Player => _player;
        public ClassicOcrVideoPreview(string videoFileName)
        {
            UiUtil.PreInitialize(this);
            _player.InitializeVolume(Configuration.Settings.General.VideoPlayerDefaultVolume);
            _player.TryLoadGfx();
            VideoFileName = videoFileName;
            Text = L("OCR — просмотр видео", "OCR — video preview");
            ClientSize = new Size(720, 480); MinimumSize = new Size(500, 360);
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(4) };
            var open = new Button { AutoSize = true, Text = L("Открыть видео…", "Open video…"), Name = "openOcrPreviewVideo" };
            _play.Text = L("Проиграть реплику", "Play cue");
            actions.Controls.AddRange(new Control[] { open, _play, _file });
            layout.Controls.Add(actions, 0, 0); layout.Controls.Add(_player, 0, 1); layout.Controls.Add(_cueText, 0, 2); Controls.Add(layout);
            open.Click += (sender, e) =>
            {
                using (var picker = new OpenFileDialog { Filter = UiUtil.GetVideoFileFilter(true), Title = open.Text })
                    if (picker.ShowDialog(this) == DialogResult.OK) OpenVideo(picker.FileName);
            };
            _play.Click += (sender, e) => PlayCue();
            _timer.Tick += (sender, e) =>
            {
                _player.RefreshProgressBar();
                if (_playingCue && _cue != null && _player.CurrentPosition >= _cue.EndTime.TotalSeconds)
                {
                    _playingCue = false;
                    _player.Pause();
                }
            };
            Shown += (sender, e) => { if (File.Exists(VideoFileName)) OpenVideo(VideoFileName); };
            UiUtil.FixFonts(this);
        }
        public void OpenVideo(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The preview video was not found.", path);
            _timer.Stop(); _playingCue = false; _play.Enabled = false;
            if (_player.VideoPlayer != null) _player.PauseAndDisposePlayer();
            if (!string.Equals(VideoFileName, path, StringComparison.OrdinalIgnoreCase)) AudioTrackNumber = -1;
            VideoFileName = path; _file.Text = Path.GetFileName(path);
            var info = UiUtil.GetVideoInfo(path);
            UiUtil.InitializeVideoPlayerAndContainer(path, info, _player, (sender, e) =>
            {
                if (IsDisposed) return;
                _player.Pause();
                if (AudioTrackNumber >= 0)
                {
                    if (_player.VideoPlayer is LibMpvDynamic mpv) mpv.AudioTrackNumber = AudioTrackNumber;
                    else if (_player.VideoPlayer is LibVlcDynamic vlc) vlc.AudioTrackNumber = AudioTrackNumber;
                    else if (_player.VideoPlayer is ClassicFfmpegPlayer ffmpeg) ffmpeg.AudioTrackNumber = AudioTrackNumber;
                }
                if (_cue != null) _player.CurrentPosition = _cue.StartTime.TotalSeconds;
                _play.Enabled = _cue != null;
                _timer.Start();
            }, (sender, e) => { _playingCue = false; _player.Pause(); });
        }
        public void SelectCue(Paragraph cue)
        {
            _cue = new Paragraph(cue);
            _playingCue = false;
            _cueText.Text = cue.StartTime + " → " + cue.EndTime + Environment.NewLine + HtmlUtil.RemoveHtmlTags(cue.Text, true);
            if (_player.VideoPlayer != null)
            {
                _player.Pause(); _player.CurrentPosition = cue.StartTime.TotalSeconds; _play.Enabled = true;
            }
        }
        public void PlayCue()
        {
            if (_cue == null || _player.VideoPlayer == null) return;
            _player.CurrentPosition = _cue.StartTime.TotalSeconds;
            _playingCue = true; _player.Play();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop(); _timer.Dispose();
                if (_player.VideoPlayer != null) _player.PauseAndDisposePlayer();
            }
            base.Dispose(disposing);
        }
        private static string L(string ru, string en) => ClassicSpeechSettings.L(ru, en);
    }
}
