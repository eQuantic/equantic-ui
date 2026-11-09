## ADDED Requirements

### Requirement: Each system's tests run as parallel groups

CI SHALL run each system's test projects as parallel jobs, one per group: the Conformance project, the
Web project, and every other project found under `tests/`. The projects SHALL be found and never
listed, so a new project lands in a group without an edit to the workflow. A group that finds no
project SHALL fail, and a job SHALL name every red project of its group at its end.

#### Scenario: A new test project

- **WHEN** a project is added under `tests/` and nothing else changes
- **THEN** it runs in the `rest` group on every system

#### Scenario: A named project renamed away

- **WHEN** the Conformance project is renamed and the grouping is not
- **THEN** the `conformance` group finds no project and its job fails, saying so

### Requirement: A restore reads its packages from a cache keyed on their versions

The jobs that restore .NET packages SHALL restore them from a cache whose key changes when a version
changes in any file that declares one (`global.json`, `Directory.Build.props`, `Directory.Build.targets`,
the project files and the `packages.lock.json` files), with no fallback to another key's cache. The
repository's own packages (`equantic.*`) SHALL never be kept in it.

#### Scenario: A version changes

- **WHEN** a pull request changes the version of a referenced package
- **THEN** its jobs miss the cache and restore from nuget.org

#### Scenario: A package this repository built

- **WHEN** a job restores an `equantic.*` package it packed
- **THEN** that package is not in the cache the next run restores

## MODIFIED Requirements

### Requirement: A pull request merges on its own CI, against the current main

The repository's ruleset SHALL require the CI's jobs to pass on a pull request's head: `test-runtime`,
the three `test` jobs of Ubuntu and the three of Windows (`conformance`, `web` and `rest`),
`test (macos-latest)`, `samples`, the three `build-platform-runtimes`, `build-packages`,
`commit-messages`, `openspec`, `session-start`, `wiki-checkout` and `package-extension`. It SHALL require
that head to be up to date with main, so a pull request merges only on a CI run that saw the main it
lands on. Every job in a dependency chain SHALL be required, since GitHub counts a job skipped for a
failed dependency as passed.

#### Scenario: main moved after the CI ran

- **WHEN** a pull request passed CI, and another pull request merged into main after it
- **THEN** GitHub refuses the merge until main is merged into the pull request and CI passes again

#### Scenario: A workflow that never ran

- **WHEN** a workflow fails before it creates a job
- **THEN** the required checks never report, and the pull request stays blocked

#### Scenario: A red fact only a Mac answers

- **WHEN** a pull request turns a `[MacFact]` red
- **THEN** `test (macos-latest)` fails and the pull request cannot merge

#### Scenario: A red test in one group

- **WHEN** a pull request turns a Conformance case red on Windows
- **THEN** `test (windows-latest, conformance)` fails and the pull request cannot merge
