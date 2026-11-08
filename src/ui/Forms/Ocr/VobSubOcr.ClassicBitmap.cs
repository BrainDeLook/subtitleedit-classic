using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.Ocr;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Forms.AudioToText;

namespace Nikse.SubtitleEdit.Forms.Ocr
{
    public sealed partial class VobSubOcr
    {
        public string PreviewVideoFileName { get; set; }
        public int PreviewAudioTrackNumber { get; set; } = -1;
        private ClassicOcrVideoPreview _classicVideoPreview;
        private void ShowClassicVideoPreview()
        {
            if (_classicVideoPreview == null || _classicVideoPreview.IsDisposed)
            {
                _classicVideoPreview = new ClassicOcrVideoPreview(PreviewVideoFileName) { AudioTrackNumber = PreviewAudioTrackNumber };
                _classicVideoPreview.FormClosed += (sender, e) => PreviewVideoFileName = ((ClassicOcrVideoPreview)sender).VideoFileName;
            }
            if (_selectedIndex >= 0 && _selectedIndex < _subtitle.Paragraphs.Count) _classicVideoPreview.SelectCue(_subtitle.Paragraphs[_selectedIndex]);
            if (!_classicVideoPreview.Visible) _classicVideoPreview.Show(this); else _classicVideoPreview.Activate();
        }
        private readonly Dictionary<int,string> _classicBitmapProviders = new Dictionary<int,string>();
        private bool IsClassicBitmapMethod => _ocrMethodIndex == _ocrMethodOllama || _classicBitmapProviders.ContainsKey(_ocrMethodIndex);
        private string ClassicBitmapProvider => _classicBitmapProviders.TryGetValue(_ocrMethodIndex, out var id) ? id : null;
        private bool _classicCaptureAssaPosition;
        private bool _classicIsolateVobSubColors;
        private bool ClassicOcrHasSeparatedBlocks(int index)
        {
            if(!_classicCaptureAssaPosition)return false;
            GetSubtitleScreenSize(index,out var w,out var h);
            using(var bitmap=GetSubtitleBitmap(index,false))using(var memory=new System.IO.MemoryStream())
            {bitmap.Save(memory,System.Drawing.Imaging.ImageFormat.Png);using(var image=SkiaSharp.SKBitmap.Decode(memory.ToArray()))return OcrAssaAlignment.HasSeparatedBlocks(image,w,h);}
        }
        private string ClassicOcrAlignment(int index,string text)
        {
            GetSubtitleTopAndHeight(index,out var x,out var y,out _,out _);GetSubtitleScreenSize(index,out var w,out var h);
            using(var bitmap=GetSubtitleBitmap(index,false))using(var memory=new System.IO.MemoryStream())
            {bitmap.Save(memory,System.Drawing.Imaging.ImageFormat.Png);using(var image=SkiaSharp.SKBitmap.Decode(memory.ToArray()))return OcrAssaAlignment.Detect(image,x,y,w,h,text,true).Text;}
        }
        private void InitializeClassicBitmap()
        {
            _classicIsolateVobSubColors = ClassicEditorPreferences.Load().VobSubIsolateColors;
            var isolate = new ToolStripMenuItem(ClassicSpeechSettings.L("Изолировать цвет текста VobSub для OCR", "Isolate VobSub text colour for OCR")) { CheckOnClick = true, Checked = _classicIsolateVobSubColors };
            isolate.CheckedChanged += (sender, e) =>
            {
                _classicIsolateVobSubColors = isolate.Checked;
                var preferences = ClassicEditorPreferences.Load(); preferences.VobSubIsolateColors = isolate.Checked;
                try { preferences.Save(); }
                catch (System.IO.IOException error) { MessageBox.Show(this, error.Message, Text); }
                catch (UnauthorizedAccessException error) { MessageBox.Show(this, error.Message, Text); }
                if (_selectedIndex >= 0) SubtitleListView1SelectedIndexChanged(null, EventArgs.Empty);
            };
            contextMenuStripImage.Items.Add(isolate);
            InitializeClassicImageNavigation();
            _classicCaptureAssaPosition=ClassicEditorPreferences.Load().OcrAssaPosition;
            var capture=new ToolStripMenuItem(ClassicSpeechSettings.L("Определять позицию ASSA (верх/центр/низ)","Capture ASSA position (top/middle/bottom)")){CheckOnClick=true,Checked=_classicCaptureAssaPosition};
            capture.CheckedChanged+=(sender,e)=>{_classicCaptureAssaPosition=capture.Checked;var preferences=ClassicEditorPreferences.Load();preferences.OcrAssaPosition=capture.Checked;try{preferences.Save();}catch(Exception ex)when(ex is System.IO.IOException || ex is UnauthorizedAccessException){MessageBox.Show(this,ex.Message,Text);}};
            contextMenuStripImage.Items.Add(capture);
            var previewText = ClassicSpeechSettings.L("Просмотр видео…", "Video preview…");
            contextMenuStripImage.Items.Add(new ToolStripSeparator());
            contextMenuStripImage.Items.Add(previewText, null, (sender, e) => ShowClassicVideoPreview());
            contextMenuStripListview.Items.Add(new ToolStripSeparator());
            contextMenuStripListview.Items.Add(previewText, null, (sender, e) => ShowClassicVideoPreview());
            FormClosed += (sender, e) => _classicVideoPreview?.Dispose();
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
                        nikseComboBoxOllamaModel.Text = ClassicBitmapSettings.Load(id).SelectedModel;
                    }
            };
        }
    }
    public sealed class ClassicBitmapForm : ClassicServiceSettingsForm
    {
        public ClassicBitmapForm(string provider) : base(provider == "compatible" ? "OCR — llama.cpp / Vision API" : "OCR: " + provider)
        {
            var settings = ClassicBitmapSettings.Load(provider);
            Add("url", "URL", settings.Api.Url); Add("key", "API key", settings.Api.ApiKey, true);
            Add("model", provider == "compatible" ? ClassicSpeechSettings.L("Модель внешнего сервера", "External server model") : ClassicSpeechSettings.L("Модель", "Model"), settings.Api.Model);
            Add("models", provider == "crispembed" ? ClassicSpeechSettings.L("Доступные модели", "Available models") : ClassicSpeechSettings.L("Сведения", "Details"),
                provider == "crispembed" ? string.Join(Environment.NewLine, ClassicBitmapOcr.Models.Select(m => m.Name)) : "OCR sends the selected image to the configured server.", multiline: true);
            Fields["models"].ReadOnly = true;
            CheckBox local = null;
            if (provider == "compatible")
            {
                ClientSize = new Size(780, 560);
                local = new CheckBox { Name = "localLlamaOcr", Text = ClassicSpeechSettings.L("Локальная OCR-модель llama.cpp", "Local llama.cpp OCR model"), AutoSize = true, Checked = settings.UseLocalLlama };
                var row = _table.RowCount++;
                _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _table.Controls.Add(local, 1, row);
                var manager = new Button { Name = "llamaOcrModels", Text = ClassicSpeechSettings.L("Выбрать / скачать OCR-модель…", "Select / download OCR model…"), AutoSize = true };
                row = _table.RowCount++;
                _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _table.Controls.Add(manager, 1, row);
                Action refresh = () =>
                {
                    foreach (var field in new[] { "url", "key", "model" }) Fields[field].Enabled = !local.Checked;
                    manager.Enabled = local.Checked;
                    Fields["models"].Text = local.Checked
                        ? ClassicLlamaSettings.LoadOcr().Model + Environment.NewLine + ClassicSpeechSettings.L("Движок общий с переводом; выбор OCR-модели сохраняется отдельно. Установка запускается кнопкой скачивания в окне моделей.", "The runtime is shared with translation; the OCR model selection is saved separately. Installation starts with Download in the model window.")
                        : ClassicSpeechSettings.L("Изображение субтитра отправляется на указанный сервер.", "The subtitle image is sent to the configured server.");
                };
                local.CheckedChanged += (sender, e) => refresh();
                manager.Click += (sender, e) => { using (var form = new ClassicLlamaForm(true)) form.ShowDialog(this); refresh(); };
                refresh();
            }
            SaveWith(() => {
                if (provider == "crispembed" && !ClassicBitmapOcr.Models.Any(m => m.Name == Fields["model"].Text.Trim())) throw new ArgumentException("Select a model file name from the list.");
                settings.UseLocalLlama = local?.Checked == true;
                settings.Api.Url = Fields["url"].Text.Trim(); settings.Api.ApiKey = Fields["key"].Text.Trim(); settings.Api.Model = Fields["model"].Text.Trim(); settings.Save(provider);
            });
            if (provider == "crispembed") DownloadWith((cancel, progress) => ClassicBitmapOcr.EnsureAsync(Fields["model"].Text.Trim(), cancel, progress));
        }
    }
}
