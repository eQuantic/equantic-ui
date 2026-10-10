## ADDED Requirements

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

## MODIFIED Requirements

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
