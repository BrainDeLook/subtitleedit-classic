using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private ClassicCustomShortcuts _customClassicShortcuts;
        private bool _runningClassicSteps;
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        private static bool ClassicRightAlt => (GetAsyncKeyState(0xA5) & 0x8000) != 0;
        private void InitializeClassicCustomShortcuts()
        {
            if (_customClassicShortcuts != null) return;
            _customClassicShortcuts = ClassicCustomShortcuts.Load();
            var item = new ToolStripMenuItem(ClassicAssistedText.L("Составные горячие клавиши…", "Custom shortcut steps…")) { Name = "classicShortcutSteps" };
            item.Click += (s, e) => EditClassicCustomShortcuts(); optionsToolStripMenuItem.DropDownItems.Add(item);
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Handle configured steps before WinForms menu shortcuts. AltGr must
            // remain available for keyboard-layout text input, including plugin menus.
            var keys = new KeyEventArgs(keyData);
            if (HandleClassicCustomShortcut(keys)) return true;
            if (ClassicRightAlt) return false;
            return base.ProcessCmdKey(ref msg, keyData);
        }
        private bool HandleClassicCustomShortcut(KeyEventArgs e)
        {
            if (_runningClassicSteps || _paletteAction != null || _customClassicShortcuts == null) return false;
            var slot = _customClassicShortcuts.Slots.FirstOrDefault(s => s.Matches(e.KeyData, ClassicRightAlt) &&
                (s.Scope == "everywhere" || s.Scope == "grid" && SubtitleListview1.ContainsFocus || s.Scope == "text" && textBoxListViewText.ContainsFocus || s.Scope == "waveform" && audioVisualizer.ContainsFocus));
            if (slot == null) return false;
            e.SuppressKeyPress = true; RunClassicCustomSteps(slot); return true;
        }
        private void RunClassicCustomSteps(ClassicCustomShortcut slot)
        {
            if (_runningClassicSteps || slot.Steps.Count == 0) return;
            _runningClassicSteps = true;
            var wasPaused = _makeHistoryPaused;
            MakeHistoryForUndo(slot.Name); _makeHistoryPaused = true;
            try
            {
                // Commands are resolved again for every step; a preceding command can change
                // document state or install/remove a menu (including plugin commands).
                foreach (var step in slot.Steps)
                {
                    if (IsDisposed) break;
                    if (step.Kind == "command")
                    {
                        var command = BuildClassicPaletteCommands().FirstOrDefault(c => c.Id == step.Value);
                        if (command == null || !ClassicCommandPalette.Run(command)) throw new InvalidOperationException("Command is unavailable: " + step.Value);
                        continue;
                    }
                    var indices = SubtitleListview1.GetSelectedIndices();
                    if (indices.Length == 0) continue;
                    MakeHistoryForUndo(slot.Name);
                    foreach (var index in indices)
                    {
                        var p = _subtitle.Paragraphs[index];
                        if (step.Kind == "replace") p.Text = step.Replace(p.Text);
                        else if (step.Position == "start") p.Text = step.Value + p.Text;
                        else if (step.Position == "end") p.Text += step.Value;
                        else if (textBoxListViewText.ContainsFocus && index == FirstSelectedIndex)
                        {
                            textBoxListViewText.SelectedText = step.Value; p.Text = textBoxListViewText.Text;
                        }
                        else p.Text += step.Value;
                    }
                    SubtitleListview1.Fill(_subtitle, _subtitleOriginal); SubtitleListview1.SelectIndexAndEnsureVisible(indices[0]);
                    RefreshSelectedParagraph(); audioVisualizer.Invalidate();
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, slot.Name); }
            finally { _makeHistoryPaused = wasPaused; _runningClassicSteps = false; }
        }
        private void EditClassicCustomShortcuts()
        {
            using (var form = new Form { Text = ClassicAssistedText.L("Составные клавиши и плагины", "Custom shortcuts and plugins"), Size = new Size(1050, 690), MinimumSize = new Size(800, 540), StartPosition = FormStartPosition.CenterParent })
            {
                var copy = JsonConvert.DeserializeObject<ClassicCustomShortcuts>(JsonConvert.SerializeObject(_customClassicShortcuts));
                var slots = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
                var name = new TextBox { Width = 180 };
                var scopes = new[] { "everywhere", "grid", "text", "waveform" };
                var scope = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
                scope.Items.AddRange(new object[] { ClassicAssistedText.L("Везде", "Everywhere"), ClassicAssistedText.L("Таблица", "Subtitle grid"), ClassicAssistedText.L("Поле текста", "Text box"), ClassicAssistedText.L("Осциллограмма", "Waveform") });
                var altgr = new CheckBox { Text = "AltGr", AutoSize = true };
                var capture = new Button { AutoSize = true };
                var steps = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
                steps.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = ClassicAssistedText.L("Действие", "Action"), DataPropertyName = "Kind", DataSource = new[] { "command", "insert", "replace" } });
                steps.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = ClassicAssistedText.L("Текст / команда / поиск", "Text / command / search"), DataPropertyName = "Value", FillWeight = 180 });
                steps.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = ClassicAssistedText.L("Замена", "Replacement"), DataPropertyName = "Replacement" });
                steps.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Regex", DataPropertyName = "Regex", FillWeight = 45 });
                steps.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = ClassicAssistedText.L("Регистр", "Case"), DataPropertyName = "CaseSensitive", FillWeight = 45 });
                steps.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = ClassicAssistedText.L("Вставлять", "Insert at"), DataPropertyName = "Position", DataSource = new[] { "cursor", "start", "end" } });
                int previous = -1;
                System.ComponentModel.BindingList<ClassicShortcutStep> rows = null;
                Action commit = () => { if (previous < 0) return; steps.EndEdit(); var slot = copy.Slots[previous]; slot.Name = name.Text; slot.AltGr = altgr.Checked; slot.Scope = scopes[Math.Max(0, scope.SelectedIndex)]; slot.Steps = rows.ToList(); };
                for (int i = 0; i < 8; i++) slots.Items.Add(ClassicAssistedText.L("Слот ", "Slot ") + (i + 1));
                slots.SelectedIndexChanged += (sender,args) => { commit(); previous = slots.SelectedIndex; var slot = copy.Slots[previous]; name.Text = slot.Name; scope.SelectedIndex = Math.Max(0, Array.IndexOf(scopes, slot.Scope)); altgr.Checked = slot.AltGr; capture.Text = slot.Key == Keys.None ? ClassicAssistedText.L("Назначить клавиши…", "Assign keys…") : new KeysConverter().ConvertToString(slot.Key); rows = new System.ComponentModel.BindingList<ClassicShortcutStep>(slot.Steps); steps.DataSource = rows; };
                capture.Click += (sender,args) =>
                {
                    using (var key = new Form { Text = ClassicAssistedText.L("Нажмите сочетание", "Press a shortcut"), ClientSize = new Size(430, 100), KeyPreview = true, StartPosition = FormStartPosition.CenterParent })
                    {
                        key.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Text = ClassicAssistedText.L("Ctrl/Alt + клавиша или F1–F24. Esc — отмена.", "Ctrl/Alt + key or F1–F24. Esc cancels.") });
                        key.KeyDown += (a,e) => { if (new[] { Keys.ControlKey, Keys.Menu, Keys.ShiftKey, Keys.RMenu, Keys.LMenu }.Contains(e.KeyCode)) return; if (e.KeyCode == Keys.Escape) { key.Close(); return; } e.SuppressKeyPress = true; copy.Slots[previous].Key = e.KeyData; altgr.Checked = ClassicRightAlt; capture.Text = new KeysConverter().ConvertToString(e.KeyData); key.Close(); }; key.ShowDialog(form);
                    }
                };
                var commands = new ComboBox { Width = 500, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" }; commands.Items.AddRange(BuildClassicPaletteCommands().Cast<object>().ToArray());
                var addCommand = new Button { Text = ClassicAssistedText.L("Добавить команду", "Add command"), AutoSize = true };
                var insert = new Button { Text = ClassicAssistedText.L("Вставка текста", "Insert text"), AutoSize = true };
                var replace = new Button { Text = ClassicAssistedText.L("Поиск и замена", "Find and replace"), AutoSize = true };
                addCommand.Click += (sender,args) => { if (commands.SelectedItem is ClassicPaletteCommand cmd) rows.Add(new ClassicShortcutStep { Kind = "command", Value = cmd.Id }); };
                insert.Click += (sender,args) => rows.Add(new ClassicShortcutStep { Kind = "insert" }); replace.Click += (sender,args) => rows.Add(new ClassicShortcutStep { Kind = "replace" });
                Button up = new Button { Text = "↑", AutoSize = true }, down = new Button { Text = "↓", AutoSize = true };
                Action<int> move = delta => { int i = steps.CurrentRow?.Index ?? -1, next = i + delta; if (i < 0 || next < 0 || next >= rows.Count) return; var item = rows[i]; rows.RemoveAt(i); rows.Insert(next, item); steps.CurrentCell = steps.Rows[next].Cells[0]; }; up.Click += (sender,args) => move(-1); down.Click += (sender,args) => move(1);
                var save = new Button { Text = LanguageSettings.Current.General.Ok, AutoSize = true };
                save.Click += (sender,args) => { try { commit(); copy.Save(); _customClassicShortcuts = copy; form.DialogResult = DialogResult.OK; } catch (Exception ex) { MessageBox.Show(form, ex.Message); } };
                var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, AutoScroll = true }; top.Controls.AddRange(new Control[] { slots, name, scope, altgr, capture });
                var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 90, AutoScroll = true }; bottom.Controls.AddRange(new Control[] { commands, addCommand, insert, replace, up, down, save });
                form.Controls.Add(steps); form.Controls.Add(top); form.Controls.Add(bottom); slots.SelectedIndex = 0; form.ShowDialog(this);
            }
        }
    }
}
