## ADDED Requirements

### Requirement: Every test runs where it can answer, once

CI SHALL run every test project under `tests/` on Linux and on Windows, and on macOS every test class
that asks for macOS in its own file: a MacFact, MacTheory or MacFontFact, `OperatingSystem.IsMacOS()`,
`OSPlatform.OSX` or `[SupportedOSPlatform("macos")]`. The macOS classes SHALL be derived from the tests
and never listed, and a project whose derived classes run no test SHALL fail the job, as SHALL a
derivation that finds no class. The job that packs the packages SHALL run no test.

#### Scenario: A new Apple-only fact

- **WHEN** a test class gains a `[MacFact]` and nothing else changes
- **THEN** the macOS job runs it

#### Scenario: A filter that stopped matching

- **WHEN** a project's derived classes select no test
- **THEN** the macOS job fails, naming the project

#### Scenario: A test that answers the same everywhere

- **WHEN** a test class asks nothing of the operating system
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
