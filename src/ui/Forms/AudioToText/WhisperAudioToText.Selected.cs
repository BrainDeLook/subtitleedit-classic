using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.AudioToText;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Forms.AudioToText
{
    public sealed partial class WhisperAudioToText
    {
        private ClassicSelectedSpeech _selectedSpeech;
        private DataGridView _selectedSpeechGrid;
        private TableLayoutPanel _selectedSpeechArea;
        private Button _retrySelectedSpeech;
        private bool _retrySelectedOnly;
        public ClassicSelectedSpeech SelectedSpeech => _selectedSpeech;

        public void ConfigureSelectedSpeech(List<AudioClipsGet.AudioClip> clips)
        {
            _selectedSpeech = new ClassicSelectedSpeech(clips.Select(c => Tuple.Create(c.Paragraph, c.AudioFileName)));
            Text += " — " + ClassicSpeechSettings.L("выбранные строки", "selected lines");
            _selectedSpeechGrid = new DataGridView { Name = "selectedSpeechResults", Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BackgroundColor = SystemColors.Window };
            foreach (var title in new[] { "#", ClassicSpeechSettings.L("Время", "Time"), ClassicSpeechSettings.L("Состояние", "Status"), ClassicSpeechSettings.L("Исходный текст", "Original text"), ClassicSpeechSettings.L("Результат / ошибка", "Result / error") }) _selectedSpeechGrid.Columns.Add("c" + _selectedSpeechGrid.Columns.Count, title);
            _selectedSpeechGrid.Columns[0].FillWeight = 25; _selectedSpeechGrid.Columns[1].FillWeight = 110; _selectedSpeechGrid.Columns[2].FillWeight = 85;
            _selectedSpeechGrid.Columns[3].FillWeight = _selectedSpeechGrid.Columns[4].FillWeight = 220;
            foreach (var row in _selectedSpeech.Rows) _selectedSpeechGrid.Rows.Add(row.Source.Number, row.Source.StartTime.ToDisplayString(), "", row.Source.Text, "");
            _retrySelectedSpeech = new Button { Name = "buttonRetryFailedSpeech", AutoSize = true, Text = ClassicSpeechSettings.L("Повторить неудачные", "Retry failed lines") };
            _retrySelectedSpeech.Click += (s, e) => { _retrySelectedOnly = true; ButtonGenerate_Click(s, e); };
            var footer = new FlowLayoutPanel { Name = "selectedSpeechFooter", Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, FlowDirection = FlowDirection.RightToLeft };
            buttonBatchMode.Text = ClassicSpeechSettings.L("Применить распознанные", "Apply transcribed lines"); buttonBatchMode.AutoSize = true;
            footer.Controls.AddRange(new Control[] { buttonCancel, buttonBatchMode, _retrySelectedSpeech, buttonGenerate });
            foreach (Control button in footer.Controls) { button.Anchor = AnchorStyles.Top | AnchorStyles.Left; button.Margin = new Padding(4); }
            var progress = new Panel { Dock = DockStyle.Fill, Height = 44 };
            progress.Controls.AddRange(new Control[] { labelProgress, labelElapsed, progressBar1 });
            labelProgress.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            labelElapsed.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            labelProgress.Height = labelElapsed.Height = Math.Max(24, Font.Height + 6);
            labelProgress.Location = new Point(0, 0); labelElapsed.Top = 0; progressBar1.Bounds = new Rectangle(0, labelProgress.Height + 3, 200, 14); progressBar1.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            progress.Resize += (s, e) => { labelElapsed.Left = Math.Max(0, progress.Width - labelElapsed.Width); labelProgress.Width = Math.Max(100, labelElapsed.Left - 8); progressBar1.Width = progress.Width; };
            _selectedSpeechArea = new TableLayoutPanel { Name = "selectedSpeechArea", ColumnCount = 1, RowCount = 3, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom };
            _selectedSpeechArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); _selectedSpeechArea.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Max(48, Font.Height + 25))); _selectedSpeechArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _selectedSpeechArea.Controls.Add(_selectedSpeechGrid, 0, 0); _selectedSpeechArea.Controls.Add(progress, 0, 1); _selectedSpeechArea.Controls.Add(footer, 0, 2); Controls.Add(_selectedSpeechArea);
            checkBoxAutoAdjustTimings.Visible = false;
            runOnlyPostProcessingToolStripMenuItem.Visible = toolStripSeparatorRunOnlyPostprocessing.Visible = false;
            if (clips.Count == 1 && clips[0].Paragraph.DurationTotalMilliseconds > 8000)
            {
                var split = new ToolStripMenuItem(ClassicSpeechSettings.L("Распознать фрагмент с разбиением и новыми таймингами…", "Transcribe clip with splitting and new timings…")) { Name = "selectedSpeechSplitClip" };
                split.Click += (s, e) => { if (_modernCancellation != null) return; SaveModernSelection(); DialogResult = DialogResult.Retry; };
                contextMenuStripWhisperAdvanced.Items.Add(new ToolStripSeparator()); contextMenuStripWhisperAdvanced.Items.Add(split);
            }
            foreach (var i in Enumerable.Range(0, _selectedSpeech.Rows.Count)) RefreshSelectedSpeechRow(i);
            Resize += (s, e) => LayoutSelectedSpeech();
            LayoutSelectedSpeech();
        }

        private void LayoutSelectedSpeech()
        {
            if (_selectedSpeechArea == null) return;
            groupBoxInputFiles.Visible = false; listViewInputFiles.Visible = false; textBoxLog.Visible = false;
            _selectedSpeechArea.Bounds = new Rectangle(12, buttonAdvanced.Bottom + 10, Math.Max(1, ClientSize.Width - 24), Math.Max(1, ClientSize.Height - buttonAdvanced.Bottom - 20));
            _selectedSpeechArea.BringToFront();
        }
        private void SetSelectedSpeechState()
        {
            var idle = _modernCancellation == null;
            _retrySelectedSpeech.Enabled = idle && _selectedSpeech.Rows.Any(r => r.State != SelectedSpeechState.Complete);
            buttonBatchMode.Enabled = idle && _selectedSpeech.Rows.Any(r => r.State == SelectedSpeechState.Complete);
            checkBoxUsePostProcessing.Enabled = idle;
            checkBoxTranslateToEnglish.Enabled = idle && (!IsModernSpeech || ModernEngine.Family == "cpp" || ModernEngine.Family == "whisperx");
            buttonCancel.Visible = true;
        }
        private void RefreshSelectedSpeechRow(int i)
        {
            if (IsDisposed || Disposing) return;
            var row = _selectedSpeech.Rows[i]; var grid = _selectedSpeechGrid.Rows[i];
            grid.Cells[2].Value = row.State == SelectedSpeechState.Complete ? ClassicSpeechSettings.L("Готово", "Done") : row.State == SelectedSpeechState.Failed ? ClassicSpeechSettings.L("Ошибка / нет речи", "Failed / no speech") : row.State == SelectedSpeechState.Running ? ClassicSpeechSettings.L("Распознавание…", "Transcribing…") : ClassicSpeechSettings.L("Ожидает", "Pending");
            grid.Cells[4].Value = row.State == SelectedSpeechState.Failed ? row.Error : row.Text;
            grid.Cells[4].ToolTipText = Convert.ToString(grid.Cells[4].Value);
            grid.DefaultCellStyle.BackColor = row.State == SelectedSpeechState.Failed ? Color.MistyRose : SystemColors.Window;
        }
        private async Task GenerateSelectedSpeech()
        {
            if (_modernCancellation != null) return;
            var failedOnly = _retrySelectedOnly; _retrySelectedOnly = false;
            var modern = IsModernSpeech;
            if (modern) SaveModernSelection();
            var engine = modern ? ModernEngine : null; var model = comboBoxModels.SelectedItem as SpeechModel;
            if (modern && model == null) return;
            if (modern && engine.Family!="compatible-api" && EnglishOnly(model) && _modernOptions.Language != "en") { MessageBox.Show(this, ClassicSpeechSettings.L("Выбери English или многоязычную модель.", "Choose English or a multilingual model."), Text); return; }
            if (modern && engine.Family!="compatible-api" && (model.Name==ClassicLegacyAsr.AnimeName || model.Name.StartsWith("reazonspeech-", StringComparison.OrdinalIgnoreCase) || model.Name.Contains("-ja-")) && _modernOptions.Language != "ja") { MessageBox.Show(this, ClassicSpeechSettings.L("Для этой модели выбери Japanese.", "Choose Japanese for this model."), Text); return; }
            BeginModernOperation(); SetSelectedSpeechState();
            var job = Path.Combine(ClassicSpeechCatalog.Root, "Jobs", "selected-" + Guid.NewGuid().ToString("N"));
            try
            {
                var cancel = _modernCancellation.Token; var progress = ModernProgress(System.Diagnostics.Stopwatch.StartNew());
                if (modern) await EnsureModernDependencies(engine, model, cancel, progress);
                var aligner = modern ? ModernAligner(engine) : null;
                await _selectedSpeech.RunAsync(async (row, token) =>
                {
                    var folder = Path.Combine(job, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
                    var wave = Path.Combine(folder, "clip.wav"); File.Copy(row.AudioFile, wave);
                    Subtitle transcript;
                    if (modern) transcript = await ClassicSpeechRunner.TranscribeAsync(engine, model, _modernOptions, wave, folder, aligner == null ? null : ClassicSpeechCatalog.ModelPath(engine, aligner), token, progress);
                    else transcript = TranscribeViaWhisper(wave, wave);
                    token.ThrowIfCancellationRequested();
                    if (transcript == null) return null;
                    if (modern)
                    {
                        string previous = null;
                        for (int i = 0; i < transcript.Paragraphs.Count; i++)
                        {
                            var p = transcript.Paragraphs[i];
                            if (_modernOptions.RemoveNonSpeech && SpeechToTextQualityReport.IsNonSpeechLine(p.Text) || _modernOptions.RemoveRepeats && SpeechToTextQualityReport.IsRepeatOf(p.Text, previous ?? "")) { transcript.Paragraphs.RemoveAt(i--); continue; }
                            previous = p.Text;
                        }
                    }
                    var language = modern ? (_modernOptions.TranslateToEnglish ? "en" : _modernOptions.Language) : (checkBoxTranslateToEnglish.Checked ? "en" : _languageCode);
                    var post = new AudioToTextPostProcessor(language);
                    transcript = ClassicSpeechExtras.ProcessByActor(transcript, group => post.Fix(AudioToTextPostProcessor.Engine.Whisper, group, checkBoxUsePostProcessing.Checked,
                        Configuration.Settings.Tools.WhisperPostProcessingAddPeriods, Configuration.Settings.Tools.WhisperPostProcessingMergeLines, Configuration.Settings.Tools.WhisperPostProcessingFixCasing,
                        false, Configuration.Settings.Tools.WhisperPostProcessingSplitLines));
                    return string.Join(Environment.NewLine, transcript.Paragraphs.Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
                }, failedOnly, cancel, RefreshSelectedSpeechRow);
            }
            catch (OperationCanceledException) { labelProgress.Text = ClassicSpeechSettings.L("Остановлено; готовые строки сохранены", "Stopped; completed lines retained"); }
            catch (Exception ex) { ShowModernError(ex); }
            finally
            {
                timer1.Stop();
                try { if (Directory.Exists(job)) Directory.Delete(job, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                var reportSubtitle = new Subtitle();
                foreach (var row in _selectedSpeech.Rows) { var cue = new Paragraph(row.Source, false); if (row.State == SelectedSpeechState.Complete) cue.Text = row.Text; reportSubtitle.Paragraphs.Add(cue); }
                _qualityReport = AnalyzeSpeech(reportSubtitle);
                EndModernOperation();
                if (!IsDisposed) { SetSelectedSpeechState(); LayoutSelectedSpeech(); labelProgress.Text = ClassicSpeechSettings.L("Распознано: ", "Transcribed: ") + _selectedSpeech.Rows.Count(r => r.State == SelectedSpeechState.Complete) + "/" + _selectedSpeech.Rows.Count; }
            }
        }
    }
}
