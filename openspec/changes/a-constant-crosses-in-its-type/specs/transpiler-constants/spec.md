# Spec Delta

## Purpose

How a compile-time constant reaches JavaScript: as its value in its C# type, wherever it is written.

## ADDED Requirements

### Requirement: A constant crosses as its value in its C# type

A reference to a `const` SHALL be written as the constant's value in its C# type: a `decimal` as the runtime's exact Decimal with its scale, a `long` or a `ulong` as a BigInt whatever its size, a `float` as the double it is, and a `char` and a `string` as quoted text. This SHALL hold for a constant reached through its type, for one reached by its bare name under a `using static`, and for one the app declares.

#### Scenario: decimal's constants

- **WHEN** `decimal.MaxValue.ToString()` and `decimal.One + "|" + decimal.Zero + "|" + decimal.MinusOne` run
- **THEN** they answer "79228162514264337593543950335" and "1|0|-1", as in .NET, with the type spelled `decimal` or `Decimal`

#### Scenario: A long constant meets a long

- **WHEN** `long t = 20000000;` divides by `TimeSpan.TicksPerSecond`
- **THEN** it answers 2, as in .NET, where the constant as a number threw a TypeError

#### Scenario: A constant the app declares

- **WHEN** a record declares `public const decimal Rate = 1.50m;` and `(Money.Rate * 2).ToString()` runs
- **THEN** it answers "3.00", as in .NET

### Requirement: A decimal literal is its value

A decimal literal SHALL be written from the value the parser read, so a digit separator never reaches the runtime's parser.

#### Scenario: A digit separator

- **WHEN** `(1_000.5m).ToString()` runs
- **THEN** it answers "1000.5", as in .NET

### Requirement: A skipped parameter's default is its value in the parameter's type

When a named argument skips a parameter, the default filled in SHALL be the constant written in the parameter's type, for a creation and for an invocation alike.

#### Scenario: A decimal default

- **WHEN** `decimal F(decimal a = 1.5m, int b = 0) => a * b;` is called as `F(b: 2)`
- **THEN** it answers "3.0", as in .NET

#### Scenario: A char default and a string default with a quote

- **WHEN** `new string(c, n)` takes `char c = 'x'` skipped, and a method returns `string s = "it's"` skipped
- **THEN** they answer "xxx" and "it's2", as in .NET, where the char was a bare identifier and the quote broke the literal
