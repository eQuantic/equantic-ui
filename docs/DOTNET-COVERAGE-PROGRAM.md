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

**Faithful semantic model.** Type-gated strategies (e.g. record-keyed dictionaries) rely on Roslyn
resolving BCL generics like `Dictionary<RecordKey,V>`. The eqc build reconstructs the project's
compilation from its `.cs` files, which skip `obj/` — and that's where the SDK writes
`*.GlobalUsings.g.cs`. eqc therefore feeds that generated file into the compilation
(`ProjectCompilationHelper.GetGeneratedGlobalUsingsFiles`), so types used unqualified under
`<ImplicitUsings>` resolve exactly as in a real `dotnet build` (no hardcoded namespace list — the real
SDK artifact is consumed, honoring custom `<Using>` items too).

## The .NET-compat TypeScript runtime (`eq` namespace)

A first-party library of faithful .NET implementations. Candidates, by need:

| .NET concept | JS gap | Plan |
|---|---|---|
| `long` / `ulong` (Int64) | JS number is float64 → loses precision > 2^53 | map to **BigInt**; emit `BigInt` literals + `eq.long` ops |
| `decimal` | no base-10 exact type | a `Decimal` class (exact), `eq.decimal` arithmetic + banker's rounding |
| `int`/`short`/`byte` overflow | JS doesn't wrap | accept JS number for the common case; offer checked ops; **document** |
| `char` | no char type (UTF-16 code unit) | represent as number where used arithmetically; string where textual |
| `Math.Round` midpoint | JS rounds half-up | `eq.round` with MidpointRounding.ToEven (banker's) |
| `string.Format` / `ToString(fmt)` | partial | ✅ `$eq.text.stringFormat` (positional `{i}` / `{i:spec}` reusing the interpolation formatter, `{{`/`}}` unescape); `format` (`ToString(fmt)`) — remaining: extend specifiers (culture-aware C/P, custom numeric/date) |
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
  long/ulong ✅(BigInt, wire-as-string), float ✅, parsing/Convert ✅; explicit `checked`/`unchecked`
  overflow ✅, default-context overflow = documented divergence (JS float64 doesn't wrap — not a gap).
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
  etc., first-occurrence order); **ILookup `[key]` indexer ✅** (returns the group for a key, or an empty
  sequence for an absent key — never throws).
- **Collections**: List ✅, Dictionary ✅ (string/number/enum keys → plain object; **record/struct/tuple
  keys → `$eq.collections.valueMap`**, a structurally-keyed map so two equal-by-value keys collide as in
  .NET — construction, `d[k]` get/set, `ContainsKey`/`Add`/`Remove`/`Clear`/`TryGetValue`/
  `GetValueOrDefault`, `Keys`/`Values`/`Count`, `foreach` over `{key,value}` all routed; the plain-object
  path is untouched), HashSet ✅, Queue ✅, Stack ✅, **LinkedList ✅** (doubly-linked, `First`/`Last`
  nodes), **sorted collections ✅** (`SortedSet`, `SortedDictionary`, `SortedList` → key-sorted
  enumeration via `$eq.collections.sorted*`; default comparer — culture-sensitive string ordering out of
  scope) — all compat types.
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
  Tier 2 (real pipeline) ✅ — the parser discovers positional records by scanning, the compiler emits
  each as a standalone `export class` module (with its `$eq` import), and components that reference a
  record **reactively import it** (registry built by scanning — no hardcoded type list). Tier 3 (SSR) ✅
  — `hydrateValue` rebuilds a record instance from the plain SSR JSON on the field's prototype (witness
  = the field default), recursively, so nested records and compat-typed members are restored and the
  instance methods / `instanceof` survive hydration. Records (positional **and body**) and **plain
  structs** are all covered — a shared `ValueMembers` extraction (positional params + body
  auto-properties + public fields, in one canonical order) drives the constructor, equality, `with`,
  `toString` and the construction site (object-initializer mapped onto the constructor by member order,
  with per-member defaults). Record inheritance (`record Dog(…) : Animal(Name)` → `class Dog extends
  Animal` with a `super(…)` call, only own members re-assigned) and generic records (`record Box<T>` —
  type args erased) are covered too. **Record-keyed dictionaries** ✅ (`Dictionary<RecordKey,V>` →
  `$eq.collections.valueMap`, structural-equality keys) and **semantic (base-walk) component detection**
  ✅ are both landed.
- **Control flow**: expression-level ✅; statement-level ✅ — the harness now runs statement blocks
  (if/else, for, foreach, while, do-while, switch, break/continue, nested loops, try/catch/finally,
  local functions) in an IIFE and compares the returned value to .NET. (Found & fixed: local-function
  calls were emitted as `this.fn()`.)
