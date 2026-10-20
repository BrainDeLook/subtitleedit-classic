# 5.3 integration in Classic 4.0.28

All nine consolidated source-audit findings have implementation routes in Classic 4.0.28. This is not a certification that every upstream model, platform or workflow behaves identically. This report describes the implementations and validation shipped in 4.0.28. No paid API calls were made.

Baseline: 4.0.27, `d7723b1538b1dab71286d44a1cbe9466e75e63e4`. Reference: the pinned v5.3.0 source snapshot audited on 2026-10-20. Release application version: 4.0.28; developed and locally installed through 4.0.27.103.

| Finding | Classic implementation | Verification / limits |
|---|---|---|
| G01 | Eight named, scoped command/insert/replace sequences; keyboard capture, AltGr handling before menu dispatch; plugin commands in sequences | Sequence ordering, one undo record, AltGr matching and JSON persistence tested. Existing per-plugin assignments are retained under Settings / Shortcuts; they were already implemented, contrary to the original audit wording. Plugin command IDs now use stable names. |
| G02 | Separate speech-only peak cache and toggle; selected audio track extraction; cancel on source/track changes or shutdown; optional batched waveform renderer and grid color | Cached speech/normal waveform switching and rendering equivalence tested. A new full Roformer separation run is not part of this validation; it uses the existing verified separation adapter and downloads its model on first use. |
| G03 | Absolute frame display/input in native controls and settings; exact fractional frame clock; SMPTE/media-time adapters | Positive/negative frame round trips at five frame rates, native controls, settings persistence and media-clock copy/round-trip tested. SMPTE remains the existing per-video setting. |
| G04 | Named presets, multiple translation targets, format/extension/error/forced/language/name filters, ASS property edits and embedded/subset fonts, per-file OCR languages, retained output modification times | Real Convert-button tests write four outputs from two inputs/two languages through a mock provider; errors do not save partial translations. Native preset round trips, forced metadata, error status retention, style changes, real font attachments and timestamps tested. Existing all-teletext-page enumeration retained; no physical broadcast capture used. |
| G05 | Separate `seconv.exe`: JSON/help inventory, prompt overrides, modern translation batches and targets, image exports, Paddle/nOCR/Tesseract/compatible/CrispEmbed/Mistral/Lens OCR, nOCR optimization and legacy conversion bridge | Real ASS, IMSC, BDN, indexed BDN and DVD-SUP file generation, overwrite/source guards, multiple target files and actual loopback HTTP batching tested. Windows headless tool reuses Classic assemblies; it does not construct the editor, but still requires the distributed Windows/.NET runtime modules. Not a dependency-free cross-platform rewrite. |
| G06 | ACE-Step 1.5 prompt/presets/BPM/seed/backend, verified model download, upstream seam search, bounded-memory normalized/faded WAV rendering, preview/open existing music and track-selected audio/video mixing | Loop search, sample-equivalent streaming rendering, WAV output and actual FFmpeg mixing/track selection tested. No new 5.8 GiB ACE-Step model inference run was performed. Models are downloaded only on Generate. |
| G07 | Experimental FFmpeg video/audio backend, seek/pause/rate, presentation-timestamp stepping, hardware decode with software fallback; actor/layer timeline with seek, drag/trim, undo, zoom and vertical scrolling | Real two-track video playback/decode, pause, seek, audio delivery, track switching and bidirectional frame stepping tested. Classic docked timeline adapts the workflow rather than duplicating Avalonia layout numbers. Hardware decoding is device-dependent and not certified for every GPU. |
| G08 | SCC to QuickTime CEA-608; SCC/MCC/text caption embedding in MPEG-2 program streams, including MCC CEA-708 packets and second-field support | Real MOV CEA-608 track and MPEG GA94/CEA-708 bytes tested. MPEG output re-encodes video/audio; MCC requires MPEG, matching upstream. No physical disc/decoder tested. |
| G09 | FFmpeg 9.0.2, whisper.cpp 1.9.5, WhisperX build103 and current x64 MPV archive/hash pins; old Win7/32-bit fallbacks preserved | FFmpeg 9.0.2 archive checksum and real execution verified. Removed `-vsync` replaced in player, preview and shot detection. Other download pins checked against the pinned upstream source, not all downloaded anew. |

