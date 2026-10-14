# Tasks

## 1. Measure before changing

- [x] 1.1 Measure .NET 10's sorted dictionary, sorted list and sorted set views, walks, refusals and set operations, and read `SortedSet.TreeSubSet` for where a view counts
- [x] 1.2 Write the conformance cases (`CollectionWalkConformanceTests`, `DictionaryViewConformanceTests`, `SortedSetViewConformanceTests`) and see them fail

## 2. The views

- [x] 2.1 One live view for the runtime's three dictionaries, refusing a change in each collection's words, with vitest cases
- [x] 2.2 The sorted dictionary's and the sorted list's walks, their views' walks and what each counts as a change
- [x] 2.3 `SortedSet.getViewBetween`, a view over its set's elements, with .NET's paths through the set operations
- [x] 2.4 eqc lowers `GetViewBetween` and a dictionary's `TryAdd` through `CollectionExtensions`; the BCL baseline and the translation tests follow
- [x] 2.5 Answer the review of the whole diff: a lookup is the dictionary's only when it binds to System.Collections.Generic's, a `Remove(key, out _)` that finds nothing is no change, a view refuses `Remove` before it searches, and a view counts again wherever .NET reads it as a collection
- [x] 2.6 Prove it both ways: the conformance cases pass and the whole suite with them, and the compiler, Server and Web suites and the runtime's tsc and vitest pass

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures page, in all five languages, on the wiki branch of the same name
- [x] 3.2 One ledger entry citing the issues, as a file under `docs/ledger/`
