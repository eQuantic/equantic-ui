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

### Requirement: Text is set in each of the nine weights

`FontWeight` SHALL name the nine weights OpenType defines: `Thin` (100), `ExtraLight` (200), `Light` (300),
`Regular` (400), `Medium` (500), `SemiBold` (600), `Bold` (700), `ExtraBold` (800) and `Black` (900). Text
set in any of them SHALL be drawn at that weight: on the web, by the server and by the runtime alike, and on
Photon, by each shell's text engine. A family that ships files SHALL be drawn from the file that sets the
weight and the slant, or, when none does, from the file a browser would choose (CSS Fonts §5.2), on the web,
on Apple and on Android, and on Apple a variable file SHALL be set at the weight on its `wght` axis. A static
face's weight SHALL be the one position its `STAT` table gives it on `wght` when it gives one,
and its weight class otherwise. On Android below API 28, which cannot be asked for a weight, the shell SHALL
draw a weight below 600 as regular and the others as bold.

#### Scenario: A light weight on the web

- **WHEN** a page sets text in `FontWeight.Light`
- **THEN** the markup the server renders and the node the runtime lowers both carry `font-weight: 300`

#### Scenario: A family that ships a file per weight, on Photon

- **WHEN** Photon on Apple measures text set in `Light` in a family that ships files at 300, 400 and 700
- **THEN** it measures as the 300 file does

#### Scenario: The system face, on Photon

- **WHEN** Photon on Apple draws the same text in the system face in each of the nine weights
- **THEN** each one draws differently from the eight others

#### Scenario: Two thin faces that share a weight class

- **WHEN** an app declares Inter's `Thin` and `ExtraLight` from Google Fonts, whose files both carry the
  weight class 250 and give 100 and 200 in their `STAT`
- **THEN** the catalog carries them at 100 and 200, and the web, Apple and Android draw each weight from its
  own file

#### Scenario: A variable file, on Apple

- **WHEN** Photon on Apple measures text set in `Bold` in a family that ships one variable file whose `wght`
  axis runs from 100 to 900
- **THEN** it measures as the file's instance at 700 does, not as its default instance

#### Scenario: A weight the family does not ship

- **WHEN** text is set in `SemiBold` in a family that ships files at 300, 400 and 700
- **THEN** the web, Apple and Android draw it from the 700 file, as a browser chooses

#### Scenario: A provider's weights

- **WHEN** an app declares `[assembly: GoogleFont("Inter", FontWeight.Light, FontWeight.Black)]`
- **THEN** the build fetches Inter's 300 and 900 files, and `Fonts.Inter` carries both
