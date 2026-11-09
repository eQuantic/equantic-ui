## ADDED Requirements

### Requirement: Every test runs where it can answer, once

CI SHALL run every test project under `tests/` on Linux and on Windows, and on macOS, whole, every
test project that references the Apple track (`Native.Shell.Apple`, `.MacOS`, `.iOS`,
`Native.Engine.Metal`) or holds a test that asks for macOS in its file (a MacFact, MacTheory or
MacFontFact, `OperatingSystem.IsMacOS()`, `OSPlatform.OSX` or `[SupportedOSPlatform("macos")]`), with
the Apple workloads installed. Those projects SHALL be derived from the tests and never listed. A
project that runs no test SHALL fail the job, as SHALL a derivation that finds no project. The job
that packs the packages SHALL run no test.

#### Scenario: A new Apple-only fact

- **WHEN** a test class in a project that asked nothing of macOS before gains a `[MacFact]`
- **THEN** the macOS job runs that project

#### Scenario: A fact that probes the device

- **WHEN** a test in a project that references the Apple track skips unless a Metal device is there
- **THEN** it runs on macOS, though its file names no operating system

#### Scenario: A project that answers the same everywhere

- **WHEN** no test of a project asks for macOS and it does not reference the Apple track
- **THEN** it runs on Linux and Windows, and not on macOS

### Requirement: A new push to a pull request cancels the run it replaces

CI SHALL cancel a pull request's run in progress when the same pull request starts a new one, and
SHALL never cancel, nor queue behind another, a run on main, on a tag, or started by hand.

#### Scenario: Two pushes to a pull request

- **WHEN** a pull request gets a second push while the run of the first is still going
- **THEN** the first run is cancelled and the second one runs

#### Scenario: A push to main while pull requests run

- **WHEN** main gets a push while a pull request's runs come and go
- **THEN** main's run goes to the end

## MODIFIED Requirements

### Requirement: A pull request merges on its own CI, against the current main

The repository's ruleset SHALL require the CI's jobs (`test-runtime`, the three `test` legs, `samples`,
the three `build-platform-runtimes`, `build-packages`, `commit-messages`, `openspec`, `session-start`,
`wiki-checkout` and `package-extension`) to pass on a pull request's head, and SHALL require that head
to be up to date with main, so a pull request merges only on a CI run that saw the main it lands on.
Every job in a dependency chain SHALL be required, since GitHub counts a job skipped for a failed
dependency as passed.

#### Scenario: main moved after the CI ran

- **WHEN** a pull request passed CI, and another pull request merged into main after it
- **THEN** GitHub refuses the merge until main is merged into the pull request and CI passes again

#### Scenario: A workflow that never ran

- **WHEN** a workflow fails before it creates a job
- **THEN** the required checks never report, and the pull request stays blocked

#### Scenario: A red fact only a Mac answers

- **WHEN** a pull request turns a `[MacFact]` red
- **THEN** `test (macos-latest)` fails and the pull request cannot merge
