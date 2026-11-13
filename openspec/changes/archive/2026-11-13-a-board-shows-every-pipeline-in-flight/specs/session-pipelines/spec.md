## ADDED Requirements

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
