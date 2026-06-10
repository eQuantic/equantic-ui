# Implementation Plan — Phase 1: Transpiler Correctness & Conformance

> The linchpin of the whole project. See `ROADMAP.md` for the full picture. This plan is detailed
> enough to start coding now; later phases are sequenced at the end.

> **Status (2026-06-10): Phase 1 is essentially complete.** The conformance harness runs **492** green
> cases (target was ≥200), fail-on-unsupported diagnostics are in place (no silent miscompiles), and the
> supported subset is documented (see `docs/DOTNET-COVERAGE-PROGRAM.md` + the wiki `SupportedFeatures`).
> The milestone/exit-criteria notes below are updated to reflect this. The forward path is Phases 2–7 in
> `ROADMAP.md` (client router, hot reload, forms/validation, a11y, CSS engine, global state).

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
- [x] The supported subset is documented per construct (Supported / Partial / Unsupported). *Lives in
      `docs/DOTNET-COVERAGE-PROGRAM.md` (coverage matrix) + the wiki `SupportedFeatures` page rather than
      the originally-named `transpiler-supported-csharp.md`.*
- [x] A **conformance harness** runs N≥200 cases (transpile C# → execute JS via embedded Bun → compare
      to .NET). **492 cases green.**
- [x] Every one of the 24 fixed bugs has a conformance case (regression net).
- [x] `SemanticValidator`/the converter emit a build **error** (not a silent fallback) for unsupported
      constructs; the fallback sites are resolved to *supported* or an *explicit diagnostic*
      (`EQ2001/2002`, `EQ21xx` boundary, `EQ1001/1002` warnings).
- [x] Source maps validated by `SourceMapGeneratorTests` (generation + decode). *Remaining polish: a
      browser error → C# line smoke test (W4).*

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

**M1 — Seed corpus + regression backfill (W2 + W5). ✅ DONE (492 cases).**
The corpus grew far past the seed: **492 cases green** across arithmetic, strings (+ `StringComparison`),
the full LINQ surface, statement-level control flow, Math, enums, value types (records/structs/tuples,
named-class emission, inheritance/generics, SSR hydration), the date-time family, Nullable, StringBuilder
and the whole collections surface (List/Dictionary incl. record-keyed `valueMap`, HashSet, Queue, Stack,
LinkedList, SortedSet/SortedDictionary/SortedList, `ILookup[key]`), plus regressions for integer division,
Math.Truncate/Ceiling/Round, the switch var-pattern and the enum-as-string representation (#13).
The harness has already paid for itself several times over — it surfaced **four** previously-unknown
transpiler bugs, all now fixed:
- array creation (`new[]{…}` / `new int[]{…}`) emitted verbatim → invalid JS (ArrayCreationStrategy);
- array `.Contains` emitted `.contains` not `.includes` (receiver-type check in ContainsStrategy);
- `new HashSet<T>{…}` dropped its initializer → `new Set()` instead of `new Set([…])` (HashSetStrategy);
- `.Count` always emitted `.length`, wrong for sets (`.size`) and dictionaries (`Object.keys(x).length`)
  (type-aware MemberAccessStrategy).

The harness now supports a **shared type-declaration prelude** (e.g. an enum defined for both the
transpiler's semantic model and the .NET evaluator), serializes .NET objects in **camelCase** to
match the transpiler's property casing, evaluates .NET under **InvariantCulture** for deterministic
number/format output, and — when the emitted JS calls a runtime helper — **imports that helper from
the real bundled `runtime.js`** (Decision 3 resolved: import the real runtime, not a re-implementation).
This unlocked `ToString(format)` / formatted interpolation conformance (regression for #12). The two
items once "still to cover" are now done: **record/DTO object values** (records emit as named JS classes
and round-trip through SSR hydration) and **statement-level constructs** (the block-evaluating harness
mode landed — if/for/foreach/while/switch/try-catch/local functions). Deliberate divergences kept out of
the corpus and documented instead: midpoint/banker's rounding for raw JS arithmetic, culture-sensitive
string ordering, and default-context integer overflow (JS float64 doesn't wrap).
*Acceptance met: corpus green; CI fails if any divergence is introduced.*

**M2 — Fail-on-unsupported (W3). ✅ DONE.**
The converter/validator raise build **errors** for unsupported constructs — `EQ2001` (pointers, typed-
reference intrinsics, function pointers), `EQ2002` (`goto`/`goto case`/`goto default`), `EQ21xx`
(client-side `System.IO`/`Net.Http`/EF/threading/`Process`/P-Invoke/`Reflection.Emit` boundary) — and
anything else with no strategy is a warning (`EQ1001`/`EQ1002`), never a silent passthrough. Diagnostics
are MSBuild-canonical (`file:line`). *Acceptance met: unsupported snippets produce a clear build error;
nothing silently miscompiles.*

**M3 — Spec + source-map validation (W1 + W4). 🔄 Mostly done.**
The supported-subset spec is published as `docs/DOTNET-COVERAGE-PROGRAM.md` + the wiki `SupportedFeatures`
page (not the originally-named `transpiler-supported-csharp.md`); `SourceMapGeneratorTests` covers
generation + decode. *Remaining: the in-browser error → C# line smoke test.*

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
