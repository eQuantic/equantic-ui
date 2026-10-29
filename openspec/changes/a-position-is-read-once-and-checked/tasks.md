## 1. From the end

- [x] 1.1 Add the subscript kind to `Place`, its position counted back from the receiver's `length` through the rule a twin's indexer and a list face already follow (`CountedBack`).
- [x] 1.2 Move `IndexFromEndStrategy` to the IR, reading through the place, and take it off `ir-migration.baseline.txt`.

## 2. A list's positions

- [x] 2.1 Add `listInsert`, `listRemoveAt`, `listRemoveRange` and `listGetRange` to `utils/list.ts`, with .NET's checks in its order and words.
- [x] 2.2 Lower the bound calls through `ParameterTemplate`, and the model-less ones by position, refusing a named argument.

## 3. Tests

- [x] 3.1 A conformance test (`tests/eQuantic.UI.Conformance.Tests/PositionConformanceTests.cs`): reads, writes, compounds and steps from the end over a receiver made by a call, an offset with an effect and one written as a sum; each list member with named arguments, its order of evaluation and every position it refuses, both sides executed.

## 4. Documentation

- [x] 4.1 A ledger entry under `docs/ledger/`.
