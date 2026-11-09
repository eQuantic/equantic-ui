## 1. The longest path only packs

- [x] 1.1 `scripts/macos-tests.sh` derives the classes that ask for macOS from their files, runs them, and fails on a project that ran none
- [x] 1.2 Its self-test: every marker listed, a file with no test, a plain test, another system's fact and a build output not; an empty derivation fails; proved both ways
- [x] 1.3 `build-packages` runs no test, and `test (macos-latest)` runs the derived classes; publishing waits for it
- [x] 1.4 The derived classes run on a Mac: every project ran tests, and none failed

## 2. A superseded run is cancelled

- [x] 2.1 A `concurrency` group keyed on the pull request's number, cancelling in progress only for `pull_request`, and a group of its own run for every other event

## 3. Proof and documentation

- [x] 3.1 The run's wall time, `build-packages`' and the macOS minutes, before and after, in the pull request
- [x] 3.2 `docs/ARCHITECTURE-AUDIT.md` says where the suite runs, and one ledger entry per issue
