using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms.Tts
{
    public sealed partial class TextToSpeech
    {
        private Button _finalOnlineSettings, _finalOnlineInstall;
        private string _finalOnlineLoaded;
        private bool _finalOnlineFilling;
        private static string FinalOnlineId(TextToSpeechEngineId id) => id == TextToSpeechEngineId.EdgeTts ? "edge" : id == TextToSpeechEngineId.MistralSpeech ? "mistral" : id == TextToSpeechEngineId.GoogleSpeech ? "google" : null;
        private string SelectedFinalOnlineId => FinalOnlineId(_engines.First(e => e.Index == nikseComboBoxEngine.SelectedIndex).Id);
        private void FillFinalOnlineControls(string id)
        {
            _finalOnlineFilling = true;
            try
            {
                _finalOnlineLoaded = id;
                var settings = ClassicOnlineTtsSettings.Load(id);
                nikseComboBoxVoice.Items.Clear(); nikseComboBoxVoice.Items.AddRange(ClassicOnlineTts.Voices(id));
                nikseComboBoxVoice.SelectedItem = nikseComboBoxVoice.Items.Cast<ClassicOnlineVoice>().FirstOrDefault(v => v.Id == settings.Voice) ?? nikseComboBoxVoice.Items.Cast<ClassicOnlineVoice>().FirstOrDefault();
                if (id == "mistral")
                {
                    labelRegion.Text = CL("Модель", "Model"); labelRegion.Visible = nikseComboBoxRegion.Visible = true;
                    nikseComboBoxRegion.Items.Clear(); nikseComboBoxRegion.Items.Add("voxtral-mini-tts-2603"); nikseComboBoxRegion.SelectedIndex = 0;
                }
                if (_finalOnlineSettings == null)
                {
                    var width = Math.Max(150, nikseComboBoxVoice.Right - buttonTestVoice.Right - 12);
                    _finalOnlineSettings = new Button { Name = "buttonOnlineTtsSettings", Text = CL("Настройки…", "Settings…"), Location = new Point(buttonTestVoice.Right + 6, buttonTestVoice.Top), Size = new Size(width / 2, buttonTestVoice.Height) };
                    _finalOnlineInstall = new Button { Name = "buttonOnlineTtsRefresh", Location = new Point(_finalOnlineSettings.Right + 5, buttonTestVoice.Top), Size = new Size(width - width / 2 - 5, buttonTestVoice.Height) };
                    _finalOnlineSettings.Click += (s, e) => EditFinalOnlineSettings();
                    _finalOnlineInstall.Click += async (s, e) => await RefreshFinalOnlineAsync();
                    nikseComboBoxVoice.SelectedIndexChanged += (s, e) => CaptureFinalOnline();
                    groupBoxSettings.Controls.AddRange(new Control[] { _finalOnlineSettings, _finalOnlineInstall });
                }
                _finalOnlineSettings.Visible = _finalOnlineInstall.Visible = true;
                _finalOnlineInstall.Text = id == "edge" && !File.Exists(ClassicOnlineTts.EdgeExecutable(settings)) ? CL("Установить", "Install") : CL("Обновить голоса", "Refresh voices");
                labelProgress.Text = id == "edge" ? CL("Онлайн; ключ не требуется", "Online; no API key required") : id == "google" ? CL("Файл ключа Google — в настройках", "Google key file — in Settings") : CL("API-ключ Mistral — в настройках", "Mistral API key — in Settings");
                labelProgress.AutoSize = false; labelProgress.AutoEllipsis = true; labelProgress.Width = Math.Max(80, buttonGenerateTTS.Left - labelProgress.Left - 8); labelProgress.Height = buttonGenerateTTS.Height;
            }
            finally { _finalOnlineFilling = false; }
        }
        private void CaptureFinalOnline()
        {
            if (_finalOnlineFilling || _finalOnlineLoaded == null || SelectedFinalOnlineId != _finalOnlineLoaded || !(nikseComboBoxVoice.SelectedItem is ClassicOnlineVoice voice)) return;
            var settings = ClassicOnlineTtsSettings.Load(_finalOnlineLoaded); settings.Voice = voice.Id;
            if (_finalOnlineLoaded == "mistral") settings.Model = nikseComboBoxRegion.Text;
            settings.Save(_finalOnlineLoaded);
        }
        private void EditFinalOnlineSettings()
        {
            CaptureFinalOnline(); var id = SelectedFinalOnlineId; var settings = ClassicOnlineTtsSettings.Load(id);
            using (var dialog = new Form { Text = _engines.First(e => e.Index == nikseComboBoxEngine.SelectedIndex).Name, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.Sizable, MinimizeBox = false, MaximizeBox = false })
            {
                var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                Action<string, Control, int> add = (label, control, height) => { var row = root.RowCount++; root.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); root.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row); control.Dock = DockStyle.Fill; root.Controls.Add(control, 1, row); };
                var key = new TextBox { UseSystemPasswordChar = true, Text = settings.Credentials.ApiKey };
                var keyFile = new TextBox { Text = settings.KeyFile }; var python = new TextBox { Text = settings.Python }; var exe = new TextBox { Text = settings.Executable };
                Func<TextBox, string, Control> browse = (box, filter) => { var panel = new TableLayoutPanel { ColumnCount = 2, Margin = Padding.Empty }; panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36)); box.Dock = DockStyle.Fill; var button = new Button { Text = "…", Dock = DockStyle.Fill, Margin = new Padding(3, 0, 0, 0) }; button.Click += (s, e) => { using (var picker = new OpenFileDialog { Filter = filter }) if (picker.ShowDialog(dialog) == DialogResult.OK) box.Text = picker.FileName; }; panel.Controls.Add(box); panel.Controls.Add(button); return panel; };
                var rate = new TextBox { Text = settings.Rate }; var pitch = new TextBox { Text = settings.Pitch }; var volume = new TextBox { Text = settings.Volume };
                var note = new Label { AutoSize = false, Dock = DockStyle.Fill };
                if (id == "mistral") { add("API key", key, 34); note.Text = CL("Ключ Mistral хранится зашифрованным для текущего пользователя Windows. Использование сервиса оплачивается отдельно.", "The Mistral key is encrypted for the current Windows user. Service usage is billed separately."); }
                if (id == "google") { add(CL("JSON-ключ", "JSON key file"), browse(keyFile, "Google service account (*.json)|*.json"), 34); note.Text = CL("Выберите JSON-ключ сервисного аккаунта с доступом к Cloud Text-to-Speech. Хранится только путь; файл не копируется. API и оплата должны быть включены в проекте Google.", "Select a service-account JSON key with Cloud Text-to-Speech access. Only its path is stored; the file is not copied. Enable the API and billing in the Google project."); }
                if (id == "edge")
                {
                    add("edge-tts.exe", browse(exe, "edge-tts executable (*.exe)|*.exe"), 34);
                    add("Python", browse(python, "Python executable (*.exe)|*.exe"), 34);
                    add(CL("Скорость (%)", "Rate (%)"), rate, 34); add(CL("Высота (Hz)", "Pitch (Hz)"), pitch, 34); add(CL("Громкость (%)", "Volume (%)"), volume, 34);
                    note.Text = CL("Можно выбрать готовый edge-tts.exe или установить клиент кнопкой «Установить». Для установки нужен Python 3.10+. Пустой путь Python — автоматический поиск. Клиент устанавливается отдельно от системных пакетов. Для озвучки нужен интернет.", "Choose an existing edge-tts.exe or use Install. Installation requires Python 3.10+. An empty Python path enables automatic discovery. The client is installed in an isolated environment. Synthesis requires internet access.");
                }
                var noteRow = root.RowCount++; root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.Controls.Add(note, 0, noteRow); root.SetColumnSpan(note, 2);
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; var ok = new Button { Text = "OK", DialogResult = DialogResult.OK }; var cancel = new Button { Text = CL("Отмена", "Cancel"), DialogResult = DialogResult.Cancel }; buttons.Controls.AddRange(new Control[] { cancel, ok });
                var rowButtons = root.RowCount++; root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); root.Controls.Add(buttons, 0, rowButtons); root.SetColumnSpan(buttons, 2);
                dialog.ClientSize = new Size(620, id == "edge" ? 340 : 210); dialog.MinimumSize = dialog.Size; dialog.Controls.Add(root); dialog.AcceptButton = ok; dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                settings.Credentials.ApiKey = key.Text.Trim(); settings.KeyFile = keyFile.Text.Trim(); settings.Python = python.Text.Trim(); settings.Executable = exe.Text.Trim(); settings.Rate = ClassicOnlineTts.NormalizeProsody(rate.Text, "%"); settings.Pitch = ClassicOnlineTts.NormalizeProsody(pitch.Text, "Hz"); settings.Volume = ClassicOnlineTts.NormalizeProsody(volume.Text, "%"); settings.Save(id); FillFinalOnlineControls(id);
            }
        }
        private async Task RefreshFinalOnlineAsync()
        {
            if (_compatibleCancellation != null) return;
            CaptureFinalOnline(); var id = SelectedFinalOnlineId; var settings = ClassicOnlineTtsSettings.Load(id);
            _compatibleCancellation = new CancellationTokenSource(); SetQwenBusy(true); var enabled = buttonGenerateTTS.Enabled; buttonGenerateTTS.Enabled = false;
            try
            {
                if (id == "edge" && !File.Exists(ClassicOnlineTts.EdgeExecutable(settings))) await ClassicOnlineTts.InstallEdgeAsync(settings, _compatibleCancellation.Token, new Progress<SpeechProgress>(p => labelProgress.Text = p.Message));
                using (var engine = new ClassicOnlineTts(id, settings)) await engine.RefreshVoicesAsync(_compatibleCancellation.Token);
                FillFinalOnlineControls(id); labelProgress.Text = CL("Список голосов обновлён", "Voice list refreshed");
            }
            catch (OperationCanceledException) { labelProgress.Text = CL("Отменено", "Cancelled"); }
            catch (Exception ex) { HandleError(ex); }
            finally { _compatibleCancellation.Dispose(); _compatibleCancellation = null; _abort = false; SetQwenBusy(false); buttonGenerateTTS.Enabled = enabled; }
        }
        private async Task<bool> GenerateFinalOnlineAsync(Subtitle subtitle, bool progress, string outputOverride)
        {
            CaptureFinalOnline(); var id = SelectedFinalOnlineId; var config = ClassicOnlineTtsSettings.Load(id);
            _compatibleCancellation = CancellationTokenSource.CreateLinkedTokenSource(_castCancellation?.Token ?? CancellationToken.None);
            var enabled = buttonGenerateTTS.Enabled; SetQwenBusy(true); buttonGenerateTTS.Enabled = false;
            progressBar1.Maximum = Math.Max(1, subtitle.Paragraphs.Count); progressBar1.Value = 0; progressBar1.Visible = progress;
            try
            {
                using (var engine = new ClassicOnlineTts(id, config)) for (int i = 0; i < subtitle.Paragraphs.Count; i++)
                {
                    var p = subtitle.Paragraphs[i]; var text = Utilities.UnbreakLine(HtmlUtil.RemoveHtmlTags(p.Text)); if (string.IsNullOrWhiteSpace(text)) continue;
                    var actor = _actorAndVoices.FirstOrDefault(a => a.Actor == p.Actor);
                    var voice = ClassicOnlineTts.ResolveVoice(id, _castRegenerationVoice ?? actor?.Voice, config.Voice);
                    labelProgress.Text = CL("Озвучка ", "Generating ") + (i + 1) + " / " + subtitle.Paragraphs.Count;
                    await engine.SpeakAsync(text, voice, Path.Combine(_waveFolder, outputOverride ?? i + ".wav"), _compatibleCancellation.Token); progressBar1.Value = i + 1;
                }
                return true;
            }
            catch (OperationCanceledException) { _abort = true; return false; }
            finally { _compatibleCancellation.Dispose(); _compatibleCancellation = null; SetQwenBusy(false); buttonGenerateTTS.Enabled = enabled; progressBar1.Visible = false; labelProgress.Text = ""; }
        }
    }
}
