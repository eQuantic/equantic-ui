- **2026-10-14 · A conformance case fails where the build refuses its code**: the conformance
  harness ran what the converter wrote for a case without reading what the build refuses, so a
  construct a build stops at ran its fallback as if it had compiled. #761 took a query's `let` for an
  answer of 3 where .NET answers 6, and the build refuses the query (EQ2008), so nothing of it ever
  reaches a browser. Every entry of the harness now reads the build's refusals where the build reads
  them: the converter's errors over the text the case would run, a warning refusing nothing, and the
  build's own verdict on the prelude's declarations, asked of `ComponentCompiler` with the scan eqc
  hands it, which brings every check a build runs beside the converter (EQ1007 and EQ2010 among them)
  with nothing encoded twice. A refused
  case fails before either side runs, naming each refusal by its id and text, and a case written for
  a fence declares it (`AssertStatementsRefusedByTheBuild`) and runs nothing. The module harness reads
  every module before it keeps the ones that wrote text, where a module refused before its emitter
  ran was dropped with its refusal. The whole suite with the rule refused four cases that had been
  passing: a dictionary keyed by `^1`, the gap of #394, now pinned as its EQ1004, and three refusals a
  build should not make, each pinned until it is fixed: `Task.FromException` (EQ2004,
  [#910](https://github.com/eQuantic/equantic-ui/issues/910)), `Equals(object)` on a builder (EQ2004,
  [#909](https://github.com/eQuantic/equantic-ui/issues/909)) and a number parsed with
  `(IFormatProvider?)null` (EQ2108, [#908](https://github.com/eQuantic/equantic-ui/issues/908)). The
  three queries of #761, a `let`, a second `from` and an `into`, are cases that expect EQ2008 now,
  pinned from the runner's side
  ([#907](https://github.com/eQuantic/equantic-ui/issues/907),
  [#761](https://github.com/eQuantic/equantic-ui/issues/761)). Proposed and archived through
  OpenSpec (`openspec/specs/working-agreement`).
