# Subtitle Edit Classic

A fork of the maintained [Subtitle Edit 4.x legacy branch](https://github.com/SubtitleEdit/subtitleedit/tree/se4-legacy), preserving the original Windows Forms interface.

[Download the classic build](https://github.com/BrainDeLook/subtitleedit-classic/releases/latest) · [Upstream project](https://github.com/SubtitleEdit/subtitleedit) · [GPL-3.0 license](LICENSE.txt)

English · [Русский](README.ru.md)

The local build completes the stable 5.3 grid-column display types with **Teletext** (EBU STL row and L/C/R alignment), plus **Style / Actor → WebVTT classes / voice** when WebVTT is active. Enable them in **Options → Columns…**; WebVTT uses the existing Style and Actor preferences, with format-specific headers. Values update after cue edits and format changes, and the shared layout persists. These are derived display values: they do not replace subtitle metadata or introduce a teletext positioning editor. This addition is local and is not in the published installer.

The local build adds **Layer** (ASS only) and **Pixel width** through **Options → Columns…**. Layer follows the existing layer editors and hides in other formats while retaining its saved preference. Pixel width is the widest line without tags, measured using the classic measurement font/size/bold settings; the existing width limit and syntax-coloring switch control its warning. It estimates text width rather than rendering ASS effects or positioning. Both columns persist their visibility/order and remain hidden in older layouts. This addition is local and is not in the published installer.

The local build adds optional **Shot in / Shot out** in **Options → Columns…**. They show signed start/end offsets from the nearest shot change: negative before, positive after, in milliseconds or frames according to the editor time-display mode. Cells highlight the active timing profile’s adjustment zones. Load a video and generate/import shot changes first; distant boundaries and missing shot data stay blank. Visibility/order persist, and existing layouts keep these columns hidden. This addition is local and is not in the published installer.

## Classic 4.0.25

The local build adds **Options / subtitle-grid context menu → Columns…** for the existing classic columns: choose visibility, reorder with Up/Down or Alt+Up/Down, reset the draft, then apply with OK. Cancel leaves the layout unchanged. Header dragging and dialog changes persist across restarts and format changes. Original/network columns remain document-managed; format-dependent columns explain when they appear. The Forced toggle and legacy Tools settings stay synchronized. Cue data, original text, selection and undo history are retained. The remaining SE5-only column types are separate work; this addition is local and is not in the published installer.

The local build adds **Edit / grid context menu → Toggle forced**, an optional **Options → Show Forced column**, and **File → Save forced lines as**. Only selected working cues are marked; mixed selections are marked together and Undo/Redo restores the marks. Forced-only export creates a separate subtitle without changing the open file. Forced marks and bookmarks share the stable `.SE.bookmarks` sidecar next to the subtitle; keep that file alongside SRT/ASS to retain marks when reopening or moving files. The exported subtitle itself contains only the forced cues. This addition remains local and is not in the published installer.

The local build adds **Options → Subtitle grid formatting** with four display modes: original tags, visual formatting, hidden tags, or visual formatting with remaining nonvisual tags. HTML/ASS styling is shown in both the working and original text columns, with contrast protection for text and selection. The source text, tags, timing and saved subtitle remain unchanged. The choice persists across restarts; the existing tag view remains the default. This addition is local and is not in the published installer.

The local build also upgrades **File → Statistics** and the selected-lines statistics command with a stable 5.3-style dashboard: totals, timing/pacing ranges with meters, rule-limit checks and a CPS histogram with optimal/maximum markers. Details and repeated-word/line reports remain available, with separate copy buttons and full UTF-8 text export. The dashboard scrolls above a fixed footer; empty/untimed cues do not produce infinite speed values. Classic gap averages and physical-line/boundary percentages are corrected. Statistics use a source copy and do not edit the subtitle. This addition remains local and is not in the published installer.

The local build upgrades **Tools → List errors** (the existing configurable Ctrl+F8 command) with counted category filters, per-error cue rows, threshold details and exact Go to navigation. It respects classic syntax-colouring limits/switches, keeps the old WPM and missing-time-code checks, and distinguishes EBU teletext width from open subtitles. Export the visible rows to clipboard, text, Excel or a standalone filterable HTML page. **Batch convert → List errors** reports successfully saved text outputs by file, with CSV export and fresh results for each run. The report is read-only and leaves subtitle metadata and Undo intact. This addition is local and is not in the published installer.

The local build also adapts the stable 5.3 **Tools → Merge continuation lines**, **Convert actors**, and **Remove / replace Unicode characters** workflow. Merge offers checked candidates, gap/text limits and the complete output preview; overlapping cues and differing actor/style boundaries stay separate. Actor conversion supports broken bracket labels, two-speaker splits, inline/Actor directions, casing/colour, remembered options and selected cues. Unicode replacement lists characters above Latin-1, including emoji, with counts, editable remembered replacements and a result preview; uncheck alphabet letters you want to keep. These tools preserve cue metadata and use ordinary editor Undo. **Batch convert** has independent merge, actor-conversion and Unicode-control-removal actions with shared processing. General Unicode replacement is intentionally reviewed interactively. The changes remain local and are not in the published installer.

The local preview adds stable 5.3 **Assisted split** and **Assisted move** to the subtitle grid context menu and Tools. Choose among ranked split points or sentence/word/balance moves, with exact text, timing and reading-speed previews; click the numbered title or press 1–5/1–6. Escape cancels. Split retains the first cue's identity and divides an editable original at its own language boundary; moves update both cue texts and their boundary timing together. Styles, actors and other cue metadata are retained, with one ordinary editor Undo. Read-only original references are unchanged. The dialogs scroll above a fixed footer and offer persistent conflict-checked shortcuts, unassigned by default. Candidate generation and application use copied snapshots, reject stale editor content, and preserve nested HTML/ASS formatting. These changes are not yet in the published installer.

The local preview adapts the stable 5.3 synchronization workflow. **Synchronization → Visual sync** shares the editor's cached waveform between two resizable panes, retains ASS preview context and the selected audio track, and offers manual offset/speed adjustment. **Point sync** adds a resizable waveform to its video/time picker, precise seek shortcuts, editable time codes without video and point-list deletion. **Point sync via other subtitle** opens its reference inside the window, highlights long gaps, previews SUP and PGS/VobSub/DVB image tracks without OCR, and can set an uncovered cue's point via video. Ctrl+P opens a reference image; arrows browse images and Ctrl+plus/minus changes thumbnail height. Reapplying points transforms the frozen source rather than accumulating shifts, preserves cue metadata and intentional overlaps, and rejects invalid transforms atomically. MP4 tracks unsupported by the classic parser are normalized through FFmpeg with the selected track retained and temporary files removed. Waveforms appear only for the matching video when the main editor already has peaks. These changes are not yet in the published installer.

The local preview extends **File → Compare** with a millisecond time tolerance, signed timing deltas and a working current-file copy: take reference text, timing or both, insert a missing cue, delete a current cue and undo individual edits. Apply commits through editor undo with a concurrent-change guard; Close discards unapplied edits. Opening another file on the left makes the comparison read-only. Manual pairing and sync points now use the stable 5.3 content/timing aligner: pin current/reference cues, optionally shift the current cue and following cues up to the next point, remove/clear points and undo these operations. Points are session-only, survive Apply and reset when a file is replaced. The remaining stable 5.3 Compare workflow is also adapted: an in-window text/timing editor (F2, Ctrl+Enter, Escape), All/Differences/Text differences views with independent counts, ignore-numbering, load-saved-file-as-reference, violet working edits and a clickable whole-file overview. HTML export retains aligned and missing rows, edit colors and line breaks. Status distinguishes timing and text changes without declaring retimed identical text unrelated. This is not yet in the published installer.

The local preview adds a live **Beautify time codes** comparison: synchronized original/result timelines or waveforms, changed-cue navigation, signed time/frame deltas and inferred reason hints. Calculations use a copy and Apply returns the displayed result through normal undo. Cached editor audio is reused; waveform extraction follows the selected audio track. This is not yet in the published installer.

The local preview adds **Edit → Command palette** (Ctrl+Shift+K when free): search classic menu and main/grid/waveform shortcut actions by name, group, keys or initials, then run with Enter. It includes unassigned actions, session-only recent history and a separate configurable palette shortcut. Text-box-only commands are excluded. This is not yet in the published installer.

Local development adds named **Beautify time codes** profiles to the classic profile editor. Save, load and delete custom rule sets alongside the built-in presets; cancelling profile edits preserves active settings, while explicitly saved library entries remain available. These changes are not yet in the published installer.

Local development also routes the built-in update checker to this fork's stable releases, including a public fallback for GitHub API rate limits. nOCR training gains a font preview, multiple sizes and incremental training from an existing database, with background execution and cancellable work. These changes are not yet in the published installer.

Local development also extends AI review with category/warning filters, editable suggestions, unchecked large rewrites, separate apply/undo passes, selected-line review, preview playback, server start/stop and request pacing. These additions retain the classic interface and are not yet included in the published installer.

Local development after this release adds **llama.cpp advanced, Ollama advanced and LM Studio advanced** with numbered cue batches, bounded translated context and schema-validated output. All ten modern translation choices are also connected to classic Batch Convert, with shared settings, per-file context reset and cancellation. This local increment is not yet part of the published 4.0.25 installer; see [the unreleased changelog](CLASSIC_CHANGELOG.md).

**Classic 4.0.25** expands audio-only per-line cloning to thirteen backends (OmniVoice needs original transcripts for per-line cloning) and adds OpenRouter/DashScope/Google Cloud v2 ASR plus optional compatible-API SSE responses. **Tools → AI subtitle review / Current-line AI assistant** can download and start its own local llama.cpp model or use an existing compatible server. Only checked text changes are applied, preserving timing and metadata. **Tools → Local AI models** provides pinned engine/model downloads, CPU/Vulkan/CUDA selection, a custom model folder and start/stop controls. If MADLAD is missing, Translate opens installation settings with status, path and download size; cancelling preserves existing cues.

Translation settings also provide terminology, formality, editable prompts, an untranslated-source retry and strict line-break preservation. AI review includes persistent custom templates and source-filtered context generation. Google v2 ASR recovers a truncated tail once and cleans its temporary audio. Translation adds OpenAI-compatible, llama.cpp, NVIDIA, API Route, Perplexity Responses, Lara and downloadable local MADLAD (419 language tokens). Bitmap OCR adds Google Lens Sharp, Mistral OCR, CrispEmbed GGUF models and llama.cpp / compatible Vision, reusing the classic correction workflow. Settings provide local OCR runtime/model downloads. Lens sends the selected subtitle image to Google; the other API engines send data to their configured providers.

Native checks cover all 17 Crisp ASR backends and all eight CrispEmbed model choices on synthetic phrases. Two-reference cloning checks cover the supported audio-only engines; OmniVoice is checked with exact original transcripts. VibeVoice remains reference-sensitive: complete synthetic phrases passed with CPU reference encoding, while a short synthetic clip did not. CosyVoice had a pronunciation error in one test output. Managed Qwen passed Russian/Japanese translation, grammar review and context generation on CPU, Vulkan and CUDA; MADLAD and live Google Lens were also checked. Paid APIs were protocol-tested with simulated responses; model variants have separate native coverage. See [the changelog](CLASSIC_CHANGELOG.md) for the precise coverage. Download the normal Windows installer from [Releases](https://github.com/BrainDeLook/subtitleedit-classic/releases/latest).

Six additional stable v5.3 TTS engines run through audio.cpp: Fish Audio S2 Pro, IndexTTS 2.5, Higgs Audio v3, FireRedTTS3, KugelAudio and Breeze TTS 2. Play with speech starts at the selected main-window line. nOCR gains the stable matcher and font trainer with three line-generation algorithms while retaining the classic windows and existing databases.  See [the changelog](CLASSIC_CHANGELOG.md) for language, model-license and validation limits.

This release also ports Kokoro, F5-TTS, Pocket TTS, VibeVoice, IndexTTS 1.5, Dots-TTS, Confucius4, MOSS-TTS and both OmniVoice runtimes from stable v5.3. The shared local catalog contains 20 engines and 51 model bundles; Qwen3/Supertonic retain their existing controls. Models and verified Windows runtimes download from the classic interface. Actor casts, regeneration, live playback and the shared voice library are reused.

Pocket chooses its language through the model (six languages, no Russian checkpoint). Kokoro provides English/Chinese presets and cannot clone. Confucius offers 14 explicit target languages; Dots and Confucius expose ODE steps. Standalone OmniVoice offers voice instructions for its Default voice; the CrispASR variant keeps the model loaded between lines. F5 remains experimental and slow, as noted by stable upstream. Reference transcripts are optional for compatible engines; Qwen, F5 and standalone OmniVoice still require them. Voice packs retain empty transcript sidecars without making those references available to engines that require text.

This release also includes Edge TTS, Mistral Speech, Google Cloud Speech and Zonos (CrispASR), completing the enabled stable v5.3.0 Windows TTS catalog. Edge installs its pinned client in an isolated environment (Python 3.10+ required) and needs internet access. Mistral uses an encrypted API key; Google uses a service-account JSON key file. Voice refresh retains the previous list on errors. Zonos downloads verified model files and eSpeak NG locally, without installing an MSI into Windows; it offers one fixed voice, not cloning. English/Japanese Zonos and English/Russian Edge synthesis were checked; paid Google/Mistral synthesis was checked with simulated HTTP responses, not live credentials.

The local preview adds **Save session** and **Take history** to audio review, and **Import session** to the classic speech window. `SubtitleEditTts.json` travels with its audio/reference subfolders, preserving the full timeline, inclusion flags, actors and per-line settings without exporting API credentials. Imported clips can be assembled without synthesis. Every successful regeneration remains available in the current review's take history; only the active take is exported. Imported voice-reference working copies are cleaned on closing the window. If the session's video is unavailable, audio export remains possible; open the matching video in the editor before using synchronized playback.

Speech review now shows the selected generated clip on a waveform, with draggable cue edges, zoom/scroll and click-to-play using MPV. **Fit end to audio** adjusts the subtitle end to the actual clip length; **Reset timing** restores the times from opening review. Audio is neither trimmed nor stretched by these edits. Changed times are saved in sessions and used during final assembly.

Selected-lines speech recognition now uses the classic ASR window with modern and legacy engine choices. Review per-line results, **Retry failed lines**, then **Apply transcribed lines**: only successful selected text changes, while timing, actors/styles and unselected/failed text remain intact. Cancellation keeps completed results available. For one long clip, the context menu retains the option to transcribe with splitting and new timings. Russian and English were checked with real Parakeet CUDA 13 inference.

Burn-in audio checkboxes now show the track language, embedded title and audio parameters in a full-width list below the audio settings. Hover for complete metadata when a title is long; each checked track is exported independently.

ElevenLabs now includes stable v5.3 v4 Turbo/v4/v3 models, matching language lists and model-specific break/pause handling; its protocol is tested locally without paid API requests. Piper gains **Import voice** for a `.onnx` model and adjacent `.onnx.json` config. Imported pairs appear as Custom voices; existing files are preserved on name collisions. Real imported English/Russian Piper synthesis was checked.

Voice manager now displays a reference waveform with zoom and click-to-play through MPV. **Transcribe reference** opens the usual ASR dialog in both voice manager and import; import uses only the selected source interval. Recognized text stays editable until saved or imported, and empty results preserve existing text.

The main waveform context menu now offers **Create voice reference** for a subtitle or a 1–30 second free selection. Classic voice import opens with the range, current audio track and available cue text/name already filled. Free selections can use ASR for their transcript. Imported references appear in the shared voice manager; original subtitles are unchanged.

**Voices from video → Clone each line** can now work without original subtitle files with Fish, Higgs, IndexTTS 2.5 and Breeze. Each cue uses its own source-audio range; translated target text is never treated as the source transcript. Short clips grow into free gaps and are padded to at least one second. Other engines retain their transcript requirement. Real Higgs English/Russian output was independently transcribed for verification.

ASR now includes **OpenAI-compatible / vLLM**. Use **Advanced** to configure the URL, model, optional API key, prompt, chunk duration and timeout. Compatible local servers can run without a key. Segment/word timestamps are preserved; plain-text JSON receives estimated cue timing. Long audio is sent in bounded chunks. Protocol and loopback HTTP tests passed without paid calls; a real vLLM model and commercial services were not tested in this increment.

All 17 stable Crisp ASR backends are now selectable, with language-compatible forced aligners. Faster-Whisper-XXL offers **Multilingual (per segment)** alongside automatic language detection. **anime.ja** is available for Japanese transcription in XXL, CTranslate2 and modern WhisperX, with distilled alignment-config repair. Native tests cover Canary English/Russian, SenseVoice English/Japanese execution and WhisperX anime recognition against known Japanese dialogue phrases. All seventeen Crisp backends now have native synthetic-phrase checks. XXL retained English and Russian with explicit batched VAD and four-second chunks; small-model recognition still made word errors. Longer merged segments can miss a short language switch, so multilingual detection is not a guarantee of complete mixed-language transcription.

## Classic 4.0.24

ASR adds selectable VAD, speech isolation, speaker detection and new stable v5.3 models, including Parakeet Ultra, Phonon-2, Voxtral and Index-Echo. Speaker metadata survives postprocessing; batch subtitles use ASS when actors are present. Time-code review adds editable original/proposed waveforms and twelve language-specific wav2vec2 aligners. Download the normal Windows installer from [Releases](https://github.com/BrainDeLook/subtitleedit-classic/releases/latest). Native checks focus on Russian and English; see [the changelog](CLASSIC_CHANGELOG.md) for coverage and limits.

## Classic 4.0.23

CosyVoice 3, VoxCPM 2 and Chatterbox are available with model downloads, shared reference voices and actor casts. Select the model in the engine's Settings dialog; CosyVoice also offers preset voices. Chatterbox Turbo is English-only. The default models were tested on CUDA 13 in Russian and English; the remaining quantizations/backends have narrower coverage.

The classic interface includes actor-specific cast settings, cloning from video/original subtitle lines, a voice manager with pack import/export and reference trimming, Qwen3 VoiceDesign and Sidon cleaning. Automatic speaker detection can merge voices; review its results or use existing subtitle actors. Download the normal Windows installer from [Releases](https://github.com/BrainDeLook/subtitleedit-classic/releases/latest). Engines and models download on demand from the application.

## Features inherited from Classic 4.0.22

This release ports stable v5.3 features into the classic interface: 3D burn-in and NVENC preset migration, Supertonic-3 and OpenAI-compatible TTS, speech-line preparation, original-speech removal with HTDemucs/RoFormer, reviewed forced-alignment timing corrections and OCR Find/Replace/nOCR database optimization. A Cyrillic database is bundled without replacing an existing user database. Engine/model downloads remain available from the interface.

All 30 enabled Windows TTS implementations from stable v5.3.0 are now represented in the local preview. This catalog count does not imply that every upstream setting or model variant has been tested. See [the changelog](CLASSIC_CHANGELOG.md) for validation and limits.

Select actors with the checkboxes in the speech window; right-click assigns their voices. Removal settings can target either all original speech or only generated cue intervals; selective mode retains audio outside those intervals. In selective mode, overlaps with unchecked actors keep the original mix.

## Base version

Based on `se4-legacy` at `96e6d302659e94e77564b98805d37e52c80d6501` (2026-10-10), including 22 commits after the last stable 4.x release, 4.0.16. The default branch of this fork is `se4-legacy`; it retains the classic interface and does not migrate to the 5.x interface.

## MP4 burn-in fix for Adobe Audition

The hard-subtitle exporter enables MP4 edit lists (`-use_editlist 1`) instead of disabling them. This allows the MP4 muxer to account for AAC encoder delay without stretching the first video sample.

For the reported 23.976 fps H.264/AAC exports, disabling edit lists produced a first sample of 1513 ticks instead of 1001 ticks at a 1/24000 time base. Correcting the video timing made playback work in Audition without changing the compressed H.264 or AAC data. See [upstream issue #15992](https://github.com/SubtitleEdit/subtitleedit/issues/15992) for reproduction and controlled test results.

The timing fix remains enabled for MP4/MOV exports. The editor and classic panels are retained from the legacy branch.

## Classic 4.0.21: video OCR and local speech generation

Extract burned-in subtitles through **Video > OCR burned-in subtitles** using Paddle OCR or CrispEmbed (GLM-OCR, DeepSeek-OCR-2, PP-OCRv6). Select the scan area, test a frame, edit the extracted cues and accept them into the classic editor. The stable v5.2.0 pipeline provides grouping, duplicate merging and frame-based timing refinement. English/Russian correction tables are included; engines and models can be downloaded in the dialog.

The classic text-to-speech window adds **Qwen3 TTS (CrispASR)** with nine CustomVoice speakers and imported-reference Voice clone. Select an audio/video range, provide its exact transcript and confirm permission to use the reference. The application normalizes the recording to 24 kHz mono. An incorrect transcript can make cloning repeat unrelated phrases or produce unclear speech; the text must match the selected recording.

Choose the final WAV or dubbed-video destination before generating speech. The selected folder is remembered, successful output is published to that location, and temporary render jobs are cleaned up. Reusable models and voice references remain in the application data directory.

This release also fixes OCR settings scrolling and clipped crop buttons, and fixes burn-in motion effects using the output video dimensions instead of ASS script coordinates. Existing speech recognition, classic panels and the Audition MP4 timing fix are retained.

Validation covered Russian/English OCR and TTS, actual burned-in video, native cancellation, timed audio mixing, export replacement and temporary-job cleanup. Voice cloning was checked with a permitted synthetic reference; speaker similarity on arbitrary recordings is not guaranteed. Full v5 OCR/AI/TTS coverage and automatic per-line speaker cloning have not been migrated.
## Classic 4.0.20 speech recognition

Local speech recognition now includes Crisp ASR (GigaAM, Parakeet and Qwen3), Qwen3 ASR CPP, Whisper CPP Vulkan and standalone WhisperX CPU. Choose an engine/model and use the model download button or start transcription to fetch the required runtime and weights. New standalone engines do not need a separate Python installation. CPU, CPU legacy, Vulkan and CUDA 13 are available where supported.

Crisp transcripts are split into subtitle cues using word timings and punctuation, including continuous speech without VAD. Optional VAD/retry, a quality report, safe batch output names, process cancellation and elapsed time remain inside the classic window. Model list scrolling works when switching between short and long lists.

Russian WhisperX word alignment is a known limitation: the external alignment-model endpoint returned HTTP 500 in tests. Turn off word alignment to use segment timings. English alignment passed. The separate legacy selected-lines dialog and paid cloud services have not been migrated in this stage.

Local validation included Russian/English transcription, the user's NVIDIA GPU, VAD, real form cancellation/batch handling and a 120-second video extract: 29 cues after postprocessing, maximum duration 7.2 seconds, with recognised words preserved. CI also tests a clean CPU engine/model download and real transcription after installing Setup.exe. Short samples do not establish accuracy for every catalog model or recording.

## Classic 4.0.19 additions

The burn-in window adds compatible MP4/MKV/MOV/TS/WebM container choices and AAC, AC-3, MP3, Opus, Vorbis or source-audio copy where supported. A separate classic Advanced dialog provides text styling/effects, PNG logo placement, Blu-ray SUP overlays, audio-only input and source-size two-pass encoding. Existing ASS styles are preserved.

The release also fixes progress/footer overlap and uses integer MB for manual target sizes. Window resizing avoids intermediate editor sizes and applies video width/height together, reducing redundant native video-surface size notifications.

Validation covered 24 audio/container combinations, representative video encoders, MP4 timing, overlays, audio-only input, two-pass export, six editor layouts and the installed language picker. Resizing was profiled with an H.264 video and loaded waveform; these checks do not establish a universal frame rate for every GPU, video or display.

## Usage

Translation compatibility is tied to the legacy 4.0.16 schema, independently of the fork release number. The installer test loads every packaged translation and checks the actual language picker, including Russian.

Run the latest `SubtitleEdit-Classic-<version>-Setup.exe` from Releases. The normal Windows installer installs Subtitle Edit Classic, creates shortcuts, and registers an uninstaller. Configure or download FFmpeg when prompted, then generate burned-in subtitles normally using H.264 and AAC in MP4.

Previously exported files must be regenerated or repaired separately. Playback of a complete new export from this build should be checked in Audition at the previously affected positions; the muxer timing correction has been verified independently.

## Build from source

From a Visual Studio Developer PowerShell on Windows, with the .NET 8 SDK and .NET Framework 4.8 / 4.8.1 targeting packs:

```powershell
msbuild src/UpdateAssemblyInfo/UpdateAssemblyInfo.csproj /restore /p:Configuration=Release
& ./src/UpdateAssemblyInfo/bin/Release/net48/win-x64/UpdateAssemblyInfo.exe src/ui/Properties/AssemblyInfo.cs.template src/libse/Properties/AssemblyInfo.cs.template
msbuild SubtitleEdit.sln /restore /p:Configuration=Release /p:SolutionDir="$PWD\"
```

The solution builds the original Windows Forms application. Build the normal Windows installer with Inno Setup 6.2.1 or newer: `ISCC.exe /DCLASSIC_FORK installer/Subtitle_Edit_installer.iss`. The classic-fork switch gives the installer its own application identity, installation directory, and shortcuts.

Original Subtitle Edit is developed by Nikolaj Olsson and contributors. This fork retains the upstream GPL-3.0 license and attribution.
