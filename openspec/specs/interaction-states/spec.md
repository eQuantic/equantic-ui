# interaction-states Specification

## Purpose
What a box looks like while an interaction state is active: the partial style (`StyleDiff`) a state
lays over the base, how each of its members applies on the web and on Photon, and how the lists a
state shares with the base (the shadows, the background layers) are written while it is active, so
a hover, a focus and the states that follow them mean the same thing on every target.

## Requirements

### Requirement: A hover applies every member of its diff on every target

While a box is hovered, every member its `Hover` diff sets SHALL apply, on the web and on Photon:
background, border colour and width, elevation, opacity, gradient, backdrop blur, transform and
custom shadows. A member the diff leaves unset SHALL keep the base value.

#### Scenario: A hover that lifts a box

- **WHEN** a box declares `Hover = new StyleDiff { Transform = Transform2D.Translate(0, -2) }` and the
  pointer is over it
- **THEN** the web writes `transform: translate(0, -2px)` under the box's `:hover` class, and Photon
  draws the box translated two units up

#### Scenario: A hover that raises the elevation, on Photon

- **WHEN** a box at elevation 1 declares `Hover = new StyleDiff { Elevation = 3 }` and is hovered
- **THEN** Photon draws elevation 3's shadow, where it drew elevation 1's

### Requirement: A state's shadow list composes the way the base's does

While a state is active, the box's shadow list SHALL be the state's elevation (or the base's), then
the state's custom shadows (or the base's), then the base's inset highlight, so a state that changes
one keeps the others.

#### Scenario: A hover that deepens the elevation of a glowing card

- **WHEN** a box with a custom glow and an inset highlight declares `Hover = new StyleDiff { Elevation = 3 }`
- **THEN** the web's `:hover` `box-shadow` lists elevation 3's shadow, the glow and the inset highlight,
  where it listed elevation 3's shadow alone

#### Scenario: A hover that drops the shadow

- **WHEN** a box whose only shadow is elevation 2 declares `Hover = new StyleDiff { Elevation = 0 }`
- **THEN** the web's `:hover` writes `box-shadow: none`, so the base's shadow does not show through

### Requirement: A state's background layers compose the way the base's do

While a state sets a gradient, the box's background SHALL be the state's gradient over the base's
glow and grid pattern, with one size per layer, as the base writes them.

#### Scenario: A hover gradient over a patterned box

- **WHEN** a box with a grid pattern declares `Hover = new StyleDiff { Gradient = … }`
- **THEN** the web's `:hover` writes the gradient and the pattern as `background-image`, and `auto`
  and the pattern's size as `background-size`

### Requirement: A shadow with no geometry is not drawn

A shadow whose offset, blur and spread are all zero SHALL draw nothing on either target, and the
web SHALL leave it out of the `box-shadow` list, on both of its producers.

#### Scenario: A list with a zero shadow in it

- **WHEN** a box declares `Shadows = [new ShadowSpec(0, 0, 0, c), new ShadowSpec(2, 4, 0, c)]`
- **THEN** the web's `box-shadow` lists the second shadow alone, and Photon draws one shadow

### Requirement: An element carries each atomic class once

An element's class attribute SHALL name each atomic class once, on both web producers, including
the one class the two spellings of a vendor-prefixed property share.

#### Scenario: A backdrop blur

- **WHEN** a box declares a backdrop blur, at rest or under a state
- **THEN** its class attribute names the blur's class once

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

#### Scenario: A disabled control, pictured or inside a pressed control

- **WHEN** a disabled `Pressable` whose box declares a `Pressed` diff is drawn inside
  `Simulated(SimulatedState.Pressed, …)`, or inside an enabled `Pressable` that is pressed
- **THEN** neither target draws the box pressed, and the web writes it no press rule

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

### Requirement: A pinned header's scrolled style applies every member on every target

While the surface a `Pinned` header pins to has scrolled past `Pinned.ScrolledThreshold` (8dp), every
member of its `ScrolledStyle` SHALL apply over `Pinned.ScrolledBase`, whose border is the hairline
along the header's bottom edge, on the web and on Photon, under the header's own `Transition`. The
surface SHALL be the nearest ScrollView around the header, or the page when there is none.

#### Scenario: The member a developer reaches for first

- **WHEN** a header's `ScrolledStyle` sets `Elevation = 2`, and the page scrolls past 8px
- **THEN** the web writes the elevation's shadow under the header's `[data-eq-scrolled]` rule, and
  Photon draws it while the scroll view is past 8dp

#### Scenario: A header in a scrolling panel

- **WHEN** a header pinned inside a ScrollView panel is on a page that scrolls, and only the page has
  scrolled
- **THEN** the header is not scrolled; when the panel scrolls past the threshold, it is

#### Scenario: The threshold

- **WHEN** the surface is at 8 and then at 9
- **THEN** the header is not scrolled at 8 and is at 9, on both targets

### Requirement: A state's border follows the edges its box draws

A state's `BorderWidth` SHALL draw along the edges the box's `BorderSides` draws, and alone SHALL draw
in the base's colour, on the web as on Photon.

#### Scenario: A hover border on a box that draws one edge

- **WHEN** a box with `BorderSides = Bottom` declares `Hover = new StyleDiff { BorderWidth = 2 }`
- **THEN** the web's hover rule writes `border-width: 0 0 2px 0` in the base's colour, never a
  shorthand that draws four edges
