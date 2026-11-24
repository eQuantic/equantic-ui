# Proposal

Closes #1862, a Bug found when #1835 left the merge queue on 2026-11-24.

## Why

A merge group's run reads the wiki's master with the wiki branch of every pull request already on main
whose branch still waits. The step after a pull request lands merges its wiki branch into master and
deletes it, as the working agreement asks, and it does so whenever the pull request lands, not when
the queue is idle. `queue_checkout` in `scripts/checkout-wiki.sh` lists the wiki's heads once, clones,
then fetches each branch, so a branch deleted in between failed its fetch, and the script reported it
as a branch that does not merge. #1835's queue run 45301306261 failed that way on #1857's wiki branch,
and the queue dropped a pull request whose own change was sound.

## What Changes

- A branch whose fetch fails is looked up again among the wiki's heads. When it is gone, master is
  fetched again and merged, since it holds that branch's pages, and the run goes on, naming the branch
  as on master since the listing. Only a branch the wiki still lists that cannot be fetched, or a
  branch that does not merge, fails, with the message it had.
- A waiting branch master already holds merges as nothing, as the comment above `queue_branches`
  promises; a case of the self-test now pins it.
- `scripts/checkout-wiki.sh --self-test` gains three cases: a waiting branch merged into master and
  deleted at its fetch (a `git` that does exactly that stands in for the race), a waiting branch master
  already holds, and a branch the wiki still has whose fetch fails.

Nothing an app writes moves: no public surface, no developer surface.

## Impact

- `scripts/checkout-wiki.sh`: `queue_checkout` and its self-test
- `openspec/specs/working-agreement`: the requirement that a pull request's docs guards read its own
  wiki pages
- An entry under `docs/ledger/`
