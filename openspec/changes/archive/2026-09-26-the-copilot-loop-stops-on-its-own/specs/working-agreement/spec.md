# Spec Delta

## ADDED Requirements

### Requirement: The Copilot loop stops on its own

A pull request's author SHALL review the whole diff before opening it, and SHALL then sort every
finding of a Copilot round, those of the review's body included, as a defect (the code the pull
request changes does the wrong thing, or a guard passes where it should fail), as hardening,
documentation or a nit, or as wrong. A defect SHALL be fixed and proved both ways. Hardening,
documentation or a nit SHALL be fixed in the same push or filed as an issue, and a wrong finding
SHALL be answered with what shows so. Every thread SHALL be answered and resolved, since the ruleset
refuses a merge while one is open. A new round SHALL be requested only after a round that found a
defect, once per push of fixes, and never after the third round.

#### Scenario: A round that finds only nits

- **WHEN** every finding of a round is hardening, documentation or a nit
- **THEN** each is fixed in the same push or filed as an issue, and no new round is requested

#### Scenario: A round that finds a defect

- **WHEN** the second round finds a defect in the code the pull request changes
- **THEN** it is fixed and proved both ways, and a third round is requested after the push that carries every fix

#### Scenario: A defect after the third round

- **WHEN** the third round finds a defect
- **THEN** it is fixed and proved both ways, and no fourth round is requested

#### Scenario: A finding that does not hold

- **WHEN** a finding claims a failure that a measurement disproves
- **THEN** it is answered with that measurement and resolved, and no round is requested

#### Scenario: A draft

- **WHEN** a pull request is opened as a draft
- **THEN** no round starts until it is ready for review, and the author requests one if none has started a few minutes after

#### Scenario: A push with nothing to review

- **WHEN** a push only merges main, or only edits the pull request's body
- **THEN** it starts no round, since the ruleset does not review on push

### Requirement: A pull request merges on its own CI, against the current main

The repository's ruleset SHALL require the CI's jobs (`test-runtime`, both `test` legs, `samples`, the
three `build-platform-runtimes`, `build-packages`, `commit-messages`, `openspec`, `session-start`,
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
