# Proposal

Closes #1385, a Task under #160, beside #1308 to #1312.

## Why

GitHub Free runs at most five macOS jobs at once, and every push to a pull request held four of
them: `build-packages`, `samples`, the macOS leg of `build-platform-runtimes` and
`test (macos-latest)`. On 2026-11-10, 41 runs sat queued behind the macOS runners, nothing merged
after 14:34, and the `v0.2.0-preview.68` tag run waited behind pull requests whose heads had already
moved on. Since #1308, the merge queue's run, not the pull request's, is what decides a merge.

## What Changes

- The four macOS jobs run on `merge_group`, a push to main, a tag and `workflow_dispatch`, and never
  on `pull_request`. On a pull request they report as skipped under their names, which satisfies the
  ruleset's required checks, and the queue runs them before anything reaches main.
- The macOS leg leaves the `build-platform-runtimes` matrix and becomes a job named
  `build-platform-runtimes (macos-latest, Osx64, OsxArm64)`, because a matrix leg cannot be skipped
  alone. The legs share their steps through a composite action, `.github/actions/pack-runtime`.
- A pull request that wants the macOS jobs before the queue asks for them on its branch with
  `gh workflow run ci.yml --ref <branch>`.

Nothing an app writes changes. Neither the public surface nor the developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `working-agreement`: a pull request's own run skips the macOS jobs, and the merge queue runs them.

## Impact

`.github/workflows/ci.yml`, `.github/actions/pack-runtime`, `.github/pull_request_template.md`, and
the Workflow section of `CLAUDE.md` and `AGENTS.md`.
