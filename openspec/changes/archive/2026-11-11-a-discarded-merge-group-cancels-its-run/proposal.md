# Proposal

Closes #1446, a Task under #160.

## Why

The merge queue discards a group when an entry ahead of it fails or leaves, and builds new groups behind it. It does not cancel the discarded group's CI run:
- **The concurrency never matches it.** Each group's ref is its own (`gh-readonly-queue/main/pr-N-<sha>`), so the concurrency of `ci.yml` (#1309) never groups that run with the one that replaced it.
- **Its macOS jobs keep waiting.** They queue for the plan's five macOS runners beside the live groups'.
- **Measured on 2026-11-11 at 18:58Z.** 11 runs of discarded groups were waiting or running, beside the 5 live groups' runs, and #1378 fell out of the queue twice by timeout without its macOS jobs ever starting.

## What Changes

- **The sweep.** `scripts/cancel-discarded-merge-groups.sh` cancels every merge-group run of `ci.yml` that is not finished and whose ref the queue deleted.
  - A group the queue still holds keeps its ref, and a group that merged has its commit on main, so neither run is touched.
  - The sweeping run's own is cancelled last, since cancelling it ends the step.
  - A lookup that fails any other way than a 404 cancels nothing and fails the sweep.
  - `--dry-run` names the runs without cancelling them.
  - `--self-test` proves every path against a stub of `gh`.
- **The job.** A new `discarded-merge-groups` job in `ci.yml` runs the self-test on every event, and the sweep on a merge group, a push to main and a run by hand. A sweep the API failed marks its step and not the run, so a blip never turns main red. Its token may cancel runs (`actions: write`). The run that sweeps is among the runs it reads, so a run whose own group is gone cancels itself.
- **Nothing else moves.** The job is not a required check, so the ruleset does not change.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `working-agreement`: a merge group the queue discarded does not keep its run.

## Impact

`scripts/cancel-discarded-merge-groups.sh` and `.github/workflows/ci.yml`.
