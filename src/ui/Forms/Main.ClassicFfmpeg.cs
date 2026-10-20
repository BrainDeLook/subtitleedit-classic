using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using Nikse.SubtitleEdit.Logic.VideoPlayers;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private ClassicTrackTimeline _classicTracks;
        private void InitializeClassicFfmpeg()
        {
            if (_classicTracks != null) return;
            var player = new ToolStripMenuItem(ClassicAssistedText.L("Плеер FFmpeg (экспериментальный)", "FFmpeg player (experimental)")) { Name = "classicFfmpegPlayer", CheckOnClick = true, Checked = Configuration.Settings.General.VideoPlayer == "FFmpeg" };
            player.CheckedChanged += (s,e) => { if (player.Checked && !RequireFfmpegOk()) { player.Checked = false; return; } Configuration.Settings.General.VideoPlayer = player.Checked ? "FFmpeg" : "MPV"; if (File.Exists(_videoFileName)) { var file = _videoFileName; var at = mediaPlayer.CurrentPosition; OpenVideo(file); mediaPlayer.CurrentPosition = at; } };
            var tracks = new ToolStripMenuItem(ClassicAssistedText.L("Панель дорожек", "Track timeline")) { Name = "classicTrackTimeline", CheckOnClick = true };
            _classicTracks = new ClassicTrackTimeline { Dock = DockStyle.Bottom, Visible = false };
            Controls.Add(_classicTracks); Controls.SetChildIndex(_classicTracks, Controls.GetChildIndex(splitContainerMain) + 1);
            tracks.CheckedChanged += (s,e) => _classicTracks.Visible = tracks.Checked;
            _classicTracks.SelectCue = index => SubtitleListview1.SelectIndexAndEnsureVisible(index);
            _classicTracks.Seek = at => mediaPlayer.CurrentPosition = at;
            _classicTracks.CommitTime = (index,start,end) => { if (index < 0 || index >= _subtitle.Paragraphs.Count) return; MakeHistoryForUndo("Before track timeline edit"); var p = _subtitle.Paragraphs[index]; p.StartTime.TotalMilliseconds = start * 1000; p.EndTime.TotalMilliseconds = end * 1000; SubtitleListview1.Fill(_subtitle, _subtitleOriginal); SubtitleListview1.SelectIndexAndEnsureVisible(index); RefreshSelectedParagraph(); audioVisualizer.Invalidate(); };
            var audio = new ToolStripMenuItem(ClassicAssistedText.L("Аудиодорожка FFmpeg", "FFmpeg audio track")) { Name = "classicFfmpegAudio" };
            audio.DropDownOpening += (s,e) => { audio.DropDownItems.Clear(); if (!(mediaPlayer.VideoPlayer is ClassicFfmpegPlayer ff)) return; for (int i = 0; i < ff.AudioTracks.Count; i++) { int n = i; var item = new ToolStripMenuItem(ff.AudioTracks[i].ToString()) { Checked = ff.AudioTrackNumber == i }; item.Click += (sender,args) => { ff.AudioTrackNumber = n; VideoAudioTrackNumber = n; BeginClassicWaveform(_videoFileName, n); }; audio.DropDownItems.Add(item); } };
            var hardware = new ToolStripMenuItem(ClassicAssistedText.L("FFmpeg: аппаратное декодирование", "FFmpeg hardware decoding")) { Name = "classicFfmpegHardware", CheckOnClick = true, Checked = true };
            hardware.CheckedChanged += (s,e) => { ClassicFfmpegPlayer.PreferHardware = hardware.Checked; if (mediaPlayer.VideoPlayer is ClassicFfmpegPlayer ff) { ff.HardwareDecoding = hardware.Checked; ff.CurrentPosition = ff.CurrentPosition; } };
            toolStripMenuItemVideo.DropDownItems.AddRange(new ToolStripItem[] { player, audio, hardware, tracks });
            var lastContentRefresh = Environment.TickCount;
            var timer = new Timer { Interval = 16 }; timer.Tick += (s,e) => { if (_classicTracks.Visible) { _classicTracks.Subtitle = _subtitle; _classicTracks.Position = mediaPlayer.CurrentPosition; _classicTracks.SelectedIndex = FirstSelectedIndex; if (unchecked(Environment.TickCount - lastContentRefresh) >= 200) { _classicTracks.RefreshContent(); lastContentRefresh = Environment.TickCount; } } }; timer.Start(); FormClosed += (s,e) => { timer.Stop(); timer.Dispose(); };
        }
    }
}
