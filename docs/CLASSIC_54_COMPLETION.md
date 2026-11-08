# Local 4.0.28.57 — completion of the fixed 5.4.0 audit

Reference: upstream **v5.4.0**, commit `57273b2`. Changes after that tag are outside this update. This is an unpublished local update for the existing Windows Forms Classic application.

The 7 November audit identified **33 implementation findings and seven verification cases**. The table below records the resulting implementation, rather than treating an engine version or a matching class name as proof of completion. The original findings remain available in [the audit snapshot](UPSTREAM_5_4_AUDIT.md). The inventory retains all 334 changelog bullets, including repeated summaries and intermediate versions; it is not a claim of 334 distinct features or individual runtime certifications.

## Implemented audit findings

| Finding | Delivered behavior and owning route | Verification |
| --- | --- | --- |
| F01 | Pinned Latin 2,287 and Cyrillic 1,631 nOCR glyphs are shipped and embedded. Loading the standard database merges the new base in memory while retaining user training and the original user file. | OCR delivery suite: exact data, old-base upgrade and trained-glyph retention. |
| F02 | VobSub colour isolation and alpha/canvas handling reach normal OCR, batch and `seconv`. GUI preference and CLI opt-out are available. | Palette/alpha fixtures and the actual GUI bitmap-preparation route. |
| F03 | OCR thumbnail Ctrl+wheel zoom and Ctrl+0 reset. | Native control event checks. |
| F04 | Shared image viewer can navigate previous/next without reopening; OCR, image exports and BinaryOCR/nOCR inspectors use it. | Viewer navigation and focused-button keyboard checks. |
| F05 | Ordinary ASS GUI image export uses the new style renderer; simple rendering and the existing SRT/HTML controls remain available. | Actual ExportPngXml preparation/rendering and positioning fixtures. |
| F06 | All 24 background-border styles are available, retaining the existing five. | ASS style generation and original-control checks. |
| F07 | All 18 progress-bar styles are available. | ASS generation and style-list checks. |
| F08 | ASS Draw supports rubber-band selection, Ctrl+click multi-selection, group movement, point deletion and arrow nudging with Undo. | Drawing interaction fixtures. |
| F09 | Dropped attachment files use the same import route as the existing button. | Attachment drop and retained button checks. |
| F10 | Set position captures the paused player frame, renders the subtitle asynchronously over it, offers selection-box visibility and remembers window placement. It does not open another mpv player. | Frame ownership, asynchronous preview, cancellation, geometry and persistence checks. |
| F11 | Second subtitles score whether the video offset applies, using a copy of the source. | Zero-based/broadcast timing and source-preservation checks. |
| F12 | An independent second-subtitle font is stored in additional editor settings and applied to native ASS output or the Classic overlay. | Saved preference and generated ASS header checks. |
| F13 | Copied cue data records its video offset; ordinary and column paste remove that recorded offset even after the current video offset changes. External clipboard text is unchanged. | Actual clipboard metadata and changed-offset checks. |
| F14 | Frame-move bindings cover selected lines, selected-and-following lines and all lines, in both directions. Selection and read-only source timing are retained. | Configured key parsing and actual command execution with multiple selected rows. |
| F15 | Grid spelling is drawn only in the text column. | Owning column check in SubtitleListView; existing formatted text rendering retained. |
| F16 | Spell-check window size/location survives reopening and is constrained to available monitors. | Placement persistence fixture. |
| F17 | Optional music/lyric preservation reaches ordinary, selected, advanced, batch and CLI translation. Mixed speech/music text is not discarded. | Translation classifier and provider-call fixtures. |
| F18 | Provider changes preserve exact language variants before falling back to a base language. | Language-switch fixtures including Portuguese variants. |
| F19 | Reasoning-only llama replies retry with thinking disabled; unsupported request fields have a bounded fallback. | Offline HTTP response/request fixtures. |
| F20 | Advanced translator settings can refresh authenticated remote model lists. | Offline HTTP model-list and endpoint checks. |
| F21 | Ordinary batch save receives the selected target frame rate. | Actual MicroDVD output from the BatchConvert completion route. |
| F22 | Batch and legacy CLI RTL operations skip cues with no RTL letters. | Non-RTL text preservation fixture. |
| F23 | Batch input files and output/functions areas have a native draggable divider. | Native control/layout check. |
| F24 | `--ollama-url` is supported; bare host/port and Ollama API endpoints normalize to the chat endpoint. Conflicting URL options are rejected. | CLI parsing and endpoint fixtures. |
| F25 | Modify selection exposes Has errors for SRT as well as styled formats. | Native rule-list check independent of actor/style support. |
| F26 | The existing colour dialog adds From image, retaining the original palette and colour fields. | Native colour-picker route and selected-colour synchronization. |
| F27 | Dictionary download management can uninstall selected installed dictionary files while preserving user word/name lists. A removed dictionary invalidates cached spelling availability. | Owning file-selection/removal route reviewed; no personal dictionary is removed by the tests. |
| F28 | Video URL opening offers a typed browser-cookie selector passed to mpv/yt-dlp before initialization. | Preference round trip and generated player option. No real browser cookies are read during tests. |
| F29 | The actual Whisper advanced dialog contains the 1.9.5 help resource. | Instantiated dialog/resource check. |
| F30 | DashScope OSS multipart requests use the pinned boundary, field and filename formatting. | Exact serialized multipart bytes; no paid request. |
| F31 | Forced alignment consolidates missing-runtime/model setup and stops after three native engine failures before any success. Malformed results remain a different failure class. | Preflight and failure-policy fixtures. |
| F32 | Save as places favourite formats first without changing the selected format. | Native SaveFileDialog filter checks. |
| F33 | Classic XML languages are retained; shared new-command localization adapts pinned strings for 36 locales, with explicit language aliases. Russian/English wording is preserved. | 11 localization cases, including every catalogue and null/default language. See the precise [coverage and fallback policy](CLASSIC_54_LOCALIZATION.md). |

