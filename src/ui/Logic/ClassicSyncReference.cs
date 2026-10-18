using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.BluRaySup;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.ContainerFormats.Matroska;
using Nikse.SubtitleEdit.Core.ContainerFormats.Mp4;
using Nikse.SubtitleEdit.Core.ContainerFormats.Mp4.Boxes;
using Nikse.SubtitleEdit.Core.ContainerFormats.TransportStream;
using Nikse.SubtitleEdit.Core.VobSub;
using Nikse.SubtitleEdit.Forms;

namespace Nikse.SubtitleEdit.Logic
{
    internal sealed class ClassicSyncReference : IDisposable
    {
        public readonly Subtitle Subtitle = new Subtitle();
        public readonly List<Func<Bitmap>> Images = new List<Func<Bitmap>>();
        private readonly List<Action> _dispose = new List<Action>();
        public void Dispose() { foreach (var dispose in _dispose) dispose(); _dispose.Clear(); Images.Clear(); }
        public bool IsImageBased => Images.Count > 0;
        public static string Filter => UiUtil.SubtitleExtensionFilter.Value + "|Subtitle containers and images|*.mkv;*.mks;*.mp4;*.m4v;*.mov;*.sup";
        private void AddImage(double start, double end, Func<Bitmap> image)
        {
            Subtitle.Paragraphs.Add(new Paragraph(string.Empty, start, end) { Number = Subtitle.Paragraphs.Count + 1 }); Images.Add(image);
        }
        public static async Task<ClassicSyncReference> LoadAsync(IWin32Window owner, string file, CancellationToken cancel, int? selectedTrack = null)
        {
            cancel.ThrowIfCancellationRequested();
            if (FileUtil.IsBluRaySup(file)) return await Task.Run(() => LoadSup(file, cancel), cancel);
            if (FileUtil.IsMatroskaFileFast(file))
            {
                using (var mkv = new MatroskaFile(file))
                {
                    var tracks = await Task.Run(() => mkv.GetTracks(true), cancel);
                    if (tracks.Count == 0) throw new InvalidDataException(LanguageSettings.Current.Main.NoSubtitlesFound);
                    var at = selectedTrack ?? 0;
                    if (!selectedTrack.HasValue && tracks.Count > 1) using (var chooser = new MatroskaSubtitleChooser("mkv")) { chooser.Initialize(tracks); if (chooser.ShowDialog(owner) != DialogResult.OK) return null; at = chooser.SelectedIndex; }
                    if (at < 0 || at >= tracks.Count) throw new ArgumentOutOfRangeException(nameof(selectedTrack));
                    return await Task.Run(() => LoadMatroska(mkv, tracks[at], cancel), cancel);
                }
            }
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (new[] { ".mp4", ".mov", ".m4v", ".3gp" }.Contains(ext))
            {
                var parser = await Task.Run(() => new MP4Parser(file), cancel); var tracks = parser.GetSubtitleTracks();
                if (tracks.Count == 0 || parser.Moov?.Tracks?.Any(t => t.Mdia?.IsVobSubSubtitle == true || t.Mdia?.HandlerType == "subt") == true) return await ClassicSyncMp4Reference.LoadAsync(owner, file, cancel, selectedTrack);
                var at = selectedTrack ?? 0;
                if (!selectedTrack.HasValue && tracks.Count > 1) using (var chooser = new MatroskaSubtitleChooser("mp4")) { chooser.Initialize(tracks); if (chooser.ShowDialog(owner) != DialogResult.OK) return null; at = chooser.SelectedIndex; }
                if (at < 0 || at >= tracks.Count) throw new ArgumentOutOfRangeException(nameof(selectedTrack));
                return await Task.Run(() => LoadMp4(tracks[at], cancel), cancel);
            }
            return await Task.Run(() =>
            {
                var result = new ClassicSyncReference();
                if (result.Subtitle.LoadSubtitle(file, out _, null) == null) throw new InvalidDataException("Unknown subtitle format: " + Path.GetFileName(file));
                cancel.ThrowIfCancellationRequested(); return result;
            }, cancel);
        }
        private static ClassicSyncReference LoadSup(string file, CancellationToken cancel)
        {
            var result = new ClassicSyncReference();
            foreach (var pcs in BluRaySupParser.ParseBluRaySup(file, new StringBuilder())) { cancel.ThrowIfCancellationRequested(); result.AddImage(pcs.StartTimeCode.TotalMilliseconds, pcs.EndTimeCode.TotalMilliseconds, pcs.GetBitmap); }
            return result;
        }
        private static ClassicSyncReference LoadMatroska(MatroskaFile mkv, MatroskaTrackInfo track, CancellationToken cancel)
        {
            var result = new ClassicSyncReference();
            if (track.ContentEncodingType == 1) throw new InvalidDataException("Encrypted subtitle tracks are not supported.");
            if (track.CodecId.Equals("S_HDMV/PGS", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var pcs in BluRaySupParser.ParseBluRaySupFromMatroska(track, mkv)) { cancel.ThrowIfCancellationRequested(); result.AddImage(pcs.StartTimeCode.TotalMilliseconds, pcs.EndTimeCode.TotalMilliseconds, pcs.GetBitmap); }
                return result;
            }
            var blocks = mkv.GetSubtitle(track.TrackNumber, (p, t) => cancel.ThrowIfCancellationRequested());
            if (track.CodecId.Equals("S_VOBSUB", StringComparison.OrdinalIgnoreCase))
            {
                var palette = new Idx(track.GetCodecPrivate().SplitToLines()).Palette;
                var packs = blocks.Select(b => new VobSubMergedPack(b.GetData(track), TimeSpan.FromMilliseconds(b.Start), 32, null) { EndTime = TimeSpan.FromMilliseconds(b.End), Palette = palette }).ToList();
                packs.RemoveAll(p => p.SubPicture.SubPictureDateSize <= 2 || p.SubPicture.SubPictureDateSize <= 67 && p.SubPicture.Delay.TotalMilliseconds < 35);
                for (var i = 0; i < packs.Count; i++) if (packs[i].EndTime <= packs[i].StartTime) packs[i].EndTime = i + 1 < packs.Count ? packs[i + 1].StartTime : packs[i].StartTime.Add(TimeSpan.FromSeconds(3));
                foreach (var pack in packs) result.AddImage(pack.StartTime.TotalMilliseconds, pack.EndTime.TotalMilliseconds, pack.GetBitmap);
                return result;
            }
            if (track.CodecId.Equals("S_DVBSUB", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var block in blocks)
                {
                    cancel.ThrowIfCancellationRequested(); var data = block.GetData(track); DvbSubPes pes = null;
                    if (data.Length > 9 && data[0] == 15 && data[1] >= SubtitleSegment.PageCompositionSegment && data[1] <= SubtitleSegment.DisplayDefinitionSegment) { var buffer = new byte[data.Length + 3]; Buffer.BlockCopy(data, 0, buffer, 2, data.Length); buffer[0] = 32; buffer[buffer.Length - 1] = 255; pes = new DvbSubPes(0, buffer); }
                    else if (VobSubParser.IsMpeg2PackHeader(data)) pes = new DvbSubPes(data, Mpeg2Header.Length);
                    else if (VobSubParser.IsPrivateStream1(data, 0)) pes = new DvbSubPes(data, 0);
                    else if (data.Length > 9 && data[0] == 32 && data[1] == 0) pes = new DvbSubPes(0, data);
                    if (pes?.PageCompositions?.Any(p => p.Regions.Count > 0) == true)
                    {
                        result.AddImage(block.Start, block.End, pes.GetImageFull);
                        var owned = pes;
                        result._dispose.Add(() => { if (owned.ObjectDataList != null) foreach (var item in owned.ObjectDataList) { item.Image?.Dispose(); item.Image = null; } });
                    }
                }
                return result;
            }
            Utilities.LoadMatroskaTextSubtitle(track, mkv, blocks, result.Subtitle);
            if (track.CodecId.Equals("S_HDMV/TEXTST", StringComparison.OrdinalIgnoreCase)) Utilities.ParseMatroskaTextSt(track, blocks, result.Subtitle);
            result.Subtitle.Renumber(); return result;
        }
        private static ClassicSyncReference LoadMp4(Trak track, CancellationToken cancel)
        {
            var result = new ClassicSyncReference(); var paragraphs = track.Mdia.Minf.Stbl.GetParagraphs();
            if (track.Mdia.IsVobSubSubtitle)
            {
                for (var i = 0; i < Math.Min(paragraphs.Count, track.Mdia.Minf.Stbl.SubPictures.Count); i++)
                {
                    cancel.ThrowIfCancellationRequested(); var p = paragraphs[i]; var picture = track.Mdia.Minf.Stbl.SubPictures[i];
                    result.AddImage(p.StartTime.TotalMilliseconds, p.EndTime.TotalMilliseconds, () => picture.GetBitmap(null, Color.Transparent, Color.Black, Color.White, Color.Black, true));
                }
            }
            else result.Subtitle.Paragraphs.AddRange(paragraphs.Select(p => new Paragraph(p, false)));
            result.Subtitle.Renumber(); return result;
        }
    }
}
