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

### Requirement: A fill's edges land on whole device pixels on Photon

Photon SHALL paint an axis-aligned fill with each edge on the nearest whole device pixel under its transform, half a pixel rounding up, as a browser snaps a box's background. An axis-aligned clip SHALL land on whole device pixels the same way, as a browser snaps an overflow clip. Two fills that share an edge on a fractional device pixel SHALL NOT let the one behind show through the one in front. A fill under a transform that turns or skews it, and a disc, SHALL be painted where they are. A fill's corners SHALL keep their antialiasing, and a fill thinner than a pixel SHALL keep one pixel.

#### Scenario: A swipeable row's action behind its surface, at 2.625 device pixels a dp

- **WHEN** a red action and a white surface, both 78 dp tall from the same top, are painted at 2.625 device pixels a dp
- **THEN** the pixel row at 204.75 px, which both edges cut, holds none of the action's red

#### Scenario: A closed swipeable row at 2.625 device pixels a dp

- **WHEN** a closed `SwipeableRow` 78 dp tall, whose box clips its action and its surface, is painted at 2.625 device pixels a dp
- **THEN** the pixel row its bottom edge cuts, over the action, holds none of the action's red

#### Scenario: A centred line at a half pixel

- **WHEN** a line's exact centre puts its left edge at 130.5 at one device pixel a dp
- **THEN** its fill's edge is painted at 131, and its layout keeps 130.5
