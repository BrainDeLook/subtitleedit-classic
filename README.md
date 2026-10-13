# Subtitle Edit Classic

A fork of the maintained [Subtitle Edit 4.x legacy branch](https://github.com/SubtitleEdit/subtitleedit/tree/se4-legacy), preserving the original Windows Forms interface.

[Download the classic build](https://github.com/BrainDeLook/subtitleedit-classic/releases/latest) · [Upstream project](https://github.com/SubtitleEdit/subtitleedit) · [GPL-3.0 license](LICENSE.txt)

## Base version

Based on `se4-legacy` at `96e6d302659e94e77564b98805d37e52c80d6501` (2026-10-10), including 22 commits after the last stable 4.x release, 4.0.16. The default branch of this fork is `se4-legacy`; it retains the classic interface and does not migrate to the 5.x interface.

## MP4 burn-in fix for Adobe Audition

The hard-subtitle exporter enables MP4 edit lists (`-use_editlist 1`) instead of disabling them. This allows the MP4 muxer to account for AAC encoder delay without stretching the first video sample.

For the reported 23.976 fps H.264/AAC exports, disabling edit lists produced a first sample of 1513 ticks instead of 1001 ticks at a 1/24000 time base. Correcting the video timing made playback work in Audition without changing the compressed H.264 or AAC data. See [upstream issue #15992](https://github.com/SubtitleEdit/subtitleedit/issues/15992) for reproduction and controlled test results.

This fork changes that export parameter in the source code. The editor, classic panels, subtitle styling, and encoder selections are retained from the legacy branch.

## Usage

Extract the portable release ZIP into its own folder and run `SubtitleEdit.exe`. Keep `Settings.xml` next to the EXE for separate portable settings. Configure or download FFmpeg when prompted, then generate burned-in subtitles normally using H.264 and AAC in MP4.

Previously exported files must be regenerated or repaired separately. Playback of a complete new export from this build should be checked in Audition at the previously affected positions; the muxer timing correction has been verified independently.

## Build from source

On Windows with a .NET SDK and .NET Framework 4.8 / 4.8.1 support:

```powershell
dotnet build SubtitleEdit.sln -c Release -p:SolutionDir="$PWD\"
```

The solution builds the original Windows Forms application. Packaging follows the upstream portable distribution layout, using the newly compiled EXE and libraries.

## По-русски

Это классический Subtitle Edit 4.x со свежими изменениями из официальной ветки `se4-legacy` и исправлением таймингов при создании MP4 с хардсабом и AAC. Интерфейс остаётся прежним. Используйте portable-сборку из раздела Releases в отдельной папке.

Original Subtitle Edit is developed by Nikolaj Olsson and contributors. This fork retains the upstream GPL-3.0 license and attribution.
