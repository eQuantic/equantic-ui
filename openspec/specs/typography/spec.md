# typography Specification

## Purpose
What a type size is on every target: a size in dp, or a size that follows the window between a floor
and a ceiling, set the same way on the web, on Photon and in an email.

## Requirements

### Requirement: A type size can follow the window between a floor and a ceiling

A `TypeStyle` made with `WithFluidSize(min, percentOfWindow, max)` SHALL be set at
`percentOfWindow` percent of the window's width, never under `min` nor over `max`, with its line box
at the style's own ratio. The web SHALL write `font-size: clamp(min, Nvw, max)` and a unitless
`line-height`, identically from the server render and from hydration; Photon SHALL measure and paint
it at the window it lays out against. Where no window is known, the style SHALL be set at its
ceiling.

#### Scenario: The handoff's display heading

- **WHEN** a `Text` is set in `TypeStyle.OfSize(40, Bold).WithFluidSize(34, 4.2, 54)`
- **THEN** the web lowers it to `font-size: clamp(34px, 4.2vw, 54px)` and `line-height: 1.25`, and
  Photon lays it out 34dp tall in a 400dp window, 42dp in a 1000dp one and 54dp in a 2000dp one,
  each in a line box 1.25 times that

#### Scenario: The tracking follows the size

- **WHEN** a style 54dp tall tracked at -1.89 is made fluid between 34 and 54
- **THEN** the web writes `letter-spacing: -0.035em`, and Photon tracks it at -1.19 in a 400dp window

#### Scenario: Refused when it cannot be

- **WHEN** a fluid size is asked for with a floor that is not positive, a share of the window that
  is not positive, or a ceiling under its floor
- **THEN** the call throws, naming the argument, on both sides of the parity line

### Requirement: A size in dp gives a fluid size way

`TypeStyle.WithSize(size)` SHALL return a style whose size is `size` dp and which no longer follows
the window, and a style that never followed the window SHALL lower and lay out exactly as before.

#### Scenario: A resized fluid style

- **WHEN** `WithSize(20)` is applied to a fluid style
- **THEN** its `Fluid` is null and it is 20dp at every window

### Requirement: Text with no face of its own is set in the theme's text face

A text whose role and call site name no family SHALL be set in the theme's `TextFamily` when it is proportional, and in `MonoFamily` when it is monospaced. A family the role or the call site named SHALL stay. Photon and the email SHALL resolve it through the one merge of a role, a node and the theme. The web SHALL reach it through the document's face, which every text inherits.

#### Scenario: A brand's text face

- **WHEN** a theme answers `TextFamily` "Atkinson Hyperlegible Next", and a `Text` in a role with no family is laid out
- **THEN** Photon measures and paints it in that face, and the web sets the document's `font-family` to it

#### Scenario: A role's own face stays

- **WHEN** the same theme cuts its Heading role in "Newsreader"
- **THEN** a heading is set in "Newsreader" on every target

#### Scenario: Code keeps the code face

- **WHEN** a monospaced `Text` is laid out under a theme that answers both `TextFamily` and `MonoFamily`
- **THEN** it is set in `MonoFamily`

### Requirement: A style names its face by a family that carries its files

`TypeStyle.Family`, `IAppTheme.TextFamily` and `IAppTheme.MonoFamily` SHALL be a `FontFamily`, so a style that names a face a theme ships carries the face's files with it. The measurer, the rasterizer and the raster cache SHALL key on the family as they key on the rest of the style.

#### Scenario: A role cut in a face the theme ships

- **WHEN** a theme cuts its Heading role in a family it ships, on a machine where that family is not installed
- **THEN** Photon measures and draws a heading in that family, and the web's heading class names it and the page declares its files
