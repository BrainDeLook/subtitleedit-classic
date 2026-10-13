# Accepted local baseline: Classic 4.0.18.3

Checkpoint accepted by the user on 2026-10-13. No new GitHub release is requested; further work remains local until explicitly approved for publication.

The base is the maintained Windows Forms `se4-legacy` branch at `96e6d302659e94e77564b98805d37e52c80d6501`. The feature source for this stage is the stable `v5.2.0` tag. Experimental 5.3 features are outside this stage.

## Completed burn-in work

- Preserve the classic editor and export window; add Format and a separate Advanced dialog.
- Container-dependent AAC, AC-3, MP3, Opus, Vorbis and source-audio copy selections.
- Compatible MP4/MKV/MOV/TS/WebM output selections for the existing Windows video encoders.
- Advanced text alignment, margins, shadows, boxes and letter spacing for ordinary text subtitles; preserve ASS file styles.
- Export-time text effects, PNG logo placement/size/opacity, and direct Blu-ray SUP overlays with cut-offset handling.
- Audio-only input with a generated background and per-input source-size two-pass encoding.
- Preserve the Audition MP4/AAC timing fix (`-use_editlist 1` for MP4/MOV).
- Restore language compatibility independently of the fork release number.
- Progress/idle footer visibility and placement fixes; keep manual target sizes in integer MB.

## Validation and accepted limits

Local compilation uses the installed dependency assemblies and unchanged embedded resources. Tests covered all 24 offered audio/container combinations with independently decodable audio/video, representative codec exports, logo and SUP pixels, audio-only input, two passes, and the actual classic export pipeline. Tested MP4 video samples were uniformly 1001 ticks at a 1/24000 time base. Idle and active footer states were rendered and checked in single/batch mode at two window sizes.

These automated short-clip checks do not prove every full-length source file or every GPU/driver combination works in Audition. The user accepted this checkpoint as the starting point for further feature comparison.

Installed binary version: `4.0.18.3`.

SHA256: `133B31674DDB303EA82B1341155842035893FBCED86530A307B006C62E4398B7`.

An exact binary copy and checksum are saved outside the repository in the workspace checkpoint directory. Existing rollback copies remain available. No portable redistribution or new release was created.
