# Tasks

## 1. Measure

- [x] 1.1 Pin a new key inserted at the front, and every key replaced, on .NET and on the transpiled engine, and run them before the fix: both pass, since `CanUpdate` compares keys
- [x] 1.2 A ruler of the cost on both engines: every key of 10,000 replaced, and every key kept with a member of another type, against building them. Before the fix 1032x on .NET and 1648x and 2573x on the transpiled engine

## 2. The fix

- [x] 2.1 `UpdateKeyed` falls back to `NextUnclaimed` only for a key the table holds, and only when a key repeats in the old list
- [x] 2.2 The transpiled twin regenerated, and the twin cases and both rulers pass (1.4x and 2.5x on .NET), ceiling 20x

## 3. Documentation

- [x] 3.1 One ledger entry citing the issue, as a file of its own under `docs/ledger/`
