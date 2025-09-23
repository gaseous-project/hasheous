# Localisation Guide

## Overview
The application uses flat JSON language packs stored under `hasheous/wwwroot/localisation/`. Base language files (e.g. `en.json`) contain the full key set; regional overlay files (e.g. `fr-CA.json`) contain only the keys that differ. Missing keys fall back through a structured chain to `en`.

## Loading & Fallback
Runtime localisation is handled by `wwwroot/scripts/language.js`:
1. Determine locale: cookie `userLocale` (if present) else browser language.
2. Construct fallback chain: region/script → base language → `en`.
3. Load the first available base language file.
4. Attempt overlay candidates (first successful loads and short-circuits).
5. Expose loaded sequence via `resolvedChain` for debugging.

Chinese scripts use explicit script bases:
- `zh-Hans.json` (Simplified)
- `zh-Hant.json` (Traditional)
Regional overlays (e.g. `zh-CN.json`, `zh-TW.json`, `zh-HK.json`) stay minimal.

## Pluralisation (Automatic)
`getLang` now auto-selects plural forms if you pass a number (or an array whose first element is numeric):
- Looks for `<token>_one` when count == 1, otherwise `<token>_other`.
- Falls back to `<token>` if specialised keys are missing.
- Existing `getPlural(baseKey,count,subs)` remains available but is optional.

Example keys:
```
unique_visitors_one
unique_visitors_other
```
Usage:
```
lang.getLang('unique_visitors', 1);    // 1 unique visitor
lang.getLang('unique_visitors', 42);   // 42 unique visitors
```

## Adding a New Language
1. Copy `en.json` to `<lang>.json` and translate all values.
2. (Optional) Add overlay files `<lang>-<REGION>.json` containing only divergent keys.
3. Run the validator (see below) to confirm coverage.

## Validator CLI
The CLI provides a locale coverage check:
```
hasheous-cli locales validate
```
Specify a custom path if needed:
```
hasheous-cli locales validate C:\\path\\to\\localisation
```
Output per locale:
- Coverage (% of base `en.json` keys present)
- Missing key count (truncated list)
- Extra key count (unexpected keys vs base)

Planned enhancements (not yet implemented):
- `--fail-on-missing` for CI pipelines
- JSON report output
- Plural integrity check (ensure both `_one` and `_other` exist together)

## Overlay Strategy
Keep overlays sparse. Only add keys that change (spelling, terminology, formality). All other strings inherit from the base language file or fall back to `en`.

## Current Language Coverage
Base + overlays presently included:
- English: `en`, `en-AU`, `en-GB`, `en-CA`
- German: `de`, `de-DE`, `de-AT`, `de-CH`, `de-LU`, `de-LI`
- French: `fr`, `fr-FR`, `fr-CA`, `fr-BE`, `fr-CH`, `fr-LU`, `fr-MC`
- Spanish: `es`, `es-ES`, `es-MX`, `es-AR`, `es-CL`, `es-CO`, `es-PE`, `es-VE`, `es-EC`, `es-UY`, `es-US`, `es-419`
- Portuguese: `pt`, `pt-PT`, `pt-BR`, `pt-AO`, `pt-MZ`
- Italian: `it`, `it-IT`
- Japanese: `ja`, `ja-JP`
- Russian: `ru`, `ru-RU`
- Polish: `pl`, `pl-PL`
- Dutch: `nl`, `nl-NL`, `nl-BE`
- Turkish: `tr`, `tr-TR`
- Korean: `ko`, `ko-KR`
- Chinese: `zh-Hans` (Simplified), `zh-Hant` (Traditional) + overlays `zh-CN`, `zh-TW`, `zh-HK`
- Nordic & CEE & Others (base only unless noted): `sv`, `nb`, `da`, `fi`, `cs`, `hu`, `el`, `ro`, `uk`, `vi`, `th`, `id`

## Key Management Guidelines
- Treat `en.json` as canonical key inventory.
- Avoid deleting keys; add new ones for UI changes to prevent breaking older locale files.
- Use consistent lowercase keys (loader normalises lookups with `toLowerCase()`).

## Future Improvements (Backlog Ideas)
- Advanced plural rules (e.g. Slavic, Arabic) via CLDR mapping
- CI enforcement using validator with non-zero exit on missing keys
- Extraction of long HTML policy blocks into smaller semantic keys
- Accessibility / ARIA text keys centralised in locale files
- Right-to-left (RTL) support and direction metadata prior to adding RTL locales

## Troubleshooting
| Symptom | Possible Cause | Resolution |
|---------|----------------|-----------|
| Token displays raw key name | Key missing in overlay & base | Add to base language file (`en.json`) or correct key spelling |
| Plural not switching | `_one` / `_other` keys missing | Add both forms or rely on base key only |
| Overlay changes not visible | Browser cached JSON | Hard refresh (Ctrl+F5) or append cache-busting query param |

## Contributing Translations
1. Fork or create a branch.
2. Add/update locale JSON files.
3. Run validator: `hasheous-cli locales validate`.
4. Commit with message: `i18n: add <lang/region> translations`.
5. Open PR; include translator credit if desired.

---
For questions or to propose enhancements to localisation infrastructure, open an issue or include changes with an update to this guide (PR guard expects architecture/docs changes to be reflected).

## Localisation Endpoint (Enumeration API)
The UI dynamically builds the language selector by calling:

`GET /api/v1/localisation/`

Details:
- Hidden from Swagger (internal utility endpoint).
- Returns a JSON array grouped by base language with each locale entry: `{ code, type, display, file }`.
- Response is cached in-memory for the lifetime of the process; restart the app (or implement future invalidation) to pick up new/changed locale files.
- ETag header (SHA-256 of the serialized payload) is emitted. Clients sending `If-None-Match` with the current tag receive `304 Not Modified` with no body, reducing bandwidth.
- Spelling uses Australian English (`localisation`). The previous American spelling alias has been removed.

Example minimal response shape:
```
[
	{
		"language": "en",
		"baseDisplay": "English",
		"locales": [
			{ "code": "en", "type": "base", "display": "English", "file": "en.json" },
			{ "code": "en-AU", "type": "regional", "display": "English (AU)", "file": "en-AU.json" }
		]
	}
]
```

ETag usage example (PowerShell):
```
$r = Invoke-WebRequest https://localhost:7157/api/v1/localisation/
$etag = $r.Headers.ETag
# Subsequent conditional request
Invoke-WebRequest https://localhost:7157/api/v1/localisation/ -Headers @{ 'If-None-Match' = $etag }
```

