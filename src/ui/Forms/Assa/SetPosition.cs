using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms.Options;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.VideoPlayers;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Logic.ClassicDraw;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MessageBox = Nikse.SubtitleEdit.Forms.SeMsgBox.MessageBox;

namespace Nikse.SubtitleEdit.Forms.Assa
{
    public sealed partial class SetPosition : Form
    {
        public Subtitle UpdatedSubtitle { get; private set; }
        private readonly Subtitle _subtitle;
        private readonly Subtitle _subtitleWithNewHeader;
        private readonly int[] _selectedIndices;
        private Bitmap _pausedFrame;
        private SetPositionPreview _preview;
        private CancellationTokenSource _previewCancellation;
        private int _previewRequest;
        private readonly CheckBox _selectionBox = new CheckBox { AutoSize = true, Checked = true };
        private readonly ComboBox _alignment = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        private bool _alignmentChanged;
        private bool _closing;
        private int _x = -1;
        private int _y = -1;
        private int _tempX;
        private int _tempY;
        private bool _updatePos;
        private readonly string _videoFileName;
        private readonly VideoInfo _videoInfo;
        private bool _positionChanged;
        private bool _loading = true;
        private int _originalPlayResX;
        private int _originalPlayResY;
        private bool _playResScaleOn;
        private int _tempXScaled;
        private int _tempYScaled;
        private int _xScaled = -1;
        private int _yScaled = -1;
        private readonly double _currentPositionSeconds;

