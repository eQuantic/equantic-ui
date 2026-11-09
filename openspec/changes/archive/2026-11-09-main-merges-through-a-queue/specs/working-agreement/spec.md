## MODIFIED Requirements

### Requirement: A pull request's docs guards read its own wiki pages

CI SHALL check out the wiki for the documentation guards at the branch named exactly like the pull
request's head branch when the wiki has one and the pull request comes from this repository, and at
the wiki's default branch otherwise. In a merge queue's run, CI SHALL read the wiki's default branch
with the wiki branch of every pull request already merged whose branch still waits, and then of every
pull request the group puts on main, merged onto it in that order, leaving out a pull request from a
fork. The branch name SHALL be read as data and never
interpolated into a command. When the wiki's branches cannot be listed, when a group's commits do
not end with the pull request its ref names, or when two of its wiki branches conflict, the checkout
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

### Requirement: A pull request merges on its own CI, against the current main

The repository's ruleset SHALL require the CI's jobs (`test-runtime`, both `test` legs, `samples`, the
three `build-platform-runtimes`, `build-packages`, `commit-messages`, `openspec`, `session-start`,
`wiki-checkout` and `package-extension`) to pass, so a pull request merges only on a CI run that saw
the main it lands on: on its own head kept up to date with main, or, once the ruleset merges through
GitHub's merge queue, on the queue's run of it together with every pull request ahead of it. The CI
SHALL run every one of those jobs on a merge group, and no job that publishes. Every job in a
dependency chain SHALL be required, since GitHub counts a job skipped for a failed dependency as
passed.

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

### Requirement: A commit message carries no tool's attribution

CI SHALL fail when a commit message of a pull request, of a merge queue's group, or of a push to main,
carries a tool's attribution in a shape the guard names: a wrapper's tags, a generated-with or
generated-by line naming the tool, an assistant's co-authorship, the attribution comment a project's
pull request opens with, or a link into the tool's own pages, a session's or a conversation's. The
range it reads SHALL come from the event that started the run: a pull request's base and head, a merge
group's base and head, which holds the squash commits the queue composed, or a push's before and
after. A named phrase SHALL be refused wherever a line break falls in it. Prose that names the tool,
and a balanced markup snippet a message quotes, SHALL pass. The guard's self-test SHALL hold a fixture
that isolates each named shape and an event of each kind, and CI SHALL run it on every push. The
repository's squash default SHALL take a pull request's title and its commits' messages, never its
body, so the message a squash writes on main is one the job already read on the pull request.

#### Scenario: A squash message that took a project's pull request body

- **WHEN** the squash message of a pull request opened from a project carries the body's attribution
  comment, its link to the conversation and its signature line
- **THEN** the `commit-messages` job fails on the push to main, naming each line

#### Scenario: A squash message the queue composed

- **WHEN** a merge group holds two squash commits and the first carries a tool's attribution
- **THEN** the `commit-messages` job fails on the group's run, before the commit reaches main

#### Scenario: A range the checkout cannot measure

- **WHEN** a pull request's or a merge group's base is not in the checkout
- **THEN** the `commit-messages` job fails instead of reading the head alone

#### Scenario: A signature the squash composer wrapped

- **WHEN** a signature line's phrase is broken across two lines, with no link on either
- **THEN** the guard refuses it, reported as wrapped

#### Scenario: A pull request whose body carries a tool's attribution

- **WHEN** it is squash-merged with the repository's default
- **THEN** the commit on main carries its title and its commits' messages, and nothing of its body

#### Scenario: Prose that names the tool

- **WHEN** a commit message says the diff was reviewed in Claude Code, wrapped after the tool's first word, and quotes CLAUDE.md's first line
- **THEN** the guard passes it
