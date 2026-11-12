---
name: roadmap
description: Bring the board's Roadmap up to date - the points of delivered issues, statuses read from sub-issues, real dates, and the forecast of the rest and of each milestone. Use whenever the work moves (an issue starts, a pull request merges), as the Workflow section of CLAUDE.md says.
allowed-tools: Bash(python3 scripts/roadmap.py)
---

1. **Run `python3 scripts/roadmap.py`**, which only reads, and read the whole report.
2. **Summarise it for the person**: the pace and the window it comes from, how many changes there are
   and of which kinds (points, statuses, dates), and each milestone's plan date, range and focused date
   beside its due date, and what each sprint delivered or is planned to hold. Name a milestone whose plan passes its due date, the open issues the
   report says the plan cannot count as they are (and what each lacks), those pointed past 13, and
   those shown under way for a week with no pull request.
3. **Distrust before applying** whatever looks wrong: a delivered issue pointed far from what it
   was, a date in the past for open work, a forecast that jumped since the last run, a parent the
   report lists as having every sub-issue closed. Explain it and leave it unapplied when it makes no
   sense.
4. **Apply with `python3 scripts/roadmap.py --apply`.** When a write is refused for permissions, do
   not work around it: give the person the command to run, in a `bash` block.
5. **Confirm with a second run**, which must say "The board already shows this."

The method is `docs/ROADMAP-FORECAST.md`. A new issue is created with `scripts/new-issue.py`, whole: its
type, an open parent under an epic and its points (1, 2, 3, 5, 8 or 13, against the table there), or,
for an epic, its Priority, which ranks the epic's lane for the pace. The script never closes an issue.
