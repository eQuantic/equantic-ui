- **2026-11-12 · A test of the code editor waits for an answer's effect, not for a queue that holds something**:
  `CodeDiagnosticsTests.AnAnswerFromAnotherThread_IsCountedAndAppliedOnTheUiThread` failed a merge group again with
  #1471's fix in place ([#1298](https://github.com/eQuantic/equantic-ui/issues/1298)). The UI dispatcher is static,
  so a post another test leaves behind reaches this test's queue, ended its wait before the answer's own post, and
  the drain ran without the answer. The test now drains until the editor stops checking, and a second test puts a
  stray post in front of a late answer: with the old wait it failed 20 of 20 runs, with the new one it passes 20 of
  20, and the original race passes 300 of 300 with and without a stray post, at a load of 105.
