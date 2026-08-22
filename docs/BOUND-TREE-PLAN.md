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
| 2 | Text: string concatenation and interpolation operands settled by the bound tree (`IBinaryOperation` string Add, `IInterpolationOperation`); `StringConversion` becomes ValueFlow's | next |
| 3 | `IBinaryOperation` as an operation strategy: `IsLifted` replaces the nullable-lifting guess, `OperatorMethod` lowers user-defined operators (today: passed through), enum and width rules read the operation | |
| 4 | Statements: `IForEachLoopOperation` (element conversion, deconstruction), `IUsingOperation`, pattern operations | |
| 5 | Explicit conversions: `CastExpressionStrategy`'s tables and `IntegerWidth.Wrap` become one conversion table, used by ValueFlow and by casts | |

## Lessons recorded on the way

- `GetOperation(node)` returns the OPERAND; the implicit conversion is its `Parent`. Hook there.
- A constant folds only where the JavaScript REPRESENTATION changes (`1` → `1n`); folding `0x10`
  to `16` rewrites the author for nothing.
- An int widening to float is exact — do not fround it (FloatStore rounds at the store).
- Two chars compared stay characters: JavaScript orders 1-length strings by the same code units.
- A user-defined implicit operator passes the value through: the framework's wrappers (`SizeValue`,
  `Index`, `ColorToken`) ARE their primitive on this side. Fencing the category (EQ2010, reverted)
  broke the site's ordinary API surface.
- `TimeSpan.FromSeconds(90)` binds .NET 9's `long` overload: the runtime twins must accept a
  bigint wherever .NET declares a long. Several only worked with literals, by accident.
