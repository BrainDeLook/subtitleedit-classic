# Classic Qwen3 TTS validation

This stage adapts the stable v5.2.0 **Qwen3 TTS (CrispASR)** CustomVoice and Voice clone backends into the existing Windows Forms speech-generation dialog. This feature set ships in Classic 4.0.21; it is not a complete migration of every v5 TTS engine.

## Included

- CustomVoice 1.7B with the nine upstream speaker IDs: aiden, dylan, eric, ono_anna, ryan, serena, sohee, uncle_fu and vivian.
- Imported-reference voice cloning: select an audio/video range, provide its exact transcript and confirm reference permission. References are normalized to 24 kHz mono WAV and stored with text sidecars; generated audio is identified as synthetic in the classic settings dialog.
- Explicit output language selection, including Russian and English, plus optional voice-style instructions.
- Shared pinned CrispASR 0.8.32 runtimes: CPU, legacy CPU, Vulkan, CUDA and CUDA 13. The TTS model and codec live in a separate `TextToSpeech/ClassicQwen3` data directory.
- Download button and automatic download on generation. Model downloads are staged, checked against exact upstream byte sizes and GGUF headers, verified with SHA-256, and published only after validation. Existing runtimes use the validated speech downloader.
- One local loopback server per generation session; the model is reused across lines and the owned server is stopped on completion, failure or cancellation.
- Integration with classic voice testing, actor-to-voice assignment, clip review/regeneration, cue speed adjustment, timed audio mixing and the existing add-to-video path.

Automatic per-line cloning from video, VoiceDesign models, the other CrispASR/audio.cpp TTS backends, automatic speaker detection and the new v5 sound-only-line review are outside this stage. A Save As dialog selects the finished WAV or dubbed-video destination before generation. The final file is published after success; owned temporary render jobs are cleaned up, while imported references and models remain reusable. Piper and the existing classic engines remain available. No paid service is used by this new engine.

## Validation

- Real CustomVoice generation using CUDA 13 on the user's NVIDIA GPU, in Russian (vivian) and English (ryan).
- Independent GigaAM/Parakeet recognition recovered both test sentences from the generated audio.
- Base-model cloning from an explicitly permitted synthetic reference generated distinct new Russian and English sentences, confirmed by independent ASR. This verifies content generation and reference handling, not a measured human speaker-similarity score.
- Cloned-voice generation and cancellation also passed through the classic form. Export replacement and owned-job cleanup preserved unrelated directories.
- Real generation through the classic form, speed adjustment and final timed mixing: a 14.5-second WAV with speech beginning near the requested 1-second and 9-second cue starts.
- Cancellation through the form's Cancel button after the native server started; operation state was cleared and no owned server remained.
- The classic dialog rendered with all nine voices and a fully visible download button.

These short samples do not establish voice quality for every speaker/language. CPU and Vulkan are offered by the upstream runtime but were not benchmarked in this stage. Existing clip review and video muxing are reused; the new-engine integration test covered WAV generation and mixing, not a full feature-length dubbed video.

## Provenance

Protocol, speakers, languages, model URLs and expected sizes come from the stable tag:
https://github.com/SubtitleEdit/subtitleedit/blob/v5.2.0/src/ui/Features/Video/TextToSpeech/Engines/Qwen3TtsCrispAsr.cs

Codec SHA-256 comes from the matching `DownloadHashManager`. The stable source had no verified CustomVoice hash; this port additionally verifies its Hugging Face LFS SHA-256 (`3985813e21a3f27407110ba3afcd8f12ebc19734a5160ec31992637cd62aa67e`).

Test harness: `installer/tts-tests/TestClassicTts.cs`. Compile with the candidate executable and dependencies, .NET Framework 4.8 and a modern C# compiler. `ui` checks layout in an isolated application-data directory; `live`, `ui-run` and `ui-cancel` use the current Windows user's installed engine/model cache. Set `CLASSIC_TTS_TEST_FFMPEG` for the mixing test. Native modes may download models and should be invoked explicitly.


