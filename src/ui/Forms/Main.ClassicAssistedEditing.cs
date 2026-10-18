using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private ToolStripMenuItem _assistedSplitMenu, _assistedMoveMenu, _assistedSplitContext, _assistedMoveContext;
        private sealed class AssistedKeys { public int Split { get; set; } public int Move { get; set; } }
        private string AssistedKeysPath => Path.Combine(Configuration.DataDirectory, "ClassicAssistedEditingKeys.json");
        private void InitializeClassicAssistedEditing()
        {
            if (_assistedSplitMenu == null)
            {
                _assistedSplitMenu = new ToolStripMenuItem { Name = "classicAssistedSplit" }; _assistedMoveMenu = new ToolStripMenuItem { Name = "classicAssistedMove" };
                _assistedSplitContext = new ToolStripMenuItem { Name = "classicAssistedSplitContext" }; _assistedMoveContext = new ToolStripMenuItem { Name = "classicAssistedMoveContext" };
                toolsToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator()); toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { _assistedSplitMenu, _assistedMoveMenu });
                contextMenuStripListView.Items.Insert(0, new ToolStripSeparator()); contextMenuStripListView.Items.Insert(0, _assistedMoveContext); contextMenuStripListView.Items.Insert(0, _assistedSplitContext);
                _assistedSplitMenu.Click += (s, e) => ShowClassicAssisted(true); _assistedSplitContext.Click += (s, e) => ShowClassicAssisted(true);
                _assistedMoveMenu.Click += (s, e) => ShowClassicAssisted(false); _assistedMoveContext.Click += (s, e) => ShowClassicAssisted(false);
                SubtitleListview1.SelectedIndexChanged += (s, e) => RefreshClassicAssistedAvailability();
            }
            _assistedSplitMenu.Text = _assistedSplitContext.Text = ClassicAssistedText.L("Умное разделение реплики…", "Assisted split…");
            _assistedMoveMenu.Text = _assistedMoveContext.Text = ClassicAssistedText.L("Умный перенос текста…", "Assisted move…");
            RefreshClassicAssistedKeys(); RefreshClassicAssistedAvailability();
        }
        private void RefreshClassicAssistedAvailability()
        {
            var ready = SubtitleListview1.SelectedIndices.Count == 1 && FirstSelectedIndex >= 0 && FirstSelectedIndex < _subtitle.Paragraphs.Count;
            if (_assistedSplitMenu == null) return;
            _assistedSplitMenu.Enabled = _assistedMoveMenu.Enabled = _assistedSplitContext.Enabled = _assistedMoveContext.Enabled = ready;
        }
        private static string AssistedContent(Subtitle subtitle) => subtitle == null ? string.Empty : ClassicCompareContent(subtitle);
        private void ShowClassicAssisted(bool split)
        {
            var index = FirstSelectedIndex;
            if (SubtitleListview1.SelectedIndices.Count != 1 || index < 0 || index >= _subtitle.Paragraphs.Count) return;
            var baseline = new Subtitle(_subtitle, false); var original = _subtitleOriginal == null ? null : new Subtitle(_subtitleOriginal, false);
            var choices = split ? ClassicAssistedWorkflow.Split(baseline, original, IsOriginalEditable, index) : ClassicAssistedWorkflow.Move(baseline, original, IsOriginalEditable, index);
            if (choices.Count == 0) { ShowStatus(split ? ClassicAssistedText.L("Для этой реплики нет вариантов разделения.", "No split suggestions for this line.") : ClassicAssistedText.L("Нет вариантов переноса: предложение не продолжается в соседних репликах.", "No move suggestions: the sentence does not continue into the previous/next subtitle.")); return; }
            mediaPlayer.Pause();
            using (var form = new ClassicAssistedEditing(baseline.Paragraphs[index], choices, split, ConfigureClassicAssistedKeys))
                if (form.ShowDialog(this) == DialogResult.OK && form.SelectedChoice != null)
                    if (!ApplyClassicAssisted(baseline, original, form.SelectedChoice, split)) MessageBox.Show(this, ClassicAssistedText.L("Субтитры изменились. Открой варианты заново.", "The subtitles changed. Open the suggestions again."));
        }
        private bool ApplyClassicAssisted(Subtitle baseline, Subtitle original, ClassicAssistedChoice choice, bool split)
        {
            if (choice == null || AssistedContent(_subtitle) != AssistedContent(baseline) || AssistedContent(_subtitleOriginal) != AssistedContent(original)) return false;
            MakeHistoryForUndo(split ? "Before assisted split" : "Before assisted move");
            _subtitle.Paragraphs.Clear(); _subtitle.Paragraphs.AddRange(choice.Result.Paragraphs.Select(p => new Paragraph(p, false)));
            if (choice.OriginalResult != null && IsOriginalEditable) { _subtitleOriginal.Paragraphs.Clear(); _subtitleOriginal.Paragraphs.AddRange(choice.OriginalResult.Paragraphs.Select(p => new Paragraph(p, false))); }
            SubtitleListview1.Fill(_subtitle, _subtitleOriginal); _subtitleListViewIndex = -1; SubtitleListview1.SelectIndexAndEnsureVisible(choice.SelectedIndex);
            audioVisualizer.Invalidate(); UpdateSourceView(); SetTitle(); ShowStatus(split ? ClassicAssistedText.L("Реплика разделена.", "Subtitle split.") : ClassicAssistedText.L("Текст перенесён.", "Text moved.")); return true;
        }
        private AssistedKeys LoadClassicAssistedKeys()
        {
            try { return JsonConvert.DeserializeObject<AssistedKeys>(File.ReadAllText(AssistedKeysPath)) ?? new AssistedKeys(); } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (JsonException) { } return new AssistedKeys();
        }
        private void RefreshClassicAssistedKeys()
        {
            if (_assistedSplitMenu == null) return;
            var keys = LoadClassicAssistedKeys();
            _assistedSplitMenu.ShortcutKeys = ValidClassicPaletteKey((Keys)keys.Split) && !ClassicPaletteKeyConflict((Keys)keys.Split) ? (Keys)keys.Split : Keys.None;
            _assistedMoveMenu.ShortcutKeys = ValidClassicPaletteKey((Keys)keys.Move) && !ClassicPaletteKeyConflict((Keys)keys.Move) && (keys.Move == 0 || keys.Move != keys.Split) ? (Keys)keys.Move : Keys.None;
            _assistedSplitContext.ShortcutKeyDisplayString = _assistedSplitMenu.ShortcutKeys == Keys.None ? "" : new KeysConverter().ConvertToString(_assistedSplitMenu.ShortcutKeys);
            _assistedMoveContext.ShortcutKeyDisplayString = _assistedMoveMenu.ShortcutKeys == Keys.None ? "" : new KeysConverter().ConvertToString(_assistedMoveMenu.ShortcutKeys);
        }
        private void SaveClassicAssistedKeys(Keys split, Keys move)
        {
            if (!ValidClassicPaletteKey(split) || !ValidClassicPaletteKey(move)) throw new ArgumentException(ClassicAssistedText.L("Используйте Ctrl/Alt с клавишей или F1–F24.", "Use Ctrl/Alt with a key or F1–F24."));
            foreach (var key in new[] { split, move }) if (key != Keys.None && (ClassicPaletteKeyConflict(key) || LoadClassicPaletteShortcut() == key)) throw new ArgumentException(ClassicAssistedText.L("Сочетание занято другой командой.", "This shortcut belongs to another command."));
            if (split != Keys.None && split == move) throw new ArgumentException(ClassicAssistedText.L("Для команд нужны разные сочетания.", "Choose different shortcuts for the two commands."));
            var temp = AssistedKeysPath + ".tmp";
            try { File.WriteAllText(temp, JsonConvert.SerializeObject(new AssistedKeys { Split = (int)split, Move = (int)move })); if (File.Exists(AssistedKeysPath)) File.Replace(temp, AssistedKeysPath, null); else File.Move(temp, AssistedKeysPath); } finally { if (File.Exists(temp)) File.Delete(temp); }
            RefreshClassicAssistedKeys();
        }
        private void ConfigureClassicAssistedKeys()
        {
            using (var form = new Form { Text = ClassicAssistedText.L("Клавиши умного редактирования", "Assisted editing shortcuts"), ClientSize = new Size(560, 170), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
            {
                var keys = LoadClassicAssistedKeys(); var split = (Keys)keys.Split; var move = (Keys)keys.Move;
                var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 3 }; grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                var splitBox = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, Text = new KeysConverter().ConvertToString(split) }; var moveBox = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, Text = new KeysConverter().ConvertToString(move) };
                splitBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back) split = Keys.None; else if (ValidClassicPaletteKey(e.KeyData)) split = e.KeyData; splitBox.Text = new KeysConverter().ConvertToString(split); e.SuppressKeyPress = true; };
                moveBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back) move = Keys.None; else if (ValidClassicPaletteKey(e.KeyData)) move = e.KeyData; moveBox.Text = new KeysConverter().ConvertToString(move); e.SuppressKeyPress = true; };
                grid.Controls.Add(new Label { AutoSize = true, Text = _assistedSplitMenu.Text }, 0, 0); grid.Controls.Add(splitBox, 1, 0); grid.Controls.Add(new Label { AutoSize = true, Text = _assistedMoveMenu.Text }, 0, 1); grid.Controls.Add(moveBox, 1, 1);
                var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; var ok = new Button { Text = LanguageSettings.Current.General.Ok, AutoSize = true }; var cancel = new Button { Text = LanguageSettings.Current.General.Cancel, AutoSize = true, DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => { try { SaveClassicAssistedKeys(split, move); form.DialogResult = DialogResult.OK; } catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException) { MessageBox.Show(form, ex.Message, form.Text); } };
                footer.Controls.Add(cancel); footer.Controls.Add(ok); grid.Controls.Add(footer, 0, 2); grid.SetColumnSpan(footer, 2); form.Controls.Add(grid); form.CancelButton = cancel; form.AcceptButton = ok; UiUtil.FixFonts(form); form.ShowDialog(Form.ActiveForm ?? this);
            }
        }
    }
}
