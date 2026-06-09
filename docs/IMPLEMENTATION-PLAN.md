# Implementation Plan — Phase 1: Transpiler Correctness & Conformance

> The linchpin of the whole project. See `ROADMAP.md` for the full picture. This plan is detailed
> enough to start coding now; later phases are sequenced at the end.

## Why this phase first

"100% C#, 0 JS knowledge" is only true if the transpiler is **complete and correct**. The 24 bugs
found in review — several silent miscompilations — prove the correctness net does not yet exist.
Until a C# construct either compiles to behavior-identical JS **or fails the build with a clear
message**, every other feature is built on sand.

## Objective

For the supported subset of C#, **the emitted JS behaves identically to .NET**, verified
automatically; and **any unsupported construct fails the build** with a C# `file:line` diagnostic
instead of producing wrong JS.

### Exit criteria (Phase 1 is "done" when)
- [ ] A `transpiler-supported-csharp.md` spec lists every construct as Supported / Partial / Unsupported.
- [ ] A **conformance harness** runs N≥200 cases: transpile C# → execute JS via embedded Bun →
      compare to the .NET result. Green in CI.
- [ ] Every one of the 24 fixed bugs has a conformance case (regression net).
- [ ] `SemanticValidator` emits a build **error** (not a silent fallback) for unsupported constructs;
      the ~69 silent fallbacks are each resolved to *supported* or *explicit diagnostic*.
- [ ] Source maps validated by an automated round-trip test (started: `SourceMapGeneratorTests`).

---

## The Conformance Harness (core deliverable)

The idea: for each case, compute the answer **two ways** and assert they match.

```
                ┌─────────────────────────────┐
  C# snippet ──▶│ CSharpToJsConverter (ours)  │── JS expr ─┐
  e.g. "7 / 2"  └─────────────────────────────┘            │
       │                                          ┌─────────▼──────────┐
       │                                          │ embedded Bun runs   │
       │                                          │ JSON.stringify(...) │── actual JSON
       │                                          └─────────────────────┘
       │        ┌─────────────────────────────┐
       └───────▶│ Roslyn CSharpScript (.NET)  │── value ── JSON ───────── expected JSON
                └─────────────────────────────┘
                         assert actual == expected
```

### Mechanics
- **.NET side (expected):** `Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript.EvaluateAsync<object>(csharp)`
  → serialize with `System.Text.Json` using a canonical options set (camelCase off, invariant).
- **JS side (actual):** transpile the C# expression to a JS expression, wrap as
  `globalThis.console.log(JSON.stringify(<jsExpr>))`, **prepend the runtime helpers** the output may
  call (`format`, `parseEnum`, `StyleBuilder`, …) by importing the built runtime, then execute with
  the embedded Bun binary and capture stdout.
- **Bun invocation:** resolve the platform binary
  (`src/eQuantic.UI.Runtime.Osx64/tools/bun/bun-darwin`, `…Linux64/…/bun-linux`, `…Win64/…/bun.exe`),
  `Process.Start` it on a temp `.js` file, read stdout, parse JSON. Cache one warm process if startup
  dominates.
- **Comparison:** compare normalized JSON. Handle the known divergences explicitly (int vs float
  formatting, decimal precision, `undefined` vs `null`) — these *are* the bugs we want to catch.

### New test project
`tests/eQuantic.UI.Conformance.Tests/` (xUnit), referencing the Compiler + the runtime build.
A `ConformanceRunner` helper exposes:
```csharp
Task AssertSameAsDotNet(string csharpExpression);          // pass/fail by value equality
Task AssertEmittedJs(string csharp, string expectedJs);    // optional: pin the emitted JS too
```

---

## Workstreams

### W1 — Supported-subset specification  `docs/transpiler-supported-csharp.md`
Enumerate, with status, every: operator & expression form; control-flow statement; pattern;
type (primitive, enum, Guid, DateTime, TimeSpan, Nullable, Tuple, record, struct, collection); and
BCL surface (string methods, LINQ operators, Math, Console, Dictionary, etc.). This doc is the
contract the harness and the validator enforce.

### W2 — Conformance harness  `tests/eQuantic.UI.Conformance.Tests/`  *(M0 ✅ done)*
- `JsExecutor` — runs JS with the embedded Bun (primary); on machines where this Bun build can't
  execute (e.g. an AVX-less VM — see Decision 1), falls back to a local Node so dev/CI isn't blocked.
  Test-only fallback; the shipped SDK path still uses Bun. Returns stdout (with timeout).
