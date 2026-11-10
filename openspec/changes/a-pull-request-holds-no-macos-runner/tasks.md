# Tasks

## 1. The workflow

- [x] 1.1 The four macOS jobs skip `pull_request`, and the macOS runtimes leave the matrix as a job named like its check, with the legs' steps in `.github/actions/pack-runtime`. Verify: actionlint, and the jobs that need them read on main and on tags
- [ ] 1.2 Verify: a run on this pull request with the four checks skipped and satisfied, and a `workflow_dispatch` run on the branch with the four running

## 2. Documentation

- [x] 2.1 The Workflow section of `CLAUDE.md` and `AGENTS.md`, and the pull request template
- [x] 2.2 One entry for the ledger citing #1385, as a file of its own under `docs/ledger/`
