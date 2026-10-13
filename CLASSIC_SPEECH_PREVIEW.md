# Speech-to-text feature validation for Classic 4.0.20

Feature source: stable Subtitle Edit v5.2.0. The editor remains Windows Forms.
This feature set was accepted locally before the Classic 4.0.20 stable release.

## Added

- Crisp ASR: Parakeet, GigaAM and Qwen3. CPU, CPU legacy, Vulkan and CUDA 13.
- Qwen3 ASR CPP with required forced alignment, CPU and Vulkan.
- Whisper CPP Vulkan and standalone WhisperX CPU; no separately installed Python required.
- Private versioned engine/model downloads, pinned archive SHA256 checks, cancellable downloads and processes.
- Crisp VAD, retry without VAD on empty/missing output, per-engine extra arguments.
- Quality report for overlaps, short/long durations, non-speech descriptions and consecutive repetitions; optional removal remains off by default.
- Single-file and batch transcription, unique batch filenames that preserve existing subtitles, elapsed time and bounded progress log.
- WhisperX word alignment can be explicitly disabled to use segment timings.

Pinned runtimes: CrispASR 0.8.32, qwen3-asr.cpp 0.1.8, whisper.cpp Vulkan 1.9.3, WhisperX standalone 1.0.1.
The embedded stable catalog includes 17 engines, but this preview exposes only the validated families above.
The separate legacy selected-lines window, paid cloud services and other Crisp backends are outside this preview.

## Local checks (2026-10-14)

- UTF8 SRT and timed Qwen JSON parsing, invalid timestamp rejection, Windows argument quoting.
- Archive traversal rejection, pinned runtime archive verification, nonzero exit handling, VAD retry, cancellation including child processes.
- All four engine families and settings rendered at two widths; footer/buttons checked and screenshots inspected.
- Real transcription through the classic form, responsive message loop, returned subtitle and operation cleanup.
- Controlled form cancellation and batch output checks, including preservation of a pre-existing SRT.
- Russian: GigaAM on CPU, CPU legacy, Vulkan and CUDA 13; Crisp Qwen3 Vulkan; Qwen3 CPP Vulkan; Whisper CPP Vulkan.
- English control recording with explicitly selected Zira: Parakeet v3 Vulkan, Qwen3 CPP CPU, Whisper CPP Vulkan, WhisperX base CPU.
- Parakeet v3 on an English speech extract from the user-provided Tyson video; transcript has some recognition errors.
- Real Crisp VAD with GigaAM Russian.
- WhisperX base Russian without word alignment: valid segment timings, but the short greeting was omitted.
- Previously added hard-subtitle export, two-pass/style handling and idle/active layout checks still pass.

WhisperX Russian word alignment remains unverified: the official alignment-model endpoints returned HTTP 500. The preview explicitly downloads the complete processor and model to avoid a partial bundled HF cache. This path needs a successful online test before a stable release. English word alignment passed. Quality heuristics do not measure transcription accuracy; these short tests do not prove all catalog models or full-length files.

Controlled test sources are retained in `installer/speech-tests`. Live runs need the corresponding pinned runtimes, models and test recordings; they are not included in the installer or source tree.
