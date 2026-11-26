## Why

Closes #1922, a Bug under #1587, found when #1913 met the ceiling on 2026-11-26.

`ServedRuntimeBudgetTests` held the served runtime to the base plus each change's delta and failed only past 1% over
that. A change that grew the runtime by less than the margin passed and recorded nothing. Growth piled up unattributed,
and the first change to cross the line paid for it. `main` grew 3,907 bytes gzipped that way after its last recorded
delta. #1913, whose own growth was 106 bytes, met the ceiling, and the record its measurement wrote said 4,013: the
delta was measured against the base and every other delta, so it took in everyone's bytes. #1923 recorded the 3,907
apart, measured merge by merge, and #1852's own 4,530 had already held 3,685 that were not its own.

The margin deferred the accounting instead of absorbing noise. Since #1489 a delta file cannot conflict with another's,
so the reason for leaving small growth unrecorded is gone.

## What Changes

Nothing an app writes or serves changes. A pull request that grows the runtime records its growth, however small.

- **A change is measured against the commit it is measured from** (`ChangeGrowth`), not against the budget:
  - `EQ_RUNTIME_BASE`, when given;
  - on a named branch, where it leaves `origin/main`;
  - anywhere else, the checkout's first parent: `main` in a pull request's merge ref, what the queue already holds in a
    merge queue's run, the commit before on `main`.
  That commit's runtime is built as the Server's `BundleRuntime` builds its own: its sources from `git archive`, the
  embedded bun, and the arguments read from its own Server project.
- **Growth owes a delta of its own** (`AChangeThatGrowsTheRuntime_RecordsItsOwnDelta`). A change that grows the runtime
  by any byte and records none fails, naming the bytes and the variables that record them. A delta it records must be
  what it grew, within the noise margin. A change that grows nothing owes nothing, whatever `main` grew around it.
- **The record is the change's own growth.** `EQ_UPDATE_RUNTIME_BUDGET=1` writes the bytes the change grew the runtime
  by against that commit, not what its measurement added to the base and the other deltas.
- **The 1% stays as the margin for noise.** The runtime past 1% over the base and every delta still fails. Deltas each
  measured beside a `main` of its own do not add up byte for byte once they meet.
- **The release's fold writes the rows.** `fold-runtime-budget.sh` writes every delta as a row of
  `docs/SERVED-RUNTIME-GROWTH.md`, under the release's version, with the pull request whose commit added it. Every
  change that grows the runtime has a delta, so the page's rows are the deltas. No two pull requests meet on one line of
  the page between releases, the conflict #1489 took away and #1913 and #1923 met again there.
- **CI's test job fetches two commits**, so the first parent is there to build.
- **One rule for the embedded bun** (`tests/Shared/EmbeddedBun.cs`). It picks the zip as the SDK does, for the
  budget's base and for the conformance harness.

Parts reached: the Server's test suite, the conformance harness's bun, the fold script and its check, CI's test job,
and the working agreement's wording in `CLAUDE.md` and `AGENTS.md`. No contract or developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `working-agreement`: the served runtime's budget takes a delta for every growth, however small.

## Impact

- `ServedRuntimeBudgetTests` gains the guard and its rule's cases, and its record test reads the new meaning.
- `check-release-folds.sh` asserts the fold's rows, against main's fold, which writes none.
- Proven on a branch: a probe of 63 bytes failed the guard with no delta and passed the old margin. Recording 63 bytes
  made it pass.
