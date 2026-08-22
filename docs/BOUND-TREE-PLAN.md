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
| 1 | `ValueFlow` — the dispatcher settles every expression for the implicit conversion wrapping it in the bound tree; owns char promotion and int→long (the syntax rules were removed); yields on text (`StringConversion`) and decimal | done |
| 2 | Text: a value on its way into a concatenation (boxed, or a string operand — of `+` or of `+=`'s VALUE, never its target) or a plain interpolation hole is settled by ValueFlow; the concatenation and interpolation strategies dropped their calls. `s += flag` prints "True" for the first time | done |
| 3 | `IBinaryOperation` read where the syntax guessed: `IsLifted` decides the nullable lift (the operand-type guess stays for model-less worlds); the long branch no longer re-wraps an operand ValueFlow made a BigInt | done |
| 4 | User-defined operators, for IN-SOURCE types whose twin the emitter writes: conversion operators are emitted (`Money.fromInt`, `Money.toInt`) and called from ValueFlow (implicit) and the cast strategy (explicit); unary operators are named by arity (`opNegate`, no collision with `opSubtract`) and called through `IUnaryOperation.OperatorMethod`; compound assignments through `ICompoundAssignmentOperation.OperatorMethod`. Framework wrappers keep passing the primitive through. 10 cases both sides | done |
| 5 | Statements, instrumented first: 7 foreach cases (element conversions, deconstruction, a string's chars) found 2 gaps, both the ELEMENT conversion the syntax never shows — `foreach (long l in ints)`, `foreach (int code in chars)`. `SemanticHelper.ForEachInfo` reads `ForEachStatementInfo.ElementConversion` and `ValueFlow.Apply` (the conversion table, now a function) converts each element; a user-defined element conversion (`foreach (Money m in ints)`) works through the same call. `using` instrumented next and NOT right: the statement form disposed, the DECLARATION form (`using var r = …;`) was a bare const with a comment — never disposed, for as long as it existed. The block now owns it: what follows runs in a try whose finally disposes, reverse order for several, `disposeAsync` for `await using` (6 cases). Patterns deferred: the syntax PatternConverter is conformance-covered and the bound tree adds nothing observable yet | done |
| 7 | Typed hydration: a decimal/long that arrives from state is coerced at every use (defensive wraps); give state a typed boundary so the wraps can go | |
| 6 | Explicit conversions: `CastExpressionStrategy`'s tables and `IntegerWidth.Wrap` become one conversion table, used by ValueFlow and by casts | |

## Lessons recorded on the way

- `GetOperation(node)` returns the OPERAND; the implicit conversion is its `Parent`. Hook there.
- `decimal` and `long` have NO invariant runtime representation: a value hydrated from state
  crosses JSON as a number or a string, so the binary strategies wrap every operand DEFENSIVELY
  (`$eq.num.dec(x)`, `$eq.num.long(x)` are coercions, not conversions). ValueFlow converts at
  the implicit-conversion seams only; the wraps stay until hydration is typed — its own track.
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
