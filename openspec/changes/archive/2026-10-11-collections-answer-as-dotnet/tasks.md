# Tasks

## 1. Copies

- [x] 1.1 `Queue<T>.CopyTo`, `Stack<T>.CopyTo` and `SortedSet<T>.CopyTo`, each with .NET's refusals in .NET's order. Verify: `CollectionMemberConformanceTests.CopyTo_WritesWhereDotNetWrites`, `CopyTo_RefusesAsDotNetRefuses` and `CopyToAndCapacity_WhereDotNetAnswersTheHostsNewline`, failing against main

## 2. The sorted set's operations

- [x] 2.1 `UnionWith`, `IntersectWith`, `ExceptWith`, `SymmetricExceptWith`, the six questions and `RemoveWhere`, keeping the instance .NET keeps of two equal elements. Verify: `SortedSet_SetOperationsAnswerAsDotNet` and `SortedSet_RefusesANullArgumentByName`, failing against main

## 3. Constructors

- [x] 3.1 The queue, stack and linked-list factories take no argument, a capacity or a collection, and refuse a null collection and a negative capacity. Verify: `Constructor_RefusesANullSourceByName` and `Constructor_BuildsWhatDotNetBuilds`, failing against main
- [x] 3.2 eqc hands `sortedSet`, `dictionary`, `sortedDictionary` and `sortedList` the name of the parameter a copied source binds to, and lowers `new List<T>(collection)` to `$eq.collections.listCopy`. Verify: the same two theories, and the compiler's pins of the three constructions

## 4. Documentation and the suites

- [x] 4.1 The transpiled library's pins regenerated and read: a list copy through `listCopy`
- [x] 4.2 #715's null-argument baseline regenerated, and only shrinking
- [x] 4.3 The wiki's SupportedFeatures page (EN + pt-BR) on the branch named like this one
- [x] 4.4 The suites, each alone and read by its exit code: Compiler, Web, Server, Conformance and the runtime's `TestRuntime`
