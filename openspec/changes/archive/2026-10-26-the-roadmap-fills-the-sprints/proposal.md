# Proposal

Closes #1103, a feature of the 1.0 epic #898.

## Why

The board's Iteration field had three two-week sprints, the last ending on 2026-10-26, and no issue in
any of them, so the board could not say what a sprint delivered or what the next one holds. The
roadmap already knows both: when each issue closed, and when the plan finishes each open one.

## What Changes

- `scripts/roadmap.py` keeps two-week sprints on the board's grid, back to the sprint holding the
  repository's first day (Sprint 1) and forward as far as the plan reaches.
- A delivered issue goes in the sprint it closed in, and an open one in the sprint the plan finishes
  it in, moving as the plan moves. A parent whose work is open below it is in no sprint.
- The report says what each sprint delivered in points and what it is planned to hold, and the lines
  written on main in it, the one measure the months before the board have.

Nothing an app writes changes; the change reaches `scripts/`, `docs/ROADMAP-FORECAST.md` and the
`roadmap` skill.

## Capabilities

### New Capabilities

### Modified Capabilities
- `working-agreement`: the board's sprints are kept by the roadmap.

## Impact

`scripts/roadmap_model.py` and its test, `scripts/roadmap_board.py`, `scripts/roadmap.py`,
`docs/ROADMAP-FORECAST.md`, `.claude/skills/roadmap/SKILL.md`.
