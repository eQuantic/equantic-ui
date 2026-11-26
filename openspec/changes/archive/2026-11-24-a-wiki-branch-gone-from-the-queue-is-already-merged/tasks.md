# Tasks

## 1. The checkout

- [x] 1.1 Split the fetch from the merge in `queue_checkout`. On a fetch that fails, list the wiki's heads again; when the branch is gone, fetch master again and merge it, and go on. Verify a branch still listed whose fetch fails still fails with the message it had.

## 2. The self-test

- [x] 2.1 Add a case where a `git` standing in for the race merges a waiting branch into master and deletes it at its fetch, expecting the branch's page read from master. Verify it fails against the loop as it was ("the checkout failed") and passes with the fix.
- [x] 2.2 Add a case where master already holds a waiting branch that is kept, expecting its page and success.
- [x] 2.3 Add a case where the fetch of a branch the wiki still lists fails, expecting the failure and no directory behind. Verify `scripts/checkout-wiki.sh --self-test` passes all 25 checks.

## 3. The record

- [x] 3.1 Write the ledger entry under `docs/ledger/`, citing #1862.
