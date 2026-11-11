## MODIFIED Requirements

### Requirement: A pull request merges on its own CI, against the current main

The repository's ruleset SHALL require the CI's jobs to pass: `test-runtime`, the three `test` jobs of
Ubuntu and the three of Windows (`conformance`, `web` and `rest`), `test (macos-latest)`, `samples`, the
three `build-platform-runtimes`, `build-packages`, `commit-messages`, `openspec`, `session-start`,
`wiki-checkout` and `package-extension`, so a pull request merges only on a CI run that saw the main it
lands on: on its own head kept up to date with main, or, once the ruleset merges through GitHub's merge
queue, on the queue's run of it together with every pull request ahead of it. The CI SHALL run every
one of those jobs on a merge group, and no job that publishes. Every job in a dependency chain SHALL be
required, since GitHub counts a job skipped for a failed dependency as passed. The four jobs that run on
macOS (`test (macos-latest)`, `samples`, `build-packages` and the macOS `build-platform-runtimes`) SHALL
run on a merge group, a push to main, a tag and a run by hand, and SHALL be skipped by their own
condition on a pull request's run, where each reports as skipped under the name its required check
names.

#### Scenario: main moved after the CI ran

- **WHEN** a pull request passed CI, and another pull request merged into main after it
- **THEN** it reaches main only through a run that includes the new main: the queue's run of its
  group, or, without the queue, CI again after main is merged into it

#### Scenario: A merge group

- **WHEN** the merge queue starts a run for a group
- **THEN** every required job runs and reports on it, and no job publishes a package or a release

#### Scenario: A workflow that never ran

- **WHEN** a workflow fails before it creates a job
- **THEN** the required checks never report, and the pull request stays blocked

#### Scenario: A red fact only a Mac answers

- **WHEN** a pull request turns a `[MacFact]` red
- **THEN** `test (macos-latest)` fails in the merge queue's run and the pull request does not merge

#### Scenario: A pull request's own run

- **WHEN** a push to a pull request runs the CI
- **THEN** the four macOS jobs report as skipped under their check names, the required checks are
  satisfied, and no macOS runner is taken

#### Scenario: A red test in one group

- **WHEN** a pull request turns a Conformance case red on Windows
- **THEN** `test (windows-latest, conformance)` fails and the pull request cannot merge
