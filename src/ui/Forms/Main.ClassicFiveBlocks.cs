using System;
using System.Windows.Forms;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Forms.Assa;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private ToolStripMenuItem _classicFontCollector;
        private ToolStripMenuItem _classicEffects,_classicDraw,_classicAssFilter;
        private ToolStripMenuItem _classicTableExport, _classicSpreadsheet, _classicXmlImport, _classicCaptions, _classicImageExport, _classicVideoTools;
        private void InitializeClassicFiveBlocks()
        {
            ClassicFontCollection.LoadCollected();
            if(_classicFontCollector==null)
            {
                _classicFontCollector=new ToolStripMenuItem {Name="classicFontCollector"};
                _classicFontCollector.Click+=(s,e)=>ShowClassicFontCollector();
                toolsToolStripMenuItem.DropDownItems.Add(_classicFontCollector);
                _classicSpreadsheet = new ToolStripMenuItem { Name = "classicSpreadsheetImport" };
                _classicSpreadsheet.Click += (s, e) => { using (var form = new ClassicSpreadsheetImport()) if (form.ShowDialog(this) == DialogResult.OK) ImportClassicSubtitle(form.Result); };
                _classicCaptions = new ToolStripMenuItem { Name = "classicCaptionImport" };
                _classicCaptions.Click += (s, e) => { using (var form = new ClassicCaptionImportForm(_videoFileName)) if (form.ShowDialog(this) == DialogResult.OK) ImportClassicSubtitle(form.Result); };
                _classicXmlImport = new ToolStripMenuItem { Name = "classicGenericXmlImport" };
                _classicXmlImport.Click += (s, e) => {
                    using (var dialog = new OpenFileDialog { Filter = "XML|*.xml;*.ttml|All files|*.*" })
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                            try { var result = new Logic.ClassicImport.UnknownFormatImporterXml().AutoGuessImport(System.IO.File.ReadAllLines(dialog.FileName).ToList()); if (result.Paragraphs.Count == 0) throw new System.IO.InvalidDataException(ClassicAssistedText.L("Реплики с тайм-кодами не найдены.", "No timed subtitles found.")); ImportClassicSubtitle(result); }
                            catch (Exception ex) { MessageBox.Show(this, ex.Message); }
                };
                toolStripMenuItemImport.DropDownItems.AddRange(new ToolStripItem[] { _classicSpreadsheet, _classicXmlImport, _classicCaptions });
                _classicImageExport = new ToolStripMenuItem { Name = "classicModernImageExport" };
                _classicImageExport.Click += (s, e) => { if (!IsSubtitleLoaded) { DisplaySubtitleNotLoadedMessage(); return; } ReloadFromSourceView(); using (var form = new ClassicImageExportForm(_subtitle)) form.ShowDialog(this); };
                toolStripMenuItemExport.DropDownItems.Add(_classicImageExport);
                _classicTableExport = new ToolStripMenuItem { Name = "classicTableExport" }; _classicTableExport.Click += (s,e) => { if(!IsSubtitleLoaded){DisplaySubtitleNotLoadedMessage();return;} ReloadFromSourceView();using(var form=new ClassicTableExportForm(new Subtitle(_subtitle,false)))form.ShowDialog(this); };toolStripMenuItemExport.DropDownItems.Add(_classicTableExport);
                _classicVideoTools = new ToolStripMenuItem { Name = "classicVideoTools" };
                _classicVideoTools.Click += (s, e) => { using (var form = new ClassicVideoToolsForm(_videoFileName)) form.ShowDialog(this); };
                toolStripMenuItemVideo.DropDownItems.Add(_classicVideoTools);
                _classicEffects=new ToolStripMenuItem {Name="classicAdvancedEffects"};_classicDraw=new ToolStripMenuItem {Name="classicAssaDraw"};
                _classicEffects.Click+=(s,e)=>ShowClassicAdvancedEffects();_classicDraw.Click+=(s,e)=>ShowClassicDraw();
                toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]{_classicEffects,_classicDraw});
                _classicAssFilter=new ToolStripMenuItem {Name="classicAssFilter"};_classicAssFilter.Click+=(s,e)=>{if(!CanEditClassicAss())return;using(var form=new ClassicAssaFilter(_subtitle,mediaPlayer.GetHiddenAssLayers(_subtitle)))if(form.ShowDialog(this)==DialogResult.OK){mediaPlayer.SetHiddenAssLayers(_subtitle,form.HiddenLayers);SubtitleListview1.SelectedItems.Clear();foreach(var i in form.SelectedIndices)SubtitleListview1.Items[i].Selected=true;if(form.SelectedIndices.Length>0)SubtitleListview1.Items[form.SelectedIndices[0]].EnsureVisible();}};toolsToolStripMenuItem.DropDownItems.Add(_classicAssFilter);
            }
            _classicFontCollector.Text=ClassicAssistedText.L("Собрать шрифты ASS/SSA…","ASS/SSA font collector…");
            _classicEffects.Text=ClassicAssistedText.L("Расширенные ASS-эффекты…","Advanced ASS effects…");_classicDraw.Text=ClassicAssistedText.L("Редактор ASS Draw…","ASS Draw editor…");
            _classicAssFilter.Text=ClassicAssistedText.L("Фильтр ASS и скрытые слои…","ASS filter and hidden layers…");
            _classicTableExport.Text = ClassicAssistedText.L("CSV/XLSX: выбрать столбцы…", "CSV/XLSX: choose columns…");
            _classicSpreadsheet.Text = ClassicAssistedText.L("Таблица CSV/XLSX/ODS: выбор столбцов…", "CSV/XLSX/ODS: map columns…");
            _classicXmlImport.Text = ClassicAssistedText.L("XML: определить структуру…", "XML: detect structure…");
            _classicCaptions.Text = ClassicAssistedText.L("CEA-608/708: выбор канала…", "CEA-608/708: select channel…");
            _classicImageExport.Text = ClassicAssistedText.L("Изображения IMSC / BDN / DVD SUP…", "IMSC / BDN / DVD SUP images…");
            _classicVideoTools.Text = ClassicAssistedText.L("Монтаж, перекодирование, дорожки и главы…", "Cuts, encoding, streams and chapters…");
        }
        private void ImportClassicSubtitle(Subtitle source)
        {
            if (source == null || !ContinueNewOrExit()) return;
            MakeHistoryForUndo("Import subtitles");
            _subtitle.Paragraphs.Clear(); _subtitle.Paragraphs.AddRange(source.Paragraphs.Select(p => new Paragraph(p, false)));
            _subtitle.Header = source.Header; _subtitle.Footer = source.Footer; _subtitle.Renumber();
            _fileName = string.Empty; _converted = true; _formatManuallyChanged = true;
            SetCurrentFormat(new SubRip().Name);
            RemoveOriginal(true, false); SubtitleListview1.Fill(_subtitle, _subtitleOriginal);
            UpdateSourceView(); audioVisualizer.Invalidate(); SetTitle();
        }
        private bool CanEditClassicAss()
        {
            if(!IsSubtitleLoaded){DisplaySubtitleNotLoadedMessage();return false;}
            if(!(GetCurrentSubtitleFormat() is AdvancedSubStationAlpha)){MessageBox.Show(this,ClassicAssistedText.L("Для рисования и эффектов выберите формат Advanced SubStation Alpha.","Choose Advanced SubStation Alpha format for drawing and effects."));return false;}ReloadFromSourceView();return true;
        }
        private void ApplyClassicAssEvents(int[] selected,System.Collections.Generic.List<Paragraph> result,string action,string header=null)
        {
            MakeHistoryForUndo(action);if(header!=null)_subtitle.Header=header;var positions=new System.Collections.Generic.HashSet<int>(selected);var remaining=_subtitle.Paragraphs.Where((p,i)=>!positions.Contains(i)).ToList();remaining.AddRange(result);_subtitle.Paragraphs.Clear();_subtitle.Paragraphs.AddRange(remaining.OrderBy(p=>p.StartTime.TotalMilliseconds).ThenBy(p=>p.Layer));_subtitle.Renumber();SubtitleListview1.Fill(_subtitle,_subtitleOriginal);UpdateSourceView();audioVisualizer.Invalidate();SetTitle();
        }
        private void ShowClassicAdvancedEffects()
        {
            if(!CanEditClassicAss())return;var selected=SubtitleListview1.SelectedIndices.Cast<int>().ToArray();if(selected.Length==0)return;var source=new Subtitle(_subtitle,false);source.Paragraphs.Clear();source.Paragraphs.AddRange(selected.Select(i=>new Paragraph(_subtitle.Paragraphs[i],false)));
            using(var form=new ClassicAdvancedEffects(source,_videoFileName,audioVisualizer.WavePeaks))if(form.ShowDialog(this)==DialogResult.OK && form.Result!=null)ApplyClassicAssEvents(selected,form.Result.Paragraphs,"Apply advanced ASS effect");
        }
        private void ShowClassicDraw()
        {
            if(!CanEditClassicAss())return;var selected=SubtitleListview1.SelectedIndices.Cast<int>().ToArray();if(selected.Length==0)return;var dimensions=Logic.ClassicEffects.AdvancedEffectUtil.GetScriptResolution(_subtitle.Header,1280,720);
            using(var form=new ClassicAssaDraw(_subtitle.Paragraphs[selected[0]],dimensions.Width,dimensions.Height,_subtitle))if(form.ShowDialog(this)==DialogResult.OK && form.Result!=null)ApplyClassicAssEvents(new[]{selected[0]},form.Result,"Edit ASS drawing",CreateClassicDrawHeader());
        }
        private string CreateClassicDrawHeader()
        {
            var header = _subtitle.Header;
            foreach (var name in new[] { "ClassicDraw", "ClassicDrawText" })
                header = AdvancedSubStationAlpha.AddSsaStyle(new SsaStyle { Name = name, Alignment = "7", MarginVertical = 0, MarginLeft = 0, MarginRight = 0, ShadowWidth = 0, OutlineWidth = 0, BorderStyle = "1" }, header);
            return header;
        }
        private void ShowClassicFontCollector()
        {
            if(!IsSubtitleLoaded){DisplaySubtitleNotLoadedMessage();return;}ReloadFromSourceView();
            using(var form=new ClassicFontCollector(_subtitle))
                if(form.ShowDialog(this)==DialogResult.OK && form.UpdatedFooter!=_subtitle.Footer)
                {MakeHistoryForUndo("Embed subtitle fonts");_subtitle.Footer=form.UpdatedFooter;UpdateSourceView();SetTitle();}
        }
    }
}
