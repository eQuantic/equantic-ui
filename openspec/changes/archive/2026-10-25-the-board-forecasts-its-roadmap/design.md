# Design

## Context

The board (Project 11) holds every epic, feature, story, task and bug of the SDK as sub-issues, with
the organization's issue fields Start date and Target date, which the Roadmap view draws, and a
project field Estimate, unused. Cura's board solved the same problem with a script that reads the
board and writes the dates back (`tools/roadmap.py` in that repository). Its method counts tasks in
working days over two fixed lanes. Measured here on 2026-10-25, neither choice holds: most pull
requests merge on Saturdays and Sundays, a feature delivered on this board cost one pull request at
its median and a bug a third of one, and a feature's sub-issues are often the defects found around
it rather than its slices.

## Goals / Non-Goals

**Goals:** dates on the Roadmap view that are the board's own measurements; a forecast for each
milestone, 1.0 first, with its uncertainty said; a size for every issue, past and future, in one unit.

**Non-Goals:** a plan made by hand; dependencies between issues; closing what looks done.

## Decisions

- **Points, not pull requests or issues.** Counting units of unequal size made 1.0 land the next day.
  Fibonacci points in the existing `Estimate` field are the practice teams already know, and a pace
  in points is comparable with the estimates still ahead.
- **History pointed by its size, against the same scale.** Nothing delivered had points. Re-pointing
  three hundred issues by memory would be a guess; the lines a person wrote are the evidence the work
  left, and the scale's limits were set so its middle is the median delivered issue. Files a tool
  writes are excluded by the tool that writes them. Points given by a person are never overwritten.
- **Calendar days.** The weekend carries the most merges here (Saturday 93 and Sunday 87 of 387
  pull requests).
- **A lane per epic at its measured share.** A single queue would assume all the pace goes to the next
  issue in order, which the board does not do. A share is what the board did, and a lane that finishes
  passes its share on, so the number of epics moving at once stays what it was.
- **Work is every open issue with no open sub-issue.** An open parent whose sub-issues are all closed
  is work of its own, because those sub-issues may be defects rather than slices. The script never
  closes it: Cura's rule (all sub-issues closed, close the parent) would have closed the 1.0 security
  review (#306), whose three sub-issues are bugs.
- **The plan never reads what it writes.** The first run on the real board was not idempotent: it
  showed the parents under way In progress, and the order inside a lane put started work first,
  reading those same statuses, so the second run moved the dates again. What has started is read from
  the work itself, the open issues with no open sub-issue, and a test applies the plan to a board in
  memory and asks for it again.
- **A range, not only a date.** A thousand runs draw each day's capacity from the window and each
  issue's cost from its type's delivered costs. The plan's date is what is written; the range is what
  the report says about it.
- **Pure arithmetic, tested; the board read and written apart.** `roadmap_model.py` takes values and
  answers plans, and its tests run in CI with no network. `roadmap_board.py` is the only part that
  calls GitHub, with every query and its variables sent as JSON on standard input.

## Risks / Trade-offs

- Lines are a proxy for effort: an investigation that ends in a ten-line fix is pointed 1. The scale
  is for what nobody pointed; a person's estimate is kept.
- The board grows faster than it delivers today (453 issues gained in the window that delivered 1,141
  points). The forecast counts the board as it stands, and the report says how much it grew.
- A feature whose slices are not filed is forecast at what is filed. The plan's slices go on the board,
  and an issue past 13 points is listed until it is split.
