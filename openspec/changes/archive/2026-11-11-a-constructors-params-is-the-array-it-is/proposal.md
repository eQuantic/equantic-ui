## Why

Closes #1192, a Bug under #1174.

A method's `params` parameter is the array it is since #1159. A constructor's stayed a JavaScript rest parameter, so `new Bag(items)` spread the array into the call: past an engine's argument cap (about 638,000 in Bun, about 124,000 in V8) it threw `RangeError`, and the constructor got a copy where .NET hands it the caller's array. #1159 left it apart because a twin's one constructor dispatches its overloads on the count of arguments it receives.

Measured before the change: nothing at run time builds an app's twin by handing it elements loose. Hydration and a struct's zero build through `Object.create`, a hot reload calls a constructor with none, `$eq.exceptions.construct` passes on what the compiled site wrote, and the declarative factories are C# eqc compiles through the same site.

## What Changes

- The site decides from the bound tree, as a method's call does: the elements a call lists are packed into one array, none being an empty one, and an array handed whole goes as it is. For a `new`, a target-typed `new`, a `: this(…)` and a `: base(…)`, named arguments included; the strategy's other two paths, a construction that drops a component's dependencies and one no model binds, lay the arguments out the same way.
- The twin declares the parameter as the array it is, and its overloads count it as the one argument it always is. That tells apart constructors a rest parameter made collide: `Chained(params int[] xs)` beside `Chained(string s, int n)` was refused with EQ1009 and builds now.
- A type of the SDK's own keeps its rest parameter, which a hand-written twin may spell, as #1159 left a method's.

For a developer using the SDK: nothing they write changes; a constructor holds the array a caller hands it, of any length.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-bcl`: "A params parameter is the array it is" covers a constructor's.
- `transpiler-expressions`: "A params parameter takes what C# hands it" spreads a collection only into a rest parameter, which a constructor's is no longer.

## Impact

- eqc only: `BoundArguments`, `ObjectCreationStrategy`, `TwinConstructor`. No public surface and no developer surface moves.
