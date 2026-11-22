# Proposal

Closes #1808, a Bug under #1587 ("The transpiler's fences hold on every path, part five").

## Why

Some .NET members check overflow whatever the context they are called in, and the browser kept counting where
they throw `OverflowException`. Measured on .NET 10:

- `Enumerable.Sum` over `int` and `long`, with a selector or without and in their nullable forms, adds in a
  checked context, inside `unchecked` too. `new[] { int.MaxValue, 1 }.Sum()` throws, and so does
  `{ int.MaxValue, 1, -1 }`: the addition that leaves the range throws, whatever comes after it. The browser
  answered 2147483648, and a BigInt past 64 bits for a long.
- `Math.Abs` of a signed integer's `MinValue` throws ("Negating the minimum value of a twos complement number
  is invalid."), through `Math` and through the type alike (`int.Abs`, `long.Abs`), for `sbyte`, `short`,
  `int`, `long` and `nint`. The browser answered the magnitude, a value outside the type.
- `CopySign` of `MinValue` throws for every sign that is not negative, and answers `MinValue` for one that is.

Writing the conformance cases found two more. A nullable sum with a null in it threw a TypeError in the browser
for a `long?` and a `decimal?`, because the null was added to a BigInt and handed to `Decimal.add`. And
`int.CopySign(0, -1)` answered `-0`, a zero no integer has, which `1.0 / x` turns into negative infinity.

## What Changes

- An `int`'s and a `long`'s sum add through the runtime's checked addition (`$eq.num.addInt`,
  `$eq.num.addLong`), and a nullable's null is passed over as its type's zero. The type is the one the bound
  overload adds, as `Average` reads it, and no longer the call's answer, which said nothing about a nullable.
  A real's sum is written as it was.
- A signed integer's `Abs` and `CopySign` go through the runtime (`$eq.num.abs`, `absLong`, `copySign`,
  `copySignLong`), which throws for `MinValue` with .NET's message. A real's stay `Math.abs`.
- `Average`'s path with no model checks a long's total too, through the same addition.

eqc's strategies and the runtime are the parts reached. The developer surface does not move. The compiler's
public surface gains the names of the two additions (`Eq.AddInt`, `Eq.AddLong`), a tool surface.

## Capabilities

### Modified Capabilities

- `transpiler-sequences`: an int's and a long's sum is checked, and a nullable's passes a null over.
- `transpiler-bcl`: a signed integer's `Abs` and `CopySign` refuse `MinValue`'s magnitude.

## Principles

A translation executes identically on both sides, which the conformance suite holds by running each case on
both. The structural fix is preferred to the patch: the type comes from the overload, which says it, rather
than from what the call answers.
