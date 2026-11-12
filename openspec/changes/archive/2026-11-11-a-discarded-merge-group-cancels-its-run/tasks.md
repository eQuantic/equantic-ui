# Tasks

## 1. The sweep

- [x] 1.1 `scripts/cancel-discarded-merge-groups.sh`, with `--dry-run` and `--self-test`. Verify: the self-test's seven cases pass, a merged group and the sweeping run's order among them, and a sweep that cancels every run, one that cancels none, and one that takes any error for a 404 each fail it
- [x] 1.2 The `discarded-merge-groups` job in `ci.yml`. Verify: actionlint reports nothing that main's `ci.yml` does not
- [x] 1.3 Verify against the real queue: a dry run named the 11 runs whose groups were gone at 18:58Z (16 by 19:30Z) and kept the 5 live ones, exactly the 5 refs under `gh-readonly-queue/main`, with none merged
- [x] 1.4 Verify in the queue, after the merge: the run of the discarded group of #1362 (43110443258) named 8 discarded runs and cancelled itself last, and the run 43110526034 cancelled one more and kept the 2 live groups

## 2. Documentation

- [x] 2.1 One entry for the ledger citing #1446, as a file of its own under `docs/ledger/`