Word-list navigation was also corrected within the same pinned release scope: type-ahead uses the actual Unicode character and one clock, and right-click Remove calls the existing removal command.

## Bounded verification cases

| Case | Result and practical boundary |
| --- | --- |
| V01 | Relative seeks accumulate the requested target while a paused backend reports a stale position. Manual seeks/play/pause/stop reset it. Small-sample playback uses a timer instead of blocking the UI or stopping at zero. The shared container route is exercised with a delayed backend; this is not certification of every installed decoder/GPU combination. |
| V02 | Raw ADTS AAC now has an exact frame-duration reader connected to mpv duration/end bounds. Generated AAC and multi-raw-block frames are covered. |
| V03 | Native Main/waveform fixtures cover selected duplicates, replacement of source objects, read-only source drags and ASS same-time stacks. |
| V04 | Classic loads Settings.xml before constructing its controls and has no Avalonia live settings-import path. Follow-selection and layout values survive its XML load/save route. Waveform zoom is a session control in Classic; the upstream live-import reset path is therefore not present. This does not add a new settings-import window. |
| V05 | Classic uses its existing undocked native window with key preview and key forwarding. The Avalonia menu-bar PageUp handler does not exist here. The native full-screen route and previous/next shortcut forwarding are checked separately. |
| V06 | Classic has no Avalonia waveform-toolbar-items dialog. Its equivalent options use a scrollable native layout with accessible footer controls. Long Russian labels wrap at the minimum window size, including a 14 pt UI font. |
| V07 | Loading a settings file containing an unselected rule profile does not apply that profile to the active editor rules. |

## Validation boundaries

Focused suites cover the owning data, HTTP serialization, command execution, native forms, generated files and player boundaries. The test logs and final package checks are recorded in [the implementation status](UPSTREAM_5_4_STATUS.md). Tests use isolated data directories rather than the installed settings.

No paid service calls, large-model downloads/inference, real browser-cookie extraction, microphone recording or physical DVD tests are required or claimed. The localized adapter currently matches 601 of 1,316 literal call sites (452 of 1,130 unique strings); unmatched fork-specific explanations use English outside Russian/English. This is documented rather than described as complete translation of every fork dialog.

The updater continues to use this fork's stable releases. Avalonia presentation/themes/icons and other-platform deployment remain deliberate exclusions. No GitHub release is made by this local update.
