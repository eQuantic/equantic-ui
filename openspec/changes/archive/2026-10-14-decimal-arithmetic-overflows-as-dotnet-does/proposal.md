# Proposal

Closes #764, a Bug under #565 (The transpiler's fences hold on every path, continued).

## Why

The runtime's `Decimal` added, subtracted and multiplied on an unbounded BigInt mantissa, so a
result no decimal holds was kept where .NET rounds it or throws. `decimal.MaxValue + decimal.MaxValue`
printed a 30-digit number in the browser and is an `OverflowException` in .NET, and every caller
followed: a `Sum` and an `Average` of two `decimal.MaxValue` answered too. A product past 28 places
kept them all (`0.1234567890123456789m * 0.1234567890123456789m` printed 38 places), and a sum of
operands whose scales differ kept digits past 96 bits.

Its division computed 28 places whatever the dividend, so a large one kept digits .NET rounds away
(`15518717730321859317m / 0.00000003512407616471289109m` printed 28 places after a 27-digit integer
part), and it always dropped the trailing zeros, so an exact quotient lost the scale .NET keeps:
`4.00m / 2m` printed `2`, where .NET prints `2.00`, and so did an `Average` of `2.00m` and `4.00m`.

Measured with half a million random operations on operands of every width .NET's `DecCalc` takes a
path for (32, 64 and 96 bits, every scale), compared with .NET 10's own answers: the runtime answered
44% of them differently, the remainder's scale included (`1.5m % 2.00m` printed `1.50`, where .NET
keeps the dividend as it is, `1.5`).

## What Changes

For a developer, a `decimal` computes in the browser what it computes on the server:

- `+`, `-` and `*` are exact while the result fits 96 bits and 28 places. Past that, the result
  loses the fewest places that bring it back, rounded half to even, and where the scale runs out
  first it throws `OverflowException` with .NET's message. A rounding that carries past 96 bits
  loses one place more, as .NET's does.
- `/` keeps an exact quotient at the scale its operands give, brought up to zero when it is under,
  and an inexact one at as many places as fit, nine at a time, rounded half to even, its trailing
  zeros gone; so a quotient holds the 28 or 29 digits a decimal holds.
- `%` keeps a dividend that is zero, or smaller than the divisor, as it is, its scale included.
- A zero product keeps the scale .NET gives it: its operands' sum on .NET's 32-bit path
  (`0.00m * 1.5m` is `0.000`), 0 on its wide one and past 47 places.

`Sum`, `Average`, compound assignments and increments follow without a change of their own: they
compute through the same `Decimal`.

The change is the runtime's (`utils/decimal.ts`) alone. eqc, the realizers, the shells and the SDKs
are untouched, and neither the public surface nor the developer surface moves. Nothing breaks: an
app that met the old answers met wrong ones.

## Capabilities

### New Capabilities

- `runtime-decimal`: the runtime's `Decimal` answers each arithmetic operation as .NET's
  `decimal` does, its rounding, its overflow and its scale.

### Modified Capabilities

None.

## Impact

- `src/eQuantic.UI.Runtime/src/utils/decimal.ts`: `add`, `sub`, `mul`, `div` and `mod` follow
  .NET's `DecCalc` (`ScaleResult`, `SearchScale`, `OverflowUnscale`, `VarDecMod`).
- `tests/eQuantic.UI.Conformance.Tests/DecimalArithmeticConformanceTests.cs`: 32 cases executed on
  both sides; 24 fail without the change.
- `src/eQuantic.UI.Runtime/src/utils/decimal.spec.ts`: the same answers, pinned in the runtime's
  own suite.
