# Spec Delta

## ADDED Requirements

### Requirement: A pressed fill is its surface's Pressed diff

A control SHALL draw a pressed fill only through the `Pressed` diff of the box it fills, on the web and
on Photon, with no other mechanism writing a background under the pressed family. The fill SHALL beat
the box's own fill, its hover's and its control's focus's, and a control whose boxes declare no
pressed background SHALL keep their own fill while pressed.

#### Scenario: A filled button pressed

- **WHEN** a primary `Button` is pressed with the pointer
- **THEN** the web writes the variant's `Pressed` colour as `background-color` under the pressed
  family's selector for the surface's class, with no `!important` and no custom property on the
  control, and Photon draws the surface in that colour

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

## MODIFIED Requirements

### Requirement: A control's press shows on every box inside it

While a `Pressable` is pressed, every box inside it that declares a `Pressed` diff SHALL apply it, on
the web and on Photon, with every member applying as a hover's do and the lists it shares with the
base composed as a hover's are, up to a control nested in it: the boxes of a nested control SHALL
apply their `Pressed` diff only while that control is the one pressed, and a pressed nested control
SHALL leave the control around it unpressed. A disabled control SHALL show no press.

#### Scenario: A button that presses in

- **WHEN** a `Pressable` over a box with `Pressed = new StyleDiff { Transform = Transform2D.Scale(0.985f) }`
  is pressed with the pointer
- **THEN** the web writes `transform: scale(0.985)` under the pressed family's selector for the box's
  class, and Photon draws the box scaled to 0.985 about its centre

#### Scenario: A simulated press

- **WHEN** the same control is drawn inside `Simulated(SimulatedState.Pressed, …)`
- **THEN** both targets draw the box pressed with nothing held

#### Scenario: A disabled control, pictured or inside a pressed control

- **WHEN** a disabled `Pressable` whose box declares a `Pressed` diff is drawn inside
  `Simulated(SimulatedState.Pressed, …)`, or inside an enabled `Pressable` that is pressed
- **THEN** neither target draws the box pressed, and the web writes it no press rule

#### Scenario: A row pressed around a control

- **WHEN** a pressable row holds a pressable control, each of their boxes declaring a `Pressed` fill,
  and the row is pressed outside the control
- **THEN** both targets draw the row's box pressed and the control's box unpressed, the web through a
  pressed family that leaves out a box under a control that is not active

#### Scenario: A control pressed inside a row

- **WHEN** the control in that row is pressed
- **THEN** both targets draw the control's box pressed and the row's box unpressed, the web through a
  pressed family that selects the active control holding no active control

#### Scenario: A picture of a row around a control

- **WHEN** that row is drawn inside `Simulated(SimulatedState.Pressed | SimulatedState.Focused, …)`
- **THEN** both targets picture the row pressed and focused, ring included, and the control in it
  neither pressed nor focused, keeping its real press and focus rules on the web

### Requirement: A control's focus shows on every box inside it

While a `Pressable` has keyboard focus, every box inside it that declares a `Focus` diff SHALL apply
it, on the web and on Photon, up to a control nested in it, whose boxes SHALL apply their `Focus` diff
only while that control is the one focused.

#### Scenario: A focused control

- **WHEN** a `Pressable` over a box with `Focus = new StyleDiff { BorderColor = theme.FocusRing, BorderWidth = 2 }`
  takes keyboard focus
- **THEN** the web writes the border under the focus family's selector for the box's class, and
  Photon draws the box with that border

#### Scenario: A row focused around a control

- **WHEN** a pressable row holding a pressable control takes keyboard focus
- **THEN** both targets draw the row's box focused and the control's box unfocused, the web through a
  focus family that leaves out a box under a control inside the focused one
