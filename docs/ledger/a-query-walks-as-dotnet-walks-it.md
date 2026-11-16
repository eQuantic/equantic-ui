- **2026-11-16 · A query and a list walk as .NET's iterators walk**: two Bugs, under #565 and #845
  ([#685](https://github.com/eQuantic/equantic-ui/issues/685), [#863](https://github.com/eQuantic/equantic-ui/issues/863)).
  Every LINQ operator was an array method over a copy of its source, read whole when it was called, so a chain ran
  each stage's callbacks before the next began, `Any` and `Take` ran every selector, and a query never saw an `Add`
  made before its walk. A deferred operator now checks its arguments when called and answers a query the runtime
  walks one element at a time, and an answering operator stops where it answers. A `List<T>` carries a version every
  change .NET counts moves, so a `foreach`, a query and `List.ForEach` refuse a list changed under them with .NET's
  `InvalidOperationException`, a list held as an interface it answers to included. The browser keeps that refusal
  under every walk, as .NET in WebAssembly does; .NET on a server reads a list under a `Where` or a `Select` by a
  span for `ToList`, `Count`, `First` and the other terminals it overrides, and those cases are listed with that
  reason in `ListSpanDivergenceConformanceTests`. Measured on both sides by `LazyQueryConformanceTests` and
  `ListVersionConformanceTests`. Proposed and archived through OpenSpec (`openspec/specs/transpiler-sequences`,
  `openspec/specs/runtime-collections`).
