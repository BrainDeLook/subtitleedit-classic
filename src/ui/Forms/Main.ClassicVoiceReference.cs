using System;
using System.IO;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms.Tts;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private ToolStripMenuItem _classicVoiceReferenceMenu;
        private Paragraph _classicVoiceReferenceRange;
        private static bool VoiceReferenceRu => Configuration.Settings.General.Language.StartsWith("ru",StringComparison.OrdinalIgnoreCase);

        private void InitializeClassicVoiceReferenceMenu()
        {
            if (_classicVoiceReferenceMenu == null)
            {
                _classicVoiceReferenceMenu = new ToolStripMenuItem {Name="classicWaveformVoiceReference"};
                contextMenuStripWaveform.Items.Add(new ToolStripSeparator());
                contextMenuStripWaveform.Items.Add(_classicVoiceReferenceMenu);
                _classicVoiceReferenceMenu.Click += (s,e) => CreateClassicVoiceReference();
            }
            _classicVoiceReferenceMenu.Text = VoiceReferenceRu ? "Создать образец голоса…" : "Create voice reference…";
            SetClassicVoiceReferenceRange(_classicVoiceReferenceRange,false);
        }

        private void SetClassicVoiceReferenceRange(Paragraph range, bool freeSelection)
        {
            _classicVoiceReferenceRange = range == null ? null : new Paragraph(range,false);
            if (freeSelection && _classicVoiceReferenceRange != null) _classicVoiceReferenceRange.Text="";
            if (_classicVoiceReferenceMenu == null) return;
            var valid = range != null && range.StartTime.TotalSeconds >= 0 && range.DurationTotalSeconds >= 1 && range.DurationTotalSeconds <= 30;
            _classicVoiceReferenceMenu.Enabled = valid && File.Exists(_videoFileName);
            _classicVoiceReferenceMenu.ToolTipText = VoiceReferenceRu
                ? "Выбери реплику или участок длительностью 1–30 секунд. Используется текущая аудиодорожка."
                : "Select a subtitle or a 1–30 second range. Uses the current audio track.";
        }

        private void CreateClassicVoiceReference()
        {
            var range = _classicVoiceReferenceRange;
            if (range == null || !File.Exists(_videoFileName)) return;
            if (!RequireFfmpegOk()) return;
            mediaPlayer.Pause();
            using (var form = new ClassicQwenReferenceImport(_videoFileName,range,ClassicSelectedAudioIndex()))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                    ShowStatus(VoiceReferenceRu ? "Образец добавлен в менеджер голосов: " + form.Reference.Name : "Voice reference added to the voice manager: " + form.Reference.Name);
            }
        }
    }
}
