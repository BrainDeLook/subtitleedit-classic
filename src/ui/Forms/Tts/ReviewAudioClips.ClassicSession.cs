using Nikse.SubtitleEdit.Logic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms.Tts
{
    public sealed partial class ReviewAudioClips
    {
        private readonly Dictionary<int, List<ClassicTtsSessionItem>> _takeHistory = new Dictionary<int, List<ClassicTtsSessionItem>>();
        private CancellationTokenSource _sessionExportCancellation;
        private bool _closeAfterSessionExport;
        private Button _saveSessionButton, _historyButton;
        private void InitializeSessionReview()
        {
            var oldSize = ClientSize; var controls = Controls.Cast<Control>().ToArray(); var bounds = controls.Select(c => c.Bounds).ToArray(); var anchors = controls.Select(c => c.Anchor).ToArray();
            SuspendLayout(); foreach (var c in controls) c.Anchor = AnchorStyles.Top | AnchorStyles.Left; Controls.Clear(); ClientSize = new Size(oldSize.Width, oldSize.Height + 38); MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height + 38);
            var content = new Panel { Bounds = new Rectangle(Point.Empty, oldSize), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right }; Controls.Add(content); content.Controls.AddRange(controls);
            for (int i = 0; i < controls.Length; i++) { controls[i].Bounds = bounds[i]; controls[i].Anchor = anchors[i]; }
            var bar = new FlowLayoutPanel { Name = "sessionReviewToolbar", Bounds = new Rectangle(0, oldSize.Height, oldSize.Width, 38), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, Padding = new Padding(8, 3, 8, 3) };
            _saveSessionButton = new Button { Name = "buttonSaveSpeechSession", AutoSize = true, Text = TextToSpeech.SessionLabel("Сохранить сессию…", "Save session…") }; _saveSessionButton.Click += async (s, e) => await SaveSessionAsync();
            _historyButton = new Button { Name = "buttonSpeechTakeHistory", AutoSize = true, Text = TextToSpeech.SessionLabel("История дублей…", "Take history…") }; _historyButton.Click += (s, e) => ShowTakeHistory();
            bar.Controls.AddRange(new Control[] { _saveSessionButton, _historyButton }); Controls.Add(bar); ResumeLayout(false);
            for (int i = 0; i < _subtitle.Paragraphs.Count; i++) if (_textToSpeech.SessionRowEnabled(_subtitle.Paragraphs[i]) is bool include) listViewAudioClips.Items[i].Checked = include;
            _textToSpeech.SpeechTakeGenerated += RecordGeneratedTake;
            FormClosed += (s, e) => _textToSpeech.SpeechTakeGenerated -= RecordGeneratedTake;
            FormClosing += (s, e) => { if (_sessionExportCancellation != null) { _closeAfterSessionExport = true; _sessionExportCancellation.Cancel(); e.Cancel = true; } };
        }
        private void RecordGeneratedTake(Nikse.SubtitleEdit.Core.Common.Paragraph paragraph, TextToSpeech.FileNameAndSpeedFactor audio)
        {
            var index = _subtitle.Paragraphs.FindIndex(p => p.Id == paragraph.Id);
            if (index >= 0) RememberTake(index, _textToSpeech.CaptureSessionRow(paragraph, audio, listViewAudioClips.Items[index].Checked));
        }
        public ClassicTtsSessionItem SnapshotTake(int index) => _textToSpeech.CaptureSessionRow(_subtitle.Paragraphs[index], _fileNames[index], listViewAudioClips.Items[index].Checked);
        public void RememberTake(int index, ClassicTtsSessionItem before)
        {
            if (!_takeHistory.TryGetValue(index, out var takes)) _takeHistory[index] = takes = new List<ClassicTtsSessionItem>();
            if (!takes.Any(t => t.AudioFileName == before.AudioFileName && t.Text == before.Text && t.VoiceName == before.VoiceName)) takes.Add(TextToSpeech.CloneSessionRow(before));
        }
        public void RestoreTake(int index, ClassicTtsSessionItem take)
        {
            if (!File.Exists(take.AudioFileName)) throw new FileNotFoundException("The previous take is no longer available");
            RememberTake(index, SnapshotTake(index)); Stop();
            _textToSpeech.RestoreSessionRow(_subtitle.Paragraphs[index], take);
            _fileNames[index].Filename = take.AudioFileName; _fileNames[index].Factor = take.SpeedFactor;
            RefreshSessionReviewRow(index);
        }
        private void RefreshSessionReviewRow(int index)
        {
            var row = listViewAudioClips.Items[index]; var paragraph = _subtitle.Paragraphs[index];
            row.SubItems[2].Text = _textToSpeech.GetParagraphAudio(paragraph); row.SubItems[3].Text = paragraph.GetCharactersPerSecond().ToString("0.#");
            row.SubItems[4].Text = _fileNames[index].Factor == 1 ? "-" : (_fileNames[index].Factor * 100).ToString("0.#") + "%"; row.SubItems[5].Text = paragraph.Text;
        }
        public Task ExportSessionAsync(string fileName, CancellationToken cancel)
        {
            var snapshot = _textToSpeech.CreateSpeechSession(_subtitle, _fileNames, listViewAudioClips.Items.Cast<ListViewItem>().Select(i => i.Checked).ToArray());
            return ClassicTtsSession.ExportAsync(snapshot, fileName, cancel);
        }
        private async Task SaveSessionAsync()
        {
            if (_sessionExportCancellation != null) { _sessionExportCancellation.Cancel(); return; }
            using (var dialog = new SaveFileDialog { Title = TextToSpeech.SessionLabel("Сохранить сессию озвучки", "Save speech session"), FileName = "SubtitleEditTts.json", Filter = "TTS session|*.json", AddExtension = true, DefaultExt = "json", OverwritePrompt = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                Stop(); _sessionExportCancellation = new CancellationTokenSource(); listViewAudioClips.Enabled = buttonEdit.Enabled = buttonOK.Enabled = _historyButton.Enabled = false; _saveSessionButton.Text = TextToSpeech.SessionLabel("Отменить сохранение", "Cancel saving");
                try { await ExportSessionAsync(dialog.FileName, _sessionExportCancellation.Token); labelParagraphInfo.Text = TextToSpeech.SessionLabel("Сессия сохранена", "Session saved"); }
                catch (OperationCanceledException) { labelParagraphInfo.Text = TextToSpeech.SessionLabel("Сохранение отменено", "Saving cancelled"); }
                catch (Exception error) { MessageBox.Show(this, error.Message, Text); }
                finally { _sessionExportCancellation.Dispose(); _sessionExportCancellation = null; listViewAudioClips.Enabled = buttonEdit.Enabled = buttonOK.Enabled = _historyButton.Enabled = true; _saveSessionButton.Text = TextToSpeech.SessionLabel("Сохранить сессию…", "Save session…"); if (_closeAfterSessionExport) Close(); }
            }
        }
        private void ShowTakeHistory()
        {
            if (listViewAudioClips.SelectedIndices.Count == 0) return; var index = listViewAudioClips.SelectedIndices[0];
            var current = SnapshotTake(index);
            var takes = (_takeHistory.TryGetValue(index, out var history) ? history : new List<ClassicTtsSessionItem>()).Where(t => t.AudioFileName != current.AudioFileName || t.Text != current.Text).Concat(new[] { current }).ToList();
            using (var dialog = new Form { Text = TextToSpeech.SessionLabel("История дублей", "Take history"), ClientSize = new Size(760, 340), MinimumSize = new Size(620, 320), StartPosition = FormStartPosition.CenterParent })
            {
                var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false }; foreach (var column in new[] { "#", TextToSpeech.SessionLabel("Голос", "Voice"), TextToSpeech.SessionLabel("Движок", "Engine"), TextToSpeech.SessionLabel("Текст", "Text") }) list.Columns.Add(column, column == "#" ? 40 : column == TextToSpeech.SessionLabel("Текст", "Text") ? 300 : 170);
                for (int i = 0; i < takes.Count; i++) { var take = takes[i]; var item = new ListViewItem((i + 1).ToString() + (i == takes.Count - 1 ? " *" : "")) { Tag = take }; item.SubItems.Add(take.VoiceName); item.SubItems.Add(take.EngineName); item.SubItems.Add(take.Text); list.Items.Add(item); } list.Items[list.Items.Count - 1].Selected = true;
                var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(5) }; var cancel = new Button { Text = TextToSpeech.SessionLabel("Отмена", "Cancel"), DialogResult = DialogResult.Cancel }; var restore = new Button { Text = TextToSpeech.SessionLabel("Восстановить", "Restore"), AutoSize = true, DialogResult = DialogResult.OK }; var play = new Button { Text = TextToSpeech.SessionLabel("Прослушать", "Play"), AutoSize = true };
                play.Click += (s, e) => { if (list.SelectedItems.Count == 0) return; Stop(); var saved = _fileNames[index].Filename; try { _fileNames[index].Filename = ((ClassicTtsSessionItem)list.SelectedItems[0].Tag).AudioFileName; Play(true); } finally { _fileNames[index].Filename = saved; } };
                bar.Controls.AddRange(new Control[] { cancel, restore, play }); dialog.Controls.Add(list); dialog.Controls.Add(bar); dialog.AcceptButton = restore; dialog.CancelButton = cancel;
                var result = dialog.ShowDialog(this); Stop(); if (result == DialogResult.OK && list.SelectedItems.Count > 0) RestoreTake(index, (ClassicTtsSessionItem)list.SelectedItems[0].Tag);
            }
        }
    }
}
