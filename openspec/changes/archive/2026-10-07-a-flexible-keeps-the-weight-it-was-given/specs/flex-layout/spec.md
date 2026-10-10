## Purpose

How a flex container shares its main axis among its children: the weight, the basis and the shrink
a `Flexible` declares, and that every target renders the numbers the app wrote.

## ADDED Requirements

### Requirement: A zero weight takes no share of the leftover

A `Flexible` whose weight is zero SHALL take no share of its container's leftover main-axis space,
on the web and on Photon. It SHALL keep its basis when it declares one, and SHALL be sized by its
content when it does not, its main axis decided by that content.

#### Scenario: A zero weight beside a weight of one

- **WHEN** a row 1440 wide holds `Flexible(pane, flex: 0, basis: 540)` and `Flexible(pane, flex: 1)`
- **THEN** the first item is 540 wide and the second 900, on Photon and in a browser

#### Scenario: A zero weight without a basis

- **WHEN** a row 1440 wide holds `Flexible(box, flex: 0)` around a 200-wide box, and `Flexible(pane, flex: 1)`
- **THEN** the first item is 200 wide and the second 1240

#### Scenario: A Fill inside a zero weight without a basis

- **WHEN** a row 1440 wide holds `Flexible(pane, flex: 0)` around a pane whose width is Fill, and `Flexible(pane, flex: 1)`
- **THEN** the first item is 0 wide and the second 1440, as a browser lays out a `width: 100%` inside a `flex-basis: auto` item

#### Scenario: A wrapping row

- **WHEN** a wrapping row 1154 wide holds `Flexible(pane, flex: 0, basis: 540)` and `Flexible(pane, flex: 1, basis: 380)`
- **THEN** both sit on one line, the first 540 wide and the second 614

#### Scenario: An overflowing line

- **WHEN** a row 400 wide holds a box fixed at 100 and `Flexible(pane, flex: 0, basis: 540)`
- **THEN** the zero weight gives space back by its shrink and is 300 wide, and with `shrink: 0`, alone in the same row, it keeps all 540

#### Scenario: A hugging row

- **WHEN** a row with no width of its own, in 300 of space, holds a box fixed at 40 and `Flexible(box, flex: 0)` around a box fixed at 60
- **THEN** the row is 100 wide, because a zero weight declares no intent to fill

### Requirement: The server and the browser write the same declaration

The server render and the browser's twin SHALL lower a `Flexible` to the same `flex` declaration:
`flex: <weight> <shrink> <basis>`, the basis in px when one is declared, `auto` for a zero weight
without one, and `0%` for any other weight without one.

#### Scenario: A zero weight at a basis

- **WHEN** `Flexible(child, flex: 0, basis: 540)` is lowered by the server and by the twin
- **THEN** both write `flex: 0 1 540px`, and the component parity fixture finds the two lowered trees identical, class hashes included

#### Scenario: A zero weight without a basis

- **WHEN** `Flexible(child, flex: 0)` is lowered by the server and by the twin
- **THEN** both write `flex: 0 1 auto`

### Requirement: A number a Flexible cannot hold is refused where it is written

A negative weight, a negative shrink, and a basis that is negative or not a finite number SHALL be
refused where they are written, never clamped: in C# with an `ArgumentOutOfRangeException` naming
the property, through the constructor and through an object initializer alike, and in the browser's
twin with a `RangeError`, through its parameters and through its trailing config.

#### Scenario: A negative weight in C#

- **WHEN** an app builds `Flexible(child, flex: -1)` or `new Flexible(child) { Flex = -1 }`
- **THEN** an `ArgumentOutOfRangeException` whose parameter is `Flex` is thrown

#### Scenario: A basis that is not a size

- **WHEN** an app builds `Flexible(child, basis: float.NaN)` or `Flexible(child, basis: -20)`
- **THEN** an `ArgumentOutOfRangeException` whose parameter is `Basis` is thrown

#### Scenario: The twin's trailing config

- **WHEN** the browser builds `new Flexible(child, 1, 0, 1, { flex: -1 })`
- **THEN** a `RangeError` is thrown