## Audit corrections and runtime boundaries

- Upstream CrispEmbed 0.17.12 is a CPU pin. GPU/Vulkan remains 0.17.11 in the reference source; Classic keeps those valid assets instead of inventing a GPU 0.17.12 archive.
- The FFmpeg 9.0.2 ZIP contains only `ffmpeg.exe`; the separate FFprobe download remains separate. The local test bundle uses the available FFprobe 8 alongside FFmpeg 9.0.2.
- Classic uses externally installed VLC; its installation is not silently replaced by a runtime-pin update.
- Per-plugin shortcut configuration already existed in Classic. This work preserves it and adds sequence use and stable palette identities.
- `seconv --legacy` retains the SE4 transformation switches. `--settings` in the new path is an image-export settings file, not an import of the entire upstream settings schema.
- AI generation quality, every OCR/ASR model, all paid providers, physical DVD/broadcast devices and every hardware decoder are not certified by these tests.

## Reproducible validation

- `installer/LocalNineTests.cs`: native UI/core/media boundary checks using isolated settings and a synthetic two-audio-track source.
- `installer/LocalNineBatchTests.cs`: actual batch window conversion, multiple targets, request validation, failure handling, output timestamps and native window rendering; uses an in-process mock HTTP handler.
- `installer/LocalNineCliTests.py`: starts an ephemeral loopback server, invokes the real console process, checks generated outputs and overwrite protection. No external API is contacted.
- Build `src/seconv/seconv.csproj` as part of the solution, or use the workspace's offline Roslyn build helpers when NuGet is unavailable.

User check guide: [Russian instructions](LOCAL_53_CHECK.ru.md).

## Local follow-up: timeline playback

The viewport stays fixed while the playhead moves, advances near the right edge and follows backward seeks. Normal playback invalidates narrow cursor strips; track/content maps rebuild only after document changes. Mouse drags freeze the viewport. A dedicated 583-cue, 12-track native test covers stationary cue pixels, partial invalidation, paging, seeking and drag commits.

Shot-change marker visibility has an independent persistent checkbox under Options / Waveform. Hiding affects drawing only; marker data, snapping and the playhead remain intact. Native rendering and startup preference checks passed.

### Spectrogram playback follow-up

Local 4.0.27.103 caches the composed, scaled visible spectrogram layer instead of rebuilding it for every playhead paint. Scrolling, zooming, resizing, source replacement, combination mode and opacity invalidate the cache; source unload and disposal release it. A real Crystal Lake MKV/SRT MPV comparison over the same 80-92 second interval measured average paint times of 11.6 -> 1.9 ms for spectrogram-only, 8.2 -> 2.4 ms combined, and 2.5 -> 2.4 ms waveform-only. These are UI paint measurements from an isolated offscreen test window, not end-to-end video frame-presentation guarantees. Twenty-two renderer/cache checks pass, including pixel equivalence to the original rendering and lifecycle invalidation. Existing marker and timeline regression suites also pass.

### Navigation cleanup

Local 4.0.27.103 reparents existing command instances into task-oriented Tools, Video, import/export and row-context submenus. Save, Save As and forced-only export remain together. The obsolete header-only column toggles are replaced by the same persisted column controller used by the Columns dialog and legacy Settings synchronization. Header and row contexts provide formatting, spelling and links to the existing appearance/highlighting pages; waveform context links to waveform settings. Keyboard settings collect existing Classic shortcuts, sequences, picker keys and palette keys. Context links forward to canonical commands and are excluded from duplicate palette entries. Previous menu-command IDs are retained for custom sequences.

`installer/TestClassicNavigation.cs` passes 37 checks, using `installer/ClassicMenuBaseline.txt` to verify all 325 previously named menu entries remain reachable. It covers original command instances, shortcuts, repeated initialization, shared column/Forced state, settings-page routing, dynamic ASS context actions, restart persistence and Russian/English changes. The existing 142-check native regression suite also passed. Native dropdown screenshots were inspected. No paid API calls. The release workflow repeats the installed-package integration and navigation checks.
