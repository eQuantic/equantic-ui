## Why

Closes #1183 and #1190, Bugs under #1174.

- A component's property that keeps its store through C# 14's `field` had the store declared on the twin and never started, so until a configuration or a hydration wrote it, it read `undefined` where C# reads its type's zero: `"" + Level` rendered the word `undefined` on a client mount (#1183). A plain class's twin already starts the same store in its constructor.
- A `ref` argument counted from the end held its receiver and position for the write-back after the call, and the call read the element again in its own place, so `Twice(ref Get()[^1])` ran `Get` twice where .NET runs it once (#1190). Measured while fixing it: an array's element counted from the start was read and written back the same way, so `Twice(ref Get()[0])` ran `Get` twice too, and `Twice(ref a[i++])` stepped `i` twice and wrote the next element. And a second argument counted from the end after a first was refused (EQ1004), since the first counted as code run ahead of it, though it is taken before the call in its own order.

## What Changes

- **A component's guarded store starts as C# starts it.** The twin's constructor writes each store a property keeps with `field`: the property's initializer, written to the store itself as C# writes the backing field (`{ set => field = value + 1; } = 5` holds 5), or its type's zero, a reference's null included.
- **A `ref` argument's element is taken once.** Its receiver and position are held in a temporary the enclosing function declares, before the call, and both the argument and the write-back read through it. A temporary rather than a function around the call, so an `await` among the arguments stays the enclosing function's own. Where nothing is open to declare one (an initializer), the call keeps its former shape.
- **An array's element counted from the start is held the same way** where its receiver or its position runs code, for a `ref` and an `out` alike, and only where holding it before the call keeps C#'s order.
- **An argument held before the call does not count as code run ahead of the next one**, so `Swap(ref Get()[^1], ref Get()[^2])` builds and runs each `Get` once, in order.

For a developer using the SDK: nothing they write changes. The browser answers what .NET answers.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-classes`: a new requirement, a component's guarded store starts as C# starts it.
- `transpiler-expressions`: a new requirement, a `ref` or `out` argument's element is taken once.

## Impact

- eqc only: `TypeScriptEmitter` (a component's constructor) and `InvocationStrategy` (the by-reference arguments).
- No public surface and no developer surface moves.