- **Component class members**: a developer can author a page many ways and the transpiler covers them
  (hardened by an exhaustive authoring-coverage sweep — 10 categories, ~280 patterns via `AuthoringProbe`):
  - **Fields ✅** — `static`/`const`/instance, with transpiled initializers; statics referenced as
    `ClassName.field` (never `this.`); a type used only in a field initializer is reactively imported.
  - **Properties ✅** — auto (`{get;set;}`, with default applied in the ctor only when a prop wasn't
    supplied), computed get-only (`int X => expr`), get/set with accessor bodies, and `static` properties
    all emit as real TS getters/accessors/members. (Were previously dropped → `this.x` undefined.)
  - **Constructors ✅** — the C# ctor body now runs (e.g. `C(int id){ _id = id; }` emits `this._id = id`),
    not just the positional param→prop auto-assign. (Chaining `:this`/`:base`, overloads and C# 12 primary
    constructors remain on the backlog.)
  - **Expression-bodied `Build`** (`=> new Box{…}`) ✅ — was emitting "Build method not implemented".
  - **Field initializers ✅** — bare collection-initializer braces (`= { a, b }` / `= new[]{…}`) emit a JS
    array `[...]`; target-typed `new(args)` on a named type emits `new Type(args)` (was dropping the type).
  - **Component inheritance ✅** — a class is detected as a component transitively (subclass of a subclass of
    `Stateful`/`StatelessComponent`, via a fixpoint pre-pass), and an `abstract` base emits as an abstract TS
    class (its bodyless members are skipped) so concrete subclasses extend a real module.
  - **Static helper classes ✅** — a `static class` of helpers is emitted as its own module (mirrors the
    record path) and reactively imported, so `Helpers.Format(x)` resolves instead of leaking an import.
  - **`string.Format` ✅** — routes to `$eq.text.stringFormat`, which substitutes positional `{i}` / `{i:spec}`
    placeholders (the specifier reuses the interpolation formatter, so `{0:F2}` works) and unescapes `{{`/`}}`.
  - **Enum numeric casts ✅** — enums stay member-name strings (so equality / `switch` / dict-keys behave like
    .NET), and `(int)enum` / `(EnumType)int` bridge the string↔value gap via the enum's compile-time
    name↔value table: constant-fold to a literal (`(int)Status.Pending` → `1`, `(Status)1` → `'pending'`) or
    inline a generated map indexed by the operand. Was silently `Math.trunc('pending')` → `NaN`.
  - **`[Flags]` enums ✅** — represented NUMERICALLY (members emit their underlying value) because a
    member-name string can't express `Read | Write`. Bitwise `|`/`&`/`^` work natively, `HasFlag(f)` →
    `(v & f) === f`, and `(int)`/`(EnumType)` casts are the numeric identity. Non-flags enums are unaffected
    (still strings). Caveat: a `[Flags]` value round-tripped through the server is numeric, not the
    `JsonStringEnumConverter` `"Read, Write"` name list — keep flags state client-side.
  - **Interpolated-string text ✅** — `{{`/`}}` collapse to `{`/`}`, verbatim/raw text is decoded, and the
    result is re-escaped for the JS template literal (backslash, backtick, `${`). Was emitting raw `{{…}}`.
  - **Import collector ✅** — only types we actually emit (records/components/enums the resolver scanned)
    are imported; primitives (`int`/`bool`), `$eq`/BCL-compat types (`DateTime`/`Math`/`Guid`/…) and
    static-field names read as `ClassName.X` no longer leak as bogus `import { X } from "./X"`. Helper-method
    and property-accessor BODIES are scanned too (a type constructed only inside a helper is now imported).
  - Methods ✅, server actions ✅.
  (Surfaced + fixed via the Phase 2 sample + authoring sweep: see `docs/PHASE-2-CLIENT-ROUTER-PLAN.md` M3;
  compiler tests `ComponentStaticFieldTests`, `AuthoringCoverageTests`, `StringStrategyTests`,
  `EnumCastStrategyTests`, `EnumFlagsStrategyTests`; conformance `EnumConformanceTests`, `StringConformanceTests`.
  Remaining niche gaps — advanced pattern-matching (property/positional `var` bindings, list patterns),
  ctor chaining (`:this`/`:base`) & C# 12 primary constructors, generic-component `new T()` (JS type erasure)
  — are tracked as a backlog.) Cross-ref:
  **Phase 2 client router** is complete; `RenderContext.Route` (params/query) is the routing-facing surface.
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
