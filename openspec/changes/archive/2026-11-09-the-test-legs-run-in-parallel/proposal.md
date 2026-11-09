## Why

Closes #1310 and #1312, two Tasks under #160 (Instruments that fail, not warn).

Measured on 2026-11-09 on run 42675312854: each leg of the `test` job ran the eleven test projects one
after another, 13.4 minutes on Ubuntu and 15.1 on Windows, and one project dominated: Conformance.Tests,
5 minutes 32 seconds on Ubuntu, then Web.Tests at 2 minutes 35. And no job cached `~/.nuget/packages`,
so every job of every run downloaded its packages again.

## What Changes

- Each system's tests run as three jobs side by side: Conformance, Web, and every other project found
  under `tests/`, grouped by `scripts/test-groups.sh`. A new project lands in `rest` without an edit to
  the workflow, a group that finds no project fails, and every red project is still named at the end
  of its job. The required checks follow the new names.
- The test jobs, `test (macos-latest)`, `build-packages` and `samples` restore NuGet packages through
  one composite action, `.github/actions/nuget-cache`, from a cache keyed on the system, the job and
  `scripts/nuget-cache-key.sh`: a hash of the lines that declare a package or a version, so an edit to a
  project that declares no package keeps the cache. The three test groups of a system share one. There
  is no fallback key, so a miss restores from nuget.org as before, and this repository's own packages
  (`equantic.*`) are left out of what is saved.
- `scripts/test-groups.sh` and `scripts/macos-tests.sh` find the test projects through one helper,
  `scripts/test-projects.sh`, so the two job families agree on which projects exist, and a `find` that
  fails fails them rather than handing them part of the tree.

Nothing changes for a developer using the SDK.

## Impact

`.github/workflows/ci.yml`, `scripts/test-groups.sh` (new), the Workflow section of CLAUDE.md and
AGENTS.md, CONTRIBUTING.md and the pull request template (which no longer count the required jobs, and
point at this spec's list), the `working-agreement` spec and two ledger entries. Ruleset 21198869 swaps
the two `test` legs for the six group jobs right after this merges: until then main's new runs
would not report the old names, and a pull request still on the old workflow is blocked anyway until
it merges main, which the up-to-date rule asks of it.
