# Tasks

## 1. One equality

- [x] 1.1 `ElementEquality` decides `EqualityComparer<T>.Default` from the type, and the dictionary's key, a set's element, a list's and an array's search and LINQ's `Contains` read it; the runtime's `KeyEquality` takes a generated comparison for a tuple, an anonymous type or a pair. Verify: `ListSearchConformanceTests.Search_ComparesAsTheDefaultComparer` (the rows of #425), failing against main

## 2. A set by slot

- [x] 2.1 The slot table `utils/slots.ts`, the dictionary moved onto it, and `HashSet<T>` built on it with .NET's members and capacity. Verify: `HashSetConformanceTests` (the rows of #438 and #531, each set operation, a copy, TrimExcess), failing against main
- [x] 2.2 Every place a set is made builds the runtime's: a constructor, an initializer, a collection expression, `ToHashSet`, the hydration spec. Verify: `CollectionEqualityLoweringTests`, `HydrationSpecEmissionTests`, and `hydrate.spec.ts`

## 3. A list's and an array's members

- [x] 3.1 .NET's introspective sort, both helpers, and its binary search; the comparers that cross (`SortOrders`) and EQ2007 for the rest. Verify: `ListSortConformanceTests` (the rows of #488, the heap sort traced on both sides), failing against main
- [x] 3.2 `Find`, `FindLast`, the ranges of `FindIndex`, `FindLastIndex`, `IndexOf` and `LastIndexOf`, `RemoveAll` and `CopyTo`, every check in .NET's words. Verify: `ListSearchConformanceTests.Find_AnswersAsDotNet` and `RemoveAllAndCopyTo_AnswerAsDotNet`, failing against main

## 4. string.Join

- [x] 4.1 Join reads any sequence and writes each value by the conversion a concatenation applies; values passed one by one by their own type. Verify: `StringJoinConformanceTests` (the rows of #429 and #441), failing against main

## 5. Documentation and the suites

- [x] 5.1 The transpiled library's pins regenerated and read; the BCL audit and the IR baseline regenerated and read; the served runtime's budget moved, the commit saying what the bytes bought
- [x] 5.2 The wiki's SupportedFeatures page (EN + pt-BR) on the branch named like this one, `docs/LEDGER.md`, `docs/DIAGNOSTICS.md`
- [x] 5.3 The suites, each alone and read by its exit code: Compiler, Web, Server, Conformance and the runtime's `TestRuntime`
