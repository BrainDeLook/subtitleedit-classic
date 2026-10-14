# Classic Video OCR validation

This local preview adapts burned-in subtitle extraction from the stable Subtitle Edit v5.2.0 tag to the existing .NET Framework 4.8 / Windows Forms application. This feature set ships in Classic 4.0.21.

Open **Video > OCR burned-in subtitles**. Select the subtitle area in the preview, choose the language and engine, test a frame, and start extraction. Review or edit the resulting cues before accepting them into the subtitle editor.

## Backported behavior

- Frame grouping, confidence-weighted text voting, duplicate merging, short-observation filtering and frame-based timing refinement from v5.2.0.
- Crop selection, frame sampling and similarity thresholds, minimum duration, gap handling and optional ASS positioning.
- Paddle OCR 3.7 standalone CPU / CUDA 12.9 runtimes and upstream language/model mappings.
- CrispEmbed 0.17.9 CPU / Vulkan / CUDA runtimes, with GLM-OCR, DeepSeek-OCR-2 and PP-OCRv6 models from the stable catalog.
- Engine/model downloads within the dialog. Runtime archives are pinned and SHA-256 checked; GGUF model validation follows upstream size/header checks.
- English and Russian replacement tables from v5.2.0, using the classic OCR correction engine and preserving user additions/exclusions.
- Editable results, preview navigation, cancellation and isolated temporary files. Cancelled scans do not replace the editor's subtitles.

The existing classic interface, speech recognition and hard-subtitle generation are retained. GDI bitmap operations and bounded asynchronous tasks replace Skia/Avalonia and newer runtime APIs. This preview does not include the v5 cloud, Ollama or llama.cpp vision adapters, nor the complete newer OCR correction engine.

## Validation

- Upstream-derived grouping, voting, text cleanup, reading order, language mapping and replacement-rule checks.
- Dialog rendering at its normal and minimum sizes, real extraction through the dialog, and cancellation after native Paddle/CrispEmbed processes have started. No owned OCR process remained after cancellation.
- Synthetic English/Russian video fixtures: three known cues, including two-line subtitles. Refined start/end times were within 90 ms of fixture boundaries.
- Paddle CPU: English and Russian; Paddle CUDA: English. CrispEmbed PP-OCRv6: CPU and CUDA English. GLM-OCR and DeepSeek-OCR-2: Vulkan English and Russian.
- A 20-second sample of the user's burned-in video produced four separate Russian cues with line breaks. The private video and extracted text are not included in the repository.
- Existing speech unit checks and classic hard-subtitle export/UI checks passed.

Recognition remains model-dependent: GLM can combine two visual lines into one sentence, and accents or punctuation may vary. The fixture timing tolerance is a test result, not a guarantee for every video. Narrow the selected area to the captions to reduce recognition of unrelated scene text.

## Test sources

`installer/video-ocr-tests/TestClassicVideoOcr.cs` contains the test harness; `en.ass` and `ru.ass` define the synthetic captions. Compile against the candidate executable and its dependencies with .NET Framework 4.8 and a modern C# compiler. Set `CLASSIC_OCR_TEST_FFMPEG` to FFmpeg. Keep an empty `Dictionaries` directory beside the test executable to isolate test configuration from user data.

Harness modes include `unit`, `ui`, `scan`, `ui-run`, `ui-cancel` and `ui-cancel-crisp`. UI run/cancel modes expect `../samples/en.mp4` relative to the output folder. Generate 12-second, 1280x720, 24000/1001 fps fixtures with FFmpeg's `subtitles` filter and the supplied ASS files. Native scan tests require downloading the selected engine/model first.

Source provenance: https://github.com/SubtitleEdit/subtitleedit/tree/v5.2.0/src/ui/Features/Video/VideoOcr and the matching stable OCR catalog/helpers. Algorithm files retain their porting comments. Future engine upgrades should be reviewed against stable upstream tags.

