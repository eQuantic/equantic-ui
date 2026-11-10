# Tasks

## 1. A save during a rebuild

- [x] 1.1 Mark a save pending before the flag is tried, and run once more when the rebuild holding it ends. Verify: `SavesDuringARebuild_AreRebuiltAfterIt_Once` counts two rebuilds for three saves, and one with the old Rebuild back
- [x] 1.2 Run the hot reload tests of the Server suite. Verify: the lifetime, decision and announcement tests pass with the new loop

## 2. The wait after a save

- [x] 2.1 Make the wait a 50ms quiet window on one timer restarted by each change. Verify: the measurement's "wait" part on the dashboard sample is 91 to 97ms, where it was 436 to 475ms, on the same machine
- [x] 2.2 Lower the web's ceiling in `scripts/edit-to-view.budget.json` by what was measured, once #1261 has set it from CI. Verify: 45s to 44.5s, the 0.5s the wait after a save lost

## 3. Documentation

- [x] 3.1 One entry for the ledger citing #1274 and #1262, as a file of its own under `docs/ledger/`
