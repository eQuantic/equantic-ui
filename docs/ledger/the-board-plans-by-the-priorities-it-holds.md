- **2026-11-11 · The roadmap gives the pace by the priorities the board holds, and an issue is created
  whole**: every session had moved to 1.0 and the bug sweep, and the plan still gave each epic the
  share of the pace it delivered over the last four weeks, 18% for 1.0, which put 1.0 on 2026-12-03
  against 2026-11-15 with the pace on it. A lane now ranks by the milestone of the issue it works
  next, then by its epic's Priority, and the lanes that rank first take the whole pace; work under way
  sits in the current sprint. The board had the Priority field and none of its thirteen epics used
  it, so an issue is now created whole or not at all: `scripts/new-issue.py` refuses one with no type,
  no parent under an epic or no points, or an epic with no priority, and a session's hook refuses the
  other ways of creating one. The window of the pace was measured against 7 and 14 days and kept
  ([#1429](https://github.com/eQuantic/equantic-ui/issues/1429),
  [#1430](https://github.com/eQuantic/equantic-ui/issues/1430)).
