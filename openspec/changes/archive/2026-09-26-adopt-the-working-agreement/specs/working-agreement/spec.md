# Spec Delta

## Purpose

How a change reaches `main` in this repository, and what every working session starts from: the
written rules, the checks that fail when they are broken, and the environment a cloud session is
given before its first command.

## ADDED Requirements

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
