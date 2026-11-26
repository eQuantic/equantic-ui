- **2026-11-26 · A spreadsheet's alarms are held by the work they time, and a busy runner no longer fails them**: the
  spreadsheet's three perf alarms read the mean of forty timed steps or keys, and a busy macOS runner measured a
  scroll step at 21.6 ms against the 12 ms alarm, taking a pull request that touched no sheet out of the queue. As
  the table's were (#1397), each is now held by the work counted (the cells a step builds, and the layout nodes its
  frame walks, the same ones as the frame before it for a scroll step, a screenful for a key), beside the same
  loose time alarm read on the fastest run. Under a CPU load on every core the scroll step's mean was 13.2 ms and its
  fastest 3.9 ms. A step that rebuilt the window every time cost 4.4 ms at its fastest and fails the cell check
  ([#1915](https://github.com/eQuantic/equantic-ui/issues/1915)).
