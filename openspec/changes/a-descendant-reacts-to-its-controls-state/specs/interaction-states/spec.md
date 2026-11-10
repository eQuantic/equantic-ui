# Spec Delta

## ADDED Requirements

### Requirement: A hover inside a control is the control's

A box inside an enabled `Pressable` SHALL apply its `Hover` diff while that control is hovered, on the
web and on Photon, wherever the pointer is over the control, and a box of a control nested in it SHALL
react to its own control. A box outside every control SHALL apply its `Hover` diff while it is itself
hovered.

#### Scenario: An arrow that nudges when its button is hovered

- **WHEN** a `Pressable` holds a label and an arrow whose box declares
  `Hover = new StyleDiff { Transform = Transform2D.Translate(2, 0) }`, and the pointer is over the
  label
- **THEN** both targets draw the arrow translated two units right

#### Scenario: A control nested in a hovered control

- **WHEN** a hovered row holds a nested control whose box declares a `Hover` diff, and the pointer is
  over the row outside the nested control
- **THEN** neither target applies the nested control's `Hover` diff

### Requirement: A foreground descends to the text and icons that name none

A box MAY state a `Foreground`, and its `Hover`, `Focus` and `Pressed` diffs MAY state one too. A
`Text`, an `Icon` and a `Vector` that name no colour of their own SHALL be drawn in the foreground
of the nearest box above them that states one, resolved for that box's state, and in the theme's
`TextPrimary` when no box above them states one, on the web and on Photon.

#### Scenario: A label and an arrow that brighten under the pointer

- **WHEN** a `Pressable`'s surface states `Foreground = theme.TextSecondary` and
  `Hover = new StyleDiff { Foreground = theme.TextPrimary }`, and holds a `Text` and an `Icon` with no
  colour, and the control is hovered
- **THEN** both targets draw the text and the icon in `TextPrimary`, and in `TextSecondary` at rest

#### Scenario: A text that names its colour keeps it

- **WHEN** a `Text` with `Color = theme.Colors(Variant.Primary).Base` sits inside a box that states a
  foreground
- **THEN** both targets draw the text in its own colour

### Requirement: A translation can be a fraction of the box's own size

`Transform2D` SHALL carry a translation in fractions of the box's own laid-out width and height,
beside the translation in units, and both targets SHALL draw their sum.

#### Scenario: A band that starts outside its box

- **WHEN** a 200-unit-wide box declares `Transform = Transform2D.FractionalTranslate(-1.3f, 0)`
- **THEN** the web writes `translate(-130%, 0)` and Photon draws the box 260 units to the left
