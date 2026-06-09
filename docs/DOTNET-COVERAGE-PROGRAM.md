# .NET Coverage Program — maximal C#→JS transpilation fidelity

> Goal: transpile as much of the .NET/C# surface as possible with behavior identical to .NET;
> where JS has no native equivalent, provide a faithful **.NET-compat runtime in TypeScript**; and
> where conversion is genuinely impossible, **fail the build with a clear C# diagnostic**.
> This program extends Phase 1 (see `docs/IMPLEMENTATION-PLAN.md`) and is driven by the conformance
> harness.

## The three mechanisms (every construct resolves to exactly one)

1. **Native strategy** — emit idiomatic JS when the runtime has an equivalent
   (`x.Where(...)` → `.filter(...)`, `"a".ToUpper()` → `.toUpperCase()`). Cheapest; preferred.
2. **.NET-compat runtime helper** — when JS lacks the semantics, emit a call into a TypeScript
   library that faithfully implements the .NET behavior. The transpiler emits these under the global
   **`$eq`** namespace, organised by domain — `$eq.num.dec/long`, `$eq.math.round`, `$eq.text.format/
   stringBuilder`, `$eq.time.dateTime/timeSpan/dateTimeOffset`, `$eq.enums.parse`, `$eq.collections.*`,
   `$eq.nullable.arith/cmp` (lifted operators), `$eq.equals` (structural equality), `$eq.css.*`.
   Brought in with one
   import per module (`import { $eq } from "@equantic/runtime"`, resolved by the page import map) and
   `$eq.*` cannot shadow user code.
3. **Fail-on-unsupported** — when no faithful conversion exists (unsafe code, P/Invoke, reflection
   emit, threading, filesystem on the client), the validator emits a build **error** with `file:line`
   and a remediation hint. Never silently miscompile.

## Methodology: the harness is the engine

The conformance harness (`tests/eQuantic.UI.Conformance.Tests`) runs C# both as transpiled JS and as
real .NET, and asserts identical results. We drive coverage by **flooding it with cases** across the
.NET surface; each failure is triaged into one of the three mechanisms:

```
add conformance cases  ──▶  run  ──▶  failures
                                        │
                 ┌──────────────────────┼───────────────────────┐
        native-fixable            needs .NET semantics      impossible
        → fix/strategy            → TS compat helper         → fail-on-unsupported
                 └──────────────────────┴───────────────────────┘
                                        ▼
                          green corpus = spec + regression net
```

The corpus simultaneously becomes the supported-subset spec (W1) and the permanent regression net.

## The .NET-compat TypeScript runtime (`eq` namespace)

A first-party library of faithful .NET implementations. Candidates, by need:

