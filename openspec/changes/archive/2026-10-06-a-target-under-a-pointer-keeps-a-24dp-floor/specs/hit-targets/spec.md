## Purpose

How large a control's target is on every target: the minimum a finger gets, the floor a pointer
keeps, and the control's own content keeping its hits inside the slop that grows it.

## ADDED Requirements

### Requirement: A target under a finger is at least the finger's minimum

Under a finger (`Density.Comfortable` on Photon, a coarse pointer on the web) every interactive
control's target SHALL be at least `Touch.MinTarget` per side, grown symmetrically around its visual
bounds, without moving anything.

#### Scenario: A label-less checkbox under a finger

- **WHEN** a Checkbox without a label is realized under `Density.Comfortable` on Photon
- **THEN** its hit region is 48 × 48, centred on its 22dp box

#### Scenario: The web under a coarse pointer

- **WHEN** a page shows a label-less Checkbox on a touch screen
- **THEN** a point 12px above its box still hits the checkbox, and a point 14px above it does not

### Requirement: A target under a pointer keeps a 24dp floor

Under a pointer (`Density.Compact` on Photon, a fine pointer on the web) a control's target SHALL be
its visual bounds, grown symmetrically to at least `Touch.MinPointerTarget` (24dp) per side, the
minimum WCAG 2.2 SC 2.5.8 asks of a target. A target already past the floor SHALL grow nothing, and
`Sizing.HitTarget(size, Density.Compact)` SHALL never answer less than the floor.

#### Scenario: A label-less checkbox under a pointer, on Photon

- **WHEN** a Checkbox without a label is realized under `Density.Compact`
- **THEN** its hit region is 24 × 24, centred on its 20dp box, where it was 20 × 20

#### Scenario: A label-less checkbox under a mouse, on the web

- **WHEN** a page shows a label-less Checkbox whose box is 22px tall under a fine pointer
- **THEN** a point 1px above its box hits the checkbox, where it hit the row around it

#### Scenario: A dense button is past the floor

- **WHEN** a Small button is realized under `Density.Compact`
- **THEN** its hit target is its own 26dp visual bounds, with no slop added

### Requirement: A control's own content keeps its hits inside the slop

The slop that grows a target SHALL answer only where the control draws nothing. A point over the
control's own content SHALL hit that content, on every pointer, so the content's hover state applies
and a control inside a Pressable takes its own press.

#### Scenario: A button's hover under a mouse

- **WHEN** the mouse rests on the centre of a Button under a fine pointer
- **THEN** the Button's own box is the element under the pointer, it matches `:hover`, and it shows its
  hover fill, where a slop over the content took the hit and the fill never showed

#### Scenario: A control inside a Pressable

- **WHEN** a Pressable wraps an IconButton and a point over the IconButton is hit-tested, under a fine
  or a coarse pointer
- **THEN** the hit lands on the IconButton's own content, not on the wrapper
