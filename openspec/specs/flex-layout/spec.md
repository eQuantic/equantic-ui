# flex-layout Specification

## Purpose
How a flex container shares its main axis among its children: the weight, the basis and the shrink
a `Flexible` declares, the weight a `Spacer` declares, and that every target renders the numbers the
app wrote.

## Requirements

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
without one, and `0%` for any other weight without one, and SHALL give it a minimum main size of
zero. A declared basis SHALL be written by the rule every other length is written by, `TokenCss.Px`
on the server and its twin `px` in the browser: two decimals at most, and no trailing zeros.

#### Scenario: A zero weight at a basis

- **WHEN** `Flexible(child, flex: 0, basis: 540)` is lowered by the server and by the twin
- **THEN** both write `flex: 0 1 540px`, and the component parity fixture finds the two lowered trees identical, class hashes included

#### Scenario: A zero weight without a basis

- **WHEN** `Flexible(child, flex: 0)` is lowered by the server and by the twin
- **THEN** both write `flex: 0 1 auto`

#### Scenario: A fractional basis

- **WHEN** `Flexible(child, flex: 1, basis: 540.125f)` and `Flexible(child, flex: 1, basis: 540.12f)` are lowered by the server and by the twin, which holds the second basis as the float 540.1199951171875
- **THEN** both write `flex: 1 1 540.13px` and `flex: 1 1 540.12px`, and the component parity fixture finds the two lowered trees identical, class hashes included

#### Scenario: A zero weight's shrink factor

- **WHEN** `Flexible(child, flex: 0, basis: 540, shrink: 3)` and `Flexible(child, flex: 0, basis: 540, shrink: 1)` are lowered in a row by the server and by the twin
- **THEN** both write `flex: 0 3 540px` and `flex: 0 1 540px`, each with `min-width: 0`, and the component parity fixture finds the two lowered trees identical, class hashes included

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

### Requirement: A zero weight gives space back as the web lets it

On a line that overflows, a `Flexible` of weight zero SHALL give space back on Photon as the web's
`flex: 0 <shrink> <basis>; min-width: 0` lets it in a browser: as far as nothing, whatever the
min-content of what it holds; in proportion to its shrink times its size; and together with the
other items that shrink, whether it holds text or not. A share that would carry it below nothing
SHALL stop there, and what it could not give SHALL be shared among the others.

#### Scenario: Past its child's min-content

- **WHEN** a row 400 wide holds a box fixed at 100 and `Flexible(box 400 wide, flex: 0, basis: 540)`, or the same zero weight without a basis
- **THEN** the zero weight is 300 wide on Photon, as in Chrome, and the line ends at the row's end

#### Scenario: By its shrink times its size

- **WHEN** a row 1000 wide holds `Flexible(pane, flex: 0, basis: 540, shrink: 3)` and `Flexible(pane, flex: 0, basis: 540, shrink: 1)`
- **THEN** they are 480 and 520 wide

#### Scenario: Beside a box that fills the row

- **WHEN** a row 1000 wide holds `Flexible(box 400 wide, flex: 0, basis: 540, shrink: 3)` and a box whose width is Fill
- **THEN** they are 206.11 and 793.89 wide

#### Scenario: Holding text

- **WHEN** a row 400 wide holds `Flexible(Text("aaaa"), flex: 0, basis: 300)` and `Flexible(box 300 wide, flex: 0, basis: 300)`, and a row 300 wide holds `Flexible(Text("aaaa"), flex: 0, basis: 300, shrink: 3)` and `Flexible(Text("bbbb"), flex: 0, basis: 200, shrink: 1)`
- **THEN** the first two are 200 and 200 wide, and the second two 136.36 and 163.64
