using System;

namespace Nikse.SubtitleEdit.Forms.Options
{
    public sealed partial class Settings
    {
        // Open the existing page rather than maintaining a second settings dialog.
        internal void SelectNavigationSection(string section)
        {
            switch (section)
            {
                case "appearance": listBoxSection.SelectedIndex = AppearanceSection; break;
                case "syntax": listBoxSection.SelectedIndex = SyntaxColoringSection; break;
                case "shortcuts": listBoxSection.SelectedIndex = ShortcutsSection; break;
                case "waveform": listBoxSection.SelectedIndex = WaveformAndSpectrogramSection; break;
                default: throw new ArgumentException("Unknown settings section.", nameof(section));
            }
        }
    }
}
