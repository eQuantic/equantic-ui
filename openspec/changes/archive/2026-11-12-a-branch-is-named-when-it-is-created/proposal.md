# A branch is named when it is created, and a contributor can create an issue

Closes #1498, a Task under the epic #160 (Instruments that fail, not warn), split from #1493.

## Why

Two rules of the working agreement were held by nothing a session meets.

A branch is `<type>/<slug>`, never a tool's or an agent's own prefix, and a squash merge keeps no
head ref: the name can be corrected only before the first commit, and the rule was a sentence.

An issue is created only with `scripts/new-issue.py`, and the script read the issue types from the
organization, which answers null for each to an account that is not a member of it. A contributor's
run stopped at a `TypeError`, with every other way of creating an issue refused by a hook.

## What Changes

Nothing changes for a developer using the SDK. For a session and a contributor in this repository:

- A `PreToolUse` hook refuses a command that creates a branch of this repository named outside the
  convention (`git switch -c`, `checkout -b`, `branch`, `worktree add`, `stash branch`), reading
  the line as the shell does and following `cd` and `-C` to the tree git runs in. A name that
  already exists here or on origin, a branch of another repository and what it cannot read pass.
- `scripts/new-issue.py` reads the issue types from the repository, and skips a null one.

No public surface and no developer surface moves. Nothing is removed.

## Capabilities

### Modified Capabilities

- `working-agreement`: two requirements added.

## Impact

`.claude/hooks/`, `.claude/settings.json`, the `roadmap` job of the CI workflow, `scripts/new-issue.py`.

## Principles

It follows 9: an instrument that fails where a sentence warned. PROVING 4: the hook's self-test
runs the hook itself in a repository made for it, so the refusal is exercised, not only the
classification.
