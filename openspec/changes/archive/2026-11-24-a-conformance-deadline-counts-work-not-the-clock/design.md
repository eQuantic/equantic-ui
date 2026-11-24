## Context

Every process a test starts goes through `tests/Shared/ChildProcess.cs`, and the conformance suite starts one per case: thousands of Bun runs a session. A deadline on the clock measures the machine's load as much as the program's work.

## Decisions

**A deadline on work, read from the process's own processor time.** `Process.TotalProcessorTime` counts every thread of the process, which is where Bun does a case's work. The wait is taken on the clock in slices of the work still left, since a process cannot spend more of a processor's time than passes, and the time is read only when a slice ends, so a run that ends quickly costs one read.

**Opt-in, and taken by the conformance runner.** A `dotnet` a test starts does its work in worker processes, whose time its own process does not count, so a work deadline would never run out for a build. Those callers keep the clock, and `JsExecutor` hands its timeout as work.

**The clock still holds a program that waits.** A deadlock spends no processor time; ten minutes on the clock, under CI's hang timeout, fails it naming its command.

## Risks / Trade-offs

- A run that waits without working is now failed after ten minutes instead of twenty seconds. It is a failure either way, and CI's hang timeout is fifteen.
