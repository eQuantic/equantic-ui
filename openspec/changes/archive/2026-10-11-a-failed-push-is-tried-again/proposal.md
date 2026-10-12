# Proposal

Closes #759, a Bug under #160 (Instruments that fail, not warn). The release of
`0.2.0-preview.61` met it, and the manual workflow is Edgar's request after it.

## Why

Measured on the `v0.2.0-preview.61` tag, run 38132878034:

- `publish-nuget` pushed 33 packages, then nuget.org answered 500 three times to
  `eQuantic.UI.Native.Shell.Windows` (11:32Z). The step runs one `dotnet nuget push` per package
  under `set -euo pipefail`, and the client's own three tries fall within about seven seconds, so
  the job stopped there. The 17 packages after it in the list, `Primitives`, `Server`, `Sdk`,
  `Templates` and the six runtimes among them, were not pushed, and the release was on nuget.org
  without being restorable.
- Five reruns of the failed job each stopped on a package nuget.org already had, which answered 500
  where a duplicate answers 409. A rerun walks the list from the top, so it has to get through every
  package already published before it reaches one still owed, and none of them did.
- A one-off workflow on a throwaway branch (run 38137298726) pushed the 17 from the tag run's own
  artifact, one at a time with a pause between them and a wait before trying again. One package
  answered 500 three times and went through 30 seconds later. `release-github` had never run, so the
  release page was written by hand with that job's own command.

status.nuget.org reported every component operational throughout. `0.2.0-preview.59` lost the same
job to a runner kill after 46 of 50 packages. Both times the tree needed no correction, and the
pipeline had no way to finish the release from the packages it had already built.

## What Changes

- One script, `scripts/publish-packages.sh`, pushes a release's packages to nuget.org. It skips a
  package nuget.org already lists at that version, pauses between pushes, tries a push that fails
  again after a wait that grows, does not try again a push refused for the key, goes on to the rest
  of the list after a package it could not push, and fails at the end naming each one. A package at
  another version in the run's packages fails the publish before anything is pushed.
- `publish-nuget` runs that script, in place of its inline loop.
- A workflow run by hand, `Publish a release`, takes a run id, a tag and optionally the package ids.
  It refuses a run that is not the push of that tag, downloads that run's `nupkgs` artifact, runs the
  same script over the packages it was given, or
  over every package nuget.org does not list yet, and then writes the tag's release page. A release
  that stops halfway is finished from the packages its own run built, with no new version, and the
  key never leaves GitHub's secrets.
- One script, `scripts/release-page.sh`, writes the release page from the tag's annotation. The
  `release-github` job and the manual workflow both run it.
- `scripts/check-publish-packages.sh` runs the push loop against a stub `dotnet` and a stub index and
  asserts each promise above. A new CI job runs it on every pull request and on `main`, since the
  jobs that publish run only on a `v*` tag or by hand.
- CLAUDE.md's Version Management section says what to do when the publish stops halfway.

For a developer using the SDK nothing changes: no package, API or setting moves. For a release, a
5xx from nuget.org no longer leaves it half published, and one interrupted for a reason outside the
tree is finished in one manual run.

## Capabilities

### New Capabilities
- `release-publishing`: how a release's packages reach nuget.org, how a push that fails for a
  reason outside the package is handled, how a release interrupted halfway is finished from its own
  run's packages, and how its release page is written.

### Modified Capabilities
<!-- None: the working agreement's ruleset requirement names the jobs a pull request must pass, and
     the new check job is not added to it here (a ruleset change is the owner's). -->

## Impact

- `.github/workflows/ci.yml`: `publish-nuget` and `release-github` run the scripts, and one new job
  runs the check. The jobs the ruleset requires do not change.
- `.github/workflows/publish-release.yml`: new, `workflow_dispatch` only.
- `scripts/publish-packages.sh`, `scripts/release-page.sh` and `scripts/check-publish-packages.sh`:
  new.
- `CLAUDE.md` (Version Management) and `docs/LEDGER.md`.
- No eqc, runtime, realizer, shell, SDK or template change. The public surface
  (`PublicAPI.*.txt`) and the developer surface (`developer-surface.baseline.txt`) do not move.
