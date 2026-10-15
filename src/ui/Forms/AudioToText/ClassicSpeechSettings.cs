using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms.AudioToText
{
    public sealed class ClassicSpeechSettings : Form
    {
        internal static string L(string ru, string en) => Configuration.Settings.General.Language.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? ru : en;
        public ClassicSpeechSettings(SpeechEngine engine, ClassicSpeechOptions options)
        {
            Text = L("Параметры распознавания речи", "Speech-to-text settings");
            StartPosition = FormStartPosition.CenterParent; MinimumSize = new Size(680, 530); ClientSize = new Size(700, 500);
            UiUtil.PreInitialize(this);
            var label = new Label { Text = L("Режим вычислений", "Compute backend"), AutoSize = true, Left = 15, Top = 20 };
            var variant = new ComboBox { Left = 230, Top = 16, Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
            var variants = engine.Family == "crisp" ? new[] { "cpu", "cpu-legacy", "vulkan", "cuda13" } :
                engine.Family == "qwen" ? new[] { "cpu", "vulkan" } : engine.Family == "cpp" ? new[] { "vulkan" } : new[] { "cpu" };
            variant.Items.AddRange(variants); variant.SelectedItem = variants.Contains(options.Variant) ? options.Variant : variants[0];
            var alignerLabel = new Label { Text = L("Выравнивание таймингов", "Forced aligner"), AutoSize = true, Left = 15, Top = 55 };
            var aligner = new ComboBox { Left = 230, Top = 51, Width = 410, DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var alignerModels = engine.Models.Where(m => m.Aligner).ToList();
            if (engine.Family == "crisp") alignerModels = ClassicSpeechExtras.RankedAligners(options.Language).Select(a => a.Model).ToList();
            aligner.Items.Add(L("Встроенное (native timings)", "Built-in (native timings)"));
            foreach (var model in alignerModels) aligner.Items.Add(model);
            aligner.SelectedIndex = 0;
            for (var i = 1; i < aligner.Items.Count; i++) if (((SpeechModel)aligner.Items[i]).Name == options.Aligner) aligner.SelectedIndex = i;
            if (!engine.NativeTimings && aligner.Items.Count > 1 && aligner.SelectedIndex == 0) aligner.SelectedIndex = 1;
            aligner.Enabled = engine.Family == "crisp" || engine.Family == "qwen";
            var vad = new CheckBox { Text = L("VAD — обнаружение участков речи", "VAD — voice activity detection"), Checked = options.UseVad, Left = 15, Top = 90, AutoSize = true, Enabled = engine.Family == "crisp" };
            vad.Visible = engine.Family != "whisperx";
            if (ClassicSpeechExtras.NeedsVad(engine)) {vad.Checked = true; vad.Enabled = false;}
            var vadModel = new ComboBox {Name = "comboVadModel", Left = 420, Top = 86, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, Visible = engine.Family != "whisperx", Enabled = engine.Family == "crisp" && engine.Backend != "index-echo"};
            vadModel.Items.AddRange(ClassicSpeechExtras.VadNames); var vadChoice = engine.Backend == "index-echo" ? "silero" : options.Vad; vadModel.SelectedIndex = Math.Max(0, Array.IndexOf(ClassicSpeechExtras.VadChoices, vadChoice));
            var isolate = new CheckBox {Name = "checkIsolateSpeech", Text = L("Выделить речь перед распознаванием (Mel-Band RoFormer)", "Isolate speech before recognition (Mel-Band RoFormer)"), Checked = options.IsolateSpeech, Left = 15, Top = 150, AutoSize = true, Enabled = engine.Family == "crisp"};
            var speakers = new CheckBox {Name = "checkDetectSpeakers", Text = L("Определить говорящих (Sortformer)", "Detect speakers (Sortformer)"), Checked = options.DetectSpeakers, Left = 15, Top = 180, AutoSize = true, Enabled = ClassicSpeechExtras.CanDiarize(engine)};
            var alignWords = new CheckBox { Text = L("WhisperX: уточнять тайминги по словам (нужна модель выравнивания)", "WhisperX: align word timings (requires an alignment model)"), Checked = options.WhisperXAlignWords,
                Left = 15, Top = 90, AutoSize = true, Visible = engine.Family == "whisperx" };
            var retry = new CheckBox { Text = L("Повторить без VAD, если результат пустой", "Retry without VAD when the result is empty"), Checked = options.RetryWithoutVad, Left = 15, Top = 120, AutoSize = true, Enabled = engine.Family == "crisp" };
            var sounds = new CheckBox { Text = L("Удалять строки, содержащие только звуки/музыку", "Remove sound/music-only lines"), Checked = options.RemoveNonSpeech, Left = 15, Top = 210, AutoSize = true };
            var repeats = new CheckBox { Text = L("Удалять подряд повторяющиеся строки", "Remove consecutive repeated lines"), Checked = options.RemoveRepeats, Left = 15, Top = 240, AutoSize = true };
            var argumentsLabel = new Label { Text = L("Дополнительные параметры этого движка", "Extra arguments for this engine"), Left = 15, Top = 280, AutoSize = true };
            var arguments = new TextBox { Left = 15, Top = 305, Width = 660, Height = 110, Multiline = true, ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Text = options.ExtraArguments.TryGetValue(engine.Name, out var extra) ? extra : "" };
            var ok = new Button { Text = "OK", Width = 110, Left = 440, Top = 450, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            var cancel = new Button { Text = L("Отмена", "Cancel"), Width = 110, Left = 565, Top = 450, Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            ok.Click += (s, e) =>
            {
                options.Variant = (string)variant.SelectedItem;
                options.Aligner = aligner.SelectedItem is SpeechModel selected ? selected.Name : "";
                options.UseVad = vad.Checked; options.RetryWithoutVad = retry.Checked;
                options.Vad = ClassicSpeechExtras.VadChoices[vadModel.SelectedIndex]; options.IsolateSpeech = isolate.Checked; options.DetectSpeakers = speakers.Checked;
                options.WhisperXAlignWords = alignWords.Checked;
                options.RemoveNonSpeech = sounds.Checked; options.RemoveRepeats = repeats.Checked;
                options.ExtraArguments[engine.Name] = arguments.Text.Trim();
                options.Save(); DialogResult = DialogResult.OK;
            };
            Controls.AddRange(new Control[] { label, variant, alignerLabel, aligner, vad, vadModel, alignWords, retry, isolate, speakers, sounds, repeats, argumentsLabel, arguments, ok, cancel });
            AcceptButton = ok; CancelButton = cancel; UiUtil.FixFonts(this);
        }
    }

    public sealed class ClassicSpeechQualityForm : Form
    {
        public ClassicSpeechQualityForm(SpeechToTextQualityReport report)
        {
            Text = ClassicSpeechSettings.L("Качество распознавания речи", "Transcription quality");
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(850, 460); MinimumSize = new Size(650, 330);
            UiUtil.PreInitialize(this);
            var summary = new Label { Left = 12, Top = 12, Width = 820, Height = 42, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Text = ClassicSpeechSettings.L("Всего строк: ", "Total lines: ") + report.TotalLines + Environment.NewLine + report.ToLogString() };
            var grid = new DataGridView { Left = 12, Top = 60, Width = 826, Height = 340, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            grid.Columns.Add("number", "#"); grid.Columns[0].Width = 45;
            grid.Columns.Add("time", ClassicSpeechSettings.L("Начало", "Start")); grid.Columns[1].Width = 100;
            grid.Columns.Add("issue", ClassicSpeechSettings.L("Проверка", "Issue")); grid.Columns[2].Width = 145;
            grid.Columns.Add("detail", ClassicSpeechSettings.L("Подробности", "Details")); grid.Columns[3].Width = 105;
            grid.Columns.Add("text", ClassicSpeechSettings.L("Текст", "Text")); grid.Columns[4].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            foreach (var issue in report.Removed.Concat(report.Issues))
                grid.Rows.Add(issue.Number, issue.StartTime.ToDisplayString(), issue.Type, issue.Detail, issue.Text);
            var export = new Button { Text = ClassicSpeechSettings.L("Сохранить отчёт…", "Export report…"), Left = 12, Top = 423, Width = 160, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            export.Click += (s, e) =>
            {
                using (var save = new SaveFileDialog { Filter = "Text|*.txt", FileName = "transcription-quality.txt" })
                    if (save.ShowDialog(this) == DialogResult.OK)
                        System.IO.File.WriteAllLines(save.FileName, new[] { summary.Text }.Concat(report.Removed.Concat(report.Issues).Select(i => i.Number + "\t" + i.StartTime.ToDisplayString() + "\t" + i.Type + "\t" + i.Detail + "\t" + i.Text)));
            };
            var close = new Button { Text = ClassicSpeechSettings.L("Продолжить", "Continue"), Left = 718, Top = 423, Width = 120, Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.OK };
            Controls.AddRange(new Control[] { summary, grid, export, close }); AcceptButton = close; UiUtil.FixFonts(this);
        }
    }
}
