- **<date> · A LINQ query runs when it is walked, and a list refuses a change under its walk**: bugs under #849
  ([#685](https://github.com/eQuantic/equantic-ui/issues/685),
  [#863](https://github.com/eQuantic/equantic-ui/issues/863)). Every LINQ operator was an array method over a
  copy of its source, read whole before the first callback ran, so a callback that changed the source changed
  nothing the query read, and an operator called and never walked ran every callback anyway. The runtime's
  `utils/query.ts` now walks the source one element at a time for the operators that have a walk, stops the
  answering ones at the element that answers, and runs every other deferred operator's lowering when its query
  is walked. A `List<T>`'s twin is the array, whose iterator never ended a walk that added as it went; a list
  keeps .NET's version now, and a `foreach`, `ForEach` and every query over it refuse a change with .NET's
  InvalidOperationException. Edgar chose laziness over documenting the difference, in its own pull request with
  its cost measured. Proved by the conformance suite on both sides. Proposed and archived through OpenSpec
  (`transpiler-sequences`, `runtime-collections`).
