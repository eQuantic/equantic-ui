## ADDED Requirements

### Requirement: A decimal is cut into an integer from its own digits

An explicit conversion of a `decimal` to an integer type or a `char`, and `decimal.ToInt32` and its
siblings, SHALL cut the value toward zero from its exact digits and refuse a result outside the type
with .NET's `OverflowException` and its words for that type, whether or not the context is checked.

#### Scenario: A long past what a double holds

- **WHEN** browser-side code converts `9007199254740993m` to a `long`
- **THEN** it is `9007199254740993`, as in .NET, where the browser answered `9007199254740992`

#### Scenario: A value just under a whole number

- **WHEN** browser-side code converts `0.99999999999999999m` and `2147483647.9999999999999999m` to an `int`
- **THEN** they are `0` and `2147483647`, as in .NET, where the browser answered 1 and refused the second

#### Scenario: A value outside the type

- **WHEN** browser-side code converts `256m` to a `byte` inside `unchecked`
- **THEN** it throws `OverflowException` "Value was either too large or too small for an unsigned byte.", as in .NET

### Requirement: Math over a decimal answers as .NET's decimal does

`Math.Abs`, `Sign`, `Max`, `Min` and `Clamp` over a `decimal`, and `decimal`'s statics of the same names,
SHALL answer on the runtime's `Decimal` what .NET answers, the scale .NET keeps included, and a `Math`
member over a decimal the numeric table does not answer SHALL fail the build.

#### Scenario: A negative price

- **WHEN** browser-side code computes `Math.Abs(-12.50m)`, `Math.Max(-12.50m, 0m)` and `Math.Sign(-12.50m)`
- **THEN** they are `12.50`, `0` and `-1`, as in .NET, where the browser wrote NaN

#### Scenario: Two equal values of different scales

- **WHEN** browser-side code computes `Math.Max(1.0m, 1.00m)` and `Math.Min(1.0m, 1.00m)`
- **THEN** they are `1.0` and `1.00`, as in .NET

#### Scenario: A minimum above the maximum

- **WHEN** browser-side code computes `Math.Clamp(2m, 3.5m, 1.25m)`
- **THEN** it throws `ArgumentException` "'3.5' cannot be greater than 1.25.", as in .NET
