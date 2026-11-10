- **2026-11-09 · The extension's tooling moves past the MSAL Node advisory**: Dependabot alert #87
  (GHSA-xcx6-jx8p-x3h5, high) reached `@azure/msal-node` 5.5.0 in the VS Code extension's lockfile,
  through `@vscode/vsce` and `@azure/identity` 4.13.1, and when the issue was filed no released
  `@azure/identity` accepted the patched 7.1.0, so it waited rather than take an `overrides` entry over
  a dependency's declared range. `@azure/identity` 4.14.1 (2026-10-29) asks for `^7.1.0`, and the
  lockfile resolves it and `@azure/msal-node` 7.2.1 with nothing outside that package's tree moving.
  `npm audit` went from two high advisories to none, and the extension still packages with its host
  answering ([#1179](https://github.com/eQuantic/equantic-ui/issues/1179)). Proposed and archived
  through OpenSpec, with no spec moved: the change is in the extension's lockfile alone.
