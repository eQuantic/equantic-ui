## Why

Closes #904, #905, #910, #956 and #957, sub-issues of Feature #849.

- `1.Equals((object)1.0)` and `'a'.Equals((object)"a")` answered True, since the browser holds an int and a double as one kind of number and a char and a text of one as one kind of string. A byte's, a short's, a float's, a double's and a decimal's `Equals`, typed or not, failed the build with EQ2004.
- A `KeyValuePair` is an array here, so every route into text wrote JavaScript's `1,a` where .NET writes `[1, a]`.
- `Task.FromException` and `Task.FromCanceled` failed the build.
- `Tuple.Create` and `new Tuple<…>` are refused with EQ2004 on main, and no test said so.

## What Changes

- A new `PrimitiveEqualsStrategy` answers `Equals` on every primitive. The other value's type is read from the bound tree before C# boxed it: one of another type answers false with both values evaluated. A double and a float compare with NaN equal to NaN, and a decimal by its own `equals`.
- A value already held as `object` has lost its type and compares by what it holds, so an int and a double of one value, or a char and a text of one, are found equal there. That is the limit of a value held as `object` (#552), named in the strategy.
- `StringConversion` writes a `KeyValuePair`'s text through `$eq.text.pair`, each half as its type writes it, through a hole, a concatenation, `ToString`, `string.Join` and a method group. The formatter reads a pair held as `object` by its shape, the one array here that answers `key` and `value`.
- `Task.FromException` and `Task.FromCanceled`, generic or not, go to `$eq.task.fromException` and `$eq.task.fromCanceled`, which refuse a null exception and a token not cancelled in .NET's words. A task of either kind nobody awaits does nothing, as in .NET: its rejection is taken as read.
- A reference tuple stays refused with EQ2004, now pinned by a test.

## Impact

- Reaches the compiler (`Strategies/Primitives/PrimitiveEqualsStrategy.cs`, `BclSurfaceTailStrategy`, `StructuralEqualsStrategy`, `StringConversion`, `TaskMethodStrategy`, `Eq`) and the runtime (`utils/format.ts`, `utils/cancellation.ts`, `eq.ts`).
- `PublicAPI.Unshipped.txt` adds the strategy and four `Eq` constants, all on the compiler's tool surface. `developer-surface.baseline.txt` does not move.
