# Spec Delta

## ADDED Requirements

### Requirement: A pressed fill is its surface's Pressed diff

A control SHALL draw a pressed fill only through the `Pressed` diff of the box it fills, on the web and
on Photon, with no other mechanism writing a background under the pressed family. The fill SHALL beat
the box's own fill, its hover's and its control's focus's, and a control whose boxes declare no
pressed background SHALL keep their own fill while pressed.

#### Scenario: A filled button pressed

- **WHEN** a primary `Button` is pressed with the pointer
- **THEN** the web writes the variant's `Pressed` colour as `background-color` under
  `.eq-pressable.eq-pressable:active` for the surface's class, with no `!important` and no custom
  property on the control, and Photon draws the surface in that colour

#### Scenario: A hovered button pressed

- **WHEN** a primary `Button` is hovered and then pressed with the pointer
- **THEN** both targets draw the surface in the variant's `Pressed` colour, not its `Hover` colour

#### Scenario: A control with no pressed fill

- **WHEN** a `Pressable` over a filled box that declares no `Pressed` diff is pressed
- **THEN** both targets draw the box in its own fill, never transparent

#### Scenario: A simulated press

- **WHEN** a primary `Button` is drawn inside `Simulated(SimulatedState.Pressed, …)`
- **THEN** both targets draw the surface in the variant's `Pressed` colour with nothing held, and the
  web writes no `eq-pressed` marker on the control
