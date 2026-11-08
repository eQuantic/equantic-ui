# box-painting Specification

## Purpose
What a box paints over its fill, its inset highlight among it, the same on the web and on Photon.

## Requirements

### Requirement: A box draws its inset highlight on both targets

A box whose style sets `InsetHighlight` SHALL draw it as a hairline one point tall along the top of its padding
box, over its fill, inside its border and clipped to its corners, on Photon as on the web, where it is an inset
shadow one point down.

#### Scenario: A rounded box with no border

- **WHEN** a box with corners of 8, a blue fill and a white highlight is drawn on Photon
- **THEN** the top row of the box is white in its middle, the row below it is blue, and the corner's curve
  leaves the page's colour at the box's top-left pixel

#### Scenario: A box with a border

- **WHEN** the same box carries a border two points wide
- **THEN** the border's two rows are the border's colour, the row below them is white, and the next one is blue

#### Scenario: The web draws the same box

- **WHEN** the same box is lowered by the web realizer
- **THEN** its style carries an inset shadow one point down in the highlight's colour
