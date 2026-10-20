using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic.VideoPlayers
{
    // A CLI-backed Windows FFmpeg player. Native Classic subtitle overlay stays in
    // VideoPlayerContainer; no libmpv/VLC installation or Avalonia dependency is needed.
    public sealed class ClassicFfmpegPlayer : VideoPlayer
    {
        private Control _owner; private PictureBox _picture; private CancellationTokenSource _decode;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        public string LastError { get; private set; }
        public double PresentedPosition { get; private set; }
        public long AudioBytesDelivered { get; private set; }
        private readonly Stopwatch _clock = new Stopwatch(); private double _position, _duration, _rate = 1;
        private bool _playing, _disposed; private int _serial, _width = 640, _height = 360, _volume = 75, _audioTrack;
        private WaveOutEvent _sound; private double[] _frames = new double[0];
        public static bool PreferHardware { get; set; } = true;
        public bool HardwareDecoding { get; set; } = PreferHardware;
        public List<ClassicMediaStream> AudioTracks { get; private set; } = new List<ClassicMediaStream>();
        public int AudioTrackNumber { get => _audioTrack; set { if (value < 0 || value >= AudioTracks.Count) return; _position = CurrentPosition; _audioTrack = value; Restart(); } }
        public override string PlayerName => "FFmpeg";
        public override int Volume { get => _volume; set { _volume = Math.Max(0, Math.Min(100, value)); try { if (_sound != null) _sound.Volume = _volume / 100f; } catch (ObjectDisposedException) { } } }
        public override double Duration => _duration;
        public override double CurrentPosition { get => Math.Min(_duration, _position + (_playing ? _clock.Elapsed.TotalSeconds * _rate : 0)); set { _position = Math.Max(0, Math.Min(_duration, value)); _clock.Restart(); Restart(); } }
        public override double PlayRate { get => _rate; set { if (value < .5 || value > 2 || double.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value)); _position = CurrentPosition; _rate = value; Restart(); } }
        public override bool IsPaused => !_playing;
        public override bool IsPlaying => _playing;
        public override event EventHandler OnVideoLoaded;
        public override event EventHandler OnVideoEnded;
        private static string N(double v) => v.ToString("0.########", CultureInfo.InvariantCulture);
        public override async void Initialize(Control ownerControl, string file, EventHandler loaded, EventHandler ended)
        {
            _owner = ownerControl; VideoFileName = file; OnVideoLoaded += loaded; OnVideoEnded += ended;
            _picture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black }; _owner.Controls.Add(_picture); _picture.SendToBack();
            var token = _lifetime.Token;
            try
            {
                var info = await ClassicMediaJobs.Probe(file, token); if (_disposed) return;
                _duration = info.Seconds; AudioTracks = info.Streams.Where(s => s.Type == "audio").ToList();
                if (info.Width > 0) { _width = Math.Min(1280, info.Width); _width -= _width % 2; _height = Math.Max(2, (int)Math.Round((double)info.Height * _width / info.Width / 2) * 2); }
                OnVideoLoaded?.Invoke(this, EventArgs.Empty); Restart();
                var probe = Path.Combine(Path.GetDirectoryName(Configuration.Settings.General.FFmpegLocation), "ffprobe.exe");
                // Presentation frame timestamps, including VFR and reordered B-frames.
                var timestamps = await ClassicMediaJobs.Capture(probe, "-v error -select_streams v:0 -show_entries frame=best_effort_timestamp_time -of csv=p=0 " + ClassicMediaJobs.Q(file), token);
                _frames = timestamps.Split('\n').Select(s => double.TryParse(s.Trim().Split(',')[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : -1).Where(n => n >= 0).Distinct().OrderBy(n => n).ToArray();
                if (!_disposed && !_playing) Restart();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!_disposed) { LastError = ex.Message; SeLogger.Error(ex, "FFmpeg player initialization"); } }
        }
        public void StepFrame(int direction)
        {
            Pause(); double now = CurrentPosition;
            if (_frames.Length == 0) { CurrentPosition = now + (direction >= 0 ? 1 : -1) / Math.Max(1, Configuration.Settings.General.CurrentFrameRate); return; }
            var next = direction >= 0 ? _frames.FirstOrDefault(t => t > now + .0001) : _frames.LastOrDefault(t => t < now - .0001);
            CurrentPosition = direction >= 0 && next == 0 ? _duration : next;
        }
        public override void Play() { if (_disposed || _playing) return; _playing = true; _clock.Restart(); Restart(); }
        public override void Pause() { if (_playing) _position = CurrentPosition; _playing = false; _clock.Reset(); Restart(); }
        public override void Stop() { _playing = false; CurrentPosition = 0; }
        private void Restart()
        {
            if (_disposed || _picture == null || _duration <= 0) return;
            _decode?.Cancel(); _decode?.Dispose(); _decode = new CancellationTokenSource(); try { _sound?.Stop(); } catch (ObjectDisposedException) { } _sound = null;
            int serial = ++_serial; var token = _decode.Token; double at = _position; bool play = _playing; _clock.Restart();
            _ = DecodeVideo(at, play, serial, token);
            if (play && AudioTracks.Count > 0) _ = DecodeAudio(at, serial, token);
        }
        private Process Start(string args, Action<string> error = null)
        {
            var p = new Process { StartInfo = new ProcessStartInfo(Configuration.Settings.General.FFmpegLocation, "-nostdin -v error " + args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }, EnableRaisingEvents = true };
            p.Start(); p.ErrorDataReceived += (s,e) => { if (!string.IsNullOrEmpty(e.Data)) { SeLogger.Error(e.Data); error?.Invoke(e.Data); } }; p.BeginErrorReadLine(); return p;
        }
        private static async Task<bool> ReadFrame(Stream stream, byte[] bytes, CancellationToken token)
        { int offset = 0; while (offset < bytes.Length) { int read = await stream.ReadAsync(bytes, offset, bytes.Length - offset, token).ConfigureAwait(false); if (read == 0) return false; offset += read; } return true; }
        private async Task DecodeVideo(double at, bool play, int serial, CancellationToken token, bool softwareFallback = false)
        {
            try
            {
                string decodeError = "";
                using (var process = Start((HardwareDecoding && !softwareFallback ? "-hwaccel auto " : "") + "-ss " + N(at) + " -i " + ClassicMediaJobs.Q(VideoFileName) + " -an -sn -vf scale=" + _width + ":" + _height + " -pix_fmt bgr24 -fps_mode passthrough " + (play ? "" : "-frames:v 1 ") + "-f rawvideo pipe:1", message => decodeError = message))
                using (token.Register(() => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } }))
                {
                    int frame = Array.FindIndex(_frames, t => t >= at - .0001); if (frame < 0) frame = 0;
                    var bytes = new byte[_width * _height * 3];
                    while (await ReadFrame(process.StandardOutput.BaseStream, bytes, token).ConfigureAwait(false))
                    {
                        double timestamp = frame < _frames.Length ? _frames[frame] : at + frame / Math.Max(1, Configuration.Settings.General.CurrentFrameRate); frame++;
                        if (play) { double wait = (timestamp - at) / _rate - _clock.Elapsed.TotalSeconds; if (wait > 0) await Task.Delay(TimeSpan.FromSeconds(wait), token).ConfigureAwait(false); }
                        token.ThrowIfCancellationRequested();
                        var bitmap = new Bitmap(_width, _height, PixelFormat.Format24bppRgb); var data = bitmap.LockBits(new Rectangle(0, 0, _width, _height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
                        try { for (int y = 0; y < _height; y++) Marshal.Copy(bytes, y * _width * 3, IntPtr.Add(data.Scan0, y * data.Stride), _width * 3); } finally { bitmap.UnlockBits(data); }
                        await Present(bitmap, serial, timestamp, token).ConfigureAwait(false);
                        if (!play) break;
                    }
                    if (!token.IsCancellationRequested && serial == _serial)
                    {
                        await Task.Run(() => process.WaitForExit(), token).ConfigureAwait(false);
                        if (process.ExitCode != 0)
                        {
                            if (HardwareDecoding && !softwareFallback) { await DecodeVideo(at, play, serial, token, true).ConfigureAwait(false); return; }
                            throw new InvalidOperationException(string.IsNullOrWhiteSpace(decodeError) ? "FFmpeg could not decode the video." : decodeError);
                        }
                    }
                    if (play && !token.IsCancellationRequested && serial == _serial) _owner.BeginInvoke(new Action(() => { if (serial != _serial || _disposed) return; _position = _duration; _playing = false; _clock.Reset(); _sound?.Stop(); OnVideoEnded?.Invoke(this, EventArgs.Empty); }));
                }
            }
            catch (OperationCanceledException) { } catch (IOException) when (token.IsCancellationRequested) { } catch (Exception ex) { if (!token.IsCancellationRequested && !_disposed) { LastError = ex.Message; SeLogger.Error(ex, "FFmpeg video decoding"); } }
        }
        private async Task Present(Bitmap image, int serial, double timestamp, CancellationToken token)
        {
            var done = new TaskCompletionSource<bool>(); Bitmap pending = image;
            try { _owner.BeginInvoke(new Action(() => { var frame = Interlocked.Exchange(ref pending, null); try { if (frame == null) return; if (_disposed || serial != _serial) frame.Dispose(); else { PresentedPosition = timestamp; var old = _picture.Image; _picture.Image = frame; old?.Dispose(); } } finally { done.TrySetResult(true); } })); }
            catch (InvalidOperationException) { Interlocked.Exchange(ref pending, null)?.Dispose(); done.TrySetResult(false); }
            using (token.Register(() => { Interlocked.Exchange(ref pending, null)?.Dispose(); done.TrySetCanceled(); })) await done.Task.ConfigureAwait(false);
        }

        private async Task DecodeAudio(double at, int serial, CancellationToken token)
        {
            try
            {
                using (var process = Start("-ss " + N(at) + " -i " + ClassicMediaJobs.Q(VideoFileName) + " -map 0:a:" + _audioTrack + " -vn -sn -af atempo=" + N(_rate) + " -ar 48000 -ac 2 -f s16le pipe:1"))
                using (token.Register(() => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } }))
                using (var sound = new WaveOutEvent { DesiredLatency = 100 })
                {
                    var buffer = new BufferedWaveProvider(new WaveFormat(48000, 16, 2)) { BufferDuration = TimeSpan.FromSeconds(2) }; sound.Init(buffer); sound.Volume = _volume / 100f;
                    if (serial != _serial) return; _sound = sound;
                    var bytes = new byte[16384]; bool started = false; int read;
                    while ((read = await process.StandardOutput.BaseStream.ReadAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false)) > 0)
                    {
                        while (buffer.BufferedBytes > buffer.BufferLength - bytes.Length) await Task.Delay(15, token).ConfigureAwait(false);
                        buffer.AddSamples(bytes, 0, read); AudioBytesDelivered += read; if (!started) { sound.Play(); started = true; }
                    }
                    while (buffer.BufferedBytes > 0 && serial == _serial) await Task.Delay(20, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { } catch (IOException) when (token.IsCancellationRequested) { } catch (Exception ex) { if (!token.IsCancellationRequested) { LastError = ex.Message; SeLogger.Error(ex, "FFmpeg audio decoding"); } }
            finally { if (serial == _serial) _sound = null; }
        }
        public override void DisposeVideoPlayer()
        {
            if (_disposed) return; _disposed = true; _playing = false; _serial++; _lifetime.Cancel(); _decode?.Cancel(); try { _sound?.Stop(); } catch (ObjectDisposedException) { }
            if (_picture != null) { _picture.Image?.Dispose(); _picture.Dispose(); _picture = null; }
        }
    }
}
