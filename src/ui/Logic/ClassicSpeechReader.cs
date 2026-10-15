using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.VideoPlayers;
namespace Nikse.SubtitleEdit.Logic
{
    public sealed class ClassicSpeechLine
    {
        public Paragraph Paragraph {get;set;}
        public int Index {get;set;}
        public string Text=>Paragraph.Text;
        public double Start=>Paragraph.StartTime.TotalSeconds;
        public double End=>Paragraph.EndTime.TotalSeconds;
    }
    public sealed class ClassicSpeechVideoAdapter : IClassicSpeechVideo
    {
        public Func<bool> Open, Playing;
        public Func<double> GetPosition, GetSpeed, GetVolume;
        public Action<double> SetVolume;
        public Action Start, Hold;
        public bool HasVideo=>Open();public bool IsPlaying=>Playing();public double Position=>GetPosition();public double Speed=>GetSpeed();
        public double Volume{get=>GetVolume();set=>SetVolume(value);}
        public void Play()=>Start();public void Pause()=>Hold();
    }
    public interface IClassicSpeechAudio : IDisposable
    {
        double Duration{get;}double Position{get;}bool IsPaused{get;}double Speed{set;}
        Task LoadAudio(string path);void Play();void Pause();
    }
    internal sealed class ClassicSpeechPlayer : IClassicSpeechAudio
    {
        private LibMpvDynamic _player;
        private readonly int _volume;
        public ClassicSpeechPlayer(int volume=100){_volume=volume;}
        private bool _ended;
        public double Duration=>_player?.Duration ?? 0;
        public double Position=>_player?.CurrentPosition ?? 0;
        public bool IsPaused=>_ended || (_player?.IsPaused ?? true);
        public double Speed{set{if(_player!=null)_player.PlayRate=value;}}
        public async Task LoadAudio(string path)
        {
            Dispose();
            _ended = false;
            _player = new LibMpvDynamic();
            var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            // LibMpvDynamic pauses from its file-loaded timer. Wait for that callback before
            // allowing Play, otherwise the delayed pause immediately stops the speech.
            _player.Initialize(null, path, (s, e) => loaded.TrySetResult(true), (s, e) => _ended = true);
            if (await Task.WhenAny(loaded.Task, Task.Delay(5000)) != loaded.Task)
            {
                throw new TimeoutException("Speech audio did not finish loading: " + path);
            }
            await loaded.Task;
            if (_player.Duration <= 0)
            {
                throw new InvalidDataException("Speech audio has no playable duration: " + path);
            }
            _player.Volume = _volume;
        }
        public void Play()=>_player?.Play();
        public void Pause()=>_player?.Pause();
        public void Dispose(){_player?.DisposeVideoPlayer();_player=null;}
    }
    internal sealed class ClassicPlaybackStallDetector
    {
        private readonly int _timeout;private double _last=-1;private long _when;
        public ClassicPlaybackStallDetector(int timeout){_timeout=timeout;}
        public bool IsStalled(double position,long now){if(_last<0 || Math.Abs(position-_last)>0.02){_last=position;_when=now;}return now-_when>_timeout;}
    }
/// <summary>The video that "Play with speech" follows.</summary>
public interface IClassicSpeechVideo
{
    bool HasVideo { get; }
    double Position { get; }
    bool IsPlaying { get; }
    double Speed { get; }
    double Volume { get; set; }
    void Play();
    void Pause();
}

/// <summary>
/// Speaks the lines while the video plays: each line is spoken when the video reaches its start,
/// like a live dub. Clips are generated a few lines ahead. A clip longer than its line is sped
/// up (up to <see cref="MaxSpeedUp"/>); if it still runs into the next line, the video waits
/// for it. Pausing the video pauses the speech, and seeking restarts from the new position.
/// </summary>
public sealed class ClassicSpeechReader
{
    public const double MaxSpeedUp = 1.5;

    private const int TickMilliseconds = 40;
    private const int LookAhead = 3;
    private const double DuckFactor = 0.3;

    // A line whose start the video passed less than this ago is still spoken - mainly so the
    // line the video is parked on when playing starts is not skipped.
    internal const double StartTolerance = 0.3;

    private readonly Func<ClassicSpeechLine,string,CancellationToken,Task<string>> _generate;
    private readonly Func<IClassicSpeechAudio> _audioFactory;
    private readonly bool _lowerVideoVolume;
    private readonly bool _pauseVideoWhenLate;

