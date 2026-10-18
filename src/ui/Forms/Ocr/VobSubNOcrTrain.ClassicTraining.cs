using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.Ocr;

namespace Nikse.SubtitleEdit.Forms.Ocr
{
    public sealed partial class VobSubNOcrTrain
    {
        private bool _russian;
        private bool _closeAfterTraining;
        private ComboBox _baseDatabase;
        private TableLayoutPanel _trainingBody;
        private PictureBox _fontPreview;
        private string TrainingPreferences => Path.Combine(Configuration.DataDirectory, "ClassicNOcrTraining.json");

        public static int[] ParseFontSizes(string text)
        {
            var parts = (text ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var values = new List<int>();
            foreach (var part in parts)
            {
                if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var size) || size < 10 || size > 200)
                    throw new FormatException("Font sizes must be whole numbers from 10 to 200, separated by commas (30, 40).");
                if (!values.Contains(size)) values.Add(size);
            }
            if (values.Count == 0) throw new FormatException("Enter at least one font size (30, 40).");
            return values.ToArray();
        }

        // The original database is read into a separate object and never written during training.
        public static NOcrDb TrainSizes(NOcrTrainerSettings settings, IEnumerable<int> sizes, string basePath, Func<bool> abort, IProgress<string> progress = null)
        {
            var db = new NOcrDb(string.IsNullOrWhiteSpace(basePath) ? null : basePath);
            int learned = 0, skipped = 0;
            var watch = Stopwatch.StartNew();
            foreach (var size in sizes)
            {
                if (abort?.Invoke() == true) break;
                settings.FontSize = size;
                int sizeLearned = 0, sizeSkipped = 0;
                new NOcrTrainer().Train(settings, db, abort, p =>
                {
                    sizeLearned = p.CharactersLearned; sizeSkipped = p.CharactersSkipped;
                    if (watch.ElapsedMilliseconds < 100) return;
                    progress?.Report(p.CurrentFontName + " / " + size + " px / " + p.CurrentText + " — " + (learned + sizeLearned) + " / " + (skipped + sizeSkipped));
                    watch.Restart();
                });
                learned += sizeLearned; skipped += sizeSkipped;
                progress?.Report(size + " px — " + learned + " / " + skipped);
            }
            return db;
        }

