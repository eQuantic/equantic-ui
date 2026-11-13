# Tasks

## 1. Worktrees and the host's lock

- [x] 1.1 `scripts/one-build-at-a-time.py`, with a self-test that fails when its holders overlap
- [x] 1.2 Check: with a shared lock in place of the exclusive one, the self-test fails
- [x] 1.3 The skill: a delivery's worktree at intake, its removal after the merge, the state in it,
      every agent given its path, one build at a time per host, deliveries in parallel
- [x] 1.4 Every agent: where it works, and nothing left running
- [x] 1.5 CI's `roadmap` job runs the lock's self-test
- [x] 1.6 The lock passes a signal on to its command, exits 128 + N for a signal, reports its wait
      every minute and makes `EQ_BUILD_LOCK` absolute; check: the self-test fails without the
      forwarding and without the mapping
- [x] 1.7 Every command of a delivery runs with `EQ_WIKI_DIR` set; the worktrees' folder is one of the
      session's directories; the state outlives its worktree

## 2. What the pilot found

- [x] 2.1 The gate: a fix held to its finding, later rounds on their delta, the prover's weak tests
      strengthened by a fresh engineer, the two-round stop and its one exception
- [x] 2.2 Caps: the architect 100 turns and writing early, the engineer 200
- [x] 2.3 Handoffs: one engineer for areas that compile together; a fresh engineer for a small fix
      when the author's context is large
- [x] 2.4 Ship: CI started, Copilot's request registered, a person's review sorted, queue exits sorted
      by cause
- [x] 2.5 The docs writer cites only issues it read

## 3. The delivery

- [x] 3.1 The ledger entry
- [x] 3.2 Check: the branch hook's self-test finds every path the skill and the agents cite
