# Proposal

Closes #685, a Bug under #565 (The transpiler's fences hold on every path, continued), and #863, a Bug
under #845 (The transpiler's fences hold on every path, third part): a LINQ query and a `List<T>` walked
in the browser as no .NET iterator walks them.

## Why

- Every LINQ operator was an array method over a copy of its source, read whole when the operator was
  called (#685). A callback that changed its source did not change what was read next, a chain of
  operators ran every callback of one stage before the next began, `Any`, `First` and `Take` ran
  their selector over every element, and a query made before an `Add` never saw it.
- A `List<T>` had no version (#863): `foreach` over a list kept walking after the body added to it,
  where .NET throws `InvalidOperationException` ("Collection was modified; enumeration operation may
  not execute."), and `List.ForEach` let its action change the list.

## What Changes

- A deferred operator (`Where`, `Select`, `SelectMany`, `Concat`, `Append`, `Prepend`, `TakeWhile`,
  `SkipWhile`, `Skip`, `Take`, the orderings, `Distinct`, `GroupBy`, `Reverse`, `OfType`,
  `DefaultIfEmpty`, `Zip`, the set operators, `Chunk`, `Join`, `GroupJoin`, `SkipLast`, `TakeLast`)
  checks its arguments when it is called, by .NET's parameter names, and answers a query the runtime
  walks one element at a time when it is walked. An answering operator (`Any`, `All`, `First`,
  `ElementAt`, `Last`) stops at the element that answers, and a query over a list answers `First`,
  `Last` and `ElementAt` from that element alone, as .NET's iterators do.
- A `List<T>` carries a version that every change .NET counts moves (`Add`, `Insert`, a `Remove` that
  finds, `RemoveAt`, a range that is not empty, `Clear`, `Sort`, `Reverse`, a write through the
  indexer, which now checks its index). `foreach` over a list and a query over one compare it at every
  step and refuse a change, and `List.ForEach` refuses a change its action makes. So does a `foreach`
  over a list held as an interface it answers to (`IEnumerable<T>`, `IList<T>`, `IReadOnlyList<T>`…) or
  as a class derived from it, as .NET's list enumerator refuses the change through every face.
- A query held in a local, a field or a method's answer stops where .NET's stops, as one written in
  place does, and `Select`'s index overload runs every selector before the element that answers, as
  .NET's plain iterator does, with no count nor element read alone.
- The browser refuses a change under every walk, which is .NET's documented contract and what .NET in
  WebAssembly answers, whose LINQ is optimized for size. .NET on a server reads a list under a `Where` or
  a `Select` by a span for `ToList`, `ToArray`, `Count`, `First`, `Last`, `ElementAt` and `Any()`, an
  optimization it does not promise; those cases are listed, each with that reason, in
  `ListSpanDivergenceConformanceTests`, which asserts both sides.

For a developer: a query and a loop over a list run their callbacks in the order .NET runs them and
fail where .NET fails. No public surface and no developer surface moves; eqc's own surface gains the
runtime entry points it calls (`$eq.query.*`, `$eq.collections.list*`).

## Capabilities

### Modified Capabilities

- `transpiler-sequences`: a LINQ query reads its source when it is walked, one element at a time.
- `runtime-collections`: a walk over a `List<T>` ends when the list changes.

## Impact

- eqc (`Linq/*` strategies, `ListMethodStrategy`, the `foreach` and local declaration strategies, the
  IR migration baseline, which shrinks by the two strategies the table now answers) and the runtime
  (`utils/query.ts`, `utils/list-version.ts`, `utils/collections.ts`, `utils/list.ts`). The served
  runtime grows by the query walker and by the shared components' lists, which add, clear and walk
  through the list's version (about 3.8 KB gzipped, recorded as this change's budget delta). Realizers, the Photon shells and the SDKs are untouched.
- Principles: .NET's semantics, measured on both sides by the conformance suite.
