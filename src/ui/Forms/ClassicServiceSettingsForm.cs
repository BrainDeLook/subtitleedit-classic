using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Forms.AudioToText;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Forms
{
    public class ClassicServiceSettingsForm : Form
    {
        protected readonly Dictionary<string, TextBox> Fields = new Dictionary<string, TextBox>();
        private readonly TableLayoutPanel _table;
        private readonly FlowLayoutPanel _footer;
        private readonly Button _save;
        private CancellationTokenSource _download;
        public ClassicServiceSettingsForm(string title)
        {
            Text = title; ClientSize = new Size(700, 470); MinimumSize = new Size(600, 400); StartPosition = FormStartPosition.CenterParent;
            _footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), WrapContents = false };
            var cancel = new Button { Text = ClassicSpeechSettings.L("Отмена", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
            _save = new Button { Text = ClassicSpeechSettings.L("Сохранить", "Save"), AutoSize = true };
            _footer.Controls.AddRange(new Control[] { cancel, _save });
            _table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12) };
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true }; scroll.Controls.Add(_table);
            Controls.Add(scroll); Controls.Add(_footer); AcceptButton = _save; CancelButton = cancel;
            FormClosing += (s, e) => { if (_download != null) { _download.Cancel(); e.Cancel = true; } };
        }
        protected void Add(string key, string label, string value, bool secret = false, bool multiline = false)
        {
            var row = _table.RowCount++; _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _table.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 7, 3, 8) }, 0, row);
            var text = new TextBox { Name = key, Text = value, Dock = DockStyle.Fill, UseSystemPasswordChar = secret, Multiline = multiline, Height = multiline ? 100 : 23, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None };
            Fields.Add(key, text); _table.Controls.Add(text, 1, row);
        }
        protected void SaveWith(Action save) => _save.Click += (s, e) => { try { save(); DialogResult = DialogResult.OK; } catch (Exception error) { MessageBox.Show(this, error.Message, Text); } };
        protected void DownloadWith(Func<CancellationToken, IProgress<SpeechProgress>, Task> action)
        {
            var button = new Button { Text = ClassicSpeechSettings.L("Скачать движок и модели", "Download engine and models"), AutoSize = true };
            _footer.Controls.Add(button);
            button.Click += async (s, e) => {
                _download = new CancellationTokenSource(); _save.Enabled = button.Enabled = false; _table.Enabled = false; var title = Text;
                try { await action(_download.Token, new Progress<SpeechProgress>(p => Text = title + " — " + p.Message)); MessageBox.Show(this, ClassicSpeechSettings.L("Готово", "Ready"), title); }
                catch (OperationCanceledException) { }
                catch (Exception error) { MessageBox.Show(this, error.Message, title); }
                finally { _download.Dispose(); _download = null; _save.Enabled = button.Enabled = true; _table.Enabled = true; Text = title; }
            };
        }
    }
    public sealed class ClassicTranslationForm : ClassicServiceSettingsForm
    {
        public ClassicTranslationForm(string id) : base("Translation: " + id)
        {
            var settings = ClassicTranslationSettings.Load(id);
            if (id != "madlad") { Add("url", "URL", settings.Api.Url); Add("key", id == "lara" ? "Lara secret" : "API key", settings.Api.ApiKey, true); }
            if (id != "lara") Add("model", "Model", settings.Api.Model);
            if (id == "madlad")
            {
                Fields["model"].ReadOnly = true; Add("variant", "cpu / vulkan / cuda13", settings.Variant);
                Add("installed", ClassicSpeechSettings.L("Установка", "Installation"), ClassicNewTranslator.MadladReady(settings.Variant) ? ClassicSpeechSettings.L("Готово", "Ready") : ClassicSpeechSettings.L("Не установлено. Нажми «Скачать движок и модели» внизу. Загрузка модели — около 2 ГБ. После завершения нажми «Сохранить».", "Not installed. Click Download engine and models below (about 2 GB), then Save."), multiline: true);
                Fields["installed"].ReadOnly = true;
                Add("path", ClassicSpeechSettings.L("Файл модели", "Model file"), ClassicNewTranslator.MadladModel); Fields["path"].ReadOnly = true;
            }
            if (id == "lara") Add("accessId", "Lara access key ID", settings.AccessId);
            Add("context", ClassicSpeechSettings.L("Контекст: имена, термины", "Context: names, terms"), settings.Context, multiline: true);
            SaveWith(() => {
                if (id != "madlad") { settings.Api.Url = Fields["url"].Text.Trim(); settings.Api.ApiKey = Fields["key"].Text.Trim(); }
                if (id != "lara") settings.Api.Model = Fields["model"].Text.Trim();
                if (id == "lara") settings.AccessId = Fields["accessId"].Text.Trim();
                settings.Context = Fields["context"].Text;
                if (id == "madlad") settings.Variant = Fields["variant"].Text.Trim();
                if (id == "madlad" && !new[] { "cpu", "cpu-legacy", "vulkan", "cuda", "cuda13" }.Contains(settings.Variant)) throw new ArgumentException("Select cpu, vulkan or cuda13.");
                settings.Save(id);
            });
            if (id == "madlad") DownloadWith(async (cancel, progress) => {
                var variant = Fields["variant"].Text.Trim();
                if (!new[] { "cpu", "cpu-legacy", "vulkan", "cuda", "cuda13" }.Contains(variant)) throw new ArgumentException("Select cpu, vulkan or cuda13.");
                await ClassicNewTranslator.EnsureMadladAsync(variant, cancel, progress);
                Fields["installed"].Text = ClassicSpeechSettings.L("Готово. Нажми «Сохранить».", "Ready. Click Save.");
            });
        }
    }
}
