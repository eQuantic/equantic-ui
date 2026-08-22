# Fase 5 — translating the bound tree

## Why

Every strategy in `CodeGen/Strategies/` reads SYNTAX and asks the semantic model what it needs,
when it needs it. That is where the `Knows()` gate, the `Original()` map for rewritten nodes and
the five hand-written semantic rules (string conversion, float store, integer width, char
promotion, enum arithmetic) come from: each one re-derives, from syntax plus a type query, a fact
the compiler already wrote down once in the BOUND tree — Roslyn's `IOperation`.

The bound tree has every implicit conversion as a node, every operator resolved to its method,
`checked` as a bit, lifted operators as a flag, `foreach` with its element conversion, `using`
with its dispose. Translating IT instead of the syntax collapses the derived rules into direct
reads, and covers every SITE a construct appears at, not only the ones a strategy remembered.

## How: the strangler, again

The IR and its writers (`CodeGen/Ir/`) do not change — they never knew what they were fed. What
moves is the INPUT side, one construct at a time, with the conformance suite (both sides executed)
and the pins as the net. A bound-tree mechanism either owns a rule completely (and the syntax
strategies drop theirs) or yields to the syntax strategy that still owns it; never both, or the
value converts twice.

## Slices

| # | Slice | State |
|---|---|---|
| 0 | `SemanticHelper.GetOperation` — Original-aware, Knows-guarded access to the bound tree | done (Fase 4.2: `IsChecked`) |
| 1 | `ValueFlow` — the dispatcher settles every expression for the implicit conversion wrapping it in the bound tree; owns char promotion and int→long (the syntax rules were removed); yields on text (`StringConversion`) and decimal (the decimal yield ended in slice 7 — ValueFlow owns to-decimal now) | done |
| 2 | Text: a value on its way into a concatenation (boxed, or a string operand — of `+` or of `+=`'s VALUE, never its target) or a plain interpolation hole is settled by ValueFlow; the concatenation and interpolation strategies dropped their calls. `s += flag` prints "True" for the first time | done |
| 3 | `IBinaryOperation` read where the syntax guessed: `IsLifted` decides the nullable lift (the operand-type guess stays for model-less worlds); the long branch no longer re-wraps an operand ValueFlow made a BigInt | done |
| 4 | User-defined operators, for IN-SOURCE types whose twin the emitter writes: conversion operators are emitted (`Money.fromInt`, `Money.toInt`) and called from ValueFlow (implicit) and the cast strategy (explicit); unary operators are named by arity (`opNegate`, no collision with `opSubtract`) and called through `IUnaryOperation.OperatorMethod`; compound assignments through `ICompoundAssignmentOperation.OperatorMethod`. Framework wrappers keep passing the primitive through. 10 cases both sides | done |
| 5 | Statements, instrumented first: 7 foreach cases (element conversions, deconstruction, a string's chars) found 2 gaps, both the ELEMENT conversion the syntax never shows — `foreach (long l in ints)`, `foreach (int code in chars)`. `SemanticHelper.ForEachInfo` reads `ForEachStatementInfo.ElementConversion` and `ValueFlow.Apply` (the conversion table, now a function) converts each element; a user-defined element conversion (`foreach (Money m in ints)`) works through the same call. `using` instrumented next and NOT right: the statement form disposed, the DECLARATION form (`using var r = …;`) was a bare const with a comment — never disposed, for as long as it existed. The block now owns it: what follows runs in a try whose finally disposes, reverse order for several, `disposeAsync` for `await using` (6 cases). Patterns deferred: the syntax PatternConverter is conformance-covered and the bound tree adds nothing observable yet | done |
| 6 | Explicit conversions, instrumented first: 47 cast cases found 19 gaps in the cast-only table — every cast across the BigInt boundary (`(int)aLong` put a BigInt into Math.trunc; `(double)aLong` left a BigInt where a number was due), every `checked` cast (wrapped instead of throwing), `(float)` (dropped entirely), and a decimal source (Math.trunc of a Decimal object). `ValueFlow.Apply` IS the table now — the pair of types decides, `IConversionOperation.IsChecked` is the one extra bit — and the cast strategy keeps only what is cast-only: the enum name↔value maps and the spelled-type fallback (which borrows `IntegerWidth.Wrap`'s masks, so they exist once). Learned on the way: decimal→integral ALWAYS throws past the edge in C# (no unchecked form), and a decimal operand still has no invariant representation (`-3.99m` negates into a plain number), so the cast coerces (`$eq.num.dec(x).toNumber()`) like every use site — until slice 7. `(int)Math.Min(a, b)` is an identity conversion and now emits nothing, which is the one whitespace-plus pin change | done |
| 7 | Typed hydration — the boundary, then the wraps go. The compiler KNOWS every boundary type, and now writes it down: `HydrationSpec` computes a small JS spec from the symbol (`'long'`, `'decimal'`, date tags, `[spec]` lists, `{ dict: spec }`, a record NAME whose twin carries its own map), emitted as `static $hydration` on state classes, write-once pages and record twins, and as `$eq.hydrate(await invoke(...), spec)` around Server Action results. The runtime coerces ONCE there (`utils/hydrate.ts`; spec wins, the old witness stays as fallback), which made records-in-lists keep their prototype for the first time. THEN the defensive wraps went: the binary/compound/LINQ `$eq.num.dec(x)`/`long(x)` coercions are gone — operands are typed, ValueFlow owns to-decimal too — and what the wraps were silently carrying surfaced as five typed seams, each now owned where it belongs: the BigInt SHIFT COUNT (a C# shift count stays int; BigInt `<<` wants both sides), `l /= 3` (BigInt `/` already truncates; Math.trunc throws on it), dictionary keys read back in a foreach (`$eq.entries(d, 'long')` restores the key TYPE, not just a number), `List<long>.Sum()/Average()` (0n seed; Average divides as the double C# returns), and unary/step on decimal (`-3.99m` was negating into a plain NUMBER through its toString — now folds to `dec("-3.99")`; `m++` steps by `add(dec(1))`, postfix-in-value-position recovers the old value exactly). 30-case instrument (TypedValueFlowConformanceTests) + hydrate/adoption vitest + emission pins (HydrationSpecEmissionTests) | done |

## Lessons recorded on the way

- `GetOperation(node)` returns the OPERAND; the implicit conversion is its `Parent`. Hook there.
- `decimal` and `long` HAVE an invariant runtime representation now (slice 7): the typed boundary
  hydrates every server value once, so a Decimal is a Decimal and a BigInt a BigInt everywhere,
  and the defensive per-use coercions are gone. What the wraps were silently carrying was not
  defense but five UNTYPED SEAMS (shift counts, BigInt `/=`, dictionary keys, LINQ seeds, unary
  minus on decimal) — each is a typed conversion at its own site now. A coercion that remains is
  a lie about the type system: remove it or name the seam it stands on.
- A compound assignment's VALUE arrives wrapped in its own conversion node (like a binary
  operand), so ValueFlow settles `m += 1` with no help from the compound strategy.
- The conformance harness now hands bun the emitted TYPESCRIPT (`.ts`): a declaration whose type
  differs from its initializer carries an annotation, which `.mjs` could not parse — and which is
  exactly what a user-defined conversion produces (`let m: Money = Money.fromInt(5)`).
- A compound assignment has a Target and a Value; only the Value FLOWS. Matching any child of
  `s += x` settled the target too (`r ?? '' += 't'`), which is how slice 1's first cut failed.
- A constant folds only where the JavaScript REPRESENTATION changes (`1` → `1n`); folding `0x10`
  to `16` rewrites the author for nothing.
- An int widening to float is exact — do not fround it (FloatStore rounds at the store).
- Two chars compared stay characters: JavaScript orders 1-length strings by the same code units.
- A user-defined implicit operator passes the value through: the framework's wrappers (`SizeValue`,
  `Index`, `ColorToken`) ARE their primitive on this side. Fencing the category (EQ2010, reverted)
  broke the site's ordinary API surface.
- `TimeSpan.FromSeconds(90)` binds .NET 9's `long` overload: the runtime twins must accept a
  bigint wherever .NET declares a long. Several only worked with literals, by accident.
