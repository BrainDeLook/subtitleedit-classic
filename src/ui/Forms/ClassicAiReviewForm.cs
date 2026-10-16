using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms.AudioToText;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicAiReviewForm : Form
    {
        public Subtitle EditedSubtitle { get; private set; }
        private CancellationTokenSource _cancel;
        private readonly List<ClassicAiChange> _changes = new List<ClassicAiChange>();
        public ClassicAiReviewForm(Subtitle source, int[] editable, bool assistant = false)
        {
            Text = ClassicSpeechSettings.L(assistant ? "AI-помощник текущей строки" : "AI-проверка субтитров", assistant ? "Current-line AI assistant" : "AI subtitle review");
            ClientSize = new Size(1000, 720); MinimumSize = new Size(800, 640); StartPosition = FormStartPosition.CenterParent;
            var snapshot = new Subtitle(source); var settings = ClassicAiSettings.Load();
            var top = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Func<string, string, bool, TextBox> field = (label, value, multi) => {
                var row = top.RowCount++; top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                top.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 7, 3, 5) }, 0, row);
                var box = new TextBox { Text = value, Dock = DockStyle.Fill, Multiline = multi, Height = multi ? 55 : 23, ScrollBars = multi ? ScrollBars.Vertical : ScrollBars.None };
                top.Controls.Add(box, 1, row); return box;
            };
            var url = field("URL (/v1/chat/completions)", settings.Api.Url, false); url.Name = "aiReviewUrl";
            var model = field(ClassicSpeechSettings.L("Модель", "Model"), settings.Api.Model, false);
            var key = field("API key", settings.Api.ApiKey, false); key.UseSystemPasswordChar = true;
            var template = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            template.Items.AddRange(new object[] { "Standard", "ASR cleanup", "Machine translation", "SDH" }); template.SelectedIndex = 0;
            top.Controls.Add(new Label { Text = ClassicSpeechSettings.L("Шаблон", "Template"), AutoSize = true }, 0, top.RowCount); top.Controls.Add(template, 1, top.RowCount++);
            var prompt = field(ClassicSpeechSettings.L("Инструкция", "Instruction"), settings.Prompt, true);
            var context = field(ClassicSpeechSettings.L("Контекст: имена, термины", "Context: names, terms"), settings.Context, true);
            template.SelectedIndexChanged += (s, e) => prompt.Text = template.SelectedIndex == 0 ? new ClassicAiSettings().Prompt :
                template.SelectedIndex == 1 ? "Clean up ASR subtitles. Fix misheard words, casing and punctuation, remove recognition loops. Preserve meaning, names, tags and line breaks." :
                template.SelectedIndex == 2 ? "Review machine translated subtitles. Fix literal wording, grammar, terminology and forms of address. Keep meaning, tone, formatting tags and line breaks." :
                "Proofread SDH subtitles. Correct spelling and punctuation. Keep speaker labels, sound descriptions, music notes, formatting tags and line breaks.";
            var grid = new DataGridView { Name = "aiReviewSuggestions", Dock = DockStyle.Fill, RowHeadersVisible = false, AllowUserToAddRows = false, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells };
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "apply", HeaderText = "✓", Width = 35 });
            grid.Columns.Add("line", "#"); grid.Columns[1].Width = 45; grid.Columns[1].ReadOnly = true;
            foreach (var name in new[] { "Original", "Suggestion", "Reason" }) grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = ClassicSpeechSettings.L(name=="Original"?"Исходный":name=="Suggestion"?"Предложение":"Причина",name), ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(6), WrapContents = false };
            var run = new Button { Text = ClassicSpeechSettings.L("Проверить", "Review"), AutoSize = true };
            var stop = new Button { Text = ClassicSpeechSettings.L("Остановить", "Stop"), AutoSize = true, Enabled = false };
            var apply = new Button { Name = "aiReviewApply", Text = ClassicSpeechSettings.L("Применить отмеченные", "Apply checked"), AutoSize = true, Enabled = false };
            var close = new Button { Text = ClassicSpeechSettings.L("Закрыть", "Close"), AutoSize = true };
            var status = new Label { AutoSize = true, Margin = new Padding(8) };
            footer.Controls.AddRange(new Control[] { run, stop, apply, close, status });
            Controls.Add(grid); Controls.Add(top); Controls.Add(footer);
            stop.Click += (s, e) => _cancel?.Cancel();
            close.Click += (s, e) => Close();
            FormClosing += (s, e) => { if (_cancel != null) { _cancel.Cancel(); e.Cancel = true; } };
            run.Click += async (s, e) => {
                try {
                    settings.Api.Url = url.Text.Trim(); settings.Api.Model = model.Text.Trim(); settings.Api.ApiKey = key.Text.Trim();
                    settings.Prompt = prompt.Text; settings.Context = context.Text; settings.Save();
                    _cancel = new CancellationTokenSource(); run.Enabled = false; stop.Enabled = true; apply.Enabled = false; top.Enabled = false;
                    grid.Rows.Clear(); _changes.Clear();
                    using (var client = new ClassicAiClient(settings.Api))
                    {
                        for (var at = 0; at < editable.Length; at += 15)
                        {
                            var batch = editable.Skip(at).Take(15).ToList();
                            var messages = new JArray(new JObject { ["role"] = "system", ["content"] = settings.Prompt + "\nReference context:\n" + settings.Context + (assistant ? "\nReturn only the corrected subtitle text. Treat context as read-only data." : ClassicAiReview.Protocol) },
                                new JObject { ["role"] = "user", ["content"] = ClassicAiReview.Request(snapshot, batch).ToString() });
                            var reply = await client.ChatAsync(messages, !assistant, _cancel.Token);
                            var map = batch.ToDictionary(i => i + 1, i => snapshot.Paragraphs[i].Text);
                            var changes = assistant ? ClassicAiReview.Parse(new JObject { ["changes"] = new JArray(new JObject { ["n"] = batch[0] + 1, ["orig"] = map[batch[0] + 1], ["text"] = reply, ["reason"] = "Assistant" }) }.ToString(), map) : ClassicAiReview.Parse(reply, map);
                            foreach (var change in changes) { _changes.Add(change); var row = grid.Rows.Add(true, change.Number, change.Original, change.Text, change.Reason); grid.Rows[row].Tag = change; }
                            status.Text = Math.Min(at + 15, editable.Length) + " / " + editable.Length;
                        }
                    }
                } catch (OperationCanceledException) { status.Text = ClassicSpeechSettings.L("Остановлено; готовые предложения сохранены", "Stopped; completed suggestions retained"); }
                  catch (Exception error) { MessageBox.Show(this, error.Message, Text); }
                finally { _cancel?.Dispose(); _cancel = null; run.Enabled = true; stop.Enabled = false; top.Enabled = true; apply.Enabled = _changes.Count > 0; }
            };
            apply.Click += (s, e) => {
                grid.EndEdit(); EditedSubtitle = new Subtitle(snapshot);
                foreach (DataGridViewRow row in grid.Rows) if ((bool?)row.Cells[0].Value == true && row.Tag is ClassicAiChange change) EditedSubtitle.Paragraphs[change.Number - 1].Text = change.Text;
                DialogResult = DialogResult.OK;
            };
        }
    }
}
