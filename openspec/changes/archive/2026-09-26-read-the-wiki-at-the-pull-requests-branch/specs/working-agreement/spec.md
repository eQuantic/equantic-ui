# Spec Delta

## ADDED Requirements

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
