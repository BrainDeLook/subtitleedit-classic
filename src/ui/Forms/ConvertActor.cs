using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms
{
    public partial class ConvertActor : PositionAndSizeForm
    {
        public Subtitle Subtitle { get; set; }
        private SubtitleFormat _subtitleFormat;
        private bool _loading = true;
        private List<FixListItem> _fixItems;
        private ClassicActorOptions _options;
        private HashSet<int> _selectedIndices;
        private CheckBox _onlySelected;
        public ClassicActorOptions Options => ReadOptions();
        public void SetSelectedIndices(IEnumerable<int> indices) { _selectedIndices = new HashSet<int>(indices); }

        public class FixListItem
        {
            public Paragraph Paragraph { get; set; }
            public bool Checked { get; set; }
            public bool IsNext { get; set; }
            public Paragraph NextParagraph { get; set; }
        }

        public int NumberOfActorConversions { get; private set; }

        public ConvertActor()
        {
            UiUtil.PreInitialize(this);
            InitializeComponent();
            UiUtil.FixFonts(this);
            _fixItems = new List<FixListItem>();
            UiUtil.FixLargeFonts(this, buttonOK);
        }

        public void Initialize(Subtitle subtitle, SubtitleFormat subtitleFormat)
        {
            Subtitle = new Subtitle(subtitle, false);
            _subtitleFormat = subtitleFormat;

            Text = LanguageSettings.Current.ConvertActor.Title;

            listViewFixes.Columns[0].Text = LanguageSettings.Current.General.Apply;
            listViewFixes.Columns[1].Text = LanguageSettings.Current.General.LineNumber;
            listViewFixes.Columns[2].Text = LanguageSettings.Current.General.Before;
            listViewFixes.Columns[3].Text = LanguageSettings.Current.General.After;

            buttonOK.Text = LanguageSettings.Current.General.Ok;
            buttonCancel.Text = LanguageSettings.Current.General.Cancel;

            checkBoxColor.Text = LanguageSettings.Current.ConvertActor.SetColor;
            buttonColor.Text = LanguageSettings.Current.DCinemaProperties.FontColor;
            checkBoxChangeCasing.Text = LanguageSettings.Current.ChangeCasing.Title;
            checkBoxOnlyNames.Text = LanguageSettings.Current.ConvertActor.OnlyNames;

            checkBoxColor.Checked = Configuration.Settings.Tools.ConvertActorColorAdd;
            panelColor.BackColor = Configuration.Settings.Tools.ConvertActorColor;
            checkBoxChangeCasing.Checked = Configuration.Settings.Tools.ConvertActorCasing;

            labelConvertFrom.Text = LanguageSettings.Current.ConvertActor.ConvertActorFrom;
            labelConvertTo.Text = LanguageSettings.Current.ConvertActor.ConvertActorTo;
            var left = Math.Max(labelConvertFrom.Right, labelConvertTo.Right) + 5;
            nikseComboBoxConvertFrom.Left = left;
            nikseComboBoxConvertTo.Left = left;

            nikseComboBoxConvertFrom.Items.Clear();
            nikseComboBoxConvertFrom.Items.Add(string.Format(LanguageSettings.Current.ConvertActor.InlineActorViaX, "[]"));
            nikseComboBoxConvertFrom.Items.Add(string.Format(LanguageSettings.Current.ConvertActor.InlineActorViaX, "()"));
            nikseComboBoxConvertFrom.Items.Add(string.Format(LanguageSettings.Current.ConvertActor.InlineActorViaX, ":"));
            nikseComboBoxConvertFrom.Items.Add(LanguageSettings.Current.General.Actor);
            if (_subtitleFormat.Name == AdvancedSubStationAlpha.NameOfFormat)
            {
                nikseComboBoxConvertFrom.SelectedIndex = 3;
            }
            else
            {
                nikseComboBoxConvertFrom.SelectedIndex = 0;
            }


            nikseComboBoxConvertTo.Items.Clear();
            nikseComboBoxConvertTo.Items.Add(string.Format(LanguageSettings.Current.ConvertActor.InlineActorViaX, "[]"));
            nikseComboBoxConvertTo.Items.Add(string.Format(LanguageSettings.Current.ConvertActor.InlineActorViaX, "()"));
            nikseComboBoxConvertTo.Items.Add(string.Format(LanguageSettings.Current.ConvertActor.InlineActorViaX, ":"));
            nikseComboBoxConvertTo.Items.Add(LanguageSettings.Current.General.Actor);
            if (nikseComboBoxConvertFrom.SelectedIndex == 0)
            {
                nikseComboBoxConvertTo.SelectedIndex = 1;
            }
            else
            {
                nikseComboBoxConvertTo.SelectedIndex = 0;
            }

            nikseComboBoxCasing.Items.Clear();
            nikseComboBoxCasing.Items.Add(LanguageSettings.Current.ChangeCasing.NormalCasing);
            nikseComboBoxCasing.Items.Add(LanguageSettings.Current.ChangeCasing.AllUppercase);
            nikseComboBoxCasing.Items.Add(LanguageSettings.Current.ChangeCasing.AllLowercase);
            nikseComboBoxCasing.Items.Add(LanguageSettings.Current.ChangeCasing.ProperCase);
            nikseComboBoxCasing.SelectedIndex = 0;
            var settings = ClassicTextToolSettings.Load(); _options=settings.Actors;
            if (System.IO.File.Exists(ClassicTextToolSettings.SettingsPath))
            {
                nikseComboBoxConvertFrom.SelectedIndex=_options.From; nikseComboBoxConvertTo.SelectedIndex=_options.To;
                nikseComboBoxCasing.SelectedIndex=_options.Casing??0; checkBoxChangeCasing.Checked=_options.Casing.HasValue;
                checkBoxOnlyNames.Checked=_options.OnlyNames; checkBoxColor.Checked=_options.UseColor; panelColor.BackColor=Color.FromArgb(_options.ColorArgb);
            }
            if (_onlySelected==null) { _onlySelected=new CheckBox { AutoSize=true, Left=18, Top=checkBoxOnlyNames.Top, Text=ClassicTextTools.L("Только выбранные реплики", "Selected subtitles only"), Enabled=_selectedIndices!=null && _selectedIndices.Count>0 }; Controls.Add(_onlySelected); _onlySelected.CheckedChanged+=(s,e)=>GeneratePreview(); }

        }

        private ListViewItem MakeListViewItem(Paragraph p, bool selected, int lineNumber, string newText, string oldText, bool isNext)
        {
            var fixItem = new FixListItem { Checked = true, Paragraph = p, IsNext = isNext };
            _fixItems.Add(fixItem);

            var item = new ListViewItem(string.Empty) { Tag = p, Checked = selected || !checkBoxOnlyNames.Checked };
            item.SubItems.Add(lineNumber.ToString());
            item.SubItems.Add(UiUtil.GetListViewTextFromString(oldText));
            item.SubItems.Add(UiUtil.GetListViewTextFromString(newText));
            return item;
        }

        private void GeneratePreview()
        {
            if (Subtitle == null || _loading)
            {
                return;
            }

            ConvertActors(new Subtitle(Subtitle, false), out var numberOfConversions, true);
            NumberOfActorConversions = numberOfConversions;

            groupBoxLinesFound.Text = string.Format(LanguageSettings.Current.ConvertActor.NumberOfConversionsX, NumberOfActorConversions);
        }

        private ClassicActorOptions ReadOptions() => new ClassicActorOptions { From=nikseComboBoxConvertFrom.SelectedIndex, To=nikseComboBoxConvertTo.SelectedIndex, Casing=checkBoxChangeCasing.Checked?(int?)nikseComboBoxCasing.SelectedIndex:null, OnlyNames=checkBoxOnlyNames.Checked, UseColor=checkBoxColor.Checked, ColorArgb=panelColor.BackColor.ToArgb() };

        public Subtitle ConvertActors(Subtitle subtitle, out int numberOfConversions, bool clearFixes)
        {
            listViewFixes.ItemChecked -= listViewFixes_ItemChecked;
            try
            {
                listViewFixes.Items.Clear(); _fixItems.Clear(); var options=ReadOptions();
                var converter=ClassicTextTools.ActorConverter(subtitle,_subtitleFormat,options);
                for(int i=0;i<subtitle.Paragraphs.Count;i++)
                {
                    if (_onlySelected!=null && _onlySelected.Checked && !_selectedIndices.Contains(i)) continue;
                    var old=subtitle.Paragraphs[i]; var fix=ClassicTextTools.ConvertActor(converter,old,options);
                    if (fix==null || fix.Skip || (fix.Paragraph.Text==old.Text && fix.Paragraph.Actor==old.Actor && fix.NextParagraph==null)) continue;
                    var selected=!options.OnlyNames || fix.Selected;
                    _fixItems.Add(new FixListItem { Paragraph=fix.Paragraph, NextParagraph=fix.NextParagraph, Checked=selected });
                    // A split is one atomic conversion: either keep both speakers or keep the source cue.
                    var after=ActorPreview(fix.Paragraph)+(fix.NextParagraph==null?"":" | "+ActorPreview(fix.NextParagraph));
                    var item=new ListViewItem("") { Checked=selected }; item.SubItems.Add((i+1).ToString()); item.SubItems.Add(UiUtil.GetListViewTextFromString(ActorPreview(old))); item.SubItems.Add(UiUtil.GetListViewTextFromString(after)); listViewFixes.Items.Add(item);
                }
                numberOfConversions=_fixItems.Count(p=>p.Checked); return ClassicTextTools.ConvertActors(subtitle,_subtitleFormat,options,new HashSet<int>(_fixItems.Where(f=>f.Checked).Select(f=>subtitle.Paragraphs.FindIndex(p=>p.Id==f.Paragraph.Id))));
            }
            finally { listViewFixes.ItemChecked += listViewFixes_ItemChecked; }
        }
        private static string ActorPreview(Paragraph p) => (string.IsNullOrEmpty(p.Actor)?"":"["+p.Actor+"] ")+p.Text;

        private bool Contains(string text, char start, char end)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var startIdx = text.IndexOf(start);
            if (startIdx < 0)
            {
                return false;
            }

            var endIdx = text.IndexOf(end);
            return endIdx > startIdx;
        }

        private void ButtonCancelClick(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void ButtonOkClick(object sender, EventArgs e)
        {
            var settings=ClassicTextToolSettings.Load(); settings.Actors=ReadOptions();
            try { settings.Save(); } catch(Exception ex) when(ex is System.IO.IOException || ex is UnauthorizedAccessException) { MessageBox.Show(this,ex.Message,Text); return; }
            for(int i=Subtitle.Paragraphs.Count-1;i>=0;i--)
            {
                var fix=_fixItems.FirstOrDefault(f=>f.Checked && f.Paragraph.Id==Subtitle.Paragraphs[i].Id); if(fix==null)continue;
                Subtitle.Paragraphs[i]=new Paragraph(fix.Paragraph,false); if(fix.NextParagraph!=null)Subtitle.Paragraphs.Insert(i+1,new Paragraph(fix.NextParagraph,false));
            }

            Subtitle.Renumber();

            DialogResult = DialogResult.OK;
        }

        private void listViewFixes_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void listViewFixes_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_loading)
            {
                return;
            }

            _fixItems[e.Item.Index].Checked = e.Item.Checked;
            NumberOfActorConversions = _fixItems.Count(p=>p.Checked);
            groupBoxLinesFound.Text = string.Format(LanguageSettings.Current.ConvertActor.NumberOfConversionsX, NumberOfActorConversions);
        }


        private void ConvertActor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
            }
        }

        private void ConvertActor_Shown(object sender, EventArgs e)
        {
            ConvertActor_ResizeEnd(sender, e);

            _loading = false;

            GeneratePreview();
            listViewFixes.Focus();
            if (listViewFixes.Items.Count > 0)
            {
                listViewFixes.Items[0].Selected = true;
            }

        }

        private void checkBoxFixIncrementing_CheckedChanged(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            GeneratePreview();
            Cursor = Cursors.Default;
        }

        private void ConvertActor_ResizeEnd(object sender, EventArgs e)
        {
            columnHeaderAfter.Width = -2;
        }

        private void toolStripMenuItemSelectAll_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listViewFixes.Items)
            {
                _fixItems[item.Index].Checked = true;
                item.Checked = true;
            }
        }

        private void toolStripMenuItemInverseSelection_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listViewFixes.Items)
            {
                _fixItems[item.Index].Checked = !item.Checked;
                item.Checked = !item.Checked;
            }
        }

        private void ConvertActor_FormClosing(object sender, FormClosingEventArgs e)
        {
            Configuration.Settings.Tools.ConvertActorColorAdd = checkBoxColor.Checked;
            Configuration.Settings.Tools.ConvertActorColor = panelColor.BackColor;
            Configuration.Settings.Tools.ConvertActorCasing = checkBoxChangeCasing.Checked;
        }

        private void buttonColor_Click(object sender, EventArgs e)
        {
            using (var colorChooser = new ColorChooser { Color = panelColor.BackColor })
            {
                if (colorChooser.ShowDialog() == DialogResult.OK)
                {
                    panelColor.BackColor = colorChooser.Color;
                    GeneratePreview();
                }
            }
        }

        private void panelColor_MouseClick(object sender, MouseEventArgs e)
        {
            buttonColor_Click(sender, e);
        }

        private void nikseComboBoxConvertFrom_SelectedIndexChanged(object sender, EventArgs e)
        {
            GeneratePreview();
        }

        private void nikseComboBoxConvertTo_SelectedIndexChanged(object sender, EventArgs e)
        {
            GeneratePreview();
        }

        private void checkBoxChangeCasing_CheckedChanged(object sender, EventArgs e)
        {
            GeneratePreview();
        }

        private void nikseComboBoxCasing_SelectedIndexChanged(object sender, EventArgs e)
        {
            GeneratePreview();
        }

        private void checkBoxOnlyNames_CheckedChanged(object sender, EventArgs e)
        {
            GeneratePreview();
        }
    }
}
