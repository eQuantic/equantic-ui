# Spec Delta

## ADDED Requirements

### Requirement: A number prints through a format specifier as .NET prints it

A number formatted with a standard specifier (`C`, `D`, `E`, `F`, `G`, `N`, `P`, `R`, `X`, `B`) or a
custom picture SHALL print the text .NET prints for it, in the culture in force: from the number's exact
value, a double's binary value included, with an exact half rounded to even for a double and a float
and away from zero for a decimal and an integer, at any precision .NET takes. `E` SHALL write the
mantissa's digits, the exponent's sign and an exponent of at least three digits. `X` and `B` SHALL write
a negative integer as its two's complement at its type's width. A custom picture SHALL be drawn as
.NET draws it: its sections, its text, its percent and per mille, its exponent and its scaling commas.
A specifier the value's type does not take SHALL throw .NET's `FormatException` message.

#### Scenario: The E specifier

- **WHEN** `(12345.0).ToString("E2")` and `string.Format("{0:E2}", 12345.678)` run
- **THEN** they print `1.23E+004` and `1.23E+004`

#### Scenario: A double's exact value

- **WHEN** `(0.1).ToString("F20")` runs
- **THEN** it prints `0.10000000000000000555`

#### Scenario: A half by the type

- **WHEN** `(1.25).ToString("E1")` and `125.ToString("E1")` run
- **THEN** they print `1.2E+000` and `1.3E+002`

#### Scenario: A negative integer at its width

- **WHEN** `((short)-1).ToString("X")` and `(-1).ToString("X")` run
- **THEN** they print `FFFF` and `FFFFFFFF`

#### Scenario: A picture's sections and percent

- **WHEN** `(-1.0).ToString("0.00;(0.00)")` and `(0.25).ToString("0%")` run
- **THEN** they print `(1.00)` and `25%`

#### Scenario: A precision past 100 digits

- **WHEN** `(0.1).ToString("F101")` runs
- **THEN** it prints the 55 digits of 0.1's exact value after the point, then zeros to 101 digits

#### Scenario: The culture's symbols

- **WHEN** `(1234.0).ToString("N0")` runs in es-ES and `(-42).ToString("D")` in sv-SE
- **THEN** they print `1.234` and `−42`, with sv-SE's minus sign

#### Scenario: A specifier the type does not take

- **WHEN** `(2.5).ToString("D")` runs
- **THEN** it throws `Format specifier was invalid.`
