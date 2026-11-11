# Tasks

## 1. Measure what a contributor meets

- [x] 1.1 Read the shells' target frameworks: the iOS (`net10.0-ios`) and Android (`net10.0-android`) shells need their workloads, the macOS and Windows shells are `net10.0`, and CI installs `ios android` before building
- [x] 1.2 Read the ruleset (merge queue on, 18 checks) and CI's jobs (`samples` builds the samples on macOS), and count the open `good first issue` and `help wanted` issues: none

## 2. The agreement and the guide

- [x] 2.1 Say in the Workflow section of `CLAUDE.md` and `AGENTS.md`, the same text in both, how an issue is taken and which issues a session leaves alone
- [x] 2.2 Say the same in `CONTRIBUTING.md`, with the workloads, what to build on Linux and Windows, the `samples` job and the merge queue as they are
- [x] 2.3 Prove it: `WorkflowSectionTests` holds the two sections equal, `DocsIndexTests` resolves every path the guide cites, and `./scripts/check-openspec.sh` validates this change strictly

## 3. Documentation

- [x] 3.1 One ledger entry citing the issue, as a file of its own under `docs/ledger/`