        private void InitializeClassicTraining()
        {
            _russian = (Configuration.Settings.General.Language ?? "").StartsWith("ru", StringComparison.OrdinalIgnoreCase);
            SuspendLayout();
            // Reuse the classic controls inside a scrollable layout, including at larger DPI.
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };
            _trainingBody = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(4) };
            _trainingBody.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
            _trainingBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8), WrapContents = true };
            footer.Controls.Add(buttonOK); footer.Controls.Add(buttonTrain);
            labelInfo.AutoSize = false; labelInfo.Dock = DockStyle.Bottom; labelInfo.Height = 44; labelInfo.Padding = new Padding(10, 4, 10, 4);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, Padding = Padding.Empty };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            scroll.Margin = Padding.Empty; footer.Margin = Padding.Empty; labelInfo.Margin = Padding.Empty;
            root.Controls.Add(scroll, 0, 0); root.Controls.Add(footer, 0, 1); root.Controls.Add(labelInfo, 0, 2);
            Controls.Add(root); root.BringToFront(); scroll.Controls.Add(_trainingBody);
            AddTrainingRow(_russian ? "SRT для обучения (необязательно)" : "Training SRT (optional)", Pair(textBoxInputFile, buttonInputChoose), 40);
            listViewFonts.Height = 155;
            AddTrainingRow(_russian ? "Шрифты (отметьте нужные)" : "Fonts (check to train)", listViewFonts, 165);
            comboBoxSubtitleFontSize.DropDownStyle = ComboBoxStyle.DropDown;
            comboBoxSubtitleFontSize.Text = "30, 40";
            AddTrainingRow(_russian ? "Размеры, px (10–200)" : "Sizes, px (10–200)", comboBoxSubtitleFontSize, 36);
            var styles = new FlowLayoutPanel { Dock = DockStyle.Fill };
            checkBoxBold.AutoSize = true; checkBoxItalic.AutoSize = true;
            styles.Controls.Add(checkBoxBold); styles.Controls.Add(checkBoxItalic);
            AddTrainingRow(_russian ? "Начертания" : "Styles", styles, 34);
            _fontPreview = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.Black, Height = 76 };
            _fontPreview.Paint += PaintFontPreview;
            AddTrainingRow(_russian ? "Предпросмотр шрифта" : "Font preview", _fontPreview, 86);
            _baseDatabase = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
            _baseDatabase.Items.Add("");
            if (Directory.Exists(Configuration.OcrDirectory)) _baseDatabase.Items.AddRange(Directory.GetFiles(Configuration.OcrDirectory, "*.nocr"));
            var browse = new Button { Text = "…", Width = 34, Dock = DockStyle.Right };
            browse.Click += (s, e) => { using (var dialog = new OpenFileDialog { Filter = "nOCR DB|*.nocr", InitialDirectory = Configuration.OcrDirectory }) if (dialog.ShowDialog(this) == DialogResult.OK) _baseDatabase.Text = dialog.FileName; };
            AddTrainingRow(_russian ? "Исходная база (пусто = новая)" : "Base database (blank = new)", Pair(_baseDatabase, browse), 40);
            AddTrainingRow(_russian ? "Сегментов на символ" : "Segments per character", numericUpDownSegmentsPerCharacter, 36);
            AddTrainingRow(_russian ? "Алгоритм линий" : "Line algorithm", _lineAlgorithm, 36);
            AddTrainingRow(_russian ? "Сочетания букв" : "Letter combinations", textBoxMerged, 36);
            AddTrainingRow(_russian ? "Символы без SRT" : "Characters without SRT", _trainingCharacters, 80);
            // Old containers are now empty of reused inputs.
            groupBoxInput.Visible = false; groupBoxTrainingOptions.Visible = false;
            foreach (Control c in Controls.Cast<Control>().ToArray())
                if (c != root) c.Visible = false;
            try
            {
                var prefs = JObject.Parse(File.ReadAllText(TrainingPreferences));
                var sizes = (string)prefs["sizes"]; ParseFontSizes(sizes); comboBoxSubtitleFontSize.Text = sizes;
                _baseDatabase.Text = (string)prefs["baseDatabase"] ?? "";
            }
            catch (Exception ex) when (ex is IOException || ex is FormatException || ex is Newtonsoft.Json.JsonException) { }
            listViewFonts.SelectedIndexChanged += (s, e) => _fontPreview.Invalidate();
            comboBoxSubtitleFontSize.TextChanged += (s, e) => _fontPreview.Invalidate();
            checkBoxBold.CheckedChanged += (s, e) => _fontPreview.Invalidate();
            checkBoxItalic.CheckedChanged += (s, e) => _fontPreview.Invalidate();
            if (listViewFonts.Items.Count > 0) listViewFonts.Items[0].Selected = true;
            MinimumSize = new Size(640, 500); ClientSize = new Size(740, 770);
            ResumeLayout(true);
        }

        private void AddTrainingRow(string caption, Control control, int height)
        {
            var row = _trainingBody.RowCount++;
            _trainingBody.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            _trainingBody.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            control.Dock = DockStyle.Fill; control.Margin = new Padding(3, 5, 3, 5);
            _trainingBody.Controls.Add(control, 1, row);
        }
        private static Control Pair(Control input, Control button)
        {
            var pair = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, Margin = Padding.Empty };
            pair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); pair.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            input.Dock = DockStyle.Fill; button.Dock = DockStyle.Fill; pair.Controls.Add(input, 0, 0); pair.Controls.Add(button, 1, 0);
            return pair;
        }
        private void PaintFontPreview(object sender, PaintEventArgs e)
        {
            var name = listViewFonts.SelectedItems.Count > 0 ? listViewFonts.SelectedItems[0].Text : Font.FontFamily.Name;
            int size; try { size = ParseFontSizes(comboBoxSubtitleFontSize.Text)[0]; } catch (FormatException) { size = 30; }
            var style = (checkBoxBold.Checked ? FontStyle.Bold : FontStyle.Regular) | (checkBoxItalic.Checked ? FontStyle.Italic : FontStyle.Regular);
            try
            {
                using (var original = new Font(name, size, style, GraphicsUnit.Pixel))
                {
                    const string sample = "Subtitle Edit · Пример 123";
                    var measured = e.Graphics.MeasureString(sample, original);
                    var scale = Math.Min(1f, Math.Min((_fontPreview.Width - 10) / measured.Width, (_fontPreview.Height - 10) / measured.Height));
                    using (var font = new Font(name, Math.Max(1, size * scale), style, GraphicsUnit.Pixel))
                    using (var brush = new SolidBrush(Color.White)) e.Graphics.DrawString(sample, font, brush, new PointF(5, 5));
                }
            }
            catch (ArgumentException) { }
        }
        private void SaveClassicTrainingPreferences()
        {
            try
            {
                Directory.CreateDirectory(Configuration.DataDirectory);
                File.WriteAllText(TrainingPreferences, new JObject { ["sizes"] = comboBoxSubtitleFontSize.Text, ["baseDatabase"] = _baseDatabase.Text }.ToString());
            }
            catch (IOException) { }
        }
    }
}
