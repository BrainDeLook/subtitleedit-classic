using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class GenerateVideoWithHardSubs
    {
        private void InitializeAudioTrackLabels()
        {
            if (BatchMode || listViewAudioTracks.Items.Count <= 1) return;
            try
            {
                var tracks = ClassicAudioTrackLabels.Read(_inputVideoFileName);
                if (tracks.Count == listViewAudioTracks.Items.Count)
                    for (int i = 0; i < tracks.Count; i++)
                    {
                        var label = tracks[i].Display(i, LanguageSettings.Current.General.CultureName.StartsWith("ru", StringComparison.OrdinalIgnoreCase));
                        listViewAudioTracks.Items[i].Text = label + " — " + tracks[i].Details;
                        listViewAudioTracks.Items[i].ToolTipText = label + Environment.NewLine + tracks[i].Details;
                    }
            }
            catch (Exception ex) { SeLogger.Error(ex, "Reading burn-in audio track names"); }
            listViewAudioTracks.ShowItemToolTips = true;
            listViewAudioTracks.HeaderStyle = ColumnHeaderStyle.None;
            var top = Math.Max(comboBoxAudioBitRate.Bottom, comboBoxAudioSampleRate.Bottom) + 8;
            var height = Math.Max(54, Font.Height * (Math.Min(4, listViewAudioTracks.Items.Count) + 1) + 8);
            var extra = Math.Max(0, top + height + 12 - groupBoxAudio.Height);
            var preview = videoPlayerContainer1.Bounds;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + extra);
            MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height + extra);
            groupBoxAudio.Height += extra;
            videoPlayerContainer1.Bounds = new Rectangle(preview.X, preview.Y + extra, preview.Width, preview.Height);
            listViewAudioTracks.SetBounds(12, top, groupBoxAudio.ClientSize.Width - 24, height);
            listViewAudioTracks.Resize += (s, e) => FitAudioTrackColumn();
            FitAudioTrackColumn();
            LayoutClassicFooter();
        }
        private void FitAudioTrackColumn()
        {
            if (listViewAudioTracks.Columns.Count > 0) listViewAudioTracks.Columns[0].Width = Math.Max(1, listViewAudioTracks.ClientSize.Width - 24);
        }
    }
}
