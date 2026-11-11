## 1. Compression

- [x] 1.1 `ResponseCompressionFirst`. Verify: `ResponseCompressionTests` reads `Content-Encoding` and the same bytes for the runtime and a module, and fails with the filter taken out; a stream of events is no type it compresses

## 2. The code palette

- [x] 2.1 `ThemeBridge` writes it and `theme-bridge.ts` answers from it. Verify: `ATheme_ThatOverridesCode_CarriesItsPalette` and `theme-bridge.spec.ts`, and the materialized Photon theme answers every kind as `photonTheme` does

## 3. Processes

- [x] 3.1 `ChildOutput` for the design host, and the bounded read and the build tools' environment for hot reload's MSBuild. Verify: `ChildOutputTests`, `AnMsBuildThatLeavesItsOutputHeld_DoesNotHoldTheSaveThatAsked` and `WhatTheServerStarts_LeavesNoBuildServerBehind`

## 4. Against the real thing

- [x] 4.1 The Server, Web and Design suites, `TestRuntime`, and a sample served with `curl`

## 5. Documentation

- [x] 5.1 One entry for the ledger citing the three issues, as a file of its own under `docs/ledger/`. No wiki page says otherwise
