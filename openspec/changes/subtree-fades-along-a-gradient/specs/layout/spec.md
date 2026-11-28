## ADDED Requirements

### Requirement: A decoration past its surface paints behind the content and is clipped by a clipping surface

A `Positioned` child at negative offsets, or with fractions that place it past its `Stack`, SHALL take no part in the
stack's layout and SHALL paint past the stack's edges. Placed as the stack's first child, it SHALL paint behind every
later child of the stack and above the background of the box the stack sits in, on both targets. A surface whose
`BoxStyle.Clip` is set SHALL clip it to the surface's rounded rectangle, on both targets.

#### Scenario: A slab's corner field

- **WHEN** a box with a background, a 20 dp radius and `Clip = true` holds a `Stack` whose first child is a
  `Positioned` 240 × 240 box at `top: -60, end: -60` and whose second child is the slab's text
- **THEN** on both targets the field is drawn over the slab's background and under its text, and nothing of it is
  drawn outside the slab's rounded rectangle

#### Scenario: An overhang into empty space

- **WHEN** the hero's `Stack` holds, as its first child, a `Positioned` field at `top: -64, end: -80` with
  `StartFraction = 0.08, Start = 80, BottomFraction = 0.04, Bottom = 64`
- **THEN** the field reaches 64 dp above and 80 dp past the end of the stack on both targets, its width is 92% of the
  stack's and its height 96%, and the stack's own size is what its other children give it
