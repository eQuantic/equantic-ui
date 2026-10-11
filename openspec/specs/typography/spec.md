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