        public SetPosition(Subtitle subtitle, int[] selectedIndices, string videoFileName, VideoInfo videoInfo, double currentPositionSeconds, Bitmap pausedFrame = null)
        {
            UiUtil.PreInitialize(this);
            InitializeComponent();
            UiUtil.FixFonts(this);

            _pausedFrame = pausedFrame;
            _subtitle = subtitle;
            _videoFileName = videoFileName;
            _videoInfo = videoInfo;
            _currentPositionSeconds = currentPositionSeconds;

            _subtitleWithNewHeader = new Subtitle(_subtitle, false);
            if (_subtitleWithNewHeader.Header == null)
            {
                _subtitleWithNewHeader.Header = AdvancedSubStationAlpha.DefaultHeader;
            }

            _selectedIndices = selectedIndices;
            radioButtonSelectedLines.Checked = true;
            Text = LanguageSettings.Current.AssaSetPosition.SetPosition;
            radioButtonSelectedLines.Text = string.Format(LanguageSettings.Current.AssaOverrideTags.SelectedLinesX, _selectedIndices.Length);
            radioButtonClipboard.Text = LanguageSettings.Current.AssaSetPosition.Clipboard;
            labelInfo.Text = LanguageSettings.Current.AssaSetPosition.SetPosInfo;
            groupBoxPreview.Text = LanguageSettings.Current.General.Preview;
            groupBoxApplyTo.Text = LanguageSettings.Current.General.ApplyTo;
            labelCurrentPosition.Text = string.Format(LanguageSettings.Current.AssaSetPosition.CurrentMousePositionX, string.Empty);
            labelCurrentTextPosition.Text = string.Format(LanguageSettings.Current.AssaSetPosition.CurrentTextPositionX, string.Empty);
            labelRotateX.Text = string.Format(LanguageSettings.Current.AssaSetPosition.RotateXAxis, "X");
            labelRotateY.Text = string.Format(LanguageSettings.Current.AssaSetPosition.RotateXAxis, "Y");
            labelRotateZ.Text = string.Format(LanguageSettings.Current.AssaSetPosition.RotateXAxis, "Z");
            labelDistortX.Text = string.Format(LanguageSettings.Current.AssaSetPosition.DistortX, "X");
            labelDistortY.Text = string.Format(LanguageSettings.Current.AssaSetPosition.DistortX, "Y");
            buttonOK.Text = LanguageSettings.Current.General.Ok;
            buttonCancel.Text = LanguageSettings.Current.General.Cancel;

            numericUpDownRotateX.Left = labelRotateX.Left + labelRotateX.Width + 3;
            numericUpDownRotateY.Left = labelRotateY.Left + labelRotateY.Width + 3;
            numericUpDownRotateZ.Left = labelRotateZ.Left + labelRotateZ.Width + 3;
            numericUpDownDistortX.Left = labelDistortX.Left + labelDistortX.Width + 3;
            numericUpDownDistortY.Left = labelDistortY.Left + labelDistortY.Width + 3;

            UiUtil.FixLargeFonts(this, buttonOK);

            if (_videoInfo == null)
            {
                _videoInfo = UiUtil.GetVideoInfo(_videoFileName);
            }

            if (_videoInfo.Width <= 0 || _videoInfo.Height <= 0) { _videoInfo.Width = 1280; _videoInfo.Height = 720; }
            labelVideoResolution.Text = string.Format(LanguageSettings.Current.AssaSetPosition.VideoResolutionX, $"{_videoInfo.Width}x{_videoInfo.Height}");

            MinimumSize = new Size(760, 520);
            pictureBoxPreview.SizeMode = PictureBoxSizeMode.Zoom;
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, WrapContents = false };
            _selectionBox.Text = ClassicAssistedText.L("Рамка выделения", "Selection box");
            _selectionBox.CheckedChanged += (sender, e) => pictureBoxPreview.Invalidate();
            var alignmentNames = new[] { "↙ 1", "↓ 2", "↘ 3", "← 4", "• 5", "→ 6", "↖ 7", "↑ 8", "↗ 9" };
            _alignment.Items.AddRange(alignmentNames);
            _alignment.SelectedIndex = 1;
            _alignment.SelectedIndexChanged += (sender, e) => { if (_loading) return; _alignmentChanged = _positionChanged = true; ShowStyleAlignment(); VideoLoaded(null, null); };
            toolbar.Controls.Add(_selectionBox);
            toolbar.Controls.Add(new Label { Text = ClassicAssistedText.L("Привязка:", "Anchor:"), AutoSize = true, Padding = new Padding(10, 5, 0, 0) });
            toolbar.Controls.Add(_alignment);
            groupBoxPreview.Controls.Add(toolbar);
            toolbar.BringToFront();
            pictureBoxPreview.Paint += PaintSelection;
            pictureBoxPreview.MouseMove += (sender, e) => { if (UpdateMouseCoordinates(e.Location) && (e.Button & MouseButtons.Left) != 0) { SetMousePosition(); VideoLoaded(null, null); } };
            pictureBoxPreview.MouseDown += (sender, e) => { if (e.Button == MouseButtons.Left && UpdateMouseCoordinates(e.Location)) { SetMousePosition(); pictureBoxPreview.Capture = true; VideoLoaded(null, null); } };
            pictureBoxPreview.MouseUp += (sender, e) => pictureBoxPreview.Capture = false;
            labelInfo.Text = ClassicAssistedText.L("Щёлкните или перетащите текст. Стрелки: 1 px, Shift+стрелки: 10 px. F2: эффекты.", "Click or drag to place. Arrows: 1 px, Shift+arrows: 10 px. F2: effects.");
            Load += (sender, e) => ClassicWindowPlacement.Restore(this, "AssaSetPosition");
            FormClosed += (sender, e) => ClassicWindowPlacement.Save(this, "AssaSetPosition");

            if (Configuration.Settings.Tools.AssaSetPositionTarget == "Clipboard")
            {
                radioButtonClipboard.Checked = true;
            }
            else
            {
                radioButtonSelectedLines.Checked = true;
            }

