## MODIFIED Requirements

### Requirement: A pull request's docs guards read its own wiki pages

CI SHALL check out the wiki for the documentation guards at the branch named exactly like the pull
request's head branch when the wiki has one and the pull request comes from this repository, and at
the wiki's default branch otherwise. In a merge queue's run, CI SHALL read the wiki's default branch
with the wiki branch of every pull request already merged whose branch still waits and whose name no
open pull request uses, and then of every pull request the group puts on main, merged onto it in that
order, leaving out a pull request from a fork. The branch name SHALL be read as data and never
interpolated into a command. A waiting branch the default branch already holds SHALL merge as nothing. A
waiting branch that leaves the wiki between the listing of its branches and its fetch, as the step
after a pull request lands merges it into the default branch and deletes it, SHALL be read from the
default branch fetched again, which holds its pages, and the checkout SHALL go on. When the wiki's
branches cannot be listed, when a group's commits do not end with the pull request its ref names, when
a branch the wiki still has cannot be fetched, or when two of its wiki branches conflict, the checkout
SHALL fail and leave nothing behind, rather than read the default branch.

#### Scenario: A pull request that carries its wiki pages

- **WHEN** the wiki has a branch named like the pull request's head branch
- **THEN** the guards read that branch's pages

#### Scenario: A run with no wiki branch of its own

- **WHEN** the wiki has no branch of that name, or the run is not a pull request nor a merge group
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

#### Scenario: A merge group of two pull requests that carry pages

- **WHEN** a merge group puts two of this repository's pull requests on main, each with a wiki branch
- **THEN** the guards read the default branch with both branches merged onto it

#### Scenario: A pull request already on main whose wiki branch waits

- **WHEN** the queue merged a pull request whose wiki branch nobody has merged into master yet
- **THEN** the next group's guards read that branch's pages too

#### Scenario: A merge group with a fork's pull request

- **WHEN** a pull request in the group comes from a fork whose head branch is named like a wiki branch
- **THEN** that branch is not merged

#### Scenario: A merge group whose commits do not name its pull request

- **WHEN** the group's commits do not end with the pull request its ref names
- **THEN** the checkout fails and leaves no directory behind

#### Scenario: Two wiki branches that conflict

- **WHEN** two pull requests in the group change the same lines of a page
- **THEN** the checkout fails and leaves no directory behind

#### Scenario: A waiting branch master already holds

- **WHEN** a merged pull request's wiki branch has been merged into master and kept
- **THEN** its merge brings nothing, and the guards read master with the group's pages

#### Scenario: A waiting branch merged and deleted while the run checks the wiki out

- **WHEN** a merged pull request's wiki branch is listed, then merged into master and deleted before
  the run fetches it
- **THEN** the run fetches master again, the guards read that branch's pages from it, and the
  checkout goes on

#### Scenario: A branch the wiki still has that cannot be fetched

- **WHEN** the fetch of a wiki branch fails while the wiki still lists it
- **THEN** the checkout fails and leaves no directory behind
