# Spec Delta

## ADDED Requirements

### Requirement: A pull request's docs guards read its own wiki pages

CI SHALL check out the wiki for the documentation guards at the branch named exactly like the pull
request's head branch when the wiki has one, and at the wiki's default branch otherwise. The branch
name SHALL be read as data and never interpolated into a command. When the wiki's branches cannot be
listed, the checkout SHALL fail and leave nothing behind, rather than read the default branch.

#### Scenario: A pull request that carries its wiki pages

- **WHEN** the wiki has a branch named like the pull request's head branch
- **THEN** the guards read that branch's pages

#### Scenario: A run with no wiki branch of its own

- **WHEN** the wiki has no branch of that name, or the run is not a pull request
- **THEN** the guards read the wiki's default branch

#### Scenario: A name that is only the tail of a branch

- **WHEN** the head branch is `some-change`, and the wiki has `fix/some-change` and no `some-change`
- **THEN** the guards read the wiki's default branch

#### Scenario: The wiki's branches cannot be listed

- **WHEN** listing the wiki's branches fails, though cloning it would work
- **THEN** the checkout fails and leaves no directory behind

#### Scenario: A branch name written to run

- **WHEN** the head branch's name holds `$(touch pwned)` and a backquoted command
- **THEN** nothing it names runs

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
