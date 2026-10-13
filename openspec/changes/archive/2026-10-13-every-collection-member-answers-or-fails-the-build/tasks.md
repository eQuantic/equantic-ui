# Tasks

## 1. Measure before changing

- [x] 1.1 List the public members of the five types on .NET 10 and read where each grows, trims and bumps its version
- [x] 1.2 Write `CollectionSurfaceConformanceTests` and run them before the change: 73 of the 74 cases fail

## 2. The runtime

- [x] 2.1 Give `Queue` and `Stack` .NET's capacity, `TryDequeue`, `TryPop` and `TryPeek`, and `SortedSet` `TryGetValue`, with vitest cases
- [x] 2.2 Give `LinkedList` its node operations and `LinkedListNode` its list, refusals included, and export `linkedListNode`

## 3. The compiler

- [x] 3.1 Move `QueueStackStrategy` onto the IR with a members table per type, each take writing its `out` argument, a method group bound to its receiver, and every other member refused with EQ2004
- [x] 3.2 Pin the refusals and the node overloads' names in `CollectionSurfaceTranslationTests`, and regenerate the BCL audit's baseline
- [x] 3.3 Prove it both ways: the conformance cases pass and the whole suite with them, and the compiler, Server and Web suites and the runtime's tsc and vitest pass

## 4. Documentation

- [x] 4.1 The wiki's SupportedFeatures page, in English and Portuguese, on the wiki branch of the same name
- [x] 4.2 One `docs/LEDGER.md` line citing the issue
