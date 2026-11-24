## Why

#1824, a Bug under #1587, found while fixing #1767. `Math.Clamp` over a number other than a decimal answered the maximum where .NET refuses a minimum above it: `Math.Clamp(2.0, 3.0, 1.0)` throws "'3' cannot be greater than 1." in .NET and was 1 in the browser. The numeric table wrote `Clamp` as `Math.min(Math.max(value, min), max)` for the reals and the small widths and as a ternary for a long, and neither looks at the bounds. The same composition parts from .NET twice more: a NaN bound turns the answer into NaN (`Math.Clamp(2.0, double.NaN, 1.0)` is 1 in .NET), and a negative zero loses its sign (`Math.Clamp(-0.0, 0.0, 1.0)` is -0).

## What Changes

- `Math.Clamp` over every number the browser holds as a JavaScript number or a BigInt, and each number type's own `Clamp` and `ClampNative` (which .NET 10 refuses the same way), go to the runtime's `clamp` and `clampLong`. A minimum above the maximum is refused with .NET's `ArgumentException`, its two values written in the current culture as their type writes them (`'3,5' cannot be greater than 1,25.` in pt-BR, a float's `0.1`). Then the minimum where the value is below it, the maximum where it is above, and the value otherwise, .NET's order.
- The shared components that clamp (the code editor, the sheets, the tables, the slider, the progress bar, the pagination) are regenerated. None of them reaches a minimum above its maximum in the runtime's tests, where .NET would already refuse it on the server and on Photon.

A developer writes nothing. Neither the public surface nor the developer surface moves. The parts reached are eqc's numeric table and the runtime.

Principles followed: .NET is the oracle, every row measured on .NET 10 and compared on both sides by the conformance suite.

## Capabilities

### New Capabilities

### Modified Capabilities

- `transpiler-bcl`: a number's `Clamp` refuses a minimum above its maximum and compares in .NET's order.

## Impact

- `src/eQuantic.UI.Compiler/CodeGen/Strategies/Primitives/PrimitiveStaticStrategy.cs`, `FormatKind.cs`.
- `src/eQuantic.UI.Runtime/src/utils/clamp.ts` (new), `eq.ts`.
- The regenerated shared components, and the `bcl-surface` baseline.
- `tests/eQuantic.UI.Conformance.Tests/NumberClampConformanceTests.cs` (new).
