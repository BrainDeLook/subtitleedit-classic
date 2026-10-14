using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class GenerateVideoWithHardSubs
    {
        private ClassicBurnInOptions _classicOptions;
        private ComboBox _classicContainer;
        private Label _classicSummary;
        private Button _classicAdvanced;
        private bool _classicUpdating;
        private string _classicImageSubtitle;
        private string _classicInputFile;
        private bool _classicFooterLayout;
        private string ClassicSettingsPath => Path.Combine(Configuration.DataDirectory, "ClassicBurnIn.xml");
        private string CT(string ru, string en) => LanguageSettings.Current.General.CultureName.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? ru : en;

        private void InitializeClassicBurnIn()
        {
            _classicOptions = ClassicBurnInOptions.Load(ClassicSettingsPath);
            // Give the status rows their own space without reducing the classic settings area.
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + 20);
            MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height + 20);
            groupBoxSettings.Height -= 20;
            var button = new Button { Name = "buttonClassicAdvanced", Text = CT("Дополнительно…", "Advanced…"), Size = new Size(144, buttonGenerate.Height), Location = new Point(groupBoxSettings.Left, buttonGenerate.Top), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            button.Click += ShowClassicAdvanced;
            Controls.Add(button);
            _classicAdvanced = button;
            buttonGenerate.EnabledChanged += (s, e) => { button.Enabled = buttonGenerate.Enabled; button.Visible = buttonGenerate.Enabled; if (_classicSummary != null) _classicSummary.Visible = buttonGenerate.Enabled; buttonMode.Enabled = buttonGenerate.Enabled; buttonPreview.Enabled = buttonGenerate.Enabled; LayoutClassicFooter(); };
            _classicSummary = new Label { Name = "labelClassicSummary", Location = new Point(button.Right + 12, button.Top + 4), Size = new Size(330, 20), Anchor = AnchorStyles.Bottom | AnchorStyles.Left, ForeColor = SystemColors.GrayText, AutoEllipsis = true };
            Controls.Add(_classicSummary);
            var label = new Label { Name = "labelClassicContainer", Text = CT("Формат", "Format"), AutoSize = true, Location = new Point(comboBoxTune.Right + 8, comboBoxTune.Top + 4) };
            var containerLeft = label.Left + TextRenderer.MeasureText(label.Text, Font).Width + 4;
            _classicContainer = new ComboBox { Name = "comboBoxClassicContainer", DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(containerLeft, comboBoxTune.Top), Size = new Size(Math.Max(75, groupBoxVideo.ClientSize.Width - containerLeft - 16), 23), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            groupBoxVideo.Controls.Add(label);
            groupBoxVideo.Controls.Add(_classicContainer);
            _classicContainer.SelectedIndexChanged += (s, e) => { if (_classicUpdating) return; _classicOptions.Container = _classicContainer.Text; FillClassicAudio(); };
            comboBoxVideoEncoding.SelectedIndexChanged += (s, e) => FillClassicContainers();
            FillClassicContainers();
            UpdateClassicSummary();
            checkBoxBox.CheckedChanged += (s, e) => { if (!_classicUpdating && _classicOptions.UseTextStyle) { _classicOptions.BoxType = checkBoxBox.Checked ? 2 : 0; UpdateVideoPreview(); } };
            checkBoxAlignRight.CheckedChanged += (s, e) => { if (!_classicUpdating && _classicOptions.UseTextStyle) { _classicOptions.Alignment = checkBoxAlignRight.Checked ? 3 : 2; UpdateVideoPreview(); } };
            FormClosed += (s, e) => { try { _classicOptions.Save(ClassicSettingsPath); } catch (Exception ex) { SeLogger.Error(ex, "Saving Classic burn-in options"); } };
            // Probe the FFmpeg build without assuming that every listed hardware encoder is present.
            Shown += (s, e) => TaskDelayHelper.RunDelayed(TimeSpan.FromMilliseconds(10), FilterClassicEncoders);
            Shown += (s, e) => LayoutClassicFooter();
            Resize += (s, e) => LayoutClassicFooter();
            labelProgress.FontChanged += (s, e) => LayoutClassicFooter();
            labelProgress.TextChanged += (s, e) => LayoutClassicFooter();
            labelPass.TextChanged += (s, e) => LayoutClassicFooter();
            LayoutClassicFooter();
        }

        private void LayoutClassicFooter()
        {
            if (_classicFooterLayout) return;
            _classicFooterLayout = true;
            try
            {
                var statusHeight = Math.Max(labelProgress.PreferredSize.Height, TextRenderer.MeasureText("Ag", labelProgress.Font).Height);
                labelProgress.AutoSize = false;
                labelProgress.AutoEllipsis = true;
                labelProgress.SetBounds(progressBar1.Left, ClientSize.Height - 8 - statusHeight, progressBar1.Width, statusHeight);
                progressBar1.Top = labelProgress.Top - 4 - progressBar1.Height;
                labelPleaseWait.Top = progressBar1.Top - 5 - labelPleaseWait.PreferredSize.Height;
                // A fixed-width empty label still paints its background over the idle toolbar.
                labelProgress.Visible = !string.IsNullOrEmpty(labelProgress.Text);
                if (buttonGenerate.Enabled && labelProgress.Visible) labelProgress.Top = labelPleaseWait.Top;
                labelPass.Top = labelPleaseWait.Top;
                labelPass.Left = progressBar1.Right - labelPass.PreferredSize.Width;
                labelFileName.AutoSize = false;
                labelFileName.AutoEllipsis = true;
                labelFileName.SetBounds(labelFileName.Left, labelPleaseWait.Top - 6 - TextRenderer.MeasureText("Ag", labelFileName.Font).Height, Math.Max(1, linkLabelHelp.Left - labelFileName.Left - 8), TextRenderer.MeasureText("Ag", labelFileName.Font).Height);
                if (_classicAdvanced != null && _classicSummary != null)
                {
                    _classicAdvanced.Top = buttonGenerate.Top;
                    _classicAdvanced.Height = buttonGenerate.Height;
                    var summaryHeight = TextRenderer.MeasureText("Ag", _classicSummary.Font).Height;
                    var summaryLeft = _classicAdvanced.Right + 12;
                    _classicSummary.SetBounds(summaryLeft, _classicAdvanced.Top + Math.Max(0, (_classicAdvanced.Height - summaryHeight) / 2), Math.Max(20, buttonGenerate.Left - summaryLeft - 12), summaryHeight);
                }
            }
            finally { _classicFooterLayout = false; }
        }

        private void FillClassicContainers()
        {
            if (_classicContainer == null || _classicUpdating) return;
            _classicUpdating = true;
            var wanted = _classicOptions.Container;
            var items = ClassicBurnIn.GetContainers(comboBoxVideoEncoding.Text);
            _classicContainer.Items.Clear();
            _classicContainer.Items.AddRange(items);
            _classicContainer.SelectedItem = items.Contains(wanted) ? wanted : items[0];
            _classicOptions.Container = _classicContainer.Text;
            _classicUpdating = false;
            FillClassicAudio();
        }

        private void FillClassicAudio()
        {
            var wanted = comboBoxAudioEnc.Text;
            var items = ClassicBurnIn.GetAudioEncoders(_classicOptions.Container);
            comboBoxAudioEnc.Items.Clear();
            comboBoxAudioEnc.Items.AddRange(items);
            comboBoxAudioEnc.SelectedItem = ClassicBurnIn.GetAudioEncoder(_classicOptions.Container, wanted);
            comboBoxAudioEnc_SelectedIndexChanged(null, EventArgs.Empty);
        }

        private void FilterClassicEncoders()
        {
            var location = Configuration.Settings.General.FFmpegLocation;
            if (IsDisposed || _converting || !buttonGenerate.Enabled || !File.Exists(location)) return;
            try
            {
                using (var process = new Process { StartInfo = new ProcessStartInfo(location, "-hide_banner -encoders") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } })
                {
                    process.Start();
                    var output = process.StandardOutput.ReadToEndAsync();
                    var errors = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(3000)) { process.Kill(); return; }
                    var available = new HashSet<string>(output.Result.SplitToLines().Select(line => line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).Where(parts => parts.Length > 1 && parts[0].Length == 6).Select(parts => parts[1]));
                    if (available.Count == 0) return;
                    var wanted = comboBoxVideoEncoding.Text;
                    var preset = comboBoxPreset.Text;
                    var crf = comboBoxCrf.Text;
                    var tune = comboBoxTune.Text;
                    var supported = comboBoxVideoEncoding.Items.Cast<object>().Select(item => item.ToString()).Where(available.Contains).ToArray();
                    if (supported.Length == 0 || supported.Length == comboBoxVideoEncoding.Items.Count) return;
                    comboBoxVideoEncoding.Items.Clear();
                    comboBoxVideoEncoding.Items.AddRange(supported);
                    comboBoxVideoEncoding.Text = supported.Contains(wanted) ? wanted : supported[0];
                    if (supported.Contains(wanted)) { comboBoxPreset.Text = preset; comboBoxCrf.Text = crf; comboBoxTune.Text = tune; }
                }
            }
            catch (Exception ex) { SeLogger.Error(ex, "Probing burn-in encoders"); }
        }

        private void ShowClassicAdvanced(object sender, EventArgs e)
        {
            using (var dialog = new ClassicBurnInAdvanced(_classicOptions, _isAssa && !BatchMode, _inputVideoFileName, (int)numericUpDownWidth.Value, (int)numericUpDownHeight.Value))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _classicOptions = dialog.Options;
                _classicUpdating = true;
                if (_classicOptions.UseTextStyle) { checkBoxBox.Checked = _classicOptions.BoxType != 0; checkBoxAlignRight.Checked = _classicOptions.Alignment == 3; }
                _classicUpdating = false;
                if (_classicOptions.MatchSourceFileSize) checkBoxTargetFileSize.Checked = true;
                UpdateClassicSummary();
                UpdateVideoPreview();
            }
        }

        private void UpdateClassicSummary()
        {
            var parts = new List<string>();
            if (_classicOptions.UseTextStyle) parts.Add(CT("Оформление", "Styling"));
            if (!string.IsNullOrEmpty(_classicOptions.Effects)) parts.Add(CT("Эффекты", "Effects"));
            if (!string.IsNullOrEmpty(_classicOptions.LogoFileName)) parts.Add(CT("Логотип", "Logo"));
            if (!string.IsNullOrEmpty(_classicOptions.ImageSubtitleFileName)) parts.Add("SUP");
            if (_classicOptions.MatchSourceFileSize) parts.Add(CT("Размер исходника", "Source size"));
            if (_classicOptions.StereoMode != 0) parts.Add(_classicOptions.StereoMode == 1 ? "3D SBS" : "3D TB");
            if (_classicOptions.StereoMode != 0) parts.Add(CT("Проверка: кнопка «Предпросмотр»", "Check with Preview button"));
            _classicSummary.Text = string.Join(" · ", parts);
            numericUpDownTargetFileSize.Enabled = !_classicOptions.MatchSourceFileSize;
            buttonPreview.Visible = !_mpvOn || _classicOptions.StereoMode != 0 || !string.IsNullOrWhiteSpace(_classicOptions.LogoFileName) || !string.IsNullOrWhiteSpace(_classicOptions.ImageSubtitleFileName);
        }

        private string ClassicSaveFilter() => string.Join("|", ClassicBurnIn.GetContainers(comboBoxVideoEncoding.Text).Select(extension => extension.TrimStart('.').ToUpperInvariant() + "|*" + extension));

        private bool ValidateClassicOutput(string output)
        {
            var extension = Path.GetExtension(output).ToLowerInvariant();
            if (!ClassicBurnIn.GetContainers(comboBoxVideoEncoding.Text).Contains(extension))
            {
                MessageBox.Show(this, CT("Выбери контейнер из списка «Формат» для этого видеокодека.", "Choose a container from the Format list for this video encoder."), Text);
                return false;
            }
            _classicContainer.SelectedItem = extension;
            foreach (var file in new[] { _classicOptions.LogoFileName, _classicImageSubtitle })
                if (!string.IsNullOrWhiteSpace(file) && !File.Exists(file)) { MessageBox.Show(this, CT("Не найден файл: ", "File not found: ") + file, Text); return false; }
            // A copy operation still needs the source codec to be compatible with the chosen container.
            if (comboBoxAudioEnc.Text == "copy" && _mediaInfo != null)
            {
                var audio = _mediaInfo.Tracks.FirstOrDefault(track => track.TrackType == FfmpegTrackType.Audio);
                var info = audio == null ? string.Empty : audio.TrackInfo.ToLowerInvariant();
                var copyInvalid = extension == ".mp4" && (info.Contains("pcm_") || info.Contains("truehd") || info.Contains("dts")) || extension == ".mov" && info.Contains("opus");
                if (copyInvalid) { MessageBox.Show(this, CT("Этот звук нельзя скопировать в выбранный контейнер. Выбери AAC/AC-3 или контейнер MKV.", "The source audio cannot be copied to this container. Choose AAC/AC-3 or MKV."), Text); return false; }
            }
            return true;
        }

        private void PrepareClassicSubtitle(Subtitle subtitle)
        {
            if (_classicOptions != null)
                ClassicBurnIn.ApplyEffects(subtitle, _classicOptions, (int)numericUpDownWidth.Value, (int)numericUpDownHeight.Value, (int)numericUpDownFontSize.Value);
        }

        private ClassicBurnInOptions ClassicProcessOptions(bool preview)
        {
            var options = _classicOptions?.Clone() ?? new ClassicBurnInOptions();
            options.ImageSubtitleFileName = preview ? _classicOptions.ImageSubtitleFileName : _classicImageSubtitle;
            options.InputIsAudioOnly = !preview && _mediaInfo != null && _mediaInfo.Tracks.Any(t => t.TrackType == FfmpegTrackType.Audio) && !_mediaInfo.Tracks.Any(t => t.TrackType == FfmpegTrackType.Video);
            return options;
        }

        private void PrepareClassicInput(string inputFileName)
        {
            _classicInputFile = inputFileName;
            if (_mediaInfo != null && _mediaInfo.Tracks.Any(t => t.TrackType == FfmpegTrackType.Audio) && !_mediaInfo.Tracks.Any(t => t.TrackType == FfmpegTrackType.Video))
            {
                var seconds = _mediaInfo.Duration?.TotalSeconds ?? 0;
                _videoInfo = new VideoInfo { Width = (int)Math.Max(1920, numericUpDownWidth.Value), Height = (int)Math.Max(1080, numericUpDownHeight.Value), TotalSeconds = seconds, TotalMilliseconds = seconds * 1000, FramesPerSecond = 25, TotalFrames = seconds * 25, Success = true };
                if (numericUpDownWidth.Value == 0) numericUpDownWidth.Value = 1920;
                if (numericUpDownHeight.Value == 0) numericUpDownHeight.Value = 1080;
            }
            if (_classicOptions.MatchSourceFileSize)
            {
                checkBoxTargetFileSize.Checked = true;
                var size = (decimal)new FileInfo(inputFileName).Length / 1024 / 1024;
                numericUpDownTargetFileSize.Value = Math.Max(numericUpDownTargetFileSize.Minimum, Math.Min(numericUpDownTargetFileSize.Maximum, size));
            }
        }

        private bool ShowClassicCompositePreview()
        {
            if (_classicOptions == null || _classicOptions.StereoMode == 0 && string.IsNullOrWhiteSpace(_classicOptions.LogoFileName) && string.IsNullOrWhiteSpace(_classicOptions.ImageSubtitleFileName)) return false;
            if (!File.Exists(_inputVideoFileName)) return true;
            var assFile = Path.Combine(Path.GetTempPath(), "classic" + Guid.NewGuid().ToString("N") + ".ass");
            var videoFile = Path.ChangeExtension(assFile, ".mp4");
            try
            {
                var subtitle = new Subtitle(_assaSubtitle);
                if (!_isAssa) SetStyleForNonAssa(subtitle);
                FixRightToLeft(subtitle);
                PrepareClassicSubtitle(subtitle);
                var start = _assaSubtitle.Paragraphs.Count > 0 ? Math.Max(0, _assaSubtitle.Paragraphs[0].StartTime.TotalSeconds) : 0;
                subtitle.AddTimeToAllParagraphs(TimeSpan.FromSeconds(-start));
                File.WriteAllText(assFile, new AdvancedSubStationAlpha().ToText(subtitle, null), new UTF8Encoding(false));
                var options = _classicOptions.Clone();
                options.InputIsAudioOnly = _mediaInfo != null && !_mediaInfo.Tracks.Any(t => t.TrackType == FfmpegTrackType.Video);
                var cutStart = "-ss " + start.ToString("0.###", CultureInfo.InvariantCulture);
                using (var process = VideoPreviewGenerator.GenerateHardcodedVideoFile(_inputVideoFileName, assFile, videoFile, (int)numericUpDownWidth.Value, (int)numericUpDownHeight.Value, "libx264", "ultrafast", "yuv420p", "23", "aac", true, "48000", string.Empty, "128k", string.Empty, string.Empty, null, cutStart, "-t 3", string.Empty, options))
                {
                    Cursor = Cursors.WaitCursor;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();
                    var error = process.StandardError.ReadToEndAsync();
                    var output = process.StandardOutput.ReadToEndAsync();
                    var timer = Stopwatch.StartNew();
                    while (!process.WaitForExit(100)) { Application.DoEvents(); if (timer.Elapsed.TotalSeconds > 45) { process.Kill(); throw new InvalidOperationException(CT("Предпросмотр не завершился за 45 секунд.", "Preview exceeded 45 seconds.")); } }
                    if (process.ExitCode != 0) throw new InvalidOperationException(error.Result);
                }
                Cursor = Cursors.Default;
                using (var preview = new PreviewVideo(videoFile, string.Empty, new Subtitle())) preview.ShowDialog(this);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
            finally { Cursor = Cursors.Default; foreach (var file in new[] { assFile, videoFile }) if (File.Exists(file)) try { File.Delete(file); } catch (IOException) { } }
            return true;
        }
    }
}
