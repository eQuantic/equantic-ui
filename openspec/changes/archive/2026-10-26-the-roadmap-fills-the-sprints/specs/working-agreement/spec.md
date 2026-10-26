# Spec Delta

## ADDED Requirements

### Requirement: The board's sprints are filled from what closed and what the plan finishes

`scripts/roadmap.py` SHALL keep the board's Iteration field as sprints of two weeks on the grid of its
first iteration, named Sprint N in order of start, adding sprints before them until the first holds the
repository's first day and after them until the last holds the end of the plan. With `--apply` it SHALL
put every delivered issue with no sub-issue in the sprint holding the day it closed, and every open
issue with no open sub-issue in the sprint holding the day the plan ends it. It SHALL take an issue
out of its sprint when the plan no longer reaches it, and SHALL leave an open issue's sprint as it is
when no pace was measured.

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

#### Scenario: The board already shows the sprints

- **WHEN** the script is run with `--apply` and then run again the same day
- **THEN** the second run reports "The board already shows this."
