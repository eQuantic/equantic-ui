## Why

Closes #1202, a sub-issue of #1127 (E3a: the element engine is transpiled into the runtime).

Measuring E3a found two things eqc refuses that the element engine uses, and both are general gaps of the transpiler rather than the engine's:

- `current.GetType() == next.GetType()`, Flutter's `canUpdate`, fails the build with EQ2004, since `GetType` is fenced as a platform member: the browser cannot answer every `GetType()`, so it answers none.
- `ArgumentOutOfRangeException.ThrowIfNegative(part)` fails with EQ2004, and so does every guard of the family .NET 8 added for argument checks (`ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrEmpty` and `ThrowIfNullOrWhiteSpace`, and the nine range guards of `ArgumentOutOfRangeException`). These are the idiomatic argument checks of modern C#, so any write-once code that checks its arguments the way the analyzers recommend (CA1510, CA1512, CA1513) could not reach the browser.

## What Changes

- **A runtime type compares as .NET compares it, where the browser can tell types apart.** `a.GetType() == b.GetType()`, `a.GetType() != b.GetType()` and either one against `typeof(X)` translate to a comparison of the values' runtime types: the class each value was built as, its closed type arguments where the build marked them, and an exception's .NET type. A receiver of a type whose values the browser cannot tell apart is refused with EQ2004 naming why: a number, a string, a bool, an enum, an array, a tuple, a delegate, `object`, an interface, and a type parameter with no class constraint that qualifies. Any other use of `GetType()` stays refused, as it is today.
- **A generic class built as a closed type is marked with its type arguments**, as a generic record or struct already is (#651), so `new Box<int>()` and `new Box<string>()` are told apart. A value built where its type arguments are known only at run time (inside generic code) carries no mark and is not taken for another type, the rule records already follow.
- **Every argument guard throws as .NET throws it.** Each of the twelve guards becomes a call of the runtime's `$eq.guard`, which throws the exception .NET throws, with the parameter's name (the caller's argument text that `CallerArgumentExpression` gives, read from the bound tree), .NET's message in the current culture, and the actual value; and passes where .NET passes: a NaN counts as negative, a negative zero as zero and as negative, `ThrowIfEqual` finds two NaNs equal, a string orders in the current culture, and a null `value` of an ordering guard throws a `NullReferenceException`. A guard over a type the browser cannot hold faithfully (`Int128`, `Half`, `BigInteger`, a pointer) or order faithfully (a `Guid`, a type whose `CompareTo` is an explicit interface member) is refused with EQ2004.
- An exception's actual value is written as the guard formats it, in the current culture: the runtime's exception parts gain the text of the actual value beside the value.

`ObjectDisposedException.ThrowIf` is not part of this change: its message names the full type name of the instance it is handed, which the browser does not hold for every class. It stays refused, as it is today, and has an issue of its own, #1203.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-exceptions`: the argument guards throw as .NET throws them.
- `transpiler-classes`: a runtime type compares as .NET compares it, and a generic class built as a closed type keeps its type arguments.

## Impact

- Reaches `eQuantic.UI.Compiler` (a strategy for the guards, a strategy for the comparison of runtime types, `ObjectCreationStrategy`'s closure mark, `PatternConverter`'s rule for a type with a prototype of its own, `Eq`) and the runtime (`utils/guards.ts`, `utils/runtime-types.ts`, `utils/closures.ts`, which the closure marks move to from `eq.ts` so both read them, `utils/exceptions.ts`, `eq.ts`).
- What a developer writes does not change: C# that was refused with EQ2004 now builds and answers as it does on .NET.
- `eQuantic.UI.Compiler`'s `PublicAPI.Unshipped.txt` gains the four names of `Eq` the strategies write (`Guard`, `TypesOf`, `TypesNamed`, `TypesSame`), a tool surface that is never promised; no contract assembly's surface moves, and neither does `developer-surface.baseline.txt`.
