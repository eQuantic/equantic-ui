# Proposal

Closes #1850, a Bug under #1587.

## Why

`CultureCalendarConformanceTests.EveryDayACalendarWrites_IsWrittenAndReadAsDotNetCountsIt(fa-IR)` failed once in a whole Conformance run at a load average over 100, and passes alone. A case that answers differently under load is a defect of the case or of the harness, and here it is the harness: `JsExecutor.Run` gave the engine 20 seconds on the clock, and `ChildProcess.Run` killed it past them. The walk writes and reads back about 92,000 Persian dates in one Bun process, three seconds alone on a 14-core machine at a load of 30. At a load of 160 a process bound to the processor gets about a ninth of a core, and the walk ran past the clock. Nothing is shared between cases: each runs in a fresh Bun process, and every culture a test sets is its thread's.

## What Changes

- `ChildProcess.Run` takes `work`: the program is held to its own processor time as well, and killed once it has used that much. The clock deadline then holds only a program that waits without working.
- `JsExecutor.Run`'s timeout is that work, so a translation that loops for ever is still killed, and a case a loaded machine slows is not. Its clock deadline is ten minutes, under CI's `--blame-hang-timeout 15m`, so a run that waits without working fails naming its command before the host is taken down.
- Nothing a developer using the SDK writes or meets changes: the change is in the tests' process helper alone, and neither the public surface nor the developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none: the change is in the test harness alone, and no requirement of the product moves, so the change sets `skip_specs`.)
