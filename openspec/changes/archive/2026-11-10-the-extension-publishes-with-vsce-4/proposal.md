# Proposal

Closes #1271, a Task under #306, split from #1259.

## Why

GHSA-3g3m-2fjr-6pw4 (medium) reaches `glob` 13.0.6 in the extension's lockfile, held by
`@vscode/vsce` 3.9.2. The patched `glob` is a major away, and vsce 4.1.0 drops `glob` for
`tinyglobby`. The same release ships trusted publishing (`vsce publish --oidc`): the job's GitHub
token is exchanged for a Marketplace credential that lives fifteen minutes, so the job can publish
with no stored credential. Today it reads either a personal access token, which the Marketplace
retires on 2026-12-01, or three Azure secrets for Entra.

## What Changes

- `@vscode/vsce` moves to `^4.1.0`, and the lockfile follows: 171 packages where there were 324,
  `glob` and `@azure/identity`'s tree among those gone. `minimatch` stays, at `^10.2.6`, and is #1259's.
- `publish-extension` publishes with `--oidc` and reads no secret. `VSCE_PAT`, `AZURE_CLIENT_ID`,
  `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID`, and the `azure/login` step, are gone.
- `scripts/check-publish-packages.sh`, run by CI on every pull request, fails a workflow whose
  `publish-extension` reads a secret, publishes without `--oidc` or is not granted `id-token: write`,
  with a control per case.
- **A break for the maintainer, not an app**: the first `ext-v*` tag after this needs a Trusted
  Publishing policy on the publisher's page in the Marketplace, naming this repository and
  `.github/workflows/ci.yml`. Without it the job fails, and never falls back to a token.

Nothing an app writes changes. Neither the public surface nor the developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `working-agreement`: the extension is published with no stored credential.

## Impact

`extensions/vscode/package.json`, `package-lock.json` and `README.md`, the `publish-extension` job of
`.github/workflows/ci.yml`, and `scripts/check-publish-packages.sh`.