- `DotNetEvaluator` — Roslyn scripting wrapper returning a JSON-normalized value.
- `ConformanceRunner.AssertSameAsDotNet(csharp)`.
- Seed corpus: arithmetic (incl. integer division, `%` on negatives), string ops (Substring/IndexOf/
  Replace/Split/format), LINQ (Where/Select/First/FirstOrDefault/OrderBy stability/Distinct/Aggregate/
  Skip/Take/Any/All/Count/GroupBy), collections, control flow, pattern matching, enums, Math, ternary
  & precedence, null-coalescing.

### W3 — Fail-on-unsupported diagnostics  `src/eQuantic.UI.Compiler/Services/SemanticValidator.cs`
- Add an AST walk that flags constructs not in the supported set and reports a build **error** with
  `file:line` and a human message ("`decimal` banker's rounding is not supported; use … ").
- Audit the ~69 fallback/`return node.ToString()` sites: each becomes either real support (with a
  conformance case) or an explicit diagnostic. No silent wrong output remains.

### W4 — Source-map & debugging validation
- Extend `SourceMapGeneratorTests` to decode-and-verify mappings for a full emitted component (not
  just synthetic mappings).
- Add a smoke test that a thrown error's mapped position points at the right C# line.

### W5 — Regression backfill
- One conformance case per fixed bug (integer division, Math.Truncate/Ceiling/Round, ToString(format),
  enum-as-string, switch var-pattern, OrderBy stability, Distinct records). Locks them forever.

---

## Milestones (sequenced — start at M0)

**M0 — Harness walking skeleton (smallest end-to-end slice). ✅ DONE.**
`eQuantic.UI.Conformance.Tests` is live with `JsExecutor` + `DotNetEvaluator` + `Transpiler` +
`ConformanceRunner`; 7 arithmetic cases pass (incl. integer truncation across signs and `7.0/2 == 3.5`
proving float division is *not* truncated). *Acceptance met: the loop runs green.*

**M1 — Seed corpus + regression backfill (W2 + W5). 🔄 In progress.**
67 cases green across arithmetic, strings, LINQ, expression-level control flow and Math, plus
regressions for integer division, Math.Truncate/Ceiling/Round and the switch var-pattern. The harness
already paid for itself: it surfaced two previously-unknown transpiler bugs — array creation
(`new[]{…}` / `new int[]{…}`) was emitted verbatim (invalid JS), and array `.Contains` emitted
`.contains` instead of `.includes` — both fixed (ArrayCreationStrategy + a receiver-type check in
ContainsStrategy). Still to cover: enum-typed comparisons and `ToString(format)`/formatted
interpolation (need shared type context + the `format` helper injected — see Decision 3), and
statement-level constructs (need a block-evaluating harness mode — folds into M2).
*Acceptance: corpus green; CI fails if any divergence is introduced.*

**M2 — Fail-on-unsupported (W3).**
Validator errors on unsupported constructs; the 69 fallbacks triaged. *Acceptance: a curated list of
"known-unsupported" snippets each produce a clear build error (a negative-test suite), and no snippet
silently miscompiles.*

**M3 — Spec + source-map validation (W1 + W4).**
Publish `transpiler-supported-csharp.md`; source-map round-trip + error-mapping tests green.
*Acceptance: Phase-1 exit criteria all checked.*

---

## Decisions needed before/while coding
1. **Bun in CI / on dev machines**: confirm the embedded binary can *execute JS* on each target.
   Finding: the current dev machine is an AVX-less VM where the embedded Bun 1.3.6 (x64, non-baseline)
   crashes at JS startup (`Invalid DNS result order`) — `JsExecutor` transparently falls back to Node
   there. For real Bun coverage, ship/point at a **baseline Bun build** or run on AVX-capable hardware.
2. **Unsupported-but-common constructs** (e.g. `decimal` banker's rounding, culture-specific
   formatting): decide per item — emulate, or diagnose-and-reject. The spec (W1) records the call.
3. **Helper bundling for the harness**: import helpers from the built `runtime.js`, or maintain a
   small `conformance-preamble.js`. Recommend importing the real runtime to test the real output.

## After Phase 1
Phases 2–7 (client router → hot reload → forms/validation → component polish & a11y → first-party CSS
engine → global state & perf budgets) are sequenced in `ROADMAP.md`. Each should get its own plan doc
when it starts, following this same shape: objective → exit criteria → workstreams → milestones.
