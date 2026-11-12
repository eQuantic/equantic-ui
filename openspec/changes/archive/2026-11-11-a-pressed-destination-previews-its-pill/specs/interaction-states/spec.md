# Spec Delta

## ADDED Requirements

### Requirement: A navigation destination previews its selection when pressed

The pill of every destination of `BottomNavigation` and `NavigationRail` but the selected one SHALL
be pressed into the selection's own pill, `Primary.Subtle` at 40% opacity, on the web and on Photon,
never into `SurfaceSubtle`, the colour the handoff gives the hover. The selected destination's pill
SHALL keep its colour pressed.

#### Scenario: An unselected destination pressed

- **WHEN** a destination that is not the selected one is pressed
- **THEN** both targets draw its pill in `Primary.Subtle` at 40% opacity, never in the hover's colour

#### Scenario: The selected destination pressed

- **WHEN** the selected destination is pressed
- **THEN** its pill keeps `Primary.Subtle`
