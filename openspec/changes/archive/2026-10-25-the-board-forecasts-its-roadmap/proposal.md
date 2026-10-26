# Proposal

Closes #1090, a feature of the 1.0 epic #898.

## Why

The Roadmap view of the board is empty: no issue carries a Start date or a Target date, and nothing on
the board says how far 1.0 still is. The board has no size either. A pull request on it closes a
one-line bug or a three-thousand-line slice alike, so a forecast that counts issues or pull requests
divides big work by a pace made of small work, and dates 1.0 for tomorrow.

## What Changes

- Issues carry points, in the board's `Estimate` field: 1, 2, 3, 5, 8 or 13, and an issue bigger than
  13 is a parent split into slices. The Workflow section says a new issue is filed with its points.
- A delivered issue nobody pointed gets the points its size says: the lines a person wrote in the
  pull requests that closed it, without the files a tool writes, against a scale that doubles at each
  step. That is how the board's history is pointed, and how it stays pointed.
- `scripts/roadmap.py` reads the board, measures the pace in points over the last four weeks, plans
  the open work across the epics at the share each had, and writes the real dates of what happened and
  the forecast of the rest on epics and features, with the statuses a parent's sub-issues show. It
  reports each milestone's plan date, a range from a thousand runs, and the date it would land with all
  the pace. It never closes an issue.
- `docs/ROADMAP-FORECAST.md` writes the method down, and CI runs the arithmetic's tests.

Nothing an app writes changes: the SDK's public surface and its developer surface are untouched. The
change reaches `scripts/`, the Workflow section of `CLAUDE.md` and `AGENTS.md`, `docs/`, CI and a
Claude Code skill.

## Capabilities

### New Capabilities

### Modified Capabilities
- `working-agreement`: issues carry points, and the board's Roadmap shows real dates and a forecast
  measured on the board.

## Impact

`scripts/roadmap.py`, `scripts/roadmap_board.py`, `scripts/roadmap_model.py` and its test,
`.github/workflows/ci.yml` (a `roadmap` job), `CLAUDE.md` and `AGENTS.md` (Workflow), `ROADMAP.md`,
`docs/README.md`, `docs/ROADMAP-FORECAST.md`, `.claude/skills/roadmap/`.
