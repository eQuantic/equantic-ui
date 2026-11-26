- **2026-11-26 · A table's scroll step is held by the work it does, and a busy runner no longer fails it**: the
  table's perf alarm read the mean of forty timed steps on a shared runner, and a busy macOS runner averaged 22.9 ms
  against the 12 ms alarm on a pull request that touched no table. The guard is now the work a step does, counted,
  which no runner's load can move: the cells it builds, and the layout nodes its frame walks, the same ones as the
  frame before it. A step that rebuilt the window every time cost 1.4 ms, far under the alarm, and fails the count. The
  time alarm stays, loose, read on the fastest step, for the work per node and outside the tree that the counts cannot
  see ([#1397](https://github.com/eQuantic/equantic-ui/issues/1397)).
