## ADDED Requirements

### Requirement: A number's Clamp refuses a minimum above its maximum, and compares in .NET's order

`Math.Clamp` over a number other than a decimal, and each number type's own `Clamp` and `ClampNative`, SHALL
refuse a minimum above the maximum with .NET's `ArgumentException`, its two values written in the current
culture as their type writes them, and SHALL otherwise answer the minimum where the value is below it, the
maximum where it is above, and the value itself, in that order, as .NET does.

#### Scenario: A minimum above the maximum

- **WHEN** browser-side code evaluates `Math.Clamp(2.0, 3.5, 1.25)` in pt-BR
- **THEN** it throws `ArgumentException` "'3,5' cannot be greater than 1,25.", as .NET does, where the
  browser answered 1.25

#### Scenario: A NaN bound and a negative zero

- **WHEN** browser-side code evaluates `Math.Clamp(2.0, double.NaN, 1.0)` and `Math.Clamp(-0.0, 0.0, 1.0)`
- **THEN** they answer 1 and -0, as .NET does, where the browser answered NaN and 0
