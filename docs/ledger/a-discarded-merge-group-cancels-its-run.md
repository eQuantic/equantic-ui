- **2026-11-11 · A merge group the queue discarded has its run cancelled**: the queue discards a group
  when an entry ahead of it fails or leaves, and its run stayed queued. Its ref was unique, so the
  CI's concurrency never cancelled it. At 18:58Z, 11 such runs waited beside the 5 live groups' runs
  for the plan's 5 macOS runners, and #1378 timed out of the queue twice. Every merge group's run,
  every push to main and a run by hand now cancel the merge-group runs whose ref the queue deleted and
  whose commit is not on main, the sweeping run's own last
  (`scripts/cancel-discarded-merge-groups.sh`, in the new `discarded-merge-groups` job). A live group
  keeps its ref, a merged one is on main, and neither is touched; a lookup that fails any other way
  cancels nothing. The sweep is not a required check, and the ruleset does not change
  ([#1446](https://github.com/eQuantic/equantic-ui/issues/1446)). Proposed through OpenSpec, in
  `working-agreement`.
