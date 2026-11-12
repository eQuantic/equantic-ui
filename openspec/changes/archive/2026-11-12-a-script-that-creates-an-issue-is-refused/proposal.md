# Proposal

Closes #1478, a Task under #160.

## Why

The hook from #1430 reads the command line, and a script hides what it does in its file. On
2026-11-12 a session ran a local tool, `file-subissues.py`, which calls `gh issue create` from a
Python `subprocess` argument list. The hook let it through, and #1475 and #1477 were born without
points, so their fields were set by hand afterwards.

## What Changes

- The hook reads the file of a script the command runs (an interpreter and its file, or the file run
  as a program), and the scripts that file runs, one level down. It refuses the command when one of
  them creates an issue: `gh issue create` as a line or as an argument list, or the GraphQL
  `createIssue`. A file that is not text, or that cannot be read, passes, as a call the hook cannot
  read always did.
- `scripts/new-issue.py`, the one door, and the hook itself, whose self-test names every refusal, are
  the two scripts allowed to name a creation.
- The local tool that slipped through is retired outside the repository, with a message naming the
  script.

Nothing an app writes changes, and neither the public surface nor the developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `working-agreement`: the hook refuses a script that creates an issue inside itself.

## Impact

- `.claude/hooks/issues-are-born-whole.py` and its self-test, which CI's `roadmap` job runs.
- `docs/ROADMAP-FORECAST.md`.
