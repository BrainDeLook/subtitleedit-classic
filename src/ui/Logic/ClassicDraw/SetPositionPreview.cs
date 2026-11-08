using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Logic.ClassicDraw
{
    /// <summary>A paused source frame is decoded once; libass renders every edited position over that same frame.</summary>
    public sealed class SetPositionPreview : IDisposable
    {
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly SemaphoreSlim _renderGate = new SemaphoreSlim(1, 1);
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "se-assa-position-" + Guid.NewGuid().ToString("N"));
        private readonly Task _background;
        private readonly string _ffmpeg;
        private bool _disposed;

        public SetPositionPreview(Subtitle subtitle, string video, double seconds, int width, int height, Bitmap pausedFrame)
        {
            _ffmpeg = Configuration.Settings.General.FFmpegLocation;
            _background = Initialize(subtitle, video, seconds, width, height, pausedFrame);
        }

        private async Task Initialize(Subtitle subtitle, string video, double seconds, int width, int height, Bitmap pausedFrame)
        {
            Directory.CreateDirectory(_directory);
            if (pausedFrame != null)
            {
                await Task.Run(() => { using (pausedFrame) pausedFrame.Save(Path.Combine(_directory, "background.png"), System.Drawing.Imaging.ImageFormat.Png); }).ConfigureAwait(false);
            }
            else
            {
                var scaleWidth = Math.Min(1280, width);
                var scaleHeight = Math.Max(2, (int)Math.Round(height * scaleWidth / (double)width));
                var input = File.Exists(video)
                    ? "-ss " + Math.Max(0, seconds).ToString("0.######", CultureInfo.InvariantCulture) + " -i " + ClassicMediaJobs.Q(video)
                    : "-f lavfi -i color=c=black:s=" + scaleWidth + "x" + scaleHeight;
                await Run(input + " -vf scale=" + scaleWidth + ":" + scaleHeight + " -frames:v 1 background.png", _lifetime.Token).ConfigureAwait(false);
            }
            var fonts = await Task.Run(() => ClassicFontCollection.Scan(subtitle, _lifetime.Token), _lifetime.Token).ConfigureAwait(false);
            var embedded = fonts.Where(f => f.Embedded).GroupBy(f => f.Path).Select(g => g.First()).ToArray();
            if (embedded.Length > 0)
            {
                Directory.CreateDirectory(Path.Combine(_directory, "fonts"));
                for (var i = 0; i < embedded.Length; i++) File.WriteAllBytes(Path.Combine(_directory, "fonts", i + ".ttf"), embedded[i].Read());
            }
        }

        private Task Run(string args, CancellationToken token) => ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(_ffmpeg, "-nostdin -hide_banner -loglevel error -y " + args) { WorkingDirectory = _directory }, token, null);

        public async Task<Bitmap> Render(Subtitle subtitle, CancellationToken token)
        {
            await _background.ConfigureAwait(false);
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token))
            {
                await _renderGate.WaitAsync(linked.Token).ConfigureAwait(false);
                try
                {
                    linked.Token.ThrowIfCancellationRequested();
                    File.WriteAllText(Path.Combine(_directory, "preview.ass"), new AdvancedSubStationAlpha().ToText(subtitle, "Set position"));
                    var filter = Directory.Exists(Path.Combine(_directory, "fonts")) ? "ass=preview.ass:fontsdir=fonts" : "ass=preview.ass";
                    await Run("-i background.png -vf " + filter + " -frames:v 1 preview.png", linked.Token).ConfigureAwait(false);
                    using (var image = Image.FromFile(Path.Combine(_directory, "preview.png"))) return new Bitmap(image);
                }
                finally { _renderGate.Release(); }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lifetime.Cancel();
            // Cleanup follows outstanding decoding/rendering; closing a window never blocks on an encoder.
            _ = Cleanup();
        }

        private async Task Cleanup()
        {
            try { await _background.ConfigureAwait(false); }
            catch (Exception ex) when (ex is OperationCanceledException || ex is IOException || ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception) { SeLogger.Error(ex, "Set position background cleanup"); }
            await _renderGate.WaitAsync().ConfigureAwait(false);
            try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { SeLogger.Error(ex, "Set position preview cleanup"); }
            finally { _renderGate.Release(); _renderGate.Dispose(); _lifetime.Dispose(); }
        }
    }
}
