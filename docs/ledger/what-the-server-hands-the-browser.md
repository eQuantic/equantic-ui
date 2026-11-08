- **2026-10-30 · What the server has reaches the browser, and no child holds it**: an app's runtime and modules are
  compressed with no line of the developer's, where `AddUI` registered the providers and nothing applied them, 939,966
  bytes of `runtime.js` on the blank template ([#1002](https://github.com/eQuantic/equantic-ui/issues/1002)). A theme's
  code palette crosses with the rest of `IAppTheme` ([#936](https://github.com/eQuantic/equantic-ui/issues/936)). The
  design host drains its renderer under one deadline, and a failed rebuild's output no longer holds hot reload
  ([#997](https://github.com/eQuantic/equantic-ui/issues/997)). Proposed and archived through OpenSpec
  (`openspec/specs/served-assets`, `openspec/specs/design-system`, `openspec/specs/hot-reload`).
