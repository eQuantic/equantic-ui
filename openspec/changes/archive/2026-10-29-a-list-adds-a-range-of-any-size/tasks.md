# Tasks

## 1. The runtime

- [x] 1.1 `addRange(list, collection)` and `insertRange(list, index, collection)` in `utils/list.ts`, exported
      under `$eq.collections`: the collection copied as `listCopy` copies it, the index checked after it, one
      `push` per element, and a list that is its own range read before it changes
- [x] 1.2 Vitest cases in `list.spec.ts`: a million elements added and inserted, a list as its own range, a
      string's chars, a set, and every refusal in .NET's words

## 2. eqc

- [x] 2.1 `ListMethodStrategy` calls the two functions for the bound overload through `ParameterTemplate`,
      and for a call no model binds by its spelling; `ConvertInsertRange` and the `push(...)` spread are gone
- [x] 2.2 `MinMaxStrategy`'s no-model path folds the values with `reduce`, `Math.max`'s and `Math.min`'s
      answer for no values as its seed
- [x] 2.3 Shape tests in `ListStrategyTests` and `LinqStrategyTests`, bound and unbound

## 3. The proof

- [x] 3.1 `ListRangeConformanceTests`, executed on both sides: a million elements from an array, a list and
      `Enumerable.Range`, added and inserted; a null collection, a string with a surrogate pair, an index
      past the end and below zero, a null collection beside a bad index, named arguments out of order, and a
      sorted set's view. Every one fails before the change and passes after; five cases that the spread
      already answered right (a list as its own range, single evaluation, a set, a queue and a dictionary's
      pairs) pass on both sides, and guard what a loop over the range could break
- [x] 3.2 The pinned twins regenerated (`SharedComponentTranspilationTests`, `PrimitivesRuntimeExportTests`)
      and the served runtime's budget checked in the Server suite

## 4. Documentation

- [x] 4.1 The wiki's Compiler page, in every language it is written in: `Min` and `Max` go through the runtime,
      and `AddRange` and `InsertRange` take a range of any size
- [x] 4.2 One ledger entry citing #1060, as a file of its own under `docs/ledger/`
