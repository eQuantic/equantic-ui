# box-paint Specification

## Purpose
What a box paints from its style, the same on the web and on Photon: the layers between its edge and its child
that are neither its layout nor its shadow's depth.

## Requirements

### Requirement: A box's inset highlight is its padding box's top row on both targets

A box that sets `BoxStyle.InsetHighlight` SHALL paint one pixel row of that colour along the top of its padding box. The padding box is the box past its border on each side that has one. The row SHALL stay inside the padding box's rounded corners, and SHALL paint over the box's background layers and under its border. The web SHALL lower it to an `inset 0 1px 0` box-shadow entry, and Photon SHALL fill that row clipped to the padding box's rounded rectangle.

#### Scenario: A rounded box with no border

- **WHEN** a box with a fill, a corner radius of 8 and an inset highlight is drawn on Photon
- **THEN** its top row is the highlight, the row below it is the fill, and the pixel at its corner, outside the rounded corner, is the page behind it

#### Scenario: A box with a border

- **WHEN** a box with a 2 dp border and an inset highlight is drawn on Photon
- **THEN** its top two rows are the border, the next row is the highlight, and the row below that is the fill

#### Scenario: The web

- **WHEN** either box is lowered on the web
- **THEN** its box-shadow carries `inset 0 1px 0` and the highlight's colour
