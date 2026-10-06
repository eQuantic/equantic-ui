# Spec Delta

## ADDED Requirements

### Requirement: A control's press shows on every box inside it

While a `Pressable` is pressed, every box inside it that declares a `Pressed` diff SHALL apply it, on
the web and on Photon, with every member applying as a hover's do and the lists it shares with the
base composed as a hover's are. A disabled control SHALL show no press.

#### Scenario: A button that presses in

- **WHEN** a `Pressable` over a box with `Pressed = new StyleDiff { Transform = Transform2D.Scale(0.985f) }`
  is pressed with the pointer
- **THEN** the web writes `transform: scale(0.985)` under `.eq-pressable.eq-pressable:active` for the
  box's class, and Photon draws the box scaled to 0.985 about its centre

#### Scenario: A simulated press

- **WHEN** the same control is drawn inside `Simulated(SimulatedState.Pressed, …)`
- **THEN** both targets draw the box pressed with nothing held

### Requirement: A control's focus shows on every box inside it

While a `Pressable` has keyboard focus, every box inside it that declares a `Focus` diff SHALL apply
it, on the web and on Photon.

#### Scenario: A focused control

- **WHEN** a `Pressable` over a box with `Focus = new StyleDiff { BorderColor = theme.FocusRing, BorderWidth = 2 }`
  takes keyboard focus
- **THEN** the web writes the border under `.eq-pressable:focus-visible` for the box's class, and
  Photon draws the box with that border

### Requirement: Pressed beats focus, and focus beats hover

When more than one state applies to a box, the press SHALL win over the focus and the focus over the
hover, member by member, on both targets, whatever order the rules reach the stylesheet in.

#### Scenario: A hovered, focused and pressed box

- **WHEN** a box declares `Hover`, `Focus` and `Pressed` diffs that each set `Opacity`, and its control
  is hovered, focused and pressed at once
- **THEN** the box shows the `Pressed` diff's opacity on both targets

### Requirement: The focus ring keeps the box's shadows

The focus ring SHALL be drawn beside a focused control's own shadows, its elevation, custom shadows
and inset highlight, on both targets, and a simulated focus SHALL draw it as a real one does.

#### Scenario: A raised button takes focus on the web

- **WHEN** a `Pressable` over a box at elevation 2 takes keyboard focus
- **THEN** the box's computed `box-shadow` lists the 2dp Surface gap, the 2dp focus ring and the
  elevation's shadow

### Requirement: A state's custom shadows glide on Photon

On Photon, a box with a `Transition` over the shadow channel SHALL glide its custom shadows between
states, position by position, a shadow that appears or leaves gliding from or to a transparent one.

#### Scenario: A glow that deepens on hover

- **WHEN** a box with `Transition = new TransitionSpec(StyleChannels.Shadow, 100)` and a custom shadow
  is hovered and its hover replaces that shadow with a larger one
- **THEN** the frame 50ms later draws a shadow between the two
