# Proposal

Closes #1415, a Task under #1414 (Contributors: the repository takes work from people besides its owner).

## Why

A friend of the owner joins as a contributor with write access, and the repository was run by its
owner and the owner's agent sessions alone. Measured against what he meets: `CONTRIBUTING.md` says
the .NET 10 SDK is all a build needs while the solution holds the iOS and Android shells, which need
their workloads (CI installs them before building); it says CI does not build the samples, which its
`samples` job does, and that the merge queue is still to come, which has been on since 2026-11-10; it
sends a newcomer to `good first issue` and `help wanted`, with none open; and nothing says how an
issue is taken. Every issue is assigned to the owner's account, which the sessions work as, so the
board cannot tell a person's work from a session's, and a session could start an issue a person took.

## What Changes

- The Workflow section of `CLAUDE.md` and `AGENTS.md` says how an issue is taken: by assigning it.
  An issue assigned to anyone but the owner's account is that person's, and so is one labelled
  `good first issue`, kept for a person's first step: no session starts it or pushes to its branch.
- `CONTRIBUTING.md` says the same to a contributor, installs the two phone workloads before the
  whole solution builds, says the iOS shell builds on macOS only and what to build on Linux and
  Windows instead, and states the `samples` job and the merge queue as they are.

Nothing changes for a developer using the SDK: no part of the SDK, eqc, the runtime, a realizer or
a shell moves, and neither the public surface nor the developer surface does.

## Impact

- `CLAUDE.md`, `AGENTS.md` (the same Workflow text, held equal by `WorkflowSectionTests`) and
  `CONTRIBUTING.md`.
- The `working-agreement` spec gains the requirement below.
- Labelling the first issues and granting the contributor access are steps on GitHub, outside the
  tree.
