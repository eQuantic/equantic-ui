## Why

Closes #1273 and #1238, Bugs under #1174.

- `Queue<T>`'s and `Stack<T>`'s capacity, `LinkedList<T>.CopyTo`'s index and `HashSet<T>.CopyTo`'s index and count are refused in .NET by `ArgumentOutOfRangeException.ThrowIfNegative`, which carries `ActualValue`. The runtime wrote those words by hand in two private copies, so `ActualValue` reached C# as null while the message stated it, and the value was not written as the culture writes an `int` (#1273). Measured on .NET 10 first: `List<T>`'s, `HashSet<T>`'s and `Dictionary<,>`'s own capacity checks go through `ThrowHelper` and carry no `ActualValue`, and the runtime already says so; the eight cases this change adds are the ones that go through the guard.
- A value of a non-flags enum that no member names, `(Status)7`, crossed from the server as its digits in a string, `"7"`, where the browser holds the number 7, or the BigInt 7n for an enum over a long. A field the server filled with it compared `"7"` with `7` and answered false, and a dictionary keyed by one missed its key (#1238).

## What Changes

- The collections' negative-count refusals take their words, their `ParamName` and their `ActualValue` from the guards' one writer, `throwIfNegativeCount`, the value written as the current culture writes an `int` (sv-SE's minus sign included). The two private copies go.
- The hydration spec marks a non-flags enum, `'enum'` or `'enumLong'` for one over a long, and the runtime revives what arrived: a member's camelCase name stays as it is, and a value's digits become the number or the BigInt the browser holds, as a value, as an element and as a dictionary's key. A member's name is a C# identifier, which never starts with a digit or a sign. EqJson writes as it did.

For a developer using the SDK: nothing they write changes.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `runtime-collections`: a new requirement, a collection's negative count is refused with its value.
- `hydration-contract`: a new requirement, a value no enum member names crosses as the browser holds it.

## Impact

- The runtime (`utils/guards.ts`, `utils/collections.ts`, `utils/hash-set.ts`, `utils/hydrate.ts`) and eqc (`HydrationSpec`): every twin with a non-flags enum member now carries its tag in `$hydration`, which regenerates 21 of the runtime's shared modules.
- No public surface and no developer surface moves.
