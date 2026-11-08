# Localisation of new Classic controls

The additional Classic controls use the pinned Subtitle Edit **v5.4.0 (`57273b2`)** translations through `ClassicLocalization.Text(russian, english)`. Existing Classic XML language files and their settings remain in use. Russian and English labels supplied by the fork are preserved exactly.

The embedded catalogue includes **36 upstream locales**, including Romanian and Croatian. It preserves Portuguese/Brazilian Portuguese and Simplified/Traditional Chinese, and adapts the historical Japanese and missing Dutch/Norwegian/Spanish culture metadata. Only the requested locale is deserialised; changing the language takes effect in subsequent calls without restarting the process.

Reviewed aliases cover the new error-selection rule, image colour picker, dictionary uninstall, frame-movement scopes, music-line preservation, remote-model refresh and second-subtitle font label. Other matching labels use the source English key. The catalogue is built from the upstream translation files; it does not submit text to a translation service. Upstream machine-translated drafts retain their original quality limitations.

## Coverage boundary

The generated [coverage inventory](CLASSIC_54_LOCALIZATION_COVERAGE.json) records the literal bilingual call sites and their source keys. At generation time, **601 of 1,316 sites (452 of 1130 unique English strings)** match a pinned key or a reviewed alias. This is not a claim that every fork-only explanation has a translation in all languages. Unmatched fork-specific explanations fall back to English; the Russian/English pair is still available. The file is an explicit record of those remaining strings rather than a blanket “all languages complete” assertion.

Recreate the deterministic embedded catalogue with:

```powershell
python installer/Build-Classic54Localization.py <pinned-v5.4.0>/src/ui/Assets/Languages
```

Ordinary SDK builds use the checked-in resource and do not require Python or an upstream checkout.
