using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Logic
{
    internal static class ClassicSyncMp4Reference
    {
        // Keep the classic parser's fast text path; normalize containers it cannot read.
        public static async Task<ClassicSyncReference> LoadAsync(IWin32Window owner, string file, CancellationToken cancel, int? selectedTrack)
        {
            var ffmpeg = Configuration.Settings.General.FFmpegLocation;
            if (!File.Exists(ffmpeg)) throw new FileNotFoundException("Install FFmpeg to read this MP4 subtitle track.");
            string metadata;
            using (var process = new Process { StartInfo = new ProcessStartInfo(ffmpeg, "-hide_banner -i " + ClassicSpeechRunner.Quote(file)) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardError = true, RedirectStandardOutput = true, StandardErrorEncoding = Encoding.UTF8 } })
            {
                process.Start();
                using (cancel.Register(() => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } }))
                {
                    var error = process.StandardError.ReadToEndAsync(); var output = process.StandardOutput.ReadToEndAsync();
                    await Task.WhenAll(error, output, Task.Run(() => process.WaitForExit())); cancel.ThrowIfCancellationRequested(); metadata = error.Result;
                }
            }
            var tracks = new List<(int Stream, string Label, bool Image)>();
            foreach (Match match in Regex.Matches(metadata, @"(?m)^\s*Stream #\d+:(?<id>\d+)(?:\[[^\]]*\])?(?:\((?<language>[^)]*)\))?: Subtitle:\s*(?<codec>[^,\s]+)(?<rest>[^\r\n]*)"))
            {
                var codec = match.Groups["codec"].Value;
                tracks.Add((int.Parse(match.Groups["id"].Value), "#" + match.Groups["id"].Value + " · " + match.Groups["language"].Value + " · " + codec, codec == "dvd_subtitle" || codec == "hdmv_pgs_subtitle" || codec == "dvb_subtitle"));
            }
            if (tracks.Count == 0) throw new InvalidDataException(LanguageSettings.Current.Main.NoSubtitlesFound);
            var at = selectedTrack ?? 0;
            if (!selectedTrack.HasValue && tracks.Count > 1)
            {
                using (var dialog = new Form { Text = LanguageSettings.Current.General.OpenSubtitle, Width = 570, Height = 300, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false })
                using (var list = new ListBox { Dock = DockStyle.Fill })
                using (var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft })
                {
                    var ok = new Button { Text = LanguageSettings.Current.General.Ok, DialogResult = DialogResult.OK }; var close = new Button { Text = LanguageSettings.Current.General.Cancel, DialogResult = DialogResult.Cancel };
                    foreach (var track in tracks) list.Items.Add(track.Label); list.SelectedIndex = 0;
                    footer.Controls.Add(close); footer.Controls.Add(ok); dialog.Controls.Add(list); dialog.Controls.Add(footer); dialog.AcceptButton = ok; dialog.CancelButton = close;
                    if (dialog.ShowDialog(owner) != DialogResult.OK) return null; at = list.SelectedIndex;
                }
            }
            if (at < 0 || at >= tracks.Count) throw new ArgumentOutOfRangeException(nameof(selectedTrack));
            var temp = Path.Combine(Path.GetTempPath(), "subtitleedit-sync-" + Guid.NewGuid().ToString("N") + ".mks");
            try
            {
                await ClassicSpeechRunner.RunProcessAsync(new ProcessStartInfo(ffmpeg, "-nostdin -hide_banner -y -i " + ClassicSpeechRunner.Quote(file) + " -map 0:" + tracks[at].Stream + " -c:s " + (tracks[at].Image ? "copy" : "srt") + " -f matroska " + ClassicSpeechRunner.Quote(temp)), cancel, null);
                return await ClassicSyncReference.LoadAsync(owner, temp, cancel, 0);
            }
            finally { try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }
}
