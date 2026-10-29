# Proposal

Closes #1178, a Task under #306 (the security hardening review).

## Why

Dependabot reports seventeen open advisories against `extensions/vscode/package-lock.json`, five
high. Sixteen of them, four high, close with a version inside the ranges the extension's tooling already
declares: `undici` 7.29.0 (seven advisories, through `@vscode/vsce` → cheerio), `markdown-it` 14.3.0
(four, through vsce), `fast-uri` 3.1.7 (four, through vsce → ajv, held by the manifest's
`overrides`) and `brace-expansion` 5.0.9 (one, through minimatch).

None of them reaches a developer. The extension declares no `dependencies`, and its `.vscodeignore`
leaves `node_modules/**` out of the `.vsix`. They run where the extension is packaged, in CI's
`package-extension` job, and `vsce package` feeds them the extension's own README and changelog.

The seventeenth, `@azure/msal-node` (GHSA-xcx6-jx8p-x3h5), has no patched version that a released
`@azure/identity` accepts, and is #1179.

## What Changes

- The lockfile records `undici` 7.30.1, `markdown-it` 14.3.3, `fast-uri` 3.1.9 and
  `brace-expansion` 5.0.12. No other package moves.
- The `fast-uri` override's floor rises from `^3.1.6` to `^3.1.9`, the patched version, so the lock
  cannot settle back below it.
- Nothing a developer using the SDK or the extension writes or meets changes.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none: the change is in the extension's build tooling alone, and no requirement of the product
moves, so the change sets `skip_specs`.)

## Impact

- **Extension tooling**: `extensions/vscode/package.json` and `package-lock.json`.
- **Public surface**: none. The developer surface does not move, and neither does the `.vsix`'s
  content.
