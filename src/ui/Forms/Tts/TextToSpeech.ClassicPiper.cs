using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms.Tts
{
    public sealed partial class TextToSpeech
    {
        private Button _piperImportButton;
        private void UpdatePiperImportButton(TextToSpeechEngineId engine)
        {
            if (_piperImportButton != null)
            {
                _piperImportButton.Visible = engine == TextToSpeechEngineId.Piper;
            }
            if (engine != TextToSpeechEngineId.Piper) return;
            if (_piperImportButton == null)
            {
                _piperImportButton = new Button { Name = "buttonImportPiperVoice", Text = CL("Импорт голоса…", "Import voice…"), Height = buttonTestVoice.Height };
                _piperImportButton.Click += async (s, e) => await ImportPiperVoice(); groupBoxSettings.Controls.Add(_piperImportButton);
            }
            buttonTestVoice.Width = Math.Min(150, Math.Max(90, nikseComboBoxVoice.Width / 2 - 6));
            _piperImportButton.SetBounds(buttonTestVoice.Right + 6, buttonTestVoice.Top, Math.Max(80, nikseComboBoxVoice.Right - buttonTestVoice.Right - 6), buttonTestVoice.Height);
            _piperImportButton.Visible = true;
        }
        private async Task ImportPiperVoice()
        {
            using (var dialog = new OpenFileDialog { Title = CL("Выбери модель Piper; рядом должен быть файл .onnx.json", "Choose a Piper model; its .onnx.json must be alongside it"), Filter = "Piper ONNX|*.onnx", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _piperImportButton.Enabled = false;
                try
                {
                    var imported = await Task.Run(() => ClassicPiperVoices.Import(dialog.FileName));
                    if (IsDisposed || Disposing) return;
                    _piperVoices.Clear(); nikseComboBoxEngine_SelectedIndexChanged(null, null);
                    var voice = _piperVoices.FirstOrDefault(v => v.ModelShort.Equals(Path.GetFileName(imported), StringComparison.OrdinalIgnoreCase));
                    if (voice != null) nikseComboBoxVoice.Text = voice.ToString();
                }
                catch (Exception ex) { if (!IsDisposed) MessageBox.Show(this, ex.Message, Text); }
                finally { if (!IsDisposed) _piperImportButton.Enabled = true; }
            }
        }
    }
}
