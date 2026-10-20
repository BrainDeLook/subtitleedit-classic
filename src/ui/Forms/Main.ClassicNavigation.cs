using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
using C = Nikse.SubtitleEdit.Controls.SubtitleListView.SubtitleColumn;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Main
    {
        private readonly Dictionary<string, ToolStripMenuItem> _classicNavigationGroups = new Dictionary<string, ToolStripMenuItem>();
        private ToolStripMenuItem _classicQuickColumns;
        private bool _classicNavigationEvents;
        private readonly Dictionary<ToolStripMenuItem, string> _classicNavigationCommandIds = new Dictionary<ToolStripMenuItem, string>();
        private readonly HashSet<ToolStripMenuItem> _classicNavigationAliases = new HashSet<ToolStripMenuItem>();

        private void CaptureClassicNavigationCommandIds(ToolStripItemCollection items, string path)
        {
            foreach (var item in items.OfType<ToolStripMenuItem>())
            {
                var text = ClassicCommandPalette.Clean(item.Text);
                if (!_classicNavigationCommandIds.ContainsKey(item))
                {
                    var formerlyUnnamed = new[] { "classicImproveTimeCodes", "classicVideoOcr", "classicFfmpegPlayer", "classicFfmpegAudio", "classicFfmpegHardware", "classicTrackTimeline", "classicWaveformSettings" }.Contains(item.Name);
                    _classicNavigationCommandIds[item] = "menu:" + (string.IsNullOrEmpty(item.Name) || formerlyUnnamed ? path + "/" + text : item.Name);
                }
                CaptureClassicNavigationCommandIds(item.DropDownItems, path.Length == 0 ? text : path + " → " + text);
            }
        }

        private string ClassicNavigationCommandId(ToolStripMenuItem item, string group, string text)
            => _classicNavigationCommandIds.TryGetValue(item, out var id) ? id : "menu:" + (string.IsNullOrEmpty(item.Name) ? group + "/" + text : item.Name);

        // Keep the original command instances: shortcuts, enablement and click handlers
        // continue to belong to the existing Classic commands after reparenting.
        private ToolStripMenuItem NavigationGroup(ToolStripItemCollection owner, string name, string ru, string en, params ToolStripItem[] commands)
        {
            if (!_classicNavigationGroups.TryGetValue(name, out var group))
            {
                group = new ToolStripMenuItem { Name = name };
                _classicNavigationGroups.Add(name, group);
            }
            group.Text = ClassicAssistedText.L(ru, en);
            if (!owner.Contains(group)) owner.Add(group);
            foreach (var command in commands.Where(c => c != null))
                if (!group.DropDownItems.Contains(command)) group.DropDownItems.Add(command);
            return group;
        }

        private static ToolStripMenuItem FindClassicMenu(ToolStripItemCollection owner, string name)
        {
            foreach (var menu in owner.OfType<ToolStripMenuItem>())
            {
                if (menu.Name == name) return menu;
                var child = FindClassicMenu(menu.DropDownItems, name);
                if (child != null) return child;
            }
            return null;
        }

        private ToolStripMenuItem NavigationAction(ToolStripItemCollection owner, string name, string ru, string en, Action action)
        {
            var item = owner[name] as ToolStripMenuItem;
            if (item == null)
            {
                item = new ToolStripMenuItem { Name = name };
                item.Click += (s, e) => action();
                owner.Add(item);
            }
            item.Text = ClassicAssistedText.L(ru, en);
            return item;
        }

        private void NavigationNamedGroup(ToolStripItemCollection owner, string name, string ru, string en, params string[] commands)
            => NavigationGroup(owner, name, ru, en, commands.Select(command => (ToolStripItem)FindClassicMenu(menuStrip1.Items, command)).ToArray());

        private void InitializeClassicFileNavigation()
        {
            var translation = NavigationGroup(fileToolStripMenuItem.DropDownItems, "classicTranslationFiles", "Файлы в режиме перевода", "Translation-mode files",
                openOriginalToolStripMenuItem, saveOriginalToolStripMenuItem, saveOriginalAstoolStripMenuItem, removeOriginalToolStripMenuItem, removeTranslationToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Remove(translation);
            fileToolStripMenuItem.DropDownItems.Insert(fileToolStripMenuItem.DropDownItems.IndexOf(toolStripMenuItemImport), translation);
            NavigationNamedGroup(toolStripMenuItemImport.DropDownItems, "classicImportText", "Текст, таблицы и тайм-коды", "Text, tables and time codes",
                "toolStripMenuItemImportText", "toolStripMenuItemImportManualAnsi", "toolStripMenuItemImportTimeCodes", "classicSpreadsheetImport", "classicGenericXmlImport");
            NavigationNamedGroup(toolStripMenuItemImport.DropDownItems, "classicImportOcr", "Видео, изображения и OCR", "Video, images and OCR",
                "toolStripMenuItemImportFromVideo", "toolStripMenuItemImportImages", "toolStripMenuItemImportBluRaySup", "toolStripMenuItemImportBluraySupFileForEdit",
                "toolStripMenuItemImportSubIdx", "toolStripMenuItemImportDvdSubtitles", "toolStripMenuItemImportOcrHardSub");
            NavigationNamedGroup(toolStripMenuItemExport.DropDownItems, "classicExportText", "Текст и таблицы", "Text and tables",
                "toolStripMenuItemExportPlainText", "classicTableExport", "toolStripMenuItemExportTextTimeCodePair", "toolStripMenuItemExportCustomTextFormat");
            NavigationNamedGroup(toolStripMenuItemExport.DropDownItems, "classicExportImages", "Изображения и диски", "Images and discs",
                "classicModernImageExport", "toolStripMenuItemExportPngXml", "toolStripMenuItemExportBluraySup", "toolStripMenuItemExportBdTextSt",
                "toolStripMenuItemExportVobSubSubIdx", "toolStripMenuItemExportSpumux", "toolStripMenuItemExportDvdStudioProStl", "toolStripMenuItemExportImagePerFrame", "toolStripMenuItemExportTtmlImage");
            NavigationNamedGroup(toolStripMenuItemExport.DropDownItems, "classicExportEditing", "Монтажные программы", "Video editing applications",
                "toolStripMenuItemExportAdobeEncoreFABImageScript", "toolStripMenuItemExportAvidStl", "toolStripMenuItemExportFcpIImage", "toolStripMenuItemExportFcpXmlAdvanced",
                "toolStripMenuItemExportEdl", "toolStripMenuItemExportEdlClipName", "toolStripMenuItemExportDost", "toolStripMenuItemExportAyato");
            NavigationNamedGroup(toolStripMenuItemExport.DropDownItems, "classicExportCinema", "Кинотеатральные D-Cinema", "D-Cinema",
                "toolStripMenuItemExportDcinemaInterop", "toolStripMenuItemExportDcinemaSMPTE2014");
            NavigationNamedGroup(toolStripMenuItemExport.DropDownItems, "classicExportBroadcast", "Телевизионные форматы", "Broadcast formats",
                "toolStripMenuItemExportCapMakerPlus", "toolStripMenuItemExportCaptionInc", "toolStripMenuItemExportCavena890", "toolStripMenuItemExportCheetahCap",
                "toolStripMenuItemExportEBUSTL", "toolStripMenuItemExportPACScreenElectronics", "toolStripMenuItemExportUniPac", "toolStripMenuItemExportUltech130");
            CleanNavigationSeparators(toolStripMenuItemImport.DropDownItems);
            CleanNavigationSeparators(toolStripMenuItemExport.DropDownItems);
        }

        private void InitializeClassicNavigation()
        {
            if (!_classicNavigationEvents) CaptureClassicNavigationCommandIds(menuStrip1.Items, "");
            InitializeClassicFileNavigation();
            NavigationGroup(toolsToolStripMenuItem.DropDownItems, "classicToolsTiming", "Тайминги и интервалы", "Timing and gaps",
                adjustDisplayTimeToolStripMenuItem, toolStripMenuItemApplyDurationLimits, toolStripMenuItemSubtitlesBridgeGaps,
                setMinimumDisplayTimeBetweenParagraphsToolStripMenuItem, toolStripMenuItemBeautifyTimeCodes, _classicImproveTimeCodesMenu);
            NavigationGroup(toolsToolStripMenuItem.DropDownItems, "classicToolsText", "Текст и разбиение", "Text and splitting",
                ChangeCasingToolStripMenuItem, removeTextForHearImpairedToolStripMenuItem, toolStripMenuItemAutoMergeShortLines,
                toolStripMenuItemMergeDuplicateText, toolStripMenuItemMergeLinesWithSameTimeCodes, toolStripMenuItemAutoSplitLongLines,
                _assistedSplitMenu, _assistedMoveMenu, _classicMergeContinuation, _classicUnicodeCharacters);
            NavigationGroup(toolsToolStripMenuItem.DropDownItems, "classicToolsQuality", "Проверка и порядок строк", "Quality and line order",
                fixToolStripMenuItem, listErrorsToolStripMenuItem, netflixQualityCheckToolStripMenuItem,
                startNumberingFromToolStripMenuItem, toolStripMenuItemSortBy);
            NavigationGroup(toolsToolStripMenuItem.DropDownItems, "classicToolsAss", "ASS, актёры и стили", "ASS, actors and styles",
                _cuePickerRoot, toolStripMenuItemConvertActors, convertColorsToDialogToolStripMenuItem,
                _classicEffects, _classicDraw, _classicAssFilter, _classicFontCollector);
            NavigationGroup(toolsToolStripMenuItem.DropDownItems, "classicToolsAi", "AI-помощники", "AI assistants",
                FindClassicMenu(menuStrip1.Items, "classicAiReview"), FindClassicMenu(menuStrip1.Items, "classicAiAssistant"), FindClassicMenu(menuStrip1.Items, "classicLlamaModels"));
            NavigationGroup(toolsToolStripMenuItem.DropDownItems, "classicToolsFiles", "Работа с файлами", "Subtitle files",
                toolStripMenuItemMakeEmptyFromCurrent, splitToolStripMenuItem, appendTextVisuallyToolStripMenuItem,
                joinSubtitlesToolStripMenuItem, toolStripMenuItemBatchConvert, generateDatetimeInfoFromVideoToolStripMenuItem,
                toolStripMenuItemMeasurementConverter);

            NavigationGroup(editToolStripMenuItem.DropDownItems, "classicCueMarks", "Метки реплик", "Cue marks", _classicForcedToggle);
            NavigationGroup(toolStripMenuItemVideo.DropDownItems, "classicVideoSources", "Источники и аудиодорожки", "Sources and audio tracks",
                toolStripMenuItemOpenVideoFromUrl, toolStripMenuItemOpenDvd, toolStripMenuItemSetAudioTrack, openSecondSubtitleToolStripMenuItem);
            NavigationGroup(toolStripMenuItemVideo.DropDownItems, "classicVideoRecognition", "Распознавание речи и OCR", "Speech recognition and OCR",
                audioToTextWhisperTolStripMenuItem, videoaudioToTextToolStripMenuItem, _classicVideoOcrMenu);
            NavigationGroup(toolStripMenuItemVideo.DropDownItems, "classicVideoVoice", "Озвучка и музыка", "Speech and music",
                textToSpeechAndAddToVideoToolStripMenuItem, FindClassicMenu(menuStrip1.Items, "classicBackgroundMusic"));
            NavigationGroup(toolStripMenuItemVideo.DropDownItems, "classicVideoExport", "Создать и экспортировать видео", "Create and export video",
                generateVideoWithHardcodedSubtitleToolStripMenuItem, generateVideoWithSoftcodedSubtitlesToolStripMenuItem,
                generaeTransparentVideoWithSubtitleToolStripMenuItem, generateBlankVideoToolStripMenuItem, _classicVideoTools);
            NavigationGroup(toolStripMenuItemVideo.DropDownItems, "classicVideoChapters", "Главы и смены плана", "Chapters and shot changes",
                toolStripMenuItemImportChapters, toolStripMenuItemImportShotChanges, toolStripMenuItemListShotChanges);
            NavigationGroup(toolStripMenuItemVideo.DropDownItems, "classicVideoPlayback", "Воспроизведение и панели", "Playback and panels",
                setVideoOffsetToolStripMenuItem, smpteTimeModedropFrameToolStripMenuItem, toolStripMenuItemAddWaveformBatch,
                FindClassicMenu(menuStrip1.Items, "classicFfmpegPlayer"), FindClassicMenu(menuStrip1.Items, "classicFfmpegAudio"),
                FindClassicMenu(menuStrip1.Items, "classicFfmpegHardware"), FindClassicMenu(menuStrip1.Items, "classicTrackTimeline"),
                undockVideoControlsToolStripMenuItem, redockVideoControlsToolStripMenuItem);

            var grid = NavigationGroup(optionsToolStripMenuItem.DropDownItems, "classicGridSettings", "Список субтитров", "Subtitle list", _classicColumnsMenu);
            if (_classicQuickColumns == null)
            {
                _classicQuickColumns = new ToolStripMenuItem { Name = "classicQuickColumns" };
                _classicQuickColumns.DropDownOpening += (s, e) => PopulateClassicQuickColumns(_classicQuickColumns.DropDownItems, true);
                grid.DropDownItems.Add(_classicQuickColumns);
                PopulateClassicQuickColumns(_classicQuickColumns.DropDownItems, true);
            }
            _classicQuickColumns.Text = ClassicAssistedText.L("Показать столбцы", "Show columns");
            if (!grid.DropDownItems.Contains(_classicGridAppearanceMenu)) grid.DropDownItems.Add(_classicGridAppearanceMenu);
            var fonts = NavigationAction(grid.DropDownItems, "classicGridFontSettings", "Шрифт и цвета интерфейса…", "Interface font and colors…", () => ShowSettings(settings => settings.SelectNavigationSection("appearance")));
            var highlights = NavigationAction(grid.DropDownItems, "classicGridHighlightSettings", "Подсветка ошибок…", "Error highlighting…", () => ShowSettings(settings => settings.SelectNavigationSection("syntax")));
            var spelling = NavigationGroup(toolStripMenuItemSpellCheckMain.DropDownItems, "classicGridSpellingGroup", "Орфография в таблице", "Grid spelling",
                _gridSpellToggle, _gridSpellSettings);
            ReplaceNavigationAlias(grid.DropDownItems, spelling, "classicGridSpellingSettingsLink");
            var keyboard = NavigationGroup(optionsToolStripMenuItem.DropDownItems, "classicKeyboardSettings", "Клавиатура и команды", "Keyboard and commands",
                FindClassicMenu(menuStrip1.Items, "classicShortcutSteps"));
            NavigationAction(keyboard.DropDownItems, "classicShortcutSettings", "Горячие клавиши Classic…", "Classic shortcuts…", () => ShowSettings(settings => settings.SelectNavigationSection("shortcuts")));
            NavigationAction(keyboard.DropDownItems, "classicPaletteKeySettings", "Клавиши палитры команд…", "Command palette shortcut…", ConfigureClassicPaletteShortcut);
            ReplaceNavigationAlias(keyboard.DropDownItems, FindClassicMenu(menuStrip1.Items, "classicCuePickerKeys"), "classicCuePickerKeySettings", ClassicAssistedText.L("Клавиши выбора актёра и стиля…", "Actor and style picker shortcuts…"));
            var waveform = FindClassicMenu(menuStrip1.Items, "classicWaveformSettings");
            NavigationAction(waveform.DropDownItems, "classicWaveformMoreSettings", "Все настройки осциллограммы…", "All waveform settings…", () => ShowSettings(settings => settings.SelectNavigationSection("waveform")));

            // The old context Columns command remains the same instance.
            var contextGrid = NavigationGroup(contextMenuStripListView.Items, "classicGridSettingsContext", "Настройки списка", "Subtitle list settings", _classicColumnsContext);
            ReplaceNavigationAlias(contextGrid.DropDownItems, _classicQuickColumns, "classicQuickColumnsContext");
            ReplaceNavigationAlias(contextGrid.DropDownItems, _classicGridAppearanceMenu, "classicGridAppearanceContext");
            ReplaceNavigationAlias(contextGrid.DropDownItems, fonts, "classicGridFontSettingsContext");
            ReplaceNavigationAlias(contextGrid.DropDownItems, highlights, "classicGridHighlightSettingsContext");
            ReplaceNavigationAlias(contextGrid.DropDownItems, spelling, "classicGridSpellingContextLink");
            ReplaceNavigationAlias(contextMenuStripWaveform.Items, FindClassicMenu(menuStrip1.Items, "classicWaveformSettings"), "classicWaveformSettingsContext");
            NavigationGroup(contextMenuStripListView.Items, "classicContextSplit", "Разделить и объединить", "Split and merge",
                splitLineToolStripMenuItem, _assistedSplitContext, _assistedMoveContext, toolStripMenuItemMergeLines,
                toolStripMenuItemMergeDialog, mergeBeforeToolStripMenuItem, mergeAfterToolStripMenuItem);
            NavigationGroup(contextMenuStripListView.Items, "classicContextFormat", "Форматирование", "Formatting",
                boldToolStripMenuItem, italicToolStripMenuItem, underlineToolStripMenuItem, boxToolStripMenuItem,
                colorToolStripMenuItem, toolStripMenuItemFont, toolStripMenuItemAlignment,
                toolStripMenuItemSurroundWithMusicSymbols, removeFormattinglToolStripMenuItem);
            NavigationGroup(contextMenuStripListView.Items, "classicContextMetadata", "Стиль, актёр и метки", "Style, actor and marks",
                setStylesForSelectedLinesToolStripMenuItem, setActorForSelectedLinesToolStripMenuItem, toolStripMenuItemSetLayer,
                toolStripMenuItemAssaTools, toolStripMenuItemAssStyles, toolStripMenuItemSetRegion, toolStripMenuItemSetLanguage,
                toolStripMenuItemWebVttStyle, toolStripMenuItemWebVTT, toolStripMenuWebVttBrowserPreview,
                toolStripMenuItemBookmark, _classicForcedContext);

            if (!_classicNavigationEvents)
            {
                _classicNavigationEvents = true;
                contextMenuStripListView.Opening += (s, e) => { if (!e.Cancel) RefreshClassicNavigation(); };
                foreach (var root in new[] { toolsToolStripMenuItem, toolStripMenuItemVideo, optionsToolStripMenuItem, toolStripMenuItemSpellCheckMain })
                    root.DropDownOpening += (s, e) => RefreshClassicNavigation();
            }
            RefreshClassicNavigation();
            RefreshClassicNavigationLabels();
        }

        private void RefreshClassicNavigationLabels()
        {
            Action<string, string, string> label = (name, ru, en) =>
            {
                var item = FindClassicMenu(menuStrip1.Items, name);
                if (item != null) item.Text = ClassicAssistedText.L(ru, en);
            };
            label("classicAiReview", "AI-проверка субтитров…", "AI subtitle review…");
            label("classicAiAssistant", "AI-помощник текущей строки…", "Current-line AI assistant…");
            label("classicLlamaModels", "Локальные AI-модели…", "Local AI models…");
            label("classicBackgroundMusic", "Создать фоновую музыку…", "Generate background music…");
            label("classicFfmpegPlayer", "Плеер FFmpeg (экспериментальный)", "FFmpeg player (experimental)");
            label("classicFfmpegAudio", "Аудиодорожка FFmpeg", "FFmpeg audio track");
            label("classicFfmpegHardware", "FFmpeg: аппаратное декодирование", "FFmpeg hardware decoding");
            label("classicTrackTimeline", "Панель дорожек", "Track timeline");
            label("classicShortcutSteps", "Составные горячие клавиши…", "Custom shortcut steps…");
            label("classicWaveformSettings", "Осциллограмма", "Waveform");
            label("classicTimeDisplay", "Отображение времени", "Time code display");
            toolStripMenuItemImportOcrHardSub.Text = _classicVideoOcrMenu.Text;
            var time = FindClassicMenu(menuStrip1.Items, "classicTimeDisplay");
            time.DropDownItems[0].Text = ClassicAssistedText.L("Миллисекунды", "Milliseconds");
            time.DropDownItems[2].Text = ClassicAssistedText.L("Номера кадров", "Frame numbers");
            time.DropDownItems[4].Text = ClassicAssistedText.L("Хранить время на кадрах видео", "Store times on video frames");
            var wave = FindClassicMenu(menuStrip1.Items, "classicWaveformSettings");
            wave.DropDownItems[0].Text = ClassicAssistedText.L("Показывать только речь", "Show speech only");
            wave.DropDownItems[1].Text = ClassicAssistedText.L("Быстрый рендерер (экспериментальный)", "Fast renderer (experimental)");
            wave.DropDownItems[2].Text = ClassicAssistedText.L("Показывать метки смены плана", "Show shot-change markers");
            wave.DropDownItems[3].Text = ClassicAssistedText.L("Цвет сетки…", "Grid color…");
            _classicForcedToggle.Text = _classicForcedContext.Text = ClassicAssistedText.L("Отметить / снять Forced у выбранных реплик", "Toggle forced on selected cues");
            _classicForcedToggle.ToolTipText = _classicForcedContext.ToolTipText = ClassicAssistedText.L("Меняет отметку реплики. Видимость столбца и экспорт настраиваются отдельно.", "Changes cue metadata. Column visibility and export are separate commands.");
            // Recreate context links after translating their canonical sources.
            ReplaceNavigationAlias(contextMenuStripWaveform.Items, wave, "classicWaveformSettingsContext");
        }

        private void RefreshClassicNavigation()
        {
            foreach (var root in new[] { fileToolStripMenuItem, toolsToolStripMenuItem, toolStripMenuItemVideo, optionsToolStripMenuItem, toolStripMenuItemSpellCheckMain, editToolStripMenuItem })
                CleanNavigationSeparators(root.DropDownItems);
            CleanNavigationSeparators(contextMenuStripListView.Items);
            foreach (var group in _classicNavigationGroups.Values)
                group.Available = group.DropDownItems.OfType<ToolStripMenuItem>().Any(item => item.Available);
        }

        private static void CleanNavigationSeparators(ToolStripItemCollection items)
        {
            // Retain original separators as well as commands, but hide empty dividers.
            bool precedingCommand = false;
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripSeparator)
                {
                    item.Available = precedingCommand;
                    precedingCommand = false;
                }
                else if (item.Available) precedingCommand = true;
            }
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i] is ToolStripSeparator) items[i].Available = false;
                else if (items[i].Available) break;
            }
        }

        // Context entries forward to the actual command and read its current state.
        // They never save a second independent copy of a setting or shortcut.
        private ToolStripMenuItem NavigationAlias(ToolStripMenuItem source, string name, string caption = null)
        {
            var alias = new ToolStripMenuItem { Name = name };
            _classicNavigationAliases.Add(alias);
            alias.Disposed += (s, e) => _classicNavigationAliases.Remove(alias);
            Action sync = () =>
            {
                alias.Text = caption ?? source.Text; alias.Enabled = source.Enabled; alias.Available = source.Available;
                alias.Checked = source.Checked; alias.CheckState = source.CheckState;
                alias.ShortcutKeyDisplayString = source.ShortcutKeys == Keys.None ? source.ShortcutKeyDisplayString : new KeysConverter().ConvertToString(source.ShortcutKeys);
                alias.ToolTipText = source.ToolTipText;
            };
            if (source.HasDropDownItems)
            {
                alias.DropDownItems.Add(new ToolStripMenuItem());
                alias.DropDownOpening += (s, e) =>
                {
                    if (source == _classicQuickColumns) PopulateClassicQuickColumns(source.DropDownItems, true);
                    if (source == _classicGridAppearanceMenu) RefreshClassicGridAppearanceChecks();
                    DisposeNavigationItems(alias.DropDownItems);
                    foreach (var child in source.DropDownItems.OfType<ToolStripMenuItem>())
                        alias.DropDownItems.Add(NavigationAlias(child, name + "_" + child.Name));
                    sync();
                };
            }
            else alias.Click += (s, e) => { source.PerformClick(); sync(); };
            sync();
            return alias;
        }

        private void ReplaceNavigationAlias(ToolStripItemCollection owner, ToolStripMenuItem source, string name, string caption = null)
        {
            var old = owner[name];
            if (old != null) { owner.Remove(old); old.Dispose(); }
            owner.Add(NavigationAlias(source, name, caption));
        }

        private static void DisposeNavigationItems(ToolStripItemCollection items)
        {
            foreach (var item in items.Cast<ToolStripItem>().ToArray()) item.Dispose();
        }

        private void SetClassicColumnVisibility(C column, bool visible)
        {
            try
            {
                var preferences = CaptureClassicColumns();
                preferences.Single(p => p.Key == column.ToString()).Visible = visible;
                ApplyClassicColumns(preferences, true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { ShowStatus(ex.Message); }
        }

        private void PopulateClassicQuickColumns(ToolStripItemCollection items, bool canonical)
        {
            // Keep the original Forced-column command alive when refreshing this menu.
            if (items.Contains(_classicForcedColumn)) items.Remove(_classicForcedColumn);
            DisposeNavigationItems(items);
            var preferences = CaptureClassicColumns();
            foreach (var preference in preferences)
            {
                var column = (C)Enum.Parse(typeof(C), preference.Key);
                ToolStripMenuItem item;
                if (canonical && column == C.Forced) item = _classicForcedColumn;
                else
                {
                    item = new ToolStripMenuItem { Name = "classicColumn_" + column };
                    item.Click += (s, e) => SetClassicColumnVisibility(column, !CaptureClassicColumns().Single(p => p.Key == column.ToString()).Visible);
                }
                item.Text = ColumnLabel(column);
                item.Checked = preference.Visible;
                item.Enabled = ClassicColumnPreference.CanToggle(column);
                item.ToolTipText = ColumnNote(column);
                items.Add(item);
            }
        }

        private ContextMenuStrip CreateClassicHeaderMenu()
        {
            var menu = new ContextMenuStrip { ShowCheckMargin = true };
            UiUtil.FixFonts(menu);
            menu.Items.Add(new ToolStripMenuItem(LanguageSettings.Current.Main.Menu.ContextMenu.SizeAllColumnsToFit, null, (s, e) => SubtitleListview1.AutoSizeColumns()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(NavigationAlias(_classicColumnsMenu, "classicHeaderColumns"));
            var quick = new ToolStripMenuItem(ClassicAssistedText.L("Показать столбцы", "Show columns")) { Name = "classicHeaderQuickColumns" };
            PopulateClassicQuickColumns(quick.DropDownItems, false);
            menu.Items.Add(quick);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(NavigationAlias(_classicGridAppearanceMenu, "classicHeaderFormatting"));
            menu.Items.Add(NavigationAlias(FindClassicMenu(menuStrip1.Items, "classicGridFontSettings"), "classicHeaderFont"));
            menu.Items.Add(NavigationAlias(FindClassicMenu(menuStrip1.Items, "classicGridHighlightSettings"), "classicHeaderHighlight"));
            menu.Items.Add(NavigationAlias(_classicNavigationGroups["classicGridSpellingGroup"], "classicHeaderSpelling"));
            menu.Closed += (s, e) =>
            {
                if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)(() => menu.Dispose()));
                else menu.Dispose();
            };
            return menu;
        }
    }
}
