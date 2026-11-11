# Tasks

## 1. The case lets its folder go

- [x] 1.1 Stop the app before disposing it in every case that starts one. Verify: the 13 cases of `HotReloadDecisionTests` pass on macOS

## 2. Proved against the real thing

- [x] 2.1 Run the cases on the Windows runner, with the fix and with main's file: the class alone twenty times, and the whole Server project ten times, the load the rest job gives it. Verify: the class alone fails on neither side, and under the whole project main's file fails the case with the `IOException` and the fix fails none

## 3. Documentation

- [x] 3.1 One entry for the ledger citing #1366, as a file of its own under `docs/ledger/`
