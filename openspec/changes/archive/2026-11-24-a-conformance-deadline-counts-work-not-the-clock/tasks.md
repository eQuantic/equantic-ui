## 1. The deadline

- [x] 1.1 `ChildProcess.Run` takes `work`, held against the process's own processor time, with the clock deadline beside it
- [x] 1.2 `JsExecutor.Run` hands its timeout as work, with a ten-minute clock deadline
- [x] 1.3 `ChildProcessTests`: a program that waits without working is not held to its work, one that never stops working is killed at it, and one that waits past its clock is killed at the clock; the first and the last fail on a work budget taken on the clock
- [x] 1.4 The fa-IR walk with its Bun paused for 25 seconds (SIGSTOP, as a processor it never gets): it passes with the work deadline and fails at 20 seconds on main's clock deadline

## 2. Proof and documentation

- [x] 2.1 The full local suites, Conformance whole
- [x] 2.2 One ledger entry under `docs/ledger/` citing #1850
