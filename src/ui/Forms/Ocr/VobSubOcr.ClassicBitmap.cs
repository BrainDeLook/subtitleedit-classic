using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.Ocr;

namespace Nikse.SubtitleEdit.Forms.Ocr
{
    public sealed partial class VobSubOcr
    {
        private readonly Dictionary<int,string> _classicBitmapProviders = new Dictionary<int,string>();
        private bool IsClassicBitmapMethod => _ocrMethodIndex == _ocrMethodOllama || _classicBitmapProviders.ContainsKey(_ocrMethodIndex);
        private string ClassicBitmapProvider => _classicBitmapProviders.TryGetValue(_ocrMethodIndex, out var id) ? id : null;
        private void InitializeClassicBitmap()
        {
            _classicBitmapProviders[comboBoxOcrMethod.Items.Add("Google Lens Sharp")] = "lens";
            _classicBitmapProviders[comboBoxOcrMethod.Items.Add("Mistral OCR")] = "mistral";
            _classicBitmapProviders[comboBoxOcrMethod.Items.Add("CrispEmbed (local GGUF)")] = "crispembed";
            _classicBitmapProviders[comboBoxOcrMethod.Items.Add("llama.cpp / compatible Vision")] = "compatible";
            var settings = new Button { Name = "classicBitmapSettings", Text = Configuration.Settings.General.Language.StartsWith("ru") ? "Настройки / скачать…" : "Settings / download…", Left = 8, Top = 90, Width = 245, Height = 25 };
            groupBoxOllama.Controls.Add(settings);
            settings.Click += (s,e) => {
                var id = ClassicBitmapProvider;
                if (id == null) { MessageBox.Show(this, "Select Mistral, CrispEmbed or llama.cpp / compatible Vision first."); return; }
                if (id == "lens") { MessageBox.Show(this, Configuration.Settings.General.Language.StartsWith("ru") ? "Google Lens отправляет изображение субтитра на сервер Google. Установка модели и ключ API не требуются." : "Google Lens sends the subtitle image to Google. No model installation or API key is required."); return; }
                using (var form = new ClassicBitmapForm(id))
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        _ollamaOcr?.Dispose(); _ollamaOcr = new OllamaOcr(id);
                        nikseComboBoxOllamaModel.Text = ClassicBitmapSettings.Load(id).Api.Model;
                    }
            };
        }
    }
    public sealed class ClassicBitmapForm : ClassicServiceSettingsForm
    {
        public ClassicBitmapForm(string provider) : base("OCR: " + provider)
        {
            var settings = ClassicBitmapSettings.Load(provider);
            Add("url", "URL", settings.Api.Url); Add("key", "API key", settings.Api.ApiKey, true);
            Add("model", "Model", settings.Api.Model);
            Add("models", provider == "crispembed" ? "Available model file names" : "Note",
                provider == "crispembed" ? string.Join(Environment.NewLine, ClassicBitmapOcr.Models.Select(m => m.Name)) : "OCR sends the selected image to the configured server.", multiline: true);
            Fields["models"].ReadOnly = true;
            SaveWith(() => {
                if (provider == "crispembed" && !ClassicBitmapOcr.Models.Any(m => m.Name == Fields["model"].Text.Trim())) throw new ArgumentException("Select a model file name from the list.");
                settings.Api.Url = Fields["url"].Text.Trim(); settings.Api.ApiKey = Fields["key"].Text.Trim(); settings.Api.Model = Fields["model"].Text.Trim(); settings.Save(provider);
            });
            if (provider == "crispembed") DownloadWith((cancel, progress) => ClassicBitmapOcr.EnsureAsync(Fields["model"].Text.Trim(), cancel, progress));
        }
    }
}
