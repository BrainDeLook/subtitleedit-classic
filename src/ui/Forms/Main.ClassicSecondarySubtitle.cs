using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.VideoPlayers;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private Subtitle _classicSecondarySource, _classicSecondaryPreview;
        private Timer _classicSecondaryTimer;
        private Label _classicSecondaryLabel;
        private string _classicSecondaryStyleKey;
        private Font _classicSecondaryFont;

        private void LoadClassicSecondarySubtitle(string fileName)
        {
            var source = Subtitle.Parse(fileName);
            if (source == null || source.Paragraphs.Count == 0) { MessageBox.Show(this, ClassicErrorReport.L("Не удалось прочитать второй файл субтитров.", "Could not read the second subtitle file."), Text); return; }
            _classicSecondarySource = source;
            _classicSecondaryStyleKey = null;
            if (_classicSecondaryTimer == null)
            {
                _classicSecondaryTimer = new Timer { Interval = 80 };
                _classicSecondaryTimer.Tick += (sender, args) => RefreshClassicSecondarySubtitle();
                FormClosed += (sender, args) => { _classicSecondaryTimer.Dispose(); _classicSecondaryFont?.Dispose(); };
                _classicSecondaryLabel = new Label { Name = "classicSecondarySubtitle", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Black, ForeColor = Color.White, Visible = false };
                mediaPlayer.Controls.Add(_classicSecondaryLabel);
            }
            RefreshClassicSecondarySubtitle();
            _classicSecondaryTimer.Start();
        }

        private void RefreshClassicSecondarySubtitle()
        {
            if (_classicSecondarySource == null || mediaPlayer.VideoPlayer == null) return;
            var offset = Configuration.Settings.General.CurrentVideoOffsetInMs;
            var key = offset + "|" + _classicEditorPreferences.SecondarySubtitleFont + "|" + mediaPlayer.Duration + "|" + mediaPlayer.VideoWidth + "x" + mediaPlayer.VideoHeight;
            var native = mediaPlayer.VideoPlayer is LibMpvDynamic || mediaPlayer.VideoPlayer is LibVlcDynamic;
            if (_classicSecondaryStyleKey != key)
            {
                _classicSecondaryPreview = ClassicSecondarySubtitle.Build(_classicSecondarySource, _classicEditorPreferences.SecondarySubtitleFont, offset, mediaPlayer.Duration, Math.Max(384, mediaPlayer.VideoWidth), Math.Max(288, mediaPlayer.VideoHeight), _subtitle);
                if (native)
                {
                    var file = Path.Combine(Path.GetTempPath(), "se-secondary-" + Guid.NewGuid().ToString("N") + ".ass");
                    File.WriteAllText(file, new AdvancedSubStationAlpha().ToText(_classicSecondaryPreview, "Secondary subtitle"));
                    _filesToDelete.Add(file);
                    if (mediaPlayer.VideoPlayer is LibMpvDynamic mpv)
                    {
                        if (Configuration.Settings.General.MpvHandlesPreviewText) { Configuration.Settings.General.MpvHandlesPreviewText = false; mediaPlayer.SubtitleText = string.Empty; _restorePreviewAfterSecondSubtitle = true; }
                        mpv.LoadSecondSubtitle(file);
                    }
                    else ((LibVlcDynamic)mediaPlayer.VideoPlayer).LoadSecondSubtitle(file);
                }
                _classicSecondaryStyleKey = key;
            }
            if (native) { _classicSecondaryLabel.Visible = false; return; }
            var seconds = mediaPlayer.CurrentPosition;
            var text = string.Join(Environment.NewLine, _classicSecondaryPreview.Paragraphs.Where(p => p.StartTime.TotalSeconds <= seconds && p.EndTime.TotalSeconds > seconds).Select(p => HtmlUtil.RemoveHtmlTags(p.Text, true)));
            if (_classicSecondaryLabel.Font.Name != _classicEditorPreferences.SecondarySubtitleFont || Math.Abs(_classicSecondaryLabel.Font.Size - Configuration.Settings.General.VideoPlayerPreviewFontSize) > .1)
            {
                _classicSecondaryFont?.Dispose();
                _classicSecondaryFont = new Font(_classicEditorPreferences.SecondarySubtitleFont, Configuration.Settings.General.VideoPlayerPreviewFontSize, FontStyle.Bold);
                _classicSecondaryLabel.Font = _classicSecondaryFont;
            }
            _classicSecondaryLabel.SetBounds(0, 0, mediaPlayer.Width, Math.Max(40, _classicSecondaryLabel.Font.Height * Math.Max(1, text.Split('\n').Length) + 8));
            _classicSecondaryLabel.Text = text;
            _classicSecondaryLabel.Visible = mediaPlayer.PreviewSubtitlesVisible && text.Length > 0;
            _classicSecondaryLabel.BringToFront();
        }
    }
}
