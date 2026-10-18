# Proposal

Closes #971, a Bug under #849.

## Why

The Web suite hung, every thread waiting, until someone killed it, on about every other run. The
blame collector caught it twice in the case that builds a scratch project through `SdkProbe.Run`,
which put only the wait for the child's exit under a deadline: once the build had exited, the reads
waited for the end of each pipe with none.

Measured: `dotnet test` sets `MSBUILDENSURESTDOUTFORTASKPROCESSES=1` for its own build, and the test
host hands it to every child. With it, MSBuild starts each worker node on the probe's own stdout and
stderr, and a reusable node outlives the build. A probe's build exited after 2.4 seconds and its
streams were still open 30 seconds later, held by the node it left. Without the variable, or with
node reuse and the build servers off, nothing held them. A build that reuses an idle node starts
none, hence a hang on some runs only.

## What Changes

- One owner, `tests/Shared/ChildProcess.cs`, starts every process a test starts. `Run` takes the
  program and the deadline, drains both streams from the start, and holds the exit and the end of
  both streams to one deadline: a run that is late fails as a `TimeoutException` naming the command,
  killed with its tree when it is still running, and saying a process it started holds its output
  when it has exited. `Dotnet` starts `dotnet` without `MSBUILDENSURESTDOUTFORTASKPROCESSES` and with
  `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0` and `UseSharedCompilation=false`, a
  caller's own setting winning, so a probe's build leaves nothing running behind it. `Started` hands
  back a running process for code under test that owns its run.
- `SdkProbe.Run` keeps its shape and goes through it, and so do the thirteen other files that started
  a process of their own: four more that started `dotnet`, six that started git, the Bun version
  check, the conformance runner and hot reload's stand-in rebuild. The owner is linked into the Web,
  native engine, Server and Conformance tests. The ten `-nr:false` switches the Web cases passed go.
- A guard fails on a process started anywhere else under `tests/`, naming every file.
- CI's two `dotnet test` lines pass `--blame-hang-timeout 15m --blame-hang-dump-type none`, so a test
  that hangs stops the run and is named.
- Nothing a developer using the SDK writes or meets changes.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none: the change is in the test suites alone, and no requirement of the product moves, so the
change sets `skip_specs`.)

## Impact

- **Tests**: the Web, native engine, Server and Conformance suites, and their projects link the owner.
- **CI**: the test step's two `dotnet test` lines.
- **Public surface**: none. The developer surface does not move.
