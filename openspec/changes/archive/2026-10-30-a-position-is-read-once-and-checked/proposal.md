## Why

Closes #394 and #1160, both sub-issues of Feature #849.

A position in a sequence was written as text that read its parts as often as the text named them and checked nothing:

- `a[^n]` over an array, a list or a string was `a[a.length - n]` as text, so its receiver was evaluated twice (`Get()[^1]`, and every write through it, called `Get` twice), a deconstruction's target and a null-conditional write were refused (EQ1004), and the standalone `^n` value of the same issue is already refused and stays so. The issue's other worry, an offset spliced bare, did not hold when measured: `a[^(i + 1)]` answers right on main, since the parentheses its author wrote survive the text.
- `List<T>.Insert`, `RemoveAt`, `RemoveRange` and `GetRange` were a `splice` or a `slice` with each argument placed by its position in the call: `l.Insert(item: 9, index: 0)` inserted 0 at 9, `l.RemoveRange(count: 1, index: 0)` removed nothing, and `GetRange(i++, 2)` read `i` twice. A position .NET refuses was clamped to the array or counted from its end: `Insert(5, 9)` on a list of one appended, `RemoveAt(5)` removed nothing, `RemoveAt(-1)` removed the last element, and `GetRange(0, 5)` answered a list of one.

## What Changes

- An array's, a list's or a string's element counted from the end is a `Place` (`Place.Subscripted`), read and written through JavaScript's subscript with its position computed from the receiver: the receiver is a part evaluated once, then the offset, then the length, as C# reads them, and the writer parenthesizes the offset. Every writer that takes its place from `Place` (the assignment, the null-conditional assignment, the compound, the step, `??=`, a bool's compound, the deconstruction) gets it. `IndexFromEndStrategy` is on the IR and reads through the place. An `out` or a `ref` target holds its receiver and position before the call and is written through them. A `ref` target's read is still its own evaluation, so its receiver runs twice: #1190.
- A template part that awaits is bound as an argument of the arrow the writer makes, in `JsExprWriter`, since that arrow is no async function. Every template gets it, a twin's indexer from the end included.
- `Insert`, `RemoveAt`, `RemoveRange` and `GetRange` go through `ParameterTemplate`, each argument in its parameter's place and evaluated in the order it is written, to the runtime's `listInsert`, `listRemoveAt`, `listRemoveRange` and `listGetRange` (`utils/list.ts`), which check the position as .NET does and throw its exception in its words. A list's face (`IList<T>`) over a twin calls the twin's own `insert` and `removeAt`. With no model, a named argument is refused (EQ1004), as it is for `AddRange`.
- A fixed-size array behind `IList<T>` still inserts and removes where .NET refuses the call: that is #711, a change of the array's representation.

## Impact

- Reaches `eQuantic.UI.Compiler` (`Place`, `IndexFromEndStrategy`, `ListMethodStrategy`, `InvocationStrategy`, `OutArgument`, `JsExprWriter`, `Eq`) and the runtime (`utils/list.ts`, `eq.ts`).
- Neither `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` nor `developer-surface.baseline.txt` moves.
