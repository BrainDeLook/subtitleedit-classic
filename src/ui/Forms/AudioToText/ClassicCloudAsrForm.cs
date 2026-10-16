using System;
using System.Drawing;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic.SpeechToText;

namespace Nikse.SubtitleEdit.Forms.AudioToText
{
    public sealed class ClassicCloudAsrForm : Form
    {
        public ClassicCloudAsrForm(string id)
        {
            Text = "ASR: " + id; ClientSize = new Size(700, 530); MinimumSize = new Size(660, 540);
            StartPosition = FormStartPosition.CenterParent;
            var s = ClassicCloudAsrSettings.Load(id);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(5) };
            var cancel = new Button { Text = ClassicSpeechSettings.L("Отмена", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
            var save = new Button { Name = "saveCloudAsr", Text = ClassicSpeechSettings.L("Сохранить", "Save"), AutoSize = true };
            footer.Controls.AddRange(new Control[] { cancel, save });
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true }; scroll.Controls.Add(table); Controls.Add(scroll); Controls.Add(footer);
            Func<string, string, TextBox> field = (label, value) => {
                var row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                table.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 7, 3, 8) }, 0, row);
                var box = new TextBox { Dock = DockStyle.Fill, Text = value }; table.Controls.Add(box, 1, row); return box;
            };
            var url = field("URL", s.Api.Url); url.Name = "cloudAsrUrl";
            var model = field(ClassicSpeechSettings.L("Модель", "Model"), s.Api.Model);
            var key = field("API key", s.Api.ApiKey); key.UseSystemPasswordChar = true; key.Enabled = id != "google";
            var region = field(ClassicSpeechSettings.L("Регион (Google: us/eu/global)", "Region (Google: us/eu/global)"), s.Region); region.Enabled = id == "google";
            var keyfile = field(ClassicSpeechSettings.L("Google JSON / пусто для ADC", "Google JSON / empty for ADC"), s.KeyFile); keyfile.Enabled = id == "google";
            var project = field(ClassicSpeechSettings.L("Google project ID", "Google project ID"), s.Project); project.Enabled = id == "google";
            var bucket = field(ClassicSpeechSettings.L("Google bucket (необязательно)", "Google bucket (optional)"), s.Bucket); bucket.Enabled = id == "google";
            var chunk = field(ClassicSpeechSettings.L("Длина части, секунд (5–600)", "Chunk seconds (5–600)"), s.Api.ChunkSeconds.ToString());
            var timeout = field(ClassicSpeechSettings.L("Таймаут, секунд", "Timeout, seconds"), s.Api.TimeoutSeconds.ToString());
            var dynamicBatch = new CheckBox { Text = "Google: dynamic batching", Checked = s.DynamicBatching, Enabled = id == "google", AutoSize = true };
            table.Controls.Add(dynamicBatch, 1, table.RowCount++);
            var notice = new Label { AutoSize = true, MaximumSize = new Size(620, 0), Text = ClassicSpeechSettings.L(
                "Это облачные сервисы: аудио будет загружено провайдеру. API-ключ хранится зашифрованным для текущего пользователя Windows. Google использует JSON-ключ или ADC; временный объект удаляется после запроса. Для Китая DashScope укажи https://dashscope.aliyuncs.com.",
                "Audio is uploaded to the provider. API keys are encrypted for the current Windows user. Google uses a JSON key or ADC; temporary objects are removed after recognition. For China, use https://dashscope.aliyuncs.com.") };
            table.Controls.Add(notice, 0, table.RowCount++); table.SetColumnSpan(notice, 2);
            AcceptButton = save; CancelButton = cancel;
            save.Click += (sender, e) => {
                try {
                    s.Api.Url = url.Text.Trim(); s.Api.Model = model.Text.Trim(); s.Api.ApiKey = key.Text.Trim(); s.Region = region.Text.Trim();
                    if (id == "google" && Uri.TryCreate(s.Api.Url, UriKind.Absolute, out var endpoint) && endpoint.Host.EndsWith("speech.googleapis.com", StringComparison.OrdinalIgnoreCase))
                        s.Api.Url = "https://" + (s.Region == "global" ? "" : s.Region + "-") + "speech.googleapis.com";
                    s.KeyFile = keyfile.Text.Trim().Trim('"'); s.Project = project.Text.Trim(); s.Bucket = bucket.Text.Trim();
                    s.Api.ChunkSeconds = int.Parse(chunk.Text); s.Api.TimeoutSeconds = int.Parse(timeout.Text); s.DynamicBatching = dynamicBatch.Checked;
                    s.Save(id); DialogResult = DialogResult.OK;
                } catch (Exception error) { MessageBox.Show(this, error.Message, Text); }
            };
        }
    }
}
