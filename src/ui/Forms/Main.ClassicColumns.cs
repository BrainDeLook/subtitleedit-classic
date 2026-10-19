using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using C = Nikse.SubtitleEdit.Controls.SubtitleListView.SubtitleColumn;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private ToolStripMenuItem _classicColumnsMenu,_classicColumnsContext;
        private List<ClassicColumnPreference> _classicColumns;
        private bool _applyingClassicColumns;
        private bool CurrentColumnVisible(C column)
        {
            var tools=Configuration.Settings.Tools;
            switch(column)
            {
                case C.Start:return tools.ListViewShowColumnStartTime;case C.End:return tools.ListViewShowColumnEndTime;case C.Duration:return tools.ListViewShowColumnDuration;
                case C.CharactersPerSeconds:return tools.ListViewShowColumnCharsPerSec;case C.WordsPerMinute:return tools.ListViewShowColumnWordsPerMin;case C.Gap:return tools.ListViewShowColumnGap;
                case C.Actor:return tools.ListViewShowColumnActor || SubtitleListview1.ColumnIndexActor>=0;case C.Region:return tools.ListViewShowColumnRegion;
                case C.ShotIn:case C.ShotOut:case C.Layer:case C.PixelWidth:case C.Teletext:return false;
                case C.Forced:return ClassicGridAppearance.Load(ClassicGridAppearancePath).ShowForcedColumn;case C.Extra:return true;default:return true;
            }
        }
        private string ColumnLabel(C column)
        {
            var general=LanguageSettings.Current.General;
            switch(column)
            {
                case C.Number:return general.NumberSymbol;case C.Start:return general.StartTime;case C.End:return general.EndTime;case C.Duration:return general.Duration;
                case C.Text:return general.Text;case C.TextOriginal:return ClassicAssistedText.L("Оригинал","Original text");case C.Actor:return general.Actor;case C.Region:return general.Region;
                case C.CharactersPerSeconds:return general.CharsPerSec;case C.WordsPerMinute:return general.WordsPerMin;case C.Gap:return general.Gap;
                case C.Teletext:return "Teletext";case C.Layer:return general.Layer;case C.PixelWidth:return ClassicAssistedText.L("Ширина, px","Pixel width");
                case C.ShotIn:return "Shot in";case C.ShotOut:return "Shot out";
                case C.Extra:return ClassicAssistedText.L("Стиль / дополнительно","Style / extra");case C.Network:return ClassicAssistedText.L("Сетевой статус","Network status");default:return "Forced";
            }
        }
        private string ColumnNote(C column)
        {
            if(column==C.Number || column==C.Text)return ClassicAssistedText.L("Обязательный столбец","Required column");
            if(column==C.TextOriginal)return ClassicAssistedText.L("Показывается, когда открыт оригинал","Shown while an original subtitle is open");
            if(column==C.Network)return ClassicAssistedText.L("Управляется сетевым сеансом","Managed by the network session");
            if(column==C.Region)return ClassicAssistedText.L("В форматах, где этот столбец используется","When the subtitle format uses this column");
            if(column==C.Teletext)return ClassicAssistedText.L("Только EBU STL: строка 1–23 и выравнивание L/C/R","EBU STL only: row 1–23 and alignment L/C/R");
            if(column==C.Extra)return ClassicAssistedText.L("Стиль формата; в WebVTT — классы из <c.class>","Format style; WebVTT classes from <c.class>");
            if(column==C.Actor)return ClassicAssistedText.L("Актёр формата; в WebVTT — голос из <v Name>","Format actor; WebVTT voice from <v Name>");
            if(column==C.Layer)return ClassicAssistedText.L("Только ASS: слой наложения реплики","ASS only: subtitle stacking layer");
            if(column==C.PixelWidth)return ClassicAssistedText.L("Самая широкая строка без тегов. Используется шрифт измерения из параметров.","Widest line without tags, using the measurement font from Settings.");
            if(column==C.ShotIn || column==C.ShotOut)return ClassicAssistedText.L("От начала / конца до смены плана: − до, + после; мс или кадры. Нужны видео и смены плана.","Start / end minus nearest cut: − before, + after; ms or frames. Requires video and shot changes.");
            return string.Empty;
        }
        private void InitializeClassicColumns()
        {
            if(_classicColumnsMenu==null)
            {
                _classicColumnsMenu=new ToolStripMenuItem {Name="classicGridColumns"};_classicColumnsContext=new ToolStripMenuItem {Name="classicGridColumnsContext"};
                optionsToolStripMenuItem.DropDownItems.Add(_classicColumnsMenu);contextMenuStripListView.Items.Add(_classicColumnsContext);
                _classicColumnsMenu.Click+=(s,e)=>ShowClassicColumns();_classicColumnsContext.Click+=(s,e)=>ShowClassicColumns();
                var saved=ClassicGridAppearance.Load(ClassicGridAppearancePath).Columns;
                if(saved!=null)ApplyClassicColumns(saved,false);
                SubtitleListview1.ColumnReordered+=(s,e)=>SaveClassicColumnDrag(e);
            }
            _classicColumnsMenu.Text=_classicColumnsContext.Text=ClassicAssistedText.L("Столбцы…","Columns…");
        }
        private List<ClassicColumnPreference> DefaultClassicColumns() => ClassicColumnPreference.Normalize(null,c=>c==C.Start || c==C.End || c==C.Duration || c==C.Extra);
        private List<ClassicColumnPreference> CaptureClassicColumns()
        {
            if(_classicColumns!=null)return _classicColumns.Select(p=>p.Copy()).ToList();
            var preferences=ClassicColumnPreference.Normalize(null,CurrentColumnVisible);var current=SubtitleListview1.GetColumnDisplayOrder();
            return preferences.OrderBy(p=>Array.IndexOf(current,(C)Enum.Parse(typeof(C),p.Key))<0?int.MaxValue:Array.IndexOf(current,(C)Enum.Parse(typeof(C),p.Key))).ToList();
        }
        private void ShowClassicColumns()
        {
            ReloadFromSourceView();
            using(var dialog=new ClassicGridColumns(CaptureClassicColumns(),ColumnLabel,ColumnNote,DefaultClassicColumns))
                if(dialog.ShowDialog(this)==DialogResult.OK)try{ApplyClassicColumns(dialog.Result,true);}catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException){MessageBox.Show(this,ex.Message,dialog.Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        private void SyncClassicColumnTools()
        {
            var tools=Configuration.Settings.Tools;Func<C,bool> visible=c=>_classicColumns.Single(p=>p.Key==c.ToString()).Visible;
            tools.ListViewShowColumnStartTime=visible(C.Start);tools.ListViewShowColumnEndTime=visible(C.End);tools.ListViewShowColumnDuration=visible(C.Duration);
            tools.ListViewShowColumnCharsPerSec=visible(C.CharactersPerSeconds);tools.ListViewShowColumnWordsPerMin=visible(C.WordsPerMinute);tools.ListViewShowColumnGap=visible(C.Gap);
            tools.ListViewShowColumnActor=visible(C.Actor);tools.ListViewShowColumnRegion=visible(C.Region);
        }
        private void ApplyClassicColumns(IEnumerable<ClassicColumnPreference> preferences,bool save)
        {
            var normalized=ClassicColumnPreference.Normalize(preferences,CurrentColumnVisible);
            if(save){var settings=ClassicGridAppearance.Load(ClassicGridAppearancePath);settings.Columns=normalized;settings.ShowForcedColumn=normalized.Single(p=>p.Key==C.Forced.ToString()).Visible;settings.Save(ClassicGridAppearancePath);}
            var selected=SubtitleListview1.GetSelectedIndices();var top=SubtitleListview1.TopItem?.Index??-1;
            _applyingClassicColumns=true;
            try
            {
                _classicColumns=normalized;SyncClassicColumnTools();SubtitleListview1.SetColumnPreferences(_classicColumns);
                SubtitleListview1.Items.Clear();
                var format=_currentSubtitleFormat??_subtitle.OriginalFormat??new SubRip();
                foreach(var preference in normalized)
                {
                    var column=(C)Enum.Parse(typeof(C),preference.Key);if(!ClassicColumnPreference.CanToggle(column))continue;
                    if(!preference.Visible){SubtitleListview1.HideColumn(column);continue;}
                    switch(column)
                    {
                        case C.Start:SubtitleListview1.ShowStartColumn(ColumnLabel(column));break;case C.End:SubtitleListview1.ShowEndColumn(ColumnLabel(column));break;case C.Duration:SubtitleListview1.ShowDurationColumn(ColumnLabel(column));break;
                        case C.CharactersPerSeconds:SubtitleListview1.ShowCharsSecColumn(ColumnLabel(column));break;case C.WordsPerMinute:SubtitleListview1.ShowWordsMinColumn(ColumnLabel(column));break;case C.Gap:SubtitleListview1.ShowGapColumn(ColumnLabel(column));break;case C.Forced:SubtitleListview1.ShowForcedColumn("Forced");break;
                        case C.Layer:if(format is AdvancedSubStationAlpha)SubtitleListview1.ShowDetailColumn(column,ColumnLabel(column));else SubtitleListview1.HideColumn(column);break;
                        case C.PixelWidth:if(!format.IsVobSubIndexFile)SubtitleListview1.ShowDetailColumn(column,ColumnLabel(column));else SubtitleListview1.HideColumn(column);break;
                        case C.ShotIn:case C.ShotOut:SubtitleListview1.ShowShotColumn(column,ColumnLabel(column));break;
                        case C.Teletext:if(format is Ebu)SubtitleListview1.ShowDetailColumn(column,ColumnLabel(column));else SubtitleListview1.HideColumn(column);break;
                        case C.Extra:if(format.HasStyleSupport || IsClassicWebVtt(format))SubtitleListview1.ShowExtraColumn(ColumnLabel(column));break;
                        case C.Actor:if(format is AdvancedSubStationAlpha || format is SubStationAlpha || format is CsvNuendo || format is PodcastIndexer || IsClassicWebVtt(format))SubtitleListview1.ShowActorColumn(ColumnLabel(column));break;
                        case C.Region:if(format is TimedText10)SubtitleListview1.ShowRegionColumn(ColumnLabel(column));break;
                    }
                }
                SyncClassicDetailColumns(format);
                SubtitleListview1.Fill(_subtitle,_subtitleOriginal);_selectedIndices=selected.ToList();RestoreSubtitleListviewIndices();
                if(top>=0 && top<SubtitleListview1.Items.Count)SubtitleListview1.TopItem=SubtitleListview1.Items[top];
                RefreshClassicForcedAvailability();
                RefreshClassicShotColumns();
                SubtitleListview1.RefreshDetailColumns();
            }
            finally{_applyingClassicColumns=false;}
        }
        private static bool IsClassicWebVtt(SubtitleFormat format) => format is WebVTT || format is WebVTTFileWithLineNumber;
        private void SyncClassicDetailColumns(SubtitleFormat format)
        {
            var webVtt=IsClassicWebVtt(format);
            if(SubtitleListview1.UseWebVttMetadata && !webVtt)
            {
                SubtitleListview1.HideColumn(C.Extra);SubtitleListview1.HideColumn(C.Actor);
            }
            SubtitleListview1.UseWebVttMetadata=webVtt;
            if(_classicColumns==null)return;
            if(webVtt)
            {
                if(_classicColumns.Single(p=>p.Key==C.Extra.ToString()).Visible)SubtitleListview1.ShowDetailColumn(C.Extra,ClassicAssistedText.L("Стиль (WebVTT)","Style (WebVTT)"));
                else SubtitleListview1.HideColumn(C.Extra);
                if(_classicColumns.Single(p=>p.Key==C.Actor.ToString()).Visible)SubtitleListview1.ShowDetailColumn(C.Actor,ClassicAssistedText.L("Голос (WebVTT)","Voice (WebVTT)"));
                else SubtitleListview1.HideColumn(C.Actor);
            }
            foreach(var column in new[]{C.Layer,C.PixelWidth,C.Teletext})
            {
                var enabled=_classicColumns.Single(p=>p.Key==column.ToString()).Visible;
                var available=column==C.Layer?format is AdvancedSubStationAlpha:column==C.Teletext?format is Ebu:format!=null && !format.IsVobSubIndexFile;
                if(enabled && available)SubtitleListview1.ShowDetailColumn(column,ColumnLabel(column));
                else SubtitleListview1.HideColumn(column);
            }
        }
        private void RefreshClassicShotColumns()
        {
            if(SubtitleListview1.ColumnIndexShotIn<0 && SubtitleListview1.ColumnIndexShotOut<0)return;
            SubtitleListview1.UpdateShotChangeOffsets(string.IsNullOrEmpty(_videoFileName)?null:audioVisualizer.ShotChanges,
                Configuration.Settings.BeautifyTimeCodes.Profile, Configuration.Settings.General.CurrentFrameRate,
                Configuration.Settings.General.UseTimeFormatHHMMSSFF);
        }
        private void SyncClassicForcedColumnPreference(bool visible)
        {
            if(_classicColumns==null)return;
            _classicColumns.Single(p=>p.Key==C.Forced.ToString()).Visible=visible;SubtitleListview1.SetColumnPreferences(_classicColumns);
        }
        private void RefreshClassicColumnsFromLegacySettings()
        {
            if(_classicColumns==null)return;
            var tools=Configuration.Settings.Tools;var values=new Dictionary<C,bool>{{C.Start,tools.ListViewShowColumnStartTime},{C.End,tools.ListViewShowColumnEndTime},{C.Duration,tools.ListViewShowColumnDuration},{C.CharactersPerSeconds,tools.ListViewShowColumnCharsPerSec},{C.WordsPerMinute,tools.ListViewShowColumnWordsPerMin},{C.Gap,tools.ListViewShowColumnGap},{C.Actor,tools.ListViewShowColumnActor},{C.Region,tools.ListViewShowColumnRegion}};
            foreach(var p in _classicColumns)if(values.TryGetValue((C)Enum.Parse(typeof(C),p.Key),out var visible))p.Visible=visible;
            ApplyClassicColumns(_classicColumns,true);
        }
        private void SaveClassicColumnDrag(ColumnReorderedEventArgs args)
        {
            if(_applyingClassicColumns || SubtitleListview1.ApplyingColumnOrder)return;
            var current=SubtitleListview1.GetColumnDisplayOrder().ToList();var moved=current[args.OldDisplayIndex];current.RemoveAt(args.OldDisplayIndex);current.Insert(args.NewDisplayIndex,moved);
            var preferences=CaptureClassicColumns();var visibleKeys=new HashSet<string>(current.Select(c=>c.ToString()));var reordered=new Queue<C>(current);
            foreach(var preference in preferences)if(visibleKeys.Contains(preference.Key))preference.Key=reordered.Dequeue().ToString();
            // Keep visibility attached to its logical key rather than the old row position.
            var visibility=CaptureClassicColumns().ToDictionary(p=>p.Key,p=>p.Visible);foreach(var p in preferences)p.Visible=visibility[p.Key];
            try
            {
                var settings=ClassicGridAppearance.Load(ClassicGridAppearancePath);settings.Columns=preferences;settings.Save(ClassicGridAppearancePath);_classicColumns=preferences;
                if(SubtitleListview1.IsHandleCreated)SubtitleListview1.BeginInvoke(new Action(()=>{if(!SubtitleListview1.IsDisposed)SubtitleListview1.SetColumnPreferences(_classicColumns);}));
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException){args.Cancel=true;ShowStatus(ex.Message);}
        }
    }
}
