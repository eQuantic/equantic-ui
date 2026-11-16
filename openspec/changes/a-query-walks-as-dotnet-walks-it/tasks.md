# Tasks

## 1. A query walks as .NET's iterators walk

- [x] 1.1 The deferred operators check their arguments when called and answer a query the runtime walks lazily (#685)
- [x] 1.2 Skip and Take walk lazily, and every other deferred operator of the table runs when walked
- [x] 1.3 First, ElementAt, Last, Any and All stop at the element that answers; a projection of a list reads one element alone
- [x] 1.4 Prove it on both sides: `LazyQueryConformanceTests`, executed in .NET and in the browser's engine, fails against main and passes

## 2. A list refuses a walk once it changed

- [x] 2.1 A version on every List<T>, moved by every change .NET counts, and an indexer that checks its index (#863)
- [x] 2.2 foreach over a list, a query over one and List.ForEach compare it
- [x] 2.3 Prove it on both sides: `ListVersionConformanceTests` over every change .NET counts and every one it does not

## 3. What the review and the measurement found

- [x] 3.1 A query held in a local stops where one written in place does; Select's index overload reads no count nor element alone
- [x] 3.2 A foreach over a list held as an interface or a derived class refuses a change (`sequenceWalk`)
- [x] 3.3 With no model, ToList and ToArray over a query read it into an array; a guessed ForEach is the receiver's own; a guess claims only a call some List<T> overload takes
- [x] 3.4 The span paths of .NET's speed-optimized LINQ are listed with their reason in `ListSpanDivergenceConformanceTests`, both sides asserted

## 4. Documentation

- [x] 4.1 One ledger entry under `docs/ledger/` citing #685 and #863, and the wiki's LINQ and List<T> rows in every language they are written in
- [x] 4.2 The wiki says a callback that changes its list throws on the web, as .NET in WebAssembly does
