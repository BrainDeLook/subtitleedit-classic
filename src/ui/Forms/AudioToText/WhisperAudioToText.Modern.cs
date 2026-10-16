using Nikse.SubtitleEdit.Core.AudioToText;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MessageBox = Nikse.SubtitleEdit.Forms.SeMsgBox.MessageBox;

namespace Nikse.SubtitleEdit.Forms.AudioToText
{
    public sealed partial class WhisperAudioToText
    {
        private bool _modernReady;
        private bool _modernUpdating;
        private CancellationTokenSource _modernCancellation;
        private ClassicSpeechOptions _modernOptions;
        private ComboBox _modernBackend;
        private Label _modernBackendLabel;
        private Label _modernStatus;
        private Button _qualityButton;
        private SpeechToTextQualityReport _qualityReport;
        private long _modernLastProgress;
        private bool _modernCloseRequested;
        private System.Windows.Forms.Timer _modernClock;
        private Stopwatch _modernWatch;
        private static readonly HashSet<string> ModernNames = new HashSet<string>
        { ClassicSpeechCatalog.Crisp, ClassicSpeechCatalog.Qwen, ClassicSpeechCatalog.WhisperX, ClassicSpeechCatalog.CppVulkan };

        private bool IsModernSpeech => _modernReady && ModernNames.Contains(comboBoxWhisperEngine.Text);
        private SpeechEngine ModernEngine => IsModernSpeech ? ClassicSpeechCatalog.Get(comboBoxWhisperEngine.Text,
            (_modernBackend.SelectedItem as SpeechEngine)?.Backend ?? _modernOptions.Backend) : null;

