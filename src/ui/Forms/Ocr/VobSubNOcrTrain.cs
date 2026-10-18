using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Ocr;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MessageBox = Nikse.SubtitleEdit.Forms.SeMsgBox.MessageBox;

namespace Nikse.SubtitleEdit.Forms.Ocr
{
    public sealed partial class VobSubNOcrTrain : Form
    {
        private volatile bool _abort;
        private bool _training;
        private ComboBox _lineAlgorithm;
        private TextBox _trainingCharacters;

        public VobSubNOcrTrain()
        {
            UiUtil.PreInitialize(this);
            InitializeComponent();
            UiUtil.FixFonts(this);

            var ru=(Configuration.Settings.General.Language ?? "").StartsWith("ru",StringComparison.OrdinalIgnoreCase);
            checkBoxBold.Text=ru?"Обучать жирный":"Also train bold";checkBoxItalic.Text=ru?"Обучать курсив":"Also train italic";
            var extra=new TableLayoutPanel{Location=new Point(12,buttonTrain.Top),Width=ClientSize.Width-24,Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,Height=104,ColumnCount=2,RowCount=2,Padding=new Padding(8)};
            extra.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));extra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            extra.RowStyles.Add(new RowStyle(SizeType.Absolute,32));extra.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            _lineAlgorithm=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};_lineAlgorithm.Items.AddRange(Enum.GetNames(typeof(NOcrLineAlgorithm)));_lineAlgorithm.SelectedIndex=(int)ClassicNOcrPreferences.LoadAlgorithm();_lineAlgorithm.SelectedIndexChanged+=(s,e)=>ClassicNOcrPreferences.SaveAlgorithm((NOcrLineAlgorithm)_lineAlgorithm.SelectedIndex);
            _trainingCharacters=new TextBox{Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Vertical,Text=NOcrTrainer.DefaultTrainingCharacters+"абвгдеёжзийклмнопрстуфхцчшщъыьэюяАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ"};
            extra.Controls.Add(new Label{Text=ru?"Алгоритм линий":"Line algorithm",AutoSize=true},0,0);extra.Controls.Add(_lineAlgorithm,1,0);
            extra.Controls.Add(new Label{Text=ru?"Символы без SRT":"Characters without SRT",AutoSize=true},0,1);extra.Controls.Add(_trainingCharacters,1,1);
            ClientSize=new Size(ClientSize.Width,ClientSize.Height+114);MinimumSize=new Size(MinimumSize.Width,MinimumSize.Height+114);
            groupBoxTrainingOptions.Height-=114;Controls.Add(extra);extra.BringToFront();
            var language = LanguageSettings.Current.VobSubOcr;
            Text = language.OcrTraining;
            groupBoxInput.Text = LanguageSettings.Current.BatchConvert.Input;
            labelSubtitleForTraining.Text = language.SubtitleTrainingFile;
            labelLetterCombi.Text = language.LetterCombinations;
            groupBoxTrainingOptions.Text = language.TrainingOptions;
            labelSubtitleFont.Text = LanguageSettings.Current.Settings.SubtitleFont;
            labelSubtitleFontSize.Text = LanguageSettings.Current.Settings.SubtitleFontSize;
            buttonTrain.Text = language.StartTraining;
            labelLineSegments.Text = language.NumberOfSegments;
            comboBoxSubtitleFontSize.Left = labelSubtitleFontSize.Left + labelSubtitleFontSize.Width + 5;
            numericUpDownSegmentsPerCharacter.Left = labelLineSegments.Left + labelLineSegments.Width + 5;

            labelInfo.Text = string.Empty;
            textBoxMerged.Text = Configuration.Settings.Tools.OcrTrainMergedLetters;
            var selectedFonts = Configuration.Settings.Tools.OcrTrainFonts.Split(';');
            foreach (var fontFamily in FontHelper.GetRegularAndBoldCapableFontFamilies())
            {
                var chk = selectedFonts.Contains(fontFamily.Name);
                ListViewItem item = new ListViewItem(fontFamily.Name) { Font = new Font(fontFamily.Name, 14), Checked = chk };
                listViewFonts.Items.Add(item);
            }
            comboBoxSubtitleFontSize.SelectedIndex = 50;
            textBoxInputFile.Text = Configuration.Settings.Tools.OcrTrainSrtFile;
            InitializeClassicTraining();
        }

        internal void Initialize()
        {
        }

        private void buttonInputChoose_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                textBoxInputFile.Text = openFileDialog1.FileName;
            }
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private async void buttonTrain_Click(object sender, EventArgs e)
        {
            if (_training) { _abort = true; return; }
            try
            {
                var sizes = ParseFontSizes(comboBoxSubtitleFontSize.Text);
                var chars = _trainingCharacters.Text;
                if (File.Exists(textBoxInputFile.Text))
                {
                    var sub = new Subtitle();
                    sub.LoadSubtitle(textBoxInputFile.Text, out _, null);
                    chars = string.Concat(sub.Paragraphs.Select(p => HtmlUtil.RemoveHtmlTags(p.Text)));
                }
                if (string.IsNullOrWhiteSpace(chars) || listViewFonts.CheckedItems.Count == 0)
                    throw new InvalidOperationException(_russian ? "Выберите шрифты и символы для обучения." : "Select fonts and characters to train.");
                var basePath = _baseDatabase.Text.Trim();
                if (basePath.Length > 0 && !File.Exists(basePath)) throw new FileNotFoundException("nOCR database not found", basePath);
                var settings = new NOcrTrainerSettings
                {
                    FontNames = listViewFonts.CheckedItems.Cast<ListViewItem>().Select(x => x.Text).ToList(),
                    IncludeBold = checkBoxBold.Checked, IncludeItalic = checkBoxItalic.Checked,
                    NumberOfLineSegments = (int)numericUpDownSegmentsPerCharacter.Value,
                    CharactersToTrain = chars, MergedLetterCombinations = textBoxMerged.Text,
                    LineSegmentAlgorithm = (NOcrLineAlgorithm)_lineAlgorithm.SelectedIndex
                };
                _training = true; _abort = false;
                _trainingBody.Enabled = false; buttonOK.Enabled = false;
                buttonTrain.Text = LanguageSettings.Current.SpellCheck.Abort;
                var progress = new Progress<string>(text => { if (!IsDisposed) labelInfo.Text = text; });
                var db = await System.Threading.Tasks.Task.Run(() => TrainSizes(settings, sizes, basePath, () => _abort, progress));
                if (_closeAfterTraining || IsDisposed) return;
                labelInfo.Text = _abort ? (_russian ? "Остановлено. Можно сохранить изученные символы." : "Stopped. Learned characters can be saved.") : (_russian ? "Обучение завершено" : "Training complete");
                if (db.TotalCharacterCount == 0) return;
                saveFileDialog1.Filter = "nOCR DB|*.nocr";
                saveFileDialog1.InitialDirectory = Configuration.OcrDirectory;
                saveFileDialog1.FileName = basePath.Length > 0 ? Path.GetFileName(basePath) : "";
                if (saveFileDialog1.ShowDialog(this) == DialogResult.OK)
                {
                    db.FileName = saveFileDialog1.FileName; db.Save();
                    labelInfo.Text = (_russian ? "Сохранено: " : "Saved: ") + db.FileName;
                }
            }
            catch (Exception ex) { if (!_closeAfterTraining && !IsDisposed) MessageBox.Show(this, ex.Message); }
            finally
            {
                _training = false; _abort = false;
                if (!IsDisposed)
                {
                    _trainingBody.Enabled = true; buttonOK.Enabled = true;
                    buttonTrain.Text = LanguageSettings.Current.VobSubOcr.StartTraining;
                    if (_closeAfterTraining) Close();
                }
            }
        }

        private void VobSubNOcrTrain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(_training){_closeAfterTraining=true;_abort=true;e.Cancel=true;return;}
            SaveClassicTrainingPreferences();
            var sb = new StringBuilder();
            foreach (ListViewItem item in listViewFonts.Items)
            {
                if (item.Checked)
                {
                    sb.Append(item.Text);
                    sb.Append(";");
                }
            }

            Configuration.Settings.Tools.OcrTrainFonts = sb.ToString().Trim(';');
            Configuration.Settings.Tools.OcrTrainSrtFile = textBoxInputFile.Text;
            Configuration.Settings.Tools.OcrTrainMergedLetters = textBoxMerged.Text;
        }

        private void VobSubNOcrTrain_ResizeEnd(object sender, EventArgs e)
        {
            listViewFonts.AutoSizeLastColumn();
        }

        private void VobSubNOcrTrain_Shown(object sender, EventArgs e)
        {
            VobSubNOcrTrain_ResizeEnd(sender, e);
        }
    }
}
