# Spec Delta

## ADDED Requirements

### Requirement: A number prints through a format specifier as .NET prints it

A number formatted with a standard specifier (`E`, `F`, `N`, `P`, `C`, `G`, `R`, `D`, `X`) or a custom
picture SHALL print the text .NET prints for it: from the number's exact value, a double's binary value
included, with an exact half rounded to even for a double and a float and away from zero for a
decimal and an integer. `E` SHALL write the mantissa's digits, the exponent's sign and an exponent of
at least three digits.

#### Scenario: The E specifier

- **WHEN** `(12345.0).ToString("E2")` and `string.Format("{0:E2}", 12345.678)` run
- **THEN** they print `1.23E+004` and `1.23E+004`

#### Scenario: A double's exact value

- **WHEN** `(0.1).ToString("F20")` runs
- **THEN** it prints `0.10000000000000000555`

#### Scenario: A half by the type

- **WHEN** `(1.25).ToString("E1")` and `125.ToString("E1")` run
- **THEN** they print `1.2E+000` and `1.3E+002`
