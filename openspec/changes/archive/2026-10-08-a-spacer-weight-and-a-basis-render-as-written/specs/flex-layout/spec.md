## ADDED Requirements

### Requirement: A flexible Spacer's weight is one or more

A flexible `Spacer`'s weight SHALL be 1 or more, and a weight below 1 SHALL be refused where it is
written, never raised to 1: in C# with an `ArgumentOutOfRangeException` whose parameter is `Flex`,
through the constructor, the factory and an object initializer alike, and in the browser's twin with
a `RangeError`, through its parameter and through its trailing config. The rigid form,
`Spacer.Fixed` (`Gap` through the factories), SHALL keep a weight of zero and the length it was
given.

#### Scenario: A weight below one in C#

- **WHEN** an app builds `Spacer(0)`, `new Spacer(-3)` or `new Spacer { Flex = 0 }`
- **THEN** an `ArgumentOutOfRangeException` whose parameter is `Flex` is thrown

#### Scenario: A weight below one in the twin

- **WHEN** the browser builds `new Spacer(0)`, `new Spacer(0.5)`, `UI.spacer(0)` or `new Spacer(1, { flex: 0 })`
- **THEN** a `RangeError` is thrown

#### Scenario: The rigid form and a weight as written

- **WHEN** an app builds `Spacer.Fixed(24)` or `Gap(24)`, and a row that holds `new Spacer(3)`
- **THEN** the rigid spacer's weight is 0 and its length 24, and the row's spacer is lowered to `flex: 3 1 0%`

## MODIFIED Requirements

### Requirement: The server and the browser write the same declaration

The server render and the browser's twin SHALL lower a `Flexible` to the same `flex` declaration:
`flex: <weight> <shrink> <basis>`, the basis in px when one is declared, `auto` for a zero weight
without one, and `0%` for any other weight without one. A declared basis SHALL be written by the
rule every other length is written by, `TokenCss.Px` on the server and its twin `px` in the browser:
two decimals at most, and no trailing zeros.

#### Scenario: A zero weight at a basis

- **WHEN** `Flexible(child, flex: 0, basis: 540)` is lowered by the server and by the twin
- **THEN** both write `flex: 0 1 540px`, and the component parity fixture finds the two lowered trees identical, class hashes included

#### Scenario: A zero weight without a basis

- **WHEN** `Flexible(child, flex: 0)` is lowered by the server and by the twin
- **THEN** both write `flex: 0 1 auto`

#### Scenario: A fractional basis

- **WHEN** `Flexible(child, flex: 1, basis: 540.125f)` and `Flexible(child, flex: 1, basis: 540.12f)` are lowered by the server and by the twin, which holds the second basis as the float 540.1199951171875
- **THEN** both write `flex: 1 1 540.13px` and `flex: 1 1 540.12px`, and the component parity fixture finds the two lowered trees identical, class hashes included
