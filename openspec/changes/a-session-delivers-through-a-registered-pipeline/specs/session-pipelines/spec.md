## ADDED Requirements

### Requirement: A delivery follows the pipeline of its kind

A session that delivers a change through the `pipeline` skill SHALL run the stages of the pipeline
registered for the change's kind (`feature`, `bugfix`, `refactor`, `docs` or `review`), in order,
and SHALL take the duties of the area from what the diff touches, never from the pipeline's name.

#### Scenario: A bug in the transpiler

- **WHEN** the request is to fix a wrong translation
- **THEN** the session runs `bugfix`: a failing test, a proposal, the fix, the documentation and the gate
- **AND** the engineer is given the compiler's rules, and the proof is the conformance suite run on both sides

#### Scenario: A stage that does not apply

- **WHEN** a stage of the selected pipeline has nothing to do for this change
- **THEN** the delivery's state records it as not applicable with the reason, and no stage is skipped in silence

### Requirement: A change is proved by an agent that did not write it

The agent that proves a change SHALL NOT be the agent that wrote it, and SHALL be the only writer
in the tree while it runs. Agents that write SHALL run one at a time in the session's tree.

#### Scenario: The gate

- **WHEN** the reviewers have answered
- **THEN** the prover runs alone, takes the fix out by copying the file aside, sees the test fail, restores from the copy and touches the file
- **AND** the tree's status is what it was before it ran

### Requirement: A gate's findings are sorted as a review round's are

The gate SHALL sort each finding as a defect, as hardening or as wrong, in the Workflow's meaning
of each, SHALL stop the delivery for the person after two rounds with a defect standing, and SHALL
keep a security finding rated high out of every issue, pull request and commit message.

#### Scenario: A high security finding

- **WHEN** the security reviewer rates a finding high
- **THEN** it is written only in the delivery's report, and the session stops and asks the person to open a private advisory

### Requirement: A branch named outside the convention is refused when it is created

A session's command that creates a branch of this repository named outside `<type>/<slug>` SHALL
be refused before it runs, with the convention as the reason. A command aimed at another
repository, and one the hook cannot read, SHALL pass.

#### Scenario: A tool's own prefix

- **WHEN** a session runs `git switch -c claude/fix-the-thing` in this repository
- **THEN** the command is refused, and the session is told the type and the shape of the slug

#### Scenario: The wiki's clone

- **WHEN** a session runs `git -C ../equantic-ui.wiki switch -c anything`
- **THEN** the command passes

### Requirement: What the skill and the agents cite is there

The hook's self-test SHALL fail when the `pipeline` skill or an agent cites a path of this
repository that does not exist.

#### Scenario: A rules file is renamed

- **WHEN** a file an agent is told to read first is renamed and the agent is not changed
- **THEN** the self-test, which CI runs, fails naming the agent and the path
