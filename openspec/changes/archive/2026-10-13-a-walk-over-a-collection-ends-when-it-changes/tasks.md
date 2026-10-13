# Tasks

## 1. Measure before changing

- [x] 1.1 Read .NET 10's `Queue<T>`, `Stack<T>`, `LinkedList<T>` and `SortedSet<T>` for where each bumps its version and where its enumerator compares it
- [x] 1.2 Write `CollectionWalkConformanceTests` and run them against 9b43eb2b: 25 of the 36 cases fail

## 2. The walks

- [x] 2.1 Give the four collections a version, bumped where .NET bumps its own, and walk each as it is, refusing a change at the next step in its assembly's words, with vitest cases
- [x] 2.2 Add `SortedSet.reverse()`, a walk from the largest element that is read when it is walked
- [x] 2.3 Prove it both ways: the conformance cases pass and the whole suite with them, and the compiler, Server and Web suites and the runtime's tsc and vitest pass

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures page, in English and Portuguese, on the wiki branch of the same name
- [x] 3.2 One `docs/LEDGER.md` line citing the issue
