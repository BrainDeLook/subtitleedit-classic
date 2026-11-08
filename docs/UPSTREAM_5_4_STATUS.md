# Subtitle Edit 5.4 integration — local implementation status

Reference: [upstream v5.4.0](https://github.com/SubtitleEdit/subtitleedit/tree/v5.4.0), commit `57273b2`, released 6 November 2026. The stable summary and all twelve beta/four RC sections form the fixed scope; later main commits are excluded. Latest local package: **Classic 4.0.28.58**, unpublished.

## Result of the complete audit follow-up

The 7 November audit found **33 concrete omissions/partial implementations and seven bounded verification cases**. Their implementations and Classic-specific outcomes are now recorded in [CLASSIC_54_COMPLETION.md](CLASSIC_54_COMPLETION.md). The original [audit snapshot](UPSTREAM_5_4_AUDIT.md) is retained as the baseline, rather than rewritten to suggest the missing work was always present.

The full [inventory](UPSTREAM_5_4_AUDIT_ITEMS.json) contains **334 changelog bullets**, including repeated summary/beta/RC entries. It is not a count of distinct features or a claim that every possible runtime combination is certified.

| Inventory status | Entries | Meaning |
| --- | ---: | --- |
| `audit_finding_addressed` | 98 | Repeated release bullets associated with the 33 implemented findings; actual routes and checks are in the completion record. |
| `targeted_case_reviewed` | 13 | The seven bounded cases have a concrete check or an explicit Classic equivalent/platform limitation. |
| `implementation_recorded` | 150 | Existing parser, engine, timing, export and tool integrations retained from earlier work. These are not individually re-certified solely by a status label. |
| `classic_equivalent` | 29 | A retained Classic command/path supplies the functionality. |
| `classic_scope_exclusion` | 44 | Avalonia presentation or another-platform deployment outside the retained WinForms application. |

## Delivered functionality

| Area | Integration |
| --- | --- |
| Files and timing | Updated SubRip/TTML/SCC/MP4 handling, YTT styles, subtitle/audio-track synchronization, selected-track previews, same-time ASS stacks, complete frame-move scopes and offset-aware paste. |
| OCR | New splitter/matcher and replacement rules, exact Latin/Cyrillic base glyph data with user-training retention, VobSub colour isolation in GUI/batch/CLI, thumbnail zoom and a shared navigable image viewer. |
| ASS/export | 24 background styles, 18 progress bars, Draw multi-selection/groups/keyboard editing, attachment drops, paused-frame asynchronous positioning, ordinary style-aware ASS image exports, HDR SUP and text/image cinema MXF. |
| Translation and ASR | Atomic selected-context translation, search/replace, music-line preservation, language variants, reasoning recovery, remote-model refresh, normalized Ollama URLs, current engine catalogues, Whisper 1.9.5 help, DashScope serialization and forced-aligner setup/failure policy. |
| Editor and spelling | Optional microphone/player controls, error selection, bookmarks, regex rules, image colour picking, dictionary uninstall, text-column spelling, remembered spell-check placement, Unicode word-list navigation and favourite-first Save as. |
| Batch/CLI/player | Search/presets/plugins, a draggable native batch divider, correct target-FPS stamping without double retiming, RTL guards, CLI burn-in/OCR/cinema output, video-URL browser-cookie selection, nonblocking relative seeks and exact ADTS AAC duration in mpv. |
| Language and lifecycle | Existing Classic XML languages plus the pinned new-control adapter for 36 locales, fork-only stable updater, Windows power/taskbar scopes, local hash-verified backup/rollback package. |

Classic retains Windows Forms, its existing commands, menu groups and settings compatibility. All **325 named commands** in the retained menu baseline remain reachable. The updater continues to use this fork's stable releases.

## Toolbar follow-up — 4.0.28.58

Eight optional quick actions were added before the existing format picker: burned-in OCR, ASR, TTS, translation, speech-based timing, ASS Draw, hard-subtitle video and batch conversion. They forward to the original menu command objects. Existing toolbar controls/order are retained; the existing hard-subtitle button takes precedence over its new shortcut.

Visibility is remembered per command through the toolbar right-click menu. Localized tooltips, document/format availability, icon ownership and native overflow are covered by **19 focused checks**. The **37 navigation checks** also pass, retaining all 325 named commands. No service request or model download is needed for these tests.

## Validation of the integration baseline — 4.0.28.57

- **1,215 passed, 0 failed** in the combined regression run, including the 16 OCR/export and 34 translation/ASR/localization completion cases. Missing fixture files in the first test setup were restored before the successful run; they were not application failures.
- **68 ASS interaction/output checks**, **13 player checks**, **26 editor/batch/settings checks**, **17 native waveform/layout checks**, and **37 navigation checks** passed. Navigation also verifies all 325 retained named commands.
- Actual generated files include GUI MicroDVD output at the target frame rate, ordinary ASS image output and CLI VobSub-to-nOCR recognition. Native mpv checks use generated raw AAC and test exact duration/EOF/disposal.
- Native Main/waveform events cover duplicate selection, source replacement, read-only originals and ASS stacks. Russian layouts were inspected at default and 14 pt font sizes, including scrolling and accessible Save/Cancel controls.
- Rapid-seek checks use a backend that deliberately reports a stale paused position. Fullscreen checks cover the existing native window and actual key-routing handlers; they do not certify physical focus on every decoder/native surface.
- Clipboard offset metadata and external-text preservation were exercised in a separate run. Clipboard testing is opt-in in the reusable native fixture because the clipboard is shared with the desktop.
- Three isolated package scenarios cover install, forced rollback and path-traversal rejection, with settings retained. The distributed ZIP is separately extracted, hash-checked and installed into a fixture before use.

The final batch-window checks also exposed and corrected a native handle-creation issue: counting checkbox state now uses the action model while other rows are still receiving their native indices.

Earlier packages had separately passed 50 stable nOCR compatibility checks, 59 FFmpeg media/export checks, CLI MP4/WebM/failure handling and updater-helper success/failure fixtures. These historical results are distinguished from the new focused checks rather than counted twice.

## Practical limits

Paid services were not called. Large-model inference/downloads, real microphone capture, real browser-cookie extraction, physical DVD hardware and every GPU/player/DPI combination are not certified. The updater helper was exercised with fixture executables; this is distinct from running the production Inno Setup installer.

Localization adapts upstream strings rather than replacing Classic XML with Avalonia JSON. The adapter currently matches **601/1,316 literal sites (452/1,130 unique strings)** in 36 locales. Russian/English labels are preserved; unmatched fork-only explanations fall back to English in other locales. See [coverage and fallback policy](CLASSIC_54_LOCALIZATION.md).

Avalonia themes, SVG/theme icon packs, background art, control chrome/rendering settings and macOS/Linux deployment remain deliberate exclusions. Useful commands and output styles are not excluded merely because their upstream UI uses Avalonia.

## Build, package and manual verification

The SDK build compiles UI, core and CLI from source with the same revision, including resources/config redirects. All three current outputs are versioned **4.0.28.58**. Existing nullable/compatibility warnings remain; the build has no errors. See [build instructions](BUILD_CLASSIC.md).

The local package updates an existing Classic 4.0.28 installation. It includes runtime dependencies, checksums, license notices and a backup/rollback installer; it contains no models, generated media or test executables. Installation preserves Settings.xml, dictionaries and models. It is a PowerShell patch, not a fresh Inno Setup installation. **No GitHub release is published.**

The [consolidated Russian check guide](CLASSIC_54_CHECK.ru.md) explains where to find the additions and what to test after one installation.
