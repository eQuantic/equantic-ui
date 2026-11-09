# Proposal

Closes #1221, a Bug under #206 (the fences of i18n v1 to revisit). Met by an app that runs in `pt-PT` (myartwork3d) when it took up
`FileUpload` in 0.2.0-preview.66: its upload page read "Choose files" and "Uploaded".

## Why

The SDK's component strings (`SdkStrings`, over `SdkResources`) had catalogs for the neutral culture
(English), `es` and `pt-BR`. .NET looks a resource up through the culture's parents, so an app in
`pt-PT` read `pt-PT`, then `pt`, then the neutral catalog, and never `pt-BR`: every built-in text of
`FileUpload`, `CookieConsent`, the spreadsheet, the code editor and the rest was English in it. The
same held for `pt-AO`, `pt-MZ` and every Portuguese culture but Brazil's. Nothing failed, because a
missing translation is answered by the neutral catalog, in English, by design.

## What Changes

- `SdkResources.pt.resx`, the neutral Portuguese catalog, holds all 98 keys in European Portuguese,
  the norm of every Portuguese-speaking country but Brazil, while `pt-BR` keeps its own. It ships in
  the package as the other catalogs do (a satellite assembly, and the resx beside the Designer under
  `tools/source`, by the existing glob).
- The browser needed no change: an app's build already writes a catalog per culture the SDK's
  catalogs name (`strings/pt.json` now), and the server and the runtime already walk a culture to its
  parent before the neutral catalog. A test pins that a `pt-PT` and a `pt-AO` page read `pt` while a
  `pt-BR` page keeps its own.
- `SdkStringsLocalizationTests` reads every catalog beside the neutral one and fails on a key it
  lacks, a key the neutral one lacks, or a placeholder that differs, and reads a component's text in
  `pt`, `pt-PT`, `pt-AO` and `pt-MZ`.
- The wiki's Localization page names the `pt` catalog, in all five languages.

Nothing an app writes moves.

## Impact

- `src/eQuantic.UI.Components/SdkResources.pt.resx`, a new catalog
- `tests/eQuantic.UI.Web.Tests`, `tests/eQuantic.UI.Server.Tests`
- `openspec/specs/sdk-strings`, a new spec
- The wiki's Localization page, and an entry under `docs/ledger/`
