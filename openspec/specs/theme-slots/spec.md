# theme-slots Specification

## Purpose
TBD - created by archiving change a-theme-slot-reaches-a-component-in-order. Update Purpose after archive.

## Requirements

### Requirement: A component resolves its look from the theme's slots in one order

Each value a sized control draws SHALL resolve, the first that answers winning, from its family's slot
in the theme, then the theme's `Controls` slot, then the component's default, and a value that varies
by density SHALL be read at the context's density at every step. A slot SHALL be data only, every
member optional, and a theme that answers no slot SHALL draw every control as before.

#### Scenario: A theme changes the medium height

- **WHEN** an app's theme answers a `Controls` slot whose medium height is 52 comfortable and 44 compact
- **THEN** every medium control is that tall at that density, on the web and on Photon

#### Scenario: The design system's own theme

- **WHEN** a page renders under `PhotonTheme`
- **THEN** it renders byte for byte as it did before the slots, since its slots are the ladder

### Requirement: A theme is built from another with one slot replaced

`theme.With(…)` SHALL return a theme with the slots it is given and every other member of the theme it
wraps, the interface's defaults included, and a further `With` SHALL keep the slots already replaced.

#### Scenario: A theme from Photon with its own button

- **WHEN** an app selects `PhotonTheme.Instance.With(button: new ButtonTheme { MinWidth = 88 })`
- **THEN** its buttons never shrink below 88, and every other member answers what `PhotonTheme` answers

### Requirement: Every member of the theme reaches the browser

The theme's JSON SHALL carry every member of `IAppTheme`: each slot member by member when the theme
answers it, and the code palette, every kind's colour. A member of the interface with no key in it
SHALL fail the build.

#### Scenario: A theme with its own code palette

- **WHEN** a theme overrides `Code` and a code block renders again in the browser
- **THEN** it is coloured by that palette, as the server and Photon colour it
