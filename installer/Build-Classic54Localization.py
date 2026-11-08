"""Adapt pinned 5.4 labels to Classic L(ru, en) calls; no network or machine translation.
Usage: python installer/Build-Classic54Localization.py /path/to/pinned/src/ui/Assets/Languages
The generated catalogue is embedded, so ordinary Classic builds do not need this script.
"""
import collections
import zipfile
import io
import json
import re
import sys
from pathlib import Path

repo = Path(__file__).resolve().parent.parent
source = Path(sys.argv[1])

def flatten(value, prefix=""):
    for key, item in value.items():
        path = prefix + "." + key if prefix else key
        if isinstance(item, dict):
            yield from flatten(item, path)
        elif isinstance(item, str):
            yield path, item

def normalize(value):
    return re.sub(r"\s+", " ", value.replace("&", "").replace("_", "").replace("…", "...").strip().rstrip(".: ")).lower()

def path_order(key):
    return (not key.startswith("general."), len(key), key)

english = dict(flatten(json.loads((source / "English.json").read_text(encoding="utf-8-sig"))))
russian = dict(flatten(json.loads((source / "Russian.json").read_text(encoding="utf-8-sig"))))
by_english = collections.defaultdict(list)
by_russian = collections.defaultdict(list)
for key, value in english.items():
    by_english[normalize(value)].append(key)
for key, value in russian.items():
    by_russian[normalize(value)].append(key)
lookup = {text: sorted(paths, key=path_order)[0] for text, paths in by_english.items() if text}
# Reviewed equivalences for Classic's retained wording of new 5.4 controls.
feature_aliases = {
    "Has errors": "edit.modifySelection.hasErrors",
    "From image…": "assa.imageColorPicker",
    "Keep music/lyric lines untranslated": "translate.keepMusicLinesUntranslated",
    "Move selected cues one frame back": "options.shortcuts.moveSelectedLinesOneFrameBack",
    "Move selected cues one frame forward": "options.shortcuts.moveSelectedLinesOneFrameForward",
    "Move selected and following cues one frame back": "options.shortcuts.moveSelectedLinesAndForwardOneFrameBack",
    "Move selected and following cues one frame forward": "options.shortcuts.moveSelectedLinesAndForwardOneFrameForward",
    "Move all cues one frame back": "options.shortcuts.moveAllLinesOneFrameBack",
    "Move all cues one frame forward": "options.shortcuts.moveAllLinesOneFrameForward",
    "Engine / models…": "general.models",
    "Engine and AI models…": "general.models",
    "Image colour picker": "assa.imageColorPicker",
}
for label, key in feature_aliases.items():
    if key not in english:
        raise ValueError("Unknown reviewed source key: " + key)
    lookup[normalize(label)] = key
composite_aliases = {
    "Refresh model list": ["general.refresh", "general.models"],
    "Second subtitle font": ["video.openSecondarySubtitleOnVideoPlayer", "general.fontName"],
}
for keys in composite_aliases.values():
    for key in keys:
        if key not in english:
            raise ValueError("Unknown reviewed source key: " + key)

pattern = re.compile(r'\b(?:L|CL|P)\(\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"')
call_sites = []
for file in (repo / "src/ui").rglob("*.cs"):
    text = file.read_text(encoding="utf-8-sig")
    for match in pattern.finditer(text):
        try:
            ru, en = (json.loads('"' + part + '"') for part in match.groups())
        except json.JSONDecodeError:
            continue
        paths = [lookup[normalize(en)]] if normalize(en) in lookup else []
        key = sorted(paths, key=path_order)[0] if paths else None
        call_sites.append({"file": file.relative_to(repo).as_posix(), "line": text.count("\n", 0, match.start()) + 1, "english": en, "sourceKey": key})
        if key and collections.Counter(re.findall(r"\{\d+(?:[^}]+)?\}", en)) == collections.Counter(re.findall(r"\{\d+(?:[^}]+)?\}", english[key])):
            lookup.setdefault(normalize(en), key)

# Upstream metadata has missing or historical culture names in these three files.
metadata_fixes = {"Dutch": "nl-NL", "Spanish": "es-ES", "Norwegian": "nb-NO", "Japanese": "ja-JP"}
locales = {}
aliases = {"zh-cn": "zh-hans", "zh-sg": "zh-hans", "zh-tw": "zh-hant", "zh-hk": "zh-hant", "jp": "ja-jp", "jp-jp": "ja-jp", "no": "nb-no", "no-no": "nb-no", "nb": "nb-no"}
locale_report = []
for file in sorted(source.glob("*.json")):
    data = json.loads(file.read_text(encoding="utf-8-sig"))
    culture = metadata_fixes.get(file.stem, data.get("cultureName"))
    if not culture:
        raise ValueError("Missing language culture: " + str(file))
    culture = culture.lower()
    flat = {key.lower(): value for key, value in flatten(data)}
    labels = {}
    for text, key in lookup.items():
        value = flat.get(key.lower())
        if value and collections.Counter(re.findall(r"\{\d+(?:[^}]+)?\}", value)) == collections.Counter(re.findall(r"\{\d+(?:[^}]+)?\}", english[key])):
            # WinForms buttons use &, Avalonia uses _ for mnemonics. Keep literal field labels.
            labels[text] = value.replace("_", "")
    for label, keys in composite_aliases.items():
        labels[normalize(label)] = " — ".join(flat.get(key.lower(), english[key]).replace("_", "") for key in keys)
    locales[culture] = labels
    aliases[culture] = culture
    base = culture.split("-")[0]
    if base not in aliases or culture in ("pt-pt", "zh-hans"):
        aliases[base] = culture
    locale_report.append({"name": file.stem, "culture": culture, "labels": len(labels)})

output = repo / "src/ui/Logic/Classic54Localization.zip"
with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for name, data in [("aliases.json", aliases)] + [(culture + ".json", labels) for culture, labels in locales.items()]:
        entry = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
        entry.compress_type = zipfile.ZIP_DEFLATED
        archive.writestr(entry, json.dumps(data, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
report = {"reference": "SubtitleEdit v5.4.0 57273b2", "catalogueLocales": locale_report, "literalCallSites": len(call_sites), "matchedCallSites": sum(bool(site["sourceKey"]) for site in call_sites), "uniqueEnglishStrings": len({site["english"] for site in call_sites}), "matchedUniqueEnglishStrings": len({site["english"] for site in call_sites if site["sourceKey"]}), "reviewedFeatureAliases": feature_aliases, "reviewedCompositeAliases": composite_aliases, "callSites": call_sites}
(repo / "docs/CLASSIC_54_LOCALIZATION_COVERAGE.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("Locales:", len(locales), "call sites:", report["literalCallSites"], "matched:", report["matchedCallSites"], "resource bytes:", output.stat().st_size)
