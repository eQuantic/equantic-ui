# Tasks

## 1. The arithmetic

- [x] 1.1 Points: the scale, the files a tool writes, a pull request's lines shared among its issues, a
  person's points kept (`scripts/roadmap_model.py`)
- [x] 1.2 The pace in points over 28 calendar days with a floor of seven, and each epic's share
- [x] 1.3 The plan: work as open issues with no open sub-issue, a lane per epic, a freed place handed
  on, the order inside a lane; the dates, the statuses, never a close
- [x] 1.4 Milestones: scope, plan date, a seeded range, the focused date
- [x] 1.5 Tests for each rule, each shown to fail on a mutation of the rule it holds, run by a `roadmap`
  job in CI

## 2. The board

- [x] 2.1 Read Project 11 with the issue fields, Estimate, the closing pull requests and the files of
  those an unpointed delivered issue needs; write points, statuses and dates (`scripts/roadmap_board.py`)
- [x] 2.2 The report and `--apply` (`scripts/roadmap.py`)
- [x] 2.3 Point the open issues against the delivered examples, and file the slices a 1.0 plan names
  that the board lacked (E3, E4 and E5 of #937)
- [x] 2.4 Apply on the real board, and run again to see "The board already shows this."

## 3. The agreement and its documents

- [x] 3.1 The Workflow section of `CLAUDE.md` and `AGENTS.md`: points on a new issue, and the script run
  whenever the work moves
- [x] 3.2 `docs/ROADMAP-FORECAST.md`, indexed in `docs/README.md` and cited from `ROADMAP.md`
- [x] 3.3 The `roadmap` skill for Claude Code
- [x] 3.4 A ledger entry under `docs/ledger/` citing #1090
