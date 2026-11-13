# session-pipelines Specification

## Purpose
How a Claude Code session orchestrates a delivery in this repository when a person asks it to: the
pipeline of the change's kind, the agent, model and effort of each stage, the gate before the pull
request and what a delivery records about itself. What a delivery must satisfy stays in the
`working-agreement` capability.

## Requirements

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

### Requirement: A pipeline runs only when a person asks for it, and measures itself

While it is a pilot, the `pipeline` skill SHALL run only when a person invokes it, and a delivery
that runs through it SHALL record, as it happens, the time and the tokens of each stage, the
session's compactions and every defect found after its pull request opened.

#### Scenario: A request to fix a bug

- **WHEN** a person asks a session to fix a bug and does not invoke the skill
- **THEN** the session delivers as the Workflow says, without the pipeline

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

### Requirement: What the skill and the agents cite is there

The hook's self-test SHALL fail when the `pipeline` skill or an agent cites a path of this
repository that does not exist.

#### Scenario: A rules file is renamed

- **WHEN** a file an agent is told to read first is renamed and the agent is not changed
- **THEN** the self-test, which CI runs, fails naming the agent and the path

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

### Requirement: A delivery's state names its session and its merge

The state of a delivery run through the `pipeline` skill SHALL record the id of the session running
it, written again when another session resumes it, and SHALL record the moment its pull request
merged.

#### Scenario: A delivery starts

- **WHEN** a person invokes the skill and the state is written before the first stage
- **THEN** its `session` is the name of the folder Claude Code writes that session's transcript in

#### Scenario: The pull request merges

- **WHEN** the session sees the pull request merged
- **THEN** `measures.merged` holds the moment, and the board draws Ship as completed

### Requirement: A board reads every delivery and writes nothing

`.claude/skills/pipeline/board/board.py` SHALL list the deliveries of every worktree of the
repository, SHALL give the tokens and the time of each step, stage and delivery by one rule over
`measures`, and SHALL write no file. A step is an object under `measures.stages.<stage>` carrying
`tokens` or `duration_ms`, named by its key path; an object with no step beneath it is a note; a
count is an integer or a string of digits, never a boolean.

#### Scenario: Tokens written as a string

- **WHEN** a stage carries `"tokens": "84430"` and no object
- **THEN** it is one step of 84,430 tokens, named after the stage

#### Scenario: Rounds

- **WHEN** a stage holds `round1: {preship: {tokens: 57254}, sorted: {defects: 3}}` and `prover: {tokens: 94392}`
- **THEN** its steps are `round1 › preship` and `prover`, its tokens 151,646, and `round1 › sorted` is a note

#### Scenario: A boolean is not a count

- **WHEN** a step carries `"tokens": true`
- **THEN** it adds nothing to its stage

#### Scenario: A stage the registry does not have

- **WHEN** `measures.stages` holds `Owner review fix` and `stages` does not
- **THEN** the board draws it after the registry's stages, before Ship, and counts it in the total

### Requirement: The board shows the agents working now

The board SHALL show an agent as working on a delivery when its `agent-<id>.meta.json` carries no
`finished`, its session is in Claude Code's session registry with a live process, and its id is in
the delivery's state or its transcript's last `gitBranch` is the delivery's branch. What it is doing
SHALL be the last tool its transcript records.

#### Scenario: A session that ended

- **WHEN** an agent's meta has no `finished` and no running process holds its session
- **THEN** the board shows it working on no delivery

#### Scenario: One session, two branches

- **WHEN** a live session holds two deliveries on two branches, and an agent of it that neither state names works on the second branch
- **THEN** it is shown under the second delivery only

#### Scenario: What it is doing

- **WHEN** a working agent's transcript ends with a `Bash` tool use described as `Run the reader's self-test`, after a line longer than 256 KiB
- **THEN** the board shows `Bash: Run the reader's self-test`, its context as the last usage's sum, and the time since its first record

### Requirement: The board answers the loopback, and only what it is asked by name

The board SHALL listen on `127.0.0.1` only, SHALL refuse a request whose `Host` is not the loopback
at its port, and SHALL serve a delivery's file only when the request names the delivery and a `.md`
file or `state.json` that the delivery's folder lists, whose real path is inside that folder.

#### Scenario: A path in the request

- **WHEN** `/api/report/<delivery>/..%2F..%2Fetc%2Fpasswd` is asked
- **THEN** the answer is 404 and no file outside the delivery's folder is read

#### Scenario: A link out of the folder

- **WHEN** the delivery's folder holds `leak.md`, a symbolic link to a file outside it
- **THEN** the answer is 404 and the file's content is in no answer

#### Scenario: A page of another site

- **WHEN** a request arrives with `Host: evil.example`
- **THEN** the answer is 403, whatever the path

#### Scenario: The registry's tokens

- **WHEN** Claude Code's session registry holds a `.key` file beside a session's `.json`
- **THEN** no answer of the board carries its content

### Requirement: The board's self-test runs in CI

`board.py --self-test` SHALL run the reader and the server over a fixture written to disk and SHALL
exit non-zero when a case fails, and CI's `roadmap` job SHALL run it.

#### Scenario: A rule broken

- **WHEN** the reader stops counting a token written as a string
- **THEN** `--self-test` exits 1 naming the case, and the `roadmap` job fails
