## Why

Closes #685 and #863, sub-issues of Feature #849.

A LINQ query and a walk over a list ran ahead of .NET's:

- Every LINQ operator was an array method over a copy of its source, read whole before the first callback ran. .NET runs a query when it is walked, one element at a time, each only after the callback before it returned, so a callback that changes its source changes what is read next, and a list changed under the walk ends it: `d.Select(kv => { d["b"] = 5; return kv.Value; })` answered `1,2` where .NET answers `1,5`, and `l.Select(v => { l[1] = 5; return v; })` answered where .NET throws. An operator called and never walked ran every callback anyway, and one walked twice ran them once.
- A `List<T>`'s twin is the array itself, whose iterator reads the length at every step, so a `foreach` that added as it went never ended, where .NET's enumerator compares the list's version at every step and refuses the change.

## What Changes

- A list keeps a version, as .NET's `_version`, under a symbol on its array: every change .NET counts as one moves it (`Add`, `Insert`, a `Remove` that finds, `RemoveAt`, a non-empty range added or removed, `RemoveAll` that removes, `Clear`, `Sort` and `Reverse` always, and an element written through the indexer, which now checks its index as .NET does). A `foreach` over a list, `List<T>.ForEach` and every query over a list compare it at each step, the step after the last included, and throw .NET's InvalidOperationException. A `T[]` keeps none.
- The runtime's `utils/query.ts` runs the operators as .NET's iterators run: `Where`, `Select`, `SelectMany`, `Concat`, `Append`, `Prepend`, `TakeWhile`, `SkipWhile`, `Skip` and `Take` answer a query that checks its arguments when it is called and walks its source when it is walked; `Any`, `All`, `First`, `FirstOrDefault`, `ElementAt` and `Last` over a query stop at the element that answers, and a projection of a list reads one element alone, as .NET's does. Every other deferred operator (the orderings, the set operators, `Distinct`, `GroupBy`, `Join`, `Reverse`, `OfType`, `DefaultIfEmpty`, `Zip`, `Chunk`, `SkipLast`, `TakeLast` and the `By` family) runs its array lowering when its query is walked (`$eq.query.defer`), over the source as it is then, and refuses a null source when it is called, by the name its receiver binds to.
- `SkipStrategy` and `TakeStrategy` are gone: the table maps both to the runtime's walk.

## Impact

- Reaches `eQuantic.UI.Compiler` (`LinqTableStrategy`, `LinqSource`, `LinqArguments`, the LINQ strategies, `ListMethodStrategy`, `Place`, `ForEachStatementStrategy`, `LocalDeclarationStrategy`, `Eq`) and the runtime (`utils/query.ts`, `utils/list-version.ts`, `list.ts`, `collections.ts`, `view.ts`, `eq.ts`).
- `PublicAPI.Unshipped.txt` of `eQuantic.UI.Compiler` removes `SkipStrategy` and `TakeStrategy`, a tool surface whose removals are free at any version. `developer-surface.baseline.txt` does not move.
- The served runtime grows by the two modules; the budget and a query benchmark before and after are in the pull request.
