## ADDED Requirements

### Requirement: A Text takes the width its parent decides

A `Text` SHALL take the width its parent decides, where the parent decides one: a `Box` with a width,
a `Row` or a `Column` that stretches its children on that axis, and the page itself. Its lines SHALL
align across that width on every target. Its height SHALL stay the height of its lines inside a Box,
and a block stretch SHALL stop at an inline boundary, where the text keeps its content's width.

#### Scenario: A centred line in a sized Box

- **WHEN** a `Box` 300 wide holds a single-line `Text` with `Align = TextAlignment.Center`
- **THEN** the line is drawn centred across the 300, on Photon as on the web

#### Scenario: A centred paragraph in a stretching Column

- **WHEN** a `Column` 380 wide with `Cross = CrossAlign.Stretch` holds a two-line centred `Text`
- **THEN** both lines centre across the 380, the longest one included

#### Scenario: A button's label in a sized Box

- **WHEN** a `Box` 300 wide holds a `Pressable` whose child is a centred `Text`
- **THEN** the button hugs its label, as a `button` does inside a `div`
