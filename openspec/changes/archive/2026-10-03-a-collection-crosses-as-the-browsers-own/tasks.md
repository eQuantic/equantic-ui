# Tasks

## 1. The boundary knows the collection classes

- [x] 1.1 Add the collection classes to `src/Shared/BoundaryShape.cs` (set, sortedSet, queue, stack, linkedList, by definition name in `System.Collections.Generic`). Verify: the compiler and the generator build
- [x] 1.2 Write `{ collection, of }` from `HydrationSpec.Of` for each, before the list branch. Verify: a compiler test pins the spec of a `HashSet<string>`, a `HashSet<long>`, an `IReadOnlySet<DateTime>`, a `Stack<int>` and a `Queue<decimal>`, and fails against `main`
- [x] 1.3 Rebuild the class in the runtime's `hydrate`, a stack from the reversed array, idempotent, with vitest. Verify: `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime` passes

## 2. A collection crosses, prefetched or projected

- [x] 2.1 Accept a set and each collection class of scalars as a projection leaf. Verify: the generator test for `report.Roles.Contains("admin")` builds with no EQ2114 and projects `Roles`, and fails against `main`
- [x] 2.2 Run the crossing both ways in `HydrationCrossingTests`: a prefetch page holding a `HashSet<string>`, a `Stack<int>`, a `Queue<long>`, a `LinkedList<string>` and a `SortedSet<int>`, and a server value's `HashSet<string>` read through a pattern and `Contains`. Verify: the twin draws what the server drew, on the payload and on a navigation's state, and fails against `main`

## 3. Documentation

- [x] 3.1 Update the wiki's server-value paragraph (EN + pt-BR) for the collections that cross whole, on this pull request's wiki branch. Verify: the docs guards pass with `EQ_WIKI_DIR` on that branch
