using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using Nikse.SubtitleEdit.Logic.ClassicImageExport;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicImageExportForm : Form
    {
        private readonly Subtitle _subtitle;
        private readonly ComboBox _format = Combo(), _effect = Combo(), _stereo = Combo();
        private readonly TextBox _font = new TextBox { Text = "Arial", Dock = DockStyle.Fill };
        private readonly NumericUpDown _width = Number(320, 8192, 1920), _height = Number(240, 4320, 1080), _size = Number(8, 300, 48), _margin = Number(0, 500, 30), _depth = Number(-100, 100, 0);
        private readonly NumericUpDown _fps = new NumericUpDown { Minimum = 1, Maximum = 120, DecimalPlaces = 3, Value = 23.976m, Dock = DockStyle.Fill };
        private readonly CheckBox _full = new CheckBox { AutoSize = true }, _styles = new CheckBox { AutoSize = true, Checked = true };
        private readonly PictureBox _preview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.DimGray };
        private readonly Label _status = new Label { AutoSize = true }; private readonly Button _export = new Button { AutoSize = true };
        private CancellationTokenSource _operation; private bool _closing;
        private static string L(string ru, string en) => ClassicAssistedText.L(ru, en);
        private static ComboBox Combo() => new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private static NumericUpDown Number(int min, int max, int value) => new NumericUpDown { Minimum = min, Maximum = max, Value = value, Dock = DockStyle.Fill };
        public ClassicImageExportForm(Subtitle source)
        {
            _subtitle = new Subtitle(source, false);
            Text = L("Экспорт изображений: IMSC / BDN / DVD SUP", "Export images: IMSC / BDN / DVD SUP");
            Size = new Size(960, 700); MinimumSize = new Size(760, 610); StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 265)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoScroll = true };
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22)); settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28)); settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22)); settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            _format.Items.AddRange(new object[] { "IMSC 1.1 image (TTML + PNG)", "BDN XML + RGBA PNG", "BDN XML + 8-bit PNG", "DVD SUP (PAL/NTSC)" }); _format.SelectedIndex = 0;
            _effect.DataSource = Enum.GetValues(typeof(TextEffectPreset)); _stereo.DataSource = Enum.GetValues(typeof(Export3DMode));
            Add(settings, 0, 0, L("Формат", "Format"), _format); Add(settings, 2, 0, L("Эффект", "Effect"), _effect);
            Add(settings, 0, 1, L("Ширина", "Width"), _width); Add(settings, 2, 1, L("Высота", "Height"), _height);
            Add(settings, 0, 2, L("Шрифт", "Font"), _font); Add(settings, 2, 2, L("Размер", "Size"), _size);
            Add(settings, 0, 3, L("Кадров/с", "Frames/s"), _fps); Add(settings, 2, 3, L("Отступ", "Margin"), _margin);
            Add(settings, 0, 4, "3D", _stereo); Add(settings, 2, 4, L("Глубина 3D", "3D depth"), _depth);
            _full.Text = L("Изображение размером с кадр", "Full frame image"); _styles.Text = L("Учитывать стили ASS", "Use ASS styles");
            settings.Controls.Add(_full, 0, 5); settings.SetColumnSpan(_full, 2); settings.Controls.Add(_styles, 2, 5); settings.SetColumnSpan(_styles, 2);
            var preview = new Button { Text = L("Обновить предпросмотр", "Refresh preview"), AutoSize = true }; preview.Click += (s, e) => Preview(); settings.Controls.Add(preview, 0, 6); settings.SetColumnSpan(preview, 2);
            root.Controls.Add(settings, 0, 0); root.Controls.Add(_preview, 0, 1);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = L("Закрыть / отменить", "Close / cancel"), AutoSize = true }; cancel.Click += (s, e) => { if (_operation != null) _operation.Cancel(); else Close(); };
            _export.Text = L("Экспорт в новую папку…", "Export to new folder…"); _export.Click += async (s, e) =>
            {
                if (_operation != null) return;
                using (var dialog = new SaveFileDialog { Title = L("Имя новой папки результата", "New output folder name"), FileName = "subtitle-images", Filter = "Output folder name|*.*", CheckFileExists = false, OverwritePrompt = false })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    var options = Options(); var format = _format.SelectedIndex; _operation = new CancellationTokenSource(); settings.Enabled = false; _export.Enabled = false;
                    try { await ClassicImageJob.Export(_subtitle, options, format, dialog.FileName, _operation.Token, new Progress<int>(p => _status.Text = p + "%")); _status.Text = L("Готово: ", "Saved: ") + dialog.FileName; }
                    catch (OperationCanceledException) { _status.Text = L("Отменено", "Cancelled"); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
                    finally { _operation.Dispose(); _operation = null; settings.Enabled = true; _export.Enabled = true; if (_closing) Close(); }
                }
            };
            footer.Controls.Add(cancel); footer.Controls.Add(_export); footer.Controls.Add(_status); root.Controls.Add(footer, 0, 2); Controls.Add(root);
            _format.SelectedIndexChanged += (s, e) => { if (_format.SelectedIndex == 3) { _width.Value = 720; _height.Value = 576; _fps.Value = 25; _full.Checked = false; _stereo.SelectedIndex = 0; } };
            FormClosing += (s, e) => { if (_operation != null) { _closing = true; _operation.Cancel(); e.Cancel = true; } };
            FormClosed += (s, e) => _preview.Image?.Dispose(); Shown += (s, e) => Preview(); UiUtil.FixFonts(this);
        }
        private static void Add(TableLayoutPanel table, int col, int row, string text, Control control)
        { if(table.RowStyles.Count <= row) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); table.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, col, row); table.Controls.Add(control, col + 1, row); }
        private ClassicImageOptions Options() => new ClassicImageOptions { Width = (int)_width.Value, Height = (int)_height.Value, FontName = _font.Text, FontSize = (float)_size.Value, FrameRate = (double)_fps.Value, Margin = (int)_margin.Value, FullFrame = _full.Checked, UseAssStyles = _styles.Checked, Effect = (TextEffectPreset)_effect.SelectedItem, Stereo = (Export3DMode)_stereo.SelectedItem, Depth = (int)_depth.Value };
        private void Preview()
        {
            if (_subtitle.Paragraphs.Count == 0) return;
            try { var ip = ClassicImageJob.Render(_subtitle, _subtitle.Paragraphs[0], Options()); using (ip.Bitmap) using (var frame = FullFrameImage.Create(ip)) using (var stream = new MemoryStream(frame.ToPngArray())) using (var image = Image.FromStream(stream)) { var old = _preview.Image; _preview.Image = new Bitmap(image); old?.Dispose(); } }
            catch (Exception ex) { _status.Text = ex.Message; }
        }
    }
}
