- **2026-11-09 · Production streams rebuilds only when the app asks**: under `dotnet watch`, hot reload is
  on in any environment but Production, which serves the relay only with `HotReload = true`
  ([#1297](https://github.com/eQuantic/equantic-ui/issues/1297)). Proposed and archived through OpenSpec
  (`openspec/specs/hot-reload`).
