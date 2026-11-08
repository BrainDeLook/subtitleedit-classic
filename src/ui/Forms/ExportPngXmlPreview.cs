using Nikse.SubtitleEdit.Logic;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class ExportPngXmlPreview : Form
    {
        private double _zoomFactor = 100;
        private Bitmap _bmp;
        private bool _ownsBitmap;
        private Func<int, Bitmap> _imageProvider;
        private Action<int> _imageChanged;
        private int _imageCount;
        public int ImageIndex { get; private set; }
        private Button _previousButton, _nextButton;
        private Label _positionLabel;
        public bool AllowNext { get; set; }
        public bool NextPressed { get; private set; }
        public bool AllowPrevious { get; set; }
        public bool PreviousPressed { get; private set; }

        public ExportPngXmlPreview(Bitmap bmp)
        {
            UiUtil.PreInitialize(this);
            InitializeComponent();
            UiUtil.FixFonts(this);

            DoubleBuffered = true;

            _bmp = bmp;
            pictureBox1.Image = bmp;
            pictureBox1.Width = bmp.Width;
            pictureBox1.Height = bmp.Height;
            MaximumSize = new Size(bmp.Width + (Width - ClientSize.Width), bmp.Height + (Height - ClientSize.Height));
            var screen = Screen.FromPoint(Cursor.Position);
            if (screen.Bounds.Width > bmp.Width &&
                screen.Bounds.Height > bmp.Height)
            {
                ClientSize = new Size(bmp.Width, bmp.Height);
            }
            else
            {
                WindowState = FormWindowState.Maximized;
            }

            pictureBox2.Width = 1;
            pictureBox2.Height = 1;
            pictureBox2.Top = bmp.Height - 2;

            Text = $"{LanguageSettings.Current.General.Preview} {bmp.Width}x{bmp.Height}";

            MouseWheel += MouseWheelHandler;
        }

        public ExportPngXmlPreview(Func<int, Bitmap> imageProvider, int initialIndex, int count, Action<int> imageChanged = null)
            : this(count > 0 && initialIndex >= 0 && initialIndex < count ? imageProvider(initialIndex) : throw new ArgumentOutOfRangeException(nameof(initialIndex)))
        {
            _imageProvider = imageProvider; _imageCount = count; ImageIndex = initialIndex;
            _imageChanged = imageChanged; _ownsBitmap = true;
            MaximumSize = Size.Empty;
            MinimumSize = new Size(360, 240);
            var working = Screen.FromPoint(Cursor.Position).WorkingArea;
            ClientSize = new Size(Math.Min(Math.Max(ClientSize.Width, 520), Math.Max(360, working.Width - 32)),
                Math.Min(Math.Max(ClientSize.Height, 320), Math.Max(240, working.Height - 64)));
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(4), WrapContents = false };
            _previousButton = new Button { Name = "previousImage", Text = "←", Width = 48, Height = 25 };
            _nextButton = new Button { Name = "nextImage", Text = "→", Width = 48, Height = 25 };
            _positionLabel = new Label { AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
            _previousButton.Click += (sender, e) => MoveImage(-1);
            _nextButton.Click += (sender, e) => MoveImage(1);
            toolbar.Controls.AddRange(new Control[] { _previousButton, _nextButton, _positionLabel });
            Controls.Add(toolbar); toolbar.BringToFront();
            UpdateNavigation();
        }

        public bool MoveImage(int direction)
        {
            if (_imageProvider == null || direction == 0) return false;
            var index = ImageIndex + Math.Sign(direction);
            if (index < 0 || index >= _imageCount) return false;
            var next = _imageProvider(index); // Load before releasing the displayed frame.
            var previous = _bmp;
            _bmp = next; pictureBox1.Image = next; ImageIndex = index;
            Zoom(0); UpdateNavigation();
            previous.Dispose();
            _imageChanged?.Invoke(index);
            return true;
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (_imageProvider != null && (keyData == Keys.Right || keyData == Keys.Down || keyData == Keys.Left || keyData == Keys.Up))
            { MoveImage(keyData == Keys.Right || keyData == Keys.Down ? 1 : -1); return true; }
            return base.ProcessCmdKey(ref message, keyData);
        }

        private void UpdateNavigation()
        {
            _previousButton.Enabled = ImageIndex > 0;
            _nextButton.Enabled = ImageIndex + 1 < _imageCount;
            _positionLabel.Text = (ImageIndex + 1) + " / " + _imageCount;
        }

        private void DisposeOwnedImage()
        {
            if (!_ownsBitmap) return;
            _ownsBitmap = false;
            pictureBox1.Image = null;
            _bmp.Dispose();
        }

        private void MouseWheelHandler(object sender, MouseEventArgs e)
        {
            Zoom(e.Delta / 50.0);
        }

        private void Zoom(double delta)
        {
            double newZoomFactor = _zoomFactor += delta;
            if (newZoomFactor < 25)
            {
                _zoomFactor = 25;
            }
            else if (newZoomFactor > 500)
            {
                _zoomFactor = 500;
            }
            else
            {
                _zoomFactor = newZoomFactor;
            }

            if (_zoomFactor > 99 && _zoomFactor < 101)
            {
                pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
                _zoomFactor = 100.0;
            }
            else
            {
                pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            }

            pictureBox1.Width = (int)(_bmp.Width * _zoomFactor / 100.0);
            pictureBox1.Height = (int)(_bmp.Height * _zoomFactor / 100.0);

            Text = $"{LanguageSettings.Current.General.Preview}  {_bmp.Width}x{_bmp.Height}  {(int)_zoomFactor}%";

            Invalidate();
        }

        private void ExportPngXmlPreview_Shown(object sender, System.EventArgs e)
        {
            panel1.ScrollControlIntoView(pictureBox2);
            pictureBox2.Visible = false;
        }

        private void ExportPngXmlPreview_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                DialogResult = DialogResult.OK;
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.P)
            {
                e.SuppressKeyPress = true;
                DialogResult = DialogResult.OK;
            }
            else if (e.KeyCode == Keys.Add)
            {
                e.SuppressKeyPress = true;
                Zoom(10);
            }
            else if (e.KeyCode == Keys.Subtract)
            {
                e.SuppressKeyPress = true;
                Zoom(-10);
            }
            else if (e.KeyCode == Keys.Home || e.Control && (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0))
            {
                e.SuppressKeyPress = true;
                _zoomFactor = 100;
                Zoom(0);
            }
            else if (_imageProvider != null && (e.KeyCode == Keys.Right || e.KeyCode == Keys.Down))
            { e.SuppressKeyPress = true; MoveImage(1); }
            else if (_imageProvider != null && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up))
            { e.SuppressKeyPress = true; MoveImage(-1); }
            else if ((e.KeyCode == Keys.Right || e.KeyCode == Keys.Down) && AllowNext)
            {
                NextPressed = true;
                DialogResult = DialogResult.OK;
            }
            else if ((e.KeyCode == Keys.Left || e.KeyCode == Keys.Up) && AllowPrevious)
            {
                PreviousPressed = true;
                DialogResult = DialogResult.OK;
            }
        }
    }
}
