# Subtitle Edit Classic

**For people who want up-to-date subtitle tools without giving up the familiar Classic interface.**

Keep the Windows Forms editor, panels and shortcuts you know, with modern speech, translation, OCR and video workflows adapted from Subtitle Edit 5.x.

[![Download stable](https://img.shields.io/github/v/release/BrainDeLook/subtitleedit-classic?label=Download%20stable&color=0078D4)](https://github.com/BrainDeLook/subtitleedit-classic/releases/latest)
[![Windows build](https://github.com/BrainDeLook/subtitleedit-classic/actions/workflows/classic-build.yml/badge.svg?branch=se4-legacy)](https://github.com/BrainDeLook/subtitleedit-classic/actions/workflows/classic-build.yml)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4)](#installation)
[![License](https://img.shields.io/badge/license-GPL--3.0-3DA639)](LICENSE.txt)

**English** · [Русский](README.ru.md)

[Download](https://github.com/BrainDeLook/subtitleedit-classic/releases/latest) · [Changelog](CLASSIC_CHANGELOG.md) · [Report a bug](https://github.com/BrainDeLook/subtitleedit-classic/issues) · [Upstream](https://github.com/SubtitleEdit/subtitleedit)

## What is this fork?

Subtitle Edit Classic is an independent Windows fork of the [Subtitle Edit 4.x legacy branch](https://github.com/SubtitleEdit/subtitleedit/tree/se4-legacy). It is for users who prefer the established Classic workspace and want newer capabilities without switching to the 5.x interface.

The project keeps Classic's editing workflow and brings newer tools into its existing windows. Menus group commands by task, and shared settings are available from relevant context menus. Existing Classic commands and shortcuts remain available.

The current stable release is **4.0.28**. The fork has its own release numbering and update source. It adapts features from upstream 5.0–5.3; it is not the upstream 5.3 application or a guarantee of identical behavior in every workflow.

## What changes compared with legacy 4.x?

| Area | Additions in this fork |
| --- | --- |
| **Speech recognition** | Expanded Whisper/Crisp ASR catalog, additional VAD choices, speech separation, Sortformer speaker detection and reviewed word-alignment corrections. |
| **Speech and dubbing** | Local TTS engines, reference-voice cloning, actor casts, playback from the selected line, voice management and export to a chosen folder. Original-speech removal can target all speech or generated cue intervals. |
| **Translation and AI** | Modern provider integrations, local MADLAD/llama.cpp, Ollama and compatible servers, terminology and prompts, reviewable AI text suggestions and batch translation. |
| **OCR** | Burned-in video OCR, additional bitmap OCR engines, nOCR matching/training tools and database optimization. |
| **Timing and comparison** | Waveform-assisted timing previews, timing profiles, richer visual/point sync, editable subtitle comparisons and manual pairing. |
| **ASS/SSA** | Actor/style pickers, font collection and trimming, advanced effects and drawing tools, text/style/actor filters and preview-layer controls. |
| **Everyday editing** | Visual grid formatting, Russian live spelling and correction menus, configurable columns, forced-line marking/export, assisted split/move, richer statistics and error reports. |
| **Video and audio** | Extended hard-subtitle export, named audio-track selection, per-track stereo conversion, cuts/joins, stream/chapter tools and an MP4/AAC timing fix for Adobe Audition. |
| **Formats and automation** | Additional subtitle/broadcast formats, mapped table import/export, image exports, batch presets and a separate `seconv.exe` command-line converter. |
| **Navigation and playback** | Task-based menus, command palette, shortcut sequences, waveform modes, spectrogram caching, an experimental FFmpeg player and actor/layer timeline. |
| **Music** | ACE-Step background-music generation with optional mixing into selected audio/video tracks. |

These are additions to the existing Classic editor: its subtitle editing, synchronization, spelling and established format support remain part of the application.

## Installation

1. Open [the latest stable release](https://github.com/BrainDeLook/subtitleedit-classic/releases/latest).
2. Download and run **`SubtitleEdit-Classic-<version>-Setup.exe`**. `SHA256SUMS.txt` is available on the same release page.
3. Open the tool you need and download its engine/model when prompted. You do not need to install the entire AI catalog.

The installer uses the **Subtitle Edit Classic** application name, installation folder and shortcuts. Updating an existing Classic installation normally preserves settings. The built-in update checker follows **this fork's stable releases**.

> **Application data:** the standard installation uses `%APPDATA%\Subtitle Edit` for settings, dictionaries, downloaded models and caches. This location can also be used by an original Subtitle Edit installation. Removing it resets those settings and removes local resources; it is not required for an ordinary update.

## Engines, models and external services

Large speech, OCR, translation and music models are **downloaded on demand**, separately from the installer. Available languages, voice cloning and CPU/Vulkan/CUDA support depend on the selected engine and model. Installing a catalog entry does not imply that it supports every language or GPU.

Examples of integrated TTS families include Qwen3, Supertonic, Kokoro, F5-TTS, Pocket TTS, VibeVoice, OmniVoice, Fish Audio, IndexTTS, Higgs Audio, FireRed, MOSS-TTS, Dots-TTS and Confucius. Use the in-app catalog for the available runtimes and model choices.

Local tools run their selected engine on your computer. API-backed tools send the requested text, audio or images to the configured provider and may require a key and payment. Models have their own licenses; some restrict commercial use. Voice cloning requires a reference voice you are entitled to use and, for some engines, an exact reference transcript.

## Status and verification

This remains a **Windows/.NET Framework Classic application**. It does not replace the UI with upstream's cross-platform interface. The FFmpeg player and track timeline are experimental; hardware decoding and large-model performance depend on the machine.

Automated and local checks cover editor behavior, real media outputs, installed-package startup, format round trips and Russian/English UI behavior. **Paid API calls, physical DVD hardware and fresh inference with every large model are not included in release validation.** DVD import can use files without a physical drive.

## Documentation

- [Changelog](CLASSIC_CHANGELOG.md) — releases and detailed development history; kept outside the README.
- [5.3 integration report](LOCAL_53_INTEGRATION.md) — implemented routes, verification and known boundaries.
- [Hands-on checks, in Russian](LOCAL_53_CHECK.ru.md) — where to find and try the recent features.
- [ASS, fonts, formats and media checks](CLASSIC_FIVE_BLOCKS_CHECK.md) — the combined feature check guide.
- [Build from source](docs/BUILD.md) — Windows build and installer instructions.
- [Rendering dependencies and limitations](licenses/CLASSIC_RENDERING.md).

## Bug reports

[Open an issue in this repository](https://github.com/BrainDeLook/subtitleedit-classic/issues) with the Classic version, steps to reproduce, expected/actual behavior and the chosen engine/player when relevant. A small sample subtitle or media clip helps. Remove API keys and personal information before attaching logs.

## Credits and license

Subtitle Edit was created by **Nikolaj Olsson and the Subtitle Edit contributors**. This fork builds on their work and the maintained 4.x legacy branch, using upstream base commit [`96e6d30`](https://github.com/SubtitleEdit/subtitleedit/commit/96e6d302659e94e77564b98805d37e52c80d6501). The Classic fork is maintained in [BrainDeLook/subtitleedit-classic](https://github.com/BrainDeLook/subtitleedit-classic).

Distributed under [GPL-3.0](LICENSE.txt). Third-party engines, models, dictionaries and libraries retain their respective licenses and notices in [licenses](licenses).
