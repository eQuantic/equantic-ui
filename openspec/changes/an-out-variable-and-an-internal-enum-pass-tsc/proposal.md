## Why

Closes #1858 and #1859, Bugs under #1587, both found by #1563 (a gesture of the app's own is recognized), which they
block: they hold four of its runtime `tsc` errors. #1853 holds the rest.

**#1858.** eqc declared an `out var` as `any`. So a list read through it was untyped, `listWalk<T>(readonly T[])`
handed an `any` inferred `unknown`, and a walk's element failed `tsc` at its first use. Three errors in the arena's and
the router's twins (TS18046, TS2345):

```csharp
if (!_arenas.TryGetValue(pointer, out var arena)) return;
foreach (var member in arena.Members) member.RejectGesture(pointer);
```

**#1859.** `EnumUnionsTsGenerator` wrote a union only for a public enum. A twin imports the union of whatever enum its
C# names, so the internal `GestureArenaPhase` was imported as a `$GestureArenaPhaseValue` nothing declared (TS2305).

## What Changes

For a developer using the SDK nothing they write changes, and no module's JavaScript changes. The library's twins and an
app's modules are TypeScript the runtime's `tsc` takes in these shapes.

- **An `out var` of a named type is declared by that type** in TypeScript: `let arena!: GestureArenaPointer;`, definitely
  assigned since the call that declares it assigns it inside an arrow. A primitive's or a generic's stays `any`, as
  before.
- **A miss writes `null!` in TypeScript.** `OutArgument.Missed`, the one writer of an inline lowering's miss, writes
  .NET's null there, for `TryGetValue` and the queue's and the set's takes, since `[MaybeNullWhen(false)]` leaves a null
  in a variable whose type does not admit one.
- **A `TryParse` lowered over a reader asserts the read it lands** in the variable before testing it.
- **Every top-level non-flags enum of the vocabulary's assemblies has its union**, an internal one included
  (`EnumUnionsTsGenerator.UnionsIn`). A nested enum stays its owner's.

Parts reached: eqc (`ExpressionVariableScanner`, `OutArgument`, `DictionaryLookup`, `QueueStackStrategy`, the statement
writers) and the build's generator of enum unions. The scanner's new overloads are internal. No contract surface or
developer surface moves. Thirteen of the runtime's library modules declare their out vars by type, and their
JavaScript is unchanged.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-expression-variables`: an `out var` of a named type is declared by that type in TypeScript.
- `transpiler-classes`: every enum a twin can name has its union, an internal one included.

## Impact

- `SharedComponentTranspilationTests` gains `EmittedArena` among its emission shapes, which the runtime's `tsc` refuses on
  main with TS18046.
- `EnumUnionsTsGeneratorTests` gains an internal enum and a nested one; the test fails with the public-only filter.
- No assembly on main holds an internal enum, so `enums.generated.ts` is unchanged.