            _originalPlayResX = 384;
            _originalPlayResY = 288;
            if (_subtitle?.Header != null)
            {
                var playResX = AdvancedSubStationAlpha.GetTagFromHeader("PlayResX", "[Script Info]", _subtitle.Header);
                var playResY = AdvancedSubStationAlpha.GetTagFromHeader("PlayResY", "[Script Info]", _subtitle.Header);
                if (!string.IsNullOrEmpty(playResX) && !string.IsNullOrEmpty(playResY))
                {
                    playResX = playResX.Trim().Remove(0, 8).TrimStart().TrimStart(':').TrimStart();
                    playResY = playResY.Trim().Remove(0, 8).TrimStart().TrimStart(':').TrimStart();
                    if (int.TryParse(playResX, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var px) &&
                        int.TryParse(playResY, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var py))
                    {
                        _originalPlayResX = px;
                        _originalPlayResY = py;
                    }
                }
            }
        }

        private void ApplyCustomStyles_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                buttonOK_Click(null, null);
            }
            else if (e.KeyData == UiUtil.HelpKeys)
            {
                UiUtil.OpenUrl("https://www.nikse.dk/SubtitleEdit/AssaOverrideTags#pos");
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == Keys.F2)
            {
                panelAdvanced.Visible = !panelAdvanced.Visible;
                VideoLoaded(null, null);
            }

            if (e.Modifiers == Keys.None || e.Modifiers == Keys.Shift || e.Modifiers == Keys.Alt || e.Modifiers == Keys.Control)
            {
                var v = e.Modifiers == Keys.Shift || e.Modifiers == Keys.Control ? 10 : 1;

                if (_playResScaleOn)
                {
                    _tempXScaled = _xScaled; _tempYScaled = _yScaled;
                    if (e.KeyCode == Keys.Up && _tempXScaled != -1 && _tempYScaled != -1)
                    {
                        _tempYScaled -= v;
                        AcceptKeyAndShowInfo(e);
                    }
                    else if (e.KeyCode == Keys.Down && _tempXScaled != -1 && _tempYScaled != -1)
                    {
                        _tempYScaled += v;
                        AcceptKeyAndShowInfo(e);
                    }
                    else if (e.KeyCode == Keys.Left && _tempXScaled != -1 && _tempYScaled != -1)
                    {
                        _tempXScaled -= v;
                        AcceptKeyAndShowInfo(e);
                    }
                    else if (e.KeyCode == Keys.Right && _tempXScaled != -1 && _tempYScaled != -1)
                    {
                        _tempXScaled += v;
                        AcceptKeyAndShowInfo(e);
                    }
                }
                else
                {
                    if (e.KeyCode == Keys.Up && _x != -1 && _y != -1)
                    {
                        _y -= v;
                        AcceptKeyAndShowInfo(e);
                    }
                    else if (e.KeyCode == Keys.Down && _x != -1 && _y != -1)
                    {
                        _y += v;
                        AcceptKeyAndShowInfo(e);
                    }
                    else if (e.KeyCode == Keys.Left && _x != -1 && _y != -1)
                    {
                        _x -= v;
                        AcceptKeyAndShowInfo(e);
                    }
                    else if (e.KeyCode == Keys.Right && _x != -1 && _y != -1)
                    {
                        _x += v;
                        AcceptKeyAndShowInfo(e);
                    }
                }
            }
        }

        private void AcceptKeyAndShowInfo(KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            _positionChanged = true;
            _updatePos = false;
            if (_playResScaleOn) { _x = AssaResampler.Resample(_originalPlayResX, _videoInfo.Width, _tempXScaled); _y = AssaResampler.Resample(_originalPlayResY, _videoInfo.Height, _tempYScaled); }
            else { _tempXScaled = _x; _tempYScaled = _y; }
            _xScaled = _tempXScaled; _yScaled = _tempYScaled;
            VideoLoaded(null, null);
            if (_playResScaleOn)
            {
                labelCurrentTextPosition.Text = string.Format(LanguageSettings.Current.AssaSetPosition.CurrentTextPositionX, $"{_x},{_y}  ({_tempXScaled},{_tempYScaled})");
            }
            else
            {
                labelCurrentTextPosition.Text = string.Format(LanguageSettings.Current.AssaSetPosition.CurrentTextPositionX, $"{_x},{_y}");
            }
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            _subtitle.Header = _subtitleWithNewHeader.Header;
            if (_positionChanged)
            {
                ApplyOverrideTags(_subtitle);
            }

            DialogResult = DialogResult.OK;
        }

        private void ApplyOverrideTags(Subtitle subtitle)
        {
            var styleToApply = $"{{\\pos({_x},{_y})}}";
            if (_playResScaleOn)
            {
                styleToApply = $"{{\\pos({_xScaled},{_yScaled})}}";
            }

            if (_alignmentChanged) styleToApply = "{\\an" + (_alignment.SelectedIndex + 1) + "}" + styleToApply;
            var advancedVisible = panelAdvanced.Visible;
            if (advancedVisible)
            {
                styleToApply = AddAdvancedTags(styleToApply);
            }


            if (radioButtonClipboard.Checked)
            {
                Clipboard.SetText(styleToApply);
                return;
            }

            UpdatedSubtitle = new Subtitle(subtitle, false);
            var indices = GetIndices();

            for (var i = 0; i < UpdatedSubtitle.Paragraphs.Count; i++)
            {
                if (!indices.Contains(i))
                {
                    continue;
                }

                var p = UpdatedSubtitle.Paragraphs[i];

                RemoveOldPosTags(p);
                if (_alignmentChanged) { p.Text = Regex.Replace(p.Text, @"\\an[1-9]", string.Empty); }

                if (advancedVisible)
                {
                    RemoveAdvancedTags(p);
                }

                if (p.Text.StartsWith("{\\", StringComparison.Ordinal) && styleToApply.EndsWith("}", StringComparison.Ordinal))
                {
                    p.Text = styleToApply.TrimEnd('}') + p.Text.Remove(0, 1);
                }
                else
                {
                    p.Text = styleToApply + p.Text;
                }
            }
        }

        private static void RemoveOldPosTags(Paragraph p)
        {
            p.Text = Regex.Replace(p.Text, @"{\\pos\([\d,\.-]*\)}", string.Empty);
            p.Text = Regex.Replace(p.Text, @"\\pos\([\d,\.-]*\)", string.Empty);
        }

        private int[] GetIndices()
        {
            return _selectedIndices;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private async void VideoLoaded(object sender, EventArgs e)
        {
            if (_preview == null || _closing) return;
            _previewCancellation?.Cancel();
            var cancel = new CancellationTokenSource();
            _previewCancellation = cancel;
            var request = ++_previewRequest;
            try
            {
                await Task.Delay(75, cancel.Token);
                var subtitle = new Subtitle(_subtitleWithNewHeader, false);
                subtitle.Paragraphs.Clear();
                var p = _selectedIndices.Length > 0 ? new Paragraph(_subtitleWithNewHeader.Paragraphs[_selectedIndices[0]]) : new Paragraph(Configuration.Settings.General.PreviewAssaText, 0, 2000);
                RemoveOldPosTags(p);
                p.Text = Regex.Replace(p.Text, @"\\fad(?:e)?\([^)]*\)", string.Empty);
                var tags = _playResScaleOn ? $"{{\\pos({_xScaled},{_yScaled})}}" : $"{{\\pos({_x},{_y})}}";
                if (_alignmentChanged) { p.Text = Regex.Replace(p.Text, @"\\an[1-9]", string.Empty); tags += "{\\an" + (_alignment.SelectedIndex + 1) + "}"; }
                if (panelAdvanced.Visible) { RemoveAdvancedTags(p); tags = AddAdvancedTags(tags); }
                p.Text = tags + p.Text;
                p.StartTime.TotalMilliseconds = 0;
                p.EndTime.TotalMilliseconds = 2000;
                subtitle.Paragraphs.Add(p);
                var image = await _preview.Render(subtitle, cancel.Token);
                if (_closing || request != _previewRequest) { image.Dispose(); return; }
                var old = pictureBoxPreview.Image;
                pictureBoxPreview.Image = image;
                old?.Dispose();
                pictureBoxPreview.Invalidate();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception || ex is System.Runtime.InteropServices.ExternalException || ex is UnauthorizedAccessException)
            {
                if (!_closing && request == _previewRequest) { labelInfo.Text = ex.Message; SeLogger.Error(ex, "ASS set position preview"); }
            }
            finally { if (ReferenceEquals(_previewCancellation, cancel)) _previewCancellation = null; cancel.Dispose(); }
        }

        private RectangleF VideoRectangle()
        {
            var scale = Math.Min(pictureBoxPreview.ClientSize.Width / (float)_videoInfo.Width, pictureBoxPreview.ClientSize.Height / (float)_videoInfo.Height);
            var width = _videoInfo.Width * scale;
            var height = _videoInfo.Height * scale;
            return new RectangleF((pictureBoxPreview.Width - width) / 2, (pictureBoxPreview.Height - height) / 2, width, height);
        }

        private void PaintSelection(object sender, PaintEventArgs e)
        {
            if (!_selectionBox.Checked || _x < 0 || _y < 0) return;
            var rect = VideoRectangle();
            var scale = rect.Width / _videoInfo.Width;
            var x = rect.X + _x * scale;
            var y = rect.Y + _y * scale;
            using (var pen = new Pen(Color.DeepSkyBlue, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
            {
                e.Graphics.DrawRectangle(pen, x - 12, y - 12, 24, 24);
                e.Graphics.DrawLine(pen, x - 20, y, x + 20, y);
                e.Graphics.DrawLine(pen, x, y - 20, x, y + 20);
            }
        }

        private void SetMousePosition()
        {
            _x = _tempX; _y = _tempY;
            _xScaled = _tempXScaled; _yScaled = _tempYScaled;
            _positionChanged = true; _updatePos = false;
            labelCurrentTextPosition.Text = string.Format(LanguageSettings.Current.AssaSetPosition.CurrentTextPositionX, _playResScaleOn ? $"{_x},{_y}  ({_tempXScaled},{_tempYScaled})" : $"{_x},{_y}");
        }

        private string AddAdvancedTags(string styleToApply)
        {
            if (numericUpDownRotateX.Value != 0)
            {
                styleToApply += $"{{\\frx{numericUpDownRotateX.Value.ToString(CultureInfo.InvariantCulture)}}}";
            }

            if (numericUpDownRotateY.Value != 0)
            {
                styleToApply += $"{{\\fry{numericUpDownRotateY.Value.ToString(CultureInfo.InvariantCulture)}}}";
            }

            if (numericUpDownRotateZ.Value != 0)
            {
                styleToApply += $"{{\\frz{numericUpDownRotateZ.Value.ToString(CultureInfo.InvariantCulture)}}}";
            }

            if (numericUpDownDistortX.Value != 0)
            {
                styleToApply += $"{{\\fax{numericUpDownDistortX.Value.ToString(CultureInfo.InvariantCulture)}}}";
            }

            if (numericUpDownDistortY.Value != 0)
            {
                styleToApply += $"{{\\fay{numericUpDownDistortY.Value.ToString(CultureInfo.InvariantCulture)}}}";
            }

            styleToApply = styleToApply.Replace("}{", string.Empty);

            return styleToApply;
        }

        private static void RemoveAdvancedTags(Paragraph p)
        {
            p.Text = Regex.Replace(p.Text, @"{\\frx[\d+\.-]*}", string.Empty);
            p.Text = Regex.Replace(p.Text, @"\\frx[\d+\.-]*\\", "\\");
            p.Text = Regex.Replace(p.Text, @"\\frx[\d+\.-]*}", "}");

            p.Text = Regex.Replace(p.Text, @"{\\fry[\d+\.-]*}", string.Empty);
            p.Text = Regex.Replace(p.Text, @"\\fry[\d+\.-]*\\", "\\");
            p.Text = Regex.Replace(p.Text, @"\\fry[\d+\.-]*}", "}");

            p.Text = Regex.Replace(p.Text, @"{\\frz[\d+\.-]*}", string.Empty);
            p.Text = Regex.Replace(p.Text, @"\\frz[\d+\.-]*\\", "\\");
            p.Text = Regex.Replace(p.Text, @"\\frz[\d+\.-]*}", "}");

            p.Text = Regex.Replace(p.Text, @"{\\fax[\d+\.-]*}", string.Empty);
            p.Text = Regex.Replace(p.Text, @"\\fax[\d+\.-]*\\", "\\");
            p.Text = Regex.Replace(p.Text, @"\\fax[\d+\.-]*}", "}");

            p.Text = Regex.Replace(p.Text, @"{\\fay[\d+\.-]*}", string.Empty);
            p.Text = Regex.Replace(p.Text, @"\\fay[\d+\.-]*\\", "\\");
            p.Text = Regex.Replace(p.Text, @"\\fay[\d+\.-]*}", "}");
        }

        private void ApplyCustomStyles_FormClosing(object sender, FormClosingEventArgs e)
        {
            _closing = true;
            Configuration.Settings.Tools.AssaSetPositionTarget = radioButtonClipboard.Checked ? "Clipboard" : "SelectedLines";
            ReleasePreview();
        }

        private void ReleasePreview()
        {
            _closing = true;
            timer1.Stop();
            _previewCancellation?.Cancel();
            _preview?.Dispose();
            _preview = null;
            _pausedFrame?.Dispose();
            _pausedFrame = null;
            pictureBoxPreview.Image?.Dispose();
            pictureBoxPreview.Image = null;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (_closing)
            {
                return;
            }

            CheckImageMouseMove();

            if (_updatePos && (_x != _tempX || _y != _tempY))
            {
                _x = _tempX;
                _y = _tempY;
                _xScaled = _tempXScaled;
                _yScaled = _tempYScaled;
                VideoLoaded(null, null);
            }
        }

        private void CheckImageMouseMove()
        {
            UpdateMouseCoordinates(pictureBoxPreview.PointToClient(Cursor.Position));
        }

        private bool UpdateMouseCoordinates(Point pos)
        {
            var rect = VideoRectangle();
            if (!rect.Contains(pos)) return false;
            _tempX = (int)Math.Round((pos.X - rect.Left) * _videoInfo.Width / rect.Width);
            _tempY = (int)Math.Round((pos.Y - rect.Top) * _videoInfo.Height / rect.Height);

            if (_playResScaleOn)
            {
                _tempXScaled = AssaResampler.Resample(_videoInfo.Width, _originalPlayResX, _tempX);
                _tempYScaled = AssaResampler.Resample(_videoInfo.Height, _originalPlayResY, _tempY);
                labelCurrentPosition.Text = string.Format(LanguageSettings.Current.AssaSetPosition.CurrentMousePositionX, $"{_tempX},{_tempY}  ({_tempXScaled},{_tempYScaled})");
            }
            else
            {
                labelCurrentPosition.Text = string.Format(LanguageSettings.Current.AssaSetPosition.CurrentMousePositionX, $"{_tempX},{_tempY}");
            }
            return true;
        }

        private void SetPosition_Shown(object sender, EventArgs e)
        {
            _playResScaleOn = _originalPlayResX != _videoInfo.Width || _originalPlayResY != _videoInfo.Height;
            ShowStyleAlignment();
            ShowCurrentPosition();

            var playResX = AdvancedSubStationAlpha.GetTagFromHeader("PlayResX", "[Script Info]", _subtitleWithNewHeader.Header);
            var playResY = AdvancedSubStationAlpha.GetTagFromHeader("PlayResY", "[Script Info]", _subtitleWithNewHeader.Header);
            if (string.IsNullOrEmpty(playResX) || string.IsNullOrEmpty(playResY))
            {
                var dialogResult = MessageBox.Show(LanguageSettings.Current.AssaSetPosition.ResolutionMissing, "Subtitle Edit", MessageBoxButtons.YesNoCancel);
                if (dialogResult == DialogResult.OK || dialogResult == DialogResult.Yes)
                {
                    if (string.IsNullOrEmpty(_subtitleWithNewHeader.Header))
                    {
                        _subtitleWithNewHeader.Header = AdvancedSubStationAlpha.DefaultHeader;
                    }

                    _subtitleWithNewHeader.Header = AdvancedSubStationAlpha.AddTagToHeader("PlayResX", "PlayResX: " + _videoInfo.Width.ToString(CultureInfo.InvariantCulture), "[Script Info]", _subtitleWithNewHeader.Header);
                    _subtitleWithNewHeader.Header = AdvancedSubStationAlpha.AddTagToHeader("PlayResY", "PlayResY: " + _videoInfo.Height.ToString(CultureInfo.InvariantCulture), "[Script Info]", _subtitleWithNewHeader.Header);

                    _originalPlayResX = _videoInfo.Width;
                    _originalPlayResY = _videoInfo.Height;
                }
            }

            _playResScaleOn = _originalPlayResX != _videoInfo.Width || _originalPlayResY != _videoInfo.Height;
            if (_x == -1 || _y == -1)
            {
                var p = _subtitleWithNewHeader.GetParagraphOrDefault(_selectedIndices.FirstOrDefault());
                var style = AdvancedSubStationAlpha.GetSsaStyle(p?.Extra ?? "Default", _subtitleWithNewHeader.Header);
                var anchor = _alignment.SelectedIndex;
                _x = anchor % 3 == 0 ? style.MarginLeft : anchor % 3 == 2 ? _videoInfo.Width - style.MarginRight : _videoInfo.Width / 2;
                _y = anchor < 3 ? _videoInfo.Height - style.MarginVertical : anchor < 6 ? _videoInfo.Height / 2 : style.MarginVertical;
                _tempXScaled = _xScaled = AssaResampler.Resample(_videoInfo.Width, _originalPlayResX, _x);
                _tempYScaled = _yScaled = AssaResampler.Resample(_videoInfo.Height, _originalPlayResY, _y);
            }
            _preview = new SetPositionPreview(_subtitleWithNewHeader, _videoFileName, _currentPositionSeconds, _videoInfo.Width, _videoInfo.Height, _pausedFrame);
            _pausedFrame = null;
            _loading = false;
            VideoLoaded(null, null);

            if (_playResScaleOn)
            {
                labelVideoResolution.Text = string.Format(LanguageSettings.Current.AssaSetPosition.VideoResolutionX, $"{_videoInfo.Width}x{_videoInfo.Height}  ({_originalPlayResX}x{_originalPlayResY})");
            }
        }

        private void ShowCurrentPosition()
        {
            var indices = GetIndices();
            if (indices.Length == 0)
            {
                return;
            }

            var p = _subtitleWithNewHeader.Paragraphs[indices[0]];
            var match = Regex.Match(p.Text, @"\\pos\([\d\.,-]*");
            if (match.Success)
            {
                var arr = match.Value.Split('(', ')', ',');
                if (arr.Length > 2)
                {
                    if (double.TryParse(arr[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var px) && double.TryParse(arr[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var py))
                    {
                        _x = (int)Math.Round(px);
                        _y = (int)Math.Round(py);
                        _tempXScaled = _x;
                        _tempYScaled = _y;
                        _xScaled = _x;
                        _yScaled = _y;
                        _updatePos = false;

                        if (_playResScaleOn)
                        {
                            _x = AssaResampler.Resample(_originalPlayResX, _videoInfo.Width, _x);
                            _y = AssaResampler.Resample(_originalPlayResY, _videoInfo.Height, _y);
                            labelCurrentTextPosition.Text = string.Format(LanguageSettings.Current.AssaSetPosition.CurrentTextPositionX, $"{_x},{_y}  ({_tempXScaled},{_tempYScaled})");
                        }
                        else
                        {
                            labelCurrentTextPosition.Text = string.Format(LanguageSettings.Current.AssaSetPosition.CurrentTextPositionX, $"{_x},{_y}");
                        }
                    }
                }
            }

            match = Regex.Match(p.Text, @"\\frx[\d\.-]*");
            if (match.Success)
            {
                var arr = match.Value.Split('x', '\\', '}');
                if (arr.Length > 2)
                {
                    if (decimal.TryParse(arr[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x))
                    {
                        try
                        {
                            numericUpDownRotateX.Value = x;
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
            }

            match = Regex.Match(p.Text, @"\\fry[\d\.-]*");
            if (match.Success)
            {
                var arr = match.Value.Split('y', '\\', '}');
                if (arr.Length > 2)
                {
                    if (decimal.TryParse(arr[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x))
                    {
                        try
                        {
                            numericUpDownRotateY.Value = x;
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
            }

            match = Regex.Match(p.Text, @"\\frz[\d\.-]*");
            if (match.Success)
            {
                var arr = match.Value.Split('z', '\\', '}');
                if (arr.Length > 2)
                {
                    if (decimal.TryParse(arr[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x))
                    {
                        try
                        {
                            numericUpDownRotateZ.Value = x;
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
            }

            match = Regex.Match(p.Text, @"\\fax[\d\.-]*");
            if (match.Success)
            {
                var arr = match.Value.Split('x', '\\', '}');
                if (arr.Length > 2)
                {
                    if (decimal.TryParse(arr[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x))
                    {
                        try
                        {
                            numericUpDownDistortX.Value = x;
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
            }

            match = Regex.Match(p.Text, @"\\fay[\d\.-]*");
            if (match.Success)
            {
                var arr = match.Value.Split('y', '\\', '}');
                if (arr.Length > 2)
                {
                    if (decimal.TryParse(arr[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x))
                    {
                        try
                        {
                            numericUpDownDistortY.Value = x;
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
            }
        }

        private void ShowStyleAlignment()
        {
            var indices = GetIndices();
            if (indices.Length == 0)
            {
                labelStyleAlignment.Text = string.Format(LanguageSettings.Current.AssaSetPosition.StyleAlignmentX, "{\\an2}");

                return;
            }

            var p = _subtitleWithNewHeader.Paragraphs[indices[0]];
            var style = AdvancedSubStationAlpha.GetSsaStyle(p.Extra, _subtitleWithNewHeader.Header);
            if (_loading)
            {
                var match = Regex.Match(p.Text, @"\\an([1-9])");
                var digit = match.Success ? match.Groups[1].Value : style.Alignment;
                if (int.TryParse(digit, out var anchor) && anchor >= 1 && anchor <= 9) _alignment.SelectedIndex = anchor - 1;
            }
            labelStyleAlignment.Text = string.Format(LanguageSettings.Current.AssaSetPosition.StyleAlignmentX, "{\\an" + (_alignment.SelectedIndex + 1) + "} (" + p.Extra + ")");
        }

        private void pictureBoxPreview_Click(object sender, EventArgs e) { }

        private void SetPosition_ResizeEnd(object sender, EventArgs e) => pictureBoxPreview.Invalidate();

        private void numericUpDownRotateX_ValueChanged(object sender, EventArgs e)
        {
            if (_loading || !panelAdvanced.Visible)
            {
                return;
            }

            _positionChanged = true;
            VideoLoaded(null, null);
        }
    }
}
