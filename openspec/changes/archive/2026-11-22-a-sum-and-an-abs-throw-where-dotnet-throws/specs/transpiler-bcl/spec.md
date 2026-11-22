## ADDED Requirements

### Requirement: A signed integer's magnitude refuses its MinValue

`Abs` and `CopySign` of a signed integer, `sbyte`, `short`, `int`, `long` or `nint`, through `Math` or through the
type, SHALL answer in the browser what they answer in .NET: the magnitude, and for the type's `MinValue`, whose
magnitude is no value of the type, the `OverflowException` .NET throws, in an unchecked context too.
`CopySign` of `MinValue` SHALL answer `MinValue` for a negative sign, and SHALL answer 0 for a zero, never `-0`.
A real's `Abs` SHALL stay `Math.abs`, and a real's `CopySign` its inline lowering, a signed
zero's sign included, with nothing checked.

#### Scenario: The magnitude of MinValue

- **WHEN** browser-side code computes `Math.Abs(n)` with `n` an `int` holding `int.MinValue`, or `long.Abs(l)` with `l` holding `long.MinValue`
- **THEN** it throws `OverflowException` with "Negating the minimum value of a twos complement number is invalid.", as .NET does

#### Scenario: A sign copied onto MinValue

- **WHEN** browser-side code computes `int.CopySign(n, 1)` and `int.CopySign(n, -1)` with `n` holding `int.MinValue`
- **THEN** the first throws `OverflowException` and the second is -2147483648, as in .NET

#### Scenario: An integer's one zero

- **WHEN** browser-side code computes `1.0 / int.CopySign(n, -1)` with `n` holding 0
- **THEN** it is positive infinity, as in .NET, where the browser's `-0` made it negative
