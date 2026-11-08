using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private readonly List<(ToolStripButton Button, ToolStripMenuItem Command, ToolStripMenuItem Toggle, int Role)> _classicQuickButtons
            = new List<(ToolStripButton, ToolStripMenuItem, ToolStripMenuItem, int)>();
        private ToolStripSeparator _classicQuickSeparator;
        private ToolStripMenuItem _classicQuickVisibility;

        private void InitializeClassicQuickToolbar()
        {
            if (_classicQuickSeparator == null)
            {
                if (!_classicEditorPreferences.QuickToolbarBurnInMigrated)
                {
                    if (!_classicEditorPreferences.HiddenQuickToolbarCommands.Contains(generateVideoWithHardcodedSubtitleToolStripMenuItem.Name))
                    {
                        Configuration.Settings.General.ShowToolbarBurnIn = true;
                        Configuration.Settings.Save();
                    }
                    _classicEditorPreferences.QuickToolbarBurnInMigrated = true;
                    _classicEditorPreferences.Save();
                }
                var commands = new[] { _classicVideoOcrMenu, audioToTextWhisperTolStripMenuItem,
                    textToSpeechAndAddToVideoToolStripMenuItem, toolStripMenuItemAutoTranslate,
                    _classicImproveTimeCodesMenu, _classicDraw, generateVideoWithHardcodedSubtitleToolStripMenuItem,
                    toolStripMenuItemBatchConvert };
                var at = toolStrip1.Items.IndexOf(toolStripSeparatorSubtitleFormat);
                _classicQuickSeparator = new ToolStripSeparator { Name = "classicQuickFunctionsSeparator" };
                toolStrip1.Items.Insert(at++, _classicQuickSeparator);
                if (toolStrip1.ContextMenuStrip == null) toolStrip1.ContextMenuStrip = new ContextMenuStrip(components);
                _classicQuickVisibility = new ToolStripMenuItem { Name = "classicQuickToolbarVisibility" };
                toolStrip1.ContextMenuStrip.Items.Add(_classicQuickVisibility);
                toolStrip1.ContextMenuStrip.Opening += (sender, args) => RefreshClassicQuickToolbarState();
                for (var role = 0; role < commands.Length; role++)
                {
                    if (role == 6) continue; // The native hard-sub button already provides this action.
                    var command = commands[role];
                    var button = new ToolStripButton { Name = "classicQuick_" + command.Name,
                        DisplayStyle = ToolStripItemDisplayStyle.Image, ImageScaling = ToolStripItemImageScaling.None,
                        Overflow = ToolStripItemOverflow.AsNeeded, Tag = command };
                    button.Click += (sender, args) =>
                    {
                        if (command == toolStripMenuItemAutoTranslate) MakeAutoTranslate(false);
                        else command.PerformClick();
                    };
                    var toggle = new ToolStripMenuItem { Name = button.Name + "Visible", CheckOnClick = true };
                    toggle.Click += (sender, args) =>
                    {
                        var hidden = _classicEditorPreferences.HiddenQuickToolbarCommands.ToList();
                        hidden.Remove(command.Name);
                        if (!toggle.Checked) hidden.Add(command.Name);
                        _classicEditorPreferences.HiddenQuickToolbarCommands = hidden.ToArray();
                        try { _classicEditorPreferences.Save(); }
                        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                        { MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
                        RefreshClassicQuickToolbarState();
                    };
                    command.EnabledChanged += (sender, args) => RefreshClassicQuickToolbarState();
                    _classicQuickButtons.Add((button, command, toggle, role));
                    _classicQuickVisibility.DropDownItems.Add(toggle);
                    toolStrip1.Items.Insert(at++, button);
                }
                FormClosed += (sender, args) => { foreach (var entry in _classicQuickButtons) entry.Button.Image?.Dispose(); };
            }
            _classicQuickVisibility.Text = ClassicAssistedText.L("Быстрые кнопки функций", "Quick function buttons");
            var size = toolStripButtonSave.Image?.Width ?? 24;
            foreach (var entry in _classicQuickButtons)
            {
                entry.Button.Text = entry.Command.Text.Replace("&", "");
                entry.Button.ToolTipText = entry.Button.Text;
                entry.Button.AccessibleName = entry.Button.Text;
                entry.Toggle.Text = entry.Command.Text;
                entry.Button.Image?.Dispose();
                entry.Button.Image = DrawClassicQuickIcon(entry.Role, size);
            }
            RefreshClassicQuickToolbarState();
        }

        internal IEnumerable<(ToolStripButton Button, bool Visible)> ClassicQuickToolbarSettings =>
            _classicQuickButtons.Select(entry => (entry.Button, !_classicEditorPreferences.HiddenQuickToolbarCommands.Contains(entry.Command.Name)));

        internal bool SaveClassicQuickToolbarSettings(IReadOnlyList<bool> visibility)
        {
            var hidden = _classicQuickButtons.Where((entry, index) => !visibility[index]).Select(entry => entry.Command.Name).ToArray();
            if (_classicEditorPreferences.HiddenQuickToolbarCommands.SequenceEqual(hidden)) return false;
            _classicEditorPreferences.HiddenQuickToolbarCommands = hidden;
            _classicEditorPreferences.Save();
            RefreshClassicQuickToolbarState();
            return true;
        }

        private void RefreshClassicQuickToolbarState()
        {
            var hasCues = _subtitle.Paragraphs.Count > 0;
            foreach (var entry in _classicQuickButtons)
            {
                var shown = !_classicEditorPreferences.HiddenQuickToolbarCommands.Contains(entry.Command.Name);
                entry.Toggle.Checked = shown;
                entry.Button.Available = shown;
                var ready = entry.Role == 0 || entry.Role == 1 || entry.Role == 7 || hasCues;
                if (entry.Role == 4) ready &= _videoInfo != null && !string.IsNullOrEmpty(_videoFileName);
                // The format picker is populated after initial language setup.
                if (entry.Role == 5) ready &= _currentSubtitleFormat is AdvancedSubStationAlpha || _currentSubtitleFormat is SubStationAlpha;
                entry.Button.Enabled = ready && entry.Command.Enabled;
            }
            if (_classicQuickSeparator != null) _classicQuickSeparator.Available = _classicQuickButtons.Any(x => x.Button.Available);
        }

        private static Bitmap DrawClassicQuickIcon(int role, int size)
        {
            var image = new Bitmap(size, size);
            using (var g = Graphics.FromImage(image))
            using (var pen = new Pen(Color.FromArgb(0, 120, 215), 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var font = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(pen.Color))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(size / 32f, size / 32f);
                switch (role)
                {
                    case 0: // video frame and subtitle scan
                        g.DrawRectangle(pen, 3, 5, 26, 21);
                        g.DrawLine(pen, 6, 9, 9, 9); g.DrawLine(pen, 23, 9, 26, 9);
                        g.DrawLine(pen, 8, 20, 15, 20); g.DrawLine(pen, 18, 20, 24, 20);
                        g.DrawLine(pen, 7, 16, 7, 13); g.DrawLine(pen, 7, 13, 10, 13);
                        g.DrawLine(pen, 25, 16, 25, 13); g.DrawLine(pen, 25, 13, 22, 13);
                        break;
                    case 1: // speech recognition
                        g.DrawArc(pen, 11, 3, 10, 17, 180, 180); g.DrawLine(pen, 11, 11, 11, 15);
                        g.DrawLine(pen, 21, 11, 21, 15); g.DrawArc(pen, 11, 10, 10, 10, 0, 180);
                        g.DrawArc(pen, 7, 8, 18, 16, 0, 180); g.DrawLine(pen, 16, 24, 16, 28); g.DrawLine(pen, 11, 28, 21, 28);
                        break;
                    case 2: // speech synthesis
                        g.DrawPolygon(pen, new[] { new Point(4, 12), new Point(9, 12), new Point(15, 7), new Point(15, 25), new Point(9, 20), new Point(4, 20) });
                        g.DrawArc(pen, 12, 10, 11, 12, -60, 120); g.DrawArc(pen, 10, 5, 20, 22, -55, 110);
                        break;
                    case 3: // translation
                        g.DrawArc(pen, 3, 4, 19, 16, 135, 280); g.DrawLine(pen, 7, 18, 5, 22); g.DrawLine(pen, 5, 22, 11, 20);
                        g.DrawString("A", font, brush, 8, 5);
                        g.DrawArc(pen, 13, 11, 16, 14, 300, 200); g.DrawLine(pen, 25, 23, 27, 27); g.DrawLine(pen, 27, 27, 22, 25);
                        break;
                    case 4:
                        g.DrawEllipse(pen, 4, 4, 24, 24); g.DrawLine(pen, 16, 8, 16, 16); g.DrawLine(pen, 16, 16, 22, 20); break;
                    case 5:
                        g.DrawPolygon(pen, new[] { new Point(5, 26), new Point(8, 18), new Point(23, 3), new Point(29, 9), new Point(14, 24) }); g.DrawLine(pen, 8, 18, 14, 24); break;
                    case 6:
                        g.DrawRectangle(pen, 3, 5, 26, 22); g.DrawLine(pen, 8, 20, 24, 20); break;
                    case 7: // multiple files
                        g.DrawLines(pen, new[] { new Point(6, 21), new Point(4, 21), new Point(4, 3), new Point(18, 3), new Point(22, 7), new Point(22, 9) });
                        g.DrawLines(pen, new[] { new Point(9, 25), new Point(8, 25), new Point(8, 7), new Point(22, 7) });
                        g.DrawRectangle(pen, 12, 11, 16, 18); g.DrawLine(pen, 16, 17, 24, 17); g.DrawLine(pen, 16, 21, 24, 21); g.DrawLine(pen, 16, 25, 22, 25);
                        break;
                }
            }
            return image;
        }
    }
}