    public ClassicSpeechReader(Func<ClassicSpeechLine,string,CancellationToken,Task<string>> generate, bool lowerVideoVolume=true, bool pauseVideoWhenLate=true, Func<IClassicSpeechAudio> audioFactory=null)
    {
        _audioFactory=audioFactory ?? (()=>new ClassicSpeechPlayer());_generate=generate;_lowerVideoVolume=lowerVideoVolume;_pauseVideoWhenLate=pauseVideoWhenLate;
    }
    /// <summary>
    /// Plays the video with speech until <paramref name="cancellationToken"/> is cancelled or the
    /// video is closed. <paramref name="lines"/> must be sorted by start time.
    /// </summary>
    /// <param name="onLineStarting">Called when line i starts to be spoken.</param>
    /// <param name="onWaiting">Called when the video is held for line i's clip to be generated.</param>
    public async Task RunAsync(IReadOnlyList<ClassicSpeechLine> lines, IClassicSpeechVideo video, Action<int> onLineStarting, Action<int> onWaiting, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(Path.GetTempPath(), "se-speak-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        IClassicSpeechAudio player = null;
        var clips = new Dictionary<int, Task<string>>();
        var allClips = new List<Task<string>>();
        var generationGate = new SemaphoreSlim(1, 1);
        var generationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var speaking = -1;
        var slot = 0.0;
        var speechRate = 1.0;
        var rateKnown = false;
        var appliedPlayerSpeed = -1.0;
        var speechHeld = false;
        var videoPausedByUs = false;
        var ducked = false;
        var volumeBeforeDuck = 0.0;
        long clipPlayStartedMs = 0;
        var playRequestedMs = long.MinValue / 2;
        var stopwatch = Stopwatch.StartNew();
        var stallDetector = new ClassicPlaybackStallDetector(10000);

        void Duck()
        {
            if (_lowerVideoVolume && !ducked)
            {
                volumeBeforeDuck = video.Volume;
                video.Volume = volumeBeforeDuck * DuckFactor;
                ducked = true;
            }
        }

        void Unduck()
        {
            if (ducked)
            {
                video.Volume = volumeBeforeDuck;
                ducked = false;
            }
        }

        // The player takes a moment to report playing after Play - count it as playing meanwhile,
        // or that moment would look like the user pausing.
        void PlayVideo()
        {
            video.Play();
            playRequestedMs = stopwatch.ElapsedMilliseconds;
        }

        void StopClip()
        {
            if (speaking >= 0)
            {
                player?.Pause();
                speaking = -1;
            }

            speechHeld = false;
            Unduck();
        }

        void EnsureGenerated(int from)
        {
            for (var i = from; i < lines.Count && i < from + LookAhead; i++)
            {
                if (!clips.ContainsKey(i))
                {
                    var task = GenerateQueuedAsync(lines[i], folder, generationGate, generationCts.Token);
                    clips[i] = task;
                    allClips.Add(task);
                }
            }
        }

        try
        {
            player = _audioFactory();
            var next = FindNextLine(lines, video.Position);
            EnsureGenerated(next);
            var lastPosition = video.Position;
            var lastTickMs = stopwatch.ElapsedMilliseconds;
            PlayVideo();

            while (true)
            {
                await Task.Delay(TickMilliseconds, cancellationToken);
                if (!video.HasVideo)
                {
                    return;
                }

                var nowMs = stopwatch.ElapsedMilliseconds;
                var elapsedSeconds = (nowMs - lastTickMs) / 1000.0;
                lastTickMs = nowMs;
                var position = video.Position;
                var isPlaying = video.IsPlaying || nowMs - playRequestedMs < 500;
                var videoSpeed = video.Speed > 0 ? video.Speed : 1.0;

                if (IsSeek(lastPosition, position, elapsedSeconds, isPlaying, videoSpeed))
                {
                    StopClip();
                    if (videoPausedByUs)
                    {
                        videoPausedByUs = false;
                        PlayVideo();
                    }

                    // Clips queued for the old position are no longer needed.
                    generationCts.Cancel();
                    generationCts.Dispose();
                    generationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    clips.Clear();
                    next = FindNextLine(lines, position);
                }

                lastPosition = position;
                EnsureGenerated(next);

                if (speaking >= 0)
                {
                    // The user paused the video: hold the speech with it.
                    if (!isPlaying && !videoPausedByUs)
                    {
                        if (!speechHeld)
                        {
                            player.Pause();
                            speechHeld = true;
                        }

                        continue;
                    }

                    if (speechHeld)
                    {
                        speechHeld = false;
                        player.Play();
                        clipPlayStartedMs = nowMs;
                        stallDetector = new ClassicPlaybackStallDetector(10000);
                    }

                    if (!rateKnown && player.Duration > 0)
                    {
                        speechRate = GetSpeechRate(player.Duration, slot, MaxSpeedUp);
                        rateKnown = true;
                    }

                    var playerSpeed = speechRate * videoSpeed;
                    if (Math.Abs(playerSpeed - appliedPlayerSpeed) > 0.001)
                    {
                        player.Speed = playerSpeed;
                        appliedPlayerSpeed = playerSpeed;
                    }

                    // mpv pauses itself at the clip's end ("keep-open"); the grace period skips the
                    // moment right after loading or resuming, before it reports playing.
                    var finished = nowMs - clipPlayStartedMs > 300 && player.IsPaused;
                    if (!finished && stallDetector.IsStalled(player.Position, nowMs))
                    {
                        throw new InvalidOperationException("Speech playback stalled at line " + (speaking+1));
                    }

                    if (finished)
                    {
                        speaking = -1;
                        Unduck();
                        if (videoPausedByUs)
                        {
                            videoPausedByUs = false;
                            PlayVideo();
                            continue; // let the video move before deciding on the next line
                        }
                    }
                    else
                    {
                        if (_pauseVideoWhenLate && isPlaying && !videoPausedByUs && next < lines.Count && position >= lines[next].Start)
                        {
                            video.Pause();
                            videoPausedByUs = true;
                        }

                        continue;
                    }
                }

                if(next>=lines.Count && speaking<0)return;
                if (next >= lines.Count || (!isPlaying && !videoPausedByUs))
                {
                    continue;
                }

                var line = lines[next];
                if (position < line.Start)
                {
                    continue;
                }

                if (!_pauseVideoWhenLate && position > line.End)
                {
                    next++; // the video is already past this line
                    continue;
                }

                var clip = clips[next];
                if (!clip.IsCompleted)
                {
                    if (isPlaying && !videoPausedByUs)
                    {
                        video.Pause();
                        videoPausedByUs = true;
                        onWaiting(next);
                    }

                    continue;
                }

                var fileName = await clip; // a failed generation ends the run with its error
                cancellationToken.ThrowIfCancellationRequested();
                var index = next;
                next++;
                if (fileName == null)
                {
                    continue; // nothing to say on this line
                }

                slot = GetSlot(lines, index);
                speechRate = 1.0;
                rateKnown = false;
                await player.LoadAudio(fileName);
                appliedPlayerSpeed = videoSpeed;
                player.Speed = videoSpeed;

                // LoadAudio keeps the file open at its end, which pauses mpv - and the pause
                // carries over to the next file, so un-pause explicitly.
                player.Play();
                clipPlayStartedMs = stopwatch.ElapsedMilliseconds;
                stallDetector = new ClassicPlaybackStallDetector(10000);
                speaking = index;
                Duck();
                onLineStarting(index);

                if (videoPausedByUs)
                {
                    videoPausedByUs = false;
                    PlayVideo();
                }

                lastPosition = video.Position; // the load took a while - not a seek
                lastTickMs = stopwatch.ElapsedMilliseconds;
            }
        }
        finally
        {
            try
            {
                Unduck();
            }
            catch
            {
                // ignore - the video may be gone
            }

            if(video.HasVideo)video.Pause();
            generationCts.Cancel();
            foreach (var clip in allClips)
            {
                try
                {
                    await clip;
                }
                catch
                {
                    // ignore
                }
            }

            generationCts.Dispose();
            player?.Dispose();
            try
            {
                Directory.Delete(folder, true);
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>Generates clips one at a time, in the order they were asked for.</summary>
    private async Task<string> GenerateQueuedAsync(ClassicSpeechLine line, string folder, SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await _generate(line,folder,cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>The first line still to be spoken at <paramref name="position"/>.</summary>
    internal static int FindNextLine(IReadOnlyList<ClassicSpeechLine> lines, double position)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Start >= position - StartTolerance)
            {
                return i;
            }
        }

        return lines.Count;
    }

    /// <summary>
    /// The time line <paramref name="index"/> has to be spoken in: until its end, or until the
    /// next line starts when they overlap.
    /// </summary>
    internal static double GetSlot(IReadOnlyList<ClassicSpeechLine> lines, int index)
    {
        var line = lines[index];
        var end = line.End;
        if (index + 1 < lines.Count && lines[index + 1].Start > line.Start && lines[index + 1].Start < end)
        {
            end = lines[index + 1].Start;
        }

        return Math.Max(0.2, end - line.Start);
    }

    /// <summary>How much to speed up a clip so it fits its slot - never slower, at most <paramref name="maxSpeedUp"/>.</summary>
    internal static double GetSpeechRate(double clipDuration, double slot, double maxSpeedUp)
    {
        if (clipDuration <= slot || slot <= 0)
        {
            return 1.0;
        }

        return Math.Min(maxSpeedUp, clipDuration / slot);
    }

    /// <summary>
    /// Whether the video position jumped (the user seeked) rather than moved by playing.
    /// </summary>
    internal static bool IsSeek(double lastPosition, double position, double elapsedSeconds, bool isPlaying, double speed)
    {
        if (position < lastPosition - 0.25)
        {
            return true;
        }

        var expectedMove = isPlaying ? elapsedSeconds * speed : 0;
        return position > lastPosition + expectedMove * 1.5 + (isPlaying ? 0.75 : 0.25);
    }
}

}
