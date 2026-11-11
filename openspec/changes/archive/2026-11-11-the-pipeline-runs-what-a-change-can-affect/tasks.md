# Tasks

## 1. Measure

- [x] 1.1 Read which repository paths each test project reads: `Web.Tests`, `Compiler.Tests` and `Native.Engine.Tests` read documents, `Conformance.Tests` reads code only
- [x] 1.2 Classify main's last 80 merges: 78 code, 2 documents, 0 extension

## 2. The classifier and the workflow

- [x] 2.1 `scripts/ci-changes.sh`, with a self-test of 29 cases that the `changes` job runs before it classifies
- [x] 2.2 The `changes` job, the single jobs gated by `if:`, the matrix jobs gated by step, and main's runs in one concurrency group that cancels the superseded run
- [x] 2.3 `actionlint` finds nothing new in the workflow (the same 17 shellcheck notes as main)
- [x] 2.4 Prove it live: this pull request's run is `full` (43064534335); a draft stacked on it that changes one document is `docs` (43070269976, on the final workflow after main's #1386): it skipped `test-runtime`, `build-packages`, the macOS runtimes, `test (macos-latest)`, `samples` and `package-extension`, ran the `web` and `rest` groups, ran the `conformance` entries' one step on Ubuntu, and every required check reported success

## 3. Documentation

- [x] 3.1 One ledger entry citing the issue, as a file of its own under `docs/ledger/`
