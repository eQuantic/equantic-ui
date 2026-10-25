# Spec Delta

## ADDED Requirements

### Requirement: Issues carry points, and a delivered issue is pointed by what it delivered

An issue on the board SHALL carry its points in the board's `Estimate` field, one of 1, 2, 3, 5, 8 or
13, and an issue bigger than 13 SHALL be split into sub-issues. A delivered issue that carries no
points SHALL be given the points the size of what it delivered says: the lines changed in the merged
pull requests that closed it, a pull request's lines shared equally among the issues it closes, the
files a tool writes left out, read against a scale whose limits double at each step (under 200 lines
is 1, under 400 is 2, under 800 is 3, under 1,600 is 5, under 3,200 is 8, under 6,400 is 13, and 21
past it).

#### Scenario: A pull request closes two issues

- **WHEN** a merged pull request that changed 1,000 lines closed two issues, and neither was pointed
- **THEN** each is given the points of 500 lines, 3

#### Scenario: A tool's lines are not work

- **WHEN** a pull request changed 120 lines of a source file and 900 of a `PublicAPI.Unshipped.txt`
- **THEN** the size it gives the issue it closes is 120 lines

#### Scenario: A person's points stand

- **WHEN** a delivered issue was given 8 points before its work began
- **THEN** it keeps 8, whatever its pull requests' size says

### Requirement: The board's Roadmap shows real dates and a measured forecast

`scripts/roadmap.py` SHALL measure the pace as the points delivered over the last 28 calendar days,
never divided by fewer than seven, and plan the open work with one lane per epic, each working at the
share of the pace it had, a lane that runs out of work handing its place to the next one waiting. With
`--apply` it SHALL write on every epic and feature the Start date its work really began, the Target
date it really ended once delivered, and the plan's dates otherwise, and SHALL show In progress a
parent whose sub-issues show it under way and Done a closed issue. It SHALL never close an issue, and
its report SHALL give each open milestone the plan's date, a range from repeated runs, and the date it
would land with all the pace.

#### Scenario: A finished lane hands its place on

- **WHEN** an epic that delivered in the window runs out of work, and another epic that did not deliver
  has work waiting
- **THEN** the waiting epic starts the next day, at the share the finished one had

#### Scenario: A parent's sub-issues are all closed

- **WHEN** a feature is open and every sub-issue it has is closed
- **THEN** the report lists it for a person to close, the plan counts it as work of its own, and the
  board is not told to close it

#### Scenario: The board already shows the plan

- **WHEN** the script is run with `--apply` and then run again the same day
- **THEN** the second run reports "The board already shows this."
