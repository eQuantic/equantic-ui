## Why

Closes #1309, a Task under #160 (Instruments that fail, not warn).

Measured on 2026-11-09: `ci.yml` has no `concurrency` block, so a push to a pull request leaves the run
of the previous head going to the end. That afternoon #1251 started six runs, and three other pull
requests four each. Every run holds three macOS runners (`build-platform-runtimes`, `samples` and
`build-packages`), the scarcest kind, so a stale run made every other pull request wait three to seven
minutes in the macOS queue before `build-packages` started.

## What Changes

- `ci.yml` gets a `concurrency` group: a pull request's runs share one keyed on its number, with
  `cancel-in-progress` true, so a new push cancels the run of the head it replaces.
- A run on `main`, on a tag, on a merge group (#1308) or by hand gets a group of its own run id, so it
  is never cancelled and never queued behind another.

Nothing changes for a developer using the SDK. The change is the repository's own CI.

## Impact

`.github/workflows/ci.yml`, the `working-agreement` spec and one ledger entry.
