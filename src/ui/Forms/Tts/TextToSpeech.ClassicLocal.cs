using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms.Tts
{
    public sealed partial class TextToSpeech
    {
        private Button _localDownload, _localSettings;
        private CancellationTokenSource _localCancellation;
        private static string LocalId(TextToSpeechEngineId id) => id == TextToSpeechEngineId.CosyVoice3 ? "cosy" : id == TextToSpeechEngineId.VoxCpm2 ? "vox" : id == TextToSpeechEngineId.Chatterbox ? "chatter" : null;
        private string SelectedLocalId => LocalId(_engines.First(e => e.Index == nikseComboBoxEngine.SelectedIndex).Id);
        private void FillLocalControls()
        {
            var id = SelectedLocalId;
            var settings = ClassicLocalTtsSettings.Load(id);
            var definition = ClassicLocalTts.Catalog.First(e => e.Id == id);
            nikseComboBoxVoice.Items.AddRange(ClassicLocalTts.Voices(id));
            nikseComboBoxVoice.SelectedItem = nikseComboBoxVoice.Items.Cast<object>().FirstOrDefault(v => (v as ClassicQwenReference)?.Id == settings.Voice || v.ToString() == settings.Voice);
            if (nikseComboBoxVoice.SelectedIndex < 0 && nikseComboBoxVoice.Items.Count > 0) nikseComboBoxVoice.SelectedIndex = 0;
            labelRegion.Text = CL("Вычисления", "Compute");
            labelRegion.Visible = nikseComboBoxRegion.Visible = true;
            nikseComboBoxRegion.Items.Clear(); nikseComboBoxRegion.Items.AddRange(new object[] {"cpu", "cpu-legacy", "vulkan", "cuda", "cuda13"}); nikseComboBoxRegion.Text = settings.Variant;
            var languages = definition.LanguageChoices(settings.Model);
            labelLanguage.Visible = nikseComboBoxLanguage.Visible = languages.Length > 0;
            nikseComboBoxLanguage.Items.Clear();
            foreach (var pair in languages) {var parts = pair.Split('|'); nikseComboBoxLanguage.Items.Add(new TtsLanguage(parts[1], parts[0]));}
            nikseComboBoxLanguage.SelectedItem = nikseComboBoxLanguage.Items.Cast<TtsLanguage>().FirstOrDefault(l => l.Code == settings.Language) ?? nikseComboBoxLanguage.Items.Cast<TtsLanguage>().FirstOrDefault();
            labelApiKey.Text = CL("Скорость", "Speed");
            labelApiKey.Visible = nikseTextBoxApiKey.Visible = id != "chatter";
            nikseTextBoxApiKey.ReadOnly = false; nikseTextBoxApiKey.Text = settings.Speed.ToString(CultureInfo.CurrentCulture);
            if (_localDownload == null)
            {
                var available = nikseComboBoxVoice.Right - buttonTestVoice.Right - 10;
                _localDownload = new Button {Name = "buttonLocalDownload", Text = CL("Скачать модель", "Download model"), Location = new Point(buttonTestVoice.Right + 5, buttonTestVoice.Top), Size = new Size(available / 2, buttonTestVoice.Height)};
                _localSettings = new Button {Name = "buttonLocalSettings", Text = CL("Настройки…", "Settings…"), Location = new Point(_localDownload.Right + 5, buttonTestVoice.Top), Size = new Size(available - available / 2, buttonTestVoice.Height)};
                _localDownload.Click += async (s, e) => await DownloadLocalAsync();
                _localSettings.Click += (s, e) => EditLocalSettings();
                groupBoxSettings.Controls.AddRange(new Control[] {_localDownload, _localSettings});
            }
            labelProgress.Text = definition.Name.Split('(')[0].Trim() + " / " + definition.Model(settings.Model);
            labelProgress.AutoSize = false; labelProgress.AutoEllipsis = true;
            labelProgress.Width = Math.Max(80, buttonGenerateTTS.Left - labelProgress.Left - 8);
            labelProgress.Height = buttonGenerateTTS.Height;
        }
        private ClassicLocalTtsSettings CaptureLocal(bool requireVoice)
        {
            var id = SelectedLocalId;
            var settings = ClassicLocalTtsSettings.Load(id);
            settings.Variant = nikseComboBoxRegion.Text;
            settings.Voice = (nikseComboBoxVoice.SelectedItem as ClassicQwenReference)?.Id ?? nikseComboBoxVoice.Text;
            settings.Language = (nikseComboBoxLanguage.SelectedItem as TtsLanguage)?.Code ?? "auto";
            if (id != "chatter")
            {
                if (!double.TryParse(nikseTextBoxApiKey.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var speed) || double.IsNaN(speed) || speed < 0.25 || speed > 4)
                {if (requireVoice) throw new ArgumentException(CL("Скорость должна быть от 0,25 до 4", "Speed must be between 0.25 and 4"));}
                else settings.Speed = speed;
            }
            if (requireVoice && !ClassicLocalTts.Catalog.First(e => e.Id == id).Presets.Contains(settings.Voice)) ClassicQwenReference.Validate(settings.Voice);
            settings.Save(id); return settings;
        }
        private void EditLocalSettings()
        {
            var id = SelectedLocalId; var settings = CaptureLocal(false);
            using (var dialog = new ClassicLocalTtsSettingsForm(id, settings))
                if (dialog.ShowDialog(this) == DialogResult.OK) nikseComboBoxEngine_SelectedIndexChanged(null, null);
        }
        private void SetLocalBusy(bool busy)
        {
            SetQwenBusy(busy);
            if (_localDownload != null) _localDownload.Enabled = !busy;
            if (_localSettings != null) _localSettings.Enabled = !busy;
        }
        private async Task DownloadLocalAsync()
        {
            if (_localCancellation != null) return;
            var settings = CaptureLocal(false); var id = SelectedLocalId;
            _localCancellation = new CancellationTokenSource(); SetLocalBusy(true); buttonGenerateTTS.Enabled = false;
            try {await ClassicLocalTts.EnsureInstalledAsync(id, settings, _localCancellation.Token, new Progress<SpeechProgress>(p => labelProgress.Text = p.Message)); labelProgress.Text = CL("Движок и модель готовы", "Engine and model ready");}
            catch (OperationCanceledException) {labelProgress.Text = CL("Загрузка отменена", "Download cancelled");}
            catch (Exception ex) {HandleError(ex);}
            finally {_localCancellation.Dispose(); _localCancellation = null; _abort = false; SetLocalBusy(false); buttonGenerateTTS.Enabled = true;}
        }
        private async Task<bool> GenerateLocalAsync(Subtitle subtitle, bool progress, string outputOverride)
        {
            var id = SelectedLocalId; var settings = CaptureLocal(_perLineOriginal == null);
            _localCancellation = new CancellationTokenSource(); var cancel = _localCancellation.Token;
            SetLocalBusy(true); var enabled = buttonGenerateTTS.Enabled; buttonGenerateTTS.Enabled = false;
            progressBar1.Maximum = Math.Max(1, subtitle.Paragraphs.Count); progressBar1.Value = 0; progressBar1.Visible = progress;
            try
            {
                using (var engine = new ClassicLocalTts(id, settings))
                for (int i = 0; i < subtitle.Paragraphs.Count; i++)
                {
                    cancel.ThrowIfCancellationRequested(); var p = subtitle.Paragraphs[i]; var text = Utilities.UnbreakLine(HtmlUtil.RemoveHtmlTags(p.Text)); if (string.IsNullOrWhiteSpace(text)) continue;
                    var actor = _actorAndVoices.FirstOrDefault(a => a.Actor == p.Actor);
                    var voice = _castRegenerationVoice ?? (string.IsNullOrWhiteSpace(actor?.Voice) ? settings.Voice : ClassicLocalTts.ResolveVoice(actor.Voice));
                    if (_perLineOriginal != null) voice = await PerLineReferenceAsync(p, cancel) ?? voice;
                    labelProgress.Text = CL("Озвучка ", "Generating ") + (i + 1) + " / " + subtitle.Paragraphs.Count;
                    await engine.SpeakAsync(text, voice, Path.Combine(_waveFolder, outputOverride ?? i + ".wav"), cancel, new Progress<SpeechProgress>(x => labelProgress.Text = x.Message));
                    progressBar1.Value = i + 1;
                }
                return true;
            }
            catch (OperationCanceledException) {_abort = true; return false;}
            finally {CleanupPerLineReferences(); _localCancellation.Dispose(); _localCancellation = null; SetLocalBusy(false); buttonGenerateTTS.Enabled = enabled; progressBar1.Visible = false; labelProgress.Text = "";}
        }
    }
    public sealed class ClassicLocalTtsSettingsForm : Form
    {
        public ClassicLocalTtsSettingsForm(string id, ClassicLocalTtsSettings settings)
        {
            var ru = (Configuration.Settings.General.Language ?? "").StartsWith("ru", StringComparison.OrdinalIgnoreCase);
            Func<string, string, string> l = (r, e) => ru ? r : e;
            var definition = ClassicLocalTts.Catalog.First(x => x.Id == id);
            Text = definition.Name; ClientSize = new Size(540, 315); MinimumSize = Size; StartPosition = FormStartPosition.CenterParent;
            var table = new TableLayoutPanel {Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 7};
            foreach (var height in new[] {24, 32, 24, 32, 70, 40, 40}) table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            table.Controls.Add(new Label {Text = l("Модель", "Model"), AutoSize = true}, 0, 0);
            var model = new ComboBox {Name = "localModel", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList}; model.Items.AddRange(definition.Models.Cast<object>().ToArray()); model.SelectedItem = definition.Model(settings.Model); table.Controls.Add(model, 0, 1);
            table.Controls.Add(new Label {Text = l("Язык образца (для межъязыкового клонирования)", "Reference language (for cross-language cloning)"), AutoSize = true}, 0, 2);
            var source = new ComboBox {Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList}; source.Items.AddRange(new object[] {"auto", "ru", "en", "zh", "ja", "ko", "de", "es", "fr", "it", "pt", "ar"}); source.Text = settings.SourceLanguage; source.Enabled = id != "vox"; table.Controls.Add(source, 0, 3);
            table.Controls.Add(new Label {Dock = DockStyle.Fill, Text = l("CosyVoice: готовые голоса и клонирование. VoxCPM: нужен импортированный голос, язык определяется по тексту. Chatterbox Base: многоязычный; Turbo: только английский. Образцы хранятся в общей библиотеке голосов.", "CosyVoice supports presets and cloning. VoxCPM needs an imported voice and infers language from text. Chatterbox Base is multilingual; Turbo is English-only. Reference voices use the shared library.")}, 0, 4);
            var tools = new FlowLayoutPanel {Dock = DockStyle.Fill, WrapContents = false};
            var import = new Button {Text = l("Импорт голоса…", "Import voice…"), AutoSize = true}; var manager = new Button {Text = l("Менеджер голосов…", "Voice manager…"), AutoSize = true}; tools.Controls.AddRange(new Control[] {import, manager}); table.Controls.Add(tools, 0, 5);
            import.Click += (s, e) => {using (var dialog = new ClassicQwenReferenceImport()) if (dialog.ShowDialog(this) == DialogResult.OK) settings.Voice = dialog.Reference.Id;};
            manager.Click += (s, e) => {using (var dialog = new ClassicVoiceManager()) dialog.ShowDialog(this);};
            var buttons = new FlowLayoutPanel {Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft}; var cancel = new Button {Text = l("Отмена", "Cancel"), DialogResult = DialogResult.Cancel}; var ok = new Button {Text = "OK", DialogResult = DialogResult.OK}; buttons.Controls.AddRange(new Control[] {cancel, ok}); table.Controls.Add(buttons, 0, 6);
            ok.Click += (s, e) => {settings.Model = ((ClassicLocalTtsModel)model.SelectedItem).Key; settings.SourceLanguage = source.Text; if (id == "chatter" && settings.Model == "Turbo") settings.Language = "en"; settings.Save(id);};
            Controls.Add(table); AcceptButton = ok; CancelButton = cancel;
        }
    }
}
