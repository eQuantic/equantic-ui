## Why

Closes #1309 and #1311, two Tasks under #160 (Instruments that fail, not warn).

Measured on 2026-11-09 on run 42675312854, a pull request's run: 24.0 minutes, 33.5 of them on macOS
runners. `build-packages` sets the run's wall time at 18.2 minutes: it waits for a macOS runner, packs in
about three minutes, then runs the whole test suite a third time, 13 minutes 38 seconds, the same eleven
projects the Ubuntu and Windows legs already ran, for the few facts only a Mac answers. And with no
`concurrency` block every push left the previous head's run going: #1251 started six runs that
afternoon, and three other pull requests four each, each holding three macOS runners while the others
waited three to seven minutes for one.

## What Changes

- `build-packages` builds, packs and checks the packages, and runs no test.
- A new job, `test (macos-latest)`, runs whole, with the Apple workloads installed, the test projects
  that hold what only a Mac answers, derived by `scripts/macos-tests.sh` and never listed: one that
  references the Apple track, or one whose test asks for macOS in its file. A project is the unit
  because a fact asks for a Mac in more ways than a scan of one file can see: a Metal device it probes,
  a workload it looks for, a helper in another file, a class nested in another. A project that runs no
  test fails, and a derivation that finds nothing fails. Publishing waits for it, and the ruleset
  requires it beside the other two `test` legs.
- The Ubuntu and Windows legs still run every project.
- `ci.yml` gets a `concurrency` group: a pull request's runs share one keyed on its number, with
  `cancel-in-progress`, so a new push cancels the run of the head it replaces; every other event gets
  a group of its own run and is never cancelled.

Nothing changes for a developer using the SDK. Edgar accepted the trade of coverage on 2026-11-09: the
whole suite keeps running on two systems, and macOS runs what only it can answer.

## Impact

`.github/workflows/ci.yml`, `scripts/macos-tests.sh` (new), `docs/ARCHITECTURE-AUDIT.md`, the Workflow
section of CLAUDE.md and AGENTS.md, CONTRIBUTING.md, the pull request template, the `working-agreement`
spec and two ledger entries. Ruleset 21198869 adds `test (macos-latest)` to its required checks when
this merges.
