using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Core.Translate;
using Nikse.SubtitleEdit.Core.AutoTranslate;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class BatchConvert
    {
        private ClassicBatch53Options _batch53 = new ClassicBatch53Options();
        private readonly Dictionary<Subtitle, List<TranslateResult>> _batch53Translations = new Dictionary<Subtitle, List<TranslateResult>>();
        private readonly Dictionary<ListViewItem, string> _batch53PriorStatuses = new Dictionary<ListViewItem, string>();
        private TranslationPair _batch53Target;
        private readonly Dictionary<Subtitle, string> _batch53TsSuffix = new Dictionary<Subtitle, string>();
        private readonly Dictionary<Subtitle, TranslateResult> _batch53Primary = new Dictionary<Subtitle, TranslateResult>();
        private static IEnumerable<Control> Batch53Controls(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (var child in Batch53Controls(c)) yield return child; } }
        private void InitializeClassicBatch53()
        {
            var presetsFile = Path.Combine(Configuration.DataDirectory, "ClassicBatch53Presets.json");
            Dictionary<string, ClassicBatch53Preset> presets;
            try { presets = JsonConvert.DeserializeObject<Dictionary<string, ClassicBatch53Preset>>(File.ReadAllText(presetsFile)); } catch (IOException) { presets = null; } catch (JsonException) { presets = null; }
            if (presets == null) presets = new Dictionary<string, ClassicBatch53Preset>();
            int height = 40; SuspendLayout(); ClientSize = new Size(ClientSize.Width, ClientSize.Height + height); foreach (Control c in Controls) if ((c.Anchor & AnchorStyles.Top) != 0) c.Top += height; ResumeLayout();
            var row = new FlowLayoutPanel { Dock = DockStyle.Top, Height = height, AutoScroll = true, Name = "classic53PresetRow" };
            var name = new ComboBox { Width = 190 }; name.Items.AddRange(presets.Keys.Cast<object>().ToArray());
            Button save = new Button { Text = ClassicAssistedText.L("Сохранить пресет", "Save preset"), AutoSize = true }, load = new Button { Text = ClassicAssistedText.L("Загрузить", "Load"), AutoSize = true }, delete = new Button { Text = ClassicAssistedText.L("Удалить", "Delete"), AutoSize = true }, options = new Button { Text = ClassicAssistedText.L("Параметры 5.3…", "5.3 options…"), AutoSize = true };
            void Persist() { Directory.CreateDirectory(Path.GetDirectoryName(presetsFile)); var part = presetsFile + ".tmp"; File.WriteAllText(part, JsonConvert.SerializeObject(presets, Newtonsoft.Json.Formatting.Indented)); if (File.Exists(presetsFile)) File.Replace(part, presetsFile, null); else File.Move(part, presetsFile); name.Items.Clear(); name.Items.AddRange(presets.Keys.Cast<object>().ToArray()); }
            save.Click += (s,e) => { try { if (string.IsNullOrWhiteSpace(name.Text)) throw new ArgumentException("Enter a preset name."); var preset = new ClassicBatch53Preset { Options = JsonConvert.DeserializeObject<ClassicBatch53Options>(JsonConvert.SerializeObject(_batch53)) }; foreach (var c in Batch53Controls(this).Where(c => !string.IsNullOrEmpty(c.Name) && c != name)) { if (c is CheckBox check) preset.Controls[c.Name] = check.Checked.ToString(); else if (c is RadioButton radio) preset.Controls[c.Name] = radio.Checked.ToString(); else if (c is NumericUpDown numeric) preset.Controls[c.Name] = numeric.Value.ToString(CultureInfo.InvariantCulture); else if (c is NikseUpDown nikseNumeric) preset.Controls[c.Name] = nikseNumeric.Value.ToString(CultureInfo.InvariantCulture); else if (c is ComboBox || c is TextBox || c is NikseComboBox) preset.Controls[c.Name] = c.Text; } preset.Actions = listViewConvertOptions.Items.Cast<ListViewItem>().Where(i => i.Checked).Select(i => ((FixActionItem)i.Tag).Action.ToString()).ToList(); presets[name.Text.Trim()] = preset; Persist(); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } };
            load.Click += (s,e) => { try { if (!presets.TryGetValue(name.Text, out var preset)) return; var restored = JsonConvert.DeserializeObject<ClassicBatch53Options>(JsonConvert.SerializeObject(preset.Options)) ?? new ClassicBatch53Options(); restored.Validate(); _batch53 = restored; if (preset.Controls == null) preset.Controls = new Dictionary<string,string>(); if (preset.Actions == null) preset.Actions = new List<string>(); foreach (var c in Batch53Controls(this).ToList()) if (preset.Controls.TryGetValue(c.Name, out var value)) { if (c is CheckBox check) check.Checked = bool.Parse(value); else if (c is RadioButton radio) radio.Checked = bool.Parse(value); else if (c is NumericUpDown numeric) numeric.Value = Math.Max(numeric.Minimum, Math.Min(numeric.Maximum, decimal.Parse(value, CultureInfo.InvariantCulture))); else if (c is NikseUpDown nikseNumeric) nikseNumeric.Value = Math.Max(nikseNumeric.Minimum, Math.Min(nikseNumeric.Maximum, decimal.Parse(value, CultureInfo.InvariantCulture))); else if (c is ComboBox || c is TextBox || c is NikseComboBox) c.Text = value; } foreach (ListViewItem item in listViewConvertOptions.Items) item.Checked = preset.Actions.Contains(((FixActionItem)item.Tag).Action.ToString()); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } };
            delete.Click += (s,e) => { try { presets.Remove(name.Text); Persist(); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } };
            options.Click += (s,e) =>
            {
                using (var form = new ClassicBatchOptionsForm(_batch53))
                    if (form.ShowDialog(this) == DialogResult.OK) _batch53 = form.Options;
            };
            row.Controls.AddRange(new Control[] { name, save, load, delete, options }); Controls.Add(row); row.BringToFront();
            FormClosed += (s,e) => _batch53Translations.Clear();
        }
        private bool SkipClassicBatchItem(ListViewItem item)
        {
            string kind = item.SubItems.Count > 2 ? item.SubItems[2].Text : "";
            switch (_batch53.Filter)
            {
                case "format": return kind.IndexOf(_batch53.FilterValue, StringComparison.OrdinalIgnoreCase) < 0;
                case "extension": return !Path.GetExtension(item.Text).TrimStart('.').Equals(_batch53.FilterValue.TrimStart('.'), StringComparison.OrdinalIgnoreCase);
                case "filename": return Path.GetFileName(item.Text).IndexOf(_batch53.FilterValue, StringComparison.OrdinalIgnoreCase) < 0;
                case "language": return (kind + " " + Path.GetFileNameWithoutExtension(item.Text)).IndexOf(_batch53.FilterValue, StringComparison.OrdinalIgnoreCase) < 0;
                case "forced": return kind.StartsWith("Matroska/", StringComparison.OrdinalIgnoreCase) && kind.IndexOf("(forced)", StringComparison.OrdinalIgnoreCase) < 0;
                case "error": _batch53PriorStatuses.TryGetValue(item, out var status); return string.IsNullOrWhiteSpace(status) || status == "-" || status == LanguageSettings.Current.BatchConvert.Converted || status == LanguageSettings.Current.BatchConvert.FilterSkipped;
                default: return false;
            }
        }
        private TranslateResult RunAutoTranslate(Subtitle subtitle)
        {
            if (_batch53.Languages.Length == 0) return RunAutoTranslateOne(subtitle);
            var results = new List<TranslateResult>();
            try
            {
                foreach (var code in _batch53.Languages)
                {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    _batch53Target = _autoTranslator.GetSupportedTargetLanguages().FirstOrDefault(p => p.Code.Equals(code, StringComparison.OrdinalIgnoreCase) || p.TwoLetterIsoLanguageName.Equals(code, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Unsupported target language: " + code);
                    results.Add(RunAutoTranslateOne(new Subtitle(subtitle, false)));
                }
                _batch53Translations[results[0].Subtitle] = results.Skip(1).ToList(); _batch53Primary[results[0].Subtitle] = results[0]; return results[0];
            }
            finally { _batch53Target = null; }
        }
        private void PrepareClassicTsSave(ThreadDoWorkParameter p, string suffix)
        {
            _batch53TsSuffix[p.TranslationIdentity ?? p.Subtitle] = suffix;
            if (_batch53Primary.TryGetValue(p.TranslationIdentity ?? p.Subtitle, out var first))
                p.TargetFileName = FileNameHelper.GetFileNameWithTargetLanguage(Path.Combine(Path.GetDirectoryName(p.FileName), Path.GetFileNameWithoutExtension(p.FileName) + suffix + p.Format.Extension), null, null, p.Format, first.Source.TwoLetterIsoLanguageName, first.Target.TwoLetterIsoLanguageName);
            _batch53.Apply(p.Subtitle, p.Format, _cancellationTokenSource.Token);
        }
        private void SaveClassicTsVariants(ThreadDoWorkParameter p)
        {
            var key = p.TranslationIdentity ?? p.Subtitle;
            if (!_batch53Translations.TryGetValue(key, out var variants)) return;
            _batch53Translations.Remove(key); _batch53Primary.Remove(key);
            _batch53TsSuffix.TryGetValue(key, out var suffix); _batch53TsSuffix.Remove(key);
            foreach (var result in variants)
            {
                var next = p.Copy(); next.Subtitle = result.Subtitle;
                next.TargetFileName = FileNameHelper.GetFileNameWithTargetLanguage(Path.Combine(Path.GetDirectoryName(p.FileName), Path.GetFileNameWithoutExtension(p.FileName) + suffix + p.Format.Extension), null, null, p.Format, result.Source.TwoLetterIsoLanguageName, result.Target.TwoLetterIsoLanguageName);
                ApplyFixesStep2(next, Configuration.Settings.Tools.BatchConvertFixRtlMode); ThreadWorkerCompletedOne(next);
            }
        }
        private void ThreadWorkerCompleted(ThreadDoWorkParameter p)
        {
            _batch53Translations.TryGetValue(p.TranslationIdentity ?? p.Subtitle, out var variants); _batch53Translations.Remove(p.TranslationIdentity ?? p.Subtitle); _batch53Primary.Remove(p.TranslationIdentity ?? p.Subtitle);
            ThreadWorkerCompletedOne(p);
            if (variants == null) return;
            foreach (var result in variants)
            {
                _cancellationTokenSource.Token.ThrowIfCancellationRequested(); var next = p.Copy(); next.Subtitle = result.Subtitle;
                next.TargetFileName = FileNameHelper.GetFileNameWithTargetLanguage(p.FileName, null, null, p.Format, result.Source.TwoLetterIsoLanguageName, result.Target.TwoLetterIsoLanguageName);
                ApplyFixesStep2(next, Configuration.Settings.Tools.BatchConvertFixRtlMode); ThreadWorkerCompletedOne(next);
            }
        }
    }
}
