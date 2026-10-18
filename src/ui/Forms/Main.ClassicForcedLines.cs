using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private ToolStripMenuItem _classicForcedToggle, _classicForcedContext, _classicForcedExport, _classicForcedColumn;
        private string ForcedText(string ru, string en) => ClassicAssistedText.L(ru,en);
        private void InitializeClassicForcedLines()
        {
            if (_classicForcedToggle == null)
            {
                _classicForcedToggle = new ToolStripMenuItem { Name="classicToggleForced" };
                _classicForcedContext = new ToolStripMenuItem { Name="classicToggleForcedContext" };
                _classicForcedExport = new ToolStripMenuItem { Name="classicSaveForcedLines" };
                _classicForcedColumn = new ToolStripMenuItem { Name="classicShowForcedColumn" };
                editToolStripMenuItem.DropDownItems.Add(_classicForcedToggle);
                contextMenuStripListView.Items.Insert(0,_classicForcedContext);
                fileToolStripMenuItem.DropDownItems.Insert(fileToolStripMenuItem.DropDownItems.IndexOf(saveAsToolStripMenuItem) + 1, _classicForcedExport);
                optionsToolStripMenuItem.DropDownItems.Add(_classicForcedColumn);
                _classicForcedToggle.Click += (s,e)=>ToggleClassicForcedLines();
                _classicForcedContext.Click += (s,e)=>ToggleClassicForcedLines();
                _classicForcedExport.Click += (s,e)=>ShowClassicForcedExport();
                _classicForcedColumn.Click += (s,e)=>SetClassicForcedColumn(SubtitleListview1.ColumnIndexForced<0,true);
                editToolStripMenuItem.DropDownOpening += (s,e)=>RefreshClassicForcedAvailability();
                fileToolStripMenuItem.DropDownOpening += (s,e)=>RefreshClassicForcedAvailability();
                contextMenuStripListView.Opening += (s,e)=>RefreshClassicForcedAvailability();
                SetClassicForcedColumn(ClassicGridAppearance.Load(ClassicGridAppearancePath).ShowForcedColumn,false);
            }
            _classicForcedToggle.Text = _classicForcedContext.Text = ForcedText("Переключить отметку Forced", "Toggle forced");
            _classicForcedExport.Text = ForcedText("Сохранить Forced-реплики как…", "Save forced lines as…");
            _classicForcedColumn.Text = ForcedText("Показывать столбец Forced", "Show Forced column");
            RefreshClassicForcedAvailability();
        }
        private void RefreshClassicForcedAvailability()
        {
            if (_classicForcedToggle==null) return;
            _classicForcedToggle.Enabled = _classicForcedContext.Enabled = SubtitleListview1.SelectedIndices.Cast<int>().Any(i=>i>=0 && i<_subtitle.Paragraphs.Count);
            _classicForcedExport.Enabled = _subtitle.Paragraphs.Any(p=>p.Forced);
            _classicForcedColumn.Checked = SubtitleListview1.ColumnIndexForced>=0;
        }
        private void SetClassicForcedColumn(bool visible, bool save)
        {
            try
            {
                if(save) { var settings=ClassicGridAppearance.Load(ClassicGridAppearancePath); settings.ShowForcedColumn=visible; settings.Save(ClassicGridAppearancePath); }
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException) { MessageBox.Show(this,ex.Message);return; }
            if(visible) SubtitleListview1.ShowForcedColumn("Forced"); else SubtitleListview1.HideColumn(SubtitleListView.SubtitleColumn.Forced);
            if(_classicForcedColumn!=null) _classicForcedColumn.Checked=visible;
        }
        private void ToggleClassicForcedLines()
        {
            ReloadFromSourceView();
            var indices=SubtitleListview1.SelectedIndices.Cast<int>().Where(i=>i>=0 && i<_subtitle.Paragraphs.Count).ToArray();
            if(indices.Length==0) return;
            var forced=!indices.All(i=>_subtitle.Paragraphs[i].Forced);
            MakeHistoryForUndo(ForcedText("До изменения отметок Forced", "Before changing forced marks"));
            foreach(var index in indices) { _subtitle.Paragraphs[index].Forced=forced; SubtitleListview1.SetForced(index,forced); }
            if(forced && SubtitleListview1.ColumnIndexForced<0) SetClassicForcedColumn(true,true);
            var saved=SaveClassicSubtitleMarks();
            RefreshClassicForcedAvailability();
            ShowStatus(saved ? string.Format(ForcedText(forced ? "Отмечено Forced: {0}" : "Снята отметка Forced: {0}",forced ? "Marked as forced: {0}" : "Unmarked as forced: {0}"),indices.Length)
                : ForcedText("Отметки изменены, но не удалось сохранить файл отметок рядом с субтитрами.","Marks changed, but the sidecar could not be saved next to the subtitle."));
        }
        private bool SaveClassicSubtitleMarks() => string.IsNullOrWhiteSpace(_fileName) || new ClassicSubtitleMarksPersistence(_subtitle,_fileName).Save();
        private void ShowClassicForcedExport()
        {
            ReloadFromSourceView();
            if(!_subtitle.Paragraphs.Any(p=>p.Forced)) { ShowStatus(ForcedText("Нет реплик с отметкой Forced.","No forced lines found."));return; }
            var formats=SubtitleFormat.AllSubtitleFormats.Where(f=>f.IsTextBased || f is Ebu).ToList();
            var current=GetCurrentSubtitleFormat();var index=formats.FindIndex(f=>f.GetType()==current.GetType()); if(index<0)index=formats.FindIndex(f=>f is SubRip);
            using(var dialog=new SaveFileDialog { Title=ForcedText("Сохранить Forced-реплики как", "Save forced lines as"),
                Filter=string.Join("|",formats.Select(f=>f.FriendlyName+" (*"+f.Extension+")|*"+f.Extension)), FilterIndex=index+1,AddExtension=true,OverwritePrompt=true,
                DefaultExt=formats[index].Extension.TrimStart('.'), FileName=(string.IsNullOrEmpty(_fileName)?"Untitled":Path.GetFileNameWithoutExtension(_fileName))+".forced"+formats[index].Extension })
            {
                if(!string.IsNullOrEmpty(_fileName)) dialog.InitialDirectory=Path.GetDirectoryName(_fileName);
                if(dialog.ShowDialog(this)!=DialogResult.OK) return;
                try
                {
                    var format=formats[dialog.FilterIndex-1];
                    var path=dialog.FileName;
                    if(!path.EndsWith(format.Extension,StringComparison.OrdinalIgnoreCase)) path+=format.Extension;
                    if(path!=dialog.FileName && File.Exists(path) && MessageBox.Show(this,ForcedText("Заменить существующий файл?", "Replace the existing file?")+Environment.NewLine+path,dialog.Title,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
                    var count=SaveClassicForcedExport(path,format);ShowStatus(string.Format(ForcedText("Сохранено Forced-реплик: {0}","Forced lines saved: {0}"),count));
                }
                catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException) { MessageBox.Show(this,ex.Message,dialog.Title,MessageBoxButtons.OK,MessageBoxIcon.Warning); }
            }
        }
        private int SaveClassicForcedExport(string path, SubtitleFormat format)
        {
            var full=Path.GetFullPath(path);
            if(new[]{_fileName,_subtitleOriginalFileName}.Where(p=>!string.IsNullOrWhiteSpace(p)).Any(p=>string.Equals(Path.GetFullPath(p),full,StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(ForcedText("Выберите отдельный файл, чтобы не заменить открытые субтитры.","Choose a separate file to avoid replacing an open subtitle."));
            var subset=new Subtitle(_subtitle,false);subset.Paragraphs.Clear();
            subset.Paragraphs.AddRange(_subtitle.Paragraphs.Where(p=>p.Forced).Select(p=>new Paragraph(p,false)));subset.Renumber();
            if(subset.Paragraphs.Count==0) return 0;
            subset=GetSaveSubtitle(subset);
            if(format==null || (!format.IsTextBased && !(format is Ebu))) throw new InvalidOperationException("Unsupported forced-lines export format.");
            var temporary=full+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                if(format is Ebu ebu) { if(!ebu.Save(temporary,subset)) throw new IOException("Unable to save EBU subtitles."); }
                else File.WriteAllText(temporary,subset.ToText(format),GetCurrentEncoding());
                if(File.Exists(full)) File.Replace(temporary,full,null); else File.Move(temporary,full);
            }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
            // Formats such as SRT/ASS do not store the per-cue forced flag themselves.
            if(!new ClassicSubtitleMarksPersistence(subset,full).Save()) throw new IOException(ForcedText("Субтитры сохранены, но не удалось записать отметки Forced рядом с файлом.","Subtitles were saved, but the forced-marks sidecar could not be written."));
            return subset.Paragraphs.Count;
        }
    }
}
