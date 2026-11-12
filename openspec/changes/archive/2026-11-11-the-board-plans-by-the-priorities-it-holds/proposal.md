# Proposal

Closes #1429 and #1430, Tasks under #160.

## Why

On 2026-11-11 every session was on 1.0 and the bug sweep, and the roadmap still gave 1.0 18% of the
pace, the share it delivered over the last four weeks. That put its plan date on 2026-12-03 against
2026-11-15 with the pace on it. The sprints filled with epics nobody was on, and work already under
way was planned into later sprints. The plan read history and none of the decisions: the board had a
Priority field, and none of its thirteen epics had one set, because nothing asks for it when an issue
is created.

## What Changes

- `scripts/roadmap.py` gives the pace by rank instead of by past share. A lane ranks by the milestone
  of the issue it works next, then by its epic's Priority. The lanes that rank first take the whole
  pace and share it equally, and a lower rank is worked only when nothing above it is left. Inside an
  epic, a milestone's work goes ahead of work begun outside it.
- An open issue shown In progress or In review sits in the sprint holding today.
- The report shows, beside each lane, the part of the window it delivered. It lists the open issues
  the plan cannot count as they are, and the work shown under way for a week with no pull request.
- `scripts/new-issue.py` creates an issue whole, or refuses: a type; for an epic, its Priority; for
  any other issue, an open parent under an epic and points. It puts the issue on the board and reads
  it back. A session's `PreToolUse` hook, `.claude/hooks/issues-are-born-whole.py`, refuses the other
  ways of creating an issue.

Nothing an app writes changes, and neither the public surface nor the developer surface moves. The
pace's window was measured and kept: 7 days give 39 points a day, 14 give 30, 28 give 31.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `working-agreement`: the roadmap ranks lanes by milestone and priority, work under way sits in the
  current sprint, and an issue is created whole.

## Impact

- `scripts/roadmap_model.py`, `scripts/roadmap.py` and the new `scripts/new-issue.py`.
- `.claude/hooks/issues-are-born-whole.py` and `.claude/settings.json`.
- CI's `roadmap` job.
- `docs/ROADMAP-FORECAST.md`, the Workflow section of `CLAUDE.md` and `AGENTS.md`, `CONTRIBUTING.md`
  and the roadmap skill.
