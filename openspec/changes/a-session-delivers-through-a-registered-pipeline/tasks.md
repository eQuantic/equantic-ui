# Tasks

## 1. The plan

- [x] 1.1 The plan is reviewed by two adversarial agents before any of it is written, and the findings are on the issue
- [x] 1.2 The plan is changed by them: kinds and not areas, one writer, the prover alone, the guard on a branch's creation

## 2. The setup

- [x] 2.1 The `pipeline` skill and its registry of five pipelines
- [x] 2.2 Seven agents, each with its tools, model, effort and cap of turns
- [x] 2.3 The hook that refuses a branch outside `<type>/<slug>`, in `.claude/settings.json`
- [x] 2.4 `scripts/new-issue.py` reads the issue types from the repository
- [x] 2.5 Check: the hook's self-test passes, refuses `claude/x` in this repository and passes it in another, and fails on a cited path that is not there

## 3. The delivery

- [x] 3.1 The self-test runs in CI's `roadmap` job
- [x] 3.2 The ledger entry, and the pointer in `CLAUDE.md`
- [ ] 3.3 Check against the real thing: one pipeline run end to end on a real issue, and what it cost
