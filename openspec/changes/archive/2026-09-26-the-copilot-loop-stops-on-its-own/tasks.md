# Tasks

## 1. The rule

- [x] 1.1 The Workflow section of `CLAUDE.md` and `AGENTS.md` (steps 1 and 3), `CONTRIBUTING.md` and the pull request template say it
- [x] 1.2 The ruleset's `copilot_code_review` has `review_on_push` off (changed on 2026-09-26, only that field)
- [x] 1.2b The ruleset requires thirteen CI jobs, the whole dependency chain, and a head up to date with main (#287; read back, the other rules unchanged)
- [x] 1.3 Check: `WorkflowSectionTests` holds the two sections identical, and `./scripts/check-openspec.sh` passes

## 2. Documentation

- [x] 2.1 One `docs/LEDGER.md` line citing #446
- [x] 2.2 Check: the line is in the chronology once, and the Workflow's copies (CLAUDE.md, AGENTS.md, CONTRIBUTING.md, the template) state the same ceiling
