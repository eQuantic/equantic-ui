# working-agreement Specification

## Purpose
How a change reaches `main` in this repository, and what every working session starts from: the
written rules, the checks that fail when they are broken, and the environment a cloud session is
given before its first command.

## Requirements

### Requirement: One written working agreement for every agent

The repository SHALL carry its working agreement as a `## Workflow` section that is the same text in
`CLAUDE.md` and in `AGENTS.md`, and the test suite SHALL fail when the two copies differ or when
either file lacks the section.

#### Scenario: One copy is edited alone

- **WHEN** a commit changes the Workflow section of `CLAUDE.md` and leaves `AGENTS.md` as it was
- **THEN** the test that compares the two sections fails and names the first line that differs

#### Scenario: A file loses the section

- **WHEN** either file no longer has a `## Workflow` heading
- **THEN** the same test fails, saying which file has no section to compare

### Requirement: No attribution is written by the tooling

The project's Claude Code settings SHALL disable every attribution Claude Code adds by default: the
commit trailer, the pull request footer, and the session link that cloud and Remote Control
sessions append.

#### Scenario: A session commits and opens a pull request

- **WHEN** a session running with the project's settings creates a commit and a pull request
- **THEN** the commit message carries no co-authorship or session trailer and the pull request body
  carries no generated-with footer or session link

### Requirement: A cloud session starts with the owner's identity and no foreign signature

A session that starts in a cloud container SHALL be prepared, before its first command, to commit
as `Edgar Mesquita <edgar@equantic.tech>` with commit and tag signing turned off in the repository,
whatever identity and signing configuration the container itself provides.

#### Scenario: The container signs with a key that is not the owner's

- **WHEN** the container's global git configuration names another identity and requires signing
  with a key that does not exist
- **THEN** a commit made before the session is prepared fails
- **AND** a commit made after it succeeds, unsigned, authored and committed by the owner

### Requirement: A cloud session has the pinned toolchain

A session that starts in a cloud container SHALL have on PATH the .NET SDK version that
`global.json` pins and the OpenSpec CLI version that its lockfile pins, with OpenSpec telemetry off.
Every archive the preparation downloads SHALL be verified against a pinned SHA-256 and refused on a
mismatch, leaving nothing installed.

#### Scenario: A container with no .NET SDK

- **WHEN** a session starts in a container that has no `dotnet` on PATH
- **THEN** `dotnet --version` answers the version `global.json` pins
- **AND** `openspec --version` answers the pinned CLI version and `OPENSPEC_TELEMETRY` is `0`

#### Scenario: The archive is not the one pinned

- **WHEN** the downloaded SDK archive's SHA-256 differs from the pinned digest
- **THEN** the preparation reports the mismatch and installs nothing

### Requirement: A preparation that falls short says so to the person

When a tool the working agreement makes mandatory cannot be prepared (the OpenSpec CLI, and in a
cloud container the .NET SDK or signing turned off), the preparation SHALL warn the person in the
session, not only report it to the agent, and SHALL NOT block the session.

#### Scenario: The pinned SDK is refused

- **WHEN** the SDK archive's SHA-256 differs from the pinned digest
- **THEN** the session starts, and the person in it sees a warning naming the .NET SDK

### Requirement: A local session is left as its owner set it up

A session on a developer's own machine SHALL NOT change the git identity, the signing
configuration, the installed SDKs or Docker; it SHALL only make the pinned OpenSpec CLI available.

#### Scenario: A session starts on a laptop

- **WHEN** a session starts outside a cloud container
- **THEN** the preparation reports that the identity, the SDKs and Docker were left as they are
- **AND** the pinned OpenSpec CLI is on the session's PATH

### Requirement: Specs validate strictly, and their absence is a failure

CI SHALL validate every OpenSpec change and spec with the pinned CLI in strict mode, and SHALL fail
when a change or a spec is invalid and also when there is nothing to validate at all.

#### Scenario: A spec breaks the format

- **WHEN** a pull request adds a requirement without a scenario
- **THEN** the OpenSpec job fails and names the invalid item

#### Scenario: The specs are gone

- **WHEN** `openspec/` holds no change and no spec
- **THEN** the OpenSpec job fails instead of reporting that nothing was found

### Requirement: A pull request's docs guards read its own wiki pages

CI SHALL check out the wiki for the documentation guards at the branch named exactly like the pull
request's head branch when the wiki has one and the pull request comes from this repository, and at
the wiki's default branch otherwise. The branch name SHALL be read as data and never interpolated
into a command. When the wiki's branches cannot be
listed, the checkout SHALL fail and leave nothing behind, rather than read the default branch.

#### Scenario: A pull request that carries its wiki pages

- **WHEN** the wiki has a branch named like the pull request's head branch
- **THEN** the guards read that branch's pages

#### Scenario: A run with no wiki branch of its own

- **WHEN** the wiki has no branch of that name, or the run is not a pull request
- **THEN** the guards read the wiki's default branch

#### Scenario: A pull request from a fork

- **WHEN** a fork's pull request has a head branch named like one of the wiki's branches
- **THEN** the guards read the wiki's default branch, since that wiki branch belongs to another
  pull request

#### Scenario: A name that is only the tail of a branch

- **WHEN** the head branch is `some-change`, and the wiki has `fix/some-change` and no `some-change`
- **THEN** the guards read the wiki's default branch

#### Scenario: The wiki's branches cannot be listed

- **WHEN** listing the wiki's branches fails, though cloning it would work
- **THEN** the checkout fails and leaves no directory behind

#### Scenario: A branch name written to run

- **WHEN** the head branch's name holds `$(touch pwned)` and a backquoted command
- **THEN** nothing it names runs

### Requirement: Every docs guard reads one wiki, which a local run can name

Every test that reads the wiki SHALL find it through one locator: the directory `EQ_WIKI_DIR` names
when the variable is set, and `equantic-ui.wiki` beside the repository otherwise. An `EQ_WIKI_DIR`
that names no directory, or a directory that is not a checkout of the wiki, SHALL fail every guard
rather than let it skip. A failing guard SHALL name the wiki it read: the directory, and the branch
and commit checked out there.

#### Scenario: A local run against a pull request's wiki branch

- **WHEN** `EQ_WIKI_DIR` names a worktree of a pull request's wiki branch
- **THEN** every guard reads that worktree, and the clone beside the repository is left as it is

#### Scenario: A typo in the variable

- **WHEN** `EQ_WIKI_DIR` names a directory that does not exist, or one without the wiki's `Home.md`
- **THEN** every guard fails, saying what the variable names

#### Scenario: A guard fails

- **WHEN** a guard finds a row the docs do not have
- **THEN** its message names the directory it read, and the branch and commit checked out there

### Requirement: The wiki's master moves with main

A pull request that changes what the wiki must say SHALL carry its pages, in English and Portuguese
in one commit, on a wiki branch named like its own branch, and that branch SHALL be merged into the
wiki's master when the pull request merges, and not before.

#### Scenario: A pull request adds a diagnostic

- **WHEN** a pull request adds a diagnostic code and carries its wiki rows on its wiki branch
- **THEN** its own guards pass, and every other pull request's guards, which read master, still
  pass without the rows

#### Scenario: The pull request merges

- **WHEN** the pull request merges into main
- **THEN** its wiki branch is merged into the wiki's master and deleted
