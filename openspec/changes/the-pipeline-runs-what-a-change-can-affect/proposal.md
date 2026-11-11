# Proposal

Closes #1421, a Task under #160 (Instruments that fail, not warn).

## Why

Every pull request ran the whole pipeline, about 25 jobs, three of them on macOS, where GitHub Free
gives five runners to everyone, whatever the change touched. And since the merge queue (#1308) every
merge is a push to main whose run repeats, on the same tree, the suite the queue has just run: on
2026-11-11 a merge group waited for macOS from 15:42Z behind twelve of those runs, eleven already
superseded by a later push. A change to documents waited in that queue for jobs that cannot fail
because of it.

Measured over main's last 80 merges: 78 touched code, 2 only documents. So the superseded runs of
main are where the queue is lost, and documents are the smaller, cheaper case.

## What Changes

- A push to main cancels the run of the push before it, as a push to a pull request already does
  (#1309). The newest push's packages are the ones the convenience feed wants.
- A `changes` job reads what a pull request or a merge group touches and classifies it with
  `scripts/ci-changes.sh`, which has a self-test the job runs first: `docs` when every path is a
  document, `extension` when every path is the VS Code extension's, `full` otherwise and whenever it
  cannot tell. A push, a tag and a manual run are always `full`.
- A change to documents runs the `web` and `rest` test groups on both systems (their projects read
  docs/, the ledger, the design handoff, the wiki and CLAUDE.md), the guards that run on every change,
  and nothing else: no build, no conformance, no runtime tests, no Mac jobs, no samples, no extension.
  A change to the extension alone builds the extension and runs no .NET test.
- The 18 required checks keep their names: the matrix jobs always run and skip their steps (on
  Ubuntu when their own runner would be a Mac or Windows with nothing to do), and the single jobs are
  skipped by their `if:`, which GitHub reports as success.

Nothing changes for a developer using the SDK.

## Impact

- `.github/workflows/ci.yml` and the new `scripts/ci-changes.sh`.
- The `working-agreement` spec gains the requirement below.
