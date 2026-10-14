# Subtitle Edit Classic changelog

## 4.0.23 — local preview

- Fixed burn-in quality hints overlapping the CQ input and moved the total-bitrate explanation into its own bounded row.
- Cast settings persist each actor's engine, voice, model, language and instructions. Qwen3/Supertonic/API casts can be mixed with the existing engines; audio review regenerates each actor through the correct engine. Voice-name tags can be moved into speech-session actor metadata without removing them from the editor text.
- Clone voices from video using Sortformer speaker labels or existing subtitle actors. Review actor names and exact reference transcripts before importing. Per-line Qwen3 cloning uses original-language subtitle text, with temporary references cleaned after synthesis.
- Voice manager: edit names/transcripts, trim a reference into a new copy, import/export voice packs, copy references to another engine's folder and download the stable upstream voice packs. Attribution is retained where supplied.
- Qwen3 VoiceDesign and Sidon reference restoration use the stable v5.3 model/runtime definitions, validated downloads and cancellable local processing.

Native checks covered mixed Qwen3/Supertonic casts, actor-specific regeneration/cancellation, Russian/English VoiceDesign, cross-language per-line cloning, Sidon CPU restoration and an official English voice-pack download/import. Independent ASR recovered the generated test sentences. Sortformer ran successfully but merged two short synthetic voices into one in the test fixture; automatic speaker results require review. Existing subtitle actors offer a direct alternative. Paid cloud-provider synthesis was not exercised.

This preview remains local; no release has been published.

## 4.0.22

Feature ports are based on the stable upstream v5.3.0 tag. This release preserves the classic Windows Forms interface.

- Burn-in: half side-by-side / top-bottom 3D subtitles with depth adjustment and a rendered preview; current NVENC presets and migration of legacy low-latency/lossless presets. The ordinary player does not composite 3D overlays: use the Preview button.
- Speech generation: Supertonic-3 with ten preset voices, 31 languages and speed control; OpenAI-compatible TTS for OpenAI, OpenRouter or a custom endpoint, separate provider settings, MP3/PCM fallback and protected API-key storage on Windows.
- Prepare speech: review continuation merges and silence-only annotations before generation. Actor changes, dialogue dashes and long pauses prevent merges. Annotation text remains in the subtitles; editor changes can be undone.
- Fixed the Russian pixel-format dropdown extending outside the burn-in video column.
- Actor selection: explicit checkboxes limit synthesis and audio review to the selected actors. In selective removal mode, unselected and unnamed actors retain their original audio in video exports. Speech removal is restricted to generated cue intervals, with short internal fades; overlapping unselected actor intervals remain original because timing alone cannot separate simultaneous voices.
- Selective dubbing supports the newer FFmpeg file-argument syntax, with a fallback for older builds.
- Dubbing settings now offer a persisted choice between removing all original speech and removing only selected actors' generated cue intervals. Actor selection for synthesis remains independent; existing settings retain selective removal.
- Dubbing: remove original speech with HTDemucs or Mel-Band RoFormer and mix the remaining music/effects with generated speech. Models download on first use. Video muxing remains cancellable; MP4 uses AAC by default and preserves edit-list timing.
- Improve time codes: Canary/Qwen forced alignment, independent Parakeet word checks, rough synchronization, neighbouring-line safeguards, editable old/new proposals, selective application and optional frame snapping. Preview proposed positions in the main player. Positioned signs, comments and music cues are excluded from speech alignment.
- OCR: Find/Replace with regular expressions, whole-word search and undo; stable upstream CrispEmbed builds (CPU 0.17.12, GPU 0.17.11), Romanian/Croatian correction rules and the Cyrillic nOCR database. nOCR optimization uses the v5.3 pruning algorithm with the classic bitmap/line adapters, previews the reduction and backs up the original file.
- Retained the local speech-ending fix: relative silence thresholds and no unconditional 100 ms cuts from either end.

Validation covers real Russian/English Supertonic and Qwen synthesis, permitted synthetic-reference cloning, independent ASR checks, both native speech-removal models, real forced alignment, 3D/NVENC exports, API protocol/PCM/cancellation tests without paid requests, OCR/nOCR algorithms and classic forms. On the compressed Russian movie crop, GLM made the same spelling errors as the old runtime; this update does not guarantee error-free recognition. Separation can leave residual voices or remove parts of the background; long CPU jobs can be slow.

The full v5 catalog, voice-pack manager, Sidon reference cleaning and automatic per-line voice cloning are not included in this release.

## 4.0.21

Subtitle Edit Classic 4.0.21 brings video OCR and Qwen3 speech generation from stable Subtitle Edit 5.2.0 into the classic Windows Forms interface.

- Extract burned-in subtitles with Paddle OCR or CrispEmbed: GLM-OCR, DeepSeek-OCR-2 and PP-OCRv6. Includes crop selection, frame testing, editable cues, grouping, timing refinement and English/Russian OCR correction tables.
- Generate local speech with Qwen3 TTS CustomVoice or Voice clone. Nine preset speakers, explicit language selection, imported audio/video references, reference normalization and matching transcripts are supported. Engines and models can be downloaded from the interface.
- Choose the destination for the finished WAV or dubbed video before generation. The folder is remembered; temporary render jobs are cleaned up after completion or cancellation.
- Fixed OCR scrolling and clipped crop buttons. Fixed ASS burn-in motion effects moving text outside the script canvas when the video and ASS resolutions differ.
- Preserved existing speech recognition, classic panels, hard-subtitle features and the MP4/AAC timing fix for Adobe Audition.

Validation includes Russian/English OCR and TTS, native process cancellation, timed audio mixing, export replacement and cleanup, and an actual burned-in video sample. Independent speech recognition confirmed generated test sentences. Installer CI checks startup, packaged translations, speech/model scrolling, a clean ASR engine download, OCR algorithms/layout, TTS layout/export and the ASS canvas regression.

For cloning, the transcript must exactly match the selected reference recording. Use a clear single-speaker sample and confirm permission in the import dialog. Cloning validation used a permitted synthetic reference; voice similarity on arbitrary recordings is not guaranteed. Automatic per-line speaker cloning, VoiceDesign and the full v5 engine catalog are not included. The existing Russian WhisperX external-alignment limitation remains: disable word alignment when its provider is unavailable.

Download **SubtitleEdit-Classic-4.0.21-Setup.exe**, the normal Windows installer. Large OCR/TTS/ASR models are downloaded separately and are not bundled. SHA256SUMS.txt contains the installer checksum.
