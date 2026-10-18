using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Core.Common;
using SettingsForm = Nikse.SubtitleEdit.Forms.Options.Settings;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private string _paletteAction;
        private ToolStripMenuItem _paletteMenuItem;
        private bool ClassicShortcutMatches(Keys pressed, Keys assigned, string action) => _paletteAction == null ? pressed == assigned : _paletteAction == action;

        private void InitializeClassicCommandPalette()
        {
            if (_paletteMenuItem != null) return;
            _paletteMenuItem = new ToolStripMenuItem(VoiceReferenceRu ? "Палитра команд…" : "Command palette…") { Name = "classicCommandPalette" };
            RefreshClassicPaletteShortcut();
            _paletteMenuItem.Click += (s, e) => ShowClassicCommandPalette();
            editToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator()); editToolStripMenuItem.DropDownItems.Add(_paletteMenuItem);
        }
        private void RefreshClassicPaletteShortcut()
        {
            if (_paletteMenuItem == null) return;
            var keys = LoadClassicPaletteShortcut();
            _paletteMenuItem.ShortcutKeys = ClassicPaletteKeyConflict(keys) ? Keys.None : keys;
        }
        private string ClassicPaletteKeyFile => Path.Combine(Configuration.DataDirectory, "ClassicCommandPaletteKey.txt");
        private Keys LoadClassicPaletteShortcut()
        {
            try { if (int.TryParse(File.ReadAllText(ClassicPaletteKeyFile), out var stored) && ValidClassicPaletteKey((Keys)stored)) return (Keys)stored; }
            catch (IOException) { }
            return Keys.Control | Keys.Shift | Keys.K;
        }
        private static bool ValidClassicPaletteKey(Keys keys)
        {
            var code = keys & Keys.KeyCode;
            if (((int)keys & ~(int)(Keys.KeyCode | Keys.Control | Keys.Alt | Keys.Shift)) != 0 || !Enum.IsDefined(typeof(Keys), code)) return false;
            return keys == Keys.None || (code >= Keys.F1 && code <= Keys.F24) || ((keys & (Keys.Control | Keys.Alt)) != 0 && code >= Keys.Back && code != Keys.ControlKey && code != Keys.ShiftKey && code != Keys.Menu && code != Keys.Escape);
        }
        private bool ClassicPaletteKeyConflict(Keys keys)
        {
            if (keys == Keys.None) return false;
            return Configuration.Settings.Shortcuts.GetType().GetProperties().Where(p => p.PropertyType == typeof(string)).Any(p => UiUtil.GetKeys((string)p.GetValue(Configuration.Settings.Shortcuts)) == keys);
        }
        private void SaveClassicPaletteShortcut(Keys keys)
        {
            if (!ValidClassicPaletteKey(keys)) throw new ArgumentException(VoiceReferenceRu ? "Используйте Ctrl/Alt с клавишей или F1–F24." : "Use Ctrl/Alt with a key, or F1–F24.");
            if (ClassicPaletteKeyConflict(keys)) throw new InvalidOperationException(VoiceReferenceRu ? "Это сочетание уже назначено другой команде." : "This shortcut is already assigned to another command.");
            Directory.CreateDirectory(Configuration.DataDirectory); File.WriteAllText(ClassicPaletteKeyFile, ((int)keys).ToString(System.Globalization.CultureInfo.InvariantCulture)); RefreshClassicPaletteShortcut();
        }
        private void ConfigureClassicPaletteShortcut()
        {
            using (var dialog = new Form { Text = VoiceReferenceRu ? "Клавиши палитры команд" : "Command palette shortcut", ClientSize = new Size(470, 145), StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false, MinimizeBox = false, MaximizeBox = false, FormBorderStyle = FormBorderStyle.FixedDialog })
            {
                var keys = LoadClassicPaletteShortcut();
                var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 3 };
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                var label = new Label { Dock = DockStyle.Fill, Text = VoiceReferenceRu ? "Нажмите нужное сочетание клавиш:" : "Press the desired key combination:" };
                var input = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, Text = new KeysConverter().ConvertToString(keys) };
                input.KeyDown += (s,e) => { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) return; keys = e.KeyData; input.Text = new KeysConverter().ConvertToString(keys); e.SuppressKeyPress = true; };
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                var cancel = new Button { Text = LanguageSettings.Current.General.Cancel, DialogResult = DialogResult.Cancel, AutoSize = true };
                var save = new Button { Text = LanguageSettings.Current.General.Ok, AutoSize = true };
                var clear = new Button { Text = VoiceReferenceRu ? "Без клавиш" : "No shortcut", AutoSize = true };
                clear.Click += (s,e) => { keys = Keys.None; input.Text = ""; };
                save.Click += (s,e) => { try { SaveClassicPaletteShortcut(keys); dialog.DialogResult = DialogResult.OK; } catch (Exception ex) { SeMsgBox.MessageBox.Show(dialog, ex.Message); } };
                buttons.Controls.Add(cancel); buttons.Controls.Add(save); buttons.Controls.Add(clear);
                root.Controls.Add(label,0,0); root.Controls.Add(input,0,1); root.Controls.Add(buttons,0,2); dialog.Controls.Add(root); dialog.CancelButton = cancel; dialog.AcceptButton = save; UiUtil.FixFonts(dialog); dialog.ShowDialog(Form.ActiveForm ?? this);
            }
        }
        private bool ClassicPaletteShortcutAvailable(string action)
        {
            var needsMedia = action.StartsWith("MainVideo", StringComparison.Ordinal) || action.StartsWith("Waveform", StringComparison.Ordinal) || action.StartsWith("MainAdjust", StringComparison.Ordinal) || action.StartsWith("MainCreate", StringComparison.Ordinal);
            return !needsMedia || mediaPlayer.VideoPlayer != null;
        }
        private bool RunClassicPaletteShortcut(string action)
        {
            if (_paletteAction != null || !ClassicCommandBindings.SettingsNames.ContainsKey(action) || IsDisposed || !ClassicPaletteShortcutAvailable(action)) return false;
            try
            {
                _paletteAction = action;
                // Explicit named dispatch reuses existing guards and undo, without rebinding keys.
                var args = new KeyEventArgs((Keys)0x0100ffff);
                MainKeyDown(this, args);
                if (!args.Handled) SubtitleListview1KeyDown(this, args);
                if (!args.Handled) AudioVisualizer_KeyDown(this, args);
                return args.SuppressKeyPress || args.Handled;
            }
            finally { _paletteAction = null; }
        }
        private static bool PaletteMenuEnabled(ToolStripMenuItem item)
        {
            for (ToolStripMenuItem current = item; current != null; current = current.OwnerItem as ToolStripMenuItem)
                if (!current.Available || !current.Enabled) return false;
            return true;
        }
        private List<ClassicPaletteCommand> BuildClassicPaletteCommands()
        {
            var commands = new List<ClassicPaletteCommand>();
            var metadata = SettingsForm.BuildClassicShortcutTree(LanguageSettings.Current.Settings);
            var bySetting = new Dictionary<string, Tuple<string, string>>();
            foreach (var group in metadata.Nodes)
                foreach (var node in group.Nodes)
                {
                    if (node.Shortcut?.Shortcut == null) continue;
                    var name = node.Text; var bracket = name.LastIndexOf(" [", StringComparison.Ordinal); if (bracket >= 0) name = name.Substring(0, bracket);
                    bySetting[node.Shortcut.Shortcut.Name] = Tuple.Create(ClassicCommandPalette.Clean(name), ClassicCommandPalette.Clean(group.Text));
                }
            foreach (var binding in ClassicCommandBindings.SettingsNames)
            {
                if (!bySetting.TryGetValue(binding.Value, out var info)) continue;
                if (binding.Key.StartsWith("MainTextBox", StringComparison.Ordinal)) continue;
                var action = binding.Key;
                var property = Configuration.Settings.Shortcuts.GetType().GetProperty(binding.Value);
                var shortcut = property?.GetValue(Configuration.Settings.Shortcuts) as string;
                commands.Add(new ClassicPaletteCommand { Id = action, Name = info.Item1, Group = info.Item2, Shortcut = shortcut ?? "", Enabled = ClassicPaletteShortcutAvailable(action), Execute = () => RunClassicPaletteShortcut(action) });
            }
            AddClassicPaletteMenus(menuStrip1.Items, "", commands);
            return commands;
        }
        private void AddClassicPaletteMenus(ToolStripItemCollection items, string group, List<ClassicPaletteCommand> commands)
        {
            foreach (var menu in items.OfType<ToolStripMenuItem>())
            {
                if (menu == _paletteMenuItem || !menu.Available) continue;
                var name = ClassicCommandPalette.Clean(menu.Text);
                if (menu.HasDropDownItems) { AddClassicPaletteMenus(menu.DropDownItems, group.Length == 0 ? name : group + " → " + name, commands); continue; }
                if (string.IsNullOrWhiteSpace(name)) continue;
                // Menu-only actions include modern Classic additions and installed plugins.
                var existing = commands.FirstOrDefault(c => c.Name == name && c.Group == group);
                if (existing != null) continue;
                commands.Add(new ClassicPaletteCommand { Id = "menu:" + (string.IsNullOrEmpty(menu.Name) ? group + "/" + name : menu.Name), Name = name, Group = group, Shortcut = menu.ShortcutKeys == Keys.None ? menu.ShortcutKeyDisplayString ?? "" : new KeysConverter().ConvertToString(menu.ShortcutKeys), Enabled = PaletteMenuEnabled(menu), Execute = () => { if (!PaletteMenuEnabled(menu) || menu.IsDisposed) return false; menu.PerformClick(); return true; } });
            }
        }
        private void ShowClassicCommandPalette()
        {
            var focus = UiUtil.FindFocusedControl(this);
            using (var palette = new ClassicCommandPaletteForm(BuildClassicPaletteCommands(), ConfigureClassicPaletteShortcut))
            {
                if (palette.ShowDialog(this) != DialogResult.OK) return;
                focus?.Focus(); ClassicCommandPalette.Run(palette.SelectedCommand);
            }
        }
    }
}
