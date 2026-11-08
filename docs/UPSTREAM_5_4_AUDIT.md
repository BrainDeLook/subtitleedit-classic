# Subtitle Edit v5.4.0 parity audit — historical baseline

**Completion update, 8 November 2026:** the baseline findings below have been addressed in local **4.0.28.57**. Read the [33-item completion record and seven reviewed cases](CLASSIC_54_COMPLETION.md) for the current implementation, evidence and limitations. The tables below preserve what was missing at the time of the audit. The JSON inventory now includes both baseline and resolution statuses.

Audit date: **7 November 2026**. Upstream: [v5.4.0](https://github.com/SubtitleEdit/subtitleedit/releases/tag/v5.4.0), commit `57273b2`. Classic: `se4-legacy`, HEAD `238754413` plus the current uncommitted integration; installed local build **4.0.28.56**. Later upstream `main` commits are outside this audit.

**At this baseline, the 5.4.0 port was incomplete.** The previous status page overstated functional completion. Source presence, a new engine version, and broad regression results are not sufficient to close a feature when its data, GUI route or exact failure case is still old.

## Fixed scope and method

The entire pinned changelog through the end of beta1 was included: **334 bullet entries**, consisting of 38 release-summary entries and 296 beta/RC entries. These are not 334 distinct features: summaries and intermediate engine updates repeat. An earlier review stopped at line 397; beta1 continues through line 441, leaving **44 additional entries** out of that review.

This is a source and bundled-data audit. It follows UI callers, settings, CLI options and data files instead of treating matching class names as proof. The database comparison was rerun after normalizing V1/V2 glyph records. Paid services were not called; models were not downloaded; no application or installer was launched. Existing user settings and the installed build were not changed.

The complete bounded inventory is [UPSTREAM_5_4_AUDIT_ITEMS.json](UPSTREAM_5_4_AUDIT_ITEMS.json). Its status vocabulary deliberately distinguishes recorded implementation from a fresh per-entry runtime check:

| Status | Entries | Meaning |
| --- | ---: | --- |
| `gap_or_partial` | 98 | At least one concrete missing part; see F01-F33. Repeated entries can refer to the same finding. |
| `targeted_check_pending` | 13 | Specific remaining verification case; see V01-V07. Not asserted to be a reproduced bug. |
| `classic_scope_exclusion` | 44 | Avalonia/platform/presentation change outside the retained WinForms interface. |
| `classic_equivalent` | 29 | A retained Classic command/path supplies the equivalent functionality. |
| `implementation_recorded` | 150 | Covered by the existing integration record; this audit does not freshly certify every subcase. |

There is no overall “everything else passed” claim. In particular, entries marked `implementation_recorded` must not be used to advertise certified full parity.

## Baseline omissions and partial implementations

**33 findings**, grouped below. These are concrete work items, not newly discovered releases or an expanding upstream target. Changelog numbers refer to the pinned file.

### OCR

| ID / release entry | Remaining work and source evidence |
| --- | --- |
| **F01** · missing data<br>Lines 43, 366, 428 | **Ship the updated Latin and Cyrillic nOCR databases.** The matcher was ported but the data was not. Classic Latin has 606 glyphs versus 2,287 upstream; Cyrillic has 1,621 versus 1,631. All 52 new Latin and 10 new Cyrillic signatures relative to v5.3.0 are absent. Canonical geometry comparison handles the V1/V2 storage difference.<br>Classic: [Ocr/Latin.nocr](../Ocr/Latin.nocr); [Ocr/Cyrillic.nocr](../Ocr/Cyrillic.nocr).<br>Upstream location: `Ocr/Latin.nocr; Ocr/Cyrillic.nocr`. |
| **F02** · missing<br>Lines 438 | **Apply VobSub colour isolation in batch/CLI OCR.** The current routes pass the raw VobSub bitmap into recognition. No equivalent of the upstream VobSubColorIsolation/BinarizeForOcr step was found for nOCR and BinaryOCR.<br>Classic: [src/seconv/Program.cs:212](../src/seconv/Program.cs#L212); [src/ui/Forms/Ocr/VobSubOcr.cs:1698](../src/ui/Forms/Ocr/VobSubOcr.cs#L1698).<br>Upstream location: `src/seconv/Core/ImageOcrLoader.cs`. |
| **F03** · partial<br>Lines 416 | **Add OCR thumbnail zoom shortcuts.** The OCR image uses automatic PictureBox Zoom. Ctrl+wheel and Ctrl+0 are not wired. Progress reporting already exists and is not counted as missing.<br>Classic: [src/ui/Forms/Ocr/VobSubOcr.Designer.cs:2198](../src/ui/Forms/Ocr/VobSubOcr.Designer.cs#L2198); [src/ui/Forms/Ocr/VobSubOcr.cs](../src/ui/Forms/Ocr/VobSubOcr.cs).<br>Upstream location: `src/ui/Features/Ocr/OcrWindow.cs`. |
| **F04** · missing<br>Lines 417 | **Add the shared image viewer with in-place previous/next navigation.** Classic has image previews, but no equivalent of the shared navigable ShowImage window used by upstream OCR and image comparison. Existing preview controls are not the new viewer.<br>Classic: [src/ui/Forms/Ocr/VobSubOcr.cs](../src/ui/Forms/Ocr/VobSubOcr.cs); [src/ui/Forms/Assa/Attachments.cs:241](../src/ui/Forms/Assa/Attachments.cs#L241).<br>Upstream location: `src/ui/Features/Shared/ShowImage/ShowImageViewModel.cs:25`. |

### ASS/export

| ID / release entry | Remaining work and source evidence |
| --- | --- |
| **F05** · partial<br>Lines 47, 74, 75, 76 | **Connect ordinary GUI image exports to the new ASS style renderer.** ClassicImageJob uses the new renderer, but ordinary ExportPngXml still builds the old limited parameters and strips underline. The normal SUP/VobSub/BDN route therefore does not have the same style/tag coverage.<br>Classic: [src/ui/Forms/ExportPngXml.cs:540](../src/ui/Forms/ExportPngXml.cs#L540); [src/ui/Forms/ExportPngXml.cs:3184](../src/ui/Forms/ExportPngXml.cs#L3184); [src/ui/Logic/ClassicImageExport/ClassicImageJob.cs](../src/ui/Logic/ClassicImageExport/ClassicImageJob.cs).<br>Upstream location: `src/libuilogic/Export/ImageRenderer.cs`. |
| **F06** · partial<br>Lines 55, 420 | **Port the new Set background border styles.** The Classic picker still offers five styles. Upstream offers 24 total; the release explicitly introduces twelve new borders. This is functional drawing output, not merely an Avalonia theme change.<br>Classic: [src/ui/Forms/Assa/AssSetBackground.cs:81](../src/ui/Forms/Assa/AssSetBackground.cs#L81).<br>Upstream location: `src/ui/Features/Assa/AssaSetBackground/AssSetBackgroundViewModel.cs`. |
| **F07** · partial<br>Lines 420 | **Port the sixteen new progress-bar styles.** Classic offers Square and Rounded only; upstream offers eighteen styles in total.<br>Classic: [src/ui/Forms/Assa/AssaProgressBar.cs:75](../src/ui/Forms/Assa/AssaProgressBar.cs#L75).<br>Upstream location: `src/ui/Features/Assa/AssaProgressBar/ProgressBarStyles.cs`. |
| **F08** · partial<br>Lines 53, 419 | **Complete Draw selection and keyboard editing.** Groups, clip art and animation exist. The canvas still uses one active shape and only handles Ctrl+Z/Y at form level; rubber-band selection, Ctrl+click multi-selection, point deletion and arrow nudging are missing.<br>Classic: [src/ui/Forms/Assa/ClassicAssaDraw.cs:61](../src/ui/Forms/Assa/ClassicAssaDraw.cs#L61); [src/ui/Forms/Assa/ClassicAssaDraw.cs:197](../src/ui/Forms/Assa/ClassicAssaDraw.cs#L197).<br>Upstream location: `src/ui/Features/Assa/AssaDraw/AssaDrawViewModel.cs`. |
| **F09** · partial<br>Lines 55, 420 | **Accept file drops in ASS attachments.** Font previews already exist. Attachments has no AllowDrop, DragEnter or DragDrop route.<br>Classic: [src/ui/Forms/Assa/Attachments.cs](../src/ui/Forms/Assa/Attachments.cs); [src/ui/Forms/Assa/Attachments.Designer.cs](../src/ui/Forms/Assa/Attachments.Designer.cs).<br>Upstream location: `src/ui/Features/Assa/AssaAttachmentsViewModel.cs`. |
| **F10** · partial<br>Lines 288, 312, 319, 420 | **Finish functional Set position improvements.** Existing click-to-place/rotation are retained, but the preview still opens a separate mpv and has a synchronous FFmpeg fallback. It does not capture the paused main-player frame, load it in the background, provide the selection-box toggle or persist the window size.<br>Classic: [src/ui/Forms/Assa/SetPosition.cs:292](../src/ui/Forms/Assa/SetPosition.cs#L292); [src/ui/Forms/Assa/SetPosition.cs:545](../src/ui/Forms/Assa/SetPosition.cs#L545).<br>Upstream location: `src/ui/Features/Assa/AssaSetPosition`. |

### Editor/player

| ID / release entry | Remaining work and source evidence |
| --- | --- |
| **F11** · missing<br>Lines 181 | **Add a font picker for the second subtitle.** There is no SecondarySubtitleFont setting or equivalent picker in the current second-subtitle route.<br>Classic: [src/ui/Forms/Main.cs:35483](../src/ui/Forms/Main.cs#L35483).<br>Upstream location: `src/ui/Features/Main`. |
| **F12** · missing<br>Lines 182, 205 | **Detect whether the video offset applies to the second subtitle.** LoadSecondSubtitle is called without the new offset-detection behaviour.<br>Classic: [src/ui/Forms/Main.cs:35483](../src/ui/Forms/Main.cs#L35483).<br>Upstream location: `src/ui/Features/Main`. |
| **F13** · missing fix<br>Lines 296, 316 | **Remove the copied video offset during column paste.** Copy adds CurrentVideoOffset; Paste special reads the serialized times and writes them unchanged. Ordinary paste and column paste must not be conflated.<br>Classic: [src/ui/Forms/Main.cs:21718](../src/ui/Forms/Main.cs#L21718); [src/ui/Forms/Main.cs:33114](../src/ui/Forms/Main.cs#L33114); [src/ui/Forms/Main.cs:33208](../src/ui/Forms/Main.cs#L33208).<br>Upstream location: `src/ui/Features/Main`. |
| **F14** · partial<br>Lines 29, 407 | **Add one-frame shortcuts for following/all lines.** The new shortcut handler only moves the selected indices. The selected-and-following and all-lines scopes are absent; moving start/end or shot-change marks is a different command.<br>Classic: [src/ui/Forms/Main.ClassicEditor54.cs:54](../src/ui/Forms/Main.ClassicEditor54.cs#L54); [src/ui/Logic/ClassicEditorPreferences.cs:21](../src/ui/Logic/ClassicEditorPreferences.cs#L21).<br>Upstream location: `src/ui/Features/Main/MainViewModel.cs:17178`. |
| **F28** · missing<br>Lines 403 | **Add browser cookies and the bot-check hint to Open from URL.** Updating yt-dlp alone does not provide this UI or the cookies-from-browser argument. Neither was found in current URL-opening code.<br>Classic: [src/ui/Forms/DownloadYouTubeDl.cs](../src/ui/Forms/DownloadYouTubeDl.cs); [src/ui/Forms/Main.cs:34726](../src/ui/Forms/Main.cs#L34726).<br>Upstream location: `src/ui/Features/Video/OpenFromUrl`. |

### Spelling/editing

| ID / release entry | Remaining work and source evidence |
| --- | --- |
| **F15** · missing fix<br>Lines 380 | **Keep live spell checking out of the read-only original column.** Both text and original columns pass the same GridSpelling.Check callback to formatted-cell drawing.<br>Classic: [src/ui/Controls/SubtitleListView.cs:492](../src/ui/Controls/SubtitleListView.cs#L492); [src/ui/Controls/SubtitleListView.cs:610](../src/ui/Controls/SubtitleListView.cs#L610).<br>Upstream location: `src/ui/Features/Main/Layout/InitListViewAndEditBox.cs`. |
| **F16** · missing fix<br>Lines 435 | **Remember the Spell check window position.** SpellCheck is a plain Form. Its closing handler only saves casing preference; neither the form nor UiUtil.PreInitialize restores/saves its location.<br>Classic: [src/ui/Forms/SpellCheck.cs:699](../src/ui/Forms/SpellCheck.cs#L699); [src/ui/Logic/UiUtil.cs:508](../src/ui/Logic/UiUtil.cs#L508).<br>Upstream location: `src/ui/Features/SpellCheck`. |
| **F25** · missing<br>Lines 410 | **Add Has errors to Modify selection.** The rule enum and picker end with Blank/Style/Actor and contain no error-rule selection.<br>Classic: [src/ui/Forms/ModifySelection.cs:25](../src/ui/Forms/ModifySelection.cs#L25); [src/ui/Forms/ModifySelection.cs:99](../src/ui/Forms/ModifySelection.cs#L99).<br>Upstream location: `src/ui/Features/Edit/ModifySelection/RuleType.cs:28`. |
| **F26** · missing<br>Lines 412 | **Expose an image colour picker in the general colour dialog.** A separate ASS image picker exists, but ColorChooser does not call it; its picker button only chooses a palette colour.<br>Classic: [src/ui/Forms/ColorChooser.cs](../src/ui/Forms/ColorChooser.cs); [src/ui/Forms/Assa/ImageColorPicker.cs](../src/ui/Forms/Assa/ImageColorPicker.cs).<br>Upstream location: `src/ui/Features/Shared/ColorPicker`. |
| **F27** · missing<br>Lines 413 | **Add dictionary uninstall.** GetDictionaries offers download/download-all only; there is no uninstall control or dictionary-removal route.<br>Classic: [src/ui/Forms/GetDictionaries.cs](../src/ui/Forms/GetDictionaries.cs); [src/ui/Forms/GetDictionaries.Designer.cs](../src/ui/Forms/GetDictionaries.Designer.cs).<br>Upstream location: `src/ui/Features/SpellCheck/GetDictionaries`. |
| **F32** · missing<br>Lines 142 | **List favourite formats first in Save as.** SetSaveDialogFilter enumerates AllSubtitleFormats in its normal order and never reads FavoriteSubtitleFormats. Favourites in the main format picker are a separate path.<br>Classic: [src/ui/Logic/UiUtil.cs:597](../src/ui/Logic/UiUtil.cs#L597); [src/ui/Forms/Main.cs:5094](../src/ui/Forms/Main.cs#L5094).<br>Upstream location: `src/ui/Features/Main/MainViewModel.cs:15687`. |

### Translation

| ID / release entry | Remaining work and source evidence |
| --- | --- |
| **F17** · missing<br>Lines 113 | **Skip the supported music/lyric layouts during translation.** The keep-music-lines setting and IsMusicLine filtering are absent from current translation paths.<br>Classic: [src/ui/Forms/Translate/AutoTranslate.cs](../src/ui/Forms/Translate/AutoTranslate.cs); [src/ui/Logic/ClassicNewTranslator.Advanced.cs](../src/ui/Logic/ClassicNewTranslator.Advanced.cs).<br>Upstream location: `src/libuilogic/Translate/MergeAndSplitHelper.cs`. |
| **F18** · missing fix<br>Lines 148 | **Preserve language variants when switching provider.** Provider changes rebuild the language selection; storing TwoLetterIsoLanguageName before Code loses variants such as pt-PT.<br>Classic: [src/ui/Forms/Translate/AutoTranslate.cs:693](../src/ui/Forms/Translate/AutoTranslate.cs#L693); [src/ui/Forms/Translate/AutoTranslate.cs:1012](../src/ui/Forms/Translate/AutoTranslate.cs#L1012); [src/ui/Forms/Translate/AutoTranslate.cs:1489](../src/ui/Forms/Translate/AutoTranslate.cs#L1489).<br>Upstream location: `src/ui/Features/Translate`. |
| **F19** · missing fix<br>Lines 427 | **Recover from reasoning-only llama responses by disabling thinking.** ClassicAiClient reads message.content only and throws on empty text. There is no adaptive enable_thinking=false retry. Model-specific launch presets do not implement this recovery.<br>Classic: [src/ui/Logic/ClassicAiReview.cs:44](../src/ui/Logic/ClassicAiReview.cs#L44); [src/ui/Logic/ClassicNewTranslator.Advanced.cs:64](../src/ui/Logic/ClassicNewTranslator.Advanced.cs#L64).<br>Upstream location: `src/ui/Features/Translate/LlamaCppAdvanced/LlamaCppAdvancedClient.cs`. |
| **F20** · partial<br>Lines 427 | **Refresh remote model lists for advanced translators.** The new advanced translator settings use a model text field and do not refresh API model lists. Ordinary Ollama listing and the local model manager already exist; those are not counted as absent.<br>Classic: [src/ui/Forms/ClassicServiceSettingsForm.cs:64](../src/ui/Forms/ClassicServiceSettingsForm.cs#L64); [src/ui/Forms/Translate/AutoTranslate.cs:447](../src/ui/Forms/Translate/AutoTranslate.cs#L447).<br>Upstream location: `src/ui/Features/Translate`. |

### Batch/CLI

| ID / release entry | Remaining work and source evidence |
| --- | --- |
| **F21** · missing fix<br>Lines 320 | **Save batch output using the selected target frame rate.** Batch cue times are changed, but BatchConvertSave receives null for targetFrameRate. The save path can keep the original frame rate.<br>Classic: [src/ui/Forms/BatchConvert.cs:2520](../src/ui/Forms/BatchConvert.cs#L2520); [src/ui/Forms/BatchConvert.cs:3136](../src/ui/Forms/BatchConvert.cs#L3136).<br>Upstream location: `src/ui/Features/Tools/BatchConvert`. |
| **F22** · missing fix<br>Lines 370 | **Skip RTL conversion for subtitles with no RTL letters.** The batch RTL operations run without a right-to-left-text guard. The new safety check must cover batch and its legacy command-line route.<br>Classic: [src/ui/Forms/BatchConvert.cs:2939](../src/ui/Forms/BatchConvert.cs#L2939); [src/ui/Forms/BatchConvert.cs:3033](../src/ui/Forms/BatchConvert.cs#L3033).<br>Upstream location: `src/ui/Features/Tools/BatchConvert`. |
| **F23** · missing<br>Lines 365 | **Add a resizable files/functions divider in Batch convert.** Search and presets are present; the window has no SplitContainer/Splitter between the two areas.<br>Classic: [src/ui/Forms/BatchConvert.cs](../src/ui/Forms/BatchConvert.cs); [src/ui/Forms/BatchConvert.Designer.cs](../src/ui/Forms/BatchConvert.Designer.cs).<br>Upstream location: `src/ui/Features/Tools/BatchConvert`. |
| **F24** · partial<br>Lines 172 | **Support the documented --ollama-url option and bare host:port.** The new CLI exposes generic --api-url but no --ollama-url alias. ChatUrl also does not normalize a missing URL scheme. A generic URL field is not exact CLI compatibility.<br>Classic: [src/seconv/Program.cs:28](../src/seconv/Program.cs#L28); [src/ui/Logic/ClassicAiReview.cs:43](../src/ui/Logic/ClassicAiReview.cs#L43).<br>Upstream location: `src/seconv`. |

### ASR/runtime

| ID / release entry | Remaining work and source evidence |
| --- | --- |
| **F29** · missing help update<br>Lines 83 | **Update Whisper.cpp advanced-parameter help to 1.9.5.** The executable catalogue says 1.9.5 but the actual WhisperAdvanced resource shown by the dialog still begins Whisper CPP 1.8.2.<br>Classic: [src/ui/Forms/AudioToText/WhisperAdvanced.resx:120](../src/ui/Forms/AudioToText/WhisperAdvanced.resx#L120); [src/ui/Forms/AudioToText/WhisperAdvanced.Designer.cs:166](../src/ui/Forms/AudioToText/WhisperAdvanced.Designer.cs#L166).<br>Upstream location: `src/ui/Assets/SpeechToText`. |
| **F30** · missing fix<br>Lines 383 | **Port the DashScope OSS multipart formatting fix.** The multipart body is still emitted with .NET defaults. Upstream explicitly removes boundary quotes, quotes field/file names, clears filename* and orders headers. An in-memory serialization check under Windows PowerShell reproduced the quoted boundary, unquoted names and filename* in the Classic request; no paid request was made.<br>Classic: [src/ui/Logic/SpeechToText/ClassicCloudAsr.cs:109](../src/ui/Logic/SpeechToText/ClassicCloudAsr.cs#L109).<br>Upstream location: `src/ui/Features/Video/SpeechToText/DashScope/DashScopeSttService.cs:136`. |
| **F31** · partial<br>Lines 93, 289 | **Finish forced-aligner preflight and early crash termination.** The retimer catches failures and continues through every batch; upstream stops after three engine failures before any success. Current model setup is EnsureRuntime/EnsureModel rather than the consolidated missing-model flow. Error reporting exists, but the release behaviour is not complete.<br>Classic: [src/ui/Logic/ClassicTiming/SubtitleRetimer.cs:238](../src/ui/Logic/ClassicTiming/SubtitleRetimer.cs#L238); [src/ui/Logic/ClassicTiming/ClassicRetimingJob.cs:30](../src/ui/Logic/ClassicTiming/ClassicRetimingJob.cs#L30).<br>Upstream location: `src/ui/Features/Tools/ImproveTimeCodes/SubtitleRetimer.cs:56`. |

### Localization

| ID / release entry | Remaining work and source evidence |
| --- | --- |
| **F33** · partial<br>Lines 64, 84, 102, 103, 104, 105, 117, 125, 126, 127, 128, 129, 143, 149, 150, 151, 152, 173, 174, 191, 192, 193, 194, 212, 213, 214, 215, 229, 230, 231, 232, 233, 234, 245, 260, 261, 262, 272, 273, 274, 302, 303, 304, 305, 327, 328, 329, 330, 348, 349, 350, 384, 385, 439, 440, 441 | **Adapt translations for the new Classic commands.** Classic XML translations remain available, but new fork dialogs generally choose Russian or English directly. The upstream all-language string sync, including its Romanian/Croatian drafts, has not been adapted to the new Classic controls. Avalonia files cannot simply replace Classic XML.<br>Classic: [src/ui/Forms/AudioToText/ClassicSpeechSettings.cs](../src/ui/Forms/AudioToText/ClassicSpeechSettings.cs); [LanguageBaseEnglish.xml](../LanguageBaseEnglish.xml).<br>Upstream location: `src/ui/Logic/Config/Language`. |

## Baseline targeted checks

These seven bounded checks are not reported as proven regressions. They are part of the same fixed release audit and should not later be presented as new feature discoveries.

| ID / changelog lines | Case and reason |
| --- | --- |
| **V01** · 188, 433 | **Relative video steps across all player backends.** Classic has a paused-position cache in LibMpvDynamic, which may already cover mpv. Main.MoveVideoSeconds lacks upstream relative-target tracking. Do not label the user-visible bug reproduced until quick repeated steps and play/pause are checked on each supported backend. |
| **V02** · 268 | **Raw AAC duration in the mpv player.** The player implementations differ. Updated native libraries do not by themselves prove the AAC-duration correction at the Classic player boundary. |
| **V03** · 164, 189, 253, 377 | **Waveform selection with duplicates and a read-only original.** Classic has different waveform/timeline event paths. Verify duplicate selection, source refresh and that dragging translation never retimes the read-only original. |
| **V04** · 378, 379 | **Settings import restores player-follow, layout sizes and waveform zoom.** The integration report records navigation coverage, but that does not establish these exact import-and-restore cases. |
| **V05** · 432 | **Fullscreen shortcuts/focus and menu-bar PageUp.** The native overlay has lifecycle checks. The exact upstream keyboard bug cases still need a focused check rather than inferring parity from opening fullscreen. |
| **V06** · 99, 430 | **Long waveform-toolbar labels and short-screen scrolling.** Classic layout differs from Avalonia. These need concrete narrow-screen/high-DPI checks, not a copy of the Avalonia layout code. |
| **V07** · 294 | **Rule profile is not applied at startup unless selected.** Rule-profile support is present. The exact startup preference scenario is not individually closed by this source audit. |

## Existing equivalents and confirmed implemented details

Do not add duplicate features for these release entries:

- Copy-paste translation already has a reusable Copy source text button (`TranslateBlock`), equivalent to Re-copy.
- Multiple replace already routes through `RegexUtils.ReplaceNewLineSafe` and supports `\u`, `\l`, `\U`, `\L`, `\E` replacement escapes.
- OCR Delete/context-menu confirmation, source/translation columns, editable Multiple replace rules, Add rules, attached-font preview, plain-text batch settings, plugin search and spreadsheet column selection already exist.
- VibeVoice Q8_0/Q4_K SHA-256 values match the pinned 5.4.0 catalogue; MADLAD already exposes CPU/Vulkan/CUDA variants. These must not be listed as missing engines.
- CLI exact filenames containing commas are handled before wildcard splitting. The legacy CLI retains its MP4-to-text fallback; different routing must not be mistaken for an absent fallback.
- The integration record documents parser/fuzz fixes, subtitle/audio sync, native fullscreen controls, selected contextual translation, image rendering infrastructure, HDR SUP, MXF, YTT, OCR matcher/regex/ranges, advanced effects, batch plugins, microphone, custom rules, bookmarks and the fork-only updater. Findings above identify where that record is incomplete; they do not mean these whole subsystems are absent.

## Deliberate Classic boundary

Avalonia themes, SVG/theme icon packs, theme background art, shared Avalonia field/window chrome, Avalonia rendering-mode settings, macOS/Linux deployment and their platform-only fixes are excluded. Ordinary equivalent WinForms behaviour is retained. These exclusions do **not** cover useful new commands, border/bar output styles, accessible native interaction or updated OCR data.

The updater remains pointed at this fork’s stable releases, as explicitly requested. Upstream beta-channel switching is not a missing requirement for that updater. Upstream localization data needs adaptation rather than direct replacement of Classic XML; remaining localization is recorded as F33, not hidden as a framework exclusion.

## Completion rule

Close each F item with its actual caller/data/settings route and a relevant check. Consolidate testing by subsystem; do not ask the user to validate one tiny control at a time. Close V cases with a concrete result or a documented Classic equivalent. Keep paid calls, large-model inference and physical-DVD validation separate from whether the implementation exists. A future full-parity statement requires the recorded entries to be individually accounted for, not just a higher test count.

Only audit documentation was changed by that baseline audit pass; the subsequent functional update is recorded separately. No functional patch, new package, installation or GitHub publication was performed.
