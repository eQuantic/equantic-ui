## ADDED Requirements

### Requirement: A Photon app's theme is chosen in C# alone

A Photon app SHALL take its theme from `UseTheme(theme)` on its builder, and Photon's own theme when it
chooses none. The options bound from configuration SHALL reach no theme, so configuration names none
and the configuration binder walks nothing a theme holds.

#### Scenario: An app chooses a theme

- **WHEN** an app calls `UseTheme` with a Material theme, and its command line names `Photon:Theme`
- **THEN** the app's theme is the Material theme

#### Scenario: The bound options

- **WHEN** the types the bound options reach are walked as the binder walks them
- **THEN** none of them is a theme, and none is a `TypeStyle`
