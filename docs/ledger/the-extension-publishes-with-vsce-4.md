- **2026-11-10 · The extension publishes with vsce 4, through trusted publishing**: `@vscode/vsce`
  3.9.2 held `glob` 13.0.6, which GHSA-3g3m-2fjr-6pw4 reaches, and vsce 4.1.0 drops it. The tree went
  from 324 packages to 171. The same release ships `vsce publish --oidc`, so `publish-extension` now
  exchanges the job's token for a credential that lives fifteen minutes, and reads none of the four
  secrets it took, one of them a token the Marketplace retires on 2026-12-01.
  `check-publish-packages.sh` fails a workflow that brings one back
  ([#1271](https://github.com/eQuantic/equantic-ui/issues/1271)). Proposed and archived through
  OpenSpec (`openspec/specs/working-agreement`).
