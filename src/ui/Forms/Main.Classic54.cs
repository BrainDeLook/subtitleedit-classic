using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private void TranslateClassicSelectedRowsTogether()
        {
            ReloadFromSourceView();
            var indices = SubtitleListview1.GetSelectedIndices();
            if (indices.Length == 0) return;
            var source = _subtitle;
            if (SubtitleListview1.IsOriginalTextColumnVisible)
            {
                if (_subtitleOriginal.Paragraphs.Count != _subtitle.Paragraphs.Count)
                {
                    MessageBox.Show(this, ClassicAssistedText.L("Количество строк исходника и перевода различается.", "Source and translation have different line counts."));
                    return;
                }
                source = _subtitleOriginal;
            }
            mediaPlayer.Pause();
            SaveSubtitleListviewIndices();
            using (var form = new Translate.AutoTranslate(source, _subtitle,
                ClassicAssistedText.L("Перевод выбранных реплик с заменой на месте", "Translate selected cues and replace in place"), GetCurrentEncoding(), indices))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                if (!_subtitle.Paragraphs.Where((cue, i) => cue.Text != form.TranslatedSubtitle.Paragraphs[i].Text).Any()) return;
                MakeHistoryForUndo(_language.BeforeGoogleTranslation);
                for (var i = 0; i < _subtitle.Paragraphs.Count; i++) _subtitle.Paragraphs[i].Text = form.TranslatedSubtitle.Paragraphs[i].Text;
                SubtitleListview1.Fill(_subtitle, _subtitleOriginal);
                RestoreSubtitleListviewIndices();
                RefreshSelectedParagraph();
                UpdateSourceView();
                audioVisualizer.Invalidate();
                SetTitle();
            }
        }

        private bool _classic54Events;
        private void InitializeClassic54()
        {
            InitializeClassicEditor54();
            var ytt = NavigationAction(fileToolStripMenuItem.DropDownItems, "classicYttStyles", "Стили YouTube timed text…", "YouTube timed text styles…", () =>
            {
                if (!(GetCurrentSubtitleFormat() is Nikse.SubtitleEdit.Core.SubtitleFormats.YouTubeTimedText)) return;
                using (var form = new ClassicYttStyles(_subtitle.Header))
                {
                    if (form.ShowDialog(this) != DialogResult.OK) return;
                    MakeHistoryForUndo("Before editing YouTube timed text styles"); _subtitle.Header = form.Header;
                    if (form.AssignToSelection) foreach (var index in SubtitleListview1.GetSelectedIndices())
                    {
                        var cue = _subtitle.Paragraphs[index];
                        if (form.PenId.HasValue) cue.Extra = form.PenId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        if (form.PositionId.HasValue) cue.Region = form.PositionId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        if (form.WindowStyleId.HasValue) cue.Effect = form.WindowStyleId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                    SubtitleListview1.Fill(_subtitle, _subtitleOriginal); RefreshSelectedParagraph(); mediaPlayer.LastParagraph = null;
                    UiUtil.ShowSubtitle(_subtitle, mediaPlayer, GetCurrentSubtitleFormat());
                }
            });
            fileToolStripMenuItem.DropDownItems.Remove(ytt);
            fileToolStripMenuItem.DropDownItems.Insert(fileToolStripMenuItem.DropDownItems.IndexOf(toolStripMenuItemAssaStyles)+1,ytt);
            if (!_classic54Events) { fileToolStripMenuItem.DropDownOpening += (s, e) => ytt.Enabled = GetCurrentSubtitleFormat() is Nikse.SubtitleEdit.Core.SubtitleFormats.YouTubeTimedText; _classic54Events = true; }
            var cinema = FindClassicMenu(menuStrip1.Items, "classicExportCinema");
            NavigationAction(cinema?.DropDownItems ?? toolStripMenuItemExport.DropDownItems, "classicCinemaMxfText", "D-Cinema SMPTE 2014 MXF (текст)…", "D-Cinema SMPTE 2014 MXF (text)…", () =>
            {
                if (_subtitle.Paragraphs.Count == 0) return;
                using (var form = new ClassicCinemaMxfForm(_subtitle, _fileName)) form.ShowDialog(this);
            });
            var quality = FindClassicMenu(menuStrip1.Items, "classicToolsQuality");
            NavigationAction(quality?.DropDownItems ?? toolsToolStripMenuItem.DropDownItems, "classicCustomErrorRules", "Пользовательские правила ошибок…", "Custom error rules…", () =>
            {
                using (var form = new ClassicCustomErrorRulesForm()) if (form.ShowDialog(this) == DialogResult.OK) SubtitleListview1.SyntaxColorAllLines(_subtitle);
            });
            var source = FindClassicMenu(menuStrip1.Items, "classicVideoSources");
            if (source == null) return;
            NavigationAction(source.DropDownItems, "classicSyncToAudio", "Синхронизировать субтитры со звуком…", "Sync subtitle to audio…", () =>
            {
                if (_subtitle.Paragraphs.Count == 0 || audioVisualizer.WavePeaks == null)
                { MessageBox.Show(this, ClassicAssistedText.L("Откройте субтитры, видео и создайте осциллограмму нужной аудиодорожки.", "Open subtitles and video, then create the waveform for the required audio track.")); return; }
                mediaPlayer.Pause();
                var document = _subtitle; var snapshot = GetFastSubtitleHash(); var selected = FirstSelectedIndex;
                using (var form = new ClassicSyncToAudio(_subtitle, audioVisualizer.WavePeaks, CurrentFrameRate))
                {
                    if (form.ShowDialog(this) != DialogResult.OK) return;
                    if (!ReferenceEquals(document, _subtitle) || snapshot != GetFastSubtitleHash())
                    { MessageBox.Show(this, ClassicAssistedText.L("Документ изменился во время анализа. Повторите анализ.", "The document changed during analysis. Analyze it again.")); return; }
                    MakeHistoryForUndo("Before syncing subtitle to audio"); _subtitle = form.EditedSubtitle;
                    SubtitleListview1.Fill(_subtitle, _subtitleOriginal); _subtitleListViewIndex = -1;
                    if (selected >= 0) SubtitleListview1.SelectIndexAndEnsureVisibleFaster(selected);
                    RefreshSelectedParagraph(); audioVisualizer.Invalidate();
                }
            });
            NavigationAction(source.DropDownItems, "classicSyncAudioTrack", "Синхронизировать аудиодорожку…", "Sync audio track…", () =>
            {
                if (!File.Exists(_videoFileName) || !RequireFfmpegOk()) return;
                mediaPlayer.Pause();
                using (var form = new ClassicSyncAudioTrack(_videoFileName, ClassicSelectedAudioIndex(), mediaPlayer.CurrentPosition)) form.ShowDialog(this);
            });
        }
    }
}
