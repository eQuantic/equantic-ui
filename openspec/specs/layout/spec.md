# layout Specification

## Purpose
How a layout follows the box it is given rather than a number fixed in points: a `Drawing` at its
parent's width, a `Positioned` child placed by fractions of its stack and shifted by fractions of its
own size, and a grid track repeated as often as it fits, each the same on the web and on Photon.

## Requirements

### Requirement: A drawing at a fill width keeps the artwork's aspect

A `Drawing` whose width is `SizeValue.Fill` and whose height is omitted SHALL be as wide as its
parent offers and as tall as that width divided by the artwork's aspect, on the web (server render
and hydration, with identical markup) and on Photon. An explicit height SHALL win over the aspect.
A drawing at a dp width SHALL lay out exactly as before.

#### Scenario: A filling drawing in a 400dp column

- **WHEN** a `Drawing` of a 480 × 832 artwork with `width: SizeValue.Fill` is laid out in a column
  400dp wide
- **THEN** its box is 400 × 693.33dp on Photon, and the web lowers it to `width: 100%` with
  `aspect-ratio: 0.5769`

#### Scenario: A dp width is unchanged

- **WHEN** a `Drawing` of a 3 : 1 artwork is given `width: 240`
- **THEN** its box is 240 × 80, as before

### Requirement: A positioned child is placed by fractions of its stack and of itself

A `Positioned` child SHALL be placed at its point offsets plus its fraction offsets times the
stack's size on that axis, and then moved by its shift times its own size, on both targets. With
neither a start nor an end (or a top nor a bottom) set, the axis SHALL keep the stack's alignment.

#### Scenario: A tooltip centred above a point

- **WHEN** a 100 × 30 child is positioned in a 400 × 800 stack with `StartFraction = 0.5`,
  `TopFraction = 0.25`, `Top = -16`, `ShiftX = -0.5` and `ShiftY = -1`
- **THEN** Photon places it at x = 150, y = 154, and the web lowers the anchor to
  `left: 50%`, `top: calc(25% - 16px)` and `transform: translate(-50%, -100%)`

#### Scenario: An end fraction

- **WHEN** a 40 × 20 child is positioned in a 400 × 800 stack with `EndFraction = 0.1`
- **THEN** Photon places its right edge 40dp from the stack's right edge (x = 320)

### Requirement: An auto-fill track repeats as often as fits

A grid whose columns are one `GridTrack.AutoFill(min, weight)` SHALL have as many columns as fit
tracks of at least `min` with the grid's gap between them (at least one), and SHALL share the
remaining width between them. A grid that combines an auto-fill track with any other track SHALL be
refused at construction.

#### Scenario: Cards in an 820dp panel

- **WHEN** a grid of `GridTrack.AutoFill(210)` with a 10dp gap is laid out 820dp wide
- **THEN** it has 3 columns of 266.67dp on Photon, and the web lowers it to
  `grid-template-columns: repeat(auto-fill, minmax(min(210px, 100%), 1fr))`

#### Scenario: Narrower than one track

- **WHEN** the same grid is laid out 150dp wide
- **THEN** it has one column 150dp wide

#### Scenario: Combined with another track

- **WHEN** a grid is constructed with `[GridTrack.AutoFill(210), GridTrack.Fixed(40)]`
- **THEN** construction throws, naming the auto-fill track as one that must stand alone
