- **2026-11-26 · A still host asks for no frame while late work is posted around it, and the hot reload test says so
  every time**: `HotReloadTests.AnAppliedEditWakesEveryLiveHost` failed once under a load near 300, reading a still
  host's `NeedsRender` true before any edit was applied. A probe over the whole engine suite (#1239) named what could
  set it: work an earlier test left running, posted to the thread that draws after its test ended, which marked every
  live host's tree, and #1892 made a post the process's. A test now posts from another thread between a still host's
  frame and the assertion, and fails as the run did when a post marks every host again
  ([#1200](https://github.com/eQuantic/equantic-ui/issues/1200)).
