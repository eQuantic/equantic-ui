# A session delivers through a registered pipeline

Closes #1493, a Task under the epic #160 (Instruments that fail, not warn). This is a proof of
concept, offered for evaluation.

## Why

The Workflow section of `CLAUDE.md`, `docs/PRINCIPLES.md` and `docs/PROVING.md` say what a delivery
must satisfy. Nothing says how a Claude Code session orchestrates one: which steps run in which
order, which subagent takes each, on which model and effort, what a gate refuses, where the state of
a delivery in progress is kept. Each session rebuilds that order from memory, in one context that
also holds every file it read.

## What Changes

Nothing changes for a developer using the SDK: no assembly, package, setting or template moves.
What changes is how a session works in this repository.

- A `pipeline` skill selects a pipeline by the KIND of the change (`feature`, `bugfix`, `refactor`,
  `docs`, `review`), runs its stages in order and keeps the delivery's state. The area a change
  touches adds duties and chooses the engineer's rules, model and effort; it is not a pipeline.
- Seven subagents under `.claude/agents/`: an architect, one engineer that takes the area as a
  parameter, a debugger, a prover that is never the author, two reviewers that only read, and a
  documentation writer. Each declares its tools, its model, its effort and a cap of turns. Two were
  adapted from an MIT-licensed collection, with its notice.
- A gate before the pull request: the author's review the Workflow already asks for, a review
  against the project's rules, a security review where the diff asks for one, then the independent
  proof, alone. Findings are sorted as the Workflow sorts Copilot's, in two rounds at most.
- A hook refuses the creation of a branch of this repository named outside `<type>/<slug>`.
- `scripts/new-issue.py` reads the issue types from the repository, so an account that is not a
  member of the organization can create an issue.

No public surface and no developer surface moves. Nothing is removed.

## Capabilities

### New Capabilities

- `session-pipelines`: how a session orchestrates a delivery.

## Impact

`.claude/skills/pipeline/`, `.claude/agents/`, `.claude/hooks/`, `.claude/settings.json`, the
`roadmap` job of the CI workflow, a pointer in `CLAUDE.md` outside its Workflow section.

## Principles

It follows 9 (the instrument that fails: the branch's name is refused, not warned about, and a
cited path that is missing fails a self-test) and PROVING 4 (the hook is exercised in the condition
it exists for). It bends none. The skill restates no rule of the Workflow: it cites them, and the
Workflow wins where the two differ.
