## MODIFIED Requirements

### Requirement: A named constant in an is test compares with its value

`x is C`, where `C` binds as a constant (a `const` field or an enum member), and a constant pattern
in any other spelling (`x is 1.5m`, `case Limits.Max:`) SHALL test `x` against the constant's value
through one rule: a null constant SHALL match any absence, a decimal and a NaN SHALL compare by value,
and any other constant SHALL compare as its literal, an enum member as the value the browser holds for
it and a long as a BigInt.

#### Scenario: A const

- **WHEN** browser-side code asks `x is Limits.Max` of `x = 3`, where `Limits.Max` is the const `10`
- **THEN** the answer is `false`, as in .NET

#### Scenario: A long, a decimal and a NaN

- **WHEN** browser-side code asks `x is Limits.Five` of `long x = 5`, `o is Limits.Rate` of an
  `object` holding `1.5m`, and `d is double.NaN` of `double d = double.NaN`
- **THEN** each answer is `true`, as in .NET

## ADDED Requirements

### Requirement: A long constant is a long

A `long` or `ulong` constant SHALL cross as the BigInt every long is in the browser, whatever its size.

#### Scenario: A constant in arithmetic

- **WHEN** browser-side code computes `t.Ticks / TimeSpan.TicksPerDay` for a two-day `TimeSpan`
- **THEN** it answers `2`, as in .NET

### Requirement: A char type test asks for one code unit

`o is char` SHALL be true only for a string of one UTF-16 code unit, the browser's value of a char.

#### Scenario: A string is no char

- **WHEN** a switch over an `object` holding `"hello"` has a `char` arm before a `string` arm
- **THEN** the `string` arm is taken, as in .NET
