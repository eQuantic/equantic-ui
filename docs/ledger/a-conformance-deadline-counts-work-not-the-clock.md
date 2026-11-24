- **2026-11-24 · A conformance case's deadline counts its work, not the clock**: a Bug under #1587
  ([#1850](https://github.com/eQuantic/equantic-ui/issues/1850)). The Persian walk of
  `CultureCalendarConformanceTests` failed once in a whole Conformance run at a load over 100 and passed alone:
  `JsExecutor.Run` gave Bun 20 seconds on the clock, and at a load of 160 over 14 cores a process gets about a
  ninth of a core, so a walk of three seconds ran past it. `ChildProcess.Run` takes a deadline on work, the
  process's own processor time, beside the clock's, and the conformance runner hands its timeout as work: a
  translation that loops for ever is still killed, and a case a loaded machine slows is not. The walk, its
  Bun paused for 25 seconds, passes with it and fails at 20 seconds without it.
