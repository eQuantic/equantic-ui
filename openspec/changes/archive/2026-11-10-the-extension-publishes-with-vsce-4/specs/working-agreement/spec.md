## ADDED Requirements

### Requirement: The extension is published with no stored credential

The extension's Marketplace publish SHALL authenticate through the Marketplace's trusted publishing,
with `vsce publish --oidc` and the job's own OIDC token, and the job that publishes SHALL read no
secret. A check run on every pull request SHALL fail a workflow that breaks either.

#### Scenario: A stored token comes back

- **WHEN** a change makes `publish-extension` read `secrets.VSCE_PAT`
- **THEN** `scripts/check-publish-packages.sh` fails, saying the job reads a stored credential

#### Scenario: The job loses its token

- **WHEN** `publish-extension` is not granted `id-token: write`
- **THEN** the check fails, saying so
