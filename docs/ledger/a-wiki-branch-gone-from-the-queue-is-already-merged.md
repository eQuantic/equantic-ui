- **2026-11-24 · A wiki branch merged and deleted while a merge group checks the wiki out is read from master**: a
  merge group's run reads the wiki's master with the wiki branch of every pull request already on main whose branch
  still waits, and the step after a pull request lands merges that branch into master and deletes it whenever the pull
  request lands. `queue_checkout` listed the wiki's heads once and fetched each branch afterwards, so a branch deleted in
  between failed its fetch, was reported as a branch that does not merge, and the queue dropped #1835. A branch whose
  fetch fails is now looked up again: when it is gone, master is fetched again and merged, since it holds that branch's
  pages, and only a branch the wiki still lists fails. The checkout's self-test stands a `git` in for the race
  ([#1862](https://github.com/eQuantic/equantic-ui/issues/1862)). Proposed and archived through OpenSpec
  (`openspec/specs/working-agreement`).
