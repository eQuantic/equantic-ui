# Tasks

## 1. The sweep

- [x] 1.1 `scripts/cancel-discarded-merge-groups.sh`, with `--dry-run` and `--self-test`. Verify: the self-test's six cases pass, and a sweep that cancels every run, one that cancels none, and one that takes any error for a 404 each fail it
- [x] 1.2 The `discarded-merge-groups` job in `ci.yml`. Verify: actionlint reports nothing that main's `ci.yml` does not
- [x] 1.3 Verify against the real queue: a dry run names the 11 runs whose groups are gone and keeps the 5 live ones, exactly the 5 refs under `gh-readonly-queue/main`
- [ ] 1.4 Verify in the queue: this pull request's own group run cancels the discarded runs

## 2. Documentation

- [x] 2.1 One entry for the ledger citing #1446, as a file of its own under `docs/ledger/`
