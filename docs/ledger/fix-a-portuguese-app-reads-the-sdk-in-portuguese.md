- **2026-11-06 · A Portuguese app outside Brazil reads the SDK in Portuguese**: the SDK's strings had
  catalogs for English, `es` and `pt-BR`, and .NET walks `pt-PT`, `pt-AO` and every Portuguese
  culture but Brazil's through `pt`, never through `pt-BR`, so an app in `pt-PT` read every built-in
  text of the components in English ("Choose files" on myartwork3d's upload page). The SDK ships a
  neutral `pt` catalog in European Portuguese, all 98 keys, which a build writes as `strings/pt.json`
  and the server and the browser reach by the walk they already did. `SdkStringsLocalizationTests`
  fails on a catalog that lacks a key, carries one the neutral catalog does not or differs in a
  placeholder ([#1221](https://github.com/eQuantic/equantic-ui/issues/1221)). Proposed and archived
  through OpenSpec (`openspec/specs/sdk-strings`, a new spec).
