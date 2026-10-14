using Nikse.SubtitleEdit.Logic;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed class ClassicBurnInAdvanced : Form
    {
        private readonly ClassicBurnInOptions _options;
        private readonly bool _russian;
        private readonly CheckBox _useStyle;
        private readonly ComboBox _alignment;
        private readonly ComboBox _box;
        private readonly NumericUpDown _marginH, _marginV, _shadow, _spacing;
        private readonly Button _shadowColor;
        private readonly CheckedListBox _effects;
        private readonly TextBox _logoPath, _supPath;
        private readonly NumericUpDown _logoX, _logoY, _logoSize, _logoOpacity;
        private readonly CheckBox _sourceSize;
        private readonly ComboBox _stereoMode;
        private readonly NumericUpDown _stereoDepth;
        private readonly PictureBox _canvas;
        private readonly string _videoFileName;
        private readonly int _videoWidth, _videoHeight;
        private Image _logoImage, _frameImage;
        private Point? _dragStart;
        private Point _dragLogoPosition;
        private static readonly string[] EffectIds = { "FadeInOut", "SlowFontSizeChange", "IncreaseFontKerning", "ScrollUp", "ScrollDown", "RotateIn", "TiltBounce", "FontSizeBounceIn" };
        public ClassicBurnInOptions Options { get; private set; }

        private string T(string ru, string en) => _russian ? ru : en;

        public ClassicBurnInAdvanced(ClassicBurnInOptions options, bool isAssa, string videoFileName, int width, int height)
        {
            UiUtil.PreInitialize(this);
            _options = options.Clone();
            _russian = LanguageSettings.Current.General.CultureName.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
            _videoFileName = videoFileName;
            _videoWidth = Math.Max(1, width);
            _videoHeight = Math.Max(1, height);
            Text = T("Хардсаб — дополнительные настройки", "Burn-in — advanced settings");
            Name = "ClassicBurnInAdvanced";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(690, 600);
            MinimumSize = new Size(620, 580);
            ShowInTaskbar = false;
            KeyPreview = true;
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 47, Padding = new Padding(8), FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = LanguageSettings.Current.General.Cancel, DialogResult = DialogResult.Cancel, AutoSize = true };
            var ok = new Button { Text = LanguageSettings.Current.General.Ok, AutoSize = true };
            ok.Click += (s, e) => AcceptOptions();
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            CancelButton = cancel;
            AcceptButton = ok;
            Controls.Add(tabs);
            Controls.Add(buttons);

            var textPage = AddPage(tabs, T("Оформление", "Text style"));
            var textTable = Table(textPage);
            _useStyle = new CheckBox { Text = T("Дополнительное оформление обычных субтитров", "Use advanced styling for text subtitles"), Checked = options.UseTextStyle, AutoSize = true };
            AddFull(textTable, _useStyle);
            AddFull(textTable, Note(isAssa ? T("У ASS сохраняются собственные стили. Эти настройки применяются к SRT и другим текстовым форматам.", "ASS keeps its own styles. These settings apply to SRT and other text formats.") : T("Основные шрифт, цвет и контур задаются в привычном окне хардсаба.", "Font, text color and outline stay in the main burn-in window.")));
            _alignment = Combo(new[] { T("Снизу слева", "Bottom left"), T("Снизу по центру", "Bottom center"), T("Снизу справа", "Bottom right"), T("Посередине слева", "Middle left"), T("Посередине по центру", "Middle center"), T("Посередине справа", "Middle right"), T("Сверху слева", "Top left"), T("Сверху по центру", "Top center"), T("Сверху справа", "Top right") }, Math.Max(0, Math.Min(8, options.Alignment - 1)));
            _marginH = Number(0, 8192, options.MarginHorizontal);
            _marginV = Number(0, 8192, options.MarginVertical);
            _shadow = Number(0, 100, options.ShadowWidth, 1);
            _spacing = Number(-20, 100, options.Spacing, 1);
            _box = Combo(new[] { T("Без подложки", "No box"), T("Отдельная подложка каждой строки", "Box per line"), T("Общая подложка", "One box") }, Math.Max(0, Math.Min(2, options.BoxType)));
            _shadowColor = new Button { Text = T("Цвет тени…", "Shadow color…"), BackColor = Color.FromArgb(options.ShadowColorArgb), ForeColor = Color.White, AutoSize = true };
            _shadowColor.Click += (s, e) => { using (var dialog = new ColorDialog { Color = _shadowColor.BackColor, FullOpen = true }) if (dialog.ShowDialog(this) == DialogResult.OK) { _shadowColor.BackColor = dialog.Color; _shadowColor.ForeColor = dialog.Color.GetBrightness() > .5 ? Color.Black : Color.White; } };
            AddRow(textTable, T("Положение", "Alignment"), _alignment);
            AddRow(textTable, T("Отступ по горизонтали, px", "Horizontal margin, px"), _marginH);
            AddRow(textTable, T("Отступ по вертикали, px", "Vertical margin, px"), _marginV);
            AddRow(textTable, T("Ширина тени, px", "Shadow width, px"), _shadow);
            AddRow(textTable, T("Цвет тени", "Shadow color"), _shadowColor);
            AddRow(textTable, T("Межбуквенный интервал", "Letter spacing"), _spacing);
            AddRow(textTable, T("Подложка", "Box"), _box);
            _useStyle.Enabled = !isAssa;
            EventHandler toggleStyle = (s, e) => { foreach (var control in new Control[] { _alignment, _marginH, _marginV, _shadow, _spacing, _box, _shadowColor }) control.Enabled = _useStyle.Checked && !isAssa; };
            _useStyle.CheckedChanged += toggleStyle;
            toggleStyle(null, null);

            var effectPage = AddPage(tabs, T("Эффекты", "Effects"));
            var effectTable = Table(effectPage);
            AddFull(effectTable, Note(T("Выбранные эффекты применяются к каждой строке при экспорте. Исходный файл субтитров сохраняет своё оформление.", "Selected effects apply to each exported subtitle. The source subtitle retains its formatting.")));
            _effects = new CheckedListBox { CheckOnClick = true, Dock = DockStyle.Top, Height = 250, IntegralHeight = false };
            var effectNames = _russian ? new[] { "Плавное появление и исчезновение", "Плавное увеличение размера", "Увеличение межбуквенного интервала", "Движение вверх", "Движение вниз", "Поворот при появлении", "Покачивание", "Появление с изменением размера" } : new[] { "Fade in / out", "Slow font size change", "Increase font kerning", "Scroll up", "Scroll down", "Rotate in", "Tilt bounce", "Font size bounce in" };
            for (var i = 0; i < EffectIds.Length; i++) _effects.Items.Add(effectNames[i], (options.Effects ?? string.Empty).Split(',').Contains(EffectIds[i]));
            AddFull(effectTable, _effects);

            var logoPage = AddPage(tabs, T("Логотип", "Logo"));
            var logoTable = Table(logoPage);
            _logoPath = PathBox(options.LogoFileName);
            AddRow(logoTable, T("Изображение PNG", "PNG image"), FilePicker(_logoPath, "PNG|*.png", LoadLogo));
            var clearLogo = new Button { Text = T("Убрать логотип", "Remove logo"), AutoSize = true };
            clearLogo.Click += (s, e) => { _logoPath.Clear(); LoadLogo(); };
            AddRow(logoTable, string.Empty, clearLogo);
            _logoX = Number(0, 16384, options.LogoX);
            _logoY = Number(0, 16384, options.LogoY);
            _logoSize = Number(1, 500, options.LogoSize);
            _logoOpacity = Number(0, 100, options.LogoOpacity);
            AddRow(logoTable, T("Позиция X, px", "X position, px"), _logoX);
            AddRow(logoTable, T("Позиция Y, px", "Y position, px"), _logoY);
            AddRow(logoTable, T("Размер, % от изображения", "Size, % of image"), _logoSize);
            AddRow(logoTable, T("Непрозрачность, %", "Opacity, %"), _logoOpacity);
            AddFull(logoTable, Note(T("Перетащи логотип на кадре или задай координаты. Положение относится к выходному разрешению.", "Drag the logo or enter coordinates. Position uses the output resolution.")));
            _canvas = new PictureBox { Dock = DockStyle.Top, Height = 185, BackColor = Color.Black, BorderStyle = BorderStyle.FixedSingle };
            _canvas.Paint += PaintCanvas;
            _canvas.MouseDown += (s, e) => { if (_logoImage != null && LogoRectangle().Contains(e.Location)) { _dragStart = e.Location; _dragLogoPosition = new Point((int)_logoX.Value, (int)_logoY.Value); _canvas.Capture = true; } };
            _canvas.MouseMove += (s, e) => { if (!_dragStart.HasValue) return; var scale = CanvasScale(); _logoX.Value = Math.Min(_logoX.Maximum, Math.Max(0, _dragLogoPosition.X + (e.X - _dragStart.Value.X) / scale)); _logoY.Value = Math.Min(_logoY.Maximum, Math.Max(0, _dragLogoPosition.Y + (e.Y - _dragStart.Value.Y) / scale)); };
            _canvas.MouseUp += (s, e) => { _dragStart = null; _canvas.Capture = false; };
            foreach (var control in new[] { _logoX, _logoY, _logoSize, _logoOpacity }) control.ValueChanged += (s, e) => _canvas.Invalidate();
            AddFull(logoTable, _canvas);
            LoadLogo();

            var sourcePage = AddPage(tabs, T("Источник и размер", "Source and size"));
            var sourceTable = Table(sourcePage);
            _stereoMode = Combo(new[] { T("Обычное 2D", "Normal 2D"), "Half side-by-side", "Half top-bottom" }, Math.Max(0, Math.Min(2, options.StereoMode)));
            _stereoDepth = Number(-100, 100, options.StereoDepth);
            _stereoDepth.Enabled = _stereoMode.SelectedIndex != 0;
            _stereoMode.SelectedIndexChanged += (s,e) => _stereoDepth.Enabled = _stereoMode.SelectedIndex != 0;
            AddRow(sourceTable, T("Режим 3D", "3D mode"), _stereoMode);
            AddRow(sourceTable, T("Глубина субтитров", "Subtitle depth"), _stereoDepth);
            AddFull(sourceTable, Note(T("Для видео, уже упакованного в 3D: субтитры дублируются для обоих глаз; видео не преобразуется из 2D в 3D.", "For video already packed as 3D: subtitles are duplicated for both eyes; this does not convert 2D video to 3D.")));
            AddFull(sourceTable, Note(T("Можно прожечь графические субтитры Blu-ray напрямую, без OCR. Пустое поле использует субтитры текущего редактора; в очереди .sup выбирается для каждого файла.", "Burn Blu-ray image subtitles without OCR. Leave empty to use the editor subtitle; batch items can each use a .sup file.")));
            _supPath = PathBox(options.ImageSubtitleFileName);
            AddRow(sourceTable, T("Графические субтитры", "Image subtitles"), FilePicker(_supPath, "Blu-ray SUP|*.sup", null));
            var clearSup = new Button { Text = T("Использовать текущие субтитры", "Use current subtitle"), AutoSize = true };
            clearSup.Click += (s, e) => _supPath.Clear();
            AddRow(sourceTable, string.Empty, clearSup);
            AddFull(sourceTable, Note(T("Для .sup шрифт, подложка и текстовые эффекты не применяются. Логотип, разрешение и обрезка остаются доступны.", "Font, box and text effects do not apply to .sup. Logo, resolution and cut remain available.")));
            _sourceSize = new CheckBox { Text = T("Размер результата как у исходного видео", "Match source video file size"), AutoSize = true, Checked = options.MatchSourceFileSize };
            AddFull(sourceTable, _sourceSize);
            AddFull(sourceTable, Note(T("Включает двухпроходное кодирование; в очереди размер рассчитывается отдельно для каждого видео.", "Enables two-pass encoding; batch mode calculates the target separately for each video.")));
            UiUtil.FixFonts(this);
            Shown += (s, e) => LoadFrame();
        }

        private static TabPage AddPage(TabControl tabs, string text) { var page = new TabPage(text) { Padding = new Padding(10), AutoScroll = true }; tabs.TabPages.Add(page); return page; }
        private static TableLayoutPanel Table(Control parent) { var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 4, 0, 4) }; table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); parent.Controls.Add(table); return table; }
        private static void AddRow(TableLayoutPanel table, string text, Control control) { var row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); table.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 9, 8, 9) }, 0, row); control.Margin = new Padding(3, 5, 3, 5); table.Controls.Add(control, 1, row); }
        private static void AddFull(TableLayoutPanel table, Control control) { var row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); control.Margin = new Padding(3, 5, 3, 7); table.Controls.Add(control, 0, row); table.SetColumnSpan(control, 2); }
        private static Label Note(string text) => new Label { Text = text, AutoSize = true, MaximumSize = new Size(635, 0), ForeColor = SystemColors.GrayText };
        private static NumericUpDown Number(decimal min, decimal max, decimal value, int decimals = 0) => new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Min(max, Math.Max(min, value)), DecimalPlaces = decimals, Increment = decimals > 0 ? .1m : 1, Width = 105 };
        private static ComboBox Combo(string[] names, int index) { var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top }; combo.Items.AddRange(names); combo.SelectedIndex = index; return combo; }
        private static TextBox PathBox(string path) => new TextBox { Text = path ?? string.Empty, ReadOnly = true, Dock = DockStyle.Fill };
        private Control FilePicker(TextBox text, string filter, Action changed) { var panel = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Top, AutoSize = true }; panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38)); var button = new Button { Text = "…", Dock = DockStyle.Fill }; button.Click += (s, e) => { using (var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true }) if (dialog.ShowDialog(this) == DialogResult.OK) { text.Text = dialog.FileName; changed?.Invoke(); } }; panel.Controls.Add(text, 0, 0); panel.Controls.Add(button, 1, 0); return panel; }

        private void AcceptOptions()
        {
            if (!string.IsNullOrWhiteSpace(_logoPath.Text) && !File.Exists(_logoPath.Text) || !string.IsNullOrWhiteSpace(_supPath.Text) && !File.Exists(_supPath.Text)) { MessageBox.Show(this, T("Выбранный файл не найден.", "Selected file was not found."), Text); return; }
            _options.UseTextStyle = _useStyle.Checked;
            _options.Alignment = _alignment.SelectedIndex + 1;
            _options.MarginHorizontal = (int)_marginH.Value;
            _options.MarginVertical = (int)_marginV.Value;
            _options.ShadowWidth = _shadow.Value;
            _options.ShadowColorArgb = _shadowColor.BackColor.ToArgb();
            _options.Spacing = _spacing.Value;
            _options.BoxType = _box.SelectedIndex;
            _options.Effects = string.Join(",", Enumerable.Range(0, EffectIds.Length).Where(i => _effects.GetItemChecked(i)).Select(i => EffectIds[i]));
            _options.LogoFileName = _logoPath.Text;
            _options.LogoX = (int)_logoX.Value;
            _options.LogoY = (int)_logoY.Value;
            _options.LogoSize = (int)_logoSize.Value;
            _options.LogoOpacity = (int)_logoOpacity.Value;
            _options.ImageSubtitleFileName = _supPath.Text;
            _options.MatchSourceFileSize = _sourceSize.Checked;
            _options.StereoMode = _stereoMode.SelectedIndex;
            _options.StereoDepth = (int)_stereoDepth.Value;
            Options = _options;
            DialogResult = DialogResult.OK;
        }

        private void LoadLogo()
        {
            _logoImage?.Dispose(); _logoImage = null;
            if (File.Exists(_logoPath.Text))
                try { using (var image = Image.FromFile(_logoPath.Text)) _logoImage = new Bitmap(image); }
                catch (ArgumentException) { MessageBox.Show(this, T("Не удалось прочитать изображение.", "Could not read the image."), Text); }
            _canvas?.Invalidate();
        }
        private void LoadFrame()
        {
            if (!File.Exists(_videoFileName)) return;
            string file = null;
            try { file = VideoPreviewGenerator.GetScreenShot(_videoFileName, "00:00:01"); if (File.Exists(file)) { using (var image = Image.FromFile(file)) _frameImage = new Bitmap(image); _canvas.Invalidate(); } }
            catch (Exception ex) { Nikse.SubtitleEdit.Core.Common.SeLogger.Error(ex, "Classic burn-in logo preview"); }
            finally { if (file != null && File.Exists(file)) try { File.Delete(file); } catch (IOException) { } }
        }
        private decimal CanvasScale() => Math.Max(.001m, Math.Min((decimal)_canvas.ClientSize.Width / _videoWidth, (decimal)_canvas.ClientSize.Height / _videoHeight));
        private RectangleF VideoRectangle() { var scale = (float)CanvasScale(); var w = _videoWidth * scale; var h = _videoHeight * scale; return new RectangleF((_canvas.ClientSize.Width - w) / 2, (_canvas.ClientSize.Height - h) / 2, w, h); }
        private RectangleF LogoRectangle() { if (_logoImage == null) return RectangleF.Empty; var video = VideoRectangle(); var scale = (float)CanvasScale(); var size = (float)_logoSize.Value / 100; return new RectangleF(video.X + (float)_logoX.Value * scale, video.Y + (float)_logoY.Value * scale, _logoImage.Width * size * scale, _logoImage.Height * size * scale); }
        private void PaintCanvas(object sender, PaintEventArgs e)
        {
            var video = VideoRectangle();
            if (_frameImage != null) e.Graphics.DrawImage(_frameImage, video);
            else { using (var brush = new SolidBrush(Color.FromArgb(32, 32, 32))) e.Graphics.FillRectangle(brush, video); }
            if (_logoImage == null) return;
            e.Graphics.SetClip(video);
            using (var attributes = new System.Drawing.Imaging.ImageAttributes()) { var matrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = (float)_logoOpacity.Value / 100 }; attributes.SetColorMatrix(matrix); e.Graphics.DrawImage(_logoImage, Rectangle.Round(LogoRectangle()), 0, 0, _logoImage.Width, _logoImage.Height, GraphicsUnit.Pixel, attributes); }
            e.Graphics.ResetClip();
        }
        protected override void Dispose(bool disposing) { if (disposing) { _logoImage?.Dispose(); _frameImage?.Dispose(); } base.Dispose(disposing); }
    }
}
