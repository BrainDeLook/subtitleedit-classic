using System;
using System.Drawing;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
namespace Nikse.SubtitleEdit.Forms
{
 public sealed class ClassicEditorPreferencesForm:Form
 {
    public ClassicEditorPreferences Preferences {get;private set;}
    public ClassicEditorPreferencesForm()
    {
        var preferences=ClassicEditorPreferences.Load();Text=L("Дополнительные параметры редактора","Additional editor settings");ClientSize=new Size(900,640);MinimumSize=new Size(760,520);StartPosition=FormStartPosition.CenterParent;
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=2,AutoScroll=true};table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));
        var mode=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};mode.Items.AddRange(new object[]{L("Перечитывать, если нет правок","Reload when unmodified"),L("Всегда спрашивать","Always ask"),L("Не перечитывать","Never reload")});mode.SelectedIndex=(int)preferences.ExternalChanges;
        Add(table,L("Файл изменён другой программой","File changed by another program"),mode);
        var extras=new CheckBox{Text=L("Скорость и переход по репликам\nВидимость субтитров","Speed, previous/next cue, subtitle visibility"),AutoSize=true,MaximumSize=new Size(420,0),Checked=preferences.ExtraPlayerControls};Add(table,L("Панель проигрывателя","Player controls"),extras);
        var microphone=new CheckBox{Text=L("Показывать запись (локальный ASR)","Show recording button (local ASR)"),AutoSize=true,MaximumSize=new Size(420,0),Checked=preferences.Microphone};Add(table,L("Микрофон → текст реплики","Microphone → cue text"),microphone);
        var labels=new[]{L("Сдвинуть выбранные реплики на кадр назад","Move selected cues one frame back"),L("Сдвинуть выбранные реплики на кадр вперёд","Move selected cues one frame forward"),L("Начало назад с поджиманием предыдущей","Move start back, push previous end"),L("Конец вперёд с поджиманием следующей","Move end forward, push next start")};
        labels=new[]{labels[0],labels[1],L("Сдвинуть выбранные и последующие на кадр назад","Move selected and following cues one frame back"),L("Сдвинуть выбранные и последующие на кадр вперёд","Move selected and following cues one frame forward"),L("Сдвинуть все реплики на кадр назад","Move all cues one frame back"),L("Сдвинуть все реплики на кадр вперёд","Move all cues one frame forward"),labels[2],labels[3]};
        var values=new[]{preferences.MoveLineFrameBack,preferences.MoveLineFrameForward,preferences.MoveFollowingFrameBack,preferences.MoveFollowingFrameForward,preferences.MoveAllFrameBack,preferences.MoveAllFrameForward,preferences.PushStartBack,preferences.PushEndForward};var keys=new TextBox[8];
        for(var i=0;i<keys.Length;i++){var box=keys[i]=new TextBox{Dock=DockStyle.Fill,ReadOnly=true,Text=values[i]};box.KeyDown+=(sender,e)=>{e.SuppressKeyPress=true;if(e.KeyCode==Keys.Delete || e.KeyCode==Keys.Back){box.Text="";return;}if(e.KeyCode==Keys.ControlKey || e.KeyCode==Keys.ShiftKey || e.KeyCode==Keys.Menu)return;box.Text=new KeysConverter().ConvertToString(e.KeyData);};Add(table,labels[i],box);}
        var secondFont=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
        using(var fonts=new System.Drawing.Text.InstalledFontCollection())secondFont.Items.AddRange(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(fonts.Families,font=>(object)font.Name)));
        secondFont.SelectedItem=preferences.SecondarySubtitleFont;if(secondFont.SelectedIndex<0)secondFont.Text=preferences.SecondarySubtitleFont;
        Add(table,L("Шрифт второго субтитра","Second subtitle font"),secondFont);
        var step=new NumericUpDown{Dock=DockStyle.Fill,Minimum=1,Maximum=10000,Value=preferences.MoveBoundaryMilliseconds};Add(table,L("Шаг изменения границы, мс","Boundary step, ms"),step);
        var splitEnd=new CheckBox{AutoSize=true,MaximumSize=new Size(420,0),Text=L("В конце текста оставить весь текст в первой части","At text end, keep all text in the first half"),Checked=preferences.SplitAtVideoKeepTextAtEnd};Add(table,L("Разделить по видео и курсору","Split at video and text caret"),splitEnd);
        var profile=new CheckBox{Text=L("Профиль правил на панели осциллограммы","Rule profile on waveform toolbar"),AutoSize=true,MaximumSize=new Size(420,0),Checked=preferences.WaveformRuleProfile};Add(table,L("Осциллограмма","Waveform"),profile);
        var gap=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};var gapEnabled=new CheckBox{Text=L("Подсвечивать","Highlight"),AutoSize=true,Checked=preferences.ColorGapRange};var gapFrom=new NumericUpDown{Maximum=99999,Width=75,Value=preferences.GapRangeFrom};var gapTo=new NumericUpDown{Maximum=99999,Width=75,Value=preferences.GapRangeTo};var gapFrames=new CheckBox{Text=L("В кадрах (иначе мс)","Frames (otherwise ms)"),AutoSize=true,Checked=preferences.GapRangeFrames};gap.Controls.AddRange(new Control[]{gapEnabled,gapFrom,new Label{Text="—",Width=16},gapTo,gapFrames});Add(table,L("Диапазон интервала между репликами","Gap highlight range"),gap);
        var ignoreTags=new TextBox{Dock=DockStyle.Fill,Text=preferences.IgnoreCustomTagsPattern};Add(table,L("Не считать пользовательские теги (regex)","Ignore custom tags in counts (regex)"),ignoreTags);
        var note=new Label{AutoSize=true,MaximumSize=new Size(420,0),Text=L("Нажмите сочетание в поле; Backspace/Delete очищает его. Запись использует установленный локальный движок из окна «Аудио в текст» и не скачивает модели автоматически.","Press a shortcut in its field; Backspace/Delete clears it. Recording uses the installed local engine selected in Audio to text and never downloads models automatically.")};Add(table,"",note);
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=42,FlowDirection=FlowDirection.RightToLeft};var cancel=new Button{Text=L("Отмена","Cancel"),DialogResult=DialogResult.Cancel,AutoSize=true};var save=new Button{Text=L("Сохранить","Save"),AutoSize=true};footer.Controls.AddRange(new Control[]{cancel,save});Controls.Add(table);Controls.Add(footer);CancelButton=cancel;
        save.Click+=(sender,e)=>{if(gapTo.Value<gapFrom.Value){MessageBox.Show(this,L("Конец диапазона меньше начала.","Range end is less than its start."),Text);return;}preferences.SplitAtVideoKeepTextAtEnd=splitEnd.Checked;preferences.ColorGapRange=gapEnabled.Checked;preferences.GapRangeFrames=gapFrames.Checked;preferences.GapRangeFrom=(int)gapFrom.Value;preferences.GapRangeTo=(int)gapTo.Value;preferences.WaveformRuleProfile=profile.Checked;preferences.ExternalChanges=(ClassicExternalChangeMode)mode.SelectedIndex;preferences.ExtraPlayerControls=extras.Checked;preferences.Microphone=microphone.Checked;preferences.MoveLineFrameBack=keys[0].Text;preferences.MoveLineFrameForward=keys[1].Text;preferences.MoveFollowingFrameBack=keys[2].Text;preferences.MoveFollowingFrameForward=keys[3].Text;preferences.MoveAllFrameBack=keys[4].Text;preferences.MoveAllFrameForward=keys[5].Text;preferences.PushStartBack=keys[6].Text;preferences.PushEndForward=keys[7].Text;preferences.SecondarySubtitleFont=secondFont.Text;preferences.MoveBoundaryMilliseconds=(int)step.Value;try{Nikse.SubtitleEdit.Core.Common.TextLengthCalculator.CalcFactory.SetIgnoreTagsPattern(ignoreTags.Text);if(!string.IsNullOrWhiteSpace(ignoreTags.Text) && !Nikse.SubtitleEdit.Core.Common.TextLengthCalculator.CalcFactory.HasIgnoreTagsPattern){MessageBox.Show(this,L("Некорректное регулярное выражение.","Invalid regular expression."),Text);return;}preferences.IgnoreCustomTagsPattern=ignoreTags.Text;preferences.Save();Preferences=preferences;DialogResult=DialogResult.OK;}catch(Exception ex)when(ex is System.IO.IOException || ex is UnauthorizedAccessException){MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}};
        Action wrap = () =>
        {
            var widths = table.GetColumnWidths();
            foreach (Control control in table.Controls)
            {
                var column = table.GetColumn(control);
                if (column < 0 || column >= widths.Length) continue;
                var width = Math.Max(60, widths[column] - control.Margin.Horizontal);
                if (control is Label && control.AutoSize)
                {
                    if (control.MaximumSize.Width != width) control.MaximumSize = new Size(width, 0);
                }
                else if (control is CheckBox checkbox)
                {
                    // AutoSize clamps the rectangle to MaximumSize but the native
                    // CheckBox still measures a single line. Give it the full wrapped
                    // text height explicitly, including room beside the check glyph.
                    checkbox.AutoSize = false;
                    checkbox.MaximumSize = Size.Empty;
                    checkbox.Width = width;
                    var measured = TextRenderer.MeasureText(checkbox.Text, checkbox.Font,
                        new Size(Math.Max(1, width - 24), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                    checkbox.Height = Math.Max(checkbox.Font.Height + 8, measured.Height + 8);
                }
            }
        };
        table.SizeChanged+=(sender,e)=>wrap();Shown+=(sender,e)=>wrap();UiUtil.FixFonts(this);
    }
    private static void Add(TableLayoutPanel table,string label,Control control){var row=table.RowCount++;table.RowStyles.Add(new RowStyle(SizeType.AutoSize));table.Controls.Add(new Label{AutoSize=true,MaximumSize=new Size(360,0),Text=label,Padding=new Padding(0,7,0,8)},0,row);table.Controls.Add(control,1,row);}
    private static string L(string ru,string en)=>ClassicAssistedText.L(ru,en);
 }
}
