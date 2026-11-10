- **2026-11-09 · The extension's tooling moves past the qs advisory**: Dependabot alert #90
  (GHSA-6pq8-jhm2-2vcw, medium) reached `qs` 6.15.3 in the VS Code extension's lockfile, through
  `@vscode/vsce` and `typed-rest-client`, whose range already held the patched release. The lockfile
  now resolves `qs` 6.17.1, with nothing else moving, and `npm audit` lists `qs` no more. The other
  alert of the issue, `minimatch` below 11.0.1, waits for a release of vsce that accepts 11: 3.9.2
  and 4.1.0 both ask for 10, and no 10.x release is patched
  ([#1259](https://github.com/eQuantic/equantic-ui/issues/1259)). Proposed and archived through
  OpenSpec, with no spec moved: the change is in the extension's lockfile alone.
