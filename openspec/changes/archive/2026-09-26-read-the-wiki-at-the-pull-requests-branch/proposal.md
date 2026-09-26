# Proposal

Closes #406, a Task under the epic #160 (Instruments that fail, not warn).

## Why

The docs guards compare a pull request's code with the wiki (`WikiDiagnosticsTests` and its siblings
in `tests/eQuantic.UI.Web.Tests`), and CI cloned the wiki's master for every run. So the wiki and
main had to move together, which they cannot. A pull request that adds a diagnostic failed its own
guard until its rows were published, and once they were, every other pull request based on main
failed the same guard from the other side until the first one merged. On 2026-09-24 #386's EQ1007
rows went to master before it merged, #405 failed with "named by the wiki and gone from
docs/DIAGNOSTICS.md" in both languages, and the rows were taken back out to clear it. #418 met the
same wall with EQ1008 on 2026-09-26, and so will every change that brings a diagnostic.

## What Changes

- `scripts/checkout-wiki.sh` clones the wiki beside the repository, at the branch named exactly like
  the pull request's head branch when the wiki has one, and at master otherwise. The branch name
  arrives through `EQ_WIKI_BRANCH` and is only compared and passed as one argument. A wiki whose
  branches cannot be listed fails the checkout rather than falling back to master, and leaves
  nothing a guard could read.
- Both CI jobs that clone the wiki run the script with the head ref in their environment, and a
  `wiki-checkout` job runs its self-test against a fixture wiki, each run proven by the page it read.
- The Workflow section (`CLAUDE.md`, `AGENTS.md`), `CONTRIBUTING.md` and the pull request template
  say where a pull request's wiki pages go (a wiki branch named like the pull request's) and when
  that branch merges (into the wiki's master, with the pull request).
- Every docs guard finds the wiki through one locator, `WikiClone` in `tests/eQuantic.UI.Web.Tests`,
  which reads `EQ_WIKI_DIR` when it is set. A local run points it at a worktree of a pull request's
  wiki branch, so the clone beside the repository, which every local run shares, stays on master:
  checking a branch out there made #354's guards fail on #418's rows. A variable that names no wiki
  fails every guard instead of letting it skip, and a failing guard names the directory, branch and
  commit it read.
- The wiki guard's message names the branch CI reads.

Nothing changes for a developer using the SDK: no C#, setting, package or template moves, and
neither the public surface nor the developer surface does.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `working-agreement`: a pull request's documentation guards read its own wiki pages, and the wiki's
  master moves with main.

## Impact

`scripts/checkout-wiki.sh` (new), `.github/workflows/ci.yml` (two steps and one job), `CLAUDE.md`,
`AGENTS.md`, `CONTRIBUTING.md`, `.github/pull_request_template.md`, the three wiki guards in
`tests/eQuantic.UI.Web.Tests` and the locator they now share (`WikiClone.cs`), and `docs/LEDGER.md`. The wiki repository
holds a branch for each open pull request that changes a page, until that pull request merges.
