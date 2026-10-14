## ADDED Requirements

### Requirement: A decimal result past what a decimal holds rounds or overflows as .NET's does

A decimal sum, difference or product SHALL be exact while it fits a 96-bit mantissa and 28 places.
Past that, it SHALL lose the fewest places that bring it back, rounded half to even, and SHALL throw
`OverflowException` with .NET's message, "Value was either too large or too small for a Decimal.",
where the scale runs out first. A rounding that carries the mantissa past 96 bits SHALL lose one
place more, as .NET's does. Every caller SHALL follow, a `Sum`, an `Average` and a compound
assignment included.

#### Scenario: A sum no decimal holds

- **WHEN** browser-side code computes `decimal.MaxValue + decimal.MaxValue`
- **THEN** it throws `OverflowException` with .NET's message, as .NET does; it printed
  158456325028528675187087900670

#### Scenario: A sum of two decimals no decimal holds

- **WHEN** browser-side code computes `new[] { decimal.MaxValue, decimal.MaxValue }.Sum()`
- **THEN** it throws `OverflowException`, as .NET does

#### Scenario: A difference past 96 bits

- **WHEN** browser-side code computes `decimal.MaxValue - 0.1m`
- **THEN** the answer is 79228162514264337593543950335, as .NET's is

#### Scenario: A product past 28 places

- **WHEN** browser-side code computes `0.0000000000000000000000000015m * 0.1m`
- **THEN** the answer is 0.0000000000000000000000000002, the half rounded to even, as .NET's is

### Requirement: A decimal quotient keeps the places .NET keeps

A decimal quotient SHALL start at the dividend's scale less the divisor's. An exact quotient SHALL
keep that scale, brought up to zero when it is under, or throw `OverflowException` where it cannot
be. A quotient with a remainder SHALL take as many places as keep its mantissa within 96 bits and
its scale within 28, round the last half to even, and lose its trailing zeros.

#### Scenario: A large dividend

- **WHEN** browser-side code computes `15518717730321859317m / 0.00000003512407616471289109m`
- **THEN** the answer is 441825648525173427785103587.23, as .NET's is

#### Scenario: An exact quotient keeps its scale

- **WHEN** browser-side code computes `4.00m / 2m`
- **THEN** the answer is 2.00, as .NET's is; it was 2

#### Scenario: An inexact quotient loses its trailing zeros

- **WHEN** browser-side code computes `1m / 4m`
- **THEN** the answer is 0.25, as .NET's is

### Requirement: A decimal remainder and a zero product keep .NET's scale

A decimal remainder SHALL be the dividend as it is, its scale included, where the dividend is zero
or smaller than the divisor, and otherwise exact at the larger of the two scales. A product that is
zero SHALL keep the sum of its operands' scales where both mantissas fit 32 bits and that sum is 28
or less, SHALL be zero at a scale of 28 where the sum is between 29 and 47, and SHALL be a plain
zero otherwise, as .NET's two paths answer.

#### Scenario: A dividend smaller than its divisor

- **WHEN** browser-side code computes `1.5m % 2.00m`
- **THEN** the answer is 1.5, as .NET's is; it was 1.50

#### Scenario: A zero product on each path

- **WHEN** browser-side code computes `0.00m * 1.5m` and `0.00m * 12345678901.5m`
- **THEN** the answers are 0.000 and 0, as .NET's are
