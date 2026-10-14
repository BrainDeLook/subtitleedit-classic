# Subtitle Edit Classic

A fork of the maintained [Subtitle Edit 4.x legacy branch](https://github.com/SubtitleEdit/subtitleedit/tree/se4-legacy), preserving the original Windows Forms interface.

[Download the classic build](https://github.com/BrainDeLook/subtitleedit-classic/releases/latest) · [Upstream project](https://github.com/SubtitleEdit/subtitleedit) · [GPL-3.0 license](LICENSE.txt)

English · [Русский](README.ru.md)

## Local 4.0.22 preview

The next local build ports stable v5.3 features into the classic interface: 3D burn-in and NVENC preset migration, Supertonic-3 and OpenAI-compatible TTS, speech-line preparation, original-speech removal with HTDemucs/RoFormer, reviewed forced-alignment timing corrections and OCR Find/Replace/nOCR database optimization. A Cyrillic database is bundled without replacing an existing user database. Engine/model downloads remain available from the interface.

This build is being tested locally; the Releases link still points to the published stable version. The full v5 engine catalog, voice-pack management, Sidon cleaning and per-line voice cloning are not part of this preview. See [the changelog](CLASSIC_CHANGELOG.md) for validation and limits.

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