| .NET concept | JS gap | Plan |
|---|---|---|
| `long` / `ulong` (Int64) | JS number is float64 → loses precision > 2^53 | map to **BigInt**; emit `BigInt` literals + `eq.long` ops |
| `decimal` | no base-10 exact type | a `Decimal` class (exact), `eq.decimal` arithmetic + banker's rounding |
| `int`/`short`/`byte` overflow | JS doesn't wrap | accept JS number for the common case; offer checked ops; **document** |
| `char` | no char type (UTF-16 code unit) | represent as number where used arithmetically; string where textual |
| `Math.Round` midpoint | JS rounds half-up | `eq.round` with MidpointRounding.ToEven (banker's) |
| `string.Format` / `ToString(fmt)` | partial | `format` (exists) — extend specifiers (D, P, C culture-aware, custom) |
| `Guid` | none | `eq.Guid` (parse/new/format/equality) |
| `DateTime` / `DateTimeOffset` / `TimeSpan` | `Date` only, no formatting/arith parity | ✅ `$eq.time.dateTime` / `timeSpan` / `dateTimeOffset` (tick-precise; formatting, add/subtract, components, instant comparison) |
| `Convert.ToX` / `int.Parse` / `TryParse` | loose / different errors | `eq.convert` / strict parse matching .NET (throw on invalid, radix, overflow) |
| structural equality (records/structs) | reference equality | `eq.equals` (deep/structural) used by `==`, `Distinct`, `Contains`, dict keys |
| `IEqualityComparer` / `GetHashCode` | none | `eq.hash` + comparer support where needed |
| culture / `CultureInfo` | runtime locale | invariant by default; explicit culture pass-through |

Design notes:
- Helpers live in the runtime bundle and are imported on demand (the compiler tracks `UsedHelpers`).
- The conformance harness imports the **real** helpers from the bundled runtime, so helper behavior
  is validated against .NET, not re-implemented in the test.
- Prefer the smallest faithful implementation; cite any deliberate divergence in the spec.

## Coverage matrix (areas → status; filled in as the corpus grows)

- **Operators / expressions**: arithmetic ✅, comparison ✅, logical ✅, ternary ✅, null-coalescing ✅,
  bitwise ✅, integer division ✅, shift ✅, `checked(expr)`/`unchecked(expr)` ✅ (32-bit: unchecked
  wraps via `| 0`/`>>> 0`, checked throws `OverflowException`; long/ulong are exact BigInt and pass
  through; default-context overflow does NOT wrap — JS float64 — a documented divergence).
- **Numeric types**: int ✅, double ✅, decimal ✅(exact `Decimal`, wire-as-string + hydration),
  long/ulong ✅(BigInt, wire-as-string), float ✅, parsing/Convert ✅, overflow ⬜.
- **Strings**: core methods ✅, format specifiers ✅(F/X/N), padding/split/join ✅, interpolation ✅,
  `Trim(char)` ✅, char ops ✅, StringBuilder ✅(compat type), `StringComparison`/IgnoreCase ✅
  (Equals/StartsWith/EndsWith/Contains/IndexOf under Ordinal + IgnoreCase fold both sides; culture-
  sensitive ordering via `CompareTo` is intentionally out of scope — diverges from JS code-unit order).
- **Boolean / conversions**: bool ✅, `bool.Parse` ✅, `Convert.ToBoolean/Int32/Double/String/...` ✅.
- **LINQ**: Where/Select/SelectMany/indexed Select·Where/OrderBy/Distinct(By)/GroupBy/ToDictionary/Zip/
  Chunk/Min·MaxBy/Take(While)/Skip(While)/Aggregate/Sum/Min/Max/Average/Count/Any/All/First/Last/Concat/
  Reverse ✅; Join (order-preserving hash join), GroupJoin (left/group join), ToLookup ✅ (primitive
  keys); OrderBy/OrderByDescending + ThenBy/ThenByDescending ✅ (single stable composite sort, source
  copied); IGrouping ✅ (each group is the items array + a `key` prop — iterable, `g.Key`, `g.Sum()`,
  etc., first-occurrence order); remaining — ILookup `[key]` indexer ⬜ (use iteration/First).
- **Collections**: List ✅, Dictionary ✅, HashSet ✅, Queue ✅, Stack ✅ (compat types), LinkedList ⬜,
  sorted collections ⬜.
- **Types**: enum ✅, Guid ✅, DateTime ✅, TimeSpan ✅, DateOnly ✅, TimeOnly ✅, DateTimeOffset ✅
  (all tick-precise compat; DateTimeOffset = wall-clock + offset, compared by the instant),
  Nullable ✅ (HasValue/Value, GetValueOrDefault()/(fallback) with a type-aware default, lifted
  arithmetic/relational with null-propagation via `$eq.nullable.*`; no-arg GetValueOrDefault on
  DateTime?/Guid?/struct? returns null rather than the type default — use the fallback form there),
  Tuple ✅ (arrays; element access by position `Item1` and by declared name `(int X, int Y).X` → index),
  record/struct/tuple value semantics ✅ (records & structs are plain objects, tuples are arrays;
  `==`/`!=`/`.Equals`/`Contains`/`Distinct` are structural via `$eq.equals`, `with` copies; positional
  positional `new Point(1,2)`; deconstruction `var (a,b) = …` (tuples via array destructuring with
  discard holes, records via object destructuring keyed by Deconstruct order). **Records now emit as
  named JS classes** (Tier 1) — constructor + structural `equals` (delegated to by `$eq.equals`) +
  prototype-preserving `with` + `toString` + **user instance methods**; `==`/`Contains`/`Distinct`/
  deconstruction keep working unchanged. Non-record structs / tuples stay plain objects/arrays.
  Remaining tiers: real build-pipeline emission + import wiring (Tier 2), SSR re-hydration of record
  instances + generics/inheritance (Tier 3); record-keyed dictionaries.
- **Control flow**: expression-level ✅; statement-level ✅ — the harness now runs statement blocks
  (if/else, for, foreach, while, do-while, switch, break/continue, nested loops, try/catch/finally,
  local functions) in an IIFE and compares the returned value to .NET. (Found & fixed: local-function
  calls were emitted as `this.fn()`.)
- **Unsupported (fail-on-unsupported)**: ✅ **landed** — typed-reference intrinsics, pointers, function
  pointers raise `EQ2001`; `goto`/`goto case`/`goto default` raise `EQ2002` (no JS equivalent). `unsafe`/
  `fixed`/`lock` blocks unwrap to their body (lock is a no-op — JS is single-threaded — and pointer ops
  inside still raise `EQ2001`); a bare label drops to its inner statement. Client-side `System.IO`/
  `Net.Http`/EF·`System.Data`/OS threading/`Process`/
  P/Invoke/`Reflection.Emit` raise `EQ21xx` (boundary). Anything else with no strategy is a warning
  (`EQ1001`/`EQ1002`), not a silent passthrough. Diagnostics are MSBuild-canonical and fail the build.

## Prioritization (highest leverage first)
1. **Numeric + conversions** (this is where silent miscompiles hide: long precision, decimal, parsing).
2. **LINQ totality** (mostly native strategies; high everyday use).
3. **Strings completeness** (mostly native; char/StringBuilder/comparison gaps).
4. **Stand up the `eq` compat runtime** properly — Decimal ✅, Int64/BigInt ✅, Convert ✅, DateTime ✅,
   TimeSpan ✅, DateOnly ✅, TimeOnly ✅, DateTimeOffset ✅, Nullable ✅, structural equality ✅
   (records/structs/tuples via `$eq.equals`), Guid ✅.
5. **Statement-level harness mode** (W3) ✅ landed — control-flow blocks are validated end-to-end.
   **Fail-on-unsupported** ✅ landed.

## Definition of done (per area)
An area is "covered" when its conformance cases are green, every unsupported construct in it produces a
build error (negative tests), and its row in the matrix is ✅ with any divergences documented in the spec.