        private void InitializeModernSpeech()
        {
            if (IntPtr.Size != 8) return;
            _modernOptions = ClassicSpeechOptions.Load();
            _modernClock = new System.Windows.Forms.Timer { Interval = 500 };
            _modernClock.Tick += (s, e) => { if (_modernCancellation != null) labelElapsed.Text = _modernWatch.Elapsed.ToString(@"hh\:mm\:ss"); };
            components.Add(_modernClock);
            SuspendLayout();
            var before = Controls.Cast<Control>().ToDictionary(c => c, c => c.Bounds);
            var bottom = groupBoxModels.Bottom;
            ClientSize = new Size(Math.Max(800, ClientSize.Width), ClientSize.Height + 36);
            foreach (var pair in before)
            {
                var bounds = pair.Value;
                if (bounds.Top >= bottom) bounds.Offset(0, 36);
                if ((pair.Key.Anchor & AnchorStyles.Right) != 0 && (pair.Key.Anchor & AnchorStyles.Left) == 0) bounds.Offset(ClientSize.Width - 738, 0);
                if ((pair.Key.Anchor & AnchorStyles.Left) != 0 && (pair.Key.Anchor & AnchorStyles.Right) != 0)
                    bounds.Width = pair.Value.Width + ClientSize.Width - 738;
                pair.Key.Bounds = bounds;
            }
            groupBoxModels.Height += 36;
            _modernBackendLabel = new Label { Left = 6, Top = 78, AutoSize = true, Text = ClassicSpeechSettings.L("Модуль", "Backend") };
            _modernBackend = new ComboBox { Name = "classicSpeechBackend", Left = 85, Top = 74, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            // Start with the backends verified on the Windows execution path. The
            // full stable catalog stays available for subsequent backend validation.
            foreach (var engine in ClassicSpeechCatalog.Engines.Where(e => e.Family == "crisp" &&
                new[] { "parakeet", "gigaam", "qwen3", "voxtral", "index-echo" }.Contains(e.Backend))) _modernBackend.Items.Add(engine);
            _modernBackend.SelectedItem = _modernBackend.Items.Cast<SpeechEngine>().FirstOrDefault(e => e.Backend == _modernOptions.Backend) ?? _modernBackend.Items[0];
            _modernBackend.SelectedIndexChanged += (s, e) => { if (!_modernUpdating && IsModernSpeech) RefreshModernSpeech(); };
            _modernStatus = new Label { Name = "classicSpeechRuntimeStatus", Left = 320, Top = 79, Width = groupBoxModels.Width - 330,
                Height = 18, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, AutoEllipsis = true };
            groupBoxModels.Controls.AddRange(new Control[] { _modernBackendLabel, _modernBackend, _modernStatus });
            _qualityButton = new Button { Text = ClassicSpeechSettings.L("Качество…", "Quality…"), Width = 145, Height = buttonAdvanced.Height,
                Left = ClientSize.Width - 160, Top = buttonAdvanced.Top, Anchor = AnchorStyles.Top | AnchorStyles.Right, Enabled = _subtitle?.Paragraphs.Count > 0 };
            _qualityButton.Click += (s, e) =>
            {
                var report = _qualityReport ?? AnalyzeSpeech(_subtitle);
                using (var form = new ClassicSpeechQualityForm(report)) form.ShowDialog(this);
            };
            Controls.Add(_qualityButton);
            labelAdvanced.AutoSize = false; labelAdvanced.AutoEllipsis = true;
            labelAdvanced.Width = Math.Max(100, _qualityButton.Left - labelAdvanced.Left - 10);
            labelProgress.AutoSize = false; labelProgress.Width = ClientSize.Width - 24; labelProgress.Height = 18;
            MinimumSize = new Size(Math.Max(800, MinimumSize.Width), Math.Max(380, MinimumSize.Height));
            foreach (var name in new[] { ClassicSpeechCatalog.Crisp, ClassicSpeechCatalog.Qwen, ClassicSpeechCatalog.CppVulkan }) comboBoxWhisperEngine.Items.Add(name);
            _modernReady = true;
            comboBoxModels.SelectedIndexChanged += (s, e) => SaveModernSelection();
            if (File.Exists(Path.Combine(ClassicSpeechCatalog.Root, "settings.json")) && comboBoxWhisperEngine.Items.Contains(_modernOptions.Engine))
                comboBoxWhisperEngine.SelectedItem = _modernOptions.Engine;
            HandleModernEngineChanged();
            _initialWidth = Width;
            ResumeLayout(true);
            LayoutModernFooter();
        }

        private void LayoutModernFooter()
        {
            if (!_modernReady) return;
            if (_selectedSpeech != null) return;
            labelElapsed.AutoSize = false; labelElapsed.Width = 110;
            labelElapsed.Left = ClientSize.Width - labelElapsed.Width - 12;
            labelElapsed.Top = labelProgress.Top;
            labelProgress.Width = Math.Max(100, labelElapsed.Left - labelProgress.Left - 12);
        }

        private bool HandleModernEngineChanged()
        {
            if (!_modernReady) return false;
            if (!IsModernSpeech)
            {
                _modernBackend.Visible = _modernBackendLabel.Visible = false;
                _modernStatus.Text = "";
                labelInfo.Text = LanguageSettings.Current.AudioToText.WhisperInfo;
                linkLabeWhisperWebSite.Text = LanguageSettings.Current.AudioToText.WhisperWebsite;
                checkBoxTranslateToEnglish.Enabled = true;
                return false;
            }
            RefreshModernSpeech(); return true;
        }

        private void RefreshModernSpeech()
        {
            if (_modernUpdating || !IsModernSpeech) return;
            _modernUpdating = true;
            try
            {
                var engine = ModernEngine;
                labelInfo.Text = ClassicSpeechSettings.L("Распознавание речи: ", "Speech recognition: ") + engine.Name;
                linkLabeWhisperWebSite.Text = ClassicSpeechSettings.L("Сайт движка", "Engine website");
                _modernOptions.Engine = comboBoxWhisperEngine.Text;
                _modernOptions.Backend = engine.Backend;
                if (engine.Family == "cpp") _modernOptions.Variant = "vulkan";
                else if (engine.Family == "whisperx") _modernOptions.Variant = "cpu";
                else if (engine.Family == "qwen" && _modernOptions.Variant != "vulkan") _modernOptions.Variant = "cpu";
                _modernBackend.Visible = _modernBackendLabel.Visible = engine.Family == "crisp";
                var language = (comboBoxLanguages.SelectedItem as SpeechLanguage)?.Code ?? _modernOptions.Language;
                comboBoxLanguages.Items.Clear();
                foreach (var item in engine.Languages) comboBoxLanguages.Items.Add(item);
                comboBoxLanguages.SelectedItem = engine.Languages.FirstOrDefault(l => l.Code == language) ??
                    engine.Languages.FirstOrDefault(l => l.Code == "auto") ?? engine.Languages.First();
                comboBoxModels.Items.Clear();
                var models = engine.Models.Where(m => !m.Aligner).ToList();
                foreach (var item in models) comboBoxModels.Items.Add(item);
                _modernOptions.LastModels.TryGetValue(engine.Name, out var selected);
                comboBoxModels.SelectedItem = models.FirstOrDefault(m => m.Name == selected) ??
                    (engine.Family == "whisperx" ? models.FirstOrDefault(m => m.Name == "base") : null) ??
                    models.FirstOrDefault(m => !EnglishOnly(m)) ?? models.First();
                checkBoxTranslateToEnglish.Enabled = engine.Family == "cpp" || engine.Family == "whisperx";
                checkBoxTranslateToEnglish.Checked = checkBoxTranslateToEnglish.Enabled && _modernOptions.TranslateToEnglish;
                buttonGenerate.Enabled = true;
                UpdateModernRuntimeStatus();
            }
            finally { _modernUpdating = false; }
        }

        private void SaveModernSelection()
        {
            if (!IsModernSpeech || _modernUpdating || _modernCancellation != null) return;
            var engine = ModernEngine;
            if (comboBoxModels.SelectedItem is SpeechModel model)
            {
                _modernOptions.Model = model.Name;
                _modernOptions.LastModels[engine.Name] = model.Name;
            }
            if (comboBoxLanguages.SelectedItem is SpeechLanguage language) _modernOptions.Language = language.Code;
            _modernOptions.TranslateToEnglish = checkBoxTranslateToEnglish.Enabled && checkBoxTranslateToEnglish.Checked;
            _modernOptions.Save(); UpdateModernRuntimeStatus();
        }

        private void UpdateModernRuntimeStatus()
        {
            var engine = ModernEngine;
            if (engine == null) return;
            var installed = File.Exists(ClassicSpeechCatalog.Executable(engine, _modernOptions.Variant));
            var model = comboBoxModels.SelectedItem as SpeechModel;
            var modelInstalled = model != null && File.Exists(ClassicSpeechCatalog.ModelPath(engine, model));
            _modernStatus.Text = "v" + ClassicSpeechCatalog.PinnedVersion(engine) + " · " + _modernOptions.Variant + " · " + ClassicSpeechSettings.L(installed ? "движок установлен" : "движок не скачан", installed ? "engine installed" : "engine not downloaded") +
                " · " + (engine.Family == "whisperx" ? ClassicSpeechSettings.L("модель загружается при запуске", "model loads on first use") :
                    ClassicSpeechSettings.L(modelInstalled ? "модель установлена" : "модель не скачана", modelInstalled ? "model installed" : "model not downloaded"));
            labelAdvanced.Text = _modernOptions.ExtraArguments.TryGetValue(engine.Name, out var extra) ? extra : "";
        }

        private SpeechModel ModernAligner(SpeechEngine engine)
        {
            if (engine.NativeTimings && string.IsNullOrEmpty(_modernOptions.Aligner)) return null;
            var models = engine.Family == "crisp" ? ClassicSpeechExtras.RankedAligners(_modernOptions.Language).Select(a => a.Model).ToList() : engine.Models.Where(m => m.Aligner).ToList();
            return models.FirstOrDefault(m => m.Name == _modernOptions.Aligner) ?? (engine.NativeTimings ? null : models.FirstOrDefault());
        }

        private async Task EnsureModernDependencies(SpeechEngine engine, SpeechModel model, CancellationToken cancel, IProgress<SpeechProgress> progress)
        {
            await ClassicSpeechDownload.EnsureRuntimeAsync(engine, _modernOptions.Variant, cancel, progress);
            await ClassicSpeechDownload.EnsureModelAsync(engine, model, cancel, progress);
            var aligner = ModernAligner(engine);
            if (aligner != null) await ClassicSpeechDownload.EnsureModelAsync(engine, aligner, cancel, progress);
            await ClassicSpeechExtras.EnsureAsync(engine, _modernOptions, cancel, progress);
        }

        private IProgress<SpeechProgress> ModernProgress(Stopwatch watch)
        {
            return new Progress<SpeechProgress>(p =>
            {
                if (IsDisposed || _modernCancellation == null) return;
                if (!string.IsNullOrEmpty(p.Message))
                {
                    textBoxLog.AppendText(p.Message + Environment.NewLine);
                    if (textBoxLog.TextLength > 200000) textBoxLog.Text = textBoxLog.Text.Substring(textBoxLog.TextLength - 150000);
                }
                var now = Stopwatch.GetTimestamp();
                if (now - _modernLastProgress < Stopwatch.Frequency / 10 && p.Percent != 100) return;
                _modernLastProgress = now;
                labelProgress.Text = p.Message?.Replace('\n', ' ').Replace('\r', ' ');
                if (p.Percent >= 0) { progressBar1.Style = ProgressBarStyle.Blocks; progressBar1.Value = Math.Max(0, Math.Min(100, p.Percent)); }
                labelElapsed.Text = watch.Elapsed.ToString(@"hh\:mm\:ss");
            });
        }

        private void BeginModernOperation()
        {
            _cancel = false; _modernCancellation = new CancellationTokenSource();
            _modernWatch = Stopwatch.StartNew(); _modernClock.Start();
            SetEnabledState(false); EnableGroupBoxInputFiles(false);
            comboBoxWhisperEngine.Enabled = false; _modernBackend.Enabled = false; _qualityButton.Enabled = false;
            progressBar1.Style = ProgressBarStyle.Marquee; progressBar1.Visible = true; textBoxLog.Clear();
        }

        private void EndModernOperation()
        {
            _modernClock.Stop();
            if (!string.IsNullOrWhiteSpace(textBoxLog.Text)) SeLogger.WhisperInfo(textBoxLog.Text);
            _modernCancellation?.Dispose(); _modernCancellation = null;
            if (IsDisposed) return;
            SetEnabledState(true); EnableGroupBoxInputFiles(true);
            comboBoxWhisperEngine.Enabled = true; _modernBackend.Enabled = true; _qualityButton.Enabled = _qualityReport != null;
            progressBar1.Style = ProgressBarStyle.Blocks; progressBar1.Visible = false;
            UpdateModernRuntimeStatus();
            if (_modernCloseRequested) Close();
        }

        private async Task DownloadModernSelection()
        {
            if (_modernCancellation != null) return;
            SaveModernSelection();
            var engine = ModernEngine; var model = comboBoxModels.SelectedItem as SpeechModel;
            if (model == null) return;
            BeginModernOperation();
            try { await EnsureModernDependencies(engine, model, _modernCancellation.Token, ModernProgress(Stopwatch.StartNew())); labelProgress.Text = ClassicSpeechSettings.L("Готово", "Ready"); }
            catch (OperationCanceledException) { labelProgress.Text = ClassicSpeechSettings.L("Загрузка отменена", "Download cancelled"); }
            catch (Exception ex) { ShowModernError(ex); }
            finally { EndModernOperation(); }
        }

        private async Task GenerateModernSpeech()
        {
            if (_modernCancellation != null) return;
            SaveModernSelection();
            var engine = ModernEngine; var model = comboBoxModels.SelectedItem as SpeechModel;
            if (model == null) return;
            if (EnglishOnly(model) && _modernOptions.Language != "en")
            {
                MessageBox.Show(this, ClassicSpeechSettings.L("Эта модель предназначена только для английского. Выбери English или многоязычную модель.",
                    "This model is English-only. Choose English or a multilingual model."), Text); return;
            }
            if ((model.Name.StartsWith("reazonspeech-",StringComparison.OrdinalIgnoreCase) || model.Name.Contains("-ja-")) && _modernOptions.Language != "ja")
            {MessageBox.Show(this,ClassicSpeechSettings.L("Эта модель предназначена для японского. Выбери Japanese.","This model is Japanese-only. Choose Japanese."),Text);return;}
            var inputs = _batchMode ? listViewInputFiles.Items.Cast<ListViewItem>().Select(i => i.Text).ToList() : new List<string> { _videoFileName };
            if (inputs.Count == 0 || inputs.Any(f => !File.Exists(f))) { MessageBox.Show(this, ClassicSpeechSettings.L("Выбери входной файл.", "Choose an input file."), Text); return; }
            BeginModernOperation();
            var completed = new List<string>();
            var singleSucceeded = false;
            var watch = Stopwatch.StartNew(); var progress = ModernProgress(watch);
            try
            {
                var cancel = _modernCancellation.Token;
                await EnsureModernDependencies(engine, model, cancel, progress);
                var aligner = ModernAligner(engine);
                var alignerPath = aligner == null ? null : ClassicSpeechCatalog.ModelPath(engine, aligner);
                foreach (var input in inputs)
                {
                    cancel.ThrowIfCancellationRequested();
                    labelProgress.Text = ClassicSpeechSettings.L("Извлечение аудио: ", "Extracting audio: ") + Path.GetFileName(input);
                    var wave = GenerateWavFile(input, _audioTrackNumber);
                    cancel.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(wave) || !File.Exists(wave)) throw new IOException("Audio extraction did not produce a WAV file.");
                    var job = Path.Combine(ClassicSpeechCatalog.Root, "Jobs", Guid.NewGuid().ToString("N"));
                    try
                    {
                        var transcript = await ClassicSpeechRunner.TranscribeAsync(engine, model, _modernOptions, wave, job, alignerPath, cancel, progress);
                        if (transcript.Paragraphs.Count == 0) throw new InvalidDataException(ClassicSpeechSettings.L("Движок не обнаружил речь. Проверь язык, модель и VAD.", "No speech detected. Check language, model and VAD."));
                        _qualityReport = new SpeechToTextQualityReport();
                        var previous = "";
                        var previousActor = "";
                        for (var i = 0; i < transcript.Paragraphs.Count; i++)
                        {
                            var p = transcript.Paragraphs[i];
                            var sound = SpeechToTextQualityReport.IsNonSpeechLine(p.Text);
                            var repeated = (p.Actor ?? "") == previousActor && SpeechToTextQualityReport.IsRepeatOf(p.Text, previous);
                            if (_modernOptions.RemoveNonSpeech && sound || _modernOptions.RemoveRepeats && repeated)
                            {
                                _qualityReport.Removed.Add(SpeechToTextQualityReport.MakeIssue(sound ? SpeechToTextQualityIssueType.NonSpeech : SpeechToTextQualityIssueType.Repeated, p, i + 1, "Removed"));
                                transcript.Paragraphs.RemoveAt(i--); continue;
                            }
                            previous = p.Text;
                            previousActor = p.Actor ?? "";
                        }
                        var post = new AudioToTextPostProcessor(_modernOptions.TranslateToEnglish ? "en" : _modernOptions.Language);
                        TranscribedSubtitle = ClassicSpeechExtras.ProcessByActor(transcript, actorGroup => post.Fix(AudioToTextPostProcessor.Engine.Whisper, actorGroup, checkBoxUsePostProcessing.Checked,
                            Configuration.Settings.Tools.WhisperPostProcessingAddPeriods, Configuration.Settings.Tools.WhisperPostProcessingMergeLines,
                            Configuration.Settings.Tools.WhisperPostProcessingFixCasing, Configuration.Settings.Tools.WhisperPostProcessingFixShortDuration,
                            Configuration.Settings.Tools.WhisperPostProcessingSplitLines));
                        if (checkBoxAutoAdjustTimings.Checked && _wavePeaks != null && !_batchMode)
                            TranscribedSubtitle = WhisperTimingFixer.ShortenViaWavePeaks(TranscribedSubtitle, _wavePeaks);
                        var settings = Configuration.Settings.General;
                        _qualityReport.Analyze(TranscribedSubtitle, settings.SubtitleMinimumDisplayMilliseconds, settings.SubtitleMaximumDisplayMilliseconds, settings.SubtitleMaximumCharactersPerSeconds);
                        if (_batchMode)
                        {
                            var suffix = string.IsNullOrEmpty(engine.Backend) ? engine.Family : engine.Backend;
                            var stem = Path.Combine(Path.GetDirectoryName(input), Path.GetFileNameWithoutExtension(input) + ".asr." + suffix);
                            SubtitleFormat outputFormat = TranscribedSubtitle.Paragraphs.Any(p => !string.IsNullOrEmpty(p.Actor)) ? (SubtitleFormat)new AdvancedSubStationAlpha() : new SubRip();
                            var output = stem + outputFormat.Extension;
                            for (var number = 2; File.Exists(output); number++) output = stem + "." + number + outputFormat.Extension;
                            File.WriteAllText(output, outputFormat.ToText(TranscribedSubtitle, null), new UTF8Encoding(false)); completed.Add(output);
                        }
                    }
                    finally { if (Directory.Exists(job)) Directory.Delete(job, true); }
                }
                if (_batchMode) MessageBox.Show(this, ClassicSpeechSettings.L("Сохранено файлов: ", "Files saved: ") + completed.Count + Environment.NewLine + string.Join(Environment.NewLine, completed), Text);
                else
                {
                    if (_qualityReport.HasIssues) using (var report = new ClassicSpeechQualityForm(_qualityReport)) report.ShowDialog(this);
                    cancel.ThrowIfCancellationRequested(); singleSucceeded = true;
                }
            }
            catch (OperationCanceledException) { labelProgress.Text = ClassicSpeechSettings.L("Распознавание отменено", "Transcription cancelled"); }
            catch (Exception ex) { ShowModernError(ex); }
            finally { EndModernOperation(); }
            if (singleSucceeded && !IsDisposed) DialogResult = DialogResult.OK;
        }

