## Why

Closes #649, #911 and #970, sub-issues of Feature #849.

Three implicit conversions lost part of what C# does:

- A native integer had no representation of its own: from an `int` it was a number, from a `long` or a constant past `int` a BigInt, and JavaScript refuses to mix the two (`nint c = a + b` threw a TypeError).
- A tuple converted to a tuple of other element types passed its array through, so a `(long, long)` made from an `(int, int)` held numbers, and `t.Item1 + 1L` threw.
- A user-defined conversion and the standard conversion beside it are two conversions on one node, and only the one right around the node was applied: `long l = new Count(5)` ran the operator and not the widening, and `Ticks t = 5` the widening and not the operator.

## What Changes

- `nint` and `nuint` are held as `long` and `ulong` are, a BigInt, on every target this SDK ships to, where a native integer is 64 bits wide: `IntegerWidth`, the literals, the defaults, the ordering, the hydration spec and the emitted TypeScript type treat them as the 64-bit integers they are, and their `MinValue`, `MaxValue` and `Math` members answer at that width.
- `ValueFlow.Settle` applies every implicit conversion around a node, inner first, and a user-defined conversion asks the pair of types for the standard conversion on each side of its operator, which a tuple's element has no bound tree to show. An explicit cast of that shape has no operation of its own and takes what the operand's conversions settled.
- A tuple conversion converts each element as C# converts it (`TupleElements`), the source read once, a nested tuple and a user-defined element included; a checked context reaches the elements.

## Impact

- Reaches `eQuantic.UI.Compiler` (`ValueFlow`, `IntegerWidth`, `CastExpressionStrategy`, `ConstantLiteral`, `DefaultValue`, `HydrationSpec`, `TypeScriptEmitter` and the numeric strategies). No runtime file changes.
- Neither `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` nor `developer-surface.baseline.txt` moves.
