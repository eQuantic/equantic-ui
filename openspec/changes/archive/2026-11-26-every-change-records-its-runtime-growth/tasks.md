## 1. Every growth records its delta (#1922)

- [x] 1.1 `ChangeGrowth`: the commit a change is measured from, that commit's bundle as BundleRuntime builds it, and the deltas the change records for itself
- [x] 1.2 The guard: growth by any byte owes a delta of its own, within the noise margin of what it grew
- [x] 1.3 `EQ_UPDATE_RUNTIME_BUDGET=1` records the change's own growth
- [x] 1.4 The fold writes each delta as a row of the growth page, with its pull request, under the release's version
- [x] 1.5 CI's test job fetches two commits; one rule for the embedded bun (`tests/Shared/EmbeddedBun.cs`)

## 2. Proof, documentation and the suites

- [x] 2.1 A probe of 63 bytes fails the guard with no delta and passes once its delta is recorded
- [x] 2.2 `check-release-folds.sh` asserts the fold's rows, failing against main's fold
- [x] 2.3 `CLAUDE.md`, `AGENTS.md` and the growth page say the rule; a ledger entry under `docs/ledger/`
- [x] 2.4 Every test project of the solution with the Conformance suite in full, each read by its exit code
