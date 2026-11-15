## MODIFIED Requirements

### Requirement: One agent of a delivery builds at a time

At most one build or test suite SHALL run on a host at any moment, whichever delivery and session it
belongs to: every build and suite goes through the host's lock (`scripts/one-build-at-a-time.py`, or
the person's own when named). Agents of one delivery that run in parallel SHALL only read.

#### Scenario: Three hypotheses about a defect

- **WHEN** three debuggers investigate in parallel
- **THEN** none of them builds or runs a test, and the reproduction is written and run afterwards by one, alone

#### Scenario: Two deliveries build at once

- **WHEN** two deliveries, in two worktrees, each start a test suite
- **THEN** the second waits on the host's lock until the first ends, and says that it waits

## ADDED Requirements

### Requirement: A delivery runs in a worktree of its own

Each delivery SHALL be developed in a worktree of its own, created at intake on its branch from
`origin/main`, where every agent of the delivery works and its state is kept. The clone's own
checkout SHALL NOT be switched by a delivery, and a delivery SHALL NOT work in another's worktree.
The worktree SHALL be removed once the pull request has merged.

#### Scenario: Two sessions deliver at once

- **WHEN** a second session starts a delivery while the first is mid-gate
- **THEN** each works in its own worktree, and neither one's branch, files or state moves under the other

### Requirement: A gate's fix is held to its finding

A fix made in a round of the gate SHALL change only what its finding names. A hardening or a nit that
would change behaviour, add a public member or reach a file the change did not touch SHALL become an
issue under the delivery's parent instead. Every round after the first SHALL review only the
previous round's delta.

#### Scenario: A hardening item that would change behaviour

- **WHEN** a reviewer suggests that a node with an empty label should render as its child
- **THEN** it is filed as an issue, and the round's fixes do not include it
