## ADDED Requirements

### Requirement: A length is written alike on the server and in the browser

The twin's `px` SHALL write what the server's `TokenCss.Px` writes, which is `ToString("0.##")` of a float: its
seven significant digits, rounded half away from zero to two decimals, no trailing zero, the sign kept on a zero
that is not one exactly, so the atomic class named from the text is the same on both sides.

#### Scenario: Halfway between two hundredths

- **WHEN** `px` writes `1.005f`
- **THEN** it writes `1.01px`, as the server does; it wrote `1px`

#### Scenario: Past seven digits

- **WHEN** `px` writes `16777216f`
- **THEN** it writes `16777220px`, as the server does
