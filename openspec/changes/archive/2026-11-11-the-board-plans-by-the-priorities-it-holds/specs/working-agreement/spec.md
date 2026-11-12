# Spec Delta

## MODIFIED Requirements

### Requirement: The board's Roadmap shows real dates and a measured forecast

`scripts/roadmap.py` SHALL measure the pace as the points delivered over the last 28 calendar days,
never divided by fewer than seven, and plan the open work with one lane per epic. A lane SHALL rank by
the issue it works next: first by the milestone that issue's work belongs to, the earliest due date
first, then by its epic's Priority (Urgent, High, Medium, Low, and an epic with none after Low). Each
day the lanes that rank first SHALL take the whole pace and share it equally. What a lane leaves of
its part, because it ran out of work or because its next issue ranks lower, SHALL go to the lanes that
rank next the same day. Inside a lane the work that carries the earliest milestone SHALL come before
work begun outside it. With
`--apply` it SHALL write on every epic and feature the Start date its work really began, the Target
date it really ended once delivered, and the plan's dates otherwise, and SHALL show In progress a
parent whose sub-issues show it under way and Done a closed issue. It SHALL never close an issue, and
its report SHALL give each open milestone the plan's date, a range from repeated runs, and the date it
would land with all the pace. It SHALL list the open issues the plan cannot count as they are, each
with what it lacks.

#### Scenario: The highest priority takes the pace

- **WHEN** an Urgent epic and a Medium epic both have work, and the Medium one delivered more in the
  window
- **THEN** the Urgent epic's work is planned first with the whole pace, and the Medium epic waits

#### Scenario: A finished lane hands its place on

- **WHEN** the epics that rank first run out of work partway through a day, and another epic ranks
  next
- **THEN** that epic starts the same day, with what the day has left

#### Scenario: A milestone's work goes ahead of a higher priority

- **WHEN** a Medium epic holds an issue of the earliest milestone, and an Urgent epic holds work in no
  milestone
- **THEN** the milestone's issue is planned first, and the Medium epic ranks below the Urgent one once
  its milestone work is done

#### Scenario: A parent's sub-issues are all closed

- **WHEN** a feature is open and every sub-issue it has is closed
- **THEN** the report lists it for a person to close, the plan counts it as work of its own, and the
  board is not told to close it

#### Scenario: The board already shows the plan

- **WHEN** the script is run with `--apply` and then run again the same day
- **THEN** the second run reports "The board already shows this."

### Requirement: The board's sprints are filled from what closed and what the plan finishes

`scripts/roadmap.py` SHALL keep the board's Iteration field as sprints of two weeks on the grid of its
first iteration, named Sprint N in order of start, adding sprints before them until the first holds the
repository's first day and after them until the last holds the end of the plan. With `--apply` it SHALL
put every delivered issue with no sub-issue in the sprint holding the day it closed, every open issue
with no open sub-issue shown In progress or In review in the sprint holding today, and every other
open issue with no open sub-issue in the sprint holding the day the plan ends it. It SHALL take an
issue out of its sprint when the plan no longer reaches it, and SHALL leave an open issue's sprint as
it is when no pace was measured. Its report SHALL list the issues shown under way for a week or more
with no pull request.

#### Scenario: The plan reaches past the last sprint

- **WHEN** the board's last sprint ends on 2026-10-26 and the plan ends an issue on 2026-12-01
- **THEN** sprints are added from 2026-10-27, two weeks each, until one holds 2026-12-01, and the issue
  is put in it

#### Scenario: The project is older than its board

- **WHEN** the board's first sprint starts on 2026-09-15 and the repository was created on 2026-01-22
- **THEN** sprints are added before it, two weeks each, until one holds 2026-01-22, and that one is
  Sprint 1

#### Scenario: A parent works below

- **WHEN** a feature is open and one of its sub-issues is open
- **THEN** the sub-issue is put in a sprint and the feature in none

#### Scenario: Work under way is this sprint's

- **WHEN** an issue is shown In review and the plan, which ranks its epic low, ends it three sprints
  from now
- **THEN** it is put in the sprint holding today

#### Scenario: The board already shows the sprints

- **WHEN** the script is run with `--apply` and then run again the same day
- **THEN** the second run reports "The board already shows this."

## ADDED Requirements

### Requirement: An issue is created whole

An issue SHALL be created with what the plan counts it by. Every issue needs a type. An epic needs its
Priority and nothing above it. Every other issue needs an open parent under an epic, and points, one
of 1, 2, 3, 5, 8 or 13, except a feature or a story created to be split, which carries none of its own. `scripts/new-issue.py` SHALL refuse, before any request, an issue that lacks
one of them, naming what it lacks. It SHALL create the issue with its type, parent, milestone,
assignee and priority, put it on the board with its points and status, read it back, and fail naming
whatever did not land. A session's `PreToolUse` hook SHALL refuse the other ways a session creates an
issue (`gh issue create`, the GraphQL `createIssue`, a REST `POST` to `/issues` and an MCP tool that
creates one), naming the script, and SHALL let a read, a comment, an edit and a phrase quoted in an
argument pass.

#### Scenario: A task with no points

- **WHEN** `scripts/new-issue.py --type Task --parent 160 --title … --body-file …` is run
- **THEN** it exits 2 naming the points, and no issue is created

#### Scenario: An epic with no priority

- **WHEN** an epic is to be created without `--priority`
- **THEN** the script exits 2 naming the priority

#### Scenario: A session runs gh issue create

- **WHEN** a session's Bash command is `gh issue create --title x`
- **THEN** the hook exits 2, the command does not run, and the session is told to use the script

#### Scenario: A commit message names the command

- **WHEN** a session commits with a message that says "gh issue create"
- **THEN** the hook lets the commit run
