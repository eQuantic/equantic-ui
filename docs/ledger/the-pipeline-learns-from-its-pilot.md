- **2026-11-13 · The pipeline runs deliveries side by side, and holds a gate's fix to its finding**: a Task under #160
  ([#1526](https://github.com/eQuantic/equantic-ui/issues/1526)). The pilot (#1504) delivered a bug fix, a feature and
  a documentation change through the skill and measured them. Each delivery now has a worktree of its own from
  `origin/main`, where its agents work and its state lives, and every build of every delivery queues on one host lock
  (`scripts/one-build-at-a-time.py`), so several run at once on one machine. In every delivery a fix of one round made
  the next round's defect, so a fix now changes only what its finding names and later rounds review only their delta.
  The architect's and the engineer's caps rise, a small fix goes to a fresh engineer when its author's context is
  large, and shipping confirms that CI started and sorts every queue exit by its cause. Proposed and archived through
  OpenSpec (`session-pipelines`).