        private static bool EnglishOnly(SpeechModel model) => model.Name.Contains(".en") || model.Name.StartsWith("distil-", StringComparison.OrdinalIgnoreCase) || model.Name.StartsWith("phonon2-", StringComparison.OrdinalIgnoreCase);

        private void ShowModernError(Exception exception)
        {
            textBoxLog.AppendText(Environment.NewLine + exception);
            var summary = exception.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? exception.GetType().Name;
            MessageBox.Show(this, summary + Environment.NewLine + ClassicSpeechSettings.L("Подробности сохранены в журнале Whisper.", "Details are saved in the Whisper log."), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static SpeechToTextQualityReport AnalyzeSpeech(Subtitle subtitle)
        {
            var report = new SpeechToTextQualityReport(); var settings = Configuration.Settings.General;
            report.Analyze(subtitle ?? new Subtitle(), settings.SubtitleMinimumDisplayMilliseconds, settings.SubtitleMaximumDisplayMilliseconds, settings.SubtitleMaximumCharactersPerSeconds);
            return report;
        }

        private bool CancelModernSpeech()
        {
            if (_modernCancellation == null) return false;
            _cancel = true; _modernCancellation.Cancel(); return true;
        }

        private bool ModernClosing(FormClosingEventArgs args)
        {
            if (_modernCancellation == null) return false;
            _modernCloseRequested = true; CancelModernSpeech(); args.Cancel = true; return true;
        }
    }
}
