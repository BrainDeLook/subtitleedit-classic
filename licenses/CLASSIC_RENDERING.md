# Classic rendering and font runtime

The local five-block integration is adapted from the fixed Subtitle Edit v5.3.0 source snapshot under GPL-3.0. It does not track a moving main branch or assert complete upstream parity.

NuGet dependencies are pinned in `Directory.Packages.props`. The exact official package URLs and SHA-256 hashes used for the local candidate are recorded in `installer/classic-rendering-packages.json`. Restore the project normally for release builds; both x86 and x64 native libraries are included by the NativeAssets packages and the installer. Package licenses and bundled native-library notices are distributed from `licenses/classic-rendering/`.

- HarfBuzzSharp / Windows native assets 8.3.1.5: glyph shaping and font subsetting.
- SkiaSharp / HarfBuzz integration / Windows native assets 3.119.4: image subtitle rendering and export.
- System.Memory 4.6.3, System.Buffers 4.6.1, System.Numerics.Vectors 4.6.1 and System.Runtime.CompilerServices.Unsafe 6.1.2: .NET Framework 4.8 compatibility. The application configuration redirects Unsafe to assembly version 6.0.3.0.

Local verification uses synthetic media and bundled fixtures, without paid provider calls. A generated DVD subpicture stream verifies writing and sector reading; a physical DVD with real navigation, alternate angles and all IFO cell arrangements has not been field-tested. MXF VANC, ARIB captions and a complete rewrite of every legacy container parser are not claimed by this package. The ASS filter is a separate search/navigation window with MPV preview-layer visibility; it does not hide rows from the main editor grid. Animated ASS constructs remain best rendered through the existing libass hard-subtitle export rather than static PNG exports.

The full local regression run passed 2,936 assertions. Native activation of two hidden test windows was unavailable on the desktop; direct external-file reload and real MPV state retention passed. The x86 runtime passed 211 dedicated checks. Local package install, rollback and path-boundary rejection were tested against isolated folders.
