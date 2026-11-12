# Tasks

## 1. The plan ranks by the board's decisions

- [x] 1.1 `simulate` takes a ranking (the milestone of the lane's next issue, then its epic's Priority); the lanes that rank first share the pace equally and hand what they leave to the next rank the same day; a lane's queue puts the milestone's work ahead of work begun outside it
- [x] 1.2 Work shown In progress or In review sits in the sprint holding today; the report lists work under way a week with no pull request, and what each lane delivered in the window
- [x] 1.3 Verify: `roadmap_model_test.py` holds each rule, and a run against the board (2026-11-11) puts 1.0's plan on its focused date, 2026-11-15, where it was 2026-12-03

## 2. An issue is created whole

- [x] 2.1 `lacks()` and `incomplete()` in the model; `scripts/new-issue.py` refuses before any request, creates, adds the board item, sets status and points, and reads back
- [x] 2.2 `.claude/hooks/issues-are-born-whole.py` refuses `gh issue create`, the GraphQL `createIssue`, a REST `POST /issues` and an MCP tool that creates one, registered as a `PreToolUse` hook; its self-test runs in CI's `roadmap` job
- [x] 2.3 Verify: #1429 and #1430 created with the script, read back whole; each refusal proved; the self-test fails when a rule is broken

## 3. Documentation

- [x] 3.1 `docs/ROADMAP-FORECAST.md`, the Workflow section of `CLAUDE.md` and `AGENTS.md`, `CONTRIBUTING.md`, the roadmap skill
- [x] 3.2 The Priority of the thirteen epics set on the board (#898 Urgent, #157 High, seven Medium, four Low)
- [x] 3.3 One entry for the ledger citing #1429 and #1430, as a file of its own under `docs/ledger/`
