# Tasks

## 1. Measure

- [x] 1.1 Pin a new key inserted at the front, and every key replaced, on .NET and on the transpiled engine, and run them before the fix: both pass, since `CanUpdate` compares keys
- [x] 1.2 A ruler of the cost: replacing every key of 10,000 against building them, before the fix 1032x on .NET and 2076x on the transpiled engine

## 2. The fix

- [x] 2.1 `UpdateKeyed` falls back to `NextUnclaimed` only for a key the table holds
- [x] 2.2 The transpiled twin regenerated, and the twin cases and the ruler pass (1.5x on .NET, 3.2x transpiled)

## 3. Documentation

- [x] 3.1 One ledger entry citing the issue, as a file of its own under `docs/ledger/`
