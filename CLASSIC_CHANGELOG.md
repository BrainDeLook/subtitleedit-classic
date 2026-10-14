# Subtitle Edit Classic changelog

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
