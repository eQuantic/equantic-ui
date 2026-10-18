- **2026-10-18 · A probe's build cannot hang the Web suite**: the suite hung, every thread waiting,
  until someone killed it, on about every other run, twice caught in the case that builds a scratch
  project through `SdkProbe.Run`. `dotnet test` sets `MSBUILDENSURESTDOUTFORTASKPROCESSES=1` for its
  own build and the test host hands it to every child, so MSBuild started each worker node of a probe's
  build on the probe's own stdout and stderr. The node is reusable and outlived the build, and the
  probe read both streams to their end with no deadline: measured, a probe's build exited after 2.4
  seconds and its streams were still open 30 seconds later, held by the node it left. One owner,
  `tests/Shared/ChildProcess.cs`, now starts every `dotnet` a test starts, without that variable and
  with node reuse and the build servers off, and holds the exit and the end of both streams to one
  deadline, so a late run fails as a `TimeoutException` naming the command. A guard fails on a
  `dotnet` started anywhere else under `tests/`, where five copies had started it. Two cases pin it, a
  shell that leaves a thirty-second sleep holding its stdout and a build that starts a worker node of
  its own on the probe's pipes: on the helper as it was, the first waited out the sleep and the second
  held the test host until the blame collector's timeout. The Web suite then ran whole five times in a
  row, 1,175 tests each, and every run passed, in 96 to 111 seconds
  ([#971](https://github.com/eQuantic/equantic-ui/issues/971)).
