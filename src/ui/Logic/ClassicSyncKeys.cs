using System;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic
{
    internal static class ClassicSyncKeys
    {
        public static double? SeekSeconds(Keys key)
        {
            var shortcuts = Configuration.Settings.Shortcuts;
            var step = Math.Max(1, Configuration.Settings.Tools.MoveStartEndMs) / 1000.0;
            if (Matches(key, shortcuts.MainAdjustStartXMsBack) || Matches(key, shortcuts.MainAdjustEndXMsBack)) return -step;
            if (Matches(key, shortcuts.MainAdjustStartXMsForward) || Matches(key, shortcuts.MainAdjustEndXMsForward)) return step;
            var direction = (key & Keys.KeyCode) == Keys.Left ? -1 : (key & Keys.KeyCode) == Keys.Right ? 1 : 0;
            var modifiers = key & Keys.Modifiers;
            if (direction == 0) return null;
            if (modifiers == (Keys.Control | Keys.Shift)) return direction;
            if (modifiers == Keys.Control) return direction * 0.1;
            if (modifiers == Keys.Alt) return direction * 0.5;
            return null;
        }
        private static bool Matches(Keys key, string configured) => !string.IsNullOrWhiteSpace(configured) && UiUtil.GetKeys(configured) == key;
        public static bool VerticalZoom(Keys key, Controls.AudioVisualizer wave)
        {
            if (wave == null) return false;
            if (key == (Keys.Shift | Keys.Oemplus) || key == (Keys.Shift | Keys.Add)) { wave.VerticalZoomFactor = Math.Min(16, wave.VerticalZoomFactor * 1.2); return true; }
            if (key == (Keys.Shift | Keys.OemMinus) || key == (Keys.Shift | Keys.Subtract)) { wave.VerticalZoomFactor = Math.Max(.2, wave.VerticalZoomFactor / 1.2); return true; }
            return false;
        }
    }
}
