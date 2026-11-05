# Proposal

Closes #1224, a Task under #160 (Instruments that fail, not warn). It is the sequel of #759, which
made a release that stops halfway finishable, and of the day `0.2.0-preview.66` stopped on its key.

## Why

Measured on the `v0.2.0-preview.66` tag, run 41999444013: every job passed, the three test
platforms included, and then `publish-nuget` met nuget.org's
`403 (The specified API key has expired)` on each of the 52 packages. The publish script did what
`release-publishing` asks of a refused key, one push each and a failure naming all of them, so
nothing reached nuget.org and nothing had to be recut. The cause is outside the tree: nuget.org now
caps an API key's life at weeks, so a key stored in `secrets.NUGET_API_KEY` stops a release on a day
nobody chose, and renewing it is a chore nobody remembers until a release needs it.

nuget.org's trusted publishing removes the stored key. A policy on nuget.org names a repository, a
workflow file and an environment, and `NuGet/login` exchanges the run's GitHub OIDC token for a key
that lives one hour. `core-payment` publishes this way already.

## What Changes

- `ci.yml`'s `publish-nuget` and `publish-release.yml`'s `publish` run in the `nuget` environment,
  hold `id-token: write`, and log in with `NuGet/login` (pinned by commit) right before the push.
  The push reads the key from the login step's output. `secrets.NUGET_API_KEY` is read nowhere.
- `NUGET_USER`, an organization secret, is the nuget.org profile the login names.
- nuget.org holds two policies for this repository, one per workflow file, owner `eQuantic`,
  packages `eQuantic.UI.*`, scope "Push new packages and package versions". They exist already.
- `scripts/check-publish-packages.sh`, run by CI's `publish-packages` job on every pull request,
  fails when a job that runs `scripts/publish-packages.sh` reads a stored key, lacks the login,
  the `nuget` environment or `id-token: write`, or takes its key from anywhere but the login step.
  Four controls each break one of those in a copy of a workflow and must fail the guard.
- CLAUDE.md's Version Management section says where the key comes from, and what a refused login
  means.

Nothing an app writes moves: no public surface, no developer surface, no package changes. The
SDK's packages are published exactly as before, by the same script.

## Impact

- `.github/workflows/ci.yml`, `.github/workflows/publish-release.yml`
- `scripts/check-publish-packages.sh`
- `openspec/specs/release-publishing`: the key's source, and a requirement that no key is stored
- `CLAUDE.md`, and an entry under `docs/ledger/`
- The repository secret `NUGET_API_KEY` is no longer read. Deleting it is the owner's call, after the
  first release published this way.
